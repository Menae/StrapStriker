using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// プレイヤーの状態管理とつり革アクション全般を制御するコントローラー。
/// Joy-Con入力によるスイング、空中再掴み、発射、NPC衝突判定などを一元管理する。
/// </summary>
public class PlayerController : MonoBehaviour
{
    /// <summary>
    /// プレイヤーが取りうる状態を定義する列挙型。
    /// 状態遷移によってスイング可否や物理挙動が切り替わる。
    /// </summary>
    private enum PlayerState { Idle, Grabbing, Swaying, Launched }

    [Header("必須コンポーネント")]
    [Tooltip("プレイヤーの手の位置。つり革を掴む際の基点となります。")]
    public Transform handPoint;

    [Header("入力設定")]
    [Tooltip("キャリブレーションされた範囲に対して、何%の信号強度で「掴んだ」とみなすか (0.5 = 50%)")]
    [Range(0.1f, 0.9f)]
    public float gripNormalizedThreshold = 0.5f;

    [Header("テスト機能: 直感操作モード")]
    [Tooltip("ONにすると物理挙動を無視してJoy-Conの角度とキャラクターの角度を完全同期させる")]
    public bool useDirectControl = false;
    [Tooltip("操作が逆になる場合はチェックを入れる")]
    public bool invertDirectControl = true;
    [Tooltip("Joy-Conを何度傾けたらMAXとみなすか（値を大きくすると動きがマイルドになる）")]
    public float directControlInputRange = 90f;
    [Tooltip("追従速度（値を小さくすると重みのあるゆっくりした動きになります）")]
    public float directControlSmoothSpeed = 5f;

    [Header("アニメーション制御")]
    [Tooltip("アニメーションの最大角度（片側）")]
    public float swayMaxAngle = 60f;

    [Header("スイングアクション設定")]
    [Tooltip("Joy-Conの傾き角度がスイングの力に与える影響の大きさ")]
    public float swayForceByAngle = 20f;
    [Tooltip("Joy-Conを振る速さがパワーの蓄積に与える影響の大きさ")]
    public float swayForceByVelocity = 0.1f;
    [Tooltip("パワーが蓄積する基本レート")]
    public float swayIncreaseRate = 10f;
    [Tooltip("パワーが自然に減少していくレート")]
    public float swayDecayRate = 5f;
    [Tooltip("蓄積できるパワーの最大値")]
    public float maxSwayPower = 100f;
    [Tooltip("溜まったパワーがスイングの見た目の大きさに影響を与える度合い")]
    public float powerToSwingMultiplier = 0.1f;
    [Tooltip("スイング成功と判定されるJoy-Conの回転速度のしきい値")]
    public float swingVelocityThreshold = 15f;

    [Header("スイング角度設定")]
    [Tooltip("左スイングと認識される最大角度")]
    public float leftSwingAngleMax = 90f;
    [Tooltip("左スイングと認識される最小角度")]
    public float leftSwingAngleMin = 150f;
    [Tooltip("右スイングと認識される最小角度")]
    public float rightSwingAngleMin = 210f;
    [Tooltip("右スイングと認識される最大角度")]
    public float rightSwingAngleMax = 270f;

    [Header("発射と空中制御")]
    [Tooltip("パワーを発射の力に変換する際の倍率")]
    public float launchMultiplier = 50f;
    [Tooltip("空中で体を直立に戻す回転の速さ")]
    public float aerialRotationSpeed = 5f;
    [Tooltip("空中で再掴みした際に、既存の速度をどのくらい減衰させるか (0.5 = 50%に)")]
    [Range(0f, 1f)]
    public float aerialRecatchDampener = 0.5f;

    [Header("インタラクション設定")]
    [Tooltip("NPCに衝突した際のノックバックの強さ")]
    public float knockbackMultiplier = 5.0f;
    [Tooltip("つり革を掴める最大距離")]
    public float maxGrabDistance = 8.0f;
    [Tooltip("掴んでからスイング可能になるまでのアニメーション時間")]
    public float grabToSwayTransitionTime = 0.25f;
    [Tooltip("スイング中の衝突時に、Sway Powerを威力に上乗せする際の倍率")]
    public float swayImpactPowerBonus = 0.5f;

    [Header("接地設定")]
    [Tooltip("接地判定レイキャストの発射地点")]
    public Transform feetPosition;
    [Tooltip("地面とみなすレイヤー")]
    public LayerMask groundLayer;
    [Tooltip("地面を検知するレイキャストの距離")]
    public float groundCheckDistance = 0.1f;
    [Tooltip("着地時のブレーキの強さ。0で即停止、1でブレーキなし。")]
    [Range(0f, 1f)]
    public float groundBrakingFactor = 0.1f;

    [Header("慣性設定")]
    [Tooltip("慣性力が発射に与えるボーナスの大きさ")]
    public float inertiaBonusMultiplier = 1.5f;
    [Tooltip("慣性がスイングに与える影響の大きさ")]
    public float inertiaSwingBonus = 5f;

