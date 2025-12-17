using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// プレイ開始ごとにセンサーのキャリブレーションを実行するクラス。
/// プレイヤーごとの握力や皮膚抵抗の個人差、環境によるノイズを吸収するため、
/// ゲーム開始直前に「握った状態」と「離した状態」の信号値を計測し、
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

    [Tooltip("「握っている」と判定する信号値の閾値。この値を上回り続ける必要がある。")]
    public float waitOnThreshold = 1000f;

    [Tooltip("「離している」と判定する信号値の閾値。この値を下回り続ける必要がある。")]
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
    /// </summary>
    private void Update()
    {
        if (signalSlider != null)
        {
            signalSlider.value = GetCurrentValue();
        }
    }

    /// <summary>
    /// 現在のセンサー値を取得する。
    /// デバッグ用にスペースキー入力でのオーバーライド処理を含む。
    /// </summary>
    /// <returns>平滑化されたセンサー値、またはデバッグ用の仮想値</returns>
    private float GetCurrentValue()
    {
        // デバッグ入力: スペースキー押下時は最大値を返す
        if (Input.GetKey(KeyCode.Space))
        {
            return debugVirtualValue;
        }

        // 本番入力: ArduinoInputManagerから値を取得
        if (ArduinoInputManager.instance != null)
        {
            return ArduinoInputManager.instance.SmoothedGripValue;
        }

        return 0f;
    }

    /// <summary>
    /// キャリブレーションのメインフロー制御。
    /// 「握る（ON）」と「離す（OFF）」の各状態を指定時間維持させ、その平均値を計測する。
    /// 途中で状態が解除された場合は計測をリセットし、確実な入力を保証する。
    /// </summary>
    private IEnumerator CalibrationSequence()
    {
        // ゲーム操作をブロックするためオーバーレイを表示
        if (overlayPanel != null) overlayPanel.SetActive(true);

        float timer;
        float sum;
        int count;

        // =================================================================
        // ステップ1: 握る状態（ON）の計測
        // =================================================================

        timer = 0f;
        sum = 0f;
        count = 0;

        // 指定時間を満了するまでループし、継続的な入力を監視する
        while (timer < measureDuration)
        {
            float currentVal = GetCurrentValue();

            // 信号値が閾値を超えているか判定
            if (currentVal >= waitOnThreshold)
            {
                // 条件を満たしている場合、計測を進める
                timer += Time.deltaTime;
                sum += currentVal;
                count++;

                // ユーザーに進捗状況を提示
                float remainingTime = Mathf.Max(0f, measureDuration - timer);
                SetInstruction($"計測中... あと <size=120%>{remainingTime:F1}</size> 秒\n<color=yellow>そのまま握っていて！</color>");
            }
            else
            {
                // 条件を満たさなくなった場合、計測をリセットして最初からやり直す
                timer = 0f;
                sum = 0f;
                count = 0;

                SetInstruction("つり革を\nギュッと握ってください");
            }

            yield return null;
        }

        // 計測完了: 平均値を算出（データがない場合はデバッグ値を採用）
        float onValue = (count > 0) ? sum / count : debugVirtualValue;

        SetInstruction("OK!");
        yield return new WaitForSeconds(1.0f);


        // =================================================================
        // ステップ2: 離す状態（OFF）の計測
        // =================================================================

        timer = 0f;
        sum = 0f;
        count = 0;

        while (timer < measureDuration)
        {
            float currentVal = GetCurrentValue();

            // 信号値が閾値を下回っているか判定
            if (currentVal < waitOffThreshold)
            {
                // 条件を満たしている場合、計測を進める
                timer += Time.deltaTime;
                sum += currentVal;
                count++;

                float remainingTime = Mathf.Max(0f, measureDuration - timer);
                SetInstruction($"計測中... あと <size=120%>{remainingTime:F1}</size> 秒\n<color=yellow>そのまま手を離していて！</color>");
            }
            else
            {
                // 手が触れてしまった場合、計測をリセット
                timer = 0f;
                sum = 0f;
                count = 0;

                SetInstruction("手を離して\nリラックスしてください");
            }

            yield return null;
        }

        // 計測完了: 平均値を算出
        float offValue = (count > 0) ? sum / count : 0f;


        // =================================================================
        // ステップ3: 計測値の適用とゲーム開始
        // =================================================================

        SetInstruction("準備完了！");

        // 計測値の差分が極端に小さい場合のフェイルセーフ処理
        // センサー異常時でもゲーム進行を妨げないための暫定値を設定
        if (Mathf.Abs(onValue - offValue) < 5f)
        {
            onValue = offValue + 200f;
        }

        // 計測結果をPlayerControllerへ注入
        if (playerController != null)
        {
            playerController.SetCalibrationValues(offValue, onValue);
        }
        else
        {
            Debug.LogError("SessionCalibration: PlayerControllerの参照が設定されていません。");
        }

        yield return new WaitForSeconds(1.0f);

        // キャリブレーション画面を閉じ、ゲーム本編のチュートリアルへ移行
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
    /// 画面上の指示テキストを更新する。
    /// </summary>
    /// <param name="message">表示するメッセージ文字列</param>
    private void SetInstruction(string message)
    {
        if (instructionText != null)
        {
            instructionText.text = message;
        }
    }
}