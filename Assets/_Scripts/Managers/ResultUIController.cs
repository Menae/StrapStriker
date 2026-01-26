using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// リザルト画面の表示演出および、その後のリトライ確認入力を管理するクラス。
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

    [Header("■ 星アイコン設定")]
    public List<Image> starImages;
    public Color earnedColor = new Color(1f, 0.8f, 0f, 1f);
    public Color unearnedColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);

    [Header("■ アニメーション設定")]
    public float startDelay = 0.5f;
    public float starInterval = 0.4f;
    public AudioClip starSound;
    [Tooltip("選択時のSE")]
    public AudioClip selectSound;
    [Tooltip("決定時のSE")]
    public AudioClip decideSound;

    // --- 追加: リトライ確認用UI ---
    [Header("■ リトライ確認UI")]
    [Tooltip("「もう一度プレイしますか？」等のパネル")]
    public GameObject confirmPanel;

    [Tooltip("「はい」選択時のハイライト画像")]
    public Image yesHighlightImage;
    [Tooltip("「いいえ」選択時のハイライト画像")]
    public Image noHighlightImage;

    // --- 追加: 入力設定 (TitleFlowManagerから移植) ---
    [Header("■ 入力設定")]
    [Tooltip("M5StickCからの静電容量値がこの値を超えると『握った』と判定する")]
    [SerializeField] private int gripThreshold = 300;
    [Tooltip("傾き判定の閾値（度）")]
    [SerializeField] private float tiltThreshold = 15.0f;
    [Tooltip("入力開始までのディレイ（演出終了後、誤操作防止のため）")]
    [SerializeField] private float inputStartDelay = 1.0f;

    private AudioSource audioSource;
    private bool isYesSelected = true; // デフォルトはYes
    private bool isInputActive = false; // 入力受付中かどうか

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();

        // 初期化：パネル類は非表示
        if (resultPanel != null) resultPanel.SetActive(false);
        if (confirmPanel != null) confirmPanel.SetActive(false);
    }

    /// <summary>
    /// リザルト演出を開始する。
    /// StageManagerから呼ばれる。
    /// </summary>
    public void ShowResult(int killCount, int starCount)
    {
        if (resultPanel == null) return;

        resultPanel.SetActive(true);
        // 確認パネルはまだ出さない
        if (confirmPanel != null) confirmPanel.SetActive(false);

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

        // 3. 演出終了後、少し待ってから入力受付モードへ移行
        yield return new WaitForSeconds(inputStartDelay);
        StartInputPhase();
    }

    // =========================================================
    // 入力受付・選択ロジック
    // =========================================================

    private void StartInputPhase()
    {
        if (confirmPanel != null) confirmPanel.SetActive(true);

        isYesSelected = true; // デフォルトYes
        UpdateSelectionVisuals();

        isInputActive = true;
        StartCoroutine(InputLoopCoroutine());
    }

    private IEnumerator InputLoopCoroutine()
    {
        // 誤爆防止のため、プレイヤーが手を離すまで待機する（握りっぱなし防止）
        while (CheckGripInput())
        {
            yield return null;
        }

        while (isInputActive)
        {
            // 1. 選択の切り替え (傾き or キーボード)
            HandleSelectionInput();

            // 2. 決定 (握る or Space)
            if (CheckGripInput())
            {
                // 決定処理
                if (decideSound != null) audioSource.PlayOneShot(decideSound);

                isInputActive = false; // 入力ロック
                ExecuteChoice();
                yield break;
            }

            yield return null; // 次のフレームへ
        }
    }

    private void HandleSelectionInput()
    {
        float input = GetHorizontalInput();

        // 右入力 (D / 傾き右) -> 右側の選択肢（No）へ移動
        if (input > 0.5f && isYesSelected)
        {
            isYesSelected = false;
            if (selectSound != null) audioSource.PlayOneShot(selectSound);
            UpdateSelectionVisuals();
        }
        // 左入力 (A / 傾き左) -> 左側の選択肢（Yes）へ移動
        else if (input < -0.5f && !isYesSelected)
        {
            isYesSelected = true;
            if (selectSound != null) audioSource.PlayOneShot(selectSound);
            UpdateSelectionVisuals();
        }
    }

    private void ExecuteChoice()
    {
        if (stageManager == null)
        {
            Debug.LogError("ResultUIController: StageManagerの参照がありません！");
            return;
        }

        if (isYesSelected)
        {
            Debug.Log("リトライを選択");
            stageManager.RetryStage();
        }
        else
        {
            Debug.Log("タイトルへ戻るを選択");
            stageManager.ReturnToTitle();
        }
    }

    private void UpdateSelectionVisuals()
    {
        // Yesが選択されているならYes画像をオン、No画像をオフ
        if (yesHighlightImage != null) yesHighlightImage.gameObject.SetActive(isYesSelected);
        if (noHighlightImage != null) noHighlightImage.gameObject.SetActive(!isYesSelected);
    }

    // =========================================================
    // センサー・入力ヘルパー (TitleFlowManager互換)
    // =========================================================

    private bool CheckGripInput()
    {
        // デバッグ用キーボード入力
        if (Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.Return)) return true;

        // Arduino入力
        if (ArduinoInputManager.instance != null && ArduinoInputManager.instance.IsConnected)
        {
            return (ArduinoInputManager.GripValue1 > gripThreshold &&
                    ArduinoInputManager.GripValue2 > gripThreshold);
        }
        return false;
    }

    private float GetHorizontalInput()
    {
        // デバッグ用キーボード入力
        float key = Input.GetAxisRaw("Horizontal");
        if (Mathf.Abs(key) > 0.1f) return key;

        // Arduino 加速度入力
        if (ArduinoInputManager.instance != null && ArduinoInputManager.instance.IsConnected)
        {
            Vector3 accel = ArduinoInputManager.RawAccel;
            // Z軸回りの回転などを考慮しつつ、簡易的にX/Yで傾きを取る
            // TitleFlowManagerと同様のロジック
            float angle = -Mathf.Atan2(accel.x, accel.y) * Mathf.Rad2Deg;

            // 閾値を超えたら 1.0 または -1.0 を返す
            if (angle > tiltThreshold) return 1.0f;       // 右傾き
            else if (angle < -tiltThreshold) return -1.0f; // 左傾き
        }
        return 0f;
    }
}