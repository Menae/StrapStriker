using System.Collections;
using UnityEngine;

/// <summary>
/// 電車内の視点を、うねり・振動・衝撃の3要素で揺らすことで臨場感を演出するコンポーネント。
/// RectTransformのanchoredPositionを操作するため、UI要素にアタッチすることを想定。
/// </summary>
public class TrainViewShaker : MonoBehaviour
{
    [Header("揺れの全体設定")]
    [Tooltip("揺れの強さの全体的な倍率")]
    [Range(0f, 2f)]
    public float shakeMultiplier = 1.0f;

    [Header("大きな揺れ（うねり）の設定")]
    [Tooltip("うねりの最大オフセット（ピクセル単位）")]
    public float swayAmount = 5.0f;

    [Tooltip("うねりの速さ")]
    public float swaySpeed = 0.5f;

    [Header("細かな振動（ガタガタ）の設定")]
    [Tooltip("振動の最大オフセット（ピクセル単位）")]
    public float rattleAmount = 2.0f;

    [Tooltip("振動の速さ")]
    public float rattleSpeed = 20.0f;

    [Header("周期的な大きな揺れ（ガタンゴトン）")]
    [Tooltip("ガタン！という衝撃の強さ")]
    public float joltAmount = 15.0f;

    [Tooltip("衝撃が発生する最小間隔（秒）")]
    public float joltIntervalMin = 3.0f;

    [Tooltip("衝撃が発生する最大間隔（秒）")]
    public float joltIntervalMax = 8.0f;

    [Tooltip("衝撃が収まるまでの時間（秒）")]
    public float joltDuration = 0.2f;

    [Tooltip("「ガタン」と「ゴトン」の間の時間（秒）")]
    public float doubleJoltDelay = 0.15f;

    /// <summary>
    /// 揺らす対象のRectTransform。Start時にGetComponentで取得。
    /// </summary>
    private RectTransform rectTransform;

    /// <summary>
    /// 揺れを適用する前の初期座標。揺れ計算の基準点として使用。
    /// </summary>
    private Vector2 initialPosition;

    /// <summary>
    /// 周期的な衝撃（ガタンゴトン）によるオフセット。Lerpで減衰させる。
    /// </summary>
    private Vector2 joltOffset;

    /// <summary>
    /// PerlinNoiseのシード値（X軸）。Start時にランダム生成し、うねりと振動で異なる値を使う。
    /// </summary>
    private float perlinSeedX;

    /// <summary>
    /// PerlinNoiseのシード値（Y軸）。Start時にランダム生成し、うねりと振動で異なる値を使う。
    /// </summary>
    private float perlinSeedY;

    /// <summary>
    /// 初期化処理。RectTransformの取得、初期座標の記憶、PerlinNoiseシードの設定を行う。
    /// 周期的な衝撃コルーチンもここで開始。
    /// </summary>
    void Start()
    {
        rectTransform = GetComponent<RectTransform>();
        initialPosition = rectTransform.anchoredPosition;
        joltOffset = Vector2.zero;

        perlinSeedX = Random.Range(0f, 100f);
        perlinSeedY = Random.Range(0f, 100f);

        StartCoroutine(PeriodicJoltRoutine());
    }

    /// <summary>
    /// 毎フレーム、うねり・振動・衝撃の3要素を合成してRectTransformの座標を更新。
    /// PerlinNoiseを使用することで自然な揺れを実現。
    /// </summary>
    void Update()
    {
        // うねり成分（ゆったりとした周期の揺れ）
        float swayX = (Mathf.PerlinNoise(perlinSeedX, Time.time * swaySpeed) * 2.0f - 1.0f) * swayAmount;
        float swayY = (Mathf.PerlinNoise(perlinSeedY, Time.time * swaySpeed) * 2.0f - 1.0f) * swayAmount;

        // 振動成分（細かく速い揺れ）
        float rattleX = (Mathf.PerlinNoise(perlinSeedX + 10f, Time.time * rattleSpeed) * 2.0f - 1.0f) * rattleAmount;
        float rattleY = (Mathf.PerlinNoise(perlinSeedY + 10f, Time.time * rattleSpeed) * 2.0f - 1.0f) * rattleAmount;

        // 継続的な揺れ（うねり+振動）と衝撃オフセットを合成
        Vector2 continuousShake = new Vector2(swayX + rattleX, swayY + rattleY);
        Vector2 finalOffset = (continuousShake + joltOffset) * shakeMultiplier;

        rectTransform.anchoredPosition = initialPosition + finalOffset;
    }

    /// <summary>
    /// 周期的に「ガタンゴトン」という2回連続の衝撃を発生させるコルーチン。
    /// ランダムな間隔で待機し、短い遅延を挟んで2回の衝撃を実行。
    /// </summary>
    private IEnumerator PeriodicJoltRoutine()
    {
        while (true)
        {
            float waitTime = Random.Range(joltIntervalMin, joltIntervalMax);
            yield return new WaitForSeconds(waitTime);

            // 1回目の衝撃（ガタン）
            StartCoroutine(ExecuteJoltCoroutine());

            yield return new WaitForSeconds(doubleJoltDelay);

            // 2回目の衝撃（ゴトン）
            StartCoroutine(ExecuteJoltCoroutine());
        }
    }

    /// <summary>
    /// 1回分の衝撃を実行するコルーチン。
    /// ランダムな方向（下方向寄り）に瞬間的な揺れを発生させ、joltDuration秒かけて減衰させる。
    /// </summary>
    private IEnumerator ExecuteJoltCoroutine()
    {
        Vector2 joltDirection = new Vector2(Random.Range(-0.5f, 0.5f), -1.0f).normalized;
        Vector2 startOffset = joltDirection * joltAmount;

        float timer = 0f;
        while (timer < joltDuration)
        {
            joltOffset = Vector2.Lerp(startOffset, Vector2.zero, timer / joltDuration);
            timer += Time.deltaTime;
            yield return null;
        }

        joltOffset = Vector2.zero;
    }
}