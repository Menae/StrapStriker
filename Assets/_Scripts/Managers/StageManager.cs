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
/// どの種類の敵を何体、どこに出すかの定義。
/// </summary>
[System.Serializable]
public struct SpawnWave
{
    [Tooltip("スポーンさせる敵の種類")]
    public NPCType npcType;
    [Tooltip("スポーン数")]
    public int count;
    [Tooltip("ドア付近にスポーンさせるか（falseならエリア内ランダム）")]
    public bool spawnAtDoor;
}

[System.Serializable]
public class StationEvent
{
    [Tooltip("この駅に到着するまでの時間(秒)")]
    public float timeToArrival;

    [Tooltip("この駅で発生する敵のスポーンウェーブ")]
    public List<SpawnWave> spawnWaves;

    [Tooltip("駅の名前(UI表示用)")]
    public string stationName;

    [Tooltip("この駅で表示する背景のスプライト")]
    public Sprite stationBackgroundSprite;
}

/// <summary>
/// 混雑率に応じた開始ステージボーナスの設定構造体。
/// 指定した範囲内の混雑率であれば、指定したステージからスイングを開始する。
/// </summary>
[System.Serializable]
public struct CongestionBonusSettings
{
    [Tooltip("この設定が適用される最小混雑率 (%)")]
    public float minRate;
    [Tooltip("この設定が適用される最大混雑率 (%)")]
    public float maxRate;
    [Tooltip("適用される開始ステージ")]
    public int startStage;
}

/// <summary>
/// NPC撃破数に応じた開始ステージボーナスの設定構造体。
/// 指定した数以上倒していれば、指定したステージからスイングを開始する。
/// </summary>
[System.Serializable]
public struct DefeatedBonusSettings
{
    [Tooltip("この設定が適用される最低撃破数")]
    public int minDefeatedCount;
    [Tooltip("適用される開始ステージ")]
    public int startStage;
}

