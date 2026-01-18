using UnityEngine;

/// <summary>
/// モバイルバッテリーを持つ学生のコントローラー。
/// プレイヤーからの衝撃（衝突）を受けると、プレイヤーにバッテリーを付与して自身は倒れる。
/// </summary>
public class BatteryStudentController : NPCController
{
    protected override void Awake()
    {
        base.Awake();
        // タイプを自動設定
        npcType = NPCType.Battery;
    }

    /// <summary>
    /// 衝撃処理のオーバーライド。
    /// 衝撃を受けたら確実にプレイヤーにバッテリーを渡し、通常のリアクションを行う。
    /// </summary>
    public override void TakeImpact(Vector2 playerVelocity, float knockbackMultiplier)
    {
        // 既に倒れている場合は何もしない
        if (currentState == NPCState.KnockedDown) return;

        // プレイヤーへのバッテリー付与処理
        // ※PlayerControllerはこの後改修し、EquipBatteryメソッドを追加する想定
        PlayerController player = FindObjectOfType<PlayerController>();
        if (player != null)
        {
            player.EquipBattery();
            Debug.Log("<color=yellow>Battery Equipped!</color>");
        }

        // 基底クラスの処理（物理演算・アニメーション・消滅）をそのまま実行
        base.TakeImpact(playerVelocity, knockbackMultiplier);
    }
}