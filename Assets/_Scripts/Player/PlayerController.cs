using System.Collections;
using UnityEngine;

/// <summary>
/// プレイヤーの状態管理とつり革アクション全般を制御するコントローラー。
/// M5StickC Plus2（ArduinoInputManager）からの入力に基づき、スイング、空中再掴み、発射動作を制御する。
/// インスペクタ上でデバイスの取り付け向きに応じた軸マッピング設定が可能。
/// </summary>
public class PlayerController : MonoBehaviour
{
    /// <summary>
    /// プレイヤーの動作状態定義。
    /// </summary>
    private enum PlayerState { Idle, Grabbing, Swaying, Launched }

    /// <summary>
    /// M5StickCのセンサー軸定義。物理的な取り付け向きに合わせてマッピングを変更するために使用。
    /// </summary>
    public enum M5Axis
    {
        PlusX,  // X軸 正方向
        MinusX, // X軸 負方向 (反転)
        PlusY,  // Y軸 正方向
        MinusY, // Y軸 負方向 (反転)
        PlusZ,  // Z軸 正方向
        MinusZ  // Z軸 負方向 (反転)
    }

    // ====================================================================
    // Inspector設定項目
    // ====================================================================

    [Header("■ 必須参照")]
    [Tooltip("プレイヤーの手の位置。つり革を掴む際の基点。")]
    public Transform handPoint;
    [Tooltip("接地判定レイキャストの発射地点。")]
    public Transform feetPosition;

    [Header("■ 入力・キャリブレーション設定")]
    [Tooltip("キャリブレーション範囲に対して、何%の信号強度で「掴んだ」と判定するか (0.5 = 50%)")]
    [Range(0.1f, 0.9f)]
    public float gripNormalizedThreshold = 0.5f;
    [Tooltip("握力が途切れても、ここで設定した秒数だけ「掴み」を維持する（チャタリング対策）")]
    public float gripReleaseGracePeriod = 0.1f;
    [Tooltip("ゲーム開始後、入力受付を開始するまでの待機時間（秒）")]
    public float inputDelayAfterStart = 0.5f;

    [Header("■ センサー補正")]
    [Tooltip("デバイスの取り付け角度のズレを補正する値。M5使用時に真下でログが270になるよう調整する。")]
    public float angleCalibrationOffset = 0f;

    [Header("■ デバイス軸マッピング設定")]
    [Tooltip("角度計算の「横成分(X)」として使用するM5StickCの加速度軸")]
    public M5Axis accelHorizontalAxis = M5Axis.PlusX;
    [Tooltip("角度計算の「縦成分(Y)」として使用するM5StickCの加速度軸 (重力方向)")]
    public M5Axis accelVerticalAxis = M5Axis.MinusY;
    [Tooltip("スイング速度として使用するM5StickCのジャイロ軸 (回転軸)")]
    public M5Axis gyroRotationAxis = M5Axis.PlusZ;

    [Header("■ スイング挙動 (段階式 DirectControl)")]
    [Tooltip("スイングの最大角度制限（度数法）。この角度を超えないようにクランプされる。")]
    public float swayMaxAngle = 60f;
    [Tooltip("直感操作時の追従スムージング速度")]
    public float directControlSmoothSpeed = 10f;

    [Header("▼ 勢いステージ設定")]
    [Tooltip("勢いの最大ステージ数")]
    public int maxSwingStages = 5;
    [Tooltip("発射（ジャンプ）が可能になる最低ステージ数。これ未満で離すとただ落下する。")]
    public int minStageToLaunch = 2;
    [Tooltip("1ステージあたりに加算される発射パワー")]
    public float powerPerStage = 20f;
    [Tooltip("「有効なスイング」と判定されるための最低角度振れ幅（中心からの角度）")]
    public float validSwingThresholdAngle = 15f;
    [Tooltip("「有効なスイング」と判定されるための最低通過速度（角速度）")]
    public float validSwingThresholdVelocity = 30f;
    [Tooltip("スイングを止めてからステージが減少し始めるまでの猶予時間（秒）")]
    public float stageDecayTime = 1.0f;

    [Header("■ 物理スイング設定 (旧モード用)")]
    [Tooltip("デバイスの傾きがスイングの力に変換される倍率")]
    public float swayForceByAngle = 20f;
    [Tooltip("デバイスを振る速さ(角速度)がパワー蓄積に与える影響度")]
    public float swayForceByVelocity = 0.1f;
    [Tooltip("パワーが蓄積する基本レート")]
    public float swayIncreaseRate = 10f;
    [Tooltip("蓄積できるパワーの最大値")]
    public float maxSwayPower = 100f;
    [Tooltip("スイング成功と判定される回転速度の閾値")]
    public float swingVelocityThreshold = 15f;
    [Tooltip("蓄積パワーがスイングの見た目（トルク）に影響を与える倍率")]
    public float powerToSwingMultiplier = 0.1f;
    [Tooltip("パワーが自然減衰するレート")]
    public float swayDecayRate = 5f;

