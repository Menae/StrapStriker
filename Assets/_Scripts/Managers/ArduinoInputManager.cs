using UnityEngine;
using System.IO.Ports;
using System.Threading;
using System;
using System.IO;

public class ArduinoInputManager : MonoBehaviour
{
    public static ArduinoInputManager instance;

    [Header("フォールバック設定")]
    [Tooltip("自動検出が失敗した場合に、接続を試みるCOMポート名")]
    public string fallbackPortName = "COM3";
    [Tooltip("Arduinoと合わせるボーレート (通信速度)")]
    public int baudRate = 9600;

    public bool IsConnected { get; private set; } = false;

    // --- 他のスクリプトから参照する握力センサーの値 ---
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

            // 設定ファイルを読み込んで、フォールバック用のポート番号を上書きする
            LoadConfig();
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
        // 利用可能なポートをチェック（デバッグログ用）
        string[] availablePorts = SerialPort.GetPortNames();
        if (availablePorts.Length == 0)
        {
            Debug.LogWarning("PCにCOMポートが一つも見つかりません。");
            return;
        }
        Debug.Log("利用可能なポート: " + string.Join(", ", availablePorts));

        // Inspectorで指定されたポート名が空かどうかチェック
        if (string.IsNullOrEmpty(fallbackPortName))
        {
            Debug.LogError("<color=red>Inspectorまたはconfig.txtで 'Fallback Port Name' が設定されていません！</color>");
            return;
        }

        // --- メインの接続処理 ---
        Debug.Log($"<color=cyan>指定されたポート '{fallbackPortName}' への直接接続を試みます...</color>");
        try
        {
            // 指定されたポートに、ハンドシェイクなしで直接接続する
            serialPort = new SerialPort(fallbackPortName, baudRate);
            serialPort.ReadTimeout = 1000;
            serialPort.Open();

            // 接続成功
            isThreadRunning = true;
            readThread = new Thread(ReadSerialData);
            readThread.Start();
            IsConnected = true;

            Debug.Log($"<color=green>SUCCESS:</color> '{fallbackPortName}' への接続に成功しました！");
        }
        catch (Exception e)
        {
            // 接続失敗
            Debug.LogError($"<color=red>FAILED:</color> '{fallbackPortName}' への接続に失敗しました。Error: {e.Message}");
            IsConnected = false;
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

    // config.txtからポート番号を読み込むメソッド
    private void LoadConfig()
    {
        // ビルドした.exeファイルと同じ階層にある "config.txt" のパスを取得
        string configPath = Path.Combine(Application.dataPath, "..", "config.txt");

        Debug.Log($"Searching for config file at: {configPath}");

        // もし設定ファイルが存在すれば、その中身を読み取る
        if (File.Exists(configPath))
        {
            try
            {
                // ファイルの各行を読み込む
                string[] lines = File.ReadAllLines(configPath);
                foreach (string line in lines)
                {
                    // "port_name=COM4" のような行を探す
                    if (line.StartsWith("port_name="))
                    {
                        // "="の右側にある値（ポート名）を取得する
                        string portFromConfig = line.Split('=')[1].Trim();
                        // Inspectorで設定された値を、ファイルから読み取った値で上書きする
                        fallbackPortName = portFromConfig;
                        Debug.Log($"<color=yellow>Config Loaded:</color> Fallback port set to '{fallbackPortName}' from config.txt");
                        return; // 読み込めたので処理を終了
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