using UnityEngine;

public class QuitButtonHandler : MonoBehaviour
{
    public void QuitGame()
    {
        Debug.Log("ゲームを終了しようとしています...");

        // Unityエディタで実行している場合
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;

        // WebGLビルドの場合 (ブラウザータブはスクリプトから閉じられない)
#elif UNITY_WEBGL
        Debug.Log("WebGLではアプリケーションを終了できません。");

        // それ以外のプラットフォーム(PC, Mac, Linuxなど)で実行している場合
#else
        Application.Quit();
#endif
    }
}