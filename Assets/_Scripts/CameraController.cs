using UnityEngine;

/// <summary>
/// ターゲット（プレイヤー）をスムーズに追従し、ステージ境界内でカメラ位置を制限するコントローラー
/// </summary>
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

    /// <summary>
    /// SmoothDamp内部で使用する速度ベクトル。自動的に更新される。
    /// </summary>
    private Vector3 velocity = Vector3.zero;

    /// <summary>
    /// 全てのUpdate処理が終わった後に実行され、カメラ位置を更新する。
    /// ターゲットの最終位置に基づいて追従するため、LateUpdateで処理する。
    /// </summary>
    void LateUpdate()
    {
        // ターゲット未設定時は警告を出して処理をスキップ
        if (target == null)
        {
            Debug.LogWarning("CameraController: ターゲットが設定されていません。");
            return;
        }

        // ターゲット位置にオフセットを加えた目標位置を算出
        Vector3 targetPosition = new Vector3(target.position.x, target.position.y + yOffset, zOffset);

        // SmoothDampで現在位置から目標位置へ滑らかに補間
        Vector3 smoothedPosition = Vector3.SmoothDamp(transform.position, targetPosition, ref velocity, smoothTime);

        // ステージ境界を超えないようにX/Y座標を制限
        smoothedPosition.x = Mathf.Clamp(smoothedPosition.x, minXLimit, maxXLimit);
        smoothedPosition.y = Mathf.Clamp(smoothedPosition.y, minYLimit, maxYLimit);

        // カメラ位置を確定
        transform.position = smoothedPosition;
    }

    /// <summary>
    /// Unityエディタのシーンビューにステージ境界を黄色の線で描画する。
    /// ゲームビルドには影響しない。
    /// </summary>
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