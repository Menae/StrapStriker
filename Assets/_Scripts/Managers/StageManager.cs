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

[System.Serializable]
public struct SpawnWave
{
    public NPCType npcType;
    public int count;
    public bool spawnAtDoor;
}

[System.Serializable]
public class StationEvent
{
    public float timeToArrival;
    public List<SpawnWave> spawnWaves;

    [Header("駅名設定")]
    [Tooltip("日本語の駅名")]
    public string stationNameJP;
    [Tooltip("英語の駅名")]
    public string stationNameEN;

    public Sprite stationBackgroundSprite;
}

[System.Serializable]
public struct CongestionBonusSettings
{
    public float minRate;
    public float maxRate;
    public int startStage;
}

[System.Serializable]
public struct DefeatedBonusSettings
{
    public int minDefeatedCount;
    public int startStage;
}

/// <summary>
/// ステージ進行管理マネージャー。
/// 混雑率の内部計算、言語別の駅名/ステータス表示、共通パネルの制御を一元管理する。
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class StageManager : MonoBehaviour
{
    public enum GameState { Tutorial, Playing, Paused, GameOver, StageClear }
    public GameState CurrentState { get; private set; }
    public static Vector2 CurrentInertia { get; private set; }

    [Header("■ 慣性設定")]
    public float inertiaForce = 10f;

    [Header("■ 混雑率設定 (ロジック)")]
    [Tooltip("初期の混雑率")]
    public float initialCongestionRate = 100f;
    [Tooltip("ゲームオーバーになる混雑率（ゲージ最大値）")]
    public float maxCongestionRate = 300f;
    [Tooltip("NPCが1人減るごとに減少する混雑率")]
    public float rateDecreasePerNpc = 1.5f;

    public float CurrentCongestionRate => currentCongestionRate;

    [Header("▼ オーバーロード設定")]
    public float overloadCongestionThreshold = 250f;
    public float overloadDurationToGameOver = 5.0f;

    [Header("■ 勢いボーナス設定")]
    public List<CongestionBonusSettings> congestionBonusList;
    public List<DefeatedBonusSettings> defeatedBonusList;

    [Header("■ ステージイベント設定")]
    public List<StationEvent> stationEvents;

    [Header("■ 混雑率ゲージUI")]
    [Tooltip("日本語版のゲージ中身（Image Type: Filledにしておくこと）")]
    public Image congestionFillJP;
    [Tooltip("英語版のゲージ中身")]
    public Image congestionFillEN;

    [Header("■ UIコンテナ (言語別・常時表示要素)")]
    public GameObject uiContainerJP;
    public GameObject uiContainerEN;

    [Header("■ パネル (言語共通)")]
    public GameObject tutorialPanel;
    public GameObject pauseMenuPanel;
    public GameObject gameOverPanel;
    public GameObject clearPanel;

    [Header("■ 駅名表示 (言語別TMP)")]
    public TextMeshProUGUI stationNameTextJP;
    public TextMeshProUGUI stationNameTextEN;

    [Header("■ ステータス表示 (日本語)")]
    public GameObject statusJP_Decelerating;
    public GameObject statusJP_Stopped;
    public GameObject statusJP_Accelerating;

    [Header("■ ステータス表示 (英語)")]
    public GameObject statusEN_Decelerating;
    public GameObject statusEN_Stopped;
    public GameObject statusEN_Accelerating;

    [Header("■ 共通UI")]
    public TextMeshProUGUI defeatedNpcCountText;

    [Header("■ 駅進捗UI")]
    public Image yellowBall;
    public List<RectTransform> stationIconPositions;
    public float blinkInterval = 0.5f;

    [Header("■ 演出・効果音設定")]
    public SoundEffect arrivalSound;
    public float delayBeforeSpawn = 2.0f;
    public float stationStopTime = 5.0f;
    public float accelerationTime = 3.0f;
    public float finalRunDuration = 20f;
    public SoundEffect finalArrivalSound;
    public SoundEffect npcDefeatSound;

    [Header("■ 外部参照")]
    public ResultUIController resultUI;
    public ParallaxController parallaxController;
    public DoorManager doorController;
    public GameOverUIController gameOverUI;

    [Header("■ 評価基準")]
    public int rankThreshold2Stars = 10;
    public int rankThreshold3Stars = 20;

    [Header("■ デバッグ設定")]
    [Tooltip("【開発用】タイトルを経由せず起動した時、英語モードとして扱うか")]
    [SerializeField] private bool debugForceEnglish = false;

    // 内部変数
    private AudioSource audioSource;
    private float currentCongestionRate;
    private float currentOverloadTimer = 0f;
    private int defeatedNpcCount;
    private int currentStationIndex;
    private Coroutine blinkingEffectCoroutine;
    private bool isEnglishMode = false;

    private enum StatusDisplayType { None, Decelerating, Stopped, Accelerating }

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
    }

    void Start()
    {
        // 言語設定の取得と適用
        ApplyLanguageSettings();

        // パネル初期化
        HideAllPanels();

        // ステータス表示初期化
        SetStatusDisplay(StatusDisplayType.Stopped);

        // 駅名初期化
        UpdateStationNameUI("");

        CurrentInertia = Vector2.zero;
        Time.timeScale = 1f;

        defeatedNpcCount = 0;
        UpdateDefeatedNpcCountUI();
    }

    /// <summary>
    /// LocalizationManagerの設定に基づき、言語フラグを設定。
    /// 基本UIコンテナと駅名TMPのActive切り替えを行う。
    /// </summary>
    private void ApplyLanguageSettings()
    {
        // 1. まずは正規の手順で言語状態を取得
        isEnglishMode = false;

        if (LocalizationManager.Instance != null)
        {
            isEnglishMode = (LocalizationManager.Instance.CurrentLanguage == Language.English);
        }

        // 2. エディタ実行時かつデバッグフラグがONなら、問答無用で上書きする
#if UNITY_EDITOR
        if (debugForceEnglish)
        {
            isEnglishMode = true;
            // 混乱を防ぐため、強制上書きしたことをログに出す
            Debug.Log("<color=yellow>Debug:</color> Inspector設定により英語モードを強制適用しました");
        }
#endif

        // 3. 決定した言語モードに基づいて表示を切り替え

        // 常時表示UI（コンテナ）の切り替え
        if (uiContainerJP != null) uiContainerJP.SetActive(!isEnglishMode);
        if (uiContainerEN != null) uiContainerEN.SetActive(isEnglishMode);

        // 駅名TMPの表示切り替え
        if (stationNameTextJP != null) stationNameTextJP.gameObject.SetActive(!isEnglishMode);
        if (stationNameTextEN != null) stationNameTextEN.gameObject.SetActive(isEnglishMode);
    }

    private void HideAllPanels()
    {
        if (tutorialPanel != null) tutorialPanel.SetActive(false);
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (clearPanel != null) clearPanel.SetActive(false);
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
            CheckOverloadStatus();

            UpdateCongestionGauge();
        }

        if (CurrentState == GameState.Playing || CurrentState == GameState.Paused)
        {
            if (Input.GetKeyDown(KeyCode.Escape)) TogglePause();
        }
    }

    /// <summary>
    /// 混雑率と撃破数に基づいて開始ステージボーナスを計算する。
    /// </summary>
    public int GetCalculatedStartStage()
    {
        int congestionBonus = 0;
        int defeatedBonus = 0;

        // 1. 混雑率ボーナス
        foreach (var setting in congestionBonusList)
        {
            if (currentCongestionRate >= setting.minRate && currentCongestionRate < setting.maxRate)
            {
                congestionBonus = setting.startStage;
                break;
            }
        }

        // 2. 撃破数ボーナス
        foreach (var setting in defeatedBonusList)
        {
            if (defeatedNpcCount >= setting.minDefeatedCount)
            {
                defeatedBonus = Mathf.Max(defeatedBonus, setting.startStage);
            }
        }

        return Mathf.Max(congestionBonus, defeatedBonus);
    }

    /// <summary>
    /// 混雑率オーバーロード監視ロジック。
    /// </summary>
    private void CheckOverloadStatus()
    {
        if (currentCongestionRate >= overloadCongestionThreshold)
        {
            currentOverloadTimer += Time.deltaTime;
            if (currentOverloadTimer >= overloadDurationToGameOver)
            {
                TriggerGameOver();
            }
        }
        else
        {
            if (currentOverloadTimer > 0f)
            {
                currentOverloadTimer = 0f;
            }
        }
    }

    /// <summary>
    /// 運行ステータス表示の更新。
    /// 現在の言語モードに応じて、日本語版または英語版の画像を表示する。
    /// </summary>
    private void SetStatusDisplay(StatusDisplayType type)
    {
        // 全て一旦非表示
        if (statusJP_Decelerating != null) statusJP_Decelerating.SetActive(false);
        if (statusJP_Stopped != null) statusJP_Stopped.SetActive(false);
        if (statusJP_Accelerating != null) statusJP_Accelerating.SetActive(false);

        if (statusEN_Decelerating != null) statusEN_Decelerating.SetActive(false);
        if (statusEN_Stopped != null) statusEN_Stopped.SetActive(false);
        if (statusEN_Accelerating != null) statusEN_Accelerating.SetActive(false);

        // 現在の言語に合わせて適切なオブジェクトを表示
        switch (type)
        {
            case StatusDisplayType.Decelerating:
                if (isEnglishMode)
                {
                    if (statusEN_Decelerating != null) statusEN_Decelerating.SetActive(true);
                }
                else
                {
                    if (statusJP_Decelerating != null) statusJP_Decelerating.SetActive(true);
                }
                break;

            case StatusDisplayType.Stopped:
                if (isEnglishMode)
                {
                    if (statusEN_Stopped != null) statusEN_Stopped.SetActive(true);
                }
                else
                {
                    if (statusJP_Stopped != null) statusJP_Stopped.SetActive(true);
                }
                break;

            case StatusDisplayType.Accelerating:
                if (isEnglishMode)
                {
                    if (statusEN_Accelerating != null) statusEN_Accelerating.SetActive(true);
                }
                else
                {
                    if (statusJP_Accelerating != null) statusJP_Accelerating.SetActive(true);
                }
                break;
        }
    }

    public void StartGame()
    {
        CurrentState = GameState.Playing;
        HideAllPanels();
        Time.timeScale = 1f;

        currentCongestionRate = initialCongestionRate;
        // 初期スポーン（混雑率に基づく）
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
        // 撃破時は混雑率を下げる
        currentCongestionRate = Mathf.Max(0, currentCongestionRate - rateDecreasePerNpc);

        defeatedNpcCount++;
        UpdateDefeatedNpcCountUI();
    }

    private void UpdateDefeatedNpcCountUI()
    {
        if (defeatedNpcCountText != null)
            defeatedNpcCountText.text = defeatedNpcCount.ToString();
    }

    /// <summary>
    /// 駅名表示を更新する。
    /// 現在の言語設定に関わらず両方のTMPにテキストを流し込む（Active状態で表示制御）。
    /// </summary>
    private void UpdateStationNameUI(string jpName, string enName = "")
    {
        // 日本語TMPへの反映
        if (stationNameTextJP != null) stationNameTextJP.text = jpName;

        // 英語TMPへの反映（英語名が指定なければ日本語名を入れる）
        if (stationNameTextEN != null)
        {
            stationNameTextEN.text = string.IsNullOrEmpty(enName) ? jpName : enName;
        }
    }

    /// <summary>
    /// ゲームオーバー時の処理。
    /// 診断ログを追加し、なぜUIコントローラーが動かないかを可視化します。
    /// </summary>
    private void TriggerGameOver()
    {
        CurrentState = GameState.GameOver;
        Time.timeScale = 0f;

        // ここで接続チェック
        if (gameOverUI != null)
        {
            Debug.Log("StageManager: <color=green>Connection OK.</color> Calling GameOverUI.ShowGameOver()...");
            gameOverUI.ShowGameOver();
        }
        else
        {
            Debug.LogError("StageManager: <color=red>GameOverUI is NOT assigned in Inspector!</color> Using fallback legacy panel.");

            // 予備のパネル表示（これしか動いていない可能性が高い）
            if (gameOverPanel != null)
            {
                gameOverPanel.SetActive(true);
            }
        }
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

    // --- メイン進行ループ ---

    private IEnumerator StageProgressionCoroutine()
    {
        SetStatusDisplay(StatusDisplayType.None);

        // 最初の駅名表示
        if (stationEvents.Count > 0)
        {
            var first = stationEvents[0];
            UpdateStationNameUI($"次は {first.stationNameJP}", $"NEXT: {first.stationNameEN}");
        }
        else
        {
            UpdateStationNameUI("");
        }

        CurrentInertia = Vector2.zero;

        // 各駅への進行処理
        foreach (var station in stationEvents)
        {
            yield return new WaitForSeconds(station.timeToArrival);
            yield return StartCoroutine(ArrivalSequenceCoroutine(station));

            currentStationIndex++;
            if (currentStationIndex < stationIconPositions.Count)
            {
                if (yellowBall != null)
                    yellowBall.rectTransform.position = stationIconPositions[currentStationIndex].position;
                StartBlinking();
            }

            // 次の駅名更新
            if (currentStationIndex < stationEvents.Count)
            {
                var next = stationEvents[currentStationIndex];
                UpdateStationNameUI($"次は {next.stationNameJP}", $"NEXT: {next.stationNameEN}");
            }
        }

        // 終点へのアプローチ
        SetStatusDisplay(StatusDisplayType.None);
        UpdateStationNameUI("次は 終点", "NEXT: TERMINAL");
        CurrentInertia = Vector2.zero;

        yield return new WaitForSeconds(finalRunDuration);

        UpdateStationNameUI("終点 まもなく到着", "ARRIVING AT TERMINAL");
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

    private IEnumerator ArrivalSequenceCoroutine(StationEvent station)
    {
        if (parallaxController != null)
            parallaxController.StartApproachingStation(station.stationBackgroundSprite);

        UpdateStationNameUI($"まもなく {station.stationNameJP}", $"ARRIVING AT {station.stationNameEN}");
        SetStatusDisplay(StatusDisplayType.Decelerating);
        CurrentInertia = new Vector2(inertiaForce, 0);

        yield return new WaitForSeconds(delayBeforeSpawn);

        StopBlinking();
        if (finalArrivalSound != null && finalArrivalSound.clip != null)
        {
            audioSource.PlayOneShot(finalArrivalSound.clip, finalArrivalSound.volume);
        }

        if (doorController != null) DoorManager.OpenAllDoors();

        // スポーン処理 & 混雑率加算
        int totalSpawned = 0;
        if (station.spawnWaves != null)
        {
            foreach (var wave in station.spawnWaves)
            {
                int c = NPCManager.instance.SpawnSpecificNPCs(this, wave.npcType, wave.count, wave.spawnAtDoor);
                totalSpawned += c;
            }
        }

        currentCongestionRate += totalSpawned * rateDecreasePerNpc; // レート換算で増加させる

        // ゲームオーバー判定
        if (currentCongestionRate >= maxCongestionRate)
        {
            TriggerGameOver();
            yield break;
        }

        SetStatusDisplay(StatusDisplayType.Stopped);
        CurrentInertia = Vector2.zero;

        yield return new WaitForSeconds(stationStopTime);

        if (parallaxController != null) parallaxController.DepartFromStation();

        SetStatusDisplay(StatusDisplayType.Accelerating);
        CurrentInertia = new Vector2(-inertiaForce, 0);

        yield return new WaitForSeconds(accelerationTime);

        SetStatusDisplay(StatusDisplayType.None);
        CurrentInertia = Vector2.zero;
    }

    private void TriggerStageClear()
    {
        CurrentState = GameState.StageClear;
        SetStatusDisplay(StatusDisplayType.Stopped);
        UpdateStationNameUI("");

        Debug.Log("ステージクリア！");

        int starCount = 1;
        if (defeatedNpcCount >= rankThreshold3Stars) starCount = 3;
        else if (defeatedNpcCount >= rankThreshold2Stars) starCount = 2;

        if (resultUI != null)
        {
            resultUI.ShowResult(defeatedNpcCount, starCount);
        }
        else
        {
            if (clearPanel != null) clearPanel.SetActive(true);
        }
    }

    /// <summary>
    /// 現在の混雑率に基づいてゲージの表示（FillAmount）を更新する。
    /// </summary>
    private void UpdateCongestionGauge()
    {
        // 0.0 〜 1.0 の割合を計算
        float fillRatio = Mathf.Clamp01(currentCongestionRate / maxCongestionRate);

        if (isEnglishMode)
        {
            if (congestionFillEN != null)
            {
                congestionFillEN.fillAmount = fillRatio;
            }
        }
        else
        {
            if (congestionFillJP != null)
            {
                congestionFillJP.fillAmount = fillRatio;
            }
        }
    }

    public void TogglePause()
    {
        if (CurrentState == GameState.Paused)
        {
            CurrentState = GameState.Playing;
            HideAllPanels();
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
        if (blinkingEffectCoroutine != null) StopCoroutine(blinkingEffectCoroutine);
        blinkingEffectCoroutine = StartCoroutine(BlinkingCoroutine());
    }

    private void StopBlinking()
    {
        if (blinkingEffectCoroutine != null)
        {
            StopCoroutine(blinkingEffectCoroutine);
            blinkingEffectCoroutine = null;
        }
        if (yellowBall != null) yellowBall.enabled = true;
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
            if (yellowBall != null) yellowBall.enabled = false;
            yield return new WaitForSeconds(blinkInterval);
            if (yellowBall != null) yellowBall.enabled = true;
            yield return new WaitForSeconds(blinkInterval);
        }
    }
}