    // ■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■
    // ここを変更しました：カーブを削除し、手動設定用の変数を追加
    // ■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■
    [Header("■ アニメーション調整 (手動設定)")]
    [Tooltip("SwayTimeを 0.0 にしたい時の角度（実行中にログを見て数値を入力してください）")]
    public float swayAngleAtZero = -60f;

    [Tooltip("SwayTimeを 1.0 にしたい時の角度（実行中にログを見て数値を入力してください）")]
    public float swayAngleAtOne = 60f;
    // ■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■

    [Header("■ 発射・空中制御")]
    [Tooltip("パワーを発射速度に変換する倍率")]
    public float launchMultiplier = 1.0f; // ステージ制になったため基準を1.0に変更推奨
    [Tooltip("慣性力が働いている方向にジャンプした際の飛距離ブースト強度")]
    public float inertiaJumpBoostStrength = 80f;
    [Tooltip("空中で体を直立に戻す回転速度")]
    public float aerialRotationSpeed = 5f;
    [Tooltip("空中再掴み時の速度減衰率 (0.5 = 50%に減速)")]
    [Range(0f, 1f)]
    public float aerialRecatchDampener = 0.5f;

    [Header("■ インタラクション・環境")]
    [Tooltip("つり革を掴める最大距離")]
    public float maxGrabDistance = 8.0f;
    [Tooltip("掴んでからスイング操作可能になるまでの遷移時間")]
    public float grabToSwayTransitionTime = 0.25f;
    [Tooltip("NPC衝突時のノックバック強度")]
    public float knockbackMultiplier = 5.0f;
    [Tooltip("スイング中の衝突時、SwayPowerを威力に上乗せする倍率")]
    public float swayImpactPowerBonus = 0.5f;
    [Tooltip("慣性力がアクションに与える影響倍率")]
    public float inertiaBonusMultiplier = 1.5f;
    [Tooltip("慣性がスイングトルクに与えるボーナス")]
    public float inertiaSwingBonus = 5f;

    [Header("▼ モバイルバッテリー設定")]
    [Tooltip("バッテリー爆発時のプレイヤーへの前方推進力")]
    public float batteryExplosionSelfForce = 20f;
    [Tooltip("爆発が他のNPCに影響する半径")]
    public float batteryExplosionRadius = 3.0f;
    [Tooltip("爆発に巻き込まれたNPCに与える衝撃倍率")]
    public float batteryExplosionImpactMultiplier = 5.0f;
    [Tooltip("爆発時のエフェクト（任意）")]
    public GameObject batteryExplosionEffect;

    [Header("■ 接地判定")]
    [Tooltip("地面レイヤー")]
    public LayerMask groundLayer;
    [Tooltip("接地判定距離")]
    public float groundCheckDistance = 0.1f;
    [Tooltip("着地時のブレーキ強度 (0=即停止, 1=滑る)")]
    [Range(0f, 1f)]
    public float groundBrakingFactor = 0.1f;

    [Header("■ デバッグ・テスト機能")]
    [SerializeField] private bool debugMode = false;
    [Tooltip("【直感操作モード】物理演算を無視し、デバイス角度とキャラ角度を同期させる")]
    public bool useDirectControl = true; // デフォルトtrue推奨
    [Tooltip("操作の反転")]
    public bool invertDirectControl = true;


    // ====================================================================
    // 内部状態変数
    // ====================================================================
    // (以下、変更なしのため省略しますが、コードに含める場合はそのまま記述してください)

    // コンポーネント参照
    private Rigidbody2D rb;
    private Animator playerAnim;
    private StageManager stageManager;
    private HingeJoint2D activeHingeJoint;

    // 状態管理
    private PlayerState currentState = PlayerState.Idle;
    private HangingStrap currentStrap = null;
    private float swayPower = 0f;
    private float lastFacingDirection = 1f;
    private float gameStartTime = -1f;

    // DirectControl / ステージ管理用変数
    private int currentSwingStage = 0;
    private float lastAngleDifference = 0f;
    private float maxAmplitudeInCurrentSwing = 0f;
    private float timeSinceLastValidSwing = 0f;

    // 入力制御・M5StickCデータ
    private float currentDeviceAngle = 0f;
    private bool wasGripInputActiveLastFrame = false;
    private float timeSinceGripLow = 0f;

    // キャリブレーション値 (SessionCalibrationから注入)
    private float calibrationMin1 = 0f;
    private float calibrationMax1 = 1000f;
    private float calibrationMin2 = 0f;
    private float calibrationMax2 = 1000f;

    private bool isCalibrated = false;

    // 定数・その他
    private const float MinLaunchPower = 1f;
    private Coroutine grabToSwayCoroutine;
    private bool hasBattery = false;
    private bool wasGroundedLastFrame = true;

    // 発射直後の接地判定を無効化するためのタイマー
    private float launchGraceTimer = 0f;

    /// <summary>
    /// 初期化処理。コンポーネントの参照を取得する。
    /// </summary>
    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        // 空中での意図しない回転を防ぐ
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        playerAnim = GetComponentInChildren<Animator>();
        if (playerAnim == null)
        {
            Debug.LogError("Playerの子オブジェクトにAnimatorが見つかりません！");
        }

        stageManager = FindObjectOfType<StageManager>();
        if (stageManager == null)
        {
            Debug.LogError("シーン内にStageManagerが見つかりません！");
        }
    }

    /// <summary>
    /// 毎フレームの更新処理。入力判定と状態遷移を行う。
    /// キャリブレーション未完了時は入力を無視する。
    /// </summary>
    void Update()
    {
        // キャリブレーションがまだ完了していない、またはゲーム中でない場合は処理しない
        if (!isCalibrated || (stageManager != null && stageManager.CurrentState != StageManager.GameState.Playing))
        {
            gameStartTime = -1f;
            return;
        }

        // ゲーム開始時刻の記録
        if (gameStartTime < 0f)
        {
            gameStartTime = Time.time;
        }

        // 現在のセンサー値を取得 (2系統)
        float currentGrip1 = (ArduinoInputManager.instance != null) ? ArduinoInputManager.instance.SmoothedGripValue1 : 0f;
        float currentGrip2 = (ArduinoInputManager.instance != null) ? ArduinoInputManager.instance.SmoothedGripValue2 : 0f;

        // 正規化 (0.0 ～ 1.0)
        float norm1 = Mathf.InverseLerp(calibrationMin1, calibrationMax1, currentGrip1);
        float norm2 = Mathf.InverseLerp(calibrationMin2, calibrationMax2, currentGrip2);

        // ゲーム開始直後の誤動作防止（入力遅延）
        if (Time.time < gameStartTime + inputDelayAfterStart)
        {
            // 遅延中も前フレームの状態としては更新しておく
            bool active = (norm1 > gripNormalizedThreshold) && (norm2 > gripNormalizedThreshold);
            wasGripInputActiveLastFrame = Input.GetKey(KeyCode.Space) || active;
            return;
        }

        // --- 握力（接触）判定ロジック ---

        bool isSensorActive = (norm1 > gripNormalizedThreshold) && (norm2 > gripNormalizedThreshold);
        bool isRawGripSignalActive = Input.GetKey(KeyCode.Space) || isSensorActive;

        // --- チャタリング（瞬間的な信号切れ）対策 ---

        if (isRawGripSignalActive)
        {
            timeSinceGripLow = 0f;
        }
        else
        {
            timeSinceGripLow += Time.deltaTime;
        }

        bool isGripInputActive = isRawGripSignalActive || (timeSinceGripLow < gripReleaseGracePeriod);
        bool gripPressed = isGripInputActive && !wasGripInputActiveLastFrame;
        bool gripReleased = !isGripInputActive && wasGripInputActiveLastFrame;

        // 状態に応じたアクション実行
        switch (currentState)
        {
            case PlayerState.Idle:
            case PlayerState.Launched:
                if (gripPressed)
                {
                    GrabNearestStrap();
                }
                break;

            case PlayerState.Grabbing:
            case PlayerState.Swaying:
                if (gripReleased)
                {
                    ReleaseStrap();
                }
                break;
        }

        wasGripInputActiveLastFrame = isGripInputActive;

        // 接地判定（Raycast可視化含む）
        IsGrounded();

        // --- デバッグ用コマンド ---
        if (Input.GetKeyDown(KeyCode.E)) playerAnim.SetTrigger("Jump");
        if (Input.GetKeyDown(KeyCode.R)) playerAnim.SetTrigger("Kick");

        if (debugMode)
        {
            if (Input.GetKey(KeyCode.LeftArrow)) rb.velocity = Vector2.left * 5f;
            if (Input.GetKey(KeyCode.RightArrow)) rb.velocity = Vector2.right * 5f;
        }
    }

    /// <summary>
    /// 物理演算タイミングでの処理。
    /// </summary>
    void FixedUpdate()
    {
        // 発射直後の猶予タイマー更新
        if (launchGraceTimer > 0f)
        {
            launchGraceTimer -= Time.fixedDeltaTime;
        }

        if (currentState == PlayerState.Grabbing || currentState == PlayerState.Swaying)
        {
            ExecuteSwayingPhysics();
        }
        else if (currentState == PlayerState.Launched)
        {
            // 猶予タイマーが0以下の場合のみ、接地判定を行う
            // これにより、爆発直後に即着地してしまうのを防ぐ
            if (launchGraceTimer <= 0f && IsGrounded())
            {
                ChangeState(PlayerState.Idle);
                rb.rotation = 0f;
                rb.velocity = new Vector2(rb.velocity.x * groundBrakingFactor, rb.velocity.y);
                rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            }
            else
            {
                Quaternion targetRotation = Quaternion.identity;
                Quaternion newRotation = Quaternion.Slerp(
                    rb.transform.rotation,
                    targetRotation,
                    aerialRotationSpeed * Time.fixedDeltaTime
                );
                rb.MoveRotation(newRotation);
            }
        }

        if (!useDirectControl)
        {
            float currentDecayRate = debugMode ? 0f : swayDecayRate;
            swayPower = Mathf.Max(0, swayPower - currentDecayRate * Time.fixedDeltaTime);
        }

        // --- バッテリー爆発と着地検出ロジック ---
        bool isGroundedNow = IsGrounded();

        // 着地判定はLaunchedかIdle時のみ有効にする
        if (currentState != PlayerState.Grabbing && currentState != PlayerState.Swaying)
        {
            if (!wasGroundedLastFrame && isGroundedNow)
            {
                if (hasBattery)
                {
                    ExplodeBattery();
                }
            }
        }

        wasGroundedLastFrame = isGroundedNow;
    }

    /// <summary>
    /// アニメーション更新処理。
    /// </summary>
    private void LateUpdate()
    {
        if (playerAnim != null)
        {
            playerAnim.SetInteger("State", (int)currentState);

            if (currentState == PlayerState.Swaying || currentState == PlayerState.Grabbing)
            {
                float currentAngle = transform.eulerAngles.z;
                if (currentAngle > 180f) currentAngle -= 360f;

                float normalizedTime = Mathf.InverseLerp(swayMaxAngle, -swayMaxAngle, currentAngle);

                if (lastFacingDirection > 0)
                {
                    normalizedTime = 1f - normalizedTime;
                }

                playerAnim.SetFloat("SwayTime", normalizedTime);
            }
        }

        HandleDirection();
    }

    /// <summary>
    /// 外部（SessionCalibrationクラス）からキャリブレーション結果を注入するためのメソッド。
    /// </summary>
    public void SetCalibrationValues(float min1, float max1, float min2, float max2)
    {
        this.calibrationMin1 = min1;
        this.calibrationMax1 = max1;
        this.calibrationMin2 = min2;
        this.calibrationMax2 = max2;
        this.isCalibrated = true;

        Debug.Log($"<color=cyan>Calibration Applied:</color> Sensor1[{min1:F0}-{max1:F0}], Sensor2[{min2:F0}-{max2:F0}]");
    }

    // --- State & Direction Helpers ---

    private void ChangeState(PlayerState newState)
    {
        if (currentState == newState) return;

        if (debugMode)
        {
            Debug.Log($"<color=cyan>State Change:</color> {currentState} -> <color=yellow>{newState}</color>");
        }

        currentState = newState;
    }

    private void HandleDirection()
    {
        if (currentStrap != null)
        {
            return;
        }

        if (currentState != PlayerState.Launched)
        {
            float horizontalVelocity = rb.velocity.x;

            if (Mathf.Abs(horizontalVelocity) > 0.1f)
            {
                lastFacingDirection = Mathf.Sign(horizontalVelocity);
            }
        }

        if (playerAnim != null)
        {
            playerAnim.SetFloat("Direction", lastFacingDirection);
        }
    }

    /// <summary>
    /// スイング中の演算を実行するメソッド。
    /// キーボード入力とM5入力を「真下=270度」基準で統一して処理する。
    /// </summary>
    private void ExecuteSwayingPhysics()
    {
        // デバイス未接続かつデバッグモードOFFなら何もしない
        bool isM5Connected = (ArduinoInputManager.instance != null && ArduinoInputManager.instance.IsConnected);
        if (!isM5Connected && !debugMode) return;

        // =================================================================
        // 1. デバイス角度(0～360度)の算出
        //    目標：つり革を真下に垂らした状態 = 270.0度 になるようにする
        // =================================================================

        float inputAngle = 270f; // デフォルトは真下

        // --- A. キーボード入力 (デバッグ用) ---
        if (debugMode && !isM5Connected)
        {
            // 右キー(正)入力で角度を減らす(時計回り)、左キー(負)で増やす(反時計回り)
            float input = Input.GetAxisRaw("Horizontal");
            inputAngle = 270f - (input * swayMaxAngle);
        }
        // --- B. M5StickC入力 (本番用) ---
        else if (isM5Connected)
        {
            Vector3 rawAccel = ArduinoInputManager.RawAccel;
            float accelX = GetAxisValue(accelHorizontalAxis, rawAccel);
            float accelY = GetAxisValue(accelVerticalAxis, rawAccel);

            // Atan2で角度算出 (右=0, 上=90, 左=180, 下=-90)
            float rawAngle = Mathf.Atan2(accelY, accelX) * Mathf.Rad2Deg;

            // 0～360度に正規化
            if (rawAngle < 0f) rawAngle += 360f;

            // 補正値を適用
            inputAngle = rawAngle + angleCalibrationOffset;
        }

        // 最終的な正規化 (0～360)
        inputAngle = Mathf.Repeat(inputAngle, 360f);
        currentDeviceAngle = inputAngle;

        //★デバッグ用：このログが、キーボード操作なし/M5真下持ち の時に「270.0」になるのが正解
        Debug.Log($"CurrentAngle: {currentDeviceAngle:F1} (Target: 270.0)");

        // =================================================================
        // 2. 基準角度（270度）からの変位量を計算
        //    DeltaAngleを使うことで、359度と1度の境目なども正しく計算できる
        // =================================================================

        float angleDifference = Mathf.DeltaAngle(270f, currentDeviceAngle);

        if (invertDirectControl)
        {
            angleDifference *= -1f;
        }

        // =================================================================
        // 3. 姿勢制御と勢い計算 (共通ロジック)
        // =================================================================
        if (useDirectControl)
        {
            // --- 姿勢制御 ---
            // 角度制限 (SwayMaxAngle)
            float clampedDifference = Mathf.Clamp(angleDifference, -swayMaxAngle, swayMaxAngle);

            // ターゲット角度を決定 (270度基準)
            float targetAngle = 270f + clampedDifference;

            // Rigidbodyを回転させる
            float currentZ = transform.eulerAngles.z;
            float newZ = Mathf.LerpAngle(currentZ, targetAngle, Time.fixedDeltaTime * directControlSmoothSpeed);
            rb.MoveRotation(newZ);
            rb.angularVelocity = 0f;

            // --- 勢い（ステージ）判定 ---
            // キーボード入力時は常に最大ステージとする（デバッグのしやすさ重視）
            bool isKeyInputActive = (debugMode && !isM5Connected && Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.1f);

            if (isKeyInputActive)
            {
                currentSwingStage = maxSwingStages;
                timeSinceLastValidSwing = 0f;
            }
            else
            {
                // M5入力時のロジック
                if (Mathf.Abs(clampedDifference) > maxAmplitudeInCurrentSwing)
                {
                    maxAmplitudeInCurrentSwing = Mathf.Abs(clampedDifference);
                }

                // 中心（差分0）をまたいだかどうか
                bool crossedCenter = (Mathf.Sign(lastAngleDifference) != Mathf.Sign(clampedDifference));

                float currentAngularVelocity = 0f;
                if (isM5Connected)
                {
                    currentAngularVelocity = Mathf.Abs(GetAxisValue(gyroRotationAxis, ArduinoInputManager.RawGyro));
                }

                if (crossedCenter)
                {
                    bool isValidSwing = (maxAmplitudeInCurrentSwing >= validSwingThresholdAngle) &&
                                        (currentAngularVelocity >= validSwingThresholdVelocity);

                    if (isValidSwing)
                    {
                        if (currentSwingStage < maxSwingStages)
                        {
                            currentSwingStage++;
                            Debug.Log($"<color=cyan>Swing Stage UP!</color> Lv.{currentSwingStage}");
                        }
                        timeSinceLastValidSwing = 0f;
                    }
                    maxAmplitudeInCurrentSwing = 0f;
                }
            }

            // 減衰処理
            if (!isKeyInputActive)
            {
                timeSinceLastValidSwing += Time.deltaTime;
                if (timeSinceLastValidSwing > stageDecayTime && currentSwingStage > 0)
                {
                    currentSwingStage--;
                    timeSinceLastValidSwing = 0f;
                    Debug.Log($"<color=orange>Swing Stage Decay...</color> Lv.{currentSwingStage}");
                }
            }

            // ステージをSwayPowerに反映
            float stageRatio = (float)currentSwingStage / (float)maxSwingStages;
            swayPower = stageRatio * maxSwayPower;

            lastAngleDifference = clampedDifference;
            return;
        }

        // =================================================================
        // 4. PhysicsControl（旧物理スイングモード）
        // =================================================================

        float inputTorque = 0f;
        float gyroVelocity = 0f;

        if (debugMode && (ArduinoInputManager.instance == null || !ArduinoInputManager.instance.IsConnected))
        {
            float horizontalInput = Input.GetAxisRaw("Horizontal");
            if (Mathf.Abs(horizontalInput) > 0.1f)
            {
                ChangeState(PlayerState.Swaying);
                inputTorque = horizontalInput * swayForceByAngle;

                // キー入力中はパワーを加算する
                swayPower = Mathf.Min(maxSwayPower, swayPower + swayIncreaseRate * Time.fixedDeltaTime);
            }
            else
            {
                // 入力がないときは減衰
                swayPower = Mathf.Max(0, swayPower - swayDecayRate * Time.fixedDeltaTime);
            }
        }
        else
        {
            gyroVelocity = GetAxisValue(gyroRotationAxis, ArduinoInputManager.RawGyro);
            float normalizedForce = Mathf.Clamp(angleDifference, -90f, 90f) / 90f;

            if (Mathf.Abs(normalizedForce) > 0.1f)
            {
                ChangeState(PlayerState.Swaying);
                inputTorque = normalizedForce * swayForceByAngle * (1f + swayPower * powerToSwingMultiplier);
            }

            bool isAccelerating = (gyroVelocity > swingVelocityThreshold && normalizedForce > 0) ||
                                  (gyroVelocity < -swingVelocityThreshold && normalizedForce < 0);
            if (isAccelerating)
            {
                swayPower = Mathf.Min(maxSwayPower, swayPower + swayIncreaseRate * Time.fixedDeltaTime);
            }
            else
            {
                swayPower = Mathf.Max(0, swayPower - swayDecayRate * Time.fixedDeltaTime);
            }
        }

        float inertiaTorque = 0f;
        if (StageManager.CurrentInertia.x != 0)
        {
            inertiaTorque = StageManager.CurrentInertia.x * inertiaSwingBonus;
        }

        float totalTorque = inputTorque + inertiaTorque;
        if (Mathf.Abs(totalTorque) > 0.01f)
        {
            rb.AddTorque(totalTorque);
        }
    }

    /// <summary>
    /// 設定されたM5Axis列挙型に基づいて、Vector3のソースから特定の値を取得・符号反転するヘルパーメソッド。
    /// </summary>
    private float GetAxisValue(M5Axis axis, Vector3 source)
    {
        switch (axis)
        {
            case M5Axis.PlusX: return source.x;
            case M5Axis.MinusX: return -source.x;
            case M5Axis.PlusY: return source.y;
            case M5Axis.MinusY: return -source.y;
            case M5Axis.PlusZ: return source.z;
            case M5Axis.MinusZ: return -source.z;
            default: return 0f;
        }
    }

    // --- Action Methods ---

    /// <summary>
    /// 最も近いつり革を検索して掴む処理。
    /// つり革を掴むと同時に、StageManagerから現在の状況（混雑率や撃破数）に応じた
    /// 「開始ステージ（勢いボーナス）」を取得して適用する。
    /// </summary>
    private void GrabNearestStrap()
    {
        HangingStrap nearestStrap = HangingStrapManager.FindNearestStrap(transform.position, maxGrabDistance);
        if (nearestStrap != null)
        {
            bool isPlayerGrounded = IsGrounded();
            if (!isPlayerGrounded)
            {
                rb.velocity *= aerialRecatchDampener;
                rb.angularVelocity = 0f;
            }
            currentStrap = nearestStrap;
            rb.constraints = RigidbodyConstraints2D.None;

            activeHingeJoint = gameObject.AddComponent<HingeJoint2D>();
            activeHingeJoint.connectedBody = nearestStrap.GetComponent<Rigidbody2D>();
            activeHingeJoint.autoConfigureConnectedAnchor = false;
            activeHingeJoint.anchor = transform.InverseTransformPoint(handPoint.position);
            activeHingeJoint.connectedAnchor = nearestStrap.grabPoint.localPosition;

            // --- 開始ステージ（勢いボーナス）の適用 ---
            if (stageManager != null)
            {
                int startStage;

                // オーバーロード状態（限界突破中）は強力な初期推進力を付与する
                if (stageManager.IsOverloaded)
                {
                    startStage = 4;
                }
                else
                {
                    // 通常時は混雑率や撃破数に基づくボーナス計算を行う
                    startStage = stageManager.GetCalculatedStartStage();
                }

                // 最大ステージ数を超えない範囲で適用
                currentSwingStage = Mathf.Clamp(startStage, 0, maxSwingStages);

                // 視覚的フィードバック（SwayPower）にも即座に反映
                float stageRatio = (float)currentSwingStage / (float)maxSwingStages;
                swayPower = stageRatio * maxSwayPower;

                if (currentSwingStage > 0)
                {
                    Debug.Log($"<color=cyan>Bonus Stage Applied:</color> Lv.{currentSwingStage}");
                }
            }

            if (grabToSwayCoroutine != null) StopCoroutine(grabToSwayCoroutine);

            if (isPlayerGrounded)
            {
                ChangeState(PlayerState.Grabbing);
                grabToSwayCoroutine = StartCoroutine(TransitionToSwayingRoutine());
            }
            else
            {
                ChangeState(PlayerState.Swaying);
            }
        }
        else
        {
            Debug.Log("<color=red>失敗: 掴める範囲 (" + maxGrabDistance + "m) につり革がありません。</color>");
        }
    }

    /// <summary>
    /// つり革を離す処理。
    /// ステージ制に基づき、勢いが足りない場合は落下、十分な場合は発射を実行する。
    /// DirectControl OFF時は、旧来のSwayPower判定を使用する。
    /// </summary>
    private void ReleaseStrap()
    {
        if (grabToSwayCoroutine != null)
        {
            StopCoroutine(grabToSwayCoroutine);
            grabToSwayCoroutine = null;
        }

        Destroy(activeHingeJoint);
        activeHingeJoint = null;
        currentStrap = null;

        switch (currentState)
        {
            case PlayerState.Grabbing:
                ChangeState(PlayerState.Idle);
                currentSwingStage = 0;
                swayPower = 0f;
                break;

            case PlayerState.Swaying:

                // --- 1. 勢い不足の判定 ---
                bool isPowerInsufficient = false;

                if (useDirectControl)
                {
                    // DirectControlモード: ステージ数で判定
                    if (currentSwingStage < minStageToLaunch)
                    {
                        isPowerInsufficient = true;
                    }
                }
                else
                {
                    // 旧物理モード: パワー値で判定
                    // MinLaunchPower は const float MinLaunchPower = 1f; (内部変数)
                    if (swayPower < MinLaunchPower)
                    {
                        isPowerInsufficient = true;
                    }
                }

                if (isPowerInsufficient)
                {
                    if (IsGrounded())
                    {
                        ChangeState(PlayerState.Idle);
                    }
                    else
                    {
                        ChangeState(PlayerState.Launched);
                        rb.angularVelocity = 0f;
                        rb.velocity *= 0.5f;
                    }

                    Debug.Log($"<color=orange>Launch Failed...</color> Insufficient Power.");
                    currentSwingStage = 0;
                    swayPower = 0f;
                    return;
                }

                // --- 2. 発射処理 ---
                Debug.Log($"<color=green><b>Launch!</b></color>");
                ChangeState(PlayerState.Launched);
                rb.constraints = RigidbodyConstraints2D.None;
                rb.angularVelocity = 0f;

                Vector2 currentVelocity = rb.velocity.normalized;
                if (currentVelocity.sqrMagnitude < 0.1f) { currentVelocity = Vector2.up; }
                lastFacingDirection = Mathf.Sign(currentVelocity.x);


                // --- 慣性ジャンプブースト判定 ---
                Vector2 inertia = StageManager.CurrentInertia;
                bool isBoostApplied = false;

                if (inertia.sqrMagnitude > 0.1f)
                {
                    float dot = Vector2.Dot(currentVelocity, inertia.normalized);
                    if (dot > 0.5f)
                    {
                        isBoostApplied = true;
                    }
                }

                if (isBoostApplied)
                {
                    // 慣性ブースト適用
                    Vector2 boostForce = currentVelocity * inertiaJumpBoostStrength;
                    rb.AddForce(boostForce, ForceMode2D.Impulse);
                    Debug.Log($"<color=cyan>Inertia Boost applied!</color>");
                }
                else
                {
                    // 通常発射
                    float launchForce = 0f;

                    if (useDirectControl)
                    {
                        // DirectControl: ステージ数ベース
                        launchForce = currentSwingStage * powerPerStage * launchMultiplier;
                    }
                    else
                    {
                        // 旧物理モード: パワー値ベース
                        launchForce = swayPower * launchMultiplier;
                    }

                    Vector2 launchVector = currentVelocity * launchForce;
                    rb.AddForce(launchVector, ForceMode2D.Impulse);
                }

                currentSwingStage = 0;
                swayPower = 0f;
                maxAmplitudeInCurrentSwing = 0f;
                break;
        }
    }

    /// <summary>
    /// Grabbing状態からSwaying状態への遷移を制御するコルーチン。
    /// </summary>
    private IEnumerator TransitionToSwayingRoutine()
    {
        yield return new WaitForSeconds(grabToSwayTransitionTime);

        if (currentState == PlayerState.Grabbing)
        {
            ChangeState(PlayerState.Swaying);
        }
        grabToSwayCoroutine = null;
    }

    /// <summary>
    /// NPCとの衝突時に呼び出される処理。
    /// 自身の速度とスイングパワーを物理的な衝撃力に変換し、NPCへ伝達する。
    /// </summary>
    public void HandleNpcCollision(Collider2D other)
    {
        // Tag比較は残すが、実務的にはLayerMatrixでの制御が望ましい
        if (other.gameObject.CompareTag("NPC"))
        {
            NPCController npc = other.gameObject.GetComponentInParent<NPCController>();

            if (npc != null)
            {
                // 現在の物理速度をベースにする
                Vector2 currentVelocity = rb.velocity;

                // スイング中（攻撃判定中）なら、蓄積されたSwayPowerを速度ベクトルに上乗せする
                if (currentState == PlayerState.Swaying)
                {
                    Vector2 powerBonus = currentVelocity.normalized * swayPower * swayImpactPowerBonus;
                    currentVelocity += powerBonus;
                    //Debug.Log($"<color=red>Swing Impact!</color> PowerBonus: {powerBonus.magnitude:F1}");
                }

                // 1. 速度(m/s) × 倍率(Mass想定) = 衝撃力(Impulse) をここで確定
                Vector2 finalImpactForce = currentVelocity * knockbackMultiplier;

                // 2. 衝撃力と加害者を渡す
                //    これによりNPC側は「誰に殴られたか」を知り、カウンター等の判定を行える
                npc.TakeImpact(finalImpactForce, this.gameObject);
            }
        }
    }

    private bool isExploding = false;

    /// <summary>
    /// モバイルバッテリーを装備する。
    /// BatteryStudentControllerから呼び出される。
    /// </summary>
    public void EquipBattery()
    {
        // すでに爆発処理中、あるいはすでにバッテリーを持っている場合は重複処理しない
        if (isExploding || hasBattery) return;

        hasBattery = true;

        Debug.Log("<color=yellow>Battery Equipped!</color>");
    }

    /// <summary>
    /// 着地時のバッテリー爆発処理。
    /// 周囲のNPCを吹き飛ばし、自身を前方に加速させる。
    /// </summary>
    private void ExplodeBattery()
    {
        if (isExploding) return; // 二重発火防止
        isExploding = true;

        try
        {
            hasBattery = false;
            Debug.Log("<color=red>Battery Explosion!</color>");

            // 範囲内のNPCを取得
            Collider2D[] hitColliders = Physics2D.OverlapCircleAll(transform.position, batteryExplosionRadius);

            foreach (var hitCollider in hitColliders)
            {
                if (hitCollider.gameObject == this.gameObject) continue;

                if (hitCollider.TryGetComponent<NPCController>(out var npc))
                {
                    Vector2 direction = (npc.transform.position - transform.position).normalized;
                    direction += Vector2.up * 0.5f;
                    Vector2 explosionForce = direction.normalized * 10f * batteryExplosionImpactMultiplier;

                    npc.TakeImpact(explosionForce, this.gameObject);
                }
            }

            // プレイヤーの挙動設定
            float boostDirectionX = (lastFacingDirection != 0) ? lastFacingDirection : 1f;
            Vector2 boostDir = new Vector2(boostDirectionX, 0.5f).normalized;
            rb.AddForce(boostDir * batteryExplosionSelfForce, ForceMode2D.Impulse);

            launchGraceTimer = 0.2f;
            if (batteryExplosionEffect != null) Instantiate(batteryExplosionEffect, transform.position, Quaternion.identity);
            ChangeState(PlayerState.Launched);
        }
        finally
        {
            isExploding = false; // 必ず最後にフラグを下ろす
        }
    }

    /// <summary>
    /// 接地判定を行う。
    /// </summary>
    private bool IsGrounded()
    {
        RaycastHit2D hit = Physics2D.Raycast(feetPosition.position, Vector2.down, groundCheckDistance, groundLayer);

        Color rayColor = hit ? Color.green : Color.red;
        Debug.DrawRay(feetPosition.position, Vector2.down * groundCheckDistance, rayColor);

        return hit.collider != null;
    }
}