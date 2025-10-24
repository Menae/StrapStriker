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
    public TextMeshProUGUI defeatedNpcCountText;

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
        if (CurrentState == GameState.Playing || CurrentState == GameState.Paused)
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                TogglePause();
            }
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
        if (statusText != null) statusText.text = "走行中";
        if (stationNameText != null) stationNameText.text = "";
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
        }

        if (statusText != null) statusText.text = "走行中";
        if (stationNameText != null) stationNameText.text = "次は 終点";
        CurrentInertia = Vector2.zero;

        yield return new WaitForSeconds(finalRunDuration);

        if (stationNameText != null) stationNameText.text = "終点 まもなく到着";
        if (statusText != null) statusText.text = ">>>減速中>>>";
        CurrentInertia = new Vector2(-inertiaForce, 0);

        yield return new WaitForSeconds(delayBeforeSpawn);
        if (arrivalSound != null && arrivalSound.clip != null)
        {
            audioSource.PlayOneShot(arrivalSound.clip, arrivalSound.volume);
        }
        StopBlinking(); // 終点到着で点滅停止

        yield return new WaitForSeconds(1.0f);
        TriggerStageClear();
    }

    private IEnumerator ArrivalSequenceCoroutine(StationEvent station)
    {
        // ① 駅接近開始：ParallaxControllerに駅背景へのクロスフェードを開始させる
        if (parallaxController != null)
        {
            parallaxController.StartApproachingStation(station.stationBackgroundSprite);
        }

        // ② UI表示を「減速中」に変更
        if (stationNameText != null) stationNameText.text = $" まもなく{station.stationName}";
        if (statusText != null) statusText.text = ">>>減速中>>>";
        CurrentInertia = new Vector2(-inertiaForce, 0);

        // ③ 駅の演出時間（背景フェードや減速など）
        yield return new WaitForSeconds(delayBeforeSpawn);

        // ④ 駅に到着したので、進捗UIの点滅を停止
        StopBlinking();

        // 効果音を再生
        if (finalArrivalSound != null && finalArrivalSound.clip != null)
        {
            audioSource.PlayOneShot(finalArrivalSound.clip, finalArrivalSound.volume);
        }

        if (doorController != null)
        {
            DoorManager.OpenAllDoors();
        }

        // ⑤ NPCをスポーンさせ、混雑率を更新
        int spawnedCount = NPCManager.instance.SpawnNPCs(this, station.npcsToSpawn);
        currentCongestionRate += spawnedCount * rateDecreasePerNpc;
        UpdateCongestionUI();

        if (currentCongestionRate >= maxCongestionRate)
        {
            TriggerGameOver();
            yield break;
        }

        // ⑥ 停車状態へ移行
        if (statusText != null) statusText.text = "停車中";
        CurrentInertia = Vector2.zero;

        // ⑦ 停車時間ぶん待機
        yield return new WaitForSeconds(stationStopTime);

        // ⑧ 駅出発：ParallaxControllerに通常背景へのクロスフェードを開始させる
        if (parallaxController != null)
        {
            parallaxController.DepartFromStation();
        }

        // ⑨ UI表示を「加速中」に変更
        if (stationNameText != null) stationNameText.text = "";
        if (statusText != null) statusText.text = "<<<加速中<<<";
        CurrentInertia = new Vector2(inertiaForce, 0);

        // ⑩ 加速時間ぶん待機
        yield return new WaitForSeconds(accelerationTime);

        // ⑪ 走行状態へ移行
        if (statusText != null) statusText.text = "走行中";
        CurrentInertia = Vector2.zero;
    }

    private void TriggerStageClear()
    {
        CurrentState = GameState.StageClear;
        if (statusText != null) statusText.text = "終点";
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