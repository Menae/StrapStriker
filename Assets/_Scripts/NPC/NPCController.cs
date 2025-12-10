using System.Collections;
using UnityEngine;

/// <summary>
/// NPCの物理挙動、パーソナルスペース、衝撃反応、消滅処理を統合管理するコントローラー。
/// プレイヤーからの衝撃に応じて「座る」「倒れる」を切り替え、倒れた場合はプールに返却する。
/// </summary>
public class NPCController : MonoBehaviour
{
    /// <summary>
    /// NPCの行動状態。
    /// Idle時のみパーソナルスペース処理が有効になる。
    /// </summary>
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

    private Rigidbody2D rb;
    private Collider2D col;
    private bool isActivated = false;
    private SpriteRenderer visualSprite;
    private Animator animator;
    private StageManager stageManager;

    /// <summary>
    /// Awakeで実行される初期化処理。
    /// Rigidbody2DとCollider2D、SpriteRenderer、Animatorの参照を取得し、
    /// 重心を上部に設定して物理挙動を調整する。
    /// </summary>
    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        rb.centerOfMass = new Vector2(0, 0.5f);
        visualSprite = GetComponentInChildren<SpriteRenderer>();
        animator = GetComponentInChildren<Animator>();
    }

    /// <summary>
    /// OnDisableで実行されるクリーンアップ処理。
    /// プールへの返却時に全コルーチンを停止し、状態とアルファ値を初期化する。
    /// 次回のスポーン時に正常な状態で再利用できるようにする。
    /// </summary>
    void OnDisable()
    {
        StopAllCoroutines();
        currentState = NPCState.Idle;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        if (visualSprite != null)
        {
            visualSprite.color = new Color(visualSprite.color.r, visualSprite.color.g, visualSprite.color.b, 1f);
        }
    }

    /// <summary>
    /// FixedUpdateで実行される物理演算処理。
    /// Idle状態かつアクティブ時のみ、パーソナルスペース範囲内の他NPCから離れるようにX軸方向の力を加える。
    /// Y軸方向の力は無視し、左右にのみ反発力を適用する。
    /// </summary>
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

            totalSeparationForce.y = 0;
            rb.AddForce(totalSeparationForce * separationForce);
        }
    }

    /// <summary>
    /// StageManagerの参照を設定する。
    /// NPCPool側で各NPCをスポーンする際に呼び出され、撃破時の通知先を確保する。
    /// </summary>
    /// <param name="manager">ステージ全体を管理するStageManagerインスタンス</param>
    public void SetStageManager(StageManager manager)
    {
        stageManager = manager;
    }

    /// <summary>
    /// NPCをアクティブ化し、物理演算とFixedUpdateを有効にする。
    /// NPCManagerから呼び出され、画面内に入った際に動作を開始させる。
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
    /// NPCをスリープ状態にし、物理演算とFixedUpdateを無効化する。
    /// 画面外に出た際にNPCManagerから呼び出され、CPU負荷を軽減する。
    /// KnockedDown状態の場合は消滅処理を優先するためスリープしない。
    /// </summary>
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
    /// プレイヤーからの衝撃を受けた際のメイン処理。
    /// 衝撃の強さに応じて「座る」または「倒れる」アニメーションを再生し、状態を遷移させる。
    /// 倒れる場合は即座にStageManagerに撃破を通知し、カウントを加算する。
    /// KnockedDown状態中は新しい衝撃を受け付けない。
    /// </summary>
    /// <param name="playerVelocity">プレイヤーの速度ベクトル</param>
    /// <param name="knockbackMultiplier">ノックバック倍率</param>
    public void TakeImpact(Vector2 playerVelocity, float knockbackMultiplier)
    {
        if (currentState == NPCState.KnockedDown)
        {
            return;
        }

        Vector2 forceToApply = playerVelocity * knockbackMultiplier;
        float impactMagnitude = forceToApply.magnitude;
        rb.AddForce(forceToApply, ForceMode2D.Impulse);

        Debug.Log("NPCが " + impactMagnitude + " の力で衝撃を受けた！");

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

    /// <summary>
    /// 「座る」状態から一定時間後にIdle状態に復帰させるコルーチン。
    /// recoveryTimeの経過後、アニメーションがIdleに戻ることを前提として状態を遷移させる。
    /// </summary>
    private IEnumerator SatRecoveryRoutine()
    {
        yield return new WaitForSeconds(recoveryTime);
        currentState = NPCState.Idle;
    }

    /// <summary>
    /// フェードアウトしてオブジェクトプールに自身を返却するコルーチン。
    /// timeBeforeFade後にフェード開始し、fadeDuration秒かけてアルファ値を0にする。
    /// フェード完了後、NPCPoolに返却される。
    /// </summary>
    private IEnumerator FadeOutAndDespawnRoutine()
    {
        currentState = NPCState.KnockedDown;
        Debug.Log("NPCが消滅シーケンスを開始！");

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

        NPCPool.instance.ReturnNPC(this.gameObject);
    }
}