/// <summary>
/// ステージ全体の進行を管理するマネージャー。
/// 駅イベントの制御、混雑率の計算、慣性の管理、UIの更新に加え、
/// プレイヤーへの「勢いボーナス」の計算やオーバーロード（混雑限界）の監視を担当する。
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class StageManager : MonoBehaviour
{
    /// <summary>
    /// ゲーム全体の進行状態を表す列挙型。
    /// </summary>
    public enum GameState { Tutorial, Playing, Paused, GameOver, StageClear }

    public GameState CurrentState { get; private set; }
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

    [Header("▼ 勢いボーナス設定 (混雑率)")]
    [Tooltip("混雑率に応じた開始ステージボーナスのリスト。上から順に評価されるため、範囲が被らないよう設定推奨。")]
    public List<CongestionBonusSettings> congestionBonusList;

    [Header("▼ 勢いボーナス設定 (撃破数)")]
    [Tooltip("撃破数に応じた開始ステージボーナスのリスト。条件を満たす最大の設定が適用される。")]
    public List<DefeatedBonusSettings> defeatedBonusList;

    [Header("▼ オーバーロード設定 (混雑限界)")]
    [Tooltip("この混雑率を超えると「危険状態」とみなし、タイマーを作動させる")]
    public float overloadCongestionThreshold = 250f;
    [Tooltip("危険状態がこの秒数続くとゲームオーバーになる")]
    public float overloadDurationToGameOver = 5.0f;
    [Tooltip("現在オーバーロード中かどうかを表示するテキスト (UI用)")]
    public TextMeshProUGUI overloadWarningText;

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
    public GameObject statusObjDecelerating;
    public GameObject statusObjStopped;
    public GameObject statusObjAccelerating;

    [Header("駅進捗UI設定")]
    public Image yellowBall;
    public List<RectTransform> stationIconPositions;
    public float blinkInterval = 0.5f;

    [Header("駅到着演出の設定")]
    public SoundEffect arrivalSound;
    public float delayBeforeSpawn = 2.0f;
    public float stationStopTime = 5.0f;
    public float accelerationTime = 3.0f;

    [Header("リザルト・評価設定")]
    [Tooltip("リザルト演出を管理するスクリプト")]
    public ResultUIController resultUI;

    [Tooltip("星2を獲得するために必要な撃破数")]
    public int rankThreshold2Stars = 10;
    [Tooltip("星3を獲得するために必要な撃破数")]
    public int rankThreshold3Stars = 20;

    [Header("最終走行(クリア)の設定")]
    public float finalRunDuration = 20f;
    public SoundEffect finalArrivalSound;

    [Header("効果音設定")]
    public SoundEffect npcDefeatSound;

    [Header("背景")]
    public ParallaxController parallaxController;

    [Header("ドア")]
    public DoorManager doorController;

    private AudioSource audioSource;
    private float currentCongestionRate;
    private int defeatedNpcCount;
    private int currentStationIndex;
    private Coroutine blinkingEffectCoroutine;

    // オーバーロード監視用タイマー
    private float currentOverloadTimer = 0f;

    private enum StatusDisplayType { None, Decelerating, Stopped, Accelerating }

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
    }

    void Start()
    {
        if (tutorialPanel != null) tutorialPanel.SetActive(false);
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (clearPanel != null) clearPanel.SetActive(false);
        if (overloadWarningText != null) overloadWarningText.text = "";

        SetStatusDisplay(StatusDisplayType.Stopped);

        if (stationNameText != null) stationNameText.text = "";
        CurrentInertia = Vector2.zero;

        Time.timeScale = 1f;

        UpdateCongestionUI();

        defeatedNpcCount = 0;
        UpdateDefeatedNpcCountUI();
    }

    public void ShowTutorial()
    {
        CurrentState = GameState.Tutorial;
        if (tutorialPanel != null) tutorialPanel.SetActive(true);
        Time.timeScale = 0f;
    }

    void Update()
    {
        if (CurrentState == GameState.Playing)
        {
            // オーバーロード（混雑しすぎ）チェック
            CheckOverloadStatus();
        }

        if (CurrentState == GameState.Playing || CurrentState == GameState.Paused)
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                TogglePause();
            }
        }
    }

    /// <summary>
    /// 現在の混雑率と撃破数に基づいて、プレイヤーに付与する開始ステージを計算する。
    /// 混雑ボーナスと撃破ボーナスのうち、高い方の値を採用する。
    /// PlayerControllerからつり革を掴んだ瞬間に呼び出される想定。
    /// </summary>
    /// <returns>適用すべき開始ステージ (0以上の整数)</returns>
    public int GetCalculatedStartStage()
    {
        int congestionBonus = 0;
        int defeatedBonus = 0;

        // 1. 混雑率によるボーナス計算
        foreach (var setting in congestionBonusList)
        {
            if (currentCongestionRate >= setting.minRate && currentCongestionRate < setting.maxRate)
            {
                // 条件に合致する設定が見つかったら適用（上書き）
                congestionBonus = setting.startStage;
                // リストの下にある設定ほど優先度が高い設計にするなら break しないが、
                // 基本的には範囲指定なので最初に見つかったものを採用して break
                break;
            }
        }

        // 2. 撃破数によるボーナス計算
        // 撃破数が多いほど有利になるよう、条件を満たす最大値を探索
        foreach (var setting in defeatedBonusList)
        {
            if (defeatedNpcCount >= setting.minDefeatedCount)
            {
                // より高いボーナスがあれば更新（リストが昇順でなくても最大を取れるようにMathf.Max）
                defeatedBonus = Mathf.Max(defeatedBonus, setting.startStage);
            }
        }

        // 両方のボーナスのうち、高い方を採用して返す
        return Mathf.Max(congestionBonus, defeatedBonus);
    }

    /// <summary>
    /// 混雑率が危険域を超えているかを監視し、継続している場合はゲームオーバーにする。
    /// 警告UIの更新も行う。
    /// </summary>
    private void CheckOverloadStatus()
    {
        if (currentCongestionRate >= overloadCongestionThreshold)
        {
            currentOverloadTimer += Time.deltaTime;

            float remainingTime = Mathf.Max(0, overloadDurationToGameOver - currentOverloadTimer);

            if (overloadWarningText != null)
            {
                overloadWarningText.text = $"<color=red>DANGER! {remainingTime:F1}</color>";
                overloadWarningText.gameObject.SetActive(true);
            }

            if (currentOverloadTimer >= overloadDurationToGameOver)
            {
                Debug.Log("Overload Game Over Triggered.");
                TriggerGameOver();
            }
        }
        else
        {
            // 閾値を下回ったらタイマーリセット
            if (currentOverloadTimer > 0f)
            {
                currentOverloadTimer = 0f;
                if (overloadWarningText != null)
                {
                    overloadWarningText.gameObject.SetActive(false);
                }
            }
        }
    }

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

    public void OnNpcDefeated()
    {
        currentCongestionRate -= rateDecreasePerNpc;
        UpdateCongestionUI();

        defeatedNpcCount++;
        UpdateDefeatedNpcCountUI();
    }

    private void UpdateDefeatedNpcCountUI()
    {
        if (defeatedNpcCountText != null)
        {
            defeatedNpcCountText.text = defeatedNpcCount.ToString();
        }
    }

    private void UpdateCongestionUI()
    {
        if (congestionRateText != null)
        {
            congestionRateText.text = $"{Mathf.CeilToInt(currentCongestionRate)}";
        }
    }

    private void TriggerGameOver()
    {
        CurrentState = GameState.GameOver;
        Time.timeScale = 0f;
        if (gameOverPanel != null) gameOverPanel.SetActive(true);
        if (overloadWarningText != null) overloadWarningText.gameObject.SetActive(false);
        Debug.Log("ゲームオーバー！");
    }

    public void RetryStage()
    {
        Time.timeScale = 1f;
        SceneFader.instance.LoadSceneWithFade(SceneManager.GetActiveScene().name);
    }

    public void ReturnToTitle()
    {
        Time.timeScale = 1f;
        SceneFader.instance.LoadSceneWithFade("TitleScreen");
    }

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
    /// 減速、停車、NPCスポーン（Wave対応版）、加速の各フェーズを順番に実行する。
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

        // --- spawnWavesリストに基づいてループ処理 ---
        // 各Waveの設定に従って、指定された種類・数のNPCをスポーンさせる
        int totalSpawnedCount = 0;

        if (station.spawnWaves != null)
        {
            foreach (var wave in station.spawnWaves)
            {
                // NPCManagerに追加した SpawnSpecificNPCs メソッドを使用
                int c = NPCManager.instance.SpawnSpecificNPCs(this, wave.npcType, wave.count, wave.spawnAtDoor);
                totalSpawnedCount += c;
            }
        }

        // 混雑率の更新（スポーンした総数を使用）
        currentCongestionRate += totalSpawnedCount * rateDecreasePerNpc;
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
    /// ステージクリア処理。
    /// 撃破数に基づいて評価を計算し、リザルト画面を表示する。
    /// </summary>
    private void TriggerStageClear()
    {
        CurrentState = GameState.StageClear;

        SetStatusDisplay(StatusDisplayType.Stopped);

        if (stationNameText != null) stationNameText.text = "";

        // --- 変更: 旧パネル表示を廃止し、新しいリザルト処理へ ---
        // if (clearPanel != null) clearPanel.SetActive(true); 

        Debug.Log("ステージクリア！");

        // 星の数を計算
        int starCount = 1; // クリアすれば最低星1
        if (defeatedNpcCount >= rankThreshold3Stars)
        {
            starCount = 3;
        }
        else if (defeatedNpcCount >= rankThreshold2Stars)
        {
            starCount = 2;
        }

        // リザルトUIに表示依頼
        if (resultUI != null)
        {
            resultUI.ShowResult(defeatedNpcCount, starCount);
        }
        else
        {
            // ResultUIが設定されていない場合のフォールバック（旧パネルがあれば出す）
            if (clearPanel != null) clearPanel.SetActive(true);
        }
    }

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

    private void StartBlinking()
    {
        if (blinkingEffectCoroutine != null)
        {
            StopCoroutine(blinkingEffectCoroutine);
        }
        blinkingEffectCoroutine = StartCoroutine(BlinkingCoroutine());
    }

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

    public void PlayNpcDefeatSound()
    {
        if (npcDefeatSound != null && npcDefeatSound.clip != null)
        {
            audioSource.PlayOneShot(npcDefeatSound.clip, npcDefeatSound.volume);
        }
    }

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