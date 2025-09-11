using UnityEngine;

public class HangingStrap : MonoBehaviour
{
    // このつり革のぶら下がりポイントのTransform
    public Transform grabPoint;

    void Awake()
    {
        // GrabPointが設定されていなければ、子オブジェクトから自動で探す
        if (grabPoint == null)
        {
            grabPoint = transform.Find("GrabPoint");
        }
    }

    void OnEnable()
    {
        // 自分が有効になったら、マネージャーのリストに自分を追加する
        HangingStrapManager.Register(this);
    }

    void OnDisable()
    {
        // 自分が無効になったら、マネージャーのリストから自分を削除する
        HangingStrapManager.Unregister(this);
    }
}