    [Header("入力の安定化")]
    [Tooltip("握力が0になっても、ここで設定した秒数だけ掴み続ける（センサーのノイズ対策）")]
    public float gripReleaseGracePeriod = 0.1f;

    [Header("デバッグ設定")]
    [SerializeField] private bool debugMode = false;

    // --- キャリブレーション用内部変数 ---
    /// <summary>
    /// 離している時のセンサー値（最小値/ベースライン）。
    /// </summary>
    private float calibrationMin = 0f;

    /// <summary>
    /// 握っている時のセンサー値（最大値/アクティブ）。
    /// </summary>
    private float calibrationMax = 1000f;

    /// <summary>
    /// キャリブレーション値が設定済みかどうか。
    /// これがfalseの間は入力を受け付けません。
    /// </summary>
    private bool isCalibrated = false;

    /// <summary>
    /// 発射に最低限必要なパワー値。この値未満では発射処理が行われない。
    /// </summary>
    private const float MinLaunchPower = 1f;

    /// <summary>
    /// ゲームがPlaying状態になった時刻を記録する変数。入力遅延の起点となる。
    /// </summary>
    private float gameStartTime = -1f;

    [Tooltip("ゲーム開始後、入力受付を開始するまでの無効時間（秒）")]
    public float inputDelayAfterStart = 0.5f;

    private Rigidbody2D rb;
    private Animator playerAnim;
    private StageManager stageManager;
    private HingeJoint2D activeHingeJoint;
    private List<Joycon> joycons;
    private Joycon joycon;

    /// <summary>
    /// プレイヤーの現在の状態。Idle、Grabbing、Swaying、Launchedのいずれかを保持する。
    /// </summary>
    private PlayerState currentState = PlayerState.Idle;

    /// <summary>
    /// スイングによって蓄積されるパワー値。発射時の力や衝突時の威力に影響する。
    /// </summary>
    private float swayPower = 0f;

    /// <summary>
    /// 現在掴んでいるつり革への参照。nullの場合は未掴み状態。
    /// </summary>
    private HangingStrap currentStrap = null;

    /// <summary>
    /// 最後に向いていた方向。1=右、-1=左。アニメーション制御とキャラクターの向きに使用される。
    /// </summary>
    private float lastFacingDirection = 1f;

    /// <summary>
    /// 前フレームでの入力状態。入力の立ち上がり・立ち下がり検出に使用する。
    /// </summary>
    private bool wasGripInputActiveLastFrame = false;

    /// <summary>
    /// Joy-Conの前回のヨー角。角速度の計算に使用される。
    /// </summary>
    private float lastYaw = 0f;

    /// <summary>
    /// 握力が弱い状態が続いている時間。猶予期間内なら掴み状態を維持する。
    /// </summary>
    private float timeSinceGripLow = 0f;

    /// <summary>
    /// GrabbingからSwayingへの移行を制御するコルーチン。掴みモーション中の離脱処理に使用される。
    /// </summary>
    private Coroutine grabToSwayCoroutine;

    /// <summary>
    /// 初期化処理。コンポーネントの参照を取得する。
    /// 以前のPlayerPrefs読み込み処理は削除し、SessionCalibrationからの注入を待つ設計に変更。
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

