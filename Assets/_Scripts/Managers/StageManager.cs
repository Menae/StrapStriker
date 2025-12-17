using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 効果音の設定を保持するデータクラス。
/// AudioClipと音量を組み合わせて管理する。
/// </summary>
[System.Serializable]
public class SoundEffect
{
    [Tooltip("再生するオーディオクリップ")]
    public AudioClip clip;

    [Tooltip("この効果音の音量")]
    [Range(0f, 1f)]
    public float volume = 1.0f;
}

/// <summary>
/// 駅イベントのデータ構造。
/// 到着タイミング、スポーン数、駅名、背景スプライトをセットで保持する。
/// </summary>
[System.Serializable]
public class StationEvent
{
    [Tooltip("この駅に到着するまでの時間(秒)")]
    public float timeToArrival;

    [Tooltip("この駅でスポーンさせるNPCの数")]
    public int npcsToSpawn;

    [Tooltip("駅の名前(UI表示用)")]
    public string stationName;

    [Tooltip("この駅で表示する背景のスプライト")]
    public Sprite stationBackgroundSprite;
}

/// <summary>
/// ステージ全体の進行を管理するマネージャー。
/// 駅イベントの制御、混雑率の計算、慣性の管理、UIの更新を担当する。
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class StageManager : MonoBehaviour
{
    /// <summary>
    /// ゲーム全体の進行状態を表す列挙型。
    /// チュートリアル、プレイ中、ポーズ、ゲームオーバー、ステージクリアの5状態を管理する。
    /// </summary>
    public enum GameState { Tutorial, Playing, Paused, GameOver, StageClear }

    /// <summary>
    /// 現在のゲーム状態。
    /// 外部から参照可能だが、変更はこのクラス内でのみ行う。
    /// </summary>
    public GameState CurrentState { get; private set; }

    /// <summary>
    /// 現在の慣性力。
    /// 加速・減速時にプレイヤーや敵NPCに影響を与えるベクトル。
    /// </summary>
    public static Vector2 CurrentInertia { get; private set; }

    [Header("慣性設定")]
    [Tooltip("加速/減速中に働く慣性力の強さ")]
    public float inertiaForce = 10f;

    [Header("混雑率設定")]
    [Tooltip("初期の混雑率")]
    public float initialCongestionRate = 150f;

    [Tooltip("ゲームオーバーになる混雑率")]
    public float maxCongestionRate = 300f;

    [Tooltip("NPCが1人減るごとに、何%混雑率が下がるか")]
    public float rateDecreasePerNpc = 1.5f;

    [Header("ステージ設定")]
    [Tooltip("このステージの駅イベントリスト。順番に設定してください。")]
    public List<StationEvent> stationEvents;

    [Header("UI設定 (パネル類)")]
    public GameObject tutorialPanel;
    public GameObject pauseMenuPanel;
    public GameObject gameOverPanel;
    public GameObject clearPanel;

    [Header("UI設定 (テキスト類)")]
    public TextMeshProUGUI stationNameText;
    public TextMeshProUGUI congestionRateText;
    public TextMeshProUGUI defeatedNpcCountText;

    [Header("UI設定 (ステータス表示オブジェクト)")]
    [Tooltip("「減速中」の画像が含まれるゲームオブジェクト")]
    public GameObject statusObjDecelerating;

    [Tooltip("「停車中」の画像が含まれるゲームオブジェクト")]
    public GameObject statusObjStopped;

    [Tooltip("「加速中」の画像が含まれるゲームオブジェクト")]
    public GameObject statusObjAccelerating;

    [Header("駅進捗UI設定")]
    [Tooltip("駅の位置を示す黄色いボールのImageコンポーネント")]
    public Image yellowBall;

    [Tooltip("5つの駅アイコンのRectTransformを順番に設定")]
    public List<RectTransform> stationIconPositions;

    [Tooltip("YellowBallが点滅する間隔（秒）")]
    public float blinkInterval = 0.5f;

    [Header("駅到着演出の設定")]
    [Tooltip("駅到着時の効果音")]
    public SoundEffect arrivalSound;

    [Tooltip("「減速中」表示から効果音・スポーンまでの時間")]
    public float delayBeforeSpawn = 2.0f;

    [Tooltip("NPCがスポーンしてから「加速中」表示までの停車時間")]
    public float stationStopTime = 5.0f;

    [Tooltip("「加速中」表示から「走行中」表示までの時間")]
    public float accelerationTime = 3.0f;

    [Header("最終走行(クリア)の設定")]
    [Tooltip("最後の駅から終点に到着するまでの時間(秒)")]
    public float finalRunDuration = 20f;

    [Tooltip("終点到着時の効果音")]
    public SoundEffect finalArrivalSound;

    [Header("効果音設定")]
    [Tooltip("NPCを倒した時の効果音")]
    public SoundEffect npcDefeatSound;

    [Header("背景")]
    [Tooltip("シーンのParallaxControllerへの参照")]
    public ParallaxController parallaxController;

    [Header("ドア")]
    [Tooltip("シーンのDoorスクリプトへの参照")]
    public DoorManager doorController;

    private AudioSource audioSource;
    private float currentCongestionRate;
    private int defeatedNpcCount;
    private int currentStationIndex;
    private Coroutine blinkingEffectCoroutine;

    /// <summary>
    /// 列車の運行状態を表すステータス表示の種類。
    /// None、減速中、停車中、加速中の4状態を管理する。
    /// </summary>
    private enum StatusDisplayType { None, Decelerating, Stopped, Accelerating }

    /// <summary>
    /// Awakeで実行される初期化処理。
    /// AudioSourceコンポーネントの参照を取得する。
    /// </summary>
    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
    }

    /// <summary>
    /// Startで実行される初期化処理。
    /// 【修正版】いきなりチュートリアルは出さず、UIの非表示初期化と変数のセットアップだけ行う。
    /// キャリブレーションが動くよう、TimeScaleは1にしておく。
    /// </summary>
    void Start()
    {
        // まだチュートリアル状態にはしない（キャリブレーション待ち）

        // 全パネルを一旦非表示
        if (tutorialPanel != null) tutorialPanel.SetActive(false); // ← falseに変更
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (clearPanel != null) clearPanel.SetActive(false);

        SetStatusDisplay(StatusDisplayType.Stopped);

        if (stationNameText != null) stationNameText.text = "";
        CurrentInertia = Vector2.zero;

        // キャリブレーションのコルーチンを動かすために、時間は動かしておく
        Time.timeScale = 1f;

        UpdateCongestionUI();

        defeatedNpcCount = 0;
        UpdateDefeatedNpcCountUI();
    }

    /// <summary>
    /// キャリブレーション完了後に呼ばれる。
    /// チュートリアルを表示し、ゲーム時間を止めてプレイヤーの開始入力を待つ。
    /// </summary>
    public void ShowTutorial()
    {
        CurrentState = GameState.Tutorial;
        if (tutorialPanel != null) tutorialPanel.SetActive(true);

        // ここで初めて時間を止める（プレイヤーに説明を読ませるため）
        Time.timeScale = 0f;
    }

    /// <summary>
    /// 毎フレーム実行される更新処理。
    /// プレイ中またはポーズ中にEscキーでポーズ切り替えを受け付ける。
    /// </summary>
    void Update()
    {
        if (CurrentState == GameState.Playing || CurrentState == GameState.Paused)
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                TogglePause();
            }
        }
    }

    /// <summary>
    /// ステータス表示を切り替える。
    /// 減速中、停車中、加速中、非表示の4つの状態を管理し、該当するUIオブジェクトのみをアクティブにする。
    /// </summary>
    /// <param name="type">表示するステータスの種類</param>
    private void SetStatusDisplay(StatusDisplayType type)
    {
        if (statusObjDecelerating != null) statusObjDecelerating.SetActive(false);
        if (statusObjStopped != null) statusObjStopped.SetActive(false);
        if (statusObjAccelerating != null) statusObjAccelerating.SetActive(false);

        switch (type)
        {
            case StatusDisplayType.Decelerating:
                if (statusObjDecelerating != null) statusObjDecelerating.SetActive(true);
                break;
            case StatusDisplayType.Stopped:
                if (statusObjStopped != null) statusObjStopped.SetActive(true);
                break;
            case StatusDisplayType.Accelerating:
                if (statusObjAccelerating != null) statusObjAccelerating.SetActive(true);
                break;
            case StatusDisplayType.None:
                break;
        }
    }

    /// <summary>
    /// ゲームを開始する。
    /// チュートリアルパネルを非表示にし、TimeScaleを1に戻してゲームを進行させる。
    /// 初期混雑率を設定し、NPCをスポーンさせた後、ステージ進行コルーチンを開始する。
    /// </summary>
    public void StartGame()
    {
        CurrentState = GameState.Playing;
        if (tutorialPanel != null) tutorialPanel.SetActive(false);
        Time.timeScale = 1f;

        currentCongestionRate = initialCongestionRate;
        UpdateCongestionUI();
        NPCManager.instance.SpawnNPCsForCongestion(this, currentCongestionRate);

        currentStationIndex = 0;
        if (yellowBall != null && stationIconPositions.Count > currentStationIndex)
        {
            yellowBall.rectTransform.position = stationIconPositions[currentStationIndex].position;
            StartBlinking();
        }

        StartCoroutine(StageProgressionCoroutine());
    }

    /// <summary>
    /// NPCが倒されたときに呼び出される。
    /// 混雑率を減少させ、倒したNPCのカウントを増やして各UIを更新する。
    /// </summary>
    public void OnNpcDefeated()
    {
        currentCongestionRate -= rateDecreasePerNpc;
        UpdateCongestionUI();

        defeatedNpcCount++;
        UpdateDefeatedNpcCountUI();
    }

    /// <summary>
    /// 倒したNPCの数をUIに反映する。
    /// defeatedNpcCountTextがnullでない場合のみテキストを更新する。
    /// </summary>
    private void UpdateDefeatedNpcCountUI()
    {
        if (defeatedNpcCountText != null)
        {
            defeatedNpcCountText.text = defeatedNpcCount.ToString();
        }
    }

    /// <summary>
    /// 混雑率をUIに反映する。
    /// 小数点以下を切り上げて整数値として表示する。
    /// </summary>
    private void UpdateCongestionUI()
    {
        if (congestionRateText != null)
        {
            congestionRateText.text = $"{Mathf.CeilToInt(currentCongestionRate)}";
        }
    }

    /// <summary>
    /// ゲームオーバー処理を実行する。
    /// ゲーム状態をGameOverに変更し、TimeScaleを0にしてゲームを停止する。
    /// ゲームオーバーパネルを表示する。
    /// </summary>
    private void TriggerGameOver()
    {
        CurrentState = GameState.GameOver;
        Time.timeScale = 0f;
        if (gameOverPanel != null) gameOverPanel.SetActive(true);
        Debug.Log("ゲームオーバー！");
    }

    /// <summary>
    /// ステージをリトライする。
    /// TimeScaleを1に戻し、現在のシーンをフェード付きで再読み込みする。
    /// </summary>
    public void RetryStage()
    {
        Time.timeScale = 1f;
        SceneFader.instance.LoadSceneWithFade(SceneManager.GetActiveScene().name);
    }

    /// <summary>
    /// タイトル画面に戻る。
    /// TimeScaleを1に戻し、タイトルシーンをフェード付きで読み込む。
    /// </summary>
    public void ReturnToTitle()
    {
        Time.timeScale = 1f;
        SceneFader.instance.LoadSceneWithFade("TitleScreen");
    }

    /// <summary>
    /// ステージ全体の進行を制御するコルーチン。
    /// 各駅への到着と出発を順番に処理し、最後に終点への走行とクリア処理を実行する。
    /// </summary>
    private IEnumerator StageProgressionCoroutine()
    {
        SetStatusDisplay(StatusDisplayType.None);

        if (stationNameText != null)
        {
            if (stationEvents.Count > 0)
            {
                stationNameText.text = $"次は {stationEvents[0].stationName}";
            }
            else
            {
                stationNameText.text = "";
            }
        }

        CurrentInertia = Vector2.zero;

        foreach (var station in stationEvents)
        {
            yield return new WaitForSeconds(station.timeToArrival);
            yield return StartCoroutine(ArrivalSequenceCoroutine(station));

            currentStationIndex++;
            if (currentStationIndex < stationIconPositions.Count)
            {
                if (yellowBall != null)
                {
                    yellowBall.rectTransform.position = stationIconPositions[currentStationIndex].position;
                }
                StartBlinking();
            }

            if (stationNameText != null && currentStationIndex < stationEvents.Count)
            {
                stationNameText.text = $"次は {stationEvents[currentStationIndex].stationName}";
            }
        }

        SetStatusDisplay(StatusDisplayType.None);

        if (stationNameText != null) stationNameText.text = "次は 終点";
        CurrentInertia = Vector2.zero;

        yield return new WaitForSeconds(finalRunDuration);

        if (stationNameText != null) stationNameText.text = "終点 まもなく到着";
        SetStatusDisplay(StatusDisplayType.Decelerating);

        CurrentInertia = new Vector2(inertiaForce, 0);

        yield return new WaitForSeconds(delayBeforeSpawn);
        if (arrivalSound != null && arrivalSound.clip != null)
        {
            audioSource.PlayOneShot(arrivalSound.clip, arrivalSound.volume);
        }
        StopBlinking();

        yield return new WaitForSeconds(1.0f);
        TriggerStageClear();
    }

    /// <summary>
    /// 駅への到着から出発までの一連のシーケンスを処理するコルーチン。
    /// 減速、停車、NPCスポーン、加速の各フェーズを順番に実行する。
    /// 混雑率が最大値を超えた場合はゲームオーバーにする。
    /// </summary>
    /// <param name="station">到着する駅のイベントデータ</param>
    private IEnumerator ArrivalSequenceCoroutine(StationEvent station)
    {
        if (parallaxController != null)
        {
            parallaxController.StartApproachingStation(station.stationBackgroundSprite);
        }

        if (stationNameText != null) stationNameText.text = $"まもなく {station.stationName}";
        SetStatusDisplay(StatusDisplayType.Decelerating);

        CurrentInertia = new Vector2(inertiaForce, 0);

        yield return new WaitForSeconds(delayBeforeSpawn);

        StopBlinking();

        if (finalArrivalSound != null && finalArrivalSound.clip != null)
        {
            audioSource.PlayOneShot(finalArrivalSound.clip, finalArrivalSound.volume);
        }

        if (doorController != null)
        {
            DoorManager.OpenAllDoors();
        }

        int spawnedCount = NPCManager.instance.SpawnNPCs(this, station.npcsToSpawn);
        currentCongestionRate += spawnedCount * rateDecreasePerNpc;
        UpdateCongestionUI();

        if (currentCongestionRate >= maxCongestionRate)
        {
            TriggerGameOver();
            yield break;
        }

        SetStatusDisplay(StatusDisplayType.Stopped);
        CurrentInertia = Vector2.zero;

        yield return new WaitForSeconds(stationStopTime);

        if (parallaxController != null)
        {
            parallaxController.DepartFromStation();
        }

        SetStatusDisplay(StatusDisplayType.Accelerating);

        CurrentInertia = new Vector2(-inertiaForce, 0);

        yield return new WaitForSeconds(accelerationTime);

        SetStatusDisplay(StatusDisplayType.None);
        CurrentInertia = Vector2.zero;
    }

    /// <summary>
    /// ステージクリア処理を実行する。
    /// ゲーム状態をStageCllearに変更し、停車中表示に切り替えてクリアパネルを表示する。
    /// </summary>
    private void TriggerStageClear()
    {
        CurrentState = GameState.StageClear;

        SetStatusDisplay(StatusDisplayType.Stopped);

        if (stationNameText != null) stationNameText.text = "";
        if (clearPanel != null) clearPanel.SetActive(true);
        Debug.Log("ステージクリア！");
    }

    /// <summary>
    /// ポーズ状態を切り替える。
    /// ポーズ中であればゲームを再開し、プレイ中であればポーズする。
    /// TimeScaleとポーズメニューパネルの表示状態を適切に更新する。
    /// </summary>
    public void TogglePause()
    {
        if (CurrentState == GameState.Paused)
        {
            CurrentState = GameState.Playing;
            if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
            Time.timeScale = 1f;
        }
        else
        {
            CurrentState = GameState.Paused;
            if (pauseMenuPanel != null) pauseMenuPanel.SetActive(true);
            Time.timeScale = 0f;
        }
    }

    /// <summary>
    /// 黄色いボールの点滅を開始する。
    /// すでに点滅中の場合は既存のコルーチンを停止してから新しく開始する。
    /// </summary>
    private void StartBlinking()
    {
        if (blinkingEffectCoroutine != null)
        {
            StopCoroutine(blinkingEffectCoroutine);
        }
        blinkingEffectCoroutine = StartCoroutine(BlinkingCoroutine());
    }

    /// <summary>
    /// 黄色いボールの点滅を停止する。
    /// コルーチンを停止し、ボールを表示状態にする。
    /// </summary>
    private void StopBlinking()
    {
        if (blinkingEffectCoroutine != null)
        {
            StopCoroutine(blinkingEffectCoroutine);
            blinkingEffectCoroutine = null;
        }
        if (yellowBall != null)
        {
            yellowBall.enabled = true;
        }
    }

    /// <summary>
    /// NPC撃破時の効果音を再生する。
    /// NPCManagerなど外部システムから呼び出される想定。
    /// </summary>
    public void PlayNpcDefeatSound()
    {
        if (npcDefeatSound != null && npcDefeatSound.clip != null)
        {
            audioSource.PlayOneShot(npcDefeatSound.clip, npcDefeatSound.volume);
        }
    }

    /// <summary>
    /// 黄色いボールを点滅させるコルーチン。
    /// 指定された間隔で表示と非表示を繰り返す。
    /// </summary>
    private IEnumerator BlinkingCoroutine()
    {
        while (true)
        {
            if (yellowBall != null)
            {
                yellowBall.enabled = false;
            }
            yield return new WaitForSeconds(blinkInterval);
            if (yellowBall != null)
            {
                yellowBall.enabled = true;
            }
            yield return new WaitForSeconds(blinkInterval);
        }
    }
}