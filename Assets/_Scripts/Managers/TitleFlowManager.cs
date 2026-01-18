using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

[RequireComponent(typeof(SceneLoaderButton))]
public class TitleFlowManager : MonoBehaviour
{
    public enum TitleState
    {
        WaitToStart,    // 最初の待機画面
        SelectLanguage, // 言語選択
        ConfirmStart,   // 最終確認 (Yes/No)
        Loading         // ロード中
    }

    [Header("状態")]
    public TitleState currentState = TitleState.WaitToStart;

    [Header("UIパネル参照 (使わないものはNoneでOK)")]
    public GameObject startPromptPanel; // 背景に文字があるならNoneでOK
    public GameObject languagePanel;
    public GameObject confirmPanel;

    [Header("UIテキスト参照")]
    [Tooltip("確認画面で「日本語でよろしいですか？」等を表示するテキスト")]
    public TextMeshProUGUI confirmMessageText;

    [Header("選択肢ハイライト用 (言語)")]
    public Image japaneseBg;
    public Image englishBg;
    public Color selectedColor = Color.yellow;
    public Color normalColor = Color.white;

    [Header("選択肢ハイライト用 (確認)")]
    public Image yesBg;
    public Image noBg;

    [Header("入力設定")]
    [Tooltip("握力閾値")]
    public int gripThreshold = 500;

    [Tooltip("左右判定とみなす傾きの角度")]
    public float tiltThreshold = 15.0f;

    [Tooltip("傾きの方向が逆の場合にチェック")]
    public bool invertTilt = false;

    [Header("感度調整")]
    [Tooltip("画面遷移後、次の入力を受け付けるまでの待機時間(秒)")]
    public float stateChangeCooldown = 0.5f; // ←ここを増やすと誤操作が減ります

    private SceneLoaderButton sceneLoaderButton;
    private bool isInputLocked = false;

    private bool isEnglishSelected = false;
    private bool isYesSelected = true;

    void Start()
    {
        sceneLoaderButton = GetComponent<SceneLoaderButton>();
        ShowStateUI(TitleState.WaitToStart);
    }

    void Update()
    {
        // 入力ロック中は何もしない
        if (isInputLocked) return;

        bool isGripping = IsGripping();

        switch (currentState)
        {
            case TitleState.WaitToStart:
                if (isGripping)
                {
                    ChangeState(TitleState.SelectLanguage);
                }
                break;

            case TitleState.SelectLanguage:
                HandleLanguageSelection();
                if (isGripping)
                {
                    ChangeState(TitleState.ConfirmStart);
                }
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
                        ChangeState(TitleState.SelectLanguage);
                    }
                }
                break;
        }
    }

    // --- 入力判定 ---

    private bool IsGripping()
    {
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

    // --- ロジック ---

    private void HandleLanguageSelection()
    {
        float input = GetHorizontalInput();
        if (input < -0.5f && isEnglishSelected)
        {
            isEnglishSelected = false;
            UpdateLanguageUI();
            LocalizationManager.Instance.SetLanguage(Language.Japanese);
        }
        else if (input > 0.5f && !isEnglishSelected)
        {
            isEnglishSelected = true;
            UpdateLanguageUI();
            LocalizationManager.Instance.SetLanguage(Language.English);
        }
    }

    private void HandleConfirmSelection()
    {
        float input = GetHorizontalInput();
        if (input < -0.5f && !isYesSelected)
        {
            isYesSelected = true;
            UpdateConfirmUI();
        }
        else if (input > 0.5f && isYesSelected)
        {
            isYesSelected = false;
            UpdateConfirmUI();
        }
    }

    // --- 状態遷移 (ここを修正) ---

    private void ChangeState(TitleState newState)
    {
        StartCoroutine(StateTransitionRoutine(newState));
    }

    private IEnumerator StateTransitionRoutine(TitleState newState)
    {
        isInputLocked = true; // 即座に入力をブロック

        // 1. まず手が離れるのを待つ (これがないと押しっぱなしで全画面スキップしてしまう)
        while (IsGripping())
        {
            yield return null;
        }

        // 2. 手が離れた後も少し待つ (チャタリング/誤操作防止)
        yield return new WaitForSeconds(stateChangeCooldown);

        // 3. 状態更新
        currentState = newState;
        ShowStateUI(newState);

        if (newState == TitleState.SelectLanguage) UpdateLanguageUI();
        if (newState == TitleState.ConfirmStart)
        {
            isYesSelected = true;
            UpdateConfirmUI();

            // 選択された言語に応じたキーを選ぶ
            string msgKey = isEnglishSelected ? "ui_confirm_en" : "ui_confirm_jp";

            // LocalizationManagerからテキストとフォントを取得してセット
            if (confirmMessageText != null && LocalizationManager.Instance != null)
            {
                confirmMessageText.text = LocalizationManager.Instance.GetText(msgKey);
                confirmMessageText.font = LocalizationManager.Instance.GetCurrentFont();
            }
        }

        isInputLocked = false; // ロック解除
    }

    private void ShowStateUI(TitleState state)
    {
        // Nullチェックを追加 (変数 != null ? true : false)
        if (startPromptPanel != null) startPromptPanel.SetActive(state == TitleState.WaitToStart);
        if (languagePanel != null) languagePanel.SetActive(state == TitleState.SelectLanguage);
        if (confirmPanel != null) confirmPanel.SetActive(state == TitleState.ConfirmStart);
    }

    private void UpdateLanguageUI()
    {
        if (japaneseBg != null) japaneseBg.color = !isEnglishSelected ? selectedColor : normalColor;
        if (englishBg != null) englishBg.color = isEnglishSelected ? selectedColor : normalColor;
    }

    private void UpdateConfirmUI()
    {
        if (yesBg != null) yesBg.color = isYesSelected ? selectedColor : normalColor;
        if (noBg != null) noBg.color = !isYesSelected ? selectedColor : normalColor;
    }
}