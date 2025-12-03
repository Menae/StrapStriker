using UnityEngine;
using UnityEngine.UI; // Imageコンポーネントを使うために必要
using System.Collections.Generic;

public class CarriageIndicator : MonoBehaviour
{
    // --- Inspectorで設定する項目 ---

    [Header("監視対象")]
    [Tooltip("位置を監視するプレイヤーのTransform")]
    public Transform playerTransform;

    [Header("車両の設定")]
    [Tooltip("各車両の範囲と画像を定義するリスト")]
    public List<CarriageBoundary> carriageBoundaries;

    // --- 各車両の範囲と画像を定義するためのデータ構造 ---
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

    // --- 内部変数 ---
    private int currentCarriageIndex = -1; // 現在いる車両のインデックス

    void Start()
    {
        // ゲーム開始時に一旦すべての画像を非表示にしておく（初期状態の不整合を防ぐため）
        foreach (var boundary in carriageBoundaries)
        {
            if (boundary.targetImage != null)
            {
                boundary.targetImage.gameObject.SetActive(false);
            }
        }
    }

    void Update()
    {
        // プレイヤーが設定されていなければ処理を行わない
        if (playerTransform == null)
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
                // もし、前回チェックした時と違う車両にいたら更新処理を行う
                if (i != currentCarriageIndex)
                {
                    UpdateCarriageImage(i);
                    currentCarriageIndex = i;
                }
                // 該当の車両を見つけたので、ループを抜ける
                return;
            }
        }

        // (オプション) どの車両範囲にもいない場合、画像をすべて消したい場合はここに処理を追加
    }

    // 画像の表示・非表示を更新するメソッド
    private void UpdateCarriageImage(int newIndex)
    {
        for (int i = 0; i < carriageBoundaries.Count; i++)
        {
            if (carriageBoundaries[i].targetImage != null)
            {
                // 現在のインデックス(i)が、新しく入った車両のインデックス(newIndex)と一致すれば表示(true)、それ以外は非表示(false)
                bool isActive = (i == newIndex);
                carriageBoundaries[i].targetImage.gameObject.SetActive(isActive);
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