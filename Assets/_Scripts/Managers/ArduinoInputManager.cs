using UnityEngine;
using System.IO.Ports;
using System.Threading;
using System;
using System.IO;

/// <summary>
/// Arduinoとの通信を管理し、握力センサーの値をリアルタイムで取得する。
/// シングルトンパターンで実装され、別スレッドでシリアル通信を行う。
/// </summary>
public class ArduinoInputManager : MonoBehaviour
{
    public static ArduinoInputManager instance;

    [Header("フォールバック設定")]
    [Tooltip("自動検出が失敗した場合に、接続を試みるCOMポート名")]
    public string fallbackPortName = "COM3";
    [Tooltip("Arduinoと合わせるボーレート (通信速度)")]
    public int baudRate = 9600;

    /// <summary>
    /// Arduinoとの接続状態を取得する。
    /// </summary>
    public bool IsConnected { get; private set; } = false;

    /// <summary>
    /// 他のスクリプトから参照する握力センサーの値。
    /// 別スレッドから書き込まれるため、volatileキーワードで可視性を保証する。
    /// </summary>
    public static volatile int GripValue;

    private SerialPort serialPort;
    private Thread readThread;
    private bool isThreadRunning = false;

    /// <summary>
    /// シングルトンの初期化と設定ファイルの読み込みを行う。
    /// 最初に生成されたインスタンス以外は破棄される。
    /// </summary>
    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            LoadConfig();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// ゲーム開始時にArduinoへの接続を試みる。
    /// Awake後に呼ばれるため、LoadConfigで読み込んだ設定が反映される。
    /// </summary>
    void Start()
    {
        ConnectToArduino();
    }

    /// <summary>
    /// 指定されたCOMポートへの接続を試み、成功時にデータ読み取り用スレッドを起動する。
    /// 接続失敗時はIsConnectedがfalseのまま維持される。
    /// </summary>
    private void ConnectToArduino()
    {
        string[] availablePorts = SerialPort.GetPortNames();
        if (availablePorts.Length == 0)
        {
            Debug.LogWarning("PCにCOMポートが一つも見つかりません。");
            return;
        }
        Debug.Log("利用可能なポート: " + string.Join(", ", availablePorts));

        if (string.IsNullOrEmpty(fallbackPortName))
        {
            Debug.LogError("<color=red>Inspectorまたはconfig.txtで 'Fallback Port Name' が設定されていません！</color>");
            return;
        }

        Debug.Log($"<color=cyan>指定されたポート '{fallbackPortName}' への直接接続を試みます...</color>");
        try
        {
            serialPort = new SerialPort(fallbackPortName, baudRate);
            serialPort.ReadTimeout = 1000;
            serialPort.Open();

            isThreadRunning = true;
            readThread = new Thread(ReadSerialData);
            readThread.Start();
            IsConnected = true;

            Debug.Log($"<color=green>SUCCESS:</color> '{fallbackPortName}' への接続に成功しました！");
        }
        catch (Exception e)
        {
            Debug.LogError($"<color=red>FAILED:</color> '{fallbackPortName}' への接続に失敗しました。Error: {e.Message}");
            IsConnected = false;
        }
    }

    /// <summary>
    /// 別スレッドで実行され、Arduinoからのデータを継続的に読み取る。
    /// 読み取った整数値をGripValueに格納する。タイムアウトは正常動作として扱う。
    /// </summary>
    private void ReadSerialData()
    {
        while (isThreadRunning && serialPort != null && serialPort.IsOpen)
        {
            try
            {
                if (int.TryParse(serialPort.ReadLine(), out int value))
                {
                    GripValue = value;
                }
            }
            catch (TimeoutException)
            {
                // データ未受信時のタイムアウトは正常動作
            }
            catch (Exception)
            {
                // ポート終了時のエラーは無視
            }
        }
    }

    /// <summary>
    /// ビルド後の実行ファイルと同階層にある config.txt からポート名を読み込み、
    /// fallbackPortNameを上書きする。ファイルが存在しない場合はInspector設定値を使用する。
    /// </summary>
    private void LoadConfig()
    {
        string configPath = Path.Combine(Application.dataPath, "..", "config.txt");
        Debug.Log($"Searching for config file at: {configPath}");

        if (File.Exists(configPath))
        {
            try
            {
                string[] lines = File.ReadAllLines(configPath);
                foreach (string line in lines)
                {
                    if (line.StartsWith("port_name="))
                    {
                        string portFromConfig = line.Split('=')[1].Trim();
                        fallbackPortName = portFromConfig;
                        Debug.Log($"<color=yellow>Config Loaded:</color> Fallback port set to '{fallbackPortName}' from config.txt");
                        return;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error reading config file: {e.Message}");
            }
        }
        else
        {
            Debug.LogWarning("config.txt not found. Using default Inspector value for fallback port.");
        }
    }

    /// <summary>
    /// ゲーム終了時にスレッドを停止し、シリアルポートを安全に閉じる。
    /// スレッド終了前にポートを閉じることで、ReadLineのブロッキングを解除する。
    /// </summary>
    void OnDestroy()
    {
        isThreadRunning = false;

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

        if (readThread != null && readThread.IsAlive)
        {
            readThread.Join(100);
        }
    }
}