using UnityEngine;

public class InteractionTriggerProxy : MonoBehaviour
{
    private PlayerController playerController;

    void Awake()
    {
        playerController = GetComponentInParent<PlayerController>();
        // もし見つからなかった場合、コンソールにエラーメッセージを表示する
        if (playerController == null)
        {
            Debug.LogError("親オブジェクトにPlayerControllerが見つかりません！ InteractionTriggerが正しく動作しません。");
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log("トリガーが " + other.name + " と接触しました！"); // この行を追加
        if (playerController != null)
        {
            playerController.HandleNpcCollision(other);
        }
    }
}