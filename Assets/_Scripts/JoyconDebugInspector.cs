using UnityEngine;
using System.Collections.Generic;

public class JoyconDebugInspector : MonoBehaviour
{
    [Header("Joy-Con Sensor Data")]
    [SerializeField] private Vector3 accelerometer;
    [SerializeField] private Vector3 gyroscope;
    [SerializeField] private Quaternion orientation;

    [Header("Status")]
    [SerializeField] private string statusMessage = "Initializing...";

    private List<Joycon> joycons;

    void Start()
    {
        // 接続されているJoyconのリストを取得
        joycons = JoyconManager.instance.j;
    }

    void Update()
    {
        // Joy-Conが1台も接続されていなければ、メッセージを更新して処理を終える
        if (joycons == null || joycons.Count == 0)
        {
            statusMessage = "Joy-Con not found...";
            return;
        }

        statusMessage = "Joy-Con connected!";

        // 最初のJoy-Conを取得
        Joycon joycon = joycons[0];

        // 各センサーの値を取得して、Inspector表示用の変数に代入
        accelerometer = joycon.GetAccel();
        gyroscope = joycon.GetGyro();
        orientation = joycon.GetVector();
    }
}