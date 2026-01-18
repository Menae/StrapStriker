using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// プレイヤーの状態管理とつり革アクション全般を制御するコントローラー。
/// M5StickC Plus2（ArduinoInputManager）からの入力に基づき、スイング、空中再掴み、発射動作を制御する。
/// </summary>
public class PlayerController : MonoBehaviour
{
    /// <summary>
    /// プレイヤーの動作状態定義。
    /// </summary>
    private enum PlayerState { Idle, Grabbing, Swaying, Launched }

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

    [Header("■ スイング挙動 (物理演算)")]
    [Tooltip("スイングの最大角度制限（度数法）。アニメーションの正規化や直感操作モードの制限に使用。")]
    public float swayMaxAngle = 60f; // ★ここが不足していました
    [Tooltip("デバイスの傾きがスイングの力に変換される倍率")]
    public float swayForceByAngle = 20f;
    [Tooltip("デバイスを振る速さ(角速度)がパワー蓄積に与える影響度")]
    public float swayForceByVelocity = 0.1f;
    [Tooltip("パワーが蓄積する基本レート")]
    public float swayIncreaseRate = 10f;
    [Tooltip("パワーが自然減衰するレート")]
    public float swayDecayRate = 5f;
    [Tooltip("蓄積できるパワーの最大値")]
    public float maxSwayPower = 100f;
    [Tooltip("スイング成功と判定される回転速度の閾値")]
    public float swingVelocityThreshold = 15f;
    [Tooltip("蓄積パワーがスイングの見た目（トルク）に影響を与える倍率")]
    public float powerToSwingMultiplier = 0.1f;

    [Header("■ スイング角度認識範囲")]
    [Tooltip("左スイングと認識される最大角度")]
    public float leftSwingAngleMax = 90f;
    [Tooltip("左スイングと認識される最小角度")]
    public float leftSwingAngleMin = 150f;
    [Tooltip("右スイングと認識される最小角度")]
    public float rightSwingAngleMin = 210f;
    [Tooltip("右スイングと認識される最大角度")]
    public float rightSwingAngleMax = 270f;

    [Header("■ 発射・空中制御")]
    [Tooltip("パワーを発射速度に変換する倍率")]
    public float launchMultiplier = 50f;
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
    public bool useDirectControl = false;
    [Tooltip("操作の反転")]
    public bool invertDirectControl = true;
    [Tooltip("直感操作時の最大角度範囲")]
    public float directControlInputRange = 90f;
    [Tooltip("直感操作時の追従スムージング速度")]
    public float directControlSmoothSpeed = 5f;


    // ====================================================================
    // 内部状態変数
    // ====================================================================

    // コンポーネント参照
    private Rigidbody2D rb;
    private Animator playerAnim;
    private StageManager stageManager;
    private HingeJoint2D activeHingeJoint;

    // 状態管理
    private PlayerState currentState = PlayerState.Idle;
    private HangingStrap currentStrap = null;
    private float swayPower = 0f;
    private float lastFacingDirection = 1f; // 1=右, -1=左
    private float gameStartTime = -1f;

    // 入力制御・M5StickCデータ
    private float currentDeviceAngle = 0f;  // 加速度から算出した現在の傾き
    private bool wasGripInputActiveLastFrame = false;
    private float timeSinceGripLow = 0f;

    // キャリブレーション値 (SessionCalibrationから注入)
    // センサー1 (左手系)
    private float calibrationMin1 = 0f;
    private float calibrationMax1 = 1000f;
    // センサー2 (右手系)
    private float calibrationMin2 = 0f;
    private float calibrationMax2 = 1000f;

    private bool isCalibrated = false;

    // 定数・その他
    private const float MinLaunchPower = 1f;
    private Coroutine grabToSwayCoroutine;

    /// <summary>
    /// 初期化処理。コンポーネントの参照を取得する。
    /// Joy-Conの初期化処理を廃止し、ArduinoInputManager（M5StickC）への依存へ移行。
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

        // M5StickCはArduinoInputManagerが管理するため、ここでのデバイス検索処理は不要
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

