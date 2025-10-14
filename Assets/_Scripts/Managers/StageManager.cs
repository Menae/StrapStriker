using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

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
}

[RequireComponent(typeof(AudioSource))] // このスクリプトにはAudioSourceが必須
public class StageManager : MonoBehaviour
{
    // ゲーム全体の進行状態を管理
    public enum GameState { Tutorial, Playing, Paused, GameOver, StageClear }
    public GameState CurrentState { get; private set; }

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

    [Header("UI設定")]
    public GameObject tutorialPanel;
    public GameObject pauseMenuPanel;
    public GameObject gameOverPanel;
    public TextMeshProUGUI statusText;
    public TextMeshProUGUI stationNameText;
    public TextMeshProUGUI congestionRateText;

    [Header("駅到着演出の設定")]
    [Tooltip("駅到着時の効果音")]
    public AudioClip arrivalSound;
    [Tooltip("「減速中」表示から効果音・スポーンまでの時間")]
    public float delayBeforeSpawn = 2.0f;
    [Tooltip("NPCがスポーンしてから「加速中」表示までの停車時間")]
    public float stationStopTime = 5.0f;
    [Tooltip("「加速中」表示から「走行中」表示までの時間")]
    public float accelerationTime = 3.0f;

    private AudioSource audioSource;
    private int currentStationIndex = 0;
    private float currentCongestionRate;

    void Awake()
    {
        // 自身のAudioSourceコンポーネントを取得
        audioSource = GetComponent<AudioSource>();
    }

    void Start()
    {
        CurrentState = GameState.Tutorial;
        if (tutorialPanel != null) tutorialPanel.SetActive(true);
        if (statusText != null) statusText.text = "停車中";
        if (stationNameText != null) stationNameText.text = "";
        Time.timeScale = 0f;
        UpdateCongestionUI();
    }

    void Update()
    {
        // プレイ中にESCキーが押されたらポーズを切り替える
        if (CurrentState == GameState.Playing || CurrentState == GameState.Paused)
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                TogglePause();
            }
        }
    }

    // チュートリアルUIのボタンから呼び出すメソッド
    public void StartGame()
    {
        CurrentState = GameState.Playing;
        if (tutorialPanel != null) tutorialPanel.SetActive(false);
        Time.timeScale = 1f;

        // 混雑率を初期化してNPCをスポーン
        currentCongestionRate = initialCongestionRate;
        UpdateCongestionUI();
        NPCManager.instance.SpawnNPCsForCongestion(this, currentCongestionRate);

        // ステージ進行のメインループを開始
        StartCoroutine(StageProgressionCoroutine());
    }

    // 混雑率を更新するための公開メソッド
    public void UpdateCongestionOnNpcDefeated()
    {
        if (CurrentState != GameState.Playing) return;

        currentCongestionRate -= rateDecreasePerNpc;
        UpdateCongestionUI();
    }

    // UIのテキストを更新する
    private void UpdateCongestionUI()
    {
        if (congestionRateText != null)
        {
            congestionRateText.text = $"{Mathf.CeilToInt(currentCongestionRate)}%";
        }
    }

    // ゲームオーバー処理
    private void TriggerGameOver()
    {
        CurrentState = GameState.GameOver;
        Time.timeScale = 0f;
        if (gameOverPanel != null) gameOverPanel.SetActive(true);
        Debug.Log("ゲームオーバー！");
    }

    // UIボタンから呼び出すメソッド
    public void RetryStage()
    {
        Time.timeScale = 1f;
        // 現在のシーンを再読み込み
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void ReturnToTitle()
    {
        Time.timeScale = 1f;
        // タイトルシーンに遷移
        SceneManager.LoadScene("TitleScreen");
    }

    // ステージ進行を管理するメインのコルーチン
    private IEnumerator StageProgressionCoroutine()
    {
        if (statusText != null) statusText.text = "走行中";
        if (stationNameText != null) stationNameText.text = ""; // 走行中は駅名表示を消す

        // 設定された駅イベントを順番に処理していく
        foreach (var station in stationEvents)
        {
            // 次の駅に到着するまで待機
            yield return new WaitForSeconds(station.timeToArrival);

            // 駅到着の演出シーケンスを開始し、それが終わるまで待つ
            yield return StartCoroutine(ArrivalSequenceCoroutine(station));
        }

        // 全ての駅を通過したらステージクリア
        CurrentState = GameState.StageClear;
        if (statusText != null) statusText.text = "終点";
        Debug.Log("ステージクリア！");
        // ここでクリアUI表示やシーン遷移などを行う
    }

    // 駅到着時の演出を管理するコルーチン
    private IEnumerator ArrivalSequenceCoroutine(StationEvent station)
    {
        // ① TMPGUIを分離して変更
        if (stationNameText != null) stationNameText.text = $"{station.stationName} まもなく到着";
        if (statusText != null) statusText.text = "減速中";

        // ② サウンド再生までの待機
        yield return new WaitForSeconds(delayBeforeSpawn);
        if (arrivalSound != null)
        {
            audioSource.PlayOneShot(arrivalSound);
        }

        // ③ NPCをスポーンさせ、その分混雑率を上げる
        int spawnedCount = NPCManager.instance.SpawnNPCs(this, station.npcsToSpawn);
        currentCongestionRate += spawnedCount * rateDecreasePerNpc;
        UpdateCongestionUI();

        // 停車時間
        yield return new WaitForSeconds(stationStopTime);

        // ④ 駅名表示を消し、状態を「加速中」に変更
        if (stationNameText != null) stationNameText.text = "";
        if (statusText != null) statusText.text = "加速中";

        // 加速時間
        yield return new WaitForSeconds(accelerationTime);

        // ⑤ 状態を「走行中」に変更
        if (statusText != null) statusText.text = "走行中";
    }

    // ポーズ機能
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
}