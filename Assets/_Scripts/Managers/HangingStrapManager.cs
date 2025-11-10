using System.Collections.Generic;
using UnityEngine;

public class HangingStrapManager : MonoBehaviour
{
    private static List<HangingStrap> allStraps = new List<HangingStrap>();

    // つり革が自分をリストに登録するためのメソッド
    public static void Register(HangingStrap strap)
    {
        if (!allStraps.Contains(strap))
        {
            allStraps.Add(strap);
        }
    }

    // つり革が自分をリストから削除するためのメソッド
    public static void Unregister(HangingStrap strap)
    {
        if (allStraps.Contains(strap))
        {
            allStraps.Remove(strap);
        }
    }

    // 指定された位置から最も近いつり革を探して返すメソッド
    public static HangingStrap FindNearestStrap(Vector3 position, float maxDistance)
    {
        HangingStrap nearestStrap = null;
        float minDistanceSqr = maxDistance * maxDistance; // 距離の2乗で比較（高速化）

        foreach (var strap in allStraps)
        {
            // プレイヤーとつり革の距離の2乗を計算
            float distanceSqr = (strap.transform.position - position).sqrMagnitude;

            // これまで見つかった最短距離より近く、かつ最大範囲内なら
            if (distanceSqr < minDistanceSqr)
            {
                minDistanceSqr = distanceSqr;
                nearestStrap = strap;
            }
        }

        return nearestStrap;
    }
}