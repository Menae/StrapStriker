using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// プレイヤーのX座標に基づいて、現在いる車両に対応するUI画像を表示する。
/// 車両の境界はInspectorで設定した範囲リストで判定し、範囲外の車両画像は非表示にする。
/// </summary>
public class CarriageIndicator : MonoBehaviour
{
    [Header("監視対象")]
    [Tooltip("位置を監視するプレイヤーのTransform")]
    public Transform playerTransform;

    [Header("車両の設定")]
    [Tooltip("各車両の範囲と画像を定義するリスト")]
    public List<CarriageBoundary> carriageBoundaries;

    /// <summary>
    /// 各車両のX座標範囲と、その車両に対応する表示画像を保持する。
    /// </summary>
    [System.Serializable]
    public class CarriageBoundary
    {
        [Tooltip("識別用の名前（例：1両目）")]
        public string displayName;

        [Tooltip("この車両が始まるX座標")]
        public float startX;

        [Tooltip("この車両が終わるX座標")]
        public float endX;

        [Tooltip("この車両にいる時に有効化するImage")]
        public Image targetImage;
    }

    private int currentCarriageIndex = -1;

    /// <summary>
    /// ゲーム開始時に全車両の画像を非表示にして、初期状態の不整合を防ぐ。
    /// </summary>
    void Start()
    {
        foreach (var boundary in carriageBoundaries)
        {
            if (boundary.targetImage != null)
            {
                boundary.targetImage.gameObject.SetActive(false);
            }
        }
    }

    /// <summary>
    /// 毎フレーム、プレイヤーのX座標を監視し、車両範囲に入った場合は対応する画像を表示する。
    /// 前回と同じ車両にいる場合は処理をスキップして負荷を軽減。
    /// </summary>
    void Update()
    {
        if (playerTransform == null)
        {
            return;
        }

        float playerX = playerTransform.position.x;

        for (int i = 0; i < carriageBoundaries.Count; i++)
        {
            if (playerX >= carriageBoundaries[i].startX && playerX < carriageBoundaries[i].endX)
            {
                if (i != currentCarriageIndex)
                {
                    UpdateCarriageImage(i);
                    currentCarriageIndex = i;
                }
                return;
            }
        }
    }

    /// <summary>
    /// 指定されたインデックスの車両画像のみを表示し、それ以外を非表示にする。
    /// </summary>
    /// <param name="newIndex">表示する車両のインデックス</param>
    private void UpdateCarriageImage(int newIndex)
    {
        for (int i = 0; i < carriageBoundaries.Count; i++)
        {
            if (carriageBoundaries[i].targetImage != null)
            {
                bool isActive = (i == newIndex);
                carriageBoundaries[i].targetImage.gameObject.SetActive(isActive);
            }
        }
    }

    /// <summary>
    /// Sceneビューで車両の境界線をギズモ表示する（デバッグ用）。
    /// 開始位置はシアン、終了位置は黄色で描画。
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (carriageBoundaries == null || carriageBoundaries.Count == 0) return;

        float camHeight = Camera.main.orthographicSize * 2;

        foreach (var boundary in carriageBoundaries)
        {
            Gizmos.color = Color.cyan;
            Vector3 startLineTop = new Vector3(boundary.startX, camHeight / 2, 0);
            Vector3 startLineBottom = new Vector3(boundary.startX, -camHeight / 2, 0);
            Gizmos.DrawLine(startLineTop, startLineBottom);

            Gizmos.color = Color.yellow;
            Vector3 endLineTop = new Vector3(boundary.endX, camHeight / 2, 0);
            Vector3 endLineBottom = new Vector3(boundary.endX, -camHeight / 2, 0);
            Gizmos.DrawLine(endLineTop, endLineBottom);
        }
    }
}