using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// NPC全体のスポーン、状態管理、Physics LODを統括するマネージャー。
/// </summary>
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

    // 混雑率設定(rateDecreasePerNpc)はStageManager側で一元管理するため削除

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
        // プレイヤーを探す（PlayerControllerがついているオブジェクト）
        var playerCtrl = FindObjectOfType<PlayerController>();
        if (playerCtrl != null)
        {
            playerTransform = playerCtrl.transform;
        }

        InvokeRepeating(nameof(UpdateNpcStates), 0f, checkInterval);
    }

    public int SpawnNPCs(StageManager manager, int count)
    {
        if (doorSpawnPoints == null || doorSpawnPoints.Count == 0)
        {
            return SpawnNPCsInArea(manager, count);
        }

        int spawnedCount = 0;
        int burstCount = Mathf.FloorToInt(count * burstPercentage);

        // フェーズ1: ドア真ん前に集中スポーン
        for (int i = 0; i < burstCount; i++)
        {
            Transform randomDoor = doorSpawnPoints[Random.Range(0, doorSpawnPoints.Count)];
            float randomXOffset = Random.Range(-burstRadius, burstRadius);
            Vector2 spawnPos = new Vector2(randomDoor.position.x + randomXOffset, randomDoor.position.y);

            // NormalA をデフォルトとして使用
            SpawnSingleNPC(manager, spawnPos, NPCType.NormalA);
            spawnedCount++;
        }

        // フェーズ2: ドア周辺に拡散スポーン
        int spreadCount = count - burstCount;
        for (int i = 0; i < spreadCount; i++)
        {
            Transform randomDoor = doorSpawnPoints[Random.Range(0, doorSpawnPoints.Count)];
            float randomXOffset = Random.Range(-spreadRadius, spreadRadius);
            Vector2 spawnPos = new Vector2(randomDoor.position.x + randomXOffset, randomDoor.position.y);

            // NormalA をデフォルトとして使用
            SpawnSingleNPC(manager, spawnPos, NPCType.NormalA);
            spawnedCount++;
        }

        return spawnedCount;
    }

    /// <summary>
    /// 種類と場所を指定してNPCをスポーンさせるメソッド。
    /// StationEventのSpawnWaveから呼び出される。
    /// </summary>
    public int SpawnSpecificNPCs(StageManager manager, NPCType type, int count, bool atDoor)
    {
        int spawnedCount = 0;

        if (atDoor && doorSpawnPoints != null && doorSpawnPoints.Count > 0)
        {
            for (int i = 0; i < count; i++)
            {
                Transform randomDoor = doorSpawnPoints[Random.Range(0, doorSpawnPoints.Count)];
                float randomXOffset = Random.Range(-burstRadius, burstRadius);
                Vector2 spawnPos = new Vector2(randomDoor.position.x + randomXOffset, randomDoor.position.y);

                SpawnSingleNPC(manager, spawnPos, type);
                spawnedCount++;
            }
        }
        else
        {
            for (int i = 0; i < count; i++)
            {
                float minX = spawnAreaCenter.x - spawnAreaSize.x / 2;
                float maxX = spawnAreaCenter.x + spawnAreaSize.x / 2;
                float fixedY = spawnAreaCenter.y;
                Vector2 randomPosition = new Vector2(Random.Range(minX, maxX), fixedY);

                SpawnSingleNPC(manager, randomPosition, type);
                spawnedCount++;
            }
        }

        return spawnedCount;
    }

    /// <summary>
    /// 内部ヘルパー: タイプを指定して1体生成
    /// </summary>
    private void SpawnSingleNPC(StageManager manager, Vector2 position, NPCType type)
    {
        GameObject npcObject = NPCPool.instance.GetNPC(type);

        if (npcObject == null) return;

        npcObject.transform.position = position;

        NPCController controller = npcObject.GetComponent<NPCController>();
        if (controller != null)
        {
            controller.SetStageManager(manager);
            spawnedNpcs.Add(controller);
            // 生成時にアクティブ化
            controller.Activate();
        }
    }

    /// <summary>
    /// ドア未設定時のフォールバック処理
    /// </summary>
    private int SpawnNPCsInArea(StageManager manager, int count)
    {
        for (int i = 0; i < count; i++)
        {
            float minX = spawnAreaCenter.x - spawnAreaSize.x / 2;
            float maxX = spawnAreaCenter.x + spawnAreaSize.x / 2;
            float fixedY = spawnAreaCenter.y;
            Vector2 randomPosition = new Vector2(Random.Range(minX, maxX), fixedY);

            SpawnSingleNPC(manager, randomPosition, NPCType.NormalA);
        }
        return count;
    }

    /// <summary>
    /// 指定された混雑率を満たすのに必要なNPC数を逆算してスポーンする。
    /// 生成数に基づく正確なレートを返し、マネージャー側の数値を補正することで誤差を防ぐ。
    /// </summary>
    /// <returns>実際に生成された数に基づいた「補正後の混雑率」</returns>
    public float SpawnNPCsForCongestion(StageManager manager, float congestionRate)
    {
        if (manager.rateDecreasePerNpc <= 0) return congestionRate;

        int count = Mathf.FloorToInt(congestionRate / manager.rateDecreasePerNpc);
        int actualSpawnedCount = 0;

        bool useDoor = (doorSpawnPoints != null && doorSpawnPoints.Count > 0);

        for (int i = 0; i < count; i++)
        {
            // NormalA と NormalB をランダムに選択 (50%)
            NPCType selectedType = (Random.value > 0.5f) ? NPCType.NormalA : NPCType.NormalB;

            if (useDoor)
            {
                Transform randomDoor = doorSpawnPoints[Random.Range(0, doorSpawnPoints.Count)];
                float randomXOffset = Random.Range(-burstRadius, burstRadius);
                Vector2 spawnPos = new Vector2(randomDoor.position.x + randomXOffset, randomDoor.position.y);
                SpawnSingleNPC(manager, spawnPos, selectedType);
            }
            else
            {
                float minX = spawnAreaCenter.x - spawnAreaSize.x / 2;
                float maxX = spawnAreaCenter.x + spawnAreaSize.x / 2;
                Vector2 randomPosition = new Vector2(Random.Range(minX, maxX), spawnAreaCenter.y);
                SpawnSingleNPC(manager, randomPosition, selectedType);
            }
            actualSpawnedCount++;
        }

        // 計算上の端数を切り捨てた、実体と整合性の取れた混雑率を返す
        return actualSpawnedCount * manager.rateDecreasePerNpc;
    }

    /// <summary>
    /// 定期的にNPCの状態（距離によるアクティブ切り替え）を更新する。
    /// 不要になった（プールに戻った）NPCの参照はリストから削除する。
    /// </summary>
    void UpdateNpcStates()
    {
        if (playerTransform == null) return;

        // リスト削除を伴うため、逆順ループで処理
        for (int i = spawnedNpcs.Count - 1; i >= 0; i--)
        {
            var npc = spawnedNpcs[i];

            // NPCが削除済み、または非アクティブ（プール返却済み）の場合は管理対象から外す
            if (npc == null || !npc.gameObject.activeSelf)
            {
                spawnedNpcs.RemoveAt(i);
                continue;
            }

            float distance = Vector2.Distance(npc.transform.position, playerTransform.position);

            if (distance < activationRadius)
            {
                npc.Activate();
            }
            else
            {
                npc.Deactivate();
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0, 1, 0, 0.5f);
        Gizmos.DrawCube(spawnAreaCenter, spawnAreaSize);
    }
}