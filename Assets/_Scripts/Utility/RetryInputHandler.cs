using UnityEngine;

public class RetryInputHandler : MonoBehaviour
{
    [Header("設定")]
    [Tooltip("この値以上の握力でリトライします")]
    public int gripThreshold = 20;

    // StageManagerへの参照を保持
    private StageManager stageManager;

    // 多重でリトライが呼ばれるのを防ぐためのフラグ
    private bool isRetrying = false;

    void Start()
    {
        // シーン内のStageManagerを自動で見つけておく
        stageManager = FindObjectOfType<StageManager>();
        if (stageManager == null)
        {
            Debug.LogError("シーン内にStageManagerが見つかりません！");
        }
    }

    void Update()
    {
        // 既にリトライ処理中か、StageManagerが見つからなければ何もしない
        if (isRetrying || stageManager == null)
        {
            return;
        }

        // Arduinoの握力がしきい値以上、またはスペースキーが押されたかをチェック
        bool arduinoInput = (ArduinoInputManager.instance != null && ArduinoInputManager.GripValue >= gripThreshold);
        bool keyboardInput = Input.GetKeyDown(KeyCode.Space);

        if (arduinoInput || keyboardInput)
        {
            // 多重実行を防止
            isRetrying = true;

            Debug.Log("<color=cyan>Retry triggered!</color>");

            // StageManagerにリトライを命令する
            stageManager.RetryStage();
        }
    }
}