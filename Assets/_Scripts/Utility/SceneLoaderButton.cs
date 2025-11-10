using UnityEngine;

public class SceneLoaderButton : MonoBehaviour
{
    [Tooltip("このボタンが遷移させるシーンの名前")]
    [SerializeField] private string sceneNameToLoad;

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