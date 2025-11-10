using System.Collections.Generic;
using UnityEngine;

public class NPCPool : MonoBehaviour
{
    public static NPCPool instance;

    [Header("プール設定")]
    [Tooltip("プールするNPCのPrefab")]
    public GameObject npcPrefab;
    [Tooltip("最初に生成しておくNPCの数")]
    public int poolSize = 100;

    // NPCを格納しておくキュー（先入れ先出しのリスト）
    private Queue<GameObject> npcPool = new Queue<GameObject>();

    private void Awake()
    {
        // シングルトンパターンの実装
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
        // 指定された数だけNPCを事前生成し、非アクティブ状態でプールに追加
        for (int i = 0; i < poolSize; i++)
        {
            GameObject npc = Instantiate(npcPrefab);
            npc.SetActive(false); // 非アクティブにする
            npcPool.Enqueue(npc); // プールに追加
        }
    }

    // プールからNPCを1体取り出すメソッド
    public GameObject GetNPC()
    {
        // もしプールが空なら、念のため新しく生成する
        if (npcPool.Count == 0)
        {
            GameObject newNpc = Instantiate(npcPrefab);
            newNpc.SetActive(false);
            npcPool.Enqueue(newNpc);
        }

        // プールからNPCを取り出す
        GameObject availableNpc = npcPool.Dequeue();
        availableNpc.SetActive(true); // アクティブにして返す
        return availableNpc;
    }

    // 使い終わったNPCをプールに戻すメソッド
    public void ReturnNPC(GameObject npc)
    {
        npc.SetActive(false); // 非アクティブにする
        npcPool.Enqueue(npc); // プールに戻す
    }
}