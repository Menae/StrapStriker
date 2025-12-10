using UnityEngine;

/// <summary>
/// 握力入力またはキーボード入力によるリトライ機能を提供する。
/// StageManagerと連携し、ゲームオーバー時などのステージ再挑戦を制御する。
/// </summary>
public class RetryInputHandler : MonoBehaviour
{
    [Header("設定")]
    [Tooltip("この値以上の握力でリトライします")]
    public int gripThreshold = 20;

    private StageManager stageManager;
    private bool isRetrying = false;

    /// <summary>
    /// 初期化処理。
    /// シーン内のStageManagerを自動検索し、参照を保持する。
    /// 見つからない場合はエラーログを出力する。
    /// </summary>
    void Start()
    {
        stageManager = FindObjectOfType<StageManager>();
        if (stageManager == null)
        {
            Debug.LogError("シーン内にStageManagerが見つかりません！");
        }
    }

    /// <summary>
    /// 毎フレーム実行される更新処理。
    /// Arduino握力入力またはスペースキー入力を監視し、しきい値を超えた場合にリトライを実行する。
    /// 多重実行を防ぐため、一度リトライが発動すると次回以降は入力を受け付けない。
    /// </summary>
    void Update()
    {
        if (isRetrying || stageManager == null)
        {
            return;
        }

        bool arduinoInput = (ArduinoInputManager.instance != null && ArduinoInputManager.GripValue >= gripThreshold);
        bool keyboardInput = Input.GetKeyDown(KeyCode.Space);

        if (arduinoInput || keyboardInput)
        {
            isRetrying = true;
            Debug.Log("<color=cyan>Retry triggered!</color>");
            stageManager.RetryStage();
        }
    }
}