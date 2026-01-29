using UnityEngine;
using System.IO.Ports;
using System.Threading;
using System;
using System.IO;

/// <summary>
/// M5StickC Plus2とのシリアル通信を管理し、コントローラーの入力情報を一元管理するクラス。
/// 従来のタッチセンサーに加え、ジャイロ・加速度センサーの値もここから取得する。
/// 別スレッドでのデータ受信と、メインスレッドでのデータ提供を担当する。
/// </summary>
public class ArduinoInputManager : MonoBehaviour
{
    /// <summary>
    /// シングルトンインスタンス。
    /// </summary>
    public static ArduinoInputManager instance;

    [Header("接続設定")]
    [Tooltip("自動検出が失敗した場合に、接続を試みるCOMポート名")]
    public string fallbackPortName = "COM3";

    [Tooltip("M5StickC側の送信速度と合わせる必要があります。")]
    public int baudRate = 115200; // M5StickCの仕様に合わせて更新

    [Header("信号処理設定")]
    [Tooltip("タッチセンサーのノイズ除去係数 (0.1 = ゆっくり/安定, 1.0 = 生データそのまま)。")]
    [Range(0.01f, 1f)]
    public float smoothingFactor = 0.2f;

    [Header("デバッグ設定")]
    [Tooltip("毎フレームの握力センサー値をコンソールに出力するかどうか")]
    public bool showDebugLogs = false;

    /// <summary>
    /// デバイスとの接続状態。
    /// </summary>
    public bool IsConnected { get; private set; } = false;

    // --- M5StickC 受信データ (外部公開用) ---

    /// <summary>
    /// ジャイロセンサ（角速度）の生データ (gx, gy, gz)。
    /// 回転計算に使用。
    /// </summary>
    public static Vector3 RawGyro { get; private set; }

    /// <summary>
    /// 加速度センサの生データ (ax, ay, az)。
    /// 傾き検知・重力方向の特定に使用。
    /// </summary>
    public static Vector3 RawAccel { get; private set; }

    /// <summary>
    /// M5本体の正面ボタンの状態。
    /// true: 押されている (1), false: 離されている (0)
    /// </summary>
    public static bool IsM5BtnPressed { get; private set; }

    /// <summary>
    /// 静電容量センサー1（つり革左）の生の値。
    /// </summary>
    public static volatile int GripValue1;

    /// <summary>
    /// 静電容量センサー2（つり革右）の生の値。
    /// </summary>
    public static volatile int GripValue2;

    // --- 加工済みデータ ---

    /// <summary>
    /// ノイズ除去済みの滑らかなセンサー値（センサー1）。
    /// </summary>
    public float SmoothedGripValue1 { get; private set; } = 0f;

    /// <summary>
    /// ノイズ除去済みの滑らかなセンサー値（センサー2）。
    /// </summary>
    public float SmoothedGripValue2 { get; private set; } = 0f;

    // 内部変数
    private SerialPort serialPort;
    private Thread readThread;
    private bool isThreadRunning = false;

    // スレッド間データ受け渡し用のロックオブジェクト
    private readonly object dataLock = new object();

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
    /// ゲーム開始時にデバイスへの接続を試みる。
    /// </summary>
    void Start()
    {
        ConnectToDevice();
    }

    /// <summary>
    /// 毎フレームの更新処理。
    /// 生データを滑らかに補間する処理のみメインスレッドで行う。
    /// </summary>
    void Update()
    {
        // 握力値のスムージング処理
        SmoothedGripValue1 = Mathf.Lerp(SmoothedGripValue1, (float)GripValue1, smoothingFactor);
        SmoothedGripValue2 = Mathf.Lerp(SmoothedGripValue2, (float)GripValue2, smoothingFactor);

        // センサー値のモニタリング
        if (showDebugLogs)
        {
            Debug.Log($"[M5 Monitor] Grip1: {GripValue1} / Grip2: {GripValue2}");
        }
    }

    /// <summary>
    /// 指定されたCOMポートへの接続を試み、成功時にデータ読み取り用スレッドを起動する。
    /// </summary>
    private void ConnectToDevice()
    {
        string[] availablePorts = SerialPort.GetPortNames();
        if (availablePorts.Length == 0)
        {
            Debug.LogWarning("PCにCOMポートが見つかりません。M5StickCが接続されていない可能性があります。");
            return;
        }

        if (string.IsNullOrEmpty(fallbackPortName))
        {
            Debug.LogError("Error: ConfigまたはInspectorでポート名が設定されていません。");
            return;
        }

        try
        {
            serialPort = new SerialPort(fallbackPortName, baudRate);
            serialPort.ReadTimeout = 1000;
            serialPort.DtrEnable = true; // M5StickC/ESP32系で再起動を防ぐために必要な場合がある
            serialPort.RtsEnable = true;
            serialPort.Open();
            Debug.Log($"ポート {serialPort.PortName} を開きました。");

            isThreadRunning = true;
            readThread = new Thread(ReadSerialData);
            readThread.Start();
            IsConnected = true;

            Debug.Log($"<color=green>SUCCESS:</color> Device ('{fallbackPortName}') connected at {baudRate} bps.");
        }
        catch (Exception e)
        {
            Debug.LogError($"<color=red>FAILED:</color> Connection error on '{fallbackPortName}': {e.Message}");
            IsConnected = false;
        }
    }

