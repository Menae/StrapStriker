using System.Collections;
using UnityEngine;

/// <summary>
/// タイトル画面で、言語選択前に日本語/英語の背景画像を交互にフェード表示するクラス。
/// TrainViewShakerと同じオブジェクト（親）にアタッチして使用する想定。
/// </summary>
public class TitleBackgroundSwitcher : MonoBehaviour
{
    [Header("画像参照")]
    [Tooltip("日本語版の背景画像オブジェクト")]
    public GameObject jpBackgroundObj;

    [Tooltip("英語版の背景画像オブジェクト")]
    public GameObject enBackgroundObj;

    [Header("切り替え設定")]
    [Tooltip("1つの画像を表示し続ける時間（秒）")]
    public float displayDuration = 3.0f;

    [Tooltip("クロスフェードにかける時間（秒）")]
    public float fadeDuration = 1.0f;

    // 内部制御用
    private CanvasGroup jpCanvasGroup;
    private CanvasGroup enCanvasGroup;
    private Coroutine switchingCoroutine;

    void Start()
    {
        // CanvasGroupの取得・追加（透明度操作のため必須）
        jpCanvasGroup = SetupCanvasGroup(jpBackgroundObj);
        enCanvasGroup = SetupCanvasGroup(enBackgroundObj);

        // 初期状態：JPを表示、ENを非表示
        jpCanvasGroup.alpha = 1f;
        enCanvasGroup.alpha = 0f;

        // ループ開始
        switchingCoroutine = StartCoroutine(SwitchingRoutine());
    }

    /// <summary>
    /// CanvasGroupがなければ追加し、取得して返すヘルパー
    /// </summary>
    private CanvasGroup SetupCanvasGroup(GameObject obj)
    {
        if (obj == null) return null;
        CanvasGroup cg = obj.GetComponent<CanvasGroup>();
        if (cg == null) cg = obj.AddComponent<CanvasGroup>();
        return cg;
    }

    /// <summary>
    /// 交互に切り替えるコルーチン
    /// </summary>
    private IEnumerator SwitchingRoutine()
    {
        bool isJpActive = true;

        while (true)
        {
            // 指定時間待機
            yield return new WaitForSeconds(displayDuration);

            // 切り替え開始
            isJpActive = !isJpActive;

            // フェード処理（並行して実行）
            // JPがActiveになるなら -> JPを1へ、ENを0へ
            if (isJpActive)
            {
                StartCoroutine(FadeCanvasGroup(jpCanvasGroup, 1f, fadeDuration));
                StartCoroutine(FadeCanvasGroup(enCanvasGroup, 0f, fadeDuration));
            }
            else
            {
                StartCoroutine(FadeCanvasGroup(jpCanvasGroup, 0f, fadeDuration));
                StartCoroutine(FadeCanvasGroup(enCanvasGroup, 1f, fadeDuration));
            }

            // フェードが終わるまで待つ必要があればここに記述するが、
            // displayDurationに含める運用の方が調整しやすいため、そのままループ
        }
    }

    /// <summary>
    /// 指定されたCanvasGroupのAlphaを滑らかに変更する
    /// </summary>
    private IEnumerator FadeCanvasGroup(CanvasGroup cg, float targetAlpha, float duration)
    {
        float startAlpha = cg.alpha;
        float time = 0f;

        while (time < duration)
        {
            time += Time.deltaTime;
            cg.alpha = Mathf.Lerp(startAlpha, targetAlpha, time / duration);
            yield return null;
        }
        cg.alpha = targetAlpha;
    }

    /// <summary>
    /// 言語が確定した時に外部から呼ぶメソッド。
    /// ループを止め、指定された言語の画像を即座に表示する。
    /// </summary>
    public void StopSwitchingAndFix(bool isJapanese)
    {
        if (switchingCoroutine != null) StopCoroutine(switchingCoroutine);
        StopAllCoroutines(); // フェード中も強制停止

        if (jpCanvasGroup != null) jpCanvasGroup.alpha = isJapanese ? 1f : 0f;
        if (enCanvasGroup != null) enCanvasGroup.alpha = isJapanese ? 0f : 1f;

        this.enabled = false; // このスクリプト自体を止める
    }
}