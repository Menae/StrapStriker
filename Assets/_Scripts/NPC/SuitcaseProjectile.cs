using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class SuitcaseProjectile : MonoBehaviour
{
    [Header("設定")]
    [Tooltip("スーツケースが他のNPCに与える衝撃倍率")]
    public float impactMultiplier = 2.0f;
    [Tooltip("発射後、自然消滅するまでの時間")]
    public float lifeTime = 5.0f;

    private bool isLaunched = false;
    private Rigidbody2D rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    public void Launch(Vector2 velocity)
    {
        isLaunched = true;

        rb.velocity = velocity; // 初速だけ与える

        Destroy(gameObject, lifeTime);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!isLaunched) return;

        if (collision.gameObject.CompareTag("NPC"))
        {
            NPCController targetNpc = collision.gameObject.GetComponent<NPCController>();

            if (targetNpc != null && targetNpc.npcType != NPCType.Suitcase && targetNpc.npcType != NPCType.Battery)
            {
                targetNpc.TakeImpact(rb.velocity, impactMultiplier);
            }
        }
    }
}