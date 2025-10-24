// DoorManager.cs
using System.Collections.Generic;
using UnityEngine;

public class DoorManager : MonoBehaviour
{
    // シーン内の全てのドアを管理する静的リスト
    private static List<TrainDoor> allDoors = new List<TrainDoor>();

    public static void Register(TrainDoor door)
    {
        if (!allDoors.Contains(door))
        {
            allDoors.Add(door);
        }
    }

    public static void Unregister(TrainDoor door)
    {
        if (allDoors.Contains(door))
        {
            allDoors.Remove(door);
        }
    }

    // StageManagerから呼び出す、全ドアを開閉させるための命令
    public static void OpenAllDoors()
    {
        foreach (var door in allDoors)
        {
            door.OpenAndClose();
        }
    }
}