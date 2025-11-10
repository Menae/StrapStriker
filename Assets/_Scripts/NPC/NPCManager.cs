using System.Collections.Generic;
using UnityEngine;

public class NPCManager : MonoBehaviour
{
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

    [Header("ドア周辺スポーン設定")]
    [Tooltip("ドアの位置となるTransformのリスト")]
    public List<Transform> doorSpawnPoints;
    [Tooltip("ドアの真ん前に集中してスポーンする半径")]
    public float burstRadius = 1.0f;
    [Tooltip("ドアの周辺に広がってスポーンする半径")]
    public float spreadRadius = 5.0f;
    [Tooltip("全スポーン数のうち、ドアの真ん前に集中させる割合 (0.8 = 80%)")]
    [Range(0f, 1f)]
    public float burstPercentage = 0.8f;

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
    }

    /// <summary>
    /// StageManagerの指示でNPCをスポーンさせる
    /// </summary>
    /// <param name="manager">呼び出し元のStageManager</param>
    /// <param name="count">スポーンさせる数</param>
    /// <returns>実際にスポーンした数</returns>
    public int SpawnNPCs(StageManager manager, int count)
    {
        // ドアが1つも設定されていない場合は、以前のロジックでスポーンする
        if (doorSpawnPoints == null || doorSpawnPoints.Count == 0)
        {
            Debug.LogWarning("ドアが設定されていません。通常のランダムスポーンを実行します。");
            return SpawnNPCsInArea(manager, count); // 古いロジックを呼び出す
        }

        int spawnedCount = 0;
        // 集中スポーンさせるNPCの数を計算
        int burstCount = Mathf.FloorToInt(count * burstPercentage);

        // --- フェーズ1: 集中スポーン (Burst) ---
        for (int i = 0; i < burstCount; i++)
        {
            Transform randomDoor = doorSpawnPoints[Random.Range(0, doorSpawnPoints.Count)];

            // X座標だけをランダムにし、Y座標はドアの位置に固定する
            float randomXOffset = Random.Range(-burstRadius, burstRadius);
            Vector2 spawnPos = new Vector2(randomDoor.position.x + randomXOffset, randomDoor.position.y);

            SpawnSingleNPC(manager, spawnPos);
            spawnedCount++;
        }

        // --- フェーズ2: 拡散スポーン (Spread) ---
        int spreadCount = count - burstCount;
        for (int i = 0; i < spreadCount; i++)
        {
            Transform randomDoor = doorSpawnPoints[Random.Range(0, doorSpawnPoints.Count)];

            // X座標だけをランダムにし、Y座標はドアの位置に固定する
            float randomXOffset = Random.Range(-spreadRadius, spreadRadius);
            Vector2 spawnPos = new Vector2(randomDoor.position.x + randomXOffset, randomDoor.position.y);

            SpawnSingleNPC(manager, spawnPos);
            spawnedCount++;
        }

        return spawnedCount;
    }

    // 1体のNPCを特定の位置にスポーンさせる処理
    private void SpawnSingleNPC(StageManager manager, Vector2 position)
    {
        GameObject npcObject = NPCPool.instance.GetNPC();
        npcObject.transform.position = position;

        NPCController controller = npcObject.GetComponent<NPCController>();
        if (controller != null)
        {
            controller.SetStageManager(manager);
            spawnedNpcs.Add(controller);
        }
    }

    // 古いエリア内ランダムスポーンのロジック
    private int SpawnNPCsInArea(StageManager manager, int count)
    {
        for (int i = 0; i < count; i++)
        {
            float minX = spawnAreaCenter.x - spawnAreaSize.x / 2;
            float maxX = spawnAreaCenter.x + spawnAreaSize.x / 2;
            float fixedY = spawnAreaCenter.y;
            Vector2 randomPosition = new Vector2(Random.Range(minX, maxX), fixedY);
            SpawnSingleNPC(manager, randomPosition);
        }
        return count;
    }

    /// <summary>
    /// 混雑率からNPCの数を計算してスポーンする
    /// </summary>
    /// <param name="manager">呼び出し元のStageManager</param>
    /// <param name="congestionRate">現在の混雑率</param>
    public void SpawnNPCsForCongestion(StageManager manager, float congestionRate)
    {
        // rateDecreasePerNpcが0だとエラーになるのを防ぐ
        if (manager.rateDecreasePerNpc <= 0) return;

        int count = Mathf.FloorToInt(congestionRate / manager.rateDecreasePerNpc);
        SpawnNPCs(manager, count);
    }

    // 全NPCの状態を更新する（Physics LOD）
    void UpdateNpcStates()
    {
        if (playerTransform == null) return;

        foreach (NPCController npc in spawnedNpcs)
        {
            if (npc == null || !npc.gameObject.activeInHierarchy) continue;

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