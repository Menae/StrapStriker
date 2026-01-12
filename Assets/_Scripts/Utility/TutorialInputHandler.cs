using UnityEngine;

/// <summary>
/// チュートリアル状態でのゲーム開始入力を検知する。
/// Arduino握力センサーまたはスペースキー入力でStageManagerにゲーム開始を通知する。
/// </summary>
public class TutorialInputHandler : MonoBehaviour
{
    [Header("設定")]
    [Tooltip("この値以上の握力でゲームを開始します")]
    public int gripThreshold = 20;

    private StageManager stageManager;
    private bool gameStarted = false;

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
        if (gameStarted || stageManager == null)
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
            gameStarted = true;
            Debug.Log("<color=cyan>Game Start triggered!</color>");
            stageManager.StartGame();
        }
    }
}