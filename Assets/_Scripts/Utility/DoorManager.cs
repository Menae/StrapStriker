using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// シーン内の全TrainDoorインスタンスを集中管理し、一括開閉を可能にする。
/// StageManagerなど外部システムから呼び出される想定。
/// </summary>
public class DoorManager : MonoBehaviour
{
    private static List<TrainDoor> allDoors = new List<TrainDoor>();

    /// <summary>
    /// TrainDoorインスタンスを管理リストに登録する。
    /// 重複登録は自動的に回避される。
    /// </summary>
    /// <param name="door">登録対象のTrainDoorインスタンス</param>
    public static void Register(TrainDoor door)
    {
        if (!allDoors.Contains(door))
        {
            allDoors.Add(door);
        }
    }

    /// <summary>
    /// TrainDoorインスタンスを管理リストから削除する。
    /// OnDisable時などに呼び出される想定。
    /// </summary>
    /// <param name="door">削除対象のTrainDoorインスタンス</param>
    public static void Unregister(TrainDoor door)
    {
        if (allDoors.Contains(door))
        {
            allDoors.Remove(door);
        }
    }

    /// <summary>
    /// 登録済みの全TrainDoorに対してOpenAndClose()を実行する。
    /// StageManagerから特定タイミング（ステージクリア時など）で呼び出される。
    /// </summary>
    public static void OpenAllDoors()
    {
        foreach (var door in allDoors)
        {
            door.OpenAndClose();
        }
    }
}