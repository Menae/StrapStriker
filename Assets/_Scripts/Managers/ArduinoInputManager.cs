using UnityEngine;
using System.IO.Ports;
using System.Threading;
using System;
using System.IO;

/// <summary>
/// M5StickC Plus2とのシリアル通信および入力データを一元管理するクラス。
/// 静電容量センサー、ジャイロ、加速度センサーの値を取得し、メインスレッドへ提供する。
/// </summary>
public class ArduinoInputManager : MonoBehaviour
{
    /// <summary>
    /// シングルトンインスタンス。
    /// </summary>
    public static ArduinoInputManager instance;

    [Header("接続設定")]
    [Tooltip("自動検出失敗時に接続を試みるCOMポート名")]
    public string fallbackPortName = "COM3";

    [Tooltip("デバイス側のボーレート設定")]
    public int baudRate = 115200;

    [Header("信号処理設定")]
    [Tooltip("センサー値の平滑化係数 (0.1: 滑らか ～ 1.0: 生データ)")]
    [Range(0.01f, 1f)]
    public float smoothingFactor = 0.2f;

    [Header("トラブルシューティング")]
    [Tooltip("GripValue2の異常値（10000以上）を検出し、0に補正するかどうか")]
    public bool fixGripAnomaly = true;

    [Header("デバッグ設定")]
    [Tooltip("受信したセンサー値をコンソールに出力するか")]
    public bool showDebugLogs = false;

    /// <summary>
    /// デバイスとの接続状態。
    /// </summary>
    public bool IsConnected { get; private set; } = false;

    // --- 受信データ (外部参照用) ---

    /// <summary>
    /// ジャイロセンサ生データ (角速度)。
    /// </summary>
    public static Vector3 RawGyro { get; private set; }

    /// <summary>
    /// 加速度センサ生データ。
    /// </summary>
    public static Vector3 RawAccel { get; private set; }

    /// <summary>
    /// 本体正面ボタンの状態 (true: 押下中)。
    /// </summary>
    public static bool IsM5BtnPressed { get; private set; }

    /// <summary>
    /// 左側つり革の静電容量センサー値 (生データ)。
    /// </summary>
    public static volatile int GripValue1;

    /// <summary>
    /// 右側つり革の静電容量センサー値 (生データ)。
    /// </summary>
    public static volatile int GripValue2;

    // --- 加工済みデータ ---

    public float SmoothedGripValue1 { get; private set; } = 0f;
    public float SmoothedGripValue2 { get; private set; } = 0f;

    // 内部変数
    private SerialPort serialPort;
    private Thread readThread;
    private bool isThreadRunning = false;

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

    void Start()
    {
        ConnectToDevice();
    }

    /// <summary>
    /// センサー値の平滑化とモニタリングを行う。
    /// </summary>
    void Update()
    {
        // センサー値の平滑化（線形補間）
        SmoothedGripValue1 = Mathf.Lerp(SmoothedGripValue1, (float)GripValue1, smoothingFactor);
        SmoothedGripValue2 = Mathf.Lerp(SmoothedGripValue2, (float)GripValue2, smoothingFactor);

        if (showDebugLogs)
        {
            Debug.Log($"[M5 Monitor] Grip1: {GripValue1} / Grip2: {GripValue2}");
        }
    }

    /// <summary>
    /// シリアルポートへの接続を試行し、成功時は読取スレッドを開始する。
    /// </summary>
    private void ConnectToDevice()
    {
        string[] availablePorts = SerialPort.GetPortNames();
        if (availablePorts.Length == 0)
        {
            Debug.LogWarning("COMポートが見つかりません。デバイス接続を確認してください。");
            return;
        }

        if (string.IsNullOrEmpty(fallbackPortName))
        {
            Debug.LogError("ポート名が未設定です。InspectorまたはConfigを確認してください。");
            return;
        }

        try
        {
            serialPort = new SerialPort(fallbackPortName, baudRate);
            serialPort.ReadTimeout = 1000;
            serialPort.DtrEnable = true;
            serialPort.RtsEnable = true;
            serialPort.Open();

            isThreadRunning = true;
            readThread = new Thread(ReadSerialData);
            readThread.Start();
            IsConnected = true;

            Debug.Log($"接続成功: {fallbackPortName} ({baudRate} bps)");
        }
        catch (Exception e)
        {
            Debug.LogError($"接続エラー ({fallbackPortName}): {e.Message}");
            IsConnected = false;
        }
    }

