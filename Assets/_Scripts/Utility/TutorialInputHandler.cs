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

    /// <summary>
    /// 初期化処理。
    /// シーン内のStageManagerを自動検索して参照を取得する。
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
    /// ゲーム未開始時にArduino握力またはスペースキー入力を監視し、
    /// しきい値を超えたらStageManager.StartGame()を呼び出す。
    /// 複数回呼び出しを防ぐためgameStartedフラグで制御する。
    /// </summary>
    void Update()
    {
        if (gameStarted || stageManager == null)
        {
            return;
        }

        bool arduinoInput = (ArduinoInputManager.instance != null && ArduinoInputManager.GripValue >= gripThreshold);
        bool keyboardInput = Input.GetKeyDown(KeyCode.Space);

        if (arduinoInput || keyboardInput)
        {
            gameStarted = true;
            Debug.Log("<color=cyan>Game Start triggered!</color>");
            stageManager.StartGame();
        }
    }
}