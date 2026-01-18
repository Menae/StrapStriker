using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// NPC全体のスポーン、状態管理、Physics LODを統括するマネージャー。
/// ドア周辺への集中スポーンとプレイヤー距離に基づくアクティベーションを制御する。
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

    private List<NPCController> spawnedNpcs = new List<NPCController>();
    private Transform playerTransform;

    /// <summary>
    /// シングルトンの初期化。
    /// 既に別のインスタンスが存在する場合は自身を破棄する。
    /// </summary>
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

    /// <summary>
    /// プレイヤーTransformの取得とNPC状態更新の定期実行を開始。
    /// InvokeRepeatingでcheckInterval秒ごとにUpdateNpcStatesを呼び出す。
    /// </summary>
    void Start()
    {
        playerTransform = FindObjectOfType<PlayerController>().transform;
        InvokeRepeating(nameof(UpdateNpcStates), 0f, checkInterval);
    }

    /// <summary>
    /// StageManagerの指示でNPCをスポーンさせる。
    /// ドアが設定されている場合はドア周辺に集中・拡散スポーンを実行し、
    /// 未設定の場合はエリア内ランダムスポーンにフォールバックする。
    /// </summary>
    /// <param name="manager">呼び出し元のStageManager</param>
    /// <param name="count">スポーンさせる数</param>
    /// <returns>実際にスポーンした数</returns>
    public int SpawnNPCs(StageManager manager, int count)
    {
        if (doorSpawnPoints == null || doorSpawnPoints.Count == 0)
        {
            Debug.LogWarning("ドアが設定されていません。通常のランダムスポーンを実行します。");
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

            SpawnSingleNPC(manager, spawnPos);
            spawnedCount++;
        }

        // フェーズ2: ドア周辺に拡散スポーン
        int spreadCount = count - burstCount;
        for (int i = 0; i < spreadCount; i++)
        {
            Transform randomDoor = doorSpawnPoints[Random.Range(0, doorSpawnPoints.Count)];
            float randomXOffset = Random.Range(-spreadRadius, spreadRadius);
            Vector2 spawnPos = new Vector2(randomDoor.position.x + randomXOffset, randomDoor.position.y);

            SpawnSingleNPC(manager, spawnPos);
            spawnedCount++;
        }

        return spawnedCount;
    }

    /// <summary>
    /// 1体のNPCをプールから取得し、指定座標にスポーンさせる。
    /// 現状はとりあえず Normal タイプを生成する設定。
    /// </summary>
    private void SpawnSingleNPC(StageManager manager, Vector2 position)
    {
        // 変更: GetNPCに引数(NPCType.Normal)を追加
        GameObject npcObject = NPCPool.instance.GetNPC(NPCType.Normal);
        npcObject.transform.position = position;

        NPCController controller = npcObject.GetComponent<NPCController>();
        if (controller != null)
        {
            controller.SetStageManager(manager);
            spawnedNpcs.Add(controller);
        }
    }

    /// <summary>
    /// 種類と場所を指定してNPCをスポーンさせる新メソッド。
    /// StationEventのSpawnWaveから呼び出される。
    /// </summary>
    public int SpawnSpecificNPCs(StageManager manager, NPCType type, int count, bool atDoor)
    {
        int spawnedCount = 0;

        if (atDoor && doorSpawnPoints != null && doorSpawnPoints.Count > 0)
        {
            // ドア周辺へのスポーン
            for (int i = 0; i < count; i++)
            {
                Transform randomDoor = doorSpawnPoints[Random.Range(0, doorSpawnPoints.Count)];
                // 少しランダムに散らす
                float randomXOffset = Random.Range(-burstRadius, burstRadius);
                Vector2 spawnPos = new Vector2(randomDoor.position.x + randomXOffset, randomDoor.position.y);

                SpawnSingleNPC(manager, spawnPos, type);
                spawnedCount++;
            }
        }
        else
        {
            // エリア内ランダムスポーン
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
        // NPCPoolから指定タイプのオブジェクトを取得
        GameObject npcObject = NPCPool.instance.GetNPC(type);

        if (npcObject == null) return; // 登録忘れ等の対策

        npcObject.transform.position = position;

        NPCController controller = npcObject.GetComponent<NPCController>();
        if (controller != null)
        {
            controller.SetStageManager(manager);
            spawnedNpcs.Add(controller);
        }
    }

    /// <summary>
    /// ドアが未設定の場合のフォールバック処理。
    /// spawnAreaCenter/Sizeで定義されたエリア内にX軸ランダム、Y軸固定でNPCをスポーンする。
    /// </summary>
    /// <param name="manager">StageManagerインスタンス</param>
    /// <param name="count">スポーン数</param>
    /// <returns>実際にスポーンした数</returns>
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
    /// 混雑率から必要なNPC数を逆算してスポーンする。
    /// rateDecreasePerNpcが0の場合はゼロ除算を防ぐため何もしない。
    /// </summary>
    /// <param name="manager">StageManagerインスタンス</param>
    /// <param name="congestionRate">現在の混雑率</param>
    public void SpawnNPCsForCongestion(StageManager manager, float congestionRate)
    {
        if (manager.rateDecreasePerNpc <= 0) return;

        int count = Mathf.FloorToInt(congestionRate / manager.rateDecreasePerNpc);
        // 混雑初期配置はとりあえずNormalタイプをランダム配置
        SpawnSpecificNPCs(manager, NPCType.Normal, count, false);
    }

    /// <summary>
    /// InvokeRepeatingで定期実行されるPhysics LOD処理。
    /// プレイヤーとの距離を計測し、activationRadius内のNPCをアクティブ化、範囲外を非アクティブ化する。
    /// </summary>
    void UpdateNpcStates()
    {
        if (playerTransform == null) return;

        foreach (NPCController npc in spawnedNpcs)
        {
            if (npc == null || !npc.gameObject.activeInHierarchy) continue;

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

    /// <summary>
    /// Sceneビューにスポーン範囲を半透明の緑色キューブで可視化する。
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0, 1, 0, 0.5f);
        Gizmos.DrawCube(spawnAreaCenter, spawnAreaSize);
    }
}