    /// <summary>
    /// シリアルデータの読み取りループ（別スレッド実行）。
    /// </summary>
    private void ReadSerialData()
    {
        string buffer = "";

        while (isThreadRunning && serialPort != null && serialPort.IsOpen)
        {
            try
            {
                if (serialPort.BytesToRead > 0)
                {
                    char c = (char)serialPort.ReadChar();

                    if (c == '\n' || c == '\r')
                    {
                        if (!string.IsNullOrEmpty(buffer))
                        {
                            ProcessLine(buffer);
                            buffer = "";
                        }
                    }
                    else
                    {
                        buffer += c;
                    }
                }
            }
            catch (Exception e)
            {
                if (isThreadRunning) Debug.LogWarning($"読み取りエラー: {e.Message}");
            }
        }
    }

    /// <summary>
    /// 受信したCSV形式の文字列を解析し、各センサー変数へ反映する。
    /// </summary>
    private void ProcessLine(string line)
    {
        string[] parts = line.Split(',');

        // データ形式: gx, gy, gz, ax, ay, az, button, t1, t2
        if (parts.Length >= 9)
        {
            try
            {
                float.TryParse(parts[0].Trim(), out float gx);
                float.TryParse(parts[1].Trim(), out float gy);
                float.TryParse(parts[2].Trim(), out float gz);
                float.TryParse(parts[3].Trim(), out float ax);
                float.TryParse(parts[4].Trim(), out float ay);
                float.TryParse(parts[5].Trim(), out float az);
                float.TryParse(parts[6].Trim(), out float btn);
                float.TryParse(parts[7].Trim(), out float t1);
                float.TryParse(parts[8].Trim(), out float t2);

                // ---------------------------------------------------------
                // 異常値フィルタリング (Software Fix)
                // ハードウェア起因でGripValue2が未接触時に超高数値（20000等）になる場合、
                // 強制的に0（離している状態）として扱う。
                // ---------------------------------------------------------
                if (fixGripAnomaly)
                {
                    if (t2 >= 10000f)
                    {
                        t2 = 0f;
                    }
                }

                // 各変数の更新
                RawGyro = new Vector3(gx, gy, gz);
                RawAccel = new Vector3(ax, ay, az);
                IsM5BtnPressed = (btn >= 1.0f);

                GripValue1 = (int)t1;
                GripValue2 = (int)t2;
            }
            catch (Exception e)
            {
                Debug.LogWarning("データ解析失敗: " + e.Message);
            }
        }
    }

    /// <summary>
    /// 外部コンフィグファイルからポート設定を読み込む。
    /// エディタ実行時はInspector設定を優先するためスキップする。
    /// </summary>
    private void LoadConfig()
    {
#if UNITY_EDITOR
        Debug.Log("Editor Mode: Config読み込みをスキップし、Inspector設定を使用します。");
#else
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
                        Debug.Log($"Config適用: ポートを '{fallbackPortName}' に設定しました。");
                        return;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Config読み込みエラー: {e.Message}");
            }
        }
        else
        {
            try
            {
                string defaultContent = $"port_name={fallbackPortName}\n# Set your COM port here (e.g. COM3)";
                File.WriteAllText(configPath, defaultContent);
            }
            catch (Exception e)
            {
                Debug.LogError($"Config作成エラー: {e.Message}");
            }
        }
#endif
    }

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
                Debug.LogError($"ポート切断エラー: {e.Message}");
            }
        }

        if (readThread != null && readThread.IsAlive)
        {
            readThread.Join(100);
        }
    }
}