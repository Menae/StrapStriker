using System.Collections;
using UnityEngine;

public class NPCController : MonoBehaviour
{
    [Header("NPC設定")]
    [Tooltip("プーリング識別用")]
    public NPCType npcType = NPCType.NormalA;

    protected enum NPCState { Idle, KnockedDown }
    protected NPCState currentState = NPCState.Idle;

    [Header("パーソナルスペースAI設定")]
    public float personalSpaceRadius = 0.5f;
    public float separationForce = 1.0f;
    private readonly Collider2D[] _nearCollidersBuffer = new Collider2D[10];

    [Header("物理リアクション設定")]
    [Tooltip("この衝撃値を超えるとダウンする（これ未満なら微動だにしない）")]
    public float fallenThreshold = 40f;

    [Tooltip("NPCの重さ (RigidbodyのMassに適用)")]
    public float weight = 1.0f;

    [Header("消滅設定")]
    public float timeBeforeFade = 2.0f;
    public float fadeDuration = 1.0f;

    protected Rigidbody2D rb;
    protected Collider2D col;
    protected SpriteRenderer visualSprite;
    protected Animator animator;
    protected StageManager stageManager;
    protected bool isActivated = false;

    protected static readonly int AnimHashFallen = Animator.StringToHash("Fallen");

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        visualSprite = GetComponentInChildren<SpriteRenderer>();
        animator = GetComponentInChildren<Animator>();

        if (rb != null)
        {
            rb.centerOfMass = new Vector2(0, 0.5f);
            rb.mass = weight; // 重さを適用
        }
    }

    protected virtual void OnDisable()
    {
        StopAllCoroutines();
        currentState = NPCState.Idle;
        if (visualSprite != null)
        {
            var c = visualSprite.color;
            visualSprite.color = new Color(c.r, c.g, c.b, 1f);
        }
    }

    protected virtual void FixedUpdate()
    {
        // 倒れていない時のみ、重なり防止の微弱な力をかける
        if (currentState == NPCState.Idle && isActivated)
        {
            PerformSeparation();
        }
    }

    private void PerformSeparation()
    {
        int layerMask = LayerMask.GetMask("NPC");
        int count = Physics2D.OverlapCircleNonAlloc(transform.position, personalSpaceRadius, _nearCollidersBuffer, layerMask);
        if (count == 0) return;

        Vector2 totalSeparationForce = Vector2.zero;
        int separationCount = 0;

        for (int i = 0; i < count; i++)
        {
            var otherCol = _nearCollidersBuffer[i];
            if (otherCol.gameObject == gameObject) continue;
            float distance = Vector2.Distance(transform.position, otherCol.transform.position);
            if (distance <= Mathf.Epsilon) continue;

            float strength = 1.0f - (distance / personalSpaceRadius);
            Vector2 pushDirection = (transform.position - otherCol.transform.position).normalized;
            totalSeparationForce += pushDirection * strength;
            separationCount++;
        }

        if (separationCount > 0)
        {
            totalSeparationForce.y = 0;
            rb.AddForce(totalSeparationForce * separationForce);
        }
    }

    public void SetStageManager(StageManager manager) => stageManager = manager;

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
        isActivated = false;
        rb.simulated = false;
        this.enabled = false;
        rb.velocity = Vector2.zero;
    }

    /// <summary>
    /// 閾値を超えた時だけ AddForce と ダウン処理を行う。
    /// </summary>
    public virtual void TakeImpact(Vector2 impactForce, GameObject instigator)
    {
        if (currentState == NPCState.KnockedDown) return;

        float impactMagnitude = impactForce.magnitude;

        // 閾値判定
        if (impactMagnitude > fallenThreshold)
        {
            // 閾値を超えた：吹っ飛んでダウン
            rb.AddForce(impactForce, ForceMode2D.Impulse);
            HandleDefeat();
        }
        else
        {
            // 閾値以下：微動だにしない
        }
    }

    protected void HandleDefeat()
    {
        if (stageManager != null)
        {
            stageManager.PlayNpcDefeatSound();
            stageManager.OnNpcDefeated();
        }

        currentState = NPCState.KnockedDown;

        // アニメーションは Fallen のみ
        if (animator != null) animator.SetTrigger(AnimHashFallen);

        StartCoroutine(FadeOutAndDespawnRoutine());
    }

    protected IEnumerator FadeOutAndDespawnRoutine()
    {
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

        NPCPool.instance.ReturnNPC(this.gameObject, this.npcType);
    }
}