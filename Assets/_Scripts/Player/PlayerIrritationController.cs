using UnityEngine;

/// <summary>
/// StageManagerの混雑率を監視し、単一のAnimatorにレベル通知を送るコントローラー。
/// </summary>
public class PlayerIrritationController : MonoBehaviour
{
    [Header("■ 参照設定")]
    [SerializeField] private StageManager stageManager;

    [Header("■ アニメーション制御")]
    [SerializeField, Tooltip("エフェクト再生用のAnimator（Playerの子オブジェクトに配置）")]
    private Animator effectsAnimator;

    [Header("■ 段階しきい値設定")]
    [SerializeField] private float thresholdLv1 = 100f;
    [SerializeField] private float thresholdLv2 = 150f;
    [SerializeField] private float thresholdLv3 = 200f;
    [SerializeField] private float thresholdLv4 = 250f;

    // パラメータのIDをハッシュ化して高速化
    private static readonly int IrritationLevelParamID = Animator.StringToHash("IrritationLevel");

    private int lastLevel = -1;

    private void Start()
    {
        if (stageManager == null) stageManager = FindObjectOfType<StageManager>();

        // 初期化
        UpdateEffectAnimator(0);
    }

    private void Update()
    {
        if (stageManager == null || effectsAnimator == null) return;

        // 現在のレベル算出
        int currentLevel = CalculateIrritationLevel(stageManager.CurrentCongestionRate);

        // 変化があった時だけAnimatorに通知
        if (currentLevel != lastLevel)
        {
            UpdateEffectAnimator(currentLevel);
            lastLevel = currentLevel;
        }
    }

    private int CalculateIrritationLevel(float rate)
    {
        if (rate >= thresholdLv4) return 4;
        if (rate >= thresholdLv3) return 3;
        if (rate >= thresholdLv2) return 2;
        if (rate >= thresholdLv1) return 1;
        return 0; // なし
    }

    private void UpdateEffectAnimator(int level)
    {
        // Animatorに整数を送るだけ（あとはAnimator側で遷移してもらう）
        effectsAnimator.SetInteger(IrritationLevelParamID, level);

        if (level > 0)
        {
            Debug.Log($"<color=red>イライラ度:</color> Lv.{level}");
        }
    }
}