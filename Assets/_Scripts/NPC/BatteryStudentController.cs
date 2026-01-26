using UnityEngine;

public class BatteryStudentController : NPCController
{
    // ■ 追加: 無限ループ防止用フラグ
    private bool hasGivenBattery = false;

    protected override void Awake()
    {
        base.Awake();
        npcType = NPCType.Battery;
    }

    private void OnEnable()
    {
        // プールから再利用されるときにリセット
        hasGivenBattery = false;
    }

    public override void TakeImpact(Vector2 impactForce, GameObject instigator)
    {
        // 既に倒れている場合は何もしない
        if (currentState == NPCState.KnockedDown) return;

        // まだ渡していない(false)場合のみ処理する
        if (!hasGivenBattery && instigator != null && instigator.TryGetComponent<PlayerController>(out var player))
        {
            hasGivenBattery = true;

            player.EquipBattery();
            Debug.Log("<color=yellow>Battery Equipped!</color>");
        }

        base.TakeImpact(impactForce, instigator);
    }
}