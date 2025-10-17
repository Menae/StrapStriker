using UnityEngine;

public class AnimationEventProxy : MonoBehaviour
{
    // 親にいるPlayerControllerへの参照
    private PlayerController playerController;

    void Awake()
    {
        // 自分の親オブジェクトからPlayerControllerを探して、変数に保存しておく
        playerController = GetComponentInParent<PlayerController>();
    }

    /// <summary>
    /// アニメーションイベントから呼び出される、転送専用のメソッド
    /// </summary>
    public void ForwardJumpAnimationFinished()
    {
        // PlayerControllerが見つかっていれば、本来のメソッドを呼び出す
        if (playerController != null)
        {
            playerController.OnJumpAnimationFinished();
        }
    }
}