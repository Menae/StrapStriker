using System.Collections.Generic;
using UnityEngine;
using System;
using TMPro;

public enum Language
{
    Japanese,
    English
}

public class LocalizationManager : MonoBehaviour
{
    public static LocalizationManager Instance;

    public Language CurrentLanguage { get; private set; } = Language.Japanese;

    public event Action OnLanguageChanged;

    [Header("フォント設定")]
    [Tooltip("日本語表示用のフォントアセット")]
    public TMP_FontAsset japaneseFont;
    [Tooltip("英語表示用のフォントアセット")]
    public TMP_FontAsset englishFont;

    // 辞書データ
    private Dictionary<string, Dictionary<Language, string>> localizedText;

    private void Awake()
    {
        // シングルトンパターンの確立
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeDictionary();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void InitializeDictionary()
    {
        localizedText = new Dictionary<string, Dictionary<Language, string>>()
        {
            // キー                 日本語                      英語
            { "ui_start",   new Dictionary<Language, string> { { Language.Japanese, "ゲーム開始" }, { Language.English, "START GAME" } } },
            { "ui_retry",   new Dictionary<Language, string> { { Language.Japanese, "握ってリトライ" },   { Language.English, "GRIP TO RETRY" } } },
            { "ui_result",  new Dictionary<Language, string> { { Language.Japanese, "結果発表" },   { Language.English, "RESULT" } } },
            { "msg_clear",  new Dictionary<Language, string> { { Language.Japanese, "クリア！" },   { Language.English, "CLEARED!" } } },
            { "ui_language", new Dictionary<Language, string> { { Language.Japanese, "言語: 日本語" }, { Language.English, "Lang: English" } } },
            { "calib_grip_instruction", new Dictionary<Language, string> {
                { Language.Japanese, "つり革を\nギュッと握ってください" },
                { Language.English, "Please grip the strap\nfirmly with your hand." }
            } },

            { "calib_measuring_grip", new Dictionary<Language, string> {
                { Language.Japanese, "計測中... あと <size=120%>{0:F1}</size> 秒\n<color=yellow>そのまま握っていて！</color>" },
                { Language.English, "Measuring... <size=120%>{0:F1}</size> sec\n<color=yellow>Keep gripping!</color>" }
            } },

            { "calib_ok", new Dictionary<Language, string> {
                { Language.Japanese, "OK!" },
                { Language.English, "OK!" }
            } },

            { "calib_release_instruction", new Dictionary<Language, string> {
                { Language.Japanese, "手を離して\nリラックスしてください" },
                { Language.English, "Release your hands\nand relax." }
            } },

            { "calib_measuring_release", new Dictionary<Language, string> {
                { Language.Japanese, "計測中... あと <size=120%>{0:F1}</size> 秒\n<color=yellow>そのまま手を離していて！</color>" },
                { Language.English, "Measuring... <size=120%>{0:F1}</size> sec\n<color=yellow>Keep hands off!</color>" }
            } },

            { "calib_ready", new Dictionary<Language, string> {
                { Language.Japanese, "準備完了！" },
                { Language.English, "Ready!" }
            } },
            { "ui_confirm_jp", new Dictionary<Language, string> {
                { Language.Japanese, "日本語でよろしいですか？" },
                { Language.English, "Start in Japanese?" } // 念のため英語訳も入れておく
            } },

            { "ui_confirm_en", new Dictionary<Language, string> {
                { Language.Japanese, "英語で開始しますか？" },
                { Language.English, "Start in English?" }
            } },

            { "calib_start_msg", new Dictionary<Language, string> {
                { Language.Japanese, "センサーの調整を行います" },
                { Language.English, "Calibrating Sensors..." }
            } },

            { "calib_prepare_format", new Dictionary<Language, string> {
                { Language.Japanese, "{0}\n<size=80%>計測開始まで {1:F0} 秒</size>" },
                { Language.English, "{0}\n<size=80%>Starts in {1:F0} sec</size>" }
            } },

            { "calib_intro_instruction", new Dictionary<Language, string> {
                { Language.Japanese, "画面の指示に従って\nつり革を操作してください" },
                { Language.English, "Please follow the instructions\nto operate the strap." }
            } },
        };
    }

    public string GetText(string key)
    {
        if (localizedText.ContainsKey(key))
        {
            return localizedText[key][CurrentLanguage];
        }
        return key;
    }

    /// <summary>
    /// 現在の言語に対応したフォントアセットを返す
    /// </summary>
    public TMP_FontAsset GetCurrentFont()
    {
        switch (CurrentLanguage)
        {
            case Language.English:
                return englishFont != null ? englishFont : japaneseFont; // 設定忘れ対策
            case Language.Japanese:
            default:
                return japaneseFont;
        }
    }

    public void SetLanguage(Language lang)
    {
        CurrentLanguage = lang;
        OnLanguageChanged?.Invoke();
    }

    public void ToggleLanguage()
    {
        if (CurrentLanguage == Language.Japanese) SetLanguage(Language.English);
        else SetLanguage(Language.Japanese);
    }
}