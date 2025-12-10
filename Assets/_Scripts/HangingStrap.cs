using UnityEngine;

/// <summary>
/// つり革オブジェクトを表すコンポーネント。
/// プレイヤーがぶら下がるための位置情報（GrabPoint）を管理し、
/// 有効化・無効化時にマネージャーへの登録/解除を自動で行う。
/// </summary>
public class HangingStrap : MonoBehaviour
{
    /// <summary>
    /// プレイヤーがぶら下がる際の基準位置。
    /// 未設定の場合、Awake時に子オブジェクト「GrabPoint」を自動検索する。
    /// </summary>
    public Transform grabPoint;

    /// <summary>
    /// 初期化処理。Unity起動時に一度だけ実行される。
    /// GrabPointが未設定の場合、子オブジェクトから「GrabPoint」という名前のTransformを検索して自動設定する。
    /// </summary>
    void Awake()
    {
        if (grabPoint == null)
        {
            grabPoint = transform.Find("GrabPoint");
        }
    }

    /// <summary>
    /// オブジェクトが有効化された際に実行される。
    /// HangingStrapManagerに自身を登録し、検出可能な状態にする。
    /// </summary>
    void OnEnable()
    {
        HangingStrapManager.Register(this);
    }

    /// <summary>
    /// オブジェクトが無効化された際に実行される。
    /// HangingStrapManagerから自身を削除し、検出対象から除外する。
    /// </summary>
    void OnDisable()
    {
        HangingStrapManager.Unregister(this);
    }
}