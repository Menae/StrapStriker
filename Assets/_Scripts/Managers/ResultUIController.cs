using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// リザルト画面の表示演出および、その後のリトライ確認入力を管理するクラス。
/// 言語設定に応じて表示内容（タイトル、確認パネル）を切り替える機能を実装。
/// </summary>
public class ResultUIController : MonoBehaviour
{
    [Header("■ 外部参照")]
    [Tooltip("リトライ/タイトル戻りの関数を呼ぶために必要")]
    public StageManager stageManager;

    [Header("■ 基本リザルトUI")]
    [Tooltip("リザルト画面のパネル全体")]
    public GameObject resultPanel;
    [Tooltip("撃破数を表示するテキスト")]
    public TextMeshProUGUI killCountText;

    // --- 言語別タイトル ---
    [Header("■ ユーウツ画像")]
    [Tooltip("日本語")]
    public GameObject titleJP;
    [Tooltip("英語Melancholy")]
    public GameObject titleEN;

    [Header("■ 星アイコン設定")]
    public List<Image> starImages;
    public Color earnedColor = new Color(1f, 0.8f, 0f, 1f);
    public Color unearnedColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);

    [Header("■ アニメーション設定")]
    [Tooltip("表示開始時の待機時間")]
    public float startDelay = 0.5f;
    [Tooltip("星が1つ表示される間隔")]
    public float starInterval = 0.4f;
    [Tooltip("演出終了後、確認パネルが表示されるまでの待機時間")]
    public float delayBeforeConfirmPanel = 1.0f;

    [Tooltip("星が表示される時の効果音")]
    public AudioClip starSound;
    [Tooltip("選択時のSE")]
    public AudioClip selectSound;
    [Tooltip("決定時のSE")]
    public AudioClip decideSound;

    // --- リトライ確認用UI (言語別) ---
    [Header("■ リトライ確認UI (日本語)")]
    [Tooltip("「もう一度プレイしますか？」パネル (JP)")]
    public GameObject confirmPanelJP;
    [Tooltip("「はい」ハイライト (JP)")]
    public Image haiHighlight;
    [Tooltip("「いいえ」ハイライト (JP)")]
    public Image iieHighlight;

    [Header("■ リトライ確認UI (英語)")]
    [Tooltip("「Play Again?」パネル (EN)")]
    public GameObject confirmPanelEN;
    [Tooltip("「YES」ハイライト (EN)")]
    public Image yesHighlight;
    [Tooltip("「NO」ハイライト (EN)")]
    public Image noHighlight;

    // --- 入力設定 ---
    [Header("■ 入力設定")]
    [Tooltip("M5StickCからの静電容量値がこの値を超えると『握った』と判定する")]
    [SerializeField] private int gripThreshold = 300;
    [Tooltip("傾き判定の閾値（度）")]
    [SerializeField] private float tiltThreshold = 15.0f;

    private AudioSource audioSource;
    private bool isYesSelected = true; // デフォルトはYes(左)
    private bool isInputActive = false; // 入力受付中かどうか
    private bool isEnglishMode = false; // 現在の言語モード

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();

        // 初期化：パネル類は非表示
        if (resultPanel != null) resultPanel.SetActive(false);
        if (confirmPanelJP != null) confirmPanelJP.SetActive(false);
        if (confirmPanelEN != null) confirmPanelEN.SetActive(false);
    }

    /// <summary>
    /// リザルト演出を開始する。
    /// 言語設定を確認し、適切なタイトルを表示する。
    /// </summary>
    public void ShowResult(int killCount, int starCount)
    {
        if (resultPanel == null) return;

        resultPanel.SetActive(true);

        // 確認パネルはまだ出さない
        if (confirmPanelJP != null) confirmPanelJP.SetActive(false);
        if (confirmPanelEN != null) confirmPanelEN.SetActive(false);

        // --- 言語判定とタイトルの切り替え ---
        isEnglishMode = false;
        if (LocalizationManager.Instance != null)
        {
            isEnglishMode = (LocalizationManager.Instance.CurrentLanguage == Language.English);
        }

        // タイトルの出し分け
        if (titleJP != null) titleJP.SetActive(!isEnglishMode);
        if (titleEN != null) titleEN.SetActive(isEnglishMode);

        // 初期化: 星をすべて「未獲得色」にする
        foreach (var star in starImages)
        {
            star.color = unearnedColor;
        }

        killCountText.text = "0";

        // アニメーションコルーチン開始
        StartCoroutine(ResultAnimationRoutine(killCount, starCount));
    }

    private IEnumerator ResultAnimationRoutine(int targetScore, int starCount)
    {
        yield return new WaitForSeconds(startDelay);

        // 1. スコアのカウントアップ演出
        float duration = 1.0f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = elapsed / duration;
            int currentDisplayScore = Mathf.FloorToInt(Mathf.Lerp(0, targetScore, progress));
            killCountText.text = currentDisplayScore.ToString();
            yield return null;
        }
        killCountText.text = targetScore.ToString();

        // 2. 星の表示演出
        for (int i = 0; i < starImages.Count; i++)
        {
            if (i < starCount)
            {
                yield return new WaitForSeconds(starInterval);
                starImages[i].color = earnedColor;
                if (starSound != null) audioSource.PlayOneShot(starSound);
            }
        }

        // 3. 演出終了後、指定時間待ってからパネルを表示
        yield return new WaitForSeconds(delayBeforeConfirmPanel);
        StartInputPhase();
    }

    // =========================================================
    // 入力受付・選択ロジック
    // =========================================================

    private void StartInputPhase()
    {
        // 言語に応じた確認パネルを表示
        if (isEnglishMode)
        {
            if (confirmPanelEN != null) confirmPanelEN.SetActive(true);
        }
        else
        {
            if (confirmPanelJP != null) confirmPanelJP.SetActive(true);
        }

        isYesSelected = true; // デフォルトYes (Left)
        UpdateSelectionVisuals();

        isInputActive = true;
        StartCoroutine(InputLoopCoroutine());
    }

    private IEnumerator InputLoopCoroutine()
    {
        // 誤爆防止のため、プレイヤーが手を離すまで待機する
        while (CheckGripInput())
        {
            yield return null;
        }

        while (isInputActive)
        {
            // 1. 選択の切り替え
            HandleSelectionInput();

            // 2. 決定
            if (CheckGripInput())
            {
                if (decideSound != null) audioSource.PlayOneShot(decideSound);
                isInputActive = false;
                ExecuteChoice();
                yield break;
            }

            yield return null;
        }
    }

    private void HandleSelectionInput()
    {
        float input = GetHorizontalInput();

        // 右入力 -> 右側の選択肢（No/いいえ）へ
        if (input > 0.5f && isYesSelected)
        {
            isYesSelected = false;
            if (selectSound != null) audioSource.PlayOneShot(selectSound);
            UpdateSelectionVisuals();
        }
        // 左入力 -> 左側の選択肢（Yes/はい）へ
        else if (input < -0.5f && !isYesSelected)
        {
            isYesSelected = true;
            if (selectSound != null) audioSource.PlayOneShot(selectSound);
            UpdateSelectionVisuals();
        }
    }

    private void ExecuteChoice()
    {
        if (stageManager == null) return;

        if (isYesSelected)
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
            // 英語版ハイライト更新
            if (yesHighlight != null) yesHighlight.gameObject.SetActive(isYesSelected);
            if (noHighlight != null) noHighlight.gameObject.SetActive(!isYesSelected);
        }
        else
        {
            // 日本語版ハイライト更新
            if (haiHighlight != null) haiHighlight.gameObject.SetActive(isYesSelected);
            if (iieHighlight != null) iieHighlight.gameObject.SetActive(!isYesSelected);
        }
    }

    // =========================================================
    // センサー・入力ヘルパー
    // =========================================================

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
        float key = Input.GetAxisRaw("Horizontal");
        if (Mathf.Abs(key) > 0.1f) return key;

        if (ArduinoInputManager.instance != null && ArduinoInputManager.instance.IsConnected)
        {
            Vector3 accel = ArduinoInputManager.RawAccel;
            float angle = -Mathf.Atan2(accel.x, accel.y) * Mathf.Rad2Deg;

            if (angle > tiltThreshold) return 1.0f;
            else if (angle < -tiltThreshold) return -1.0f;
        }
        return 0f;
    }
}