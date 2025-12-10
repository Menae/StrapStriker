using UnityEngine;

/// <summary>
/// 握力センサーの値に応じてシーン遷移を実行する。
/// 閾値を超えた握力を検知するか、デバッグモード時はスペースキーで次シーンへ移動。
/// </summary>
[RequireComponent(typeof(SceneLoaderButton))]
public class GripToStart : MonoBehaviour
{
    [Header("握力設定")]
    [Tooltip("この値以上の握力でゲームを開始します")]
    public int gripThreshold = 500;

    /// <summary>
    /// 連携するSceneLoaderButtonへの参照。
    /// Start()で同一GameObject上から取得。
    /// </summary>
    private SceneLoaderButton sceneLoaderButton;

    /// <summary>
    /// シーン遷移処理の多重実行を防ぐフラグ。
    /// true の場合、以降の入力をすべて無視。
    /// </summary>
    private bool isLoading = false;

    /// <summary>
    /// 初期化処理。GameObject起動時に1回だけ呼ばれる。
    /// 同一GameObject上のSceneLoaderButtonコンポーネントを取得。
    /// </summary>
    void Start()
    {
        sceneLoaderButton = GetComponent<SceneLoaderButton>();
    }

    /// <summary>
    /// 毎フレーム呼ばれる更新処理。
    /// Arduino接続時は握力センサー値、未接続時はスペースキー入力を監視し、
    /// 条件を満たした場合にシーン遷移を実行する。
    /// </summary>
    void Update()
    {
        // 既にシーン遷移中なら何もしない
        if (isLoading)
        {
            return;
        }

        // Arduinoマネージャーが存在し、かつArduinoとの接続に成功している場合
        if (ArduinoInputManager.instance != null && ArduinoInputManager.instance.IsConnected)
        {
            // 握力センサーの値が閾値を超えたらシーン遷移を開始
            if (ArduinoInputManager.GripValue > gripThreshold)
            {
                isLoading = true;
                Debug.Log($"<color=cyan>Grip detected! Loading next scene...</color>");
                sceneLoaderButton.LoadTargetScene();
            }
        }
        // マネージャーが存在しない、または接続に失敗している場合
        else
        {
            // デバッグ用：スペースキー入力でシーン遷移を開始
            if (Input.GetKeyDown(KeyCode.Space))
            {
                isLoading = true;
                Debug.Log("<color=yellow>Debug: Loading next scene on Space key press.</color>");
                sceneLoaderButton.LoadTargetScene();
            }
        }
    }
}