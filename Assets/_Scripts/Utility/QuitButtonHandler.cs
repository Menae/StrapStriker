using UnityEngine;

/// <summary>
/// ゲーム終了ボタンの処理を管理するハンドラー。
/// プラットフォームに応じて適切な終了処理を実行する。
/// </summary>
public class QuitButtonHandler : MonoBehaviour
{
    /// <summary>
    /// ゲームを終了する。
    /// エディタ実行時は再生モードを停止、WebGLでは警告ログのみ、スタンドアロンビルドではアプリケーションを終了する。
    /// </summary>
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