        // JoyconManagerの初期化タイミングによってはnullの可能性があるためチェック
        if (JoyconManager.instance != null)
        {
            joycons = JoyconManager.instance.j;
            if (joycons != null && joycons.Count > 0)
            {
                joycon = joycons[0];
            }
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

        // ゲーム開始時刻の記録（入力遅延用）
        if (gameStartTime < 0f)
        {
            gameStartTime = Time.time;
        }

        // ゲーム開始直後の誤動作防止（入力遅延）
        if (Time.time < gameStartTime + inputDelayAfterStart)
        {
            // 遅延中も前フレームの状態としては更新しておく
            float current = (ArduinoInputManager.instance != null) ? ArduinoInputManager.instance.SmoothedGripValue : 0f;
            float norm = Mathf.InverseLerp(calibrationMin, calibrationMax, current);
            wasGripInputActiveLastFrame = Input.GetKey(KeyCode.Space) || (norm > gripNormalizedThreshold);
            return;
        }

        // --- 握力（接触）判定ロジック ---

        // 1. ArduinoInputManagerから平滑化された現在値を取得
        float currentGrip = (ArduinoInputManager.instance != null) ? ArduinoInputManager.instance.SmoothedGripValue : 0f;

        // 2. キャリブレーション範囲に基づいて 0.0～1.0 に正規化
        // InverseLerpは min > max の場合でも適切に補間比率を計算してくれる
        float normalizedGrip = Mathf.InverseLerp(calibrationMin, calibrationMax, currentGrip);

        // 3. 閾値判定
        bool isSensorActive = normalizedGrip > gripNormalizedThreshold;

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
    /// <param name="minGrip">離している状態（OFF）の計測値</param>
    /// <param name="maxGrip">握っている状態（ON）の計測値</param>
    public void SetCalibrationValues(float minGrip, float maxGrip)
    {
        this.calibrationMin = minGrip;
        this.calibrationMax = maxGrip;
        this.isCalibrated = true;

        // デバッグログで注入された値を確認
        Debug.Log($"<color=cyan>Calibration Applied:</color> OFF(Min)={minGrip:F1}, ON(Max)={maxGrip:F1}");
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
    /// 直感操作モードではJoy-Con角度に応じた直接的な回転制御を行い、
    /// 通常モードではトルクベースの物理スイングと慣性効果を適用する。
    /// </summary>
    private void ExecuteSwayingPhysics()
    {
        if (useDirectControl)
        {
            if (joycon == null && !debugMode) return;

            float inputRatio = 0f;

            if (debugMode)
            {
                inputRatio = Input.GetAxisRaw("Horizontal");
            }
            else
            {
                Quaternion orientation = joycon.GetVector();
                float currentYaw = orientation.eulerAngles.y;

                float angleDifference = Mathf.DeltaAngle(180f, currentYaw);

                inputRatio = Mathf.Clamp(angleDifference / directControlInputRange, -1f, 1f);
            }

            if (invertDirectControl)
            {
                inputRatio *= -1f;
            }

            float targetAngle = -inputRatio * swayMaxAngle;

            float currentZ = transform.eulerAngles.z;
            if (currentZ > 180f) currentZ -= 360f;

            float newZ = Mathf.Lerp(currentZ, targetAngle, Time.fixedDeltaTime * directControlSmoothSpeed);
            rb.MoveRotation(newZ);

            rb.angularVelocity = 0f;

            float anglePowerRatio = Mathf.Abs(newZ) / swayMaxAngle;
            swayPower = anglePowerRatio * maxSwayPower;

            return;
        }

        float inputTorque = 0f;

        if (debugMode)
        {
            float horizontalInput = Input.GetAxisRaw("Horizontal");

            if (Mathf.Abs(horizontalInput) > 0.1f)
            {
                ChangeState(PlayerState.Swaying);

                float powerIncrease = swayIncreaseRate * Time.fixedDeltaTime;
                swayPower = Mathf.Min(maxSwayPower, swayPower + powerIncrease);
                Debug.Log($"<color=yellow>[DEBUG]</color> <color=green>パワー増加</color> => 現在のパワー: <b>{swayPower.ToString("F1")}</b>");

                inputTorque = horizontalInput * swayForceByAngle * (1f + swayPower * powerToSwingMultiplier);
            }
        }
        else
        {
            if (joycon == null) return;

            Quaternion orientation = joycon.GetVector();
            Vector3 eulerAngles = orientation.eulerAngles;
            float currentYaw = eulerAngles.y;
            float yawVelocity = Mathf.DeltaAngle(lastYaw, currentYaw) / Time.fixedDeltaTime;
            lastYaw = currentYaw;

            float normalizedForce = 0f;
            if (currentYaw > rightSwingAngleMin && currentYaw < rightSwingAngleMax)
            {
                normalizedForce = Mathf.InverseLerp(rightSwingAngleMin, rightSwingAngleMax, currentYaw);
                ChangeState(PlayerState.Swaying);
            }
            else if (currentYaw > leftSwingAngleMax && currentYaw < leftSwingAngleMin)
            {
                normalizedForce = -Mathf.InverseLerp(leftSwingAngleMin, leftSwingAngleMax, currentYaw);
                ChangeState(PlayerState.Swaying);
            }

            if (normalizedForce != 0)
            {
                inputTorque = normalizedForce * swayForceByAngle * (1f + swayPower * powerToSwingMultiplier);
            }

            bool isTimingGood = (yawVelocity > swingVelocityThreshold && normalizedForce > 0) ||
                                (yawVelocity < -swingVelocityThreshold && normalizedForce < 0);

            if (isTimingGood)
            {
                float powerIncrease = (Mathf.Abs(normalizedForce) + Mathf.Abs(yawVelocity) * swayForceByVelocity)
                                    * swayIncreaseRate
                                    * Time.fixedDeltaTime;
                swayPower = Mathf.Min(maxSwayPower, swayPower + powerIncrease);
                Debug.Log($"<color=green>パワー増加</color> => 現在のパワー: <b>{swayPower.ToString("F1")}</b>");
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
            activeHingeJoint = gameObject.AddComponent<HingeJoint2D>();
            activeHingeJoint.connectedBody = nearestStrap.GetComponent<Rigidbody2D>();
            activeHingeJoint.autoConfigureConnectedAnchor = false;
            activeHingeJoint.anchor = transform.InverseTransformPoint(handPoint.position);
            activeHingeJoint.connectedAnchor = nearestStrap.grabPoint.localPosition;
            if (joycon != null)
            {
                lastYaw = joycon.GetVector().eulerAngles.y;
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