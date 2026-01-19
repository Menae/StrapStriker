using UnityEngine;

public class SuitcaseTouristController : NPCController
{
    [Header("スーツケース設定")]
    [Tooltip("発射するスーツケースのオブジェクト")]
    public GameObject suitcaseObject;
    [Tooltip("スーツケースを射出する力")]
    public float shootForce = 10f;

    // 初期位置・回転のキャッシュ
    private Vector3 defaultSuitcasePosition;
    private Quaternion defaultSuitcaseRotation;

    protected override void Awake()
    {
        base.Awake();
        npcType = NPCType.Suitcase;

        if (suitcaseObject != null)
        {
            defaultSuitcasePosition = suitcaseObject.transform.localPosition;
            defaultSuitcaseRotation = suitcaseObject.transform.localRotation;
        }
    }

    private void OnEnable()
    {
        if (suitcaseObject != null)
        {
            suitcaseObject.SetActive(true);
            suitcaseObject.transform.SetParent(this.transform);
            suitcaseObject.transform.localPosition = defaultSuitcasePosition;
            suitcaseObject.transform.localRotation = defaultSuitcaseRotation;

            if (suitcaseObject.TryGetComponent<Rigidbody2D>(out var sRb))
            {
                sRb.simulated = false;
                sRb.velocity = Vector2.zero;
            }
        }
    }

    /// <summary>
    /// 衝撃処理のオーバーライド
    /// スーツケースを分離し、衝撃の方向へ射出する
    /// </summary>
    public override void TakeImpact(Vector2 impactForce, GameObject instigator)
    {
        if (currentState == NPCState.KnockedDown) return;

        // スーツケースの発射処理
        if (suitcaseObject != null && suitcaseObject.transform.parent == this.transform)
        {
            DetachAndLaunchSuitcase(impactForce, instigator);
        }

        // 本体の吹き飛び処理（基底クラス）
        base.TakeImpact(impactForce, instigator);
    }

    /// <summary>
    /// メソッド抽出による可読性向上：スーツケースの切り離しと発射
    /// </summary>
    private void DetachAndLaunchSuitcase(Vector2 impactForce, GameObject instigator)
    {
        suitcaseObject.transform.SetParent(null);

        // コンポーネント取得（なければ追加）の簡略化
        if (!suitcaseObject.TryGetComponent(out Rigidbody2D sRb)) sRb = suitcaseObject.AddComponent<Rigidbody2D>();
        if (!suitcaseObject.TryGetComponent(out Collider2D sCol)) sCol = suitcaseObject.AddComponent<BoxCollider2D>();

        sRb.simulated = true;
        sRb.constraints = RigidbodyConstraints2D.FreezePositionY | RigidbodyConstraints2D.FreezeRotation;

        // instigatorとの衝突を無視する
        if (instigator != null && instigator.TryGetComponent<Collider2D>(out var instigatorCol))
        {
            Physics2D.IgnoreCollision(sCol, instigatorCol, true);
        }

        // プロジェクタイル化
        if (!suitcaseObject.TryGetComponent(out SuitcaseProjectile projectile))
            projectile = suitcaseObject.AddComponent<SuitcaseProjectile>();

        // --- 方向計算の最適化 ---
        // 衝撃力(impactForce)の方向を利用する。
        // 衝撃がほぼ垂直(0に近い)場合は、加害者との位置関係から方向を算出するフォールバックを入れる
        float directionX = Mathf.Sign(impactForce.x);

        if (Mathf.Abs(impactForce.x) < 0.1f && instigator != null)
        {
            // 加害者が左にいたら右へ(1)、右にいたら左へ(-1)
            directionX = Mathf.Sign(suitcaseObject.transform.position.x - instigator.transform.position.x);
        }

        Vector2 launchVelocity = new Vector2(directionX * shootForce, 0f);
        projectile.Launch(launchVelocity);
    }
}