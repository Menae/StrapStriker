using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;

public class SimpleSceneFader : MonoBehaviour
{
    [Header("必須コンポーネント")]
    [Tooltip("フェード効果に使うUIのImageをアタッチしてください。")]
    [SerializeField] private Image fadeImage;

    [Header("遷移設定")]
    [Tooltip("遷移したいシーンの名前")]
    [SerializeField] private string targetSceneName = "YourSceneName";
    [Tooltip("画面が完全に暗くなるまでにかかる時間")]
    [SerializeField] private float fadeOutDuration = 1.0f;

    [Header("シーン開始時の設定")]
    [Tooltip("シーンが始まった時に画面が明るくなるまでにかかる時間")]
    [SerializeField] private float fadeInDuration = 1.0f;

    [Header("色設定")]
    [Tooltip("フェードの色")]
    [SerializeField] private Color fadeColor = Color.black;

    void Start()
    {
        // fadeImageが設定されていなければ、エラーを出して何もしない
        if (fadeImage == null)
        {
            Debug.LogError("Fade Imageが設定されていません！ このコンポーネントは機能しません。", this.gameObject);
            return;
        }

        // このスクリプトが有効になった時、自動でフェードインを開始する
        StartCoroutine(FadeInCoroutine());
    }

    /// <summary>
    /// UIボタンのOnClickイベントから呼び出すためのメソッドです。
    /// </summary>
    public void StartFadeOut()
    {
        if (fadeImage == null) return;
        StartCoroutine(FadeOutCoroutine());
    }

    // フェードアウト(徐々に暗くなる)処理
    private IEnumerator FadeOutCoroutine()
    {
        float timer = 0f;
        fadeImage.gameObject.SetActive(true);

        while (timer < fadeOutDuration)
        {
            float alpha = Mathf.Lerp(0f, 1f, timer / fadeOutDuration);
            fadeImage.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, alpha);
            timer += Time.deltaTime;
            yield return null;
        }
        fadeImage.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 1f);

        SceneManager.LoadScene(targetSceneName);
    }

    // フェードイン(徐々に明るくなる)処理
    private IEnumerator FadeInCoroutine()
    {
        float timer = 0f;
        fadeImage.gameObject.SetActive(true);

        while (timer < fadeInDuration)
        {
            float alpha = Mathf.Lerp(1f, 0f, timer / fadeInDuration);
            fadeImage.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, alpha);
            timer += Time.deltaTime;
            yield return null;
        }
        fadeImage.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 0f);
        fadeImage.gameObject.SetActive(false);
    }
}