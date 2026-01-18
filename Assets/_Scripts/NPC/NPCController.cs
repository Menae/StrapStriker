using System.Collections;
using UnityEngine;

/// <summary>
/// NPCの基底クラス。
/// 共通の物理挙動、パーソナルスペース、消滅処理を管理する。
/// 特殊な敵を作る場合はこのクラスを継承する。
/// </summary>
public class NPCController : MonoBehaviour
{
    // 自身のタイプ（Inspectorで設定、または継承先で指定）
    [Header("NPC設定")]
    public NPCType npcType = NPCType.Normal;

    protected enum NPCState
    {
        Idle,       // 通常時
        KnockedDown // 座る、または倒れている状態
    }
    protected NPCState currentState = NPCState.Idle;

    [Header("パーソナルスペースAI設定")]
    public float personalSpaceRadius = 0.5f;
    public float separationForce = 1.0f;

    [Header("物理リアクション設定")]
    public float satThreshold = 5.0f;
    public float fallenThreshold = 40f;
    public float recoveryTime = 3.0f;

    [Header("消滅設定")]
    public float timeBeforeFade = 2.0f;
    public float fadeDuration = 1.0f;

    // protectedに変更（継承先で使えるようにする）
    protected Rigidbody2D rb;
    protected Collider2D col;
    protected bool isActivated = false;
    protected SpriteRenderer visualSprite;
    protected Animator animator;
    protected StageManager stageManager;

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        rb.centerOfMass = new Vector2(0, 0.5f);
        visualSprite = GetComponentInChildren<SpriteRenderer>();
        animator = GetComponentInChildren<Animator>();
    }

    protected virtual void OnDisable()
    {
        StopAllCoroutines();
        currentState = NPCState.Idle;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        if (visualSprite != null)
        {
            visualSprite.color = new Color(visualSprite.color.r, visualSprite.color.g, visualSprite.color.b, 1f);
        }
    }

    protected virtual void FixedUpdate()
    {
        if (currentState == NPCState.Idle && isActivated)
        {
            int layerMask = LayerMask.GetMask("NPC");
            Collider2D[] nearColliders = Physics2D.OverlapCircleAll(transform.position, personalSpaceRadius, layerMask);

            Vector2 totalSeparationForce = Vector2.zero;

            foreach (var col in nearColliders)
            {
                if (col.gameObject == this.gameObject) continue;

                float distance = Vector2.Distance(transform.position, col.transform.position);
                float strength = 1.0f - (distance / personalSpaceRadius);
                Vector2 pushDirection = (transform.position - col.transform.position).normalized;
                totalSeparationForce += pushDirection * strength;
            }

            totalSeparationForce.y = 0;
            rb.AddForce(totalSeparationForce * separationForce);
        }
    }

    public void SetStageManager(StageManager manager)
    {
        stageManager = manager;
    }

    public void Activate()
    {
        if (isActivated) return;
        isActivated = true;
        rb.simulated = true;
        this.enabled = true;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
    }

    public void Deactivate()
    {
        if (currentState == NPCState.KnockedDown) return;
        if (!isActivated) return;

        isActivated = false;
        rb.simulated = false;
        this.enabled = false;
        rb.velocity = Vector2.zero;
    }

    /// <summary>
    /// 衝撃を受けた際の処理。
    /// virtual にすることで、継承先で独自のリアクション（爆発、カウンター等）を実装可能にする。
    /// </summary>
    public virtual void TakeImpact(Vector2 playerVelocity, float knockbackMultiplier)
    {
        if (currentState == NPCState.KnockedDown) return;

        Vector2 forceToApply = playerVelocity * knockbackMultiplier;
        float impactMagnitude = forceToApply.magnitude;
        rb.AddForce(forceToApply, ForceMode2D.Impulse);

        // Debug.Log("NPCが " + impactMagnitude + " の力で衝撃を受けた！");

        if (impactMagnitude > fallenThreshold)
        {
            if (stageManager != null)
            {
                stageManager.PlayNpcDefeatSound();
                stageManager.OnNpcDefeated();
            }

            currentState = NPCState.KnockedDown;
            animator.SetTrigger("Fallen");
            StartCoroutine(FadeOutAndDespawnRoutine());
        }
        else if (impactMagnitude > satThreshold)
        {
            currentState = NPCState.KnockedDown;
            animator.SetTrigger("Sat");
            StartCoroutine(SatRecoveryRoutine());
        }
    }

    protected IEnumerator SatRecoveryRoutine()
    {
        yield return new WaitForSeconds(recoveryTime);
        currentState = NPCState.Idle;
    }

    protected IEnumerator FadeOutAndDespawnRoutine()
    {
        currentState = NPCState.KnockedDown;
        // Debug.Log("NPCが消滅シーケンスを開始！");

        yield return new WaitForSeconds(timeBeforeFade);

        float timer = 0f;
        Color startColor = visualSprite.color;
        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, timer / fadeDuration);
            visualSprite.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
            yield return null;
        }

        // 返却時に自身のタイプを指定する
        NPCPool.instance.ReturnNPC(this.gameObject, this.npcType);
    }
}