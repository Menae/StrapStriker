using UnityEngine;

/// <summary>
/// 衝撃を受けた際にスーツケースを切り離し、地面を滑らせるコントローラー。
/// </summary>
public class SuitcaseTouristController : NPCController
{
    [Header("Visuals")]
    [SerializeField] private GameObject suitcaseVisual;

    [Header("Physics Settings")]
    [Tooltip("切り離し時の初速")]
    [SerializeField] private float slideSpeed = 15f;

    [Tooltip("滑走時の減速率 (空気抵抗)")]
    [SerializeField] private float groundFriction = 2.0f;

    [Header("Combat")]
    [Tooltip("衝突時のダメージ量")]
    [SerializeField] private float damage = 100f;

    private Vector3 defaultLocalPos;
    private Quaternion defaultLocalRot;

    protected override void Awake()
    {
        base.Awake();
        npcType = NPCType.Suitcase;

        if (suitcaseVisual == null)
        {
            Debug.LogError($"{name}: SuitcaseVisual is not assigned.");
            return;
        }

        defaultLocalPos = suitcaseVisual.transform.localPosition;
        defaultLocalRot = suitcaseVisual.transform.localRotation;
    }

    private void OnEnable()
    {
        if (suitcaseVisual == null) return;

        // プール復帰時のリセット処理
        suitcaseVisual.SetActive(true);
        suitcaseVisual.transform.SetParent(transform);
        suitcaseVisual.transform.localPosition = defaultLocalPos;
        suitcaseVisual.transform.localRotation = defaultLocalRot;
        suitcaseVisual.layer = gameObject.layer;

        // 残存コンポーネントの削除
        if (suitcaseVisual.TryGetComponent<SuitcaseProjectile>(out var proj)) DestroyImmediate(proj);
        if (suitcaseVisual.TryGetComponent<Rigidbody2D>(out var rb)) DestroyImmediate(rb);
        if (suitcaseVisual.TryGetComponent<BoxCollider2D>(out var col)) DestroyImmediate(col);
    }

    public override void TakeImpact(Vector2 impactForce, GameObject instigator)
    {
        if (currentState == NPCState.KnockedDown) return;

        if (impactForce.magnitude > fallenThreshold)
        {
            DetachAndSlide(impactForce);

            rb.AddForce(impactForce, ForceMode2D.Impulse);
            HandleDefeat();
        }
    }

    private void DetachAndSlide(Vector2 impactForce)
    {
        if (suitcaseVisual == null) return;

        suitcaseVisual.transform.SetParent(null);

        // 物理コンポーネントの設定
        var rb = suitcaseVisual.AddComponent<Rigidbody2D>();
        var col = suitcaseVisual.AddComponent<BoxCollider2D>();
        var projectile = suitcaseVisual.AddComponent<SuitcaseProjectile>();

        // レイヤー設定 (Projectile)
        int layerIndex = LayerMask.NameToLayer("Projectile");
        if (layerIndex != -1) suitcaseVisual.layer = layerIndex;

        // 滑走挙動の設定
        rb.gravityScale = 1f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.drag = groundFriction;

        Physics2D.IgnoreCollision(col, GetComponent<Collider2D>());

        // 進行方向の決定
        float dirX = Mathf.Sign(impactForce.x);
        if (Mathf.Abs(impactForce.x) < 0.1f)
        {
            dirX = (Random.value > 0.5f) ? 1f : -1f;
        }

        // 水平方向へ射出
        Vector2 velocity = new Vector2(dirX * slideSpeed, 0f);
        projectile.Initialize(velocity, gameObject, damage);
    }
}