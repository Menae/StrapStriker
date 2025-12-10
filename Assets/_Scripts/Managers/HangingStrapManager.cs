using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// シーン内の全つり革を一元管理し、最寄りのつり革検索を提供する静的マネージャー
/// </summary>
public class HangingStrapManager : MonoBehaviour
{
    private static List<HangingStrap> allStraps = new List<HangingStrap>();

    /// <summary>
    /// つり革を管理リストに登録する
    /// 重複登録は自動的に回避される
    /// </summary>
    /// <param name="strap">登録対象のつり革</param>
    public static void Register(HangingStrap strap)
    {
        if (!allStraps.Contains(strap))
        {
            allStraps.Add(strap);
        }
    }

    /// <summary>
    /// つり革を管理リストから削除する
    /// 存在しない場合は何もしない
    /// </summary>
    /// <param name="strap">削除対象のつり革</param>
    public static void Unregister(HangingStrap strap)
    {
        if (allStraps.Contains(strap))
        {
            allStraps.Remove(strap);
        }
    }

    /// <summary>
    /// 指定位置から最も近いつり革を検索する
    /// 距離判定にはsqrMagnitudeを使用し、平方根計算を省略して高速化
    /// </summary>
    /// <param name="position">検索基準となる位置（通常はプレイヤーの座標）</param>
    /// <param name="maxDistance">検索範囲の最大距離</param>
    /// <returns>範囲内で最も近いつり革。見つからない場合はnull</returns>
    public static HangingStrap FindNearestStrap(Vector3 position, float maxDistance)
    {
        HangingStrap nearestStrap = null;
        float minDistanceSqr = maxDistance * maxDistance;

        foreach (var strap in allStraps)
        {
            float distanceSqr = (strap.transform.position - position).sqrMagnitude;

            if (distanceSqr < minDistanceSqr)
            {
                minDistanceSqr = distanceSqr;
                nearestStrap = strap;
            }
        }

        return nearestStrap;
    }
}