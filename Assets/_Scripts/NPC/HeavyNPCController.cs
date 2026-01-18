using UnityEngine;

/// <summary>
/// 質量の高い（デブ）NPCのコントローラー。
/// 一定以下の衝撃は無効化し、プレイヤーを弾き飛ばすカウンター動作を行う。
/// </summary>
public class HeavyNPCController : NPCController
{
    [Header("弾き返し設定")]
    [Tooltip("この値以下の衝撃なら弾き返す（通常の倒れる閾値より高く設定する）")]
    public float heavyResistanceThreshold = 15.0f;
    [Tooltip("プレイヤーを弾き返す力の倍率")]
    public float bounceBackMultiplier = 1.5f;

    protected override void Awake()
    {
        base.Awake();
        npcType = NPCType.Heavy;

        // 質量を重くして物理的にも吹っ飛びにくくする
        if (rb != null) rb.mass = 3.0f;
    }

    public override void TakeImpact(Vector2 playerVelocity, float knockbackMultiplier)
    {
        if (currentState == NPCState.KnockedDown) return;

        // 衝撃力を計算
        Vector2 forceToApply = playerVelocity * knockbackMultiplier;
        float impactMagnitude = forceToApply.magnitude;

        // 閾値チェック：衝撃が弱すぎる場合
        if (impactMagnitude < heavyResistanceThreshold)
        {
            // 弾き返し処理
            // プレイヤーを探して逆方向の力を加える
            PlayerController player = FindObjectOfType<PlayerController>();
            if (player != null)
            {
                Rigidbody2D playerRb = player.GetComponent<Rigidbody2D>();
                if (playerRb != null)
                {
                    // 入ってきたベクトルを反転させて返す
                    Vector2 bounceForce = -playerVelocity * bounceBackMultiplier;
                    playerRb.AddForce(bounceForce, ForceMode2D.Impulse);

                    Debug.Log("<color=red>Boing!</color> 弾き返された！");

                    // ※ここで「耐えた」アニメーション等を再生すると良い
                    if (animator != null) animator.SetTrigger("Endure");
                }
            }
            return; // ここで終了（倒れない）
        }

        // 閾値を超えていれば、通常の吹っ飛び処理へ
        base.TakeImpact(playerVelocity, knockbackMultiplier);
    }
}