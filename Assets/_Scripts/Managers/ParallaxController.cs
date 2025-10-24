using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ParallaxController : MonoBehaviour
{
    private enum ParallaxState
    {
        Looping,             // 通常の背景ループ中
        FadingToStation,     // 駅へフェード中
        StoppedAtStation,    // 駅に停車中
        FadingToLooping      // 通常ループへフェード中
    }

    [System.Serializable]
    public class ParallaxLayer
    {
        public GameObject layerPrefab;
        public float scrollSpeed;
        [HideInInspector] public Transform[] instances = new Transform[3];
        [HideInInspector] public float spriteWidth;
    }

    [Header("通常ループ設定")]
    [Tooltip("奥から手前の順番でループ用のレイヤーを設定")]
    public List<ParallaxLayer> loopingLayers;
    [Tooltip("チェックを入れると、背景が右方向に流れます")]
    public bool scrollRight = false;

    [Header("駅イベント設定")]
    [Tooltip("駅の背景を表示するSpriteRenderer")]
    public SpriteRenderer stationBackgroundRenderer;
    [Tooltip("通常背景と駅背景が切り替わる時のフェード時間")]
    public float backgroundFadeDuration = 1.5f;

    private ParallaxState currentState;
    private Transform cameraTransform;

    void Start()
    {
        cameraTransform = Camera.main.transform;

        foreach (var layer in loopingLayers)
        {
            // 実行時に配列のサイズを強制的に3にリセットし、古いシリアライズデータの問題を回避する
            layer.instances = new Transform[3];

            layer.spriteWidth = layer.layerPrefab.GetComponent<SpriteRenderer>().bounds.size.x;
            for (int i = 0; i < 3; i++)
            {
                var instance = Instantiate(layer.layerPrefab, transform);
                layer.instances[i] = instance.transform; // ← これでエラーが起きなくなる
                float initialX = (i - 1) * layer.spriteWidth;
                instance.transform.position = new Vector2(initialX, transform.position.y);
            }
        }

        if (stationBackgroundRenderer != null)
        {
            stationBackgroundRenderer.enabled = false;
        }

        currentState = ParallaxState.Looping;
    }

    void LateUpdate()
    {
        // もし状態が「通常ループ中」なら、背景をスクロールさせる
        if (currentState == ParallaxState.Looping)
        {
            UpdateLoopingLayers();
        }
        // それ以外（駅に接近中、停車中、出発中）の場合
        else
        {
            // 駅の背景を、常にカメラのX座標に追従させる
            if (stationBackgroundRenderer != null && stationBackgroundRenderer.enabled)
            {
                stationBackgroundRenderer.transform.position = new Vector2(
                    cameraTransform.position.x,
                    stationBackgroundRenderer.transform.position.y
                );
            }
        }
    }

    // --- 通常ループの処理 ---
    private void UpdateLoopingLayers()
    {
        Vector3 direction = scrollRight ? Vector3.right : Vector3.left;
        foreach (var layer in loopingLayers)
        {
            float movement = layer.scrollSpeed * Time.deltaTime;
            for (int i = 0; i < 3; i++)
            {
                layer.instances[i].position += direction * movement;
                float distanceToCamera = cameraTransform.position.x - layer.instances[i].position.x;
                if (distanceToCamera > layer.spriteWidth * 1.5f)
                {
                    layer.instances[i].position += Vector3.right * layer.spriteWidth * 3;
                }
                else if (distanceToCamera < -layer.spriteWidth * 1.5f)
                {
                    layer.instances[i].position += Vector3.left * layer.spriteWidth * 3;
                }
            }
        }
    }

    // --- StageManagerから呼ばれる公開メソッド ---
    public void StartApproachingStation(Sprite stationSprite)
    {
        if (currentState != ParallaxState.Looping) return;
        if (stationBackgroundRenderer == null || stationSprite == null) return;

        currentState = ParallaxState.FadingToStation;
        stationBackgroundRenderer.sprite = stationSprite;

        StartCoroutine(CrossfadeCoroutine(true));
    }

    public void DepartFromStation()
    {
        if (currentState != ParallaxState.StoppedAtStation) return;

        currentState = ParallaxState.FadingToLooping;
        StartCoroutine(CrossfadeCoroutine(false));
    }

    // --- クロスフェード処理のコルーチン ---
    private IEnumerator CrossfadeCoroutine(bool showStation)
    {
        float timer = 0f;
        stationBackgroundRenderer.enabled = true;

        // 目標のアルファ値
        float targetStationAlpha = showStation ? 1.0f : 0.0f;
        float targetLoopingAlpha = showStation ? 0.0f : 1.0f;

        while (timer < backgroundFadeDuration)
        {
            float progress = timer / backgroundFadeDuration;

            // 駅背景のアルファ値を変更
            Color stationColor = stationBackgroundRenderer.color;
            stationColor.a = Mathf.Lerp(stationColor.a, targetStationAlpha, progress);
            stationBackgroundRenderer.color = stationColor;

            // 通常ループ背景のアルファ値を変更
            foreach (var layer in loopingLayers)
            {
                for (int i = 0; i < 3; i++)
                {
                    var renderer = layer.instances[i].GetComponent<SpriteRenderer>();
                    Color loopColor = renderer.color;
                    loopColor.a = Mathf.Lerp(loopColor.a, targetLoopingAlpha, progress);
                    renderer.color = loopColor;
                }
            }

            timer += Time.deltaTime;
            yield return null;
        }

        // 最終的な状態を確定
        if (showStation)
        {
            currentState = ParallaxState.StoppedAtStation;
        }
        else
        {
            currentState = ParallaxState.Looping;
            stationBackgroundRenderer.enabled = false;
        }
    }
}