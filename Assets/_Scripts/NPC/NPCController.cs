using System.Collections;
using UnityEngine;

public class NPCController : MonoBehaviour
{
    [Header("NPC設定")]
    public NPCType npcType = NPCType.Normal;

    protected enum NPCState { Idle, KnockedDown }
    protected NPCState currentState = NPCState.Idle;

    [Header("パーソナルスペースAI設定")]
    public float personalSpaceRadius = 0.5f;
    public float separationForce = 1.0f;

    // GC Allocationを防ぐためのバッファ
    private readonly Collider2D[] _nearCollidersBuffer = new Collider2D[10];

    [Header("物理リアクション設定")]
    public float satThreshold = 5.0f;
    public float fallenThreshold = 40f;
    public float recoveryTime = 3.0f;

    [Header("消滅設定")]
    public float timeBeforeFade = 2.0f;
    public float fadeDuration = 1.0f;

    protected Rigidbody2D rb;
    protected Collider2D col;
    protected SpriteRenderer visualSprite;
    protected Animator animator;
    protected StageManager stageManager;
    protected bool isActivated = false;

    // Animatorパラメータのハッシュ化
    protected static readonly int AnimHashFallen = Animator.StringToHash("Fallen");
    protected static readonly int AnimHashSat = Animator.StringToHash("Sat");

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        visualSprite = GetComponentInChildren<SpriteRenderer>();
        animator = GetComponentInChildren<Animator>();

        if (rb != null) rb.centerOfMass = new Vector2(0, 0.5f);
    }

    protected virtual void OnDisable()
    {
        StopAllCoroutines();
        currentState = NPCState.Idle;
        // 色のリセット処理
        if (visualSprite != null)
        {
            var c = visualSprite.color;
            visualSprite.color = new Color(c.r, c.g, c.b, 1f);
        }
    }

    protected virtual void FixedUpdate()
    {
        if (currentState == NPCState.Idle && isActivated)
        {
            PerformSeparation();
        }
    }

    // 分離ロジックをメソッド抽出
    private void PerformSeparation()
    {
        int layerMask = LayerMask.GetMask("NPC");

        // NonAllocを使用してGCを回避
        int count = Physics2D.OverlapCircleNonAlloc(transform.position, personalSpaceRadius, _nearCollidersBuffer, layerMask);

        if (count == 0) return;

        Vector2 totalSeparationForce = Vector2.zero;
        int separationCount = 0;

        for (int i = 0; i < count; i++)
        {
            var otherCol = _nearCollidersBuffer[i];
            if (otherCol.gameObject == gameObject) continue;

            float distance = Vector2.Distance(transform.position, otherCol.transform.position);
            // ゼロ除算対策
            if (distance <= Mathf.Epsilon) continue;

            float strength = 1.0f - (distance / personalSpaceRadius);
            Vector2 pushDirection = (transform.position - otherCol.transform.position).normalized;
            totalSeparationForce += pushDirection * strength;
            separationCount++;
        }

        if (separationCount > 0)
        {
            totalSeparationForce.y = 0; // 横軸のみに制限
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
        // 既に倒れている場合は物理演算を残す等の判断が必要ならここに記述
        if (currentState == NPCState.KnockedDown) return;

        isActivated = false;
        rb.simulated = false;
        this.enabled = false;
        rb.velocity = Vector2.zero;
    }

    /// <summary>
    /// 衝撃処理
    /// 呼び出し元で計算済みの「最終的な衝撃ベクトル」と「加害者」を受け取る
    /// </summary>
    public virtual void TakeImpact(Vector2 impactForce, GameObject instigator)
    {
        if (currentState == NPCState.KnockedDown) return;

        float impactMagnitude = impactForce.magnitude;
        rb.AddForce(impactForce, ForceMode2D.Impulse);

        if (impactMagnitude > fallenThreshold)
        {
            HandleDefeat();
        }
        else if (impactMagnitude > satThreshold)
        {
            currentState = NPCState.KnockedDown;
            animator.SetTrigger(AnimHashSat);
            StartCoroutine(SatRecoveryRoutine());
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
        animator.SetTrigger(AnimHashFallen);
        StartCoroutine(FadeOutAndDespawnRoutine());
    }

    protected IEnumerator SatRecoveryRoutine()
    {
        yield return new WaitForSeconds(recoveryTime);
        currentState = NPCState.Idle;
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