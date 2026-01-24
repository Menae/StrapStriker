using UnityEngine;

/// <summary>
/// ターゲット（プレイヤー）のX軸（水平方向）のみを追従し、Y軸（高さ）を固定するカメラコントローラー。
/// 横スクロールアクションゲームにおける一般的なカメラ挙動を実現する。
/// ステージの左右端への移動制限機能、およびSceneビューでの配置を維持するオフセット機能を備える。
/// </summary>
public class CameraController : MonoBehaviour
{
    // ====================================================================
    // 設定項目 (Inspector)
    // ====================================================================

    [Header("追従対象")]
    [Tooltip("カメラが追従するターゲット（プレイヤー）。")]
    [SerializeField] private Transform target;

    [Header("オフセット設定")]
    [Tooltip("有効の場合、ゲーム開始時の「カメラとターゲットの位置関係」を維持して追従を開始する。\n無効の場合、ターゲットのX座標に完全に重なるように補正される。")]
    [SerializeField] private bool useSceneViewOffset = true;

    [Header("カメラ挙動")]
    [Tooltip("ターゲットの動きに対してカメラが追いつくまでの遅延時間（秒）。\n値が小さいほど機敏に、大きいほど滑らかに追従する。")]
    [Range(0.01f, 1.0f)]
    [SerializeField] private float smoothTime = 0.3f;

    [Header("ステージ境界設定 (X軸)")]
    [Tooltip("カメラ移動の左端限界座標。これより左にはスクロールしない。")]
    [SerializeField] private float minXLimit = -10.0f;

    [Tooltip("カメラ移動の右端限界座標。これより右にはスクロールしない。")]
    [SerializeField] private float maxXLimit = 10.0f;

    // ====================================================================
    // 内部変数
    // ====================================================================

    /// <summary>
    /// ターゲットとのX軸方向の距離（オフセット）。
    /// </summary>
    private float xOffset = 0.0f;

    /// <summary>
    /// ゲーム開始時に決定される、カメラの固定Y座標。
    /// プレイヤーがジャンプしてもこの高さは維持される。
    /// </summary>
    private float fixedYPosition;

    /// <summary>
    /// ゲーム開始時に決定される、カメラの固定Z座標。
    /// </summary>
    private float fixedZPosition;

    /// <summary>
    /// Vector3.SmoothDampで使用する現在の速度参照変数は。
    /// </summary>
    private Vector3 currentVelocity = Vector3.zero;

    // ====================================================================
    // 実行処理
    // ====================================================================

    /// <summary>
    /// 初期化処理。
    /// Sceneビュー上で配置されたカメラの座標を元に、追従の基準となるオフセットと固定軸を決定する。
    /// </summary>
    private void Start()
    {
        if (target == null)
        {
            Debug.LogWarning("CameraController: ターゲットが設定されていません。");
            return;
        }

        // 高さ(Y)と奥行き(Z)は、配置された位置で固定する
        fixedYPosition = transform.position.y;
        fixedZPosition = transform.position.z;

        // 水平方向(X)のオフセット計算
        if (useSceneViewOffset)
        {
            // 配置された「ズレ」を維持する
            xOffset = transform.position.x - target.position.x;
        }
        else
        {
            // ターゲットの中心を捉える
            xOffset = 0f;
        }
    }

    /// <summary>
    /// カメラ位置の更新処理。
    /// ターゲットの移動完了後（LateUpdate）に実行することで、ジッター（微細な震え）を防止する。
    /// </summary>
    private void LateUpdate()
    {
        if (target == null) return;

        // 目標座標の算出
        // X軸: ターゲットの現在位置 + 初期オフセット
        // Y軸: 固定値
        // Z軸: 固定値
        Vector3 targetPosition = new Vector3(
            target.position.x + xOffset,
            fixedYPosition,
            fixedZPosition
        );

        // 現在位置から目標位置へ滑らかに移動させる（減衰振動）
        Vector3 smoothedPosition = Vector3.SmoothDamp(
            transform.position,
            targetPosition,
            ref currentVelocity,
            smoothTime
        );

        // ステージの左右境界を超えないようにX座標を制限（クランプ）する
        smoothedPosition.x = Mathf.Clamp(smoothedPosition.x, minXLimit, maxXLimit);

        // 最終的な座標を適用
        transform.position = smoothedPosition;
    }

    /// <summary>
    /// エディタのSceneビューに移動制限範囲（境界線）を可視化するデバッグ描画。
    /// </summary>
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        float currentZ = transform.position.z;
        float infiniteY = 1000f; // 垂直方向の境界線を視覚的に表現するための仮の高さ

        // 左端の境界線を描画
        Gizmos.DrawLine(
            new Vector3(minXLimit, -infiniteY, currentZ),
            new Vector3(minXLimit, infiniteY, currentZ)
        );

        // 右端の境界線を描画
        Gizmos.DrawLine(
            new Vector3(maxXLimit, -infiniteY, currentZ),
            new Vector3(maxXLimit, infiniteY, currentZ)
        );
    }
}