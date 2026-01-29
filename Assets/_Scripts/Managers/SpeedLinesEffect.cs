using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 複数のRawImageを物理的に移動させて速度感を演出するエフェクトコントローラー。
/// 加速用・減速用でそれぞれ別のRawImage群（ヒエラルキーの別オブジェクトの子要素）を割り当てて制御する仕様。
/// </summary>
public class SpeedLinesEffect : MonoBehaviour
{
    [Header("Target Images")]
    [Tooltip("加速時に使用するRawImage群（加速用オブジェクトの子要素を全て登録）")]
    public RawImage[] accelerationImages;
    [Tooltip("減速時に使用するRawImage群（減速用オブジェクトの子要素を全て登録）")]
    public RawImage[] decelerationImages;

    // テクスチャ設定は各RawImageに直接設定されている前提のため削除

    [Header("Scroll Settings")]
    [Tooltip("スクロール速度の全体倍率")]
    [Range(0f, 5f)]
    public float scrollSpeedMultiplier = 1.0f;

    [Tooltip("加速時の移動方向と基本速度")]
    public Vector2 accelerationVelocity = new Vector2(1000f, 500f);

    [Tooltip("減速時の移動方向と基本速度")]
    public Vector2 decelerationVelocity = new Vector2(-1000f, -500f);

    [Header("Fade Settings")]
    [Tooltip("フェードイン・アウトの所要時間（秒）")]
    public float fadeDuration = 0.5f;

    // 内部変数
    private RawImage[] currentActiveImages; // 現在制御中の画像群
    private Vector2 currentVelocity;
    private float targetAlpha = 0f;
    private float currentAlpha = 0f;

    // 全画像の初期位置を保持する辞書
    private Dictionary<RawImage, Vector2> initialPositions = new Dictionary<RawImage, Vector2>();

    void Awake()
    {
        // 加速・減速両方の画像の初期位置を保存し、非表示にする
        InitializeImages(accelerationImages);
        InitializeImages(decelerationImages);

        currentAlpha = 0f;
    }

    private void InitializeImages(RawImage[] images)
    {
        if (images == null) return;
        foreach (var img in images)
        {
            if (img == null) continue;

            // 初期位置を記録（重複登録防止）
            if (!initialPositions.ContainsKey(img))
            {
                initialPositions.Add(img, img.rectTransform.anchoredPosition);
            }

            // 初期状態は透明＆無効化
            Color c = img.color;
            c.a = 0f;
            img.color = c;
            img.enabled = false;
        }
    }

    void Update()
    {
        // 制御対象がなければ何もしない
        if (currentActiveImages == null || currentActiveImages.Length == 0) return;

        // --- 1. フェード計算 ---
        if (!Mathf.Approximately(currentAlpha, targetAlpha))
        {
            currentAlpha = Mathf.MoveTowards(currentAlpha, targetAlpha, Time.deltaTime / fadeDuration);
        }

        // --- 2. 状態適用 ---
        bool isVisible = (currentAlpha > 0f);

        if (isVisible)
        {
            Vector2 step = currentVelocity * scrollSpeedMultiplier * Time.deltaTime;

            foreach (var img in currentActiveImages)
            {
                if (img == null) continue;

                // アルファ値更新
                Color c = img.color;
                c.a = currentAlpha;
                img.color = c;

                // 有効化
                if (!img.enabled) img.enabled = true;

                // 移動
                img.rectTransform.anchoredPosition += step;
            }
        }
        else
        {
            // 完全透明時は無効化して負荷を下げる
            foreach (var img in currentActiveImages)
            {
                if (img == null) continue;
                if (img.enabled) img.enabled = false;
            }
        }
    }

    /// <summary>
    /// 加速エフェクト再生（加速用画像群を使用）
    /// </summary>
    public void PlayAcceleration()
    {
        SwitchActiveSet(accelerationImages, accelerationVelocity);
    }

    /// <summary>
    /// 減速エフェクト再生（減速用画像群を使用）
    /// </summary>
    public void PlayDeceleration()
    {
        SwitchActiveSet(decelerationImages, decelerationVelocity);
    }

    /// <summary>
    /// エフェクト停止（フェードアウト）
    /// </summary>
    public void Stop()
    {
        targetAlpha = 0f;
    }

    /// <summary>
    /// 表示する画像群を切り替える内部処理
    /// </summary>
    private void SwitchActiveSet(RawImage[] newSet, Vector2 velocity)
    {
        // 直前まで表示していた別のセットがあれば、即座に非表示にする
        if (currentActiveImages != null && currentActiveImages != newSet)
        {
            foreach (var img in currentActiveImages)
            {
                if (img == null) continue;
                Color c = img.color;
                c.a = 0f;
                img.color = c;
                img.enabled = false;
            }
        }

        // 新しいセットに切り替え
        currentActiveImages = newSet;
        currentVelocity = velocity;

        // 新しいセットの位置をリセット
        ResetPositions(currentActiveImages);

        // フェードイン開始（切り替え時の違和感をなくすため0からスタート）
        currentAlpha = 0f;
        targetAlpha = 1f;
    }

    /// <summary>
    /// 指定された画像群の位置を初期位置に戻す
    /// </summary>
    private void ResetPositions(RawImage[] images)
    {
        if (images == null) return;
        foreach (var img in images)
        {
            if (img != null && initialPositions.ContainsKey(img))
            {
                img.rectTransform.anchoredPosition = initialPositions[img];
            }
        }
    }
}