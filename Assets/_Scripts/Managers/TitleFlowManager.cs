using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// タイトル画面における一連の進行（ステートマシン）とUI表現を制御するマネージャークラス。
/// センサー入力に応じた言語選択、および開始確認のフローを提供し、
/// デザイナー作成の画像オブジェクトの表示・非表示を切り替えることで状態を視覚化する。
/// </summary>
[RequireComponent(typeof(SceneLoaderButton))]
public class TitleFlowManager : MonoBehaviour
{
    /// <summary>
    /// タイトル画面の各進行フェーズを定義する列挙型。
    /// </summary>
    public enum TitleState
    {
        WaitToStart,    // 初期待機画面（「握って開始」等）
        SelectLanguage, // 言語選択画面
        ConfirmStart,   // 最終開始確認画面（Yes/No）
        Loading         // シーン遷移中
    }

    [Header("■ 実行状態")]
    [SerializeField] private TitleState currentState = TitleState.WaitToStart;
    public TitleState CurrentState => currentState;

    // ---------------------------------------------------------
    // UIコンテナ参照
    // ---------------------------------------------------------
    [Header("■ 階層パネル参照")]
    [SerializeField, Tooltip("開始待機時の親パネル")]
    private GameObject startPromptPanel;

    [SerializeField, Tooltip("言語選択画面の親パネル")]
    private GameObject languageSelectPanel;

    [SerializeField, Tooltip("日本語用確認画面の親パネル")]
    private GameObject confirmPanelJP;

    [SerializeField, Tooltip("英語用確認画面の親パネル")]
    private GameObject confirmPanelEN;

    // ---------------------------------------------------------
    // 選択状態ハイライト（Selected画像）
    // ---------------------------------------------------------
    [Header("■ 言語選択ハイライト")]
    [SerializeField, Tooltip("日本語選択時に表示する画像オブジェクト")]
    private Image jpSelectedImage;
    [SerializeField, Tooltip("英語選択時に表示する画像オブジェクト")]
    private Image enSelectedImage;

    [Header("■ 日本語確認用ハイライト")]
    [SerializeField, Tooltip("日本語モード：『はい』選択時の画像")]
    private Image jpYesImage;
    [SerializeField, Tooltip("日本語モード：『いいえ』選択時の画像")]
    private Image jpNoImage;

    [Header("■ 英語確認用ハイライト")]
    [SerializeField, Tooltip("英語モード：『YES』選択時の画像")]
    private Image enYesImage;
    [SerializeField, Tooltip("英語モード：『NO』選択時の画像")]
    private Image enNoImage;

    // ---------------------------------------------------------
    // デバイス入力・感度設定
    // ---------------------------------------------------------
    [Header("■ デバイス入力設定")]
    [SerializeField, Tooltip("M5StickCからの静電容量値がこの値を超えると『握った』と判定する")]
    private int gripThreshold = 300;

    [SerializeField, Tooltip("デバイスをこの角度（度）以上傾けると左右選択とみなす")]
    private float tiltThreshold = 15.0f;

    [SerializeField, Tooltip("加速度センサの軸方向を反転させる場合に有効化")]
    private bool invertTilt = false;

    [Header("■ 操作感調整")]
    [SerializeField, Tooltip("状態遷移後、次の入力を受け付けるまでのインターバル（秒）")]
    private float stateChangeCooldown = 0.5f;

    // 内部制御変数
    private SceneLoaderButton sceneLoaderButton;
    private bool isInputLocked = false;
    private bool isEnglishSelected = false; // 偽:日本語, 真:英語
    private bool isYesSelected = true;      // 選択カーソルの位置

    /// <summary>
    /// コンポーネントの初期化および初期状態のUI構築を行う。
    /// </summary>
    private void Start()
    {
        sceneLoaderButton = GetComponent<SceneLoaderButton>();

        // 全UI要素を現在のステートに基づきリセット
        UpdateVisuals();
    }

    /// <summary>
    /// フレーム毎の入力監視とステート更新のハンドリング。
    /// </summary>
    private void Update()
    {
        if (isInputLocked) return;

        bool isGripping = CheckGripInput();

        switch (currentState)
        {
            case TitleState.WaitToStart:
                if (isGripping) ChangeState(TitleState.SelectLanguage);
                break;

            case TitleState.SelectLanguage:
                HandleLanguageSelection();
                if (isGripping) ChangeState(TitleState.ConfirmStart);
                break;

            case TitleState.ConfirmStart:
                HandleConfirmSelection();
                if (isGripping)
                {
                    if (isYesSelected)
                    {
                        ChangeState(TitleState.Loading);
                        sceneLoaderButton.LoadTargetScene();
                    }
                    else
                    {
                        // 「いいえ」選択時は言語選択へ戻る
                        ChangeState(TitleState.SelectLanguage);
                    }
                }
                break;
        }
    }

    // =========================================================
    // 入力インターフェース
    // =========================================================

