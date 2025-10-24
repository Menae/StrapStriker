using UnityEngine;
using System.IO.Ports; // シリアル通信に必要
using System.Threading; // スレッド処理に必要
using System; // 例外処理に必要

public class ArduinoInputManager : MonoBehaviour
{
    // --- シングルトンインスタンス ---
    public static ArduinoInputManager instance;

    [Header("フォールバック設定")]
    [Tooltip("自動検出が失敗した場合に、接続を試みるCOMポート名")]
    public string fallbackPortName = "COM3";
    [Tooltip("Arduinoと合わせるボーレート (通信速度)")]
    public int baudRate = 9600;

    public bool IsConnected { get; private set; } = false;

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
        string[] availablePorts = SerialPort.GetPortNames();
        if (availablePorts.Length == 0)
        {
            Debug.LogWarning("No COM ports found.");
            return;
        }

        // ハンドシェイクによる自動検出を試みる
        Debug.Log($"Phase 1: Found {availablePorts.Length} ports. Searching for Arduino via handshake...");
        foreach (string port in availablePorts)
        {
            SerialPort tempPort = new SerialPort(port, baudRate);
            tempPort.ReadTimeout = 200;
            try
            {
                tempPort.Open();
                tempPort.Write("p");
                string response = tempPort.ReadLine().Trim();

                if (response == "STRAP_STRIKER_GRIP")
                {
                    Debug.Log($"<color=cyan>SUCCESS:</color> Arduino found on {port} via handshake!");
                    serialPort = tempPort;
                    isThreadRunning = true;
                    readThread = new Thread(ReadSerialData);
                    readThread.Start();
                    IsConnected = true;
                    return; // 接続に成功したので、ここで処理を終了
                }
                else
                {
                    tempPort.Close();
                }
            }
            catch (Exception)
            {
                if (tempPort.IsOpen) tempPort.Close();
            }
        }

        // 自動検出が失敗した場合、フォールバック接続を試みる
        Debug.LogWarning($"<color=orange>Phase 1 FAILED:</color> Handshake failed on all ports. Trying fallback to '{fallbackPortName}'.");

        // フォールバック用のポート名が指定されており、かつ利用可能なポートリストに存在するかチェック
        bool portExists = false;
        if (!string.IsNullOrEmpty(fallbackPortName))
        {
            foreach (string port in availablePorts)
            {
                if (port == fallbackPortName)
                {
                    portExists = true;
                    break;
                }
            }
        }

        if (portExists)
        {
            try
            {
                // ハンドシェイクなしで、指定されたポートに直接接続を試みる
                serialPort = new SerialPort(fallbackPortName, baudRate);
                serialPort.ReadTimeout = 1000;
                serialPort.Open();

                isThreadRunning = true;
                readThread = new Thread(ReadSerialData);
                readThread.Start();
                IsConnected = true;

                Debug.Log($"<color=cyan>SUCCESS:</color> Connected to fallback port {fallbackPortName}!");
            }
            catch (Exception e)
            {
                Debug.LogError($"<color=red>Phase 2 FAILED:</color> Could not connect to fallback port '{fallbackPortName}'. Error: {e.Message}");
            }
        }
        else
        {
            Debug.LogError($"<color=red>Phase 2 FAILED:</color> Fallback port '{fallbackPortName}' not found or not specified.");
        }
    }

    // 別スレッドで実行されるデータ読み取りメソッド
    private void ReadSerialData()
    {
        while (isThreadRunning && serialPort != null && serialPort.IsOpen)
        {
            try
            {
                // Arduinoから読み取った文字列を整数に変換できたら
                if (int.TryParse(serialPort.ReadLine(), out int value))
                {
                    // 静的変数に値を格納するだけ。Unityの命令は一切呼ばない。
                    GripValue = value;
                }
            }
            catch (TimeoutException)
            {
                // データが来ていない場合はタイムアウトするが、正常な動作なので何もしない
            }
            catch (Exception)
            {
                // ポートが閉じた時などにエラーが出るが、スレッド終了時には正常なので無視してOK
            }
        }
    }

    // ゲーム終了時に呼ばれる処理
    void OnDestroy()
    {
        isThreadRunning = false;

        // スレッドが終了するのを待つ「前」に、シリアルポートを強制的に閉じる
        if (serialPort != null && serialPort.IsOpen)
        {
            try
            {
                serialPort.Close();
                Debug.Log("<color=cyan>Serial port closed.</color>");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error closing serial port: {e.Message}");
            }
        }

        // スレッドが安全に終了したことを確認する
        if (readThread != null && readThread.IsAlive)
        {
            readThread.Join(100);
        }
    }
}