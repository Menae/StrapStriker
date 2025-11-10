using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;

public class SceneFader : MonoBehaviour
{
    public static SceneFader instance;

    [Tooltip("フェードに使うUIのImageコンポーネント")]
    public Image fadeImage;
    [Tooltip("デフォルトのフェード時間")]
    public float defaultFadeDuration = 1.0f;
    [Tooltip("フェードの色")]
    public Color fadeColor = Color.black;

    private void Awake()
    {
        // シングルトンパターンの実装
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject); // シーンをまたいでも破棄されないようにする
        }
        else
        {
            Destroy(gameObject); // 既にインスタンスが存在する場合は破棄
        }
    }

    private void OnEnable()
    {
        // シーンがロードされた時に自動でフェードインが始まるようにイベントを登録
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // シーンロード時に実行されるメソッド
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        StartCoroutine(FadeInCoroutine(defaultFadeDuration));
    }

    /// <summary>
    /// 他のスクリプトからシーン遷移を呼び出すための公開メソッド
    /// </summary>
    public void LoadSceneWithFade(string sceneName)
    {
        StartCoroutine(FadeOutCoroutine(sceneName, defaultFadeDuration));
    }

    /// <summary>
    /// フェード時間を指定してシーン遷移を呼び出す
    /// </summary>
    public void LoadSceneWithFade(string sceneName, float duration)
    {
        StartCoroutine(FadeOutCoroutine(sceneName, duration));
    }

    private IEnumerator FadeOutCoroutine(string sceneName, float duration)
    {
        fadeImage.gameObject.SetActive(true);
        fadeImage.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 0f);

        float startTime = Time.realtimeSinceStartup; // 現実世界の開始時刻を記録
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            // 開始時刻からの現実の経過時間を毎フレーム計算
            elapsedTime = Time.realtimeSinceStartup - startTime;
            float alpha = Mathf.Lerp(0f, 1f, elapsedTime / duration);
            fadeImage.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, alpha);
            yield return null;
        }

        fadeImage.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 1f);
        SceneManager.LoadScene(sceneName);
    }

    private IEnumerator FadeInCoroutine(float duration)
    {
        fadeImage.gameObject.SetActive(true);
        fadeImage.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 1f);

        // まず、シーンロード直後の不安定な1フレームをやり過ごす
        yield return null;

        // 完全に安定した次のフレームで、改めて開始時刻を記録する
        float startTime = Time.realtimeSinceStartup;
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime = Time.realtimeSinceStartup - startTime;
            float alpha = Mathf.Lerp(1f, 0f, elapsedTime / duration);
            fadeImage.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, alpha);
            yield return null;
        }

        fadeImage.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 0f);
        fadeImage.gameObject.SetActive(false);
    }
}