    /// <summary>
    /// 別スレッドで実行されるデータ読み取りループ。
    /// CSV形式のデータを解析し、各センサー値を更新する。
    /// フォーマット: gx, gy, gz, ax, ay, az, button, t1, t2
    /// </summary>
    private void ReadSerialData()
    {
        string buffer = ""; // 届いた文字を貯める箱

        while (isThreadRunning && serialPort != null && serialPort.IsOpen)
        {
            try
            {
                // データが届いているか確認
                if (serialPort.BytesToRead > 0)
                {
                    // 1文字ずつ読み込む
                    char c = (char)serialPort.ReadChar();

                    // もし改行（\n か \r）が届いたら、1行完成とみなす
                    if (c == '\n' || c == '\r')
                    {
                        if (!string.IsNullOrEmpty(buffer))
                        {
                            // 完成した1行をデバッグ表示
                            Debug.Log("受信完了: " + buffer);

                            // データを分解して反映する処理を呼び出す
                            ProcessLine(buffer);
                            buffer = ""; // 箱を空にする
                        }
                    }
                    else
                    {
                        buffer += c; // 改行以外なら箱に貯める
                    }
                }
            }
            catch (Exception e)
            {
                if (isThreadRunning) Debug.LogWarning($"Serial Read Error: {e.Message}");
            }
        }
    }

    // 分解処理を別出しにすると分かりやすくなります
    private void ProcessLine(string line)
{
    // カンマで分割
    string[] parts = line.Split(',');

    // 9個以上のデータがあることを確認
    if (parts.Length >= 9)
    {
        try {
            // すべてを一旦 float.TryParse + Trim() で読み込む（これが一番確実です）
            // float.TryParseは、前後の空白を無視し、整数も小数も読み込めます
            float.TryParse(parts[0].Trim(), out float gx);
            float.TryParse(parts[1].Trim(), out float gy);
            float.TryParse(parts[2].Trim(), out float gz);
            float.TryParse(parts[3].Trim(), out float ax);
            float.TryParse(parts[4].Trim(), out float ay);
            float.TryParse(parts[5].Trim(), out float az);
            float.TryParse(parts[6].Trim(), out float btn);
            float.TryParse(parts[7].Trim(), out float t1);
            float.TryParse(parts[8].Trim(), out float t2);

            // 値を反映（static変数に代入）
            RawGyro = new Vector3(gx, gy, gz);
            RawAccel = new Vector3(ax, ay, az);
            IsM5BtnPressed = (btn >= 1.0f);
            
            // ★ ここでログを出して、解析後の数字を確認！
            Debug.Log($"解析成功: センサー1={t1}, センサー2={t2}");

            GripValue1 = (int)t1;
            GripValue2 = (int)t2;
        }
        catch (System.Exception e) {
            Debug.LogWarning("解析中にエラーが発生しました: " + e.Message);
        }
    }
}

    /// <summary>
    /// 外部ファイル 'config.txt' からポート設定を読み込む。
    /// エディタ実行時はInspectorの設定を優先するため読み込みをスキップする。
    /// </summary>
    private void LoadConfig()
    {
#if UNITY_EDITOR
        // エディタ上ではConfigを無視して、Inspectorの設定値を使う
        Debug.Log("Editor Mode: Skipping Config Load. Using Inspector settings.");
#else
        // ビルド済みアプリ（本番環境）でのみ実行されるコード
        string configPath = Path.Combine(Application.dataPath, "..", "config.txt");

        if (File.Exists(configPath))
        {
            try
            {
                string[] lines = File.ReadAllLines(configPath);
                foreach (string line in lines)
                {
                    if (line.StartsWith("port_name="))
                    {
                        fallbackPortName = line.Split('=')[1].Trim();
                        Debug.Log($"Config Loaded: Port set to '{fallbackPortName}'");
                        return;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Config Read Error: {e.Message}");
            }
        }
        else
        {
            // Configが存在しない場合はデフォルト値で作成
            try
            {
                string defaultContent = $"port_name={fallbackPortName}\n# Set your COM port here (e.g. COM3)";
                File.WriteAllText(configPath, defaultContent);
            }
            catch (Exception e)
            {
                Debug.LogError($"Config Write Error: {e.Message}");
            }
        }
#endif
    }

    /// <summary>
    /// 終了時のクリーンアップ処理。
    /// </summary>
    void OnDestroy()
    {
        isThreadRunning = false;

        if (serialPort != null && serialPort.IsOpen)
        {
            try
            {
                serialPort.Close();
            }
            catch (Exception e)
            {
                Debug.LogError($"Port Close Error: {e.Message}");
            }
        }

        if (readThread != null && readThread.IsAlive)
        {
            readThread.Join(100);
        }
    }
}