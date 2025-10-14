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
    }

    /// <summary>
    /// StageManagerの指示でNPCをスポーンさせる
    /// </summary>
    /// <param name="manager">呼び出し元のStageManager</param>
    /// <param name="count">スポーンさせる数</param>
    /// <returns>実際にスポーンした数</returns>
    public int SpawnNPCs(StageManager manager, int count)
    {
        int spawnedCount = 0;
        for (int i = 0; i < count; i++)
        {
            GameObject npcObject = NPCPool.instance.GetNPC();

            // X座標はランダム、Y座標はspawnAreaCenter.yに固定
            float minX = spawnAreaCenter.x - spawnAreaSize.x / 2;
            float maxX = spawnAreaCenter.x + spawnAreaSize.x / 2;
            float fixedY = spawnAreaCenter.y;

            Vector2 randomPosition;
            int maxTries = 100; // 無限ループを避けるための試行回数の上限
            int tries = 0;

            // 生成した座標がプレイヤーのセーフゾーン内だったら、座標を再抽選する
            do
            {
                randomPosition = new Vector2(Random.Range(minX, maxX), fixedY);
                tries++;
                if (tries > maxTries)
                {
                    Debug.LogWarning("Could not find a valid spawn position outside the player's safe radius. Spawning NPC anyway.");
                    break;
                }
            } while (Vector2.Distance(randomPosition, playerTransform.position) < playerSafeRadius);

            npcObject.transform.position = randomPosition;

            NPCController controller = npcObject.GetComponent<NPCController>();
            if (controller != null)
            {
                controller.SetStageManager(manager); // NPCに司令塔を教える
                spawnedNpcs.Add(controller);
                spawnedCount++;
            }
        }
        return spawnedCount;
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