    /// <summary>
    /// ハードウェア（M5StickC）またはキーボードからの握力入力を判定する。
    /// </summary>
    private bool CheckGripInput()
    {
        if (Input.GetKey(KeyCode.Space)) return true;

        if (ArduinoInputManager.instance != null && ArduinoInputManager.instance.IsConnected)
        {
            return (ArduinoInputManager.GripValue1 > gripThreshold &&
                    ArduinoInputManager.GripValue2 > gripThreshold);
        }
        return false;
    }

    /// <summary>
    /// デバイスの傾きに基づく水平方向の入力を取得する。
    /// </summary>
    /// <returns>左入力時は負値、右入力時は正値</returns>
    private float GetHorizontalInput()
    {
        float key = Input.GetAxisRaw("Horizontal");
        if (Mathf.Abs(key) > 0.1f) return key;

        if (ArduinoInputManager.instance != null && ArduinoInputManager.instance.IsConnected)
        {
            Vector3 accel = ArduinoInputManager.RawAccel;
            // 垂直・水平軸のAtan2により傾斜角を算出
            float angle = -Mathf.Atan2(accel.x, accel.y) * Mathf.Rad2Deg;
            if (invertTilt) angle *= -1;

            if (angle > tiltThreshold) return 1.0f;
            else if (angle < -tiltThreshold) return -1.0f;
        }
        return 0f;
    }

    // =========================================================
    // ステート制御ロジック
    // =========================================================

    private void HandleLanguageSelection()
    {
        float input = GetHorizontalInput();
        if (input < -0.5f && isEnglishSelected)
        {
            ApplyLanguageChange(false);
        }
        else if (input > 0.5f && !isEnglishSelected)
        {
            ApplyLanguageChange(true);
        }
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

    private void HandleConfirmSelection()
    {
        float input = GetHorizontalInput();
        if (input < -0.5f && !isYesSelected)
        {
            isYesSelected = true;
            UpdateVisuals();
        }
        else if (input > 0.5f && isYesSelected)
        {
            isYesSelected = false;
            UpdateVisuals();
        }
    }

    private void ChangeState(TitleState newState)
    {
        StartCoroutine(StateTransitionRoutine(newState));
    }

    /// <summary>
    /// 状態遷移時の入力ロックおよび後処理を管理するコルーチン。
    /// 手が離れるまで遷移を待機することでチャタリングや誤操作を防止する。
    /// </summary>
    private IEnumerator StateTransitionRoutine(TitleState newState)
    {
        isInputLocked = true;

        // 物理的な入力解除を待機
        while (CheckGripInput()) yield return null;

        yield return new WaitForSeconds(stateChangeCooldown);

        currentState = newState;

        // 状態遷移時にデフォルト位置（YES）へリセット
        if (newState == TitleState.ConfirmStart) isYesSelected = true;

        // 【重要】状態確定後に全UIをリフレッシュし、古い表示をクリーンアップ
        UpdateVisuals();

        isInputLocked = false;
    }

    // =========================================================
    // UIレンダリング・クリーンアップロジック
    // =========================================================

    /// <summary>
    /// 現在のステート、選択言語、カーソル位置に基づき、全UI要素の表示状態を一括更新する。
    /// </summary>
    private void UpdateVisuals()
    {
        // 1. パネル自体の表示・非表示
        if (startPromptPanel != null) startPromptPanel.SetActive(currentState == TitleState.WaitToStart);
        if (languageSelectPanel != null) languageSelectPanel.SetActive(currentState == TitleState.SelectLanguage);

        bool isConfirm = (currentState == TitleState.ConfirmStart);
        if (confirmPanelJP != null) confirmPanelJP.SetActive(isConfirm && !isEnglishSelected);
        if (confirmPanelEN != null) confirmPanelEN.SetActive(isConfirm && isEnglishSelected);

        // 2. 言語選択ハイライトの更新（ステート外なら強制非表示）
        bool isLangSelect = (currentState == TitleState.SelectLanguage);
        SafeSetActive(jpSelectedImage, isLangSelect && !isEnglishSelected);
        SafeSetActive(enSelectedImage, isLangSelect && isEnglishSelected);

        // 3. 確認画面ハイライトの更新（ステート外、または別言語なら強制非表示）
        bool isConfirmJP = (isConfirm && !isEnglishSelected);
        bool isConfirmEN = (isConfirm && isEnglishSelected);

        SafeSetActive(jpYesImage, isConfirmJP && isYesSelected);
        SafeSetActive(jpNoImage, isConfirmJP && !isYesSelected);
        SafeSetActive(enYesImage, isConfirmEN && isYesSelected);
        SafeSetActive(enNoImage, isConfirmEN && !isYesSelected);
    }

    /// <summary>
    /// コンポーネントが未割当（Null）の場合を考慮した安全なSetActive処理。
    /// </summary>
    private void SafeSetActive(Graphic uiElement, bool active)
    {
        if (uiElement != null && uiElement.gameObject != null)
        {
            uiElement.gameObject.SetActive(active);
        }
    }
}