        // ゲーム開始時刻の記録（入力遅延用）
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
            // 両方のセンサーが閾値を超えているかを判定
            bool active = (norm1 > gripNormalizedThreshold) && (norm2 > gripNormalizedThreshold);
            wasGripInputActiveLastFrame = Input.GetKey(KeyCode.Space) || active;
            return;
        }

        // --- 握力（接触）判定ロジック ---

        // 3. 閾値判定 (両方のセンサーが閾値を超えた場合のみ「掴んでいる」とみなす)
        bool isSensorActive = (norm1 > gripNormalizedThreshold) && (norm2 > gripNormalizedThreshold);

        // Spaceキー(デバッグ用) または センサー判定
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

        // 猶予期間内なら、信号が切れても「掴み状態」を維持する
        bool isGripInputActive = isRawGripSignalActive || (timeSinceGripLow < gripReleaseGracePeriod);

        // 入力の立ち上がり・立ち下がり検出
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
    /// 物理演算タイミングでの処理。スイング中の物理トルク計算、空中姿勢制御、着地処理を行う。
    /// 直感操作モードでない場合はパワーの自然減衰も適用される。
    /// </summary>
    void FixedUpdate()
    {
        if (currentState == PlayerState.Grabbing || currentState == PlayerState.Swaying)
        {
            ExecuteSwayingPhysics();
        }
        else if (currentState == PlayerState.Launched)
        {
            if (IsGrounded())
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
    }

    /// <summary>
    /// アニメーション更新処理。状態やスイング角度に応じてAnimatorパラメータを設定する。
    /// キャラクターの向きもここで更新される。
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

                // swayMaxAngle を使用して正規化
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
    /// このメソッドが呼ばれることで isCalibrated が true になり、操作が可能になる。
    /// </summary>
    /// <param name="min1">センサー1の最小値(OFF)</param>
    /// <param name="max1">センサー1の最大値(ON)</param>
    /// <param name="min2">センサー2の最小値(OFF)</param>
    /// <param name="max2">センサー2の最大値(ON)</param>
    public void SetCalibrationValues(float min1, float max1, float min2, float max2)
    {
        this.calibrationMin1 = min1;
        this.calibrationMax1 = max1;
        this.calibrationMin2 = min2;
        this.calibrationMax2 = max2;
        this.isCalibrated = true;

        // デバッグログで注入された値を確認
        Debug.Log($"<color=cyan>Calibration Applied:</color> Sensor1[{min1:F0}-{max1:F0}], Sensor2[{min2:F0}-{max2:F0}]");
    }

    /// <summary>
    /// 状態遷移を実行する。デバッグモード時は遷移ログを出力する。
    /// 同じ状態への遷移は無視される。
    /// </summary>
    /// <param name="newState">遷移先の状態</param>
    private void ChangeState(PlayerState newState)
    {
        if (currentState == newState) return;

        if (debugMode)
        {
            Debug.Log($"<color=cyan>State Change:</color> {currentState} -> <color=yellow>{newState}</color>");
        }

        currentState = newState;
    }

    /// <summary>
    /// キャラクターの向きを速度に基づいて更新する。
    /// つり革を掴んでいる間やLaunched状態では向きの更新を行わない。
    /// 一定以上の速度がある場合のみ向きを変更し、Animatorパラメータに反映する。
    /// </summary>
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
    /// スイング中の物理演算を実行する。
    /// M5StickCの加速度・ジャイロセンサーの値に基づき、キャラクターの回転とパワー蓄積を行う。
    /// </summary>
    private void ExecuteSwayingPhysics()
    {
        // Arduino（M5StickC）が未接続の場合は、デバッグモードでない限り処理しない
        if ((ArduinoInputManager.instance == null || !ArduinoInputManager.instance.IsConnected) && !debugMode) return;

        // --- 1. デバイスの傾き角度を計算 (加速度センサー利用) ---
        float inputAngle = 0f;

        if (debugMode && (ArduinoInputManager.instance == null || !ArduinoInputManager.instance.IsConnected))
        {
            // デバッグ用: キーボード入力 (-1.0 ~ 1.0) を角度に変換
            inputAngle = -Input.GetAxisRaw("Horizontal") * swayMaxAngle;
        }
        else
        {
            // 加速度(ax, ay)から角度を算出
            // M5StickCを縦持ち（ケーブル下）と仮定。重力方向とのアークタンジェントで角度を求める。
            Vector3 accel = ArduinoInputManager.RawAccel;

            // Atan2(y, x) で角度(ラジアン)を取得し、度数法に変換
            float calculatedAngle = Mathf.Atan2(accel.x, -accel.y) * Mathf.Rad2Deg;

            // ノイズ対策のため、極端に小さな変化は無視するか、ローパスフィルタをかけるのが望ましいが
            // ここでは簡易的に現在の値をそのまま採用
            inputAngle = calculatedAngle;
        }

        // ★修正点：メンバ変数へ代入（Warning解消 & 状態保持）
        currentDeviceAngle = inputAngle;

        // --- 2. 直感操作モード (Direct Control) ---
        if (useDirectControl)
        {
            // M5の傾きをそのままキャラクターの回転に反映
            float targetAngle = Mathf.Clamp(inputAngle, -swayMaxAngle, swayMaxAngle);

            // 操作反転オプション
            if (invertDirectControl)
            {
                targetAngle *= -1f;
            }

            // 現在の角度
            float currentZ = transform.eulerAngles.z;
            if (currentZ > 180f) currentZ -= 360f;

            // スムーズに回転させる
            float newZ = Mathf.Lerp(currentZ, targetAngle, Time.fixedDeltaTime * directControlSmoothSpeed);
            rb.MoveRotation(newZ);

            rb.angularVelocity = 0f;

            // 角度の深さに応じてパワーを決定
            float anglePowerRatio = Mathf.Abs(newZ) / swayMaxAngle;
            swayPower = anglePowerRatio * maxSwayPower;

            return;
        }

        // --- 3. 物理スイングモード (Physics Control) ---

        float inputTorque = 0f;
        float gyroVelocity = 0f;

        if (debugMode && (ArduinoInputManager.instance == null || !ArduinoInputManager.instance.IsConnected))
        {
            // デバッグ入力
            float horizontalInput = Input.GetAxisRaw("Horizontal");
            if (Mathf.Abs(horizontalInput) > 0.1f)
            {
                ChangeState(PlayerState.Swaying);
                // パワー加算
                float powerIncrease = swayIncreaseRate * Time.fixedDeltaTime;
                swayPower = Mathf.Min(maxSwayPower, swayPower + powerIncrease);

                inputTorque = horizontalInput * swayForceByAngle * (1f + swayPower * powerToSwingMultiplier);
            }
        }
        else
        {
            // ジャイロセンサーから回転速度(deg/sec)を取得
            gyroVelocity = ArduinoInputManager.RawGyro.z;

            // 角度による入力判定 (-1.0 ~ 1.0 に正規化)
            float normalizedForce = 0f;

            // inputAngle (現在の傾き) を使用して判定
            float clampedAngle = Mathf.Clamp(inputAngle, -90f, 90f);
            normalizedForce = clampedAngle / 90f; // -1(左) ~ 1(右)

            // 閾値を超えたらスイング状態へ
            if (Mathf.Abs(normalizedForce) > 0.1f)
            {
                ChangeState(PlayerState.Swaying);
                inputTorque = normalizedForce * swayForceByAngle * (1f + swayPower * powerToSwingMultiplier);
            }

            // タイミング判定: 「傾きの方向」と「ジャイロの回転方向」が一致している時＝加速中
            bool isAccelerating = (gyroVelocity > swingVelocityThreshold && normalizedForce > 0) ||
                                  (gyroVelocity < -swingVelocityThreshold && normalizedForce < 0);

            if (isAccelerating)
            {
                // 加速度と傾きの深さに応じてパワーを蓄積
                float powerIncrease = (Mathf.Abs(normalizedForce) + Mathf.Abs(gyroVelocity * 0.01f) * swayForceByVelocity)
                                    * swayIncreaseRate
                                    * Time.fixedDeltaTime;

                swayPower = Mathf.Min(maxSwayPower, swayPower + powerIncrease);
            }
        }

        // 慣性ボーナスの適用
        float inertiaTorque = 0f;
        if (StageManager.CurrentInertia.x != 0)
        {
            inertiaTorque = StageManager.CurrentInertia.x * inertiaSwingBonus;
        }

        // 最終的なトルク適用
        float totalTorque = inputTorque + inertiaTorque;

        if (Mathf.Abs(totalTorque) > 0.01f)
        {
            rb.AddTorque(totalTorque);
        }
    }

    /// <summary>
    /// Idle状態での移動処理（現在は未実装）。将来的な拡張用のプレースホルダー。
    /// </summary>
    private void ExecuteIdleMovement()
    {
    }

    /// <summary>
    /// 最も近いつり革を検索して掴む処理。
    /// 地上から掴む場合はGrabbing状態からSwaying状態へ遷移するコルーチンを開始し、
    /// 空中再掴みの場合は速度を減衰させて即座にSwaying状態へ移行する。
    /// HingeJoint2Dを生成してつり革との物理的な接続を確立する。
    /// </summary>
    private void GrabNearestStrap()
    {
        HangingStrap nearestStrap = HangingStrapManager.FindNearestStrap(transform.position, maxGrabDistance);
        if (nearestStrap != null)
        {
            Debug.Log("<color=green>成功: " + nearestStrap.name + " を掴みます！</color>");

            bool isPlayerGrounded = IsGrounded();
            if (!isPlayerGrounded)
            {
                rb.velocity *= aerialRecatchDampener;
                rb.angularVelocity = 0f;
            }
            currentStrap = nearestStrap;
            rb.constraints = RigidbodyConstraints2D.None;

            // ジョイント生成
            activeHingeJoint = gameObject.AddComponent<HingeJoint2D>();
            activeHingeJoint.connectedBody = nearestStrap.GetComponent<Rigidbody2D>();
            activeHingeJoint.autoConfigureConnectedAnchor = false;
            activeHingeJoint.anchor = transform.InverseTransformPoint(handPoint.position);
            activeHingeJoint.connectedAnchor = nearestStrap.grabPoint.localPosition;

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
    /// つり革を離す処理。状態に応じて発射・落下・Idle状態への遷移を制御する。
    /// Grabbing状態では単純に離してIdle状態へ移行し、
    /// Swaying状態では蓄積パワーに応じて発射するか落下するかを判定する。
    /// 発射時は速度方向への推力を加え、向きを確定させる。
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
                swayPower = 0f;
                break;

            case PlayerState.Swaying:
                if (swayPower < MinLaunchPower)
                {
                    if (!IsGrounded())
                    {
                        ChangeState(PlayerState.Launched);
                    }
                    else
                    {
                        ChangeState(PlayerState.Idle);
                    }

                    swayPower = 0f;
                    return;
                }

                Debug.Log($"<color=orange><b>発射！</b></color> 使用パワー: {swayPower.ToString("F1")}");
                ChangeState(PlayerState.Launched);
                rb.constraints = RigidbodyConstraints2D.None;

                rb.angularVelocity = 0f;

                Vector2 currentVelocity = rb.velocity.normalized;
                if (currentVelocity.sqrMagnitude < 0.1f) { currentVelocity = Vector2.up; }

                lastFacingDirection = Mathf.Sign(currentVelocity.x);
                if (Mathf.Abs(currentVelocity.x) < 0.01f)
                {
                }

                Vector2 launchBoost = currentVelocity * swayPower * launchMultiplier;
                rb.AddForce(launchBoost, ForceMode2D.Impulse);

                swayPower = 0f;
                break;
        }
    }

    /// <summary>
    /// Grabbing状態からSwaying状態への遷移を制御するコルーチン。
    /// 掴みアニメーションの再生時間分待機してから状態を切り替える。
    /// 待機中に離脱した場合は遷移をキャンセルする。
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
    /// NPCとの衝突時に呼び出される。
    /// スイング中の場合はSwayPowerを威力に加算してNPCに衝突威力を伝達する。
    /// </summary>
    /// <param name="other">衝突したNPCのCollider2D</param>
    public void HandleNpcCollision(Collider2D other)
    {
        if (other.gameObject.CompareTag("NPC"))
        {
            NPCController npc = other.gameObject.GetComponentInParent<NPCController>();

            if (npc != null)
            {
                Vector2 impactVelocity = rb.velocity;

                if (currentState == PlayerState.Swaying)
                {
                    Vector2 powerBonus = impactVelocity.normalized * swayPower * swayImpactPowerBonus;
                    impactVelocity += powerBonus;
                    Debug.Log($"<color=red>スイングインパクト！</color> パワーボーナス: {powerBonus.magnitude}");
                }

                npc.TakeImpact(impactVelocity, knockbackMultiplier);
            }
        }
    }

    /// <summary>
    /// 接地判定を行う。feetPositionから下方向へレイキャストを飛ばし、groundLayerに該当する地面を検出する。
    /// Sceneビューでレイを可視化し、ヒット時は緑、ミス時は赤で描画される。
    /// </summary>
    /// <returns>地面に接地している場合はtrue、空中の場合はfalse</returns>
    private bool IsGrounded()
    {
        RaycastHit2D hit = Physics2D.Raycast(feetPosition.position, Vector2.down, groundCheckDistance, groundLayer);

        Color rayColor = hit ? Color.green : Color.red;
        Debug.DrawRay(feetPosition.position, Vector2.down * groundCheckDistance, rayColor);

        if (hit.collider != null)
        {
        }
        else
        {
        }

        return hit.collider != null;
    }
}