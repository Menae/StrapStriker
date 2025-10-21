using System.Collections;
using UnityEngine;

public class NPCController : MonoBehaviour
{
    // NPCの状態管理
    private enum NPCState
    {
        Idle,       // 通常時
        KnockedDown // 座る、または倒れている状態
    }
    private NPCState currentState = NPCState.Idle;

    [Header("パーソナルスペースAI設定")]
    [Tooltip("この半径内に他のNPCがいると、離れようとします")]
    public float personalSpaceRadius = 0.5f;
    [Tooltip("他のNPCから離れる時の力の強さ")]
    public float separationForce = 1.0f;

    [Header("物理リアクション設定")]
    [Tooltip("この衝撃の強さを超えると『座る』アニメーションを再生")]
    public float satThreshold = 5.0f;
    [Tooltip("この衝撃の強さを超えると『倒れる』アニメーションを再生して消滅")]
    public float fallenThreshold = 40f;
    [Tooltip("『座る』状態から復帰するまでの時間")]
    public float recoveryTime = 3.0f;

    [Header("消滅設定")]
    [Tooltip("衝撃を受けてからフェードアウトが始まるまでの待機時間")]
    public float timeBeforeFade = 2.0f;
    [Tooltip("フェードアウトにかかる時間")]
    public float fadeDuration = 1.0f;

    // --- 内部変数 ---
    private Rigidbody2D rb;
    private Collider2D col;
    private bool isActivated = false;
    private SpriteRenderer visualSprite;
    private Animator animator;
    private StageManager stageManager; // StageManagerへの参照

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        rb.centerOfMass = new Vector2(0, 0.5f);
        visualSprite = GetComponentInChildren<SpriteRenderer>();
        animator = GetComponentInChildren<Animator>();
    }

    void OnDisable()
    {
        StopAllCoroutines();
        currentState = NPCState.Idle;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        // プールに戻る際にアルファ値を1(不透明)に戻す
        if (visualSprite != null)
        {
            visualSprite.color = new Color(visualSprite.color.r, visualSprite.color.g, visualSprite.color.b, 1f);
        }
    }

    void FixedUpdate()
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

            // Y方向の力を無視し、X方向(左右)にのみ力を加えるようにする
            totalSeparationForce.y = 0;
            rb.AddForce(totalSeparationForce * separationForce);
        }
    }

    /// <summary>
    /// StageManagerをセットするための公開メソッド
    /// </summary>
    public void SetStageManager(StageManager manager)
    {
        stageManager = manager;
    }

    /// <summary>
    /// NPCをアクティブにするメソッド(Managerから呼ばれる)
    /// </summary>
    public void Activate()
    {
        if (isActivated) return;
        isActivated = true;
        rb.simulated = true;
        this.enabled = true;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
    }

    /// <summary>
    /// NPCをスリープさせるメソッド(Managerから呼ばれる)
    /// </summary>
    public void Deactivate()
    {
        if (currentState == NPCState.KnockedDown) return;
        if (!isActivated) return;

        isActivated = false;
        rb.simulated = false;
        this.enabled = false;

        rb.velocity = Vector2.zero; // スリープする際に速度をリセット
    }

    /// <summary>
    /// プレイヤーからの衝撃を受けた際のメイン処理
    /// </summary>
    public void TakeImpact(Vector2 playerVelocity, float knockbackMultiplier)
    {
        // すでにダウン中は新しい衝撃を受け付けない
        if (currentState == NPCState.KnockedDown)
        {
            return;
        }

        Vector2 forceToApply = playerVelocity * knockbackMultiplier;
        float impactMagnitude = forceToApply.magnitude;
        rb.AddForce(forceToApply, ForceMode2D.Impulse);

        Debug.Log("NPCが " + impactMagnitude + " の力で衝撃を受けた！");

        // 衝撃が「倒れる」しきい値を超えた場合
        if (impactMagnitude > fallenThreshold)
        {
            currentState = NPCState.KnockedDown;
            animator.SetTrigger("Fallen");
            StartCoroutine(FadeOutAndDespawnRoutine());
        }
        // 衝撃が「座る」しきい値を超えた場合
        else if (impactMagnitude > satThreshold)
        {
            currentState = NPCState.KnockedDown;
            animator.SetTrigger("Sat");
            StartCoroutine(SatRecoveryRoutine()); // 座りからの復帰コルーチン
        }
    }

    /// <summary>
    /// 「座る」状態から一定時間後にアイドル状態に戻すコルーチン
    /// </summary>
    private IEnumerator SatRecoveryRoutine()
    {
        yield return new WaitForSeconds(recoveryTime);
        // アニメーション側でアイドル状態に戻っていることを想定
        currentState = NPCState.Idle;
    }

    /// <summary>
    /// フェードアウトしてオブジェクトプールに自身を返却するコルーチン
    /// </summary>
    private IEnumerator FadeOutAndDespawnRoutine()
    {
        currentState = NPCState.KnockedDown;
        Debug.Log("NPCが消滅シーケンスを開始！");

        // フェードが始まるまで待機
        yield return new WaitForSeconds(timeBeforeFade);

        // 既存のフェード処理
        float timer = 0f;
        Color startColor = visualSprite.color;
        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, timer / fadeDuration);
            visualSprite.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
            yield return null;
        }

        // 司令塔に報告
        if (stageManager != null)
        {
            stageManager.OnNpcDefeated();
        }
        else
        {
            Debug.LogError("<color=red>エラー: StageManagerへの参照がありません！報告できませんでした。</color>");
        }

        // 完全に消滅したら、プールに自身を返却する
        NPCPool.instance.ReturnNPC(this.gameObject);
    }
}