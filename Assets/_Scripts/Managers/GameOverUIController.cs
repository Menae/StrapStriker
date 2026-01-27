using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// ゲームオーバー画面の演出と入力制御を管理するクラス。
/// 言語設定に応じて表示するボタン群（JP/EN）を切り替える機能を実装。
/// </summary>
public class GameOverUIController : MonoBehaviour
{
    [Header("■ 外部参照")]
    public StageManager stageManager;

    [Header("■ UI参照 (共通)")]
    public GameObject gameOverPanel;

    [Header("■ UI参照 (日本語)")]
    [Tooltip("日本語ボタン群の親オブジェクト")]
    public GameObject jpButtonHolder;
    [Tooltip("日本語：リトライ選択時のハイライト")]
    public Image jpRetryHighlight;
    [Tooltip("日本語：タイトル選択時のハイライト")]
    public Image jpTitleHighlight;

    [Header("■ UI参照 (英語)")]
    [Tooltip("英語ボタン群の親オブジェクト")]
    public GameObject enButtonHolder;
    [Tooltip("英語：Retry選択時のハイライト")]
    public Image enRetryHighlight;
    [Tooltip("英語：Title選択時のハイライト")]
    public Image enTitleHighlight;

    [Header("■ 入力設定")]
    [Tooltip("入力開始までのディレイ（秒）")]
    public float inputStartDelay = 1.0f;
    [SerializeField] private int gripThreshold = 300;
    [SerializeField] private float tiltThreshold = 15.0f;

    [Header("■ 音響設定")]
    public AudioClip selectSound;
    public AudioClip decideSound;

    private AudioSource audioSource;
    private bool isRetrySelected = true; // デフォルトはリトライ(左)
    private bool isInputActive = false;  // 入力受付中フラグ
    private bool isEnglishMode = false;  // 現在の言語モード

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();

        if (gameOverPanel != null) gameOverPanel.SetActive(false);
    }

    /// <summary>
    /// ゲームオーバー演出を開始する。
    /// 現在の言語設定を取得し、適切なUIを表示する。
    /// </summary>
    public void ShowGameOver()
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(true);

        // 言語判定
        isEnglishMode = false;
        if (LocalizationManager.Instance != null)
        {
            isEnglishMode = (LocalizationManager.Instance.CurrentLanguage == Language.English);
        }

        // 言語ごとのホルダー表示切り替え
        if (jpButtonHolder != null) jpButtonHolder.SetActive(!isEnglishMode);
        if (enButtonHolder != null) enButtonHolder.SetActive(isEnglishMode);

        isRetrySelected = true;
        isInputActive = false;
        UpdateSelectionVisuals();

        // 演出待機コルーチンを起動
        StartCoroutine(WaitAndEnableInput());
    }

    /// <summary>
    /// 演出待機後、入力を有効化する。
    /// </summary>
    private IEnumerator WaitAndEnableInput()
    {
        // TimeScale=0でも動くようRealtimeを使用
        yield return new WaitForSecondsRealtime(inputStartDelay);

        // 決定入力（握り/Space）が離されるまで待機
        while (CheckGripInput())
        {
            yield return null;
        }

        isInputActive = true;
    }

    private void Update()
    {
        if (!isInputActive) return;

        // 1. 選択切り替え
        float hInput = GetHorizontalInput();
        HandleSelectionInput(hInput);

        // 2. 決定操作
        if (CheckGripInput())
        {
            if (decideSound != null) audioSource.PlayOneShot(decideSound);

            isInputActive = false; // 二重実行防止
            ExecuteChoice();
        }
    }

    private void HandleSelectionInput(float input)
    {
        // 右入力 -> タイトルへ (右側の選択肢)
        if (input > 0.5f && isRetrySelected)
        {
            isRetrySelected = false;
            if (selectSound != null) audioSource.PlayOneShot(selectSound);
            UpdateSelectionVisuals();
        }
        // 左入力 -> リトライ (左側の選択肢)
        else if (input < -0.5f && !isRetrySelected)
        {
            isRetrySelected = true;
            if (selectSound != null) audioSource.PlayOneShot(selectSound);
            UpdateSelectionVisuals();
        }
    }

    private void ExecuteChoice()
    {
        if (stageManager == null) return;

        if (isRetrySelected)
        {
            stageManager.RetryStage();
        }
        else
        {
            stageManager.ReturnToTitle();
        }
    }

    /// <summary>
    /// 現在の言語モードと言語選択状態に合わせてハイライトを更新する。
    /// </summary>
    private void UpdateSelectionVisuals()
    {
        if (isEnglishMode)
        {
            // 英語版UIの更新
            if (enRetryHighlight != null) enRetryHighlight.gameObject.SetActive(isRetrySelected);
            if (enTitleHighlight != null) enTitleHighlight.gameObject.SetActive(!isRetrySelected);
        }
        else
        {
            // 日本語版UIの更新
            if (jpRetryHighlight != null) jpRetryHighlight.gameObject.SetActive(isRetrySelected);
            if (jpTitleHighlight != null) jpTitleHighlight.gameObject.SetActive(!isRetrySelected);
        }
    }

    // --- センサー・入力判定 ---

    private bool CheckGripInput()
    {
        if (Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.Return)) return true;

        if (ArduinoInputManager.instance != null && ArduinoInputManager.instance.IsConnected)
        {
            return (ArduinoInputManager.GripValue1 > gripThreshold &&
                    ArduinoInputManager.GripValue2 > gripThreshold);
        }
        return false;
    }

    private float GetHorizontalInput()
    {
        // 物理キー入力を最優先
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) return 1.0f;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) return -1.0f;

        // Arduino 加速度入力
        if (ArduinoInputManager.instance != null && ArduinoInputManager.instance.IsConnected)
        {
            Vector3 accel = ArduinoInputManager.RawAccel;
            float angle = -Mathf.Atan2(accel.x, accel.y) * Mathf.Rad2Deg;

            if (angle > tiltThreshold) return 1.0f;       // 右傾き
            else if (angle < -tiltThreshold) return -1.0f; // 左傾き
        }

        return 0f;
    }
}