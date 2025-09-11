using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("追従対象")]
    [Tooltip("カメラが追従するターゲット（通常はプレイヤー）")]
    public Transform target;

    [Header("カメラ設定")]
    [Tooltip("カメラがターゲットに追従する際の滑らかさ。値が小さいほど速く追従します。")]
    [Range(0.01f, 1.0f)]
    public float smoothTime = 0.3f;
    [Tooltip("カメラの初期Y座標オフセット")]
    public float yOffset = 2.0f;
    [Tooltip("カメラの初期Z座標オフセット (2Dゲームでは通常-10など)")]
    public float zOffset = -10.0f;

    [Header("ステージ境界設定")]
    [Tooltip("カメラがこれ以上左に移動しないX座標")]
    public float minXLimit = -10.0f;
    [Tooltip("カメラがこれ以上右に移動しないX座標")]
    public float maxXLimit = 10.0f;
    [Tooltip("カメラがこれ以上下に移動しないY座標")]
    public float minYLimit = 0.0f;
    [Tooltip("カメラがこれ以上上に移動しないY座標")]
    public float maxYLimit = 15.0f;

    private Vector3 velocity = Vector3.zero; // SmoothDampで内部的に使用する速度

    void LateUpdate()
    {
        if (target == null)
        {
            Debug.LogWarning("CameraController: ターゲットが設定されていません。");
            return;
        }

        // ターゲットの新しい目標位置を計算
        Vector3 targetPosition = new Vector3(target.position.x, target.position.y + yOffset, zOffset);

        // SmoothDampを使って現在のカメラ位置から目標位置へ滑らかに移動
        Vector3 smoothedPosition = Vector3.SmoothDamp(transform.position, targetPosition, ref velocity, smoothTime);

        // ステージ境界を超えないようにX座標とY座標をクランプ（制限）
        smoothedPosition.x = Mathf.Clamp(smoothedPosition.x, minXLimit, maxXLimit);
        smoothedPosition.y = Mathf.Clamp(smoothedPosition.y, minYLimit, maxYLimit);

        // カメラの位置を更新
        transform.position = smoothedPosition;
    }

    // デバッグ用にステージ境界をシーンビューに表示
    void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Vector3 cameraPos = transform.position;

        // 左右の境界線
        Gizmos.DrawLine(new Vector3(minXLimit, minYLimit, cameraPos.z), new Vector3(minXLimit, maxYLimit, cameraPos.z));
        Gizmos.DrawLine(new Vector3(maxXLimit, minYLimit, cameraPos.z), new Vector3(maxXLimit, maxYLimit, cameraPos.z));

        // 上下の境界線
        Gizmos.DrawLine(new Vector3(minXLimit, minYLimit, cameraPos.z), new Vector3(maxXLimit, minYLimit, cameraPos.z));
        Gizmos.DrawLine(new Vector3(minXLimit, maxYLimit, cameraPos.z), new Vector3(maxXLimit, maxYLimit, cameraPos.z));
    }
}