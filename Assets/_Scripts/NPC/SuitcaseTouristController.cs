using UnityEngine;

public class SuitcaseTouristController : NPCController
{
    [Header("スーツケース設定")]
    [Tooltip("発射するスーツケースのオブジェクト")]
    public GameObject suitcaseObject;
    [Tooltip("スーツケースを射出する力")]
    public float shootForce = 10f;

    // プレハブでの初期位置を覚えておく変数
    private Vector3 defaultSuitcasePosition;
    private Quaternion defaultSuitcaseRotation;

    protected override void Awake()
    {
        base.Awake();
        npcType = NPCType.Suitcase;

        // 追加: ゲーム開始時(Awake)に、あなたがプレハブで配置した位置を記憶する
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

            // 記憶しておいた位置に戻す
            suitcaseObject.transform.localPosition = defaultSuitcasePosition;
            suitcaseObject.transform.localRotation = defaultSuitcaseRotation;

            // 物理挙動をリセット（持っている間は重力で落ちないようにする）
            Rigidbody2D sRb = suitcaseObject.GetComponent<Rigidbody2D>();
            if (sRb != null)
            {
                sRb.simulated = false; // 物理演算を無効化（＝その場に留まる）
                sRb.velocity = Vector2.zero; // 速度もゼロに
            }
        }
    }

    public override void TakeImpact(Vector2 playerVelocity, float knockbackMultiplier)
    {
        if (currentState == NPCState.KnockedDown) return;

        if (suitcaseObject != null && suitcaseObject.transform.parent == this.transform)
        {
            suitcaseObject.transform.SetParent(null);

            Rigidbody2D sRb = suitcaseObject.GetComponent<Rigidbody2D>();
            Collider2D sCol = suitcaseObject.GetComponent<Collider2D>();

            if (sRb == null) sRb = suitcaseObject.AddComponent<Rigidbody2D>();
            if (sCol == null) sCol = suitcaseObject.AddComponent<BoxCollider2D>();

            sRb.simulated = true;

            // Y軸固定・回転固定（これはそのまま）
            sRb.constraints = RigidbodyConstraints2D.FreezePositionY | RigidbodyConstraints2D.FreezeRotation;

            // プレイヤー衝突無視（そのまま）
            PlayerController player = FindObjectOfType<PlayerController>();
            if (player != null)
            {
                Collider2D playerCol = player.GetComponent<Collider2D>();
                if (playerCol != null && sCol != null)
                {
                    Physics2D.IgnoreCollision(sCol, playerCol, true);
                }
            }

            SuitcaseProjectile projectile = suitcaseObject.GetComponent<SuitcaseProjectile>();
            if (projectile == null) projectile = suitcaseObject.AddComponent<SuitcaseProjectile>();

            // --- 速度を直接指定してLaunchする ---

            // 進行方向の決定
            float directionX = Mathf.Sign(playerVelocity.x);
            if (Mathf.Abs(playerVelocity.x) < 0.1f)
            {
                directionX = Mathf.Sign(suitcaseObject.transform.position.x - player.transform.position.x);
            }

            // 「方向 × 速さ（ShootForce）」を計算して渡す
            Vector2 launchVelocity = new Vector2(directionX * shootForce, 0f);

            // 新しいLaunchメソッドを呼び出す
            projectile.Launch(launchVelocity);
        }

        base.TakeImpact(playerVelocity, knockbackMultiplier);
    }
}