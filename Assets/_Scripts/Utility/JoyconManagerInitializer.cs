// JoyconManagerInitializer.cs (新規作成)

using UnityEngine;

public class JoyconManagerInitializer : MonoBehaviour
{
    void Awake()
    {
        // もしシーンにJoyconManagerのインスタンスが存在しなかったら
        if (JoyconManager.instance == null)
        {
            // 「JoyconManager」という名前で新しいGameObjectを作成し、
            // そこにJoyconManagerコンポーネントを追加する
            new GameObject("JoyconManager").AddComponent<JoyconManager>();
        }
    }

    // アプリケーションが終了する直前に呼び出される
    private void OnApplicationQuit()
    {
        if (JoyconManager.instance != null && JoyconManager.instance.j != null)
        {
            Debug.Log("<color=orange>Application quitting. Detaching all Joy-Cons...</color>");
            foreach (var joycon in JoyconManager.instance.j)
            {
                joycon.Detach();
            }
        }
    }
}