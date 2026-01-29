using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// ゲームオーバー画面の演出と入力制御を管理するクラス。
/// 言語設定に応じて表示するボタン群（JP/EN）を切り替える機能を実装。
/// 長押しによる決定操作（時間停止中でも動作）に対応。
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
    [Tooltip("日本語：リトライ選択時のハイライト (Image Type: Filled推奨)")]
    public Image jpRetryHighlight;
    [Tooltip("日本語：タイトル選択時のハイライト (Image Type: Filled推奨)")]
    public Image jpTitleHighlight;

    [Header("■ UI参照 (英語)")]
    [Tooltip("英語ボタン群の親オブジェクト")]
    public GameObject enButtonHolder;
    [Tooltip("英語：Retry選択時のハイライト (Image Type: Filled推奨)")]
    public Image enRetryHighlight;
    [Tooltip("英語：Title選択時のハイライト (Image Type: Filled推奨)")]
    public Image enTitleHighlight;

    [Header("■ 入力設定")]
    [Tooltip("入力開始までのディレイ（秒）")]
    public float inputStartDelay = 1.0f;
    [SerializeField] private int gripThreshold = 300;
    [SerializeField] private float tiltThreshold = 15.0f;

    [Header("■ 長押し決定設定")]
    [Tooltip("決定に必要な長押し時間（秒）")]
    [SerializeField] private float holdDuration = 2.0f;

    [Header("■ 音響設定")]
    public AudioClip selectSound;
    public AudioClip decideSound;

    private AudioSource audioSource;
    private bool isRetrySelected = true; // デフォルトはリトライ(左)
    private bool isInputActive = false;  // 入力受付中フラグ
    private bool isEnglishMode = false;  // 現在の言語モード
    private float currentHoldTimer = 0f; // 現在の長押し時間

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

        ResetHoldState();
        isInputActive = true;
    }

    private void Update()
    {
        if (!isInputActive) return;

        // 1. 選択切り替え
        float hInput = GetHorizontalInput();
        if (HandleSelectionInput(hInput))
        {
            // 選択変更時はチャージリセット
            ResetHoldState();
        }

        // 2. 長押し決定操作
        if (CheckGripInput())
        {
            // ゲームオーバー中はTimeScale=0なので unscaledDeltaTime を使用
            currentHoldTimer += Time.unscaledDeltaTime;

            // 0 -> 1 にチャージするアニメーション
            float progress = Mathf.Clamp01(currentHoldTimer / holdDuration);
            UpdateFillAnimation(progress);

            if (currentHoldTimer >= holdDuration)
            {
                if (decideSound != null) audioSource.PlayOneShot(decideSound);
                isInputActive = false;
                ExecuteChoice();
            }
        }
        else
        {
            // 離している間は「選択中(Fill=1)」状態に戻す
            ResetHoldState();
        }
    }

    /// <summary>
    /// 選択入力を処理する。
    /// </summary>
    /// <returns>選択が変更されたら true</returns>
    private bool HandleSelectionInput(float input)
    {
        bool changed = false;

        // 右入力 -> タイトルへ (右側の選択肢)
        if (input > 0.5f && isRetrySelected)
        {
            isRetrySelected = false;
            if (selectSound != null) audioSource.PlayOneShot(selectSound);
            UpdateSelectionVisuals();
            changed = true;
        }
        // 左入力 -> リトライ (左側の選択肢)
        else if (input < -0.5f && !isRetrySelected)
        {
            isRetrySelected = true;
            if (selectSound != null) audioSource.PlayOneShot(selectSound);
            UpdateSelectionVisuals();
            changed = true;
        }

        return changed;
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
            SafeSetActive(enRetryHighlight, isRetrySelected);
            SafeSetActive(enTitleHighlight, !isRetrySelected);
        }
        else
        {
            // 日本語版UIの更新
            SafeSetActive(jpRetryHighlight, isRetrySelected);
            SafeSetActive(jpTitleHighlight, !isRetrySelected);
        }

        // 切り替え直後はFill=1（満タン）にする
        UpdateFillAnimation(1.0f);
    }

    private void SafeSetActive(Image img, bool active)
    {
        if (img != null && img.gameObject != null)
        {
            img.gameObject.SetActive(active);
        }
    }

    /// <summary>
    /// 長押しタイマーをリセットし、見た目を選択中状態（Fill=1）に戻す
    /// </summary>
    private void ResetHoldState()
    {
        currentHoldTimer = 0f;
        UpdateFillAnimation(1.0f);
    }

    /// <summary>
    /// 現在選択中のハイライト画像のFillAmountを更新する
    /// </summary>
    private void UpdateFillAnimation(float amount)
    {
        Image current = GetCurrentHighlight();
        if (current != null)
        {
            current.fillAmount = amount;
        }
    }

    private Image GetCurrentHighlight()
    {
        if (isEnglishMode)
            return isRetrySelected ? enRetryHighlight : enTitleHighlight;
        else
            return isRetrySelected ? jpRetryHighlight : jpTitleHighlight;
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