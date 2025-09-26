using System.Collections;
using UnityEngine;

public class NPCController : MonoBehaviour
{
    // 状態管理
    private enum NPCState
    {
        Idle,       // 通常時
        KnockedDown // 吹っ飛ばされてダウンしている状態
    }
    private NPCState currentState = NPCState.Idle;

    [Header("物理リアクション設定")]
    public float knockdownThreshold = 5.0f; // この速度以上の衝撃でダウンする
    public float recoveryTime = 3.0f;       // ダウンしてから起き上がるまでの時間

    [Header("自己復帰設定")]
    [Tooltip("この角度以上傾くと『転倒』と見なす")]
    public float tiltAngleThreshold = 30f;
    [Tooltip("転倒してから自動で起き上がるまでの時間")]
    public float selfRightingTime = 2.0f;

    private float tiltedTimer = 0f; // 傾いている時間を計測するタイマー

    private Rigidbody2D rb;
    private Collider2D col;
    private bool isActivated = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        rb.centerOfMass = new Vector2(0, 0.5f); // 重心を少し上に設定
    }

    void OnDisable()
    {
        StopAllCoroutines();
        currentState = NPCState.Idle;
        tiltedTimer = 0f; // タイマーをリセット
    }

    void Update()
    {
        // アイドル状態の時だけ、転倒していないかセルフチェックする
        if (currentState == NPCState.Idle)
        {
            // 現在のZ軸の回転角度を取得 (0が直立)
            float currentAngle = transform.eulerAngles.z;

            // Mathf.DeltaAngleで0度からの差を正しく計算し、その絶対値がしきい値を超えているかチェック
            if (Mathf.Abs(Mathf.DeltaAngle(currentAngle, 0)) > tiltAngleThreshold)
            {
                // 傾いていたらタイマーを進める
                tiltedTimer += Time.deltaTime;
            }
            else
            {
                // まっすぐならタイマーをリセット
                tiltedTimer = 0f;
            }

            // タイマーが自己復帰時間を超えたら、起き上がり処理を開始
            if (tiltedTimer >= selfRightingTime)
            {
                StartCoroutine(SelfRightingRoutine());
                tiltedTimer = 0f; // タイマーをリセット
            }
        }
    }

    // NPCをアクティブにするメソッド(Managerから呼ばれる)
    public void Activate()
    {
        if (isActivated) return; // すでにアクティブなら何もしない
        isActivated = true;

        rb.simulated = true; // Rigidbodyの物理シミュレーションを開始
        this.enabled = true;   // このスクリプトのUpdateなどを有効化
        rb.constraints = RigidbodyConstraints2D.FreezeRotation; // ★★★ 追加: 最初は回転を固定
    }

    // NPCをスリープさせるメソッド(Managerから呼ばれる)
    public void Deactivate()
    {
        if (!isActivated) return; // すでに非アクティブなら何もしない
        isActivated = false;

        rb.simulated = false; // Rigidbodyの物理シミュレーションを停止
        this.enabled = false;    // このスクリプトのUpdateなどを無効化
    }

    public void TakeImpact(Vector2 playerVelocity, float knockbackMultiplier)
    {
        if (currentState == NPCState.KnockedDown)
        {
            return;
        }

        Vector2 forceToApply = playerVelocity * knockbackMultiplier;
        rb.AddForce(forceToApply, ForceMode2D.Impulse);

        Debug.Log("NPCが " + forceToApply.magnitude + " の力で衝撃を受けた！");

        if (forceToApply.magnitude > knockdownThreshold)
        {
            rb.constraints = RigidbodyConstraints2D.None; // ★★★ 追加: ダウンする瞬間に回転の固定を解除
            tiltedTimer = 0f; // 念のためタイマーをリセット
            StartCoroutine(KnockdownRoutine());
        }
    }

    // プレイヤーからの衝撃でダウンした際の復帰処理
    private IEnumerator KnockdownRoutine()
    {
        currentState = NPCState.KnockedDown;
        Debug.Log("NPCがダウンした!, Frame: " + Time.frameCount);
        yield return new WaitForSeconds(recoveryTime);

        // 起き上がり処理
        rb.rotation = 0f;
        rb.angularVelocity = 0f;
        rb.velocity = Vector2.zero;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation; // ★★★ 追加: 起き上がったら再び回転を固定

        currentState = NPCState.Idle;
        Debug.Log("NPCが起き上がった!, Frame: " + Time.frameCount);
    }

    // 自己復帰するためのコルーチン
    private IEnumerator SelfRightingRoutine()
    {
        Debug.Log("自己復帰処理を開始！");
        // 他の処理と競合しないように、物理的な動きを一旦リセット
        rb.velocity = Vector2.zero;
        rb.angularVelocity = 0;

        float startRotation = rb.rotation;
        float elapsedTime = 0f;
        float duration = 0.5f; // 0.5秒かけて起き上がる

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            // LerpAngleでスムーズに角度を0(直立)に戻す
            rb.rotation = Mathf.LerpAngle(startRotation, 0f, elapsedTime / duration);
            yield return null;
        }
        rb.rotation = 0f; // 最後にきっちり0度にする
        rb.constraints = RigidbodyConstraints2D.FreezeRotation; // ★★★ 追加: 起き上がったら再び回転を固定
    }
}