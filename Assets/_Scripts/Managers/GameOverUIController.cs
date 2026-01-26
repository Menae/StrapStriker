using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// ゲームオーバー画面の演出と入力制御を管理するクラス。
/// Time.timeScale = 0 の環境下でも確実に入力を受け付けるよう設計。
/// </summary>
public class GameOverUIController : MonoBehaviour
{
    [Header("■ 外部参照")]
    public StageManager stageManager;

    [Header("■ UI参照")]
    public GameObject gameOverPanel;
    public Image retryHighlightImage;
    public Image titleHighlightImage;

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

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();

        if (gameOverPanel != null) gameOverPanel.SetActive(false);
    }

    /// <summary>
    /// ゲームオーバー演出を開始する。
    /// </summary>
    public void ShowGameOver()
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(true);

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
        // センサーのノイズ等で進行不能になるのを防ぐため、傾きの判定は含めない
        while (CheckGripInput())
        {
            yield return null;
        }

        isInputActive = true;
    }

    /// <summary>
    /// Time.timeScaleが0でもUpdateは動作するため、ここで入力を監視する。
    /// </summary>
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

    private void UpdateSelectionVisuals()
    {
        if (retryHighlightImage != null) retryHighlightImage.gameObject.SetActive(isRetrySelected);
        if (titleHighlightImage != null) titleHighlightImage.gameObject.SetActive(!isRetrySelected);
    }

    // --- センサー・入力判定 ---

    private bool CheckGripInput()
    {
        // Space / Enter キーでの決定
        if (Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.Return)) return true;

        if (ArduinoInputManager.instance != null && ArduinoInputManager.instance.IsConnected)
        {
            return (ArduinoInputManager.GripValue1 > gripThreshold &&
                    ArduinoInputManager.GripValue2 > gripThreshold);
        }
        return false;
    }

    /// <summary>
    /// キーボードとArduinoの入力を取得する。
    /// TimeScale=0でも確実に反応するよう、物理キーを直接監視する。
    /// </summary>
    private float GetHorizontalInput()
    {
        // 1. 物理キー入力を最優先でチェック
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) return 1.0f;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) return -1.0f;

        // 2. Arduino 加速度入力
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