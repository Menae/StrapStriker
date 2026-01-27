using UnityEngine;

/// <summary>
/// プレイヤーの子オブジェクトに配置し、トリガー衝突を親のPlayerControllerへ中継する。
/// InteractionTriggerなど、プレイヤー本体とは別のColliderでNPC接触を検知したい場合に使用。
/// </summary>
public class InteractionTriggerProxy : MonoBehaviour
{
    private PlayerController playerController;

    /// <summary>
    /// 初期化時に親オブジェクトからPlayerControllerを取得。
    /// 見つからない場合はエラーログを出力し、以降の衝突イベントは無効化される。
    /// </summary>
    void Awake()
    {
        playerController = GetComponentInParent<PlayerController>();
        if (playerController == null)
        {
            Debug.LogError("親オブジェクトにPlayerControllerが見つかりません！ InteractionTriggerが正しく動作しません。");
        }
    }

    /// <summary>
    /// 2Dトリガーに何かが侵入した際、親のPlayerControllerへNPC衝突処理を委譲する。
    /// PlayerControllerがnullの場合は何もしない。
    /// </summary>
    /// <param name="other">侵入したCollider2D</param>
    void OnTriggerEnter2D(Collider2D other)
    {
        if (playerController != null)
        {
            playerController.HandleNpcCollision(other);
        }
    }
}