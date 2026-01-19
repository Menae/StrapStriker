using UnityEngine;

/// <summary>
/// スーツケースのプロジェクタイル制御。
/// 物理的な運動量（質量×速度）を衝撃力として相手に伝達する。
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class SuitcaseProjectile : MonoBehaviour
{
    [Header("Physics Settings")]
    [SerializeField, Tooltip("衝突時の衝撃力補正係数（基本は1.0）")]
    private float impactPowerMultiplier = 1.0f;

    [SerializeField, Tooltip("発射後の生存時間（秒）")]
    private float lifeTime = 5.0f;

    private bool isLaunched = false;
    private Rigidbody2D rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    /// <summary>
    /// 外部（SuitcaseTouristController）からの射出エントリーポイント
    /// </summary>
    public void Launch(Vector2 velocity)
    {
        isLaunched = true;
        rb.velocity = velocity;

        // メモリリーク防止のため、一定時間後に自己破壊
        Destroy(gameObject, lifeTime);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // 未発射の接触は無視
        if (!isLaunched) return;

        // 特定の相手に当てたくない場合は、Layer Collision Matrixで物理的に無効化することを推奨。
        if (collision.gameObject.TryGetComponent<NPCController>(out var targetNpc))
        {
            // --- 物理計算: 衝撃力(Impulse)の算出 ---
            // relativeVelocityを使用することで、双方が動いている場合の相対的な衝撃を正確に計算
            Vector2 impactImpulse = collision.relativeVelocity * rb.mass * impactPowerMultiplier;

            // 衝突判定のタイミングにより相対速度が極小になるケースのフォールバック
            if (impactImpulse.sqrMagnitude < 0.01f)
            {
                impactImpulse = rb.velocity * rb.mass * impactPowerMultiplier;
            }

            // Arg1: 計算された物理的衝撃ベクトル
            // Arg2: 加害者（このスーツケース自体）を渡すことで、相手側が「何にぶつかったか」を識別可能にする
            targetNpc.TakeImpact(impactImpulse, this.gameObject);
        }
    }
}