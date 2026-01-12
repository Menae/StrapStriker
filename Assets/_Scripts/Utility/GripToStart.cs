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

    private SceneLoaderButton sceneLoaderButton;
    private bool isLoading = false;

    void Start()
    {
        sceneLoaderButton = GetComponent<SceneLoaderButton>();
    }

    void Update()
    {
        if (isLoading)
        {
            return;
        }

        if (ArduinoInputManager.instance != null && ArduinoInputManager.instance.IsConnected)
        {
            // 両方のセンサーが閾値を超えたらシーン遷移を開始
            if (ArduinoInputManager.GripValue1 > gripThreshold &&
                ArduinoInputManager.GripValue2 > gripThreshold)
            {
                isLoading = true;
                Debug.Log($"<color=cyan>Grip detected! Loading next scene...</color>");
                sceneLoaderButton.LoadTargetScene();
            }
        }
        else
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                isLoading = true;
                Debug.Log("<color=yellow>Debug: Loading next scene on Space key press.</color>");
                sceneLoaderButton.LoadTargetScene();
            }
        }
    }
}