using UnityEngine;

public class TutorialInputHandler : MonoBehaviour
{
    [Header("設定")]
    [Tooltip("この値以上の握力でゲームを開始します")]
    public int gripThreshold = 20;

    // StageManagerへの参照を保持
    private StageManager stageManager;

    // ゲームが既に開始されたかを管理するフラグ
    private bool gameStarted = false;

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
        // 既にゲームが始まっているか、StageManagerが見つからなければ何もしない
        if (gameStarted || stageManager == null)
        {
            return;
        }

        // Arduinoの握力がしきい値以上、またはスペースキーが押されたかをチェック
        bool arduinoInput = (ArduinoInputManager.instance != null && ArduinoInputManager.GripValue >= gripThreshold);
        bool keyboardInput = Input.GetKeyDown(KeyCode.Space);

        if (arduinoInput || keyboardInput)
        {
            // StartGame()を複数回呼び出すのを防ぐ
            gameStarted = true;

            Debug.Log("<color=cyan>Game Start triggered!</color>");

            // StageManagerにゲーム開始を命令する
            stageManager.StartGame();
        }
    }
}