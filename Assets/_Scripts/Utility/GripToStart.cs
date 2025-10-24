using UnityEngine;

// SceneLoaderButtonと同じゲームオブジェクトにアタッチすることを想定
[RequireComponent(typeof(SceneLoaderButton))]
public class GripToStart : MonoBehaviour
{
    [Header("握力設定")]
    [Tooltip("この値以上の握力でゲームを開始します")]
    public int gripThreshold = 500;

    // 連携するSceneLoaderButtonへの参照
    private SceneLoaderButton sceneLoaderButton;

    // 多重でシーン遷移が呼ばれるのを防ぐためのフラグ
    private bool isLoading = false;

    void Start()
    {
        // 同じゲームオブジェクトにアタッチされているSceneLoaderButtonを取得
        sceneLoaderButton = GetComponent<SceneLoaderButton>();
    }

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
            // 握力センサーの値をチェックする
            if (ArduinoInputManager.GripValue > gripThreshold)
            {
                isLoading = true;
                Debug.Log($"<color=cyan>Grip detected! Loading next scene...</color>");
                sceneLoaderButton.LoadTargetScene();
            }
        }
        // それ以外の場合（マネージャーが存在しない、または接続に失敗している場合）
        else
        {
            // デバッグ用のスペースキー入力をチェックする
            if (Input.GetKeyDown(KeyCode.Space))
            {
                isLoading = true;
                Debug.Log("<color=yellow>Debug: Loading next scene on Space key press.</color>");
                sceneLoaderButton.LoadTargetScene();
            }
        }
    }
}