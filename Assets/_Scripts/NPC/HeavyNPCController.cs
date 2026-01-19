using UnityEngine;

public class HeavyNPCController : NPCController
{
    [Header("重量級設定")]
    [Tooltip("この衝撃力(Impulse)未満は耐える")]
    public float impactResistanceThreshold = 20.0f;

    [Tooltip("カウンターの強さ")]
    public float bounceBackMultiplier = 1.5f;

    [Tooltip("耐えた時のよろめき力")]
    public float flinchForce = 2.0f;

    [Tooltip("ガードブレイク時の吹き飛び倍率")]
    public float breakGuardMultiplier = 1.5f;

    // Animatorハッシュ（派生クラス用）
    private static readonly int AnimHashEndure = Animator.StringToHash("Endure");

    protected override void Awake()
    {
        base.Awake();
        npcType = NPCType.Heavy;
    }

    /// <summary>
    /// 衝撃処理のオーバーライド
    /// </summary>
    /// <param name="impactForce">入力された衝撃インパルス</param>
    /// <param name="instigator">衝撃を与えたオブジェクト（プレイヤー等）</param>
    public override void TakeImpact(Vector2 impactForce, GameObject instigator)
    {
        if (currentState == NPCState.KnockedDown) return;

        float impactMagnitude = impactForce.magnitude;

        // --- 1. スーパーアーマー判定 (Guard & Counter) ---
        if (impactMagnitude < impactResistanceThreshold)
        {
            // よろめき演出
            if (rb != null)
            {
                rb.AddForce(impactForce.normalized * flinchForce, ForceMode2D.Impulse);
            }

            // 引数のinstigatorを使ってカウンター
            if (instigator != null && instigator.TryGetComponent<Rigidbody2D>(out var targetRb))
            {
                // 相手を来た方向へ弾き返す
                Vector2 counterDir = (instigator.transform.position - transform.position).normalized;
                targetRb.AddForce(counterDir * impactMagnitude * bounceBackMultiplier, ForceMode2D.Impulse);
            }

            if (animator != null) animator.SetTrigger(AnimHashEndure);

            // 処理終了（基底クラスの吹き飛び処理を行わない）
            return;
        }

        // --- 2. ガードブレイク (Guard Break) ---
        // 閾値を超えたため、倍率をかけて派手に吹き飛ばす
        Vector2 breakForce = impactForce * breakGuardMultiplier;

        // 基底クラスへ委譲
        base.TakeImpact(breakForce, instigator);
    }
}