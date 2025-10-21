using UnityEngine;
using System.IO.Ports; // シリアル通信に必要
using System.Threading; // スレッド処理に必要
using System; // 例外処理に必要

public class ArduinoInputManager : MonoBehaviour
{
    // --- シングルトンインスタンス ---
    public static ArduinoInputManager instance;

    [Header("シリアルポート設定")]
    [Tooltip("Arduinoが接続されているCOMポート名 (例: COM3)")]
    public string portName = "COM3";
    [Tooltip("Arduinoと合わせるボーレート (通信速度)")]
    public int baudRate = 9600;

    // --- 他のスクリプトから参照する握力センサーの値 ---
    // volatileキーワードは、複数のスレッドからアクセスされる変数のお守りのようなもの
    public static volatile int GripValue;

    // --- 内部変数 ---
    private SerialPort serialPort;
    private Thread readThread;
    private bool isThreadRunning = false;

    void Awake()
    {
        // シングルトンパターンの実装
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject); // シーンをまたいでも破棄されないようにする
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        // シリアルポートに接続し、データの読み取りを開始する
        ConnectToArduino();
    }

    private void ConnectToArduino()
    {
        // PCで利用可能なCOMポートの全リストを取得
        string[] availablePorts = SerialPort.GetPortNames();

        // 指定したポートがリストに存在するかどうかを確認
        bool portExists = false;
        foreach (string openPort in availablePorts)
        {
            if (openPort == portName)
            {
                portExists = true;
                break;
            }
        }

        // もし指定したポートが存在しなければ、警告を出してメソッドを抜ける
        if (!portExists)
        {
            Debug.LogWarning($"<color=orange>Port '{portName}' not found. Arduino input is disabled.</color>");
            return;
        }

        // ポートが存在する場合のみ、接続を試みる
        try
        {
            serialPort = new SerialPort(portName, baudRate);
            serialPort.ReadTimeout = 1000;
            serialPort.Open();

            isThreadRunning = true;
            readThread = new Thread(ReadSerialData);
            readThread.Start();

            Debug.Log($"<color=cyan>Arduino on {portName} connected successfully!</color>");
        }
        catch (Exception e)
        {
            Debug.LogError($"<color=red>Error connecting to Arduino: {e.Message}</color>");
        }
    }

    // 別スレッドで実行されるデータ読み取りメソッド
    private void ReadSerialData()
    {
        while (isThreadRunning && serialPort != null && serialPort.IsOpen)
        {
            try
            {
                // Arduinoから送られてきた1行分のデータを読み取る
                string data = serialPort.ReadLine();
                // 読み取った文字列を整数に変換しようと試みる
                if (int.TryParse(data, out int value))
                {
                    // 成功したら、静的変数に値を格納
                    GripValue = value;
                    Debug.Log($"Grip Value: {GripValue}");
                }
            }
            catch (TimeoutException)
            {
                // データが来ていない場合はタイムアウトするが、正常な動作なので何もしない
            }
            catch (Exception e)
            {
                // その他のエラーが発生した場合
                Debug.LogWarning($"Error reading from serial port: {e.Message}");
            }
        }
    }

    // ゲーム終了時に呼ばれる処理
    void OnDestroy()
    {
        // スレッドを安全に停止させる
        if (isThreadRunning)
        {
            isThreadRunning = false;
            // スレッドが完全に終了するのを待つ
            if (readThread != null && readThread.IsAlive)
            {
                readThread.Join();
            }
        }

        // シリアルポートを安全に閉じる
        if (serialPort != null && serialPort.IsOpen)
        {
            serialPort.Close();
            Debug.Log("<color=cyan>Serial port closed.</color>");
        }
    }
}