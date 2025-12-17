using UnityEngine;
using System.IO.Ports;
using System.Threading;
using System;
using System.IO;

/// <summary>
/// Arduinoとのシリアル通信を管理し、握力センサー（タッチセンサー）の値をリアルタイムで取得するクラス。
/// 別スレッドでのデータ受信と、メインスレッドでのノイズ除去（スムージング）処理を担当する。
/// シングルトンパターンで実装され、シーン遷移しても破棄されない。
/// </summary>
public class ArduinoInputManager : MonoBehaviour
{
    /// <summary>
    /// シングルトンインスタンス。どこからでもアクセス可能。
    /// </summary>
    public static ArduinoInputManager instance;

    [Header("フォールバック設定")]
    [Tooltip("自動検出が失敗した場合に、接続を試みるCOMポート名（例: COM3, /dev/tty.usbmodem...）")]
    public string fallbackPortName = "COM3";

    [Tooltip("Arduino側のSerial.beginと合わせるボーレート (通信速度)")]
    public int baudRate = 9600;

    [Header("信号処理設定")]
    [Tooltip("生データのノイズをどの程度滑らかにするか (0.1 = ゆっくり/安定, 0.9 = 素早い/敏感)。値を小さくするとノイズに強くなるが遅延が増える。")]
    [Range(0.01f, 1f)]
    public float smoothingFactor = 0.2f;

    /// <summary>
    /// Arduinoとの接続状態。接続成功時にtrueとなる。
    /// </summary>
    public bool IsConnected { get; private set; } = false;

    /// <summary>
    /// Arduinoから送られてきた生のセンサー値。
    /// 別スレッドから頻繁に書き込まれるため、volatileキーワードでメモリの可視性を保証している。
    /// ノイズが含まれるため、直接の使用は非推奨。
    /// </summary>
    public static volatile int GripValue;

    /// <summary>
    /// ノイズ除去済みの滑らかなセンサー値。
    /// ゲームロジック（キャリブレーションや判定）では、原則としてこの値を使用する。
    /// </summary>
    public float SmoothedGripValue { get; private set; } = 0f;

    // 内部変数
    private SerialPort serialPort;
    private Thread readThread;
    private bool isThreadRunning = false;

    /// <summary>
    /// 初期化処理。シングルトンの設定とConfigファイルの読み込みを行う。
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
    /// </summary>
    void Start()
    {
        ConnectToArduino();
    }

    /// <summary>
    /// 毎フレームの更新処理。
    /// 別スレッドで受信した生データを、メインスレッドで滑らかに補間（スムージング）する。
    /// </summary>
    void Update()
    {
        // 生データ(GripValue)に向かって、SmoothedGripValueを徐々に近づける（線形補間）。
        // これにより、センサー特有のスパイクノイズやチャタリングを抑制し、安定した値を提供する。
        SmoothedGripValue = Mathf.Lerp(SmoothedGripValue, (float)GripValue, smoothingFactor);
    }

    /// <summary>
    /// 指定されたCOMポートへの接続を試み、成功時にデータ読み取り用スレッドを起動する。
    /// </summary>
    private void ConnectToArduino()
    {
        // PC上の利用可能なポートを取得（デバッグ用）
        string[] availablePorts = SerialPort.GetPortNames();
        if (availablePorts.Length == 0)
        {
            Debug.LogWarning("PCにCOMポートが一つも見つかりません。Arduinoが接続されていない可能性があります。");
            return;
        }

        if (string.IsNullOrEmpty(fallbackPortName))
        {
            Debug.LogError("<color=red>Error:</color> Inspectorまたはconfig.txtで 'Fallback Port Name' が設定されていません！");
            return;
        }

        try
        {
            // ポートを開く
            serialPort = new SerialPort(fallbackPortName, baudRate);
            serialPort.ReadTimeout = 1000; // 読み取りタイムアウト設定
            serialPort.Open();

            // 受信スレッドの開始
            isThreadRunning = true;
            readThread = new Thread(ReadSerialData);
            readThread.Start();
            IsConnected = true;

            Debug.Log($"<color=green>SUCCESS:</color> Arduino ('{fallbackPortName}') への接続に成功しました！");
        }
        catch (Exception e)
        {
            Debug.LogError($"<color=red>FAILED:</color> '{fallbackPortName}' への接続に失敗しました。Error: {e.Message}");
            IsConnected = false;
        }
    }

    /// <summary>
    /// 別スレッドで実行されるデータ読み取りループ。
    /// Serial.printlnで送られてくる1行ごとのデータを解析し、GripValueを更新する。
    /// </summary>
    private void ReadSerialData()
    {
        while (isThreadRunning && serialPort != null && serialPort.IsOpen)
        {
            try
            {
                // 1行読み取り、整数に変換して格納
                string line = serialPort.ReadLine();
                if (int.TryParse(line, out int value))
                {
                    GripValue = value;
                }
            }
            catch (TimeoutException)
            {
                // データが来ていない時はここに来るが、正常動作なので無視
            }
            catch (Exception e)
            {
                // ポートが抜かれたり、閉じられた際のエラー
                if (isThreadRunning)
                {
                    Debug.LogWarning($"Serial Read Error: {e.Message}");
                }
            }
        }
    }

    /// <summary>
    /// 実行ファイルと同階層にある 'config.txt' からポート設定を読み込む。
    /// ファイルが存在しない場合は、現在の設定値で自動生成する。
    /// </summary>
    private void LoadConfig()
    {
        // Application.dataPathの親ディレクトリ（.exeと同じ場所）を指定
        string configPath = Path.Combine(Application.dataPath, "..", "config.txt");

        // ファイルが存在するか確認
        if (File.Exists(configPath))
        {
            try
            {
                // 読み込み処理
                string[] lines = File.ReadAllLines(configPath);
                foreach (string line in lines)
                {
                    // "port_name=COM3" のような行を探す
                    if (line.StartsWith("port_name="))
                    {
                        string portFromConfig = line.Split('=')[1].Trim();
                        fallbackPortName = portFromConfig;
                        Debug.Log($"<color=yellow>Config Loaded:</color> Port set to '{fallbackPortName}' from config.txt");
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
            // ファイルが無いなら、デフォルト設定で自動生成する
            try
            {
                string defaultContent = $"port_name={fallbackPortName}\n# Change this value to match your Arduino port (e.g., COM3, /dev/tty...)";
                File.WriteAllText(configPath, defaultContent);
                Debug.Log($"<color=cyan>Config Created:</color> config.txt generated at {configPath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"Error creating config file: {e.Message}");
            }
        }
    }

    /// <summary>
    /// アプリケーション終了時やオブジェクト破棄時に呼ばれる。
    /// スレッドを安全に停止し、シリアルポートを開放する。
    /// </summary>
    void OnDestroy()
    {
        isThreadRunning = false;

        // ポートを閉じる（これによりReadLineが中断され、スレッドが終了に向かう）
        if (serialPort != null && serialPort.IsOpen)
        {
            try
            {
                serialPort.Close();
                Debug.Log("<color=cyan>Serial port closed.</color>");
            }
            catch (Exception e)
            {
                Debug.LogError($"Error closing serial port: {e.Message}");
            }
        }

        // スレッドの終了を待機
        if (readThread != null && readThread.IsAlive)
        {
            readThread.Join(100);
        }
    }
}