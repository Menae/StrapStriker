using UnityEngine;
using TMPro;

[RequireComponent(typeof(TextMeshProUGUI))]
public class LocalizeUI : MonoBehaviour
{
    [Tooltip("LocalizationManagerに登録したキー")]
    public string key;

    [Tooltip("フォントの変更を適用するか（数字だけのテキストなどはOFFでも良い）")]
    public bool applyFont = true;

    private TextMeshProUGUI textComponent;

    private void Start()
    {
        textComponent = GetComponent<TextMeshProUGUI>();

        UpdateContent();

        if (LocalizationManager.Instance != null)
        {
            LocalizationManager.Instance.OnLanguageChanged += UpdateContent;
        }
    }

    private void OnDestroy()
    {
        if (LocalizationManager.Instance != null)
        {
            LocalizationManager.Instance.OnLanguageChanged -= UpdateContent;
        }
    }

    // テキストとフォントを更新
    private void UpdateContent()
    {
        if (LocalizationManager.Instance != null && textComponent != null)
        {
            // 1. テキストの更新
            textComponent.text = LocalizationManager.Instance.GetText(key);

            // 2. フォントの更新（必要な場合のみ）
            if (applyFont)
            {
                textComponent.font = LocalizationManager.Instance.GetCurrentFont();
            }
        }
    }
}