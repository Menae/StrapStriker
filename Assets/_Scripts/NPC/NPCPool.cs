using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// NPCの種類ごとにPrefabを設定するための構造体
/// </summary>
[System.Serializable]
public struct NPCPoolSetting
{
    public NPCType type;
    public GameObject prefab;
    public int initialPoolSize;
}

/// <summary>
/// NPCのオブジェクトプールを管理するシングルトンクラス。
/// 種類（NPCType）ごとにプールを分けて管理する。
/// </summary>
public class NPCPool : MonoBehaviour
{
    public static NPCPool instance;

    [Header("プール設定")]
    [Tooltip("種類ごとのPrefabと初期数を設定")]
    public List<NPCPoolSetting> poolSettings;

    // 種類ごとのキューを管理する辞書
    private Dictionary<NPCType, Queue<GameObject>> pools = new Dictionary<NPCType, Queue<GameObject>>();

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
        // 設定リストに基づき、各タイプのプールを初期化
        foreach (var setting in poolSettings)
        {
            if (!pools.ContainsKey(setting.type))
            {
                pools[setting.type] = new Queue<GameObject>();
            }

            for (int i = 0; i < setting.initialPoolSize; i++)
            {
                CreateAndEnqueue(setting.prefab, setting.type);
            }
        }
    }

    /// <summary>
    /// 新規生成してキューに追加するヘルパーメソッド
    /// </summary>
    private void CreateAndEnqueue(GameObject prefab, NPCType type)
    {
        GameObject obj = Instantiate(prefab);
        obj.SetActive(false);
        // 生成したオブジェクトがどのタイプか分かるようにComponent等で識別しても良いが、
        // ここではプール管理のみに徹する
        pools[type].Enqueue(obj);
    }

    /// <summary>
    /// 指定したタイプのNPCをプールから取り出す。
    /// 足りない場合は自動生成して補充する。
    /// </summary>
    public GameObject GetNPC(NPCType type)
    {
        // 未登録のタイプならエラー
        if (!pools.ContainsKey(type))
        {
            Debug.LogError($"NPCPool: Type {type} is not registered in PoolSettings!");
            return null;
        }

        Queue<GameObject> targetPool = pools[type];

        // プールが空なら補充（設定リストからPrefabを検索）
        if (targetPool.Count == 0)
        {
            NPCPoolSetting setting = poolSettings.Find(s => s.type == type);
            if (setting.prefab != null)
            {
                CreateAndEnqueue(setting.prefab, type);
            }
        }

        GameObject npc = targetPool.Dequeue();
        npc.SetActive(true);
        return npc;
    }

    /// <summary>
    /// 使用済みのNPCをプールへ返却する。
    /// </summary>
    /// <param name="npc">返却するオブジェクト</param>
    /// <param name="type">そのNPCの種類</param>
    public void ReturnNPC(GameObject npc, NPCType type)
    {
        npc.SetActive(false);
        if (pools.ContainsKey(type))
        {
            pools[type].Enqueue(npc);
        }
        else
        {
            // 万が一キーがない場合は破棄
            Destroy(npc);
        }
    }
}