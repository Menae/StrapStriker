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

    void Start()
    {
        stageManager = FindObjectOfType<StageManager>();
        if (stageManager == null)
        {
            Debug.LogError("シーン内にStageManagerが見つかりません！");
        }
    }

    void Update()
    {
        if (isRetrying || stageManager == null)
        {
            return;
        }

        bool arduinoInput = false;
        if (ArduinoInputManager.instance != null)
        {
            // 両方のセンサー値が閾値を超えているか確認
            arduinoInput = (ArduinoInputManager.GripValue1 >= gripThreshold &&
                            ArduinoInputManager.GripValue2 >= gripThreshold);
        }

        bool keyboardInput = Input.GetKeyDown(KeyCode.Space);

        if (arduinoInput || keyboardInput)
        {
            isRetrying = true;
            Debug.Log("<color=cyan>Retry triggered!</color>");
            stageManager.RetryStage();
        }
    }
}