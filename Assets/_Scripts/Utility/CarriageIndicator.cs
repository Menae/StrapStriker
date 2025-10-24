using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class CarriageIndicator : MonoBehaviour
{
    // --- Inspectorで設定する項目 ---
    [Header("UI設定")]
    [Tooltip("車両番号を表示するTextMeshProコンポーネント")]
    public TextMeshProUGUI carriageText;

    [Header("監視対象")]
    [Tooltip("位置を監視するプレイヤーのTransform")]
    public Transform playerTransform;

    [Header("車両の境界設定")]
    [Tooltip("各車両の範囲を定義するリスト")]
    public List<CarriageBoundary> carriageBoundaries;

    // --- 各車両の範囲を定義するためのデータ構造 ---
    [System.Serializable]
    public class CarriageBoundary
    {
        [Tooltip("UIに表示する名前（例：1両目）")]
        public string displayName;
        [Tooltip("この車両が始まるX座標")]
        public float startX;
        [Tooltip("この車両が終わるX座標")]
        public float endX;
    }

    // --- 内部変数 ---
    private int currentCarriageIndex = -1; // 現在いる車両のインデックス

    void Update()
    {
        // プレイヤーやテキストが設定されていなければ、処理を行わない
        if (playerTransform == null || carriageText == null)
        {
            return;
        }

        // プレイヤーの現在のX座標を取得
        float playerX = playerTransform.position.x;

        // どの車両にいるか判定
        for (int i = 0; i < carriageBoundaries.Count; i++)
        {
            // プレイヤーが i番目の車両の範囲内にいるかチェック
            if (playerX >= carriageBoundaries[i].startX && playerX < carriageBoundaries[i].endX)
            {
                // もし、前回チェックした時と違う車両にいたら
                if (i != currentCarriageIndex)
                {
                    // UIテキストを更新し、現在の車両インデックスを記録
                    carriageText.text = carriageBoundaries[i].displayName;
                    currentCarriageIndex = i;
                }
                // 該当の車両を見つけたので、ループを抜ける
                return;
            }
        }
    }

    // Sceneビューに、設定した車両の境界線を視覚的に表示する（デバッグ用）
    private void OnDrawGizmosSelected()
    {
        if (carriageBoundaries == null || carriageBoundaries.Count == 0) return;

        // カメラのビューの高さを取得
        float camHeight = Camera.main.orthographicSize * 2;

        foreach (var boundary in carriageBoundaries)
        {
            // 各境界線の開始位置と終了位置に、縦線を引く
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