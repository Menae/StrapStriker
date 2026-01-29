using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// タイトル画面における進行フローとUI表現を制御するマネージャークラス。
/// 長押しによる決定操作、誤操作防止の入力ガード、言語選択および開始確認のフローを提供する。
/// </summary>
[RequireComponent(typeof(SceneLoaderButton))]
public class TitleFlowManager : MonoBehaviour
{
    public enum TitleState
    {
        WaitToStart,    // 初期待機画面
        SelectLanguage, // 言語選択画面
        ConfirmStart,   // 最終開始確認画面
        Loading         // シーン遷移中
    }

    [Header("■ 実行状態")]
    [SerializeField] private TitleState currentState = TitleState.WaitToStart;
    public TitleState CurrentState => currentState;

    // ---------------------------------------------------------
    // UIコンテナ参照
    // ---------------------------------------------------------
    [Header("■ 階層パネル参照")]
    [SerializeField] private GameObject startPromptPanel;
    [SerializeField] private GameObject languageSelectPanel;
    [SerializeField] private GameObject confirmPanelJP;
    [SerializeField] private GameObject confirmPanelEN;

    // ---------------------------------------------------------
    // 選択状態ハイライト（Filled Imageとして設定すること）
    // ---------------------------------------------------------
    [Header("■ 言語選択ハイライト")]
    [SerializeField] private Image jpSelectedImage;
    [SerializeField] private Image enSelectedImage;

    [Header("■ 日本語確認用ハイライト")]
    [SerializeField] private Image jpYesImage;
    [SerializeField] private Image jpNoImage;

    [Header("■ 英語確認用ハイライト")]
    [SerializeField] private Image enYesImage;
    [SerializeField] private Image enNoImage;

    // ---------------------------------------------------------
    // 入力・操作設定
    // ---------------------------------------------------------
    [Header("■ デバイス入力設定")]
    [SerializeField] private int gripThreshold = 300;
    [SerializeField] private float tiltThreshold = 15.0f;
    [SerializeField] private bool invertTilt = false;

    [Header("■ 長押し決定設定")]
    [Tooltip("決定に必要な長押し時間（秒）")]
    [SerializeField] private float holdDuration = 2.0f;
    [Tooltip("状態遷移後の入力ブロック時間（秒）")]
    [SerializeField] private float stateChangeCooldown = 0.5f;

    // 内部制御変数
    private SceneLoaderButton sceneLoaderButton;
    private bool isInputLocked = false;
    private bool waitForRelease = false;    // 連打防止用フラグ
    private float currentHoldTimer = 0f;    // 現在の長押し時間

    private bool isEnglishSelected = false; // 偽:日本語, 真:英語
    private bool isYesSelected = true;      // 選択カーソルの位置

    private void Start()
    {
        sceneLoaderButton = GetComponent<SceneLoaderButton>();
        UpdateVisuals();
    }

    private void Update()
    {
        if (isInputLocked) return;

        // 1. 入力状態の取得
        bool isGripping = CheckGripInput();

        // 2. 連打防止（Release Guard）チェック
        if (waitForRelease)
        {
            if (isGripping)
            {
                // 手が離されるまで待機。
                // 選択中であることを示すため、Fillは1（満タン）で維持する。
                ResetHoldState();
                return;
            }
            // 手が離されたらガード解除
            waitForRelease = false;
        }

        // 3. 各ステートの処理
        switch (currentState)
        {
            case TitleState.WaitToStart:
                // 待機画面は「握った瞬間」に次へ遷移（即時反応）
                if (isGripping)
                {
                    ChangeState(TitleState.SelectLanguage);
                }
                break;

            case TitleState.SelectLanguage:
                if (HandleLanguageSelection()) ResetHoldState();
                ProcessHoldInteraction(() => ChangeState(TitleState.ConfirmStart), isGripping);
                break;

            case TitleState.ConfirmStart:
                if (HandleConfirmSelection()) ResetHoldState();
                ProcessHoldInteraction(ExecuteConfirmAction, isGripping);
                break;
        }
    }

    // =========================================================
    // 長押し・決定ロジック
    // =========================================================

    /// <summary>
    /// 長押し判定と進行度に応じたUI更新を行う共通処理。
    /// 未入力時はFill=1（選択中）、長押し中は0→1（チャージ）で推移する。
    /// </summary>
    /// <param name="onComplete">決定時のアクション</param>
    /// <param name="isGripping">現在の入力状態</param>
    private void ProcessHoldInteraction(System.Action onComplete, bool isGripping)
    {
        if (isGripping)
        {
            // 加算
            currentHoldTimer += Time.deltaTime;

            // 進行度の正規化 (0.0 - 1.0)
            float progress = Mathf.Clamp01(currentHoldTimer / holdDuration);
            UpdateFillAnimation(progress);

            // 決定判定
            if (currentHoldTimer >= holdDuration)
            {
                onComplete?.Invoke();

                // 決定後は入力をリセットし、手を離すまでブロック
                ResetHoldState();
                waitForRelease = true;
            }
        }
        else
        {
            // 離している間は「選択されている」ことを示すためFillを1にする
            ResetHoldState();
        }
    }

    /// <summary>
    /// 長押しタイマーをリセットし、見た目を選択中状態（Fill=1）に戻す。
    /// </summary>
    private void ResetHoldState()
    {
        currentHoldTimer = 0f;
        UpdateFillAnimation(1.0f);
    }

    /// <summary>
    /// 現在アクティブな選択画像のFillAmountを更新する。
    /// </summary>
    private void UpdateFillAnimation(float fillAmount)
    {
        Image targetImage = GetCurrentActiveImage();
        if (targetImage != null)
        {
            targetImage.fillAmount = fillAmount;
        }
    }

    /// <summary>
    /// 現在のステートと選択状況に基づいて、操作対象となるImageを特定する。
    /// </summary>
    private Image GetCurrentActiveImage()
    {
        switch (currentState)
        {
            case TitleState.WaitToStart:
                return null;

            case TitleState.SelectLanguage:
                return isEnglishSelected ? enSelectedImage : jpSelectedImage;

            case TitleState.ConfirmStart:
                if (isEnglishSelected)
                    return isYesSelected ? enYesImage : enNoImage;
                else
                    return isYesSelected ? jpYesImage : jpNoImage;

            default:
                return null;
        }
    }

    private void ExecuteConfirmAction()
    {
        if (isYesSelected)
        {
            ChangeState(TitleState.Loading);
            sceneLoaderButton.LoadTargetScene();
        }
        else
        {
            // 「いいえ」なら言語選択へ戻る
            ChangeState(TitleState.SelectLanguage);
        }
    }

    // =========================================================
    // 入力インターフェース
    // =========================================================

    private bool CheckGripInput()
    {
        // デバッグ用キーボード入力
        if (Input.GetKey(KeyCode.Space)) return true;

        if (ArduinoInputManager.instance != null && ArduinoInputManager.instance.IsConnected)
        {
            return (ArduinoInputManager.GripValue1 > gripThreshold &&
                    ArduinoInputManager.GripValue2 > gripThreshold);
        }
        return false;
    }

    private float GetHorizontalInput()
    {
        float key = Input.GetAxisRaw("Horizontal");
        if (Mathf.Abs(key) > 0.1f) return key;

        if (ArduinoInputManager.instance != null && ArduinoInputManager.instance.IsConnected)
        {
            Vector3 accel = ArduinoInputManager.RawAccel;
            float angle = -Mathf.Atan2(accel.x, accel.y) * Mathf.Rad2Deg;
            if (invertTilt) angle *= -1;

            if (angle > tiltThreshold) return 1.0f;
            else if (angle < -tiltThreshold) return -1.0f;
        }
        return 0f;
    }

    // =========================================================
    // ステート制御ロジック（選択変更の検知）
    // =========================================================

    private bool HandleLanguageSelection()
    {
        float input = GetHorizontalInput();
        bool changed = false;

        if (input < -0.5f && isEnglishSelected)
        {
            ApplyLanguageChange(false);
            changed = true;
        }
        else if (input > 0.5f && !isEnglishSelected)
        {
            ApplyLanguageChange(true);
            changed = true;
        }
        return changed;
    }

    private void ApplyLanguageChange(bool useEnglish)
    {
        isEnglishSelected = useEnglish;
        UpdateVisuals();

        if (LocalizationManager.Instance != null)
        {
            LocalizationManager.Instance.SetLanguage(useEnglish ? Language.English : Language.Japanese);
        }
    }

    private bool HandleConfirmSelection()
    {
        float input = GetHorizontalInput();
        bool changed = false;

        if (input < -0.5f && !isYesSelected)
        {
            isYesSelected = true;
            UpdateVisuals();
            changed = true;
        }
        else if (input > 0.5f && isYesSelected)
        {
            isYesSelected = false;
            UpdateVisuals();
            changed = true;
        }
        return changed;
    }

    private void ChangeState(TitleState newState)
    {
        StartCoroutine(StateTransitionRoutine(newState));
    }

    private IEnumerator StateTransitionRoutine(TitleState newState)
    {
        isInputLocked = true;
        currentState = newState;

        if (newState == TitleState.ConfirmStart) isYesSelected = true;

        UpdateVisuals();

        // 遷移直後は入力をリセット（Fillは1にして選択中であることを示す）
        ResetHoldState();

        yield return new WaitForSeconds(stateChangeCooldown);

        // ※遷移直後の誤動作を防ぐため、一度手を離すまで入力を待機させる
        isInputLocked = false;
        waitForRelease = true;
    }

    // =========================================================
    // UIレンダリング
    // =========================================================

    private void UpdateVisuals()
    {
        // 1. パネル表示切り替え
        SetObjActive(startPromptPanel, currentState == TitleState.WaitToStart);
        SetObjActive(languageSelectPanel, currentState == TitleState.SelectLanguage);

        bool isConfirm = (currentState == TitleState.ConfirmStart);
        SetObjActive(confirmPanelJP, isConfirm && !isEnglishSelected);
        SetObjActive(confirmPanelEN, isConfirm && isEnglishSelected);

        // 2. 選択画像のアクティブ切り替え（非選択なら非表示）
        bool isLangSelect = (currentState == TitleState.SelectLanguage);
        SafeSetActive(jpSelectedImage, isLangSelect && !isEnglishSelected);
        SafeSetActive(enSelectedImage, isLangSelect && isEnglishSelected);

        bool isConfirmJP = (isConfirm && !isEnglishSelected);
        bool isConfirmEN = (isConfirm && isEnglishSelected);

        SafeSetActive(jpYesImage, isConfirmJP && isYesSelected);
        SafeSetActive(jpNoImage, isConfirmJP && !isYesSelected);
        SafeSetActive(enYesImage, isConfirmEN && isYesSelected);
        SafeSetActive(enNoImage, isConfirmEN && !isYesSelected);

        // 切り替え直後は「選択中(Fill=1)」として表示する
        UpdateFillAnimation(1.0f);
    }

    private void SetObjActive(GameObject obj, bool active)
    {
        if (obj != null) obj.SetActive(active);
    }

    private void SafeSetActive(Image img, bool active)
    {
        if (img != null && img.gameObject != null)
        {
            img.gameObject.SetActive(active);
            // 非表示にする際はFillを0に戻しておく（次に表示された時の一瞬のチラつき防止）
            if (!active) img.fillAmount = 0f;
        }
    }
}