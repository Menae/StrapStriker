using System.Collections;
using UnityEngine;

/// <summary>
/// 電車のドア開閉アニメーションを管理するコンポーネント。
/// 左右のドアを指定距離だけ移動させ、一定時間開いた後に閉じる。
/// </summary>
public class TrainDoor : MonoBehaviour
{
    [Header("ドアのオブジェクト設定")]
    /// <summary>
    /// 左側のドアオブジェクト。Inspectorで設定。
    /// </summary>
    public GameObject leftDoor;

    /// <summary>
    /// 右側のドアオブジェクト。Inspectorで設定。
    /// </summary>
    public GameObject rightDoor;

    [Header("ドアの動作設定")]
    /// <summary>
    /// ドアが開閉する際のアニメーション時間（秒）。
    /// </summary>
    public float openDuration = 0.5f;

    /// <summary>
    /// ドアが開いたままの状態を維持する時間（秒）。
    /// </summary>
    public float stayOpenTime = 2.0f;

    /// <summary>
    /// ドアが開く際の移動距離（ユニット単位）。
    /// 左ドアは左に、右ドアは右にこの距離だけ移動する。
    /// </summary>
    public float openDistance = 3.0f;

    /// <summary>
    /// 左ドアの初期位置。Start時に記録される。
    /// </summary>
    private Vector3 leftDoorInitialPos;

    /// <summary>
    /// 右ドアの初期位置。Start時に記録される。
    /// </summary>
    private Vector3 rightDoorInitialPos;

    /// <summary>
    /// アニメーション実行中フラグ。多重実行を防止する。
    /// </summary>
    private bool isAnimating = false;

    /// <summary>
    /// 初期化処理。ドアの初期位置を記録する。
    /// Unity起動時、またはオブジェクトがシーンに追加された際に1回だけ呼ばれる。
    /// </summary>
    void Start()
    {
        if (leftDoor != null) leftDoorInitialPos = leftDoor.transform.position;
        if (rightDoor != null) rightDoorInitialPos = rightDoor.transform.position;
    }

    /// <summary>
    /// オブジェクトが有効化された際の処理。
    /// DoorManagerにこのドアを登録する。
    /// </summary>
    void OnEnable()
    {
        DoorManager.Register(this);
    }

    /// <summary>
    /// オブジェクトが無効化された際の処理。
    /// DoorManagerからこのドアを登録解除する。
    /// </summary>
    void OnDisable()
    {
        DoorManager.Unregister(this);
    }

    /// <summary>
    /// ドアの開閉アニメーションを開始する。
    /// アニメーション実行中は多重実行を防止するため処理をスキップする。
    /// </summary>
    public void OpenAndClose()
    {
        if (isAnimating) return;
        StartCoroutine(OpenAndCloseRoutine());
    }

    /// <summary>
    /// ドアの開閉アニメーションを実行するコルーチン。
    /// 1. ドアを開く（openDuration秒かけて移動）
    /// 2. 開いた状態を維持（stayOpenTime秒）
    /// 3. ドアを閉じる（openDuration秒かけて初期位置に戻る）
    /// </summary>
    /// <returns>コルーチンのイテレータ</returns>
    private IEnumerator OpenAndCloseRoutine()
    {
        isAnimating = true;

        // ドアを開く処理
        float timer = 0f;
        Vector3 leftDoorTargetPos = leftDoorInitialPos + Vector3.left * openDistance;
        Vector3 rightDoorTargetPos = rightDoorInitialPos + Vector3.right * openDistance;

        while (timer < openDuration)
        {
            float progress = timer / openDuration;
            if (leftDoor != null) leftDoor.transform.position = Vector3.Lerp(leftDoorInitialPos, leftDoorTargetPos, progress);
            if (rightDoor != null) rightDoor.transform.position = Vector3.Lerp(rightDoorInitialPos, rightDoorTargetPos, progress);
            timer += Time.deltaTime;
            yield return null;
        }

        // 最終位置に確実に配置
        if (leftDoor != null) leftDoor.transform.position = leftDoorTargetPos;
        if (rightDoor != null) rightDoor.transform.position = rightDoorTargetPos;

        // ドアが開いた状態を維持
        yield return new WaitForSeconds(stayOpenTime);

        // ドアを閉じる処理
        timer = 0f;
        while (timer < openDuration)
        {
            float progress = timer / openDuration;
            if (leftDoor != null) leftDoor.transform.position = Vector3.Lerp(leftDoorTargetPos, leftDoorInitialPos, progress);
            if (rightDoor != null) rightDoor.transform.position = Vector3.Lerp(rightDoorTargetPos, rightDoorInitialPos, progress);
            timer += Time.deltaTime;
            yield return null;
        }

        // 初期位置に確実に戻す
        if (leftDoor != null) leftDoor.transform.position = leftDoorInitialPos;
        if (rightDoor != null) rightDoor.transform.position = rightDoorInitialPos;

        isAnimating = false;
    }
}