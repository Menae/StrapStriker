using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// リザルト画面の表示演出を管理するクラス。
/// スコアの数値カウントアップや、星の段階的な表示アニメーションを行う。
/// </summary>
public class ResultUIController : MonoBehaviour
{
    [Header("UI参照")]
    [Tooltip("リザルト画面のパネル全体")]
    public GameObject resultPanel;
    [Tooltip("撃破数を表示するテキスト")]
    public TextMeshProUGUI killCountText;
    [Tooltip("評価メッセージ（例: Excellent!）")]
    public TextMeshProUGUI rankMessageText;

    [Header("星アイコン設定")]
    [Tooltip("星画像のリスト（左から順に1, 2, 3個目）")]
    public List<Image> starImages;
    [Tooltip("星が獲得された時の色（黄色など）")]
    public Color earnedColor = new Color(1f, 0.8f, 0f, 1f);
    [Tooltip("星が未獲得の時の色（グレーなど）")]
    public Color unearnedColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);

    [Header("アニメーション設定")]
    [Tooltip("表示開始時の待機時間")]
    public float startDelay = 0.5f;
    [Tooltip("星が1つ表示される間隔")]
    public float starInterval = 0.4f;
    [Tooltip("星が表示される時の効果音")]
    public AudioClip starSound;

    private AudioSource audioSource;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();

        // 最初はパネルを非表示にしておく
        if (resultPanel != null) resultPanel.SetActive(false);
    }

    /// <summary>
    /// リザルト演出を開始する。
    /// StageManagerから呼ばれる。
    /// </summary>
    /// <param name="killCount">倒した敵の数</param>
    /// <param name="starCount">獲得した星の数(1~3)</param>
    public void ShowResult(int killCount, int starCount)
    {
        if (resultPanel == null) return;

        resultPanel.SetActive(true);

        // 初期化: 星をすべて「未獲得色」にする
        foreach (var star in starImages)
        {
            star.color = unearnedColor;
        }

        killCountText.text = "0"; // カウントアップ演出のため0スタート
        rankMessageText.text = "";

        // アニメーションコルーチン開始
        StartCoroutine(ResultAnimationRoutine(killCount, starCount));
    }

    private IEnumerator ResultAnimationRoutine(int targetScore, int starCount)
    {
        yield return new WaitForSeconds(startDelay);

        // 1. スコアのカウントアップ演出
        float duration = 1.0f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime; // TimeScaleが0でも動くようにunscaledを使う
            float progress = elapsed / duration;
            int currentDisplayScore = Mathf.FloorToInt(Mathf.Lerp(0, targetScore, progress));
            killCountText.text = currentDisplayScore.ToString();
            yield return null;
        }
        killCountText.text = targetScore.ToString();

        // 2. 星の表示演出
        for (int i = 0; i < starImages.Count; i++)
        {
            // 獲得した星の数だけ色を変える
            if (i < starCount)
            {
                yield return new WaitForSeconds(starInterval);

                starImages[i].color = earnedColor;

                // 軽く拡大縮小アニメーションなどを入れても良い（今回は簡易的に音のみ）
                if (starSound != null) audioSource.PlayOneShot(starSound);
            }
        }

        // 3. 最後に評価メッセージを表示
        yield return new WaitForSeconds(0.5f);
        if (starCount == 3) rankMessageText.text = "PERFECT!!";
        else if (starCount == 2) rankMessageText.text = "GREAT!";
        else rankMessageText.text = "GOOD";
    }
}