using UnityEngine;
using UnityEngine.SceneManagement;

public class IdleReturnToTitle : MonoBehaviour
{
    [Header("設定")]
    [Tooltip("放置とみなすまでの時間（秒）")]
    public float timeLimit = 60.0f;

    [Tooltip("操作中とみなす握力センサーの閾値")]
    public float gripThreshold = 1500f; // floatに変更し、Smoothed値と比較

    [Tooltip("戻る先のシーン名")]
    public string titleSceneName = "TitleScreen";

    [Header("入力検知オプション")]
    [Tooltip("マウス移動を検知対象に含めるか")]
    public bool detectMouseMovement = false; // 誤検知しやすいのでデフォルトOFF推奨
    [Tooltip("デバッグログを表示するか")]
    public bool showDebugLog = true;

    private float currentIdleTime = 0f;
    private bool isReturning = false;

    void Update()
    {
        if (isReturning) return;

        // 入力があるかチェック
        if (IsInputActive())
        {
            // 入力があればタイマーリセット
            currentIdleTime = 0f;
        }
        else
        {
            currentIdleTime += Time.unscaledDeltaTime;

            if (showDebugLog && Time.frameCount % 60 == 0) // 1秒に1回ログ出し
            {
                // Debug.Log($"Idle Time: {currentIdleTime:F1} / {timeLimit:F1}");
            }

            if (currentIdleTime >= timeLimit)
            {
                ReturnToTitle();
            }
        }
    }

    /// <summary>
    /// 何らかの操作が行われているかを判定
    /// </summary>
    private bool IsInputActive()
    {
        // 1. キーボード入力
        if (Input.anyKey)
        {
            if (showDebugLog && currentIdleTime > 1f) Debug.Log("<color=yellow>Reset:</color> Keyboard Input");
            return true;
        }

        // 2. マウス移動（オプション）
        if (detectMouseMovement)
        {
            if (Mathf.Abs(Input.GetAxis("Mouse X")) > 0.1f || Mathf.Abs(Input.GetAxis("Mouse Y")) > 0.1f)
            {
                if (showDebugLog && currentIdleTime > 1f) Debug.Log("<color=yellow>Reset:</color> Mouse Movement");
                return true;
            }
        }

        // 3. Arduino入力（つり革）
        if (ArduinoInputManager.instance != null && ArduinoInputManager.instance.IsConnected)
        {
            // 生データ(GripValue)ではなく、平滑化データ(SmoothedGripValue)を使う
            float val1 = ArduinoInputManager.instance.SmoothedGripValue1;
            float val2 = ArduinoInputManager.instance.SmoothedGripValue2;

            if (val1 > gripThreshold || val2 > gripThreshold)
            {
                if (showDebugLog && currentIdleTime > 1f)
                    Debug.Log($"<color=yellow>Reset:</color> Sensor Grip! (Val1:{val1:F0}, Val2:{val2:F0})");
                return true;
            }

            if (ArduinoInputManager.IsM5BtnPressed)
            {
                if (showDebugLog && currentIdleTime > 1f) Debug.Log("<color=yellow>Reset:</color> M5 Button");
                return true;
            }
        }

        return false;
    }

    private void ReturnToTitle()
    {
        isReturning = true;
        Debug.Log("<color=red>放置タイムアウト：タイトルへ戻ります</color>");

        Time.timeScale = 1f;

        if (SceneFader.instance != null)
        {
            SceneFader.instance.LoadSceneWithFade(titleSceneName);
        }
        else
        {
            SceneManager.LoadScene(titleSceneName);
        }
    }
}