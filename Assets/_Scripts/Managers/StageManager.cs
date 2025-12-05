using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

[System.Serializable]
public class SoundEffect
{
    [Tooltip("再生するオーディオクリップ")]
    public AudioClip clip;
    [Tooltip("この効果音の音量")]
    [Range(0f, 1f)]
    public float volume = 1.0f;
}

// 駅イベントのデータ構造
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

[RequireComponent(typeof(AudioSource))]
public class StageManager : MonoBehaviour
{
    // ゲーム全体の進行状態を管理
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

    // 表示状態管理用のEnum
    private enum StatusDisplayType { None, Decelerating, Stopped, Accelerating }

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
    }

    void Start()
    {
        CurrentState = GameState.Tutorial;
        if (tutorialPanel != null) tutorialPanel.SetActive(true);
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (clearPanel != null) clearPanel.SetActive(false);

        // 初期状態は「停車中」
        SetStatusDisplay(StatusDisplayType.Stopped);

        if (stationNameText != null) stationNameText.text = "";
        CurrentInertia = Vector2.zero;
        Time.timeScale = 0f;
        UpdateCongestionUI();

        defeatedNpcCount = 0;
        UpdateDefeatedNpcCountUI();
    }

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

    // ★ ステータス表示を切り替えるメソッド
    private void SetStatusDisplay(StatusDisplayType type)
    {
        // 全て一旦非表示にする（nullチェック付き）
        if (statusObjDecelerating != null) statusObjDecelerating.SetActive(false);
        if (statusObjStopped != null) statusObjStopped.SetActive(false);
        if (statusObjAccelerating != null) statusObjAccelerating.SetActive(false);

        // 指定されたものだけ有効化する
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
                // 何も表示しない（すべてfalseのまま）
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
        // 混雑率を更新
        currentCongestionRate -= rateDecreasePerNpc;
        UpdateCongestionUI();

        // 倒したNPCの数を加算してUIを更新
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
        // 走行中：何も表示しない -> 「次は～」を表示に変更
        SetStatusDisplay(StatusDisplayType.None);

        if (stationNameText != null)
        {
            // 最初の駅がある場合、開始直後から表示する
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

            // 次の駅へ向かうため、表示を更新する
            if (stationNameText != null && currentStationIndex < stationEvents.Count)
            {
                stationNameText.text = $"次は {stationEvents[currentStationIndex].stationName}";
            }
        }

        // 最終走行
        SetStatusDisplay(StatusDisplayType.None);

        if (stationNameText != null) stationNameText.text = "次は 終点";
        CurrentInertia = Vector2.zero;

        yield return new WaitForSeconds(finalRunDuration);

        // 終点への減速：減速中表示
        if (stationNameText != null) stationNameText.text = "終点 まもなく到着";
        SetStatusDisplay(StatusDisplayType.Decelerating);

        // 減速中は、慣性力は進行方向(右、プラス)に働きます
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

    private IEnumerator ArrivalSequenceCoroutine(StationEvent station)
    {
        // 駅接近開始
        if (parallaxController != null)
        {
            parallaxController.StartApproachingStation(station.stationBackgroundSprite);
        }

        // 減速中
        if (stationNameText != null) stationNameText.text = $"まもなく {station.stationName}";
        SetStatusDisplay(StatusDisplayType.Decelerating);

        // 減速中は、慣性力は進行方向(右、プラス)に働きます
        CurrentInertia = new Vector2(inertiaForce, 0);

        yield return new WaitForSeconds(delayBeforeSpawn);

        // 到着
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

        // 停車中
        SetStatusDisplay(StatusDisplayType.Stopped);
        CurrentInertia = Vector2.zero;

        yield return new WaitForSeconds(stationStopTime);

        // 駅出発
        if (parallaxController != null)
        {
            parallaxController.DepartFromStation();
        }

        SetStatusDisplay(StatusDisplayType.Accelerating);

        // 加速中は、慣性力は後方(左、マイナス)に働きます
        CurrentInertia = new Vector2(-inertiaForce, 0);

        yield return new WaitForSeconds(accelerationTime);

        // 走行中：何も表示しない
        SetStatusDisplay(StatusDisplayType.None);
        CurrentInertia = Vector2.zero;
    }

    private void TriggerStageClear()
    {
        CurrentState = GameState.StageClear;

        // ステージクリア（終点到着）なので「停車中」を表示する
        SetStatusDisplay(StatusDisplayType.Stopped);

        if (stationNameText != null) stationNameText.text = "";
        if (clearPanel != null) clearPanel.SetActive(true);
        Debug.Log("ステージクリア！");
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