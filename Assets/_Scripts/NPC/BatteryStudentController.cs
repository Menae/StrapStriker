using UnityEngine;

/// <summary>
/// モバイルバッテリーを持つ学生。
/// 攻撃者（instigator）がプレイヤーだった場合のみバッテリーを譲渡し、倒れる。
/// </summary>
public class BatteryStudentController : NPCController
{
    protected override void Awake()
    {
        base.Awake();
        npcType = NPCType.Battery;
    }

    /// <summary>
    /// 衝撃処理のオーバーライド
    /// </summary>
    /// <param name="impactForce">衝撃力</param>
    /// <param name="instigator">衝撃を与えたオブジェクト（加害者）</param>
    public override void TakeImpact(Vector2 impactForce, GameObject instigator)
    {
        // 既に倒れている場合は何もしない
        if (currentState == NPCState.KnockedDown) return;

        // FindObjectOfTypeを排除し、衝突相手を確認する
        // instigatorがnullでなく、かつPlayerControllerを持っている場合のみバッテリーを渡す
        if (instigator != null && instigator.TryGetComponent<PlayerController>(out var player))
        {
            player.EquipBattery();
            Debug.Log("<color=yellow>Battery Equipped!</color>");
        }

        // 基底クラスの処理（物理演算・アニメーション・消滅）を実行
        base.TakeImpact(impactForce, instigator);
    }
}