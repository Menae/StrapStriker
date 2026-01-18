using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// プレイ開始ごとにセンサーのキャリブレーションを実行するクラス。
/// 2本の銅箔テープ（センサー1, センサー2）それぞれの「握った状態」と「離した状態」を計測し、
/// そのセッション専用の閾値を設定する。
/// </summary>
public class SessionCalibration : MonoBehaviour
{
    [Header("UI参照")]
    [Tooltip("プレイヤーへの操作指示や計測状況を表示するテキスト")]
    public TextMeshProUGUI instructionText;

    [Tooltip("現在の信号レベルを可視化するスライダー（デバッグ確認用）")]
    public Slider signalSlider;

    [Tooltip("キャリブレーション中にゲーム画面を覆うパネル")]
    public GameObject overlayPanel;

    [Header("連携参照")]
    [Tooltip("計測結果（閾値）を適用するPlayerController")]
    public PlayerController playerController;

    [Tooltip("キャリブレーション完了後にチュートリアルを開始するStageManager")]
    public StageManager stageManager;

    [Header("計測設定")]
    [Tooltip("判定を確定させるために、状態を維持しなければならない時間（秒）")]
    public float measureDuration = 2.0f;

    [Tooltip("「握っている」と判定する信号値の閾値。両方のセンサーがこの値を上回り続ける必要がある。")]
    public float waitOnThreshold = 1000f;

    [Tooltip("「離している」と判定する信号値の閾値。両方のセンサーがこの値を下回り続ける必要がある。")]
    public float waitOffThreshold = 500f;

    [Header("デバッグ設定")]
    [Tooltip("Arduino未接続時にスペースキーで代用する際の仮想信号値")]
    public float debugVirtualValue = 3000f;

    /// <summary>
    /// シーン開始時にキャリブレーションシーケンスを開始する。
    /// </summary>
    private void Start()
    {
        StartCoroutine(CalibrationSequence());
    }

    /// <summary>
    /// 毎フレーム更新処理。
    /// 信号値をスライダーに反映し、入力状況を視覚的に確認可能にする。
    /// UI用には2つのセンサーの平均値を表示する。
    /// </summary>
    private void Update()
    {
        if (signalSlider != null)
        {
            signalSlider.value = GetAverageCurrentValue();
        }
    }

    /// <summary>
    /// 現在のセンサー値（2つのセンサーの平均）を取得する。
    /// UI表示用。
    /// </summary>
    private float GetAverageCurrentValue()
    {
        if (Input.GetKey(KeyCode.Space)) return debugVirtualValue;

        if (ArduinoInputManager.instance != null)
        {
            return (ArduinoInputManager.instance.SmoothedGripValue1 + ArduinoInputManager.instance.SmoothedGripValue2) / 2f;
        }

        return 0f;
    }

    /// <summary>
    /// キャリブレーションのメインフロー制御。
    /// 言語切り替えに対応するため、直接のテキストではなくキーを指定して指示を表示する。
    /// </summary>
    private IEnumerator CalibrationSequence()
    {
        // ゲーム操作をブロックするためオーバーレイを表示
        if (overlayPanel != null) overlayPanel.SetActive(true);

        float timer;
        float sum1, sum2;
        int count;

        // =================================================================
        // ステップ1: 握る状態（ON）の計測
        // =================================================================

        timer = 0f;
        sum1 = 0f;
        sum2 = 0f;
        count = 0;

        // 変更: 日本語を直接書かず、辞書のキーを指定
        SetInstruction("calib_grip_instruction");

        while (timer < measureDuration)
        {
            // 現在値の取得（Arduino未接続時は0またはデバッグ値）
            float val1 = 0f;
            float val2 = 0f;

            if (Input.GetKey(KeyCode.Space))
            {
                val1 = debugVirtualValue;
                val2 = debugVirtualValue;
            }
            else if (ArduinoInputManager.instance != null)
            {
                val1 = ArduinoInputManager.instance.SmoothedGripValue1;
                val2 = ArduinoInputManager.instance.SmoothedGripValue2;
            }

            // 両方のセンサーが閾値を超えているか判定
            if (val1 >= waitOnThreshold && val2 >= waitOnThreshold)
            {
                timer += Time.deltaTime;
                sum1 += val1;
                sum2 += val2;
                count++;

                float remainingTime = Mathf.Max(0f, measureDuration - timer);

                // 変更: 数値を埋め込むために、第二引数として remainingTime を渡す
                // 辞書側では "計測中... {0:F1} 秒" のように {0} で受け取る
                SetInstruction("calib_measuring_grip", remainingTime);
            }
            else
            {
                timer = 0f;
                sum1 = 0f;
                sum2 = 0f;
                count = 0;
                // リセット時もキーを指定
                SetInstruction("calib_grip_instruction");
            }

            yield return null;
        }

        // 平均値を算出
        float onValue1 = (count > 0) ? sum1 / count : debugVirtualValue;
        float onValue2 = (count > 0) ? sum2 / count : debugVirtualValue;

        // 変更: OKメッセージ
        SetInstruction("calib_ok");
        yield return new WaitForSeconds(1.0f);


        // =================================================================
        // ステップ2: 離す状態（OFF）の計測
        // =================================================================

        timer = 0f;
        sum1 = 0f;
        sum2 = 0f;
        count = 0;

        // 変更: 手を離す指示
        SetInstruction("calib_release_instruction");

        while (timer < measureDuration)
        {
            float val1 = 0f;
            float val2 = 0f;

            if (ArduinoInputManager.instance != null && !Input.GetKey(KeyCode.Space))
            {
                val1 = ArduinoInputManager.instance.SmoothedGripValue1;
                val2 = ArduinoInputManager.instance.SmoothedGripValue2;
            }

            // 両方のセンサーが閾値を下回っているか判定
            if (val1 < waitOffThreshold && val2 < waitOffThreshold)
            {
                timer += Time.deltaTime;
                sum1 += val1;
                sum2 += val2;
                count++;

                float remainingTime = Mathf.Max(0f, measureDuration - timer);

                // 変更: OFF計測中のカウントダウン
                SetInstruction("calib_measuring_release", remainingTime);
            }
            else
            {
                timer = 0f;
                sum1 = 0f;
                sum2 = 0f;
                count = 0;
                // リセット時
                SetInstruction("calib_release_instruction");
            }

            yield return null;
        }

        float offValue1 = (count > 0) ? sum1 / count : 0f;
        float offValue2 = (count > 0) ? sum2 / count : 0f;


        // =================================================================
        // ステップ3: 計測値の適用とゲーム開始
        // =================================================================

        // 変更: 完了メッセージ
        SetInstruction("calib_ready");

        // フェイルセーフ: 差分が小さすぎる場合は補正
        if (Mathf.Abs(onValue1 - offValue1) < 5f) onValue1 = offValue1 + 200f;
        if (Mathf.Abs(onValue2 - offValue2) < 5f) onValue2 = offValue2 + 200f;

        // PlayerControllerへ4つの値を注入（Min1, Max1, Min2, Max2）
        if (playerController != null)
        {
            playerController.SetCalibrationValues(offValue1, onValue1, offValue2, onValue2);
        }
        else
        {
            Debug.LogError("SessionCalibration: PlayerControllerの参照が設定されていません。");
        }

        yield return new WaitForSeconds(1.0f);

        if (overlayPanel != null) overlayPanel.SetActive(false);

        if (stageManager != null)
        {
            stageManager.ShowTutorial();
        }
        else
        {
            Debug.LogError("SessionCalibration: StageManagerの参照が設定されていません。");
        }
    }

    /// <summary>
    /// 指定されたキーに対応する翻訳テキストを表示する。
    /// 数値などの引数(args)がある場合は、{0}, {1} の部分に埋め込む。
    /// </summary>
    private void SetInstruction(string key, params object[] args)
    {
        if (instructionText != null && LocalizationManager.Instance != null)
        {
            // 1. マネージャーからフォーマット用の文字列をもらう
            string format = LocalizationManager.Instance.GetText(key);

            // 2. 引数がある場合は埋め込む、なければそのまま使う
            if (args != null && args.Length > 0)
            {
                // string.Formatを使って {0} の部分に数値をねじ込む
                instructionText.text = string.Format(format, args);
            }
            else
            {
                instructionText.text = format;
            }

            // 3. ついでにフォントも正しい言語のものに更新する
            instructionText.font = LocalizationManager.Instance.GetCurrentFont();
        }
    }
}