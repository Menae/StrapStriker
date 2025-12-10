using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// NPCのオブジェクトプールを管理するシングルトンクラス。
/// 事前にNPCを生成しておくことで、実行時の負荷を軽減する。
/// </summary>
public class NPCPool : MonoBehaviour
{
    /// <summary>
    /// シングルトンインスタンス。
    /// </summary>
    public static NPCPool instance;

    [Header("プール設定")]
    [Tooltip("プールするNPCのPrefab")]
    /// <summary>
    /// プールするNPCのPrefab。Inspectorで設定。
    /// </summary>
    public GameObject npcPrefab;

    [Tooltip("最初に生成しておくNPCの数")]
    /// <summary>
    /// 初期化時にプールへ生成するNPCの数。
    /// </summary>
    public int poolSize = 100;

    /// <summary>
    /// NPCを格納しておくキュー（先入れ先出し）。
    /// </summary>
    private Queue<GameObject> npcPool = new Queue<GameObject>();

    /// <summary>
    /// Awake時にシングルトンを初期化。
    /// 既にインスタンスが存在する場合は自身を破棄。
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
    /// Start時に指定数のNPCを事前生成し、非アクティブ状態でプールへ追加。
    /// </summary>
    void Start()
    {
        for (int i = 0; i < poolSize; i++)
        {
            GameObject npc = Instantiate(npcPrefab);
            npc.SetActive(false);
            npcPool.Enqueue(npc);
        }
    }

    /// <summary>
    /// プールからNPCを1体取り出してアクティブ化する。
    /// プールが空の場合は新規生成を行う。
    /// </summary>
    /// <returns>アクティブ化されたNPCのGameObject。</returns>
    public GameObject GetNPC()
    {
        if (npcPool.Count == 0)
        {
            GameObject newNpc = Instantiate(npcPrefab);
            newNpc.SetActive(false);
            npcPool.Enqueue(newNpc);
        }

        GameObject availableNpc = npcPool.Dequeue();
        availableNpc.SetActive(true);
        return availableNpc;
    }

    /// <summary>
    /// 使用済みのNPCを非アクティブ化してプールへ返却する。
    /// </summary>
    /// <param name="npc">返却するNPCのGameObject。</param>
    public void ReturnNPC(GameObject npc)
    {
        npc.SetActive(false);
        npcPool.Enqueue(npc);
    }
}