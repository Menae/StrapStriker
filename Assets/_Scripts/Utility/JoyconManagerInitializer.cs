using UnityEngine;

public class JoyconManagerInitializer : MonoBehaviour
{
    // このオブジェクトが破棄される時に呼び出される（エディタでの再生停止時に相当）
    private void OnDestroy()
    {
        CleanupJoycons();
    }

    // アプリケーションが完全に終了する直前に呼び出される（ビルド版の終了時に相当）
    private void OnApplicationQuit()
    {
        CleanupJoycons();
    }

    // 実際のクリーンアップ処理を行う共通メソッド
    private void CleanupJoycons()
    {
        // JoyconManagerのインスタンスが存在し、Joy-Conのリストがあれば
        if (JoyconManager.instance != null && JoyconManager.instance.j != null)
        {
            Debug.Log("<color=orange>Cleaning up Joy-Cons...</color>");

            // 接続されている全てのJoy-Conに対して、切断処理を呼び出す
            foreach (var joycon in JoyconManager.instance.j)
            {
                joycon.Detach();
            }

            // JoyconManagerのリストをクリアする
            JoyconManager.instance.j.Clear();
        }
    }
}