using UnityEngine;

public class HeavyNPCController : NPCController
{
    [Header("重量級設定")]
    [Tooltip("この衝撃力(Impulse)未満は耐えて弾き返す")]
    public float impactResistanceThreshold = 80.0f; // 前回より高めに設定

    [Tooltip("カウンターの強さ（跳ね返し倍率）")]
    public float bounceBackMultiplier = 1.5f;

    [Tooltip("耐えた時のよろめき力（自分への影響）")]
    public float flinchForce = 2.0f;

    protected override void Awake()
    {
        base.Awake();
        npcType = NPCType.Heavy;

        // インスペクタ未設定時のデフォルト値を保証
        if (fallenThreshold < impactResistanceThreshold) fallenThreshold = impactResistanceThreshold;
        if (weight <= 1.0f) weight = 2.5f;
    }

    /// <summary>
    /// 衝撃処理のオーバーライド
    /// </summary>
    public override void TakeImpact(Vector2 impactForce, GameObject instigator)
    {
        // 既に倒れているなら何もしない
        if (currentState == NPCState.KnockedDown) return;

        float impactMagnitude = impactForce.magnitude;

        // --- 1. 耐える判定 (Guard & Counter) ---
        // 設定された閾値（impactResistanceThreshold）より弱ければ耐える
        if (impactMagnitude < impactResistanceThreshold)
        {
            // よろめき（自分へのわずかな力）
            if (rb != null)
            {
                rb.AddForce(impactForce.normalized * flinchForce, ForceMode2D.Impulse);
            }

            // カウンター処理：相手（instigator）を弾き返す
            if (instigator != null && instigator.TryGetComponent<Rigidbody2D>(out var targetRb))
            {
                // 相手を「来た方向」へ弾き返すベクトル計算
                Vector2 counterDir = (instigator.transform.position - transform.position).normalized;
                targetRb.AddForce(counterDir * impactMagnitude * bounceBackMultiplier, ForceMode2D.Impulse);

                Debug.Log("<color=red>Heavy NPC Guarded & Bounced Back!</color>");
            }

            return;
        }

        // --- 2. ガードブレイク ---
        // 閾値を超えた場合は、基底クラスの処理（吹っ飛び＆ダウン）を実行
        base.TakeImpact(impactForce, instigator);
    }
}