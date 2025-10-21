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
}

[RequireComponent(typeof(AudioSource))] // このスクリプトにはAudioSourceが必須
public class StageManager : MonoBehaviour
{
    // ゲーム全体の進行状態を管理
    public enum GameState { Tutorial, Playing, Paused, GameOver, StageClear }
    public GameState CurrentState { get; private set; }

    public static Vector2 CurrentInertia { get; private set; } // 他のスクリプトから読み取れる慣性ベクトル

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

    [Header("UI設定")]
    public GameObject tutorialPanel;
    public GameObject pauseMenuPanel;
    public GameObject gameOverPanel;
    public GameObject clearPanel;
    public TextMeshProUGUI statusText;
    public TextMeshProUGUI stationNameText;
    public TextMeshProUGUI congestionRateText;
    [Tooltip("倒したNPCの数を表示するTextMeshProコンポーネント")]
    public TextMeshProUGUI defeatedNpcCountText;

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

    private AudioSource audioSource;
    private float currentCongestionRate;
    private int defeatedNpcCount;

    void Awake()
    {
        // 自身のAudioSourceコンポーネントを取得
        audioSource = GetComponent<AudioSource>();
    }

    void Start()
    {
        CurrentState = GameState.Tutorial;
        if (tutorialPanel != null) tutorialPanel.SetActive(true);
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (clearPanel != null) clearPanel.SetActive(false);
        if (statusText != null) statusText.text = "停車中";
        if (stationNameText != null) stationNameText.text = "";
        CurrentInertia = Vector2.zero;
        Time.timeScale = 0f;
        UpdateCongestionUI();

        defeatedNpcCount = 0;
        UpdateDefeatedNpcCountUI();
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

    public void OnNpcDefeated()
    {
        // 混雑率を更新
        currentCongestionRate -= rateDecreasePerNpc;
        UpdateCongestionUI();

        // 倒したNPCの数を加算してUIを更新
        defeatedNpcCount++;
        UpdateDefeatedNpcCountUI();
    }

    // 倒したNPCの数UIを更新するメソッド
    private void UpdateDefeatedNpcCountUI()
    {
        if (defeatedNpcCountText != null)
        {
            defeatedNpcCountText.text = defeatedNpcCount.ToString();
        }
    }

    // UIのテキストを更新する
    private void UpdateCongestionUI()
    {
        if (congestionRateText != null)
        {
            congestionRateText.text = $"{Mathf.CeilToInt(currentCongestionRate)}";
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
        SceneFader.instance.LoadSceneWithFade(SceneManager.GetActiveScene().name);
    }

    public void ReturnToTitle()
    {
        Time.timeScale = 1f;
        // タイトルシーンに遷移
        SceneFader.instance.LoadSceneWithFade("TitleScreen");
    }

    // ステージ進行を管理するメインのコルーチン
    private IEnumerator StageProgressionCoroutine()
    {
        if (statusText != null) statusText.text = "走行中";
        if (stationNameText != null) stationNameText.text = "";
        CurrentInertia = Vector2.zero;

        // 設定された駅イベントを順番に処理していく
        foreach (var station in stationEvents)
        {
            // 次の駅に到着するまで待機
            yield return new WaitForSeconds(station.timeToArrival);
            // 駅到着の演出シーケンスを開始し、それが終わるまで待つ
            yield return StartCoroutine(ArrivalSequenceCoroutine(station));
        }

        // 全ての駅を通過後、終点までの最終走行を開始
        if (statusText != null) statusText.text = "走行中";
        if (stationNameText != null) stationNameText.text = "次は 終点";
        CurrentInertia = Vector2.zero; // 走行中なので慣性をゼロに

        // 終点に到着するまで待機
        yield return new WaitForSeconds(finalRunDuration);

        // 終点到着の演出
        if (stationNameText != null) stationNameText.text = "終点 まもなく到着";
        if (statusText != null) statusText.text = ">>>減速中>>>";
        CurrentInertia = new Vector2(-inertiaForce, 0); // 減速中なので左向きの慣性

        // 停車演出
        yield return new WaitForSeconds(delayBeforeSpawn);
        if (arrivalSound != null && arrivalSound.clip != null)
        {
            audioSource.PlayOneShot(arrivalSound.clip, arrivalSound.volume);
        }

        // 到着音の1秒後にクリア判定
        yield return new WaitForSeconds(1.0f);

        TriggerStageClear();
    }

    // 駅到着時の演出を管理するコルーチン
    private IEnumerator ArrivalSequenceCoroutine(StationEvent station)
    {
        // ① TMPGUIを分離して変更
        if (stationNameText != null) stationNameText.text = $" まもなく{station.stationName}";
        if (statusText != null) statusText.text = ">>>減速中>>>";
        CurrentInertia = new Vector2(-inertiaForce, 0); // 減速中なので左向きの慣性

        // ② サウンド再生までの待機
        yield return new WaitForSeconds(delayBeforeSpawn);
        if (finalArrivalSound != null && finalArrivalSound.clip != null)
        {
            audioSource.PlayOneShot(finalArrivalSound.clip, finalArrivalSound.volume);
        }

        // ③ NPCをスポーンさせ、その分混雑率を上げる
        int spawnedCount = NPCManager.instance.SpawnNPCs(this, station.npcsToSpawn);
        currentCongestionRate += spawnedCount * rateDecreasePerNpc;
        UpdateCongestionUI();

        // ゲームオーバー判定
        if (currentCongestionRate >= maxCongestionRate)
        {
            TriggerGameOver();
            yield break; // コルーチンをここで中断する
        }

        // 停車時間
        yield return new WaitForSeconds(stationStopTime);

        // ④ 駅名表示を消し、状態を「加速中」に変更
        if (stationNameText != null) stationNameText.text = "";
        if (statusText != null) statusText.text = "<<<加速中<<<";
        CurrentInertia = new Vector2(inertiaForce, 0); // 加速中なので右向きの慣性

        // 加速時間
        yield return new WaitForSeconds(accelerationTime);

        // ⑤ 状態を「走行中」に変更
        if (statusText != null) statusText.text = "走行中";
        CurrentInertia = Vector2.zero; // 走行中なので慣性をゼロに
    }

    private void TriggerStageClear()
    {
        CurrentState = GameState.StageClear;
        if (statusText != null) statusText.text = "終点";
        if (stationNameText != null) stationNameText.text = "";
        if (clearPanel != null) clearPanel.SetActive(true);
        Debug.Log("ステージクリア！");
        // Time.timeScale = 0f; // 必要であれば時間を止める
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