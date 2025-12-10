using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// シーン遷移時のフェードイン・アウト演出を管理するシングルトンクラス。
/// シーンをまたいで存在し続け、自動的にフェードイン演出を実行する。
/// </summary>
public class SceneFader : MonoBehaviour
{
    /// <summary>
    /// シングルトンインスタンス。他のスクリプトからアクセス可能。
    /// </summary>
    public static SceneFader instance;

    [Tooltip("フェードに使うUIのImageコンポーネント")]
    public Image fadeImage;

    [Tooltip("デフォルトのフェード時間")]
    public float defaultFadeDuration = 1.0f;

    [Tooltip("フェードの色")]
    public Color fadeColor = Color.black;

    /// <summary>
    /// Unityライフサイクルの初期化処理。
    /// シングルトンパターンを実装し、重複インスタンスを破棄する。
    /// </summary>
    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// オブジェクト有効化時にシーンロードイベントを登録。
    /// </summary>
    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    /// <summary>
    /// オブジェクト無効化時にシーンロードイベントを解除。
    /// メモリリークを防ぐため、必ずイベント登録解除を行う。
    /// </summary>
    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    /// <summary>
    /// シーンロード完了時に自動で呼ばれるコールバック。
    /// フェードイン演出を開始する。
    /// </summary>
    /// <param name="scene">ロードされたシーン</param>
    /// <param name="mode">ロードモード（Single/Additive）</param>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        StartCoroutine(FadeInCoroutine(defaultFadeDuration));
    }

    /// <summary>
    /// デフォルトのフェード時間でシーン遷移を実行する。
    /// 他のスクリプトから呼び出し可能。
    /// </summary>
    /// <param name="sceneName">遷移先のシーン名</param>
    public void LoadSceneWithFade(string sceneName)
    {
        StartCoroutine(FadeOutCoroutine(sceneName, defaultFadeDuration));
    }

    /// <summary>
    /// 指定したフェード時間でシーン遷移を実行する。
    /// 演出の長さを個別に調整したい場合に使用。
    /// </summary>
    /// <param name="sceneName">遷移先のシーン名</param>
    /// <param name="duration">フェードアウトにかける時間（秒）</param>
    public void LoadSceneWithFade(string sceneName, float duration)
    {
        StartCoroutine(FadeOutCoroutine(sceneName, duration));
    }

    /// <summary>
    /// フェードアウト演出後にシーンをロードするコルーチン。
    /// Time.realtimeSinceStartupを使用し、TimeScaleの影響を受けない。
    /// </summary>
    /// <param name="sceneName">遷移先のシーン名</param>
    /// <param name="duration">フェードアウトにかける時間（秒）</param>
    private IEnumerator FadeOutCoroutine(string sceneName, float duration)
    {
        fadeImage.gameObject.SetActive(true);
        fadeImage.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 0f);

        float startTime = Time.realtimeSinceStartup;
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime = Time.realtimeSinceStartup - startTime;
            float alpha = Mathf.Lerp(0f, 1f, elapsedTime / duration);
            fadeImage.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, alpha);
            yield return null;
        }

        fadeImage.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 1f);
        SceneManager.LoadScene(sceneName);
    }

    /// <summary>
    /// フェードイン演出を実行するコルーチン。
    /// シーンロード直後の1フレームをスキップし、安定したタイミングで演出を開始する。
    /// </summary>
    /// <param name="duration">フェードインにかける時間（秒）</param>
    private IEnumerator FadeInCoroutine(float duration)
    {
        fadeImage.gameObject.SetActive(true);
        fadeImage.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 1f);

        yield return null;

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