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
        // まだシーン遷移中でなく、かつArduinoが接続されている場合
        if (!isLoading && ArduinoInputManager.instance != null)
        {
            // 握力センサーの値がしきい値を超えたかチェック
            if (ArduinoInputManager.GripValue > gripThreshold)
            {
                // しきい値を超えたら、フラグを立てて多重実行を防止
                isLoading = true;

                Debug.Log($"<color=cyan>Grip detected! Value: {ArduinoInputManager.GripValue}. Loading next scene...</color>");

                // SceneLoaderButtonにシーン遷移を命令する
                sceneLoaderButton.LoadTargetScene();
            }
        }
    }
}