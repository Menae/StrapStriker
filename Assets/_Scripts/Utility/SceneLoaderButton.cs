using UnityEngine;

/// <summary>
/// ボタンクリック時に指定シーンへフェード遷移を行うコンポーネント
/// </summary>
public class SceneLoaderButton : MonoBehaviour
{
    /// <summary>
    /// 遷移先のシーン名（Inspectorで設定）
    /// </summary>
    [Tooltip("このボタンが遷移させるシーンの名前")]
    [SerializeField] private string sceneNameToLoad;

    /// <summary>
    /// SceneFaderを経由してシーン遷移を実行する
    /// SceneFaderが存在しない場合はエラーログを出力して遷移しない
    /// </summary>
    public void LoadTargetScene()
    {
        // SceneFaderがシーンに存在するか確認
        if (SceneFader.instance != null)
        {
            // シングルトンのSceneFaderに、Inspectorで設定したシーンへの遷移をお願いする
            SceneFader.instance.LoadSceneWithFade(sceneNameToLoad);
        }
        else
        {
            Debug.LogError("シーン内にSceneFaderが見つかりません！");
        }
    }
}