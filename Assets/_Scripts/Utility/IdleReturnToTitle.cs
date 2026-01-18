using UnityEngine;
using UnityEngine.SceneManagement;

public class IdleReturnToTitle : MonoBehaviour
{
    [Header("設定")]
    [Tooltip("放置とみなすまでの時間（秒）")]
    public float timeLimit = 60.0f;

    [Tooltip("操作中とみなす握力センサーの閾値（これより大きければタイマーリセット）")]
    public int gripThreshold = 200;

    [Tooltip("戻る先のシーン名")]
    public string titleSceneName = "TitleScreen";

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
            // 入力がなければ時間を計測
            currentIdleTime += Time.deltaTime;

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
        // 1. キーボード・マウス入力（デバッグ用含む）
        // Input.anyKey はマウスボタンやキーボードのどれかが押されているとtrue
        if (Input.anyKey) return true;

        // マウスが動いた場合もリセットしたいなら以下を追加
        if (Mathf.Abs(Input.GetAxis("Mouse X")) > 0.1f || Mathf.Abs(Input.GetAxis("Mouse Y")) > 0.1f) return true;

        // 2. Arduino入力（つり革）
        if (ArduinoInputManager.instance != null && ArduinoInputManager.instance.IsConnected)
        {
            // どちらかのつり革が閾値以上に握られているか
            if (ArduinoInputManager.GripValue1 > gripThreshold ||
                ArduinoInputManager.GripValue2 > gripThreshold)
            {
                return true;
            }

            // M5のボタンが押されているか
            if (ArduinoInputManager.IsM5BtnPressed) return true;
        }

        return false;
    }

    private void ReturnToTitle()
    {
        isReturning = true;
        Debug.Log("放置タイムアウト：タイトルへ戻ります");

        // StageManagerで使っているフェード機能があればそれを使う
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