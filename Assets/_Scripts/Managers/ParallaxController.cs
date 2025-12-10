using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 視差効果による背景スクロールと駅イベント時の背景切り替えを管理するクローラー。
/// 通常時は複数レイヤーをループさせ、駅到着時にはクロスフェードで駅背景を表示する。
/// </summary>
public class ParallaxController : MonoBehaviour
{
    /// <summary>
    /// 背景の状態を表す列挙型。
    /// </summary>
    private enum ParallaxState
    {
        Looping,             // 通常の背景ループ中
        FadingToStation,     // 駅へフェード中
        StoppedAtStation,    // 駅に停車中
        FadingToLooping      // 通常ループへフェード中
    }

    /// <summary>
    /// 視差効果を構成する1つのレイヤー。
    /// 3つのインスタンスを生成してシームレスなループを実現する。
    /// </summary>
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

    /// <summary>
    /// 初期化処理。各レイヤーのプレハブを3つインスタンス化し、シームレスループの準備を行う。
    /// カメラ参照を取得し、駅背景を非表示にした状態で開始する。
    /// </summary>
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
                layer.instances[i] = instance.transform;
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

    /// <summary>
    /// カメラ移動後に実行される更新処理。
    /// 通常ループ中は背景をスクロールさせ、駅イベント中は駅背景をカメラに追従させる。
    /// </summary>
    void LateUpdate()
    {
        if (currentState == ParallaxState.Looping)
        {
            UpdateLoopingLayers();
        }
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

    /// <summary>
    /// 通常ループ時の背景スクロール処理。
    /// カメラ視界外に出たインスタンスを反対側に再配置してシームレスなループを実現する。
    /// </summary>
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

    /// <summary>
    /// 駅への接近を開始し、通常背景から駅背景へのクロスフェードを実行する。
    /// StageManagerなど外部システムから呼び出される想定。
    /// </summary>
    /// <param name="stationSprite">表示する駅背景のスプライト</param>
    public void StartApproachingStation(Sprite stationSprite)
    {
        if (currentState != ParallaxState.Looping) return;
        if (stationBackgroundRenderer == null || stationSprite == null) return;

        currentState = ParallaxState.FadingToStation;
        stationBackgroundRenderer.sprite = stationSprite;

        StartCoroutine(CrossfadeCoroutine(true));
    }

    /// <summary>
    /// 駅からの出発を開始し、駅背景から通常背景へのクロスフェードを実行する。
    /// StageManagerなど外部システムから呼び出される想定。
    /// </summary>
    public void DepartFromStation()
    {
        if (currentState != ParallaxState.StoppedAtStation) return;

        currentState = ParallaxState.FadingToLooping;
        StartCoroutine(CrossfadeCoroutine(false));
    }

    /// <summary>
    /// 通常背景と駅背景をアルファ値で滑らかに切り替えるコルーチン。
    /// フェード完了後、状態を更新し必要に応じて駅背景を非表示にする。
    /// </summary>
    /// <param name="showStation">trueの場合は駅背景を表示、falseの場合は通常背景を表示</param>
    private IEnumerator CrossfadeCoroutine(bool showStation)
    {
        float timer = 0f;
        stationBackgroundRenderer.enabled = true;

        float targetStationAlpha = showStation ? 1.0f : 0.0f;
        float targetLoopingAlpha = showStation ? 0.0f : 1.0f;

        while (timer < backgroundFadeDuration)
        {
            float progress = timer / backgroundFadeDuration;

            Color stationColor = stationBackgroundRenderer.color;
            stationColor.a = Mathf.Lerp(stationColor.a, targetStationAlpha, progress);
            stationBackgroundRenderer.color = stationColor;

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