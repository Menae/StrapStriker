using UnityEngine;

/// <summary>
/// 切り離されたスーツケースの衝突制御。
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class SuitcaseProjectile : MonoBehaviour
{
    [Tooltip("消滅までの時間 (秒)")]
    [SerializeField] private float lifeTime = 5.0f;

    private float damage;
    private GameObject owner;

    public void Initialize(Vector2 velocity, GameObject instigator, float damageAmount)
    {
        owner = instigator;
        damage = damageAmount;

        var rb = GetComponent<Rigidbody2D>();
        rb.velocity = velocity;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        CancelInvoke(nameof(Deactivate));
        Invoke(nameof(Deactivate), lifeTime);
    }

    private void Deactivate()
    {
        gameObject.SetActive(false);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject == owner) return;

        // NPCへのダメージ判定
        if (collision.gameObject.CompareTag("NPC"))
        {
            var target = collision.gameObject.GetComponent<NPCController>();
            if (target != null)
            {
                // 進行方向に基づいて衝撃ベクトルを計算
                Vector2 direction = GetComponent<Rigidbody2D>().velocity.normalized;
                if (direction == Vector2.zero)
                {
                    direction = (target.transform.position - transform.position).normalized;
                }

                target.TakeImpact(direction * damage, owner);
                Deactivate();
            }
        }
    }
}