using System.Collections.Generic;
using UnityEngine;

public class NPCManager : MonoBehaviour
{
    // シングルトンインスタンス
    public static NPCManager instance;

    [Header("設定")]
    [Tooltip("物理演算などを有効化するプレイヤーからの半径")]
    public float activationRadius = 50f;
    [Tooltip("NPCの状態をチェックする間隔(秒)")]
    public float checkInterval = 0.5f;

    [Header("スポーン設定")]
    [Tooltip("最初にスポーンさせるNPCの数")]
    public int initialNpcCount = 100;
    [Tooltip("スポーン時にプレイヤーから最低限確保する距離")]
    public float playerSafeRadius = 5f;

    [Header("スポーン範囲設定")]
    [Tooltip("NPCがスポーンするエリアの中心座標")]
    public Vector2 spawnAreaCenter;
    [Tooltip("NPCがスポーンするエリアのサイズ(横幅と縦幅)")]
    public Vector2 spawnAreaSize;

    // 現在シーンに存在している（プールから取り出されている）全NPCのリスト
    private List<NPCController> spawnedNpcs = new List<NPCController>();
    private Transform playerTransform;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        // プレイヤーのTransformをキャッシュしておく
        playerTransform = FindObjectOfType<PlayerController>().transform;

        // 指定した間隔でNPCの状態を更新する処理を予約
        InvokeRepeating(nameof(UpdateNpcStates), 0f, checkInterval);

        // Inspectorで設定した数の初期NPCをスポーンさせる
        SpawnInitialNPCs(initialNpcCount);
    }

    // 指定された数のNPCをランダムな位置にスポーンさせるメソッド
    public void SpawnInitialNPCs(int count)
    {
        for (int i = 0; i < count; i++)
        {
            GameObject npcObject = NPCPool.instance.GetNPC();

            // 指定したスポーン範囲内のランダムな位置を計算
            float minX = spawnAreaCenter.x - spawnAreaSize.x / 2;
            float maxX = spawnAreaCenter.x + spawnAreaSize.x / 2;
            float minY = spawnAreaCenter.y - spawnAreaSize.y / 2;
            float maxY = spawnAreaCenter.y + spawnAreaSize.y / 2;

            Vector2 randomPosition;
            // 生成した座標がプレイヤーのセーフゾーン内だったら、座標を再抽選する
            do
            {
                randomPosition = new Vector2(Random.Range(minX, maxX), Random.Range(minY, maxY));
            } while (Vector2.Distance(randomPosition, playerTransform.position) < playerSafeRadius);

            npcObject.transform.position = randomPosition;

            // 管理リストに追加
            spawnedNpcs.Add(npcObject.GetComponent<NPCController>());
        }
    }

    // 全NPCの状態を更新する（Physics LOD）
    void UpdateNpcStates()
    {
        if (playerTransform == null) return;

        foreach (NPCController npc in spawnedNpcs)
        {
            if (!npc.gameObject.activeInHierarchy) continue;

            float distance = Vector2.Distance(npc.transform.position, playerTransform.position);

            // プレイヤーとの距離に応じて、物理演算などを有効化/無効化
            if (distance < activationRadius)
            {
                npc.Activate(); // 範囲内ならアクティブに
            }
            else
            {
                npc.Deactivate(); // 範囲外ならスリープさせる
            }
        }
    }

    // UnityエディタのSceneビューに、スポーン範囲を視覚的に表示する
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0, 1, 0, 0.5f); // 半透明の緑色
        Gizmos.DrawCube(spawnAreaCenter, spawnAreaSize);
    }
}