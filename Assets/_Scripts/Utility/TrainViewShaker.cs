using System.Collections;
using UnityEngine;

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
    // --- ここからが新しい設定項目 ---
    [Tooltip("「ガタン」と「ゴトン」の間の時間（秒）")]
    public float doubleJoltDelay = 0.15f;

    private RectTransform rectTransform;
    private Vector2 initialPosition;
    private Vector2 joltOffset;

    private float perlinSeedX;
    private float perlinSeedY;

    void Start()
    {
        rectTransform = GetComponent<RectTransform>();
        initialPosition = rectTransform.anchoredPosition;
        joltOffset = Vector2.zero;

        perlinSeedX = Random.Range(0f, 100f);
        perlinSeedY = Random.Range(0f, 100f);

        StartCoroutine(PeriodicJoltRoutine());
    }

    void Update()
    {
        float swayX = (Mathf.PerlinNoise(perlinSeedX, Time.time * swaySpeed) * 2.0f - 1.0f) * swayAmount;
        float swayY = (Mathf.PerlinNoise(perlinSeedY, Time.time * swaySpeed) * 2.0f - 1.0f) * swayAmount;

        float rattleX = (Mathf.PerlinNoise(perlinSeedX + 10f, Time.time * rattleSpeed) * 2.0f - 1.0f) * rattleAmount;
        float rattleY = (Mathf.PerlinNoise(perlinSeedY + 10f, Time.time * rattleSpeed) * 2.0f - 1.0f) * rattleAmount;

        Vector2 continuousShake = new Vector2(swayX + rattleX, swayY + rattleY);
        Vector2 finalOffset = (continuousShake + joltOffset) * shakeMultiplier;

        rectTransform.anchoredPosition = initialPosition + finalOffset;
    }
    private IEnumerator PeriodicJoltRoutine()
    {
        while (true)
        {
            float waitTime = Random.Range(joltIntervalMin, joltIntervalMax);
            yield return new WaitForSeconds(waitTime);

            // 1回目の衝撃（ガタン！）を発生させる
            StartCoroutine(ExecuteJoltCoroutine());

            // 「ガタン」と「ゴトン」の間の短い待機
            yield return new WaitForSeconds(doubleJoltDelay);

            // 2回目の衝撃（ゴトン！）を発生させる
            StartCoroutine(ExecuteJoltCoroutine());
        }
    }

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