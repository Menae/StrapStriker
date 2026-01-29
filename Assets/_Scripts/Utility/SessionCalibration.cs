using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// プレイ開始時にセンサーのキャリブレーション（補正）を実行するクラス。
/// センサーの個体差や環境湿度による入力値のオフセットを吸収するため、
/// 実際の入力値（最小値・最大値）を計測し、動的に判定閾値を設定する。
/// </summary>
public class SessionCalibration : MonoBehaviour
{
    [Header("UI Reference")]
    [Tooltip("操作指示や計測状況を表示するテキスト")]
    public TextMeshProUGUI instructionText;
    [Tooltip("現在の信号レベル確認用スライダー")]
    public Slider signalSlider;
    [Tooltip("キャリブレーション中の入力ブロック用パネル")]
    public GameObject overlayPanel;

    [Header("System Reference")]
    public PlayerController playerController;
    public StageManager stageManager;

    [Header("Measurement Settings")]
    [Tooltip("計測を行う時間（秒）。この間の平均値を採用する")]
    public float measureDuration = 2.0f;
    [Tooltip("計測開始前の準備待機時間（秒）。指示が出てから計測を始めるまでの猶予")]
    public float prepareDuration = 5.0f;

    [Header("Debug")]
    [Tooltip("Arduino未接続時のデバッグ用仮想値")]
    public float debugVirtualValue = 3000f;

    private void Start()
    {
        StartCoroutine(CalibrationSequence());
    }

    private void Update()
    {
        // 信号レベルの可視化更新
        if (signalSlider != null)
        {
            signalSlider.value = GetAverageCurrentValue();
        }
    }

    /// <summary>
    /// 現在のセンサー値（2系統の平均）を取得する。
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
    /// キャリブレーションの実行シーケンス。
    /// 「離す（最小値）」→「握る（最大値）」の順で計測を行い、正常なダイナミックレンジを確保する。
    /// </summary>
    private IEnumerator CalibrationSequence()
    {
        if (overlayPanel != null) overlayPanel.SetActive(true);

        float timer;
        float sum1, sum2;
        int count;

        // ---------------------------------------------------------
        // Step 0: 開始シーケンス (導入)
        // ---------------------------------------------------------

        // 1. 開始アナウンス
        SetInstruction("calib_start_msg");
        yield return new WaitForSeconds(1.5f);

        // 2. 操作説明の導入（心の準備）
        SetInstruction("calib_intro_instruction");
        yield return new WaitForSeconds(3.0f);


        // ---------------------------------------------------------
        // Step 1: 最小値（Release/OFF）の計測
        // リラックス状態を基準点（ゼロ点）として記録する
        // ---------------------------------------------------------

        // A. 準備フェーズ（カウントダウン）
        timer = 0f;
        while (timer < prepareDuration)
        {
            timer += Time.deltaTime;
            float remaining = Mathf.Max(0f, prepareDuration - timer);

            string instruction = LocalizationManager.Instance.GetText("calib_release_instruction");
            SetInstruction("calib_prepare_format", instruction, remaining);

            yield return null;
        }

        // B. 計測フェーズ
        timer = 0f;
        sum1 = 0f; sum2 = 0f;
        count = 0;

        while (timer < measureDuration)
        {
            timer += Time.deltaTime;

            float val1 = 0f;
            float val2 = 0f;

            if (Input.GetKey(KeyCode.Space))
            {
                // Spaceキー非押下時は0（離した状態）をシミュレート
                val1 = 0f; val2 = 0f;
            }
            else if (ArduinoInputManager.instance != null)
            {
                val1 = ArduinoInputManager.instance.SmoothedGripValue1;
                val2 = ArduinoInputManager.instance.SmoothedGripValue2;
            }

            sum1 += val1;
            sum2 += val2;
            count++;

            SetInstruction("calib_measuring_release", Mathf.Max(0f, measureDuration - timer));
            yield return null;
        }

        // 最小値の確定
        float offValue1 = (count > 0) ? sum1 / count : 0f;
        float offValue2 = (count > 0) ? sum2 / count : 0f;

        SetInstruction("calib_ok");
        yield return new WaitForSeconds(1.0f);


        // ---------------------------------------------------------
        // Step 2: 最大値（Grip/ON）の計測
        // ---------------------------------------------------------

        // A. 準備フェーズ（カウントダウン）
        timer = 0f;
        while (timer < prepareDuration)
        {
            timer += Time.deltaTime;
            float remaining = Mathf.Max(0f, prepareDuration - timer);

            string instruction = LocalizationManager.Instance.GetText("calib_grip_instruction");
            SetInstruction("calib_prepare_format", instruction, remaining);

            yield return null;
        }

        // B. 計測フェーズ
        timer = 0f;
        sum1 = 0f; sum2 = 0f;
        count = 0;

        while (timer < measureDuration)
        {
            timer += Time.deltaTime;

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

            sum1 += val1;
            sum2 += val2;
            count++;

            SetInstruction("calib_measuring_grip", Mathf.Max(0f, measureDuration - timer));
            yield return null;
        }

        // 最大値の確定
        float onValue1 = (count > 0) ? sum1 / count : debugVirtualValue;
        float onValue2 = (count > 0) ? sum2 / count : debugVirtualValue;


        // ---------------------------------------------------------
        // Step 3: 値の検証と適用
        // ---------------------------------------------------------

        SetInstruction("calib_ready");

        // フェイルセーフ:
        // 入力値の変動幅が小さすぎる場合（未操作や断線等）は強制的にマージンを設ける
        float minDiff = 200f;

        if ((onValue1 - offValue1) < minDiff)
        {
            Debug.LogWarning("Calibration Warning: Sensor 1 range too narrow. Auto-adjusting margin.");
            onValue1 = offValue1 + 1000f;
        }

        if ((onValue2 - offValue2) < minDiff)
        {
            Debug.LogWarning("Calibration Warning: Sensor 2 range too narrow. Auto-adjusting margin.");
            onValue2 = offValue2 + 1000f;
        }

        Debug.Log($"Calibration Result -- S1: {offValue1:F0}-{onValue1:F0} / S2: {offValue2:F0}-{onValue2:F0}");

        // 結果をコントローラーへ反映
        if (playerController != null)
        {
            playerController.SetCalibrationValues(offValue1, onValue1, offValue2, onValue2);
        }
        else
        {
            Debug.LogError("SessionCalibration: PlayerController ref is missing.");
        }

        yield return new WaitForSeconds(1.0f);

        // 終了処理
        if (overlayPanel != null) overlayPanel.SetActive(false);

        if (stageManager != null)
        {
            stageManager.ShowTutorial();
        }
        else
        {
            Debug.LogError("SessionCalibration: StageManager ref is missing.");
        }
    }

    /// <summary>
    /// ローカライズキーを指定してUIテキストを更新する。
    /// 引数(args)がある場合はフォーマット文字列に埋め込む。
    /// </summary>
    private void SetInstruction(string key, params object[] args)
    {
        if (instructionText != null && LocalizationManager.Instance != null)
        {
            string format = LocalizationManager.Instance.GetText(key);
            if (args != null && args.Length > 0)
            {
                instructionText.text = string.Format(format, args);
            }
            else
            {
                instructionText.text = format;
            }
            instructionText.font = LocalizationManager.Instance.GetCurrentFont();
        }
    }
}