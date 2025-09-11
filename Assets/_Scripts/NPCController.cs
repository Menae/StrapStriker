// NPCController.cs
using System.Collections;
using UnityEngine;

public class NPCController : MonoBehaviour
{
    // ========== 状態管理 ==========
    private enum NPCState
    {
        Idle,       // 通常時
        KnockedDown // 吹っ飛ばされてダウンしている状態
    }
    private NPCState currentState = NPCState.Idle;

    [Header("物理リアクション設定")]
    public float knockdownThreshold = 5.0f; // この速度以上の衝撃でダウンする
    public float recoveryTime = 3.0f;       // ダウンしてから起き上がるまでの時間

    // ========== 内部変数 ==========
    private Rigidbody2D rb;
    private Collider2D col;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
    }

    // PlayerControllerから呼び出される、衝撃を受けた時のメイン処理
    public void TakeImpact(Vector2 playerVelocity, float knockbackMultiplier)
    {
        // すでにダウンしている時は何もしない
        if (currentState == NPCState.KnockedDown)
        {
            return;
        }

        // プレイヤーの速度（勢い）に倍率を掛けて、NPCに加える力を計算
        Vector2 forceToApply = playerVelocity * knockbackMultiplier;
        rb.AddForce(forceToApply, ForceMode2D.Impulse);

        Debug.Log("NPCが " + forceToApply.magnitude + " の力で衝撃を受けた！");

        // 衝撃の強さが閾値を超えたら、ダウン処理を開始
        if (forceToApply.magnitude > knockdownThreshold)
        {
            StartCoroutine(KnockdownRoutine());
        }
    }

    // ダウンしてから復帰するまでの一連の流れを管理するコルーチン
    private IEnumerator KnockdownRoutine()
    {
        // 状態をダウンに変更
        currentState = NPCState.KnockedDown;
        Debug.Log("NPCがダウンした!, Frame: " + Time.frameCount);

        // 指定された時間、待機する
        yield return new WaitForSeconds(recoveryTime);

        // 起き上がり処理
        // （ここではシンプルに、回転をリセットし、速度をゼロにする）
        rb.rotation = 0f;
        rb.angularVelocity = 0f;
        rb.velocity = Vector2.zero;

        // 状態を通常に戻す
        currentState = NPCState.Idle;
        Debug.Log("NPCが起き上がった!, Frame: " + Time.frameCount);
    }
}