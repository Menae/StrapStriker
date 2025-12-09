using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    // ゲーム内でプレイヤーが取りうる状態を定義
    private enum PlayerState { Idle, Grabbing, Swaying, Launched }

    [Header("必須コンポーネント")]
    [Tooltip("プレイヤーの手の位置。つり革を掴む際の基点となります。")]
    public Transform handPoint;

    [Header("入力設定")]
    [Tooltip("この値以上の握力でつり革を掴みます")]
    public int gripThreshold = 500;

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
    // 画像の左端の状態が何度か、右端の状態が何度かを設定
    // 左に60度傾いた状態から右に60度傾いた状態までのアニメーションなら 60 を設定
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

    private const float MinLaunchPower = 1f; // 発射に最低限必要なパワー

    private float gameStartTime = -1f; // ゲームがPlaying状態になった時刻
    [Tooltip("ゲーム開始後、入力受付を開始するまでの無効時間（秒）")]
    public float inputDelayAfterStart = 0.5f;

    // 内部変数
    private Rigidbody2D rb;
    private Animator playerAnim;
    private StageManager stageManager;
    private HingeJoint2D activeHingeJoint;
    private List<Joycon> joycons;
    private Joycon joycon;

    // Player State
    private PlayerState currentState = PlayerState.Idle;
    private float swayPower = 0f;
    private HangingStrap currentStrap = null;
    private float lastFacingDirection = 1f; // 最後に見ていた向き(1=右, -1=左)

    // Input Handling
    private bool wasGripInputActiveLastFrame = false;
    private float lastYaw = 0f; // Joy-Conの前回のヨー角
    private float timeSinceGripLow = 0f; // 握力が弱い状態が続いている時間

    private Coroutine grabToSwayCoroutine;


    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        playerAnim = GetComponentInChildren<Animator>();

        if (playerAnim == null)
        {
            Debug.LogError("Playerの子オブジェクトにAnimatorが見つかりません！ playerAnimがnullです。");
        }
        else
        {
            Debug.Log("<color=cyan>Animatorの参照取得に成功しました。</color>");
        }

        // シーン内にあるStageManagerを探す
        stageManager = FindObjectOfType<StageManager>();
        if (stageManager == null)
        {
            Debug.LogError("シーン内にStageManagerが見つかりません！");
        }

        // Joy-Conのセットアップ
        joycons = JoyconManager.instance.j;
        if (joycons.Count > 0)
        {
            joycon = joycons[0];
        }
    }

    void Update()
    {
        // ゲームがプレイ中でない場合、タイマーをリセットして処理を抜ける
        if (stageManager != null && stageManager.CurrentState != StageManager.GameState.Playing)
        {
            gameStartTime = -1f;
            return;
        }

        // ゲームがプレイ中になった瞬間を検知し、開始時刻を記録する
        if (gameStartTime < 0f)
        {
            gameStartTime = Time.time;
        }

        // ゲーム開始からの経過時間が、設定した無効時間より短い場合は入力を無視する
        if (Time.time < gameStartTime + inputDelayAfterStart)
        {
            // ただし、入力の記録だけは更新しておき、無効時間終了直後の誤作動を防ぐ
            wasGripInputActiveLastFrame = Input.GetKey(KeyCode.Space) || (ArduinoInputManager.GripValue > gripThreshold);
            return;
        }

        // センサーやキーボードからの入力状態を読み取る
        bool isRawGripSignalActive = Input.GetKey(KeyCode.Space) || (ArduinoInputManager.GripValue > gripThreshold);

        // 入力状態に基づいて、猶予タイマーを更新する
        if (isRawGripSignalActive)
        {
            // 信号がONなら、タイマーをリセット
            timeSinceGripLow = 0f;
        }
        else
        {
            // 信号がOFFなら、タイマーを進める
            timeSinceGripLow += Time.deltaTime;
        }

        // 最終的な「掴んでいる」状態を決定する
        // 信号がONか、または信号がOFFでも猶予期間内なら「掴んでいる」とみなす
        bool isGripInputActive = isRawGripSignalActive || (timeSinceGripLow < gripReleaseGracePeriod);

        // 前フレームとの状態比較から「掴んだ瞬間」「離した瞬間」を判定する
        bool gripPressed = isGripInputActive && !wasGripInputActiveLastFrame;
        bool gripReleased = !isGripInputActive && wasGripInputActiveLastFrame;

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

        // 接地判定を毎フレーム呼び出して、コンソールとSceneビューで確認する
        IsGrounded();

        if (Input.GetKeyDown(KeyCode.E))
        {
            playerAnim.SetTrigger("Jump");
        }
        if (Input.GetKeyDown(KeyCode.R))
        {
            playerAnim.SetTrigger("Kick");
        }
        if (debugMode)
        {
            if (Input.GetKey(KeyCode.LeftArrow))
            {
                rb.velocity = Vector2.left * 5f;
            }
            if (Input.GetKey(KeyCode.RightArrow))
            {
                rb.velocity = Vector2.right * 5f;
            }
        }
    }

    void FixedUpdate()
    {
        // 掴んでいる時だけスイングの物理処理を実行
        if (currentState == PlayerState.Grabbing || currentState == PlayerState.Swaying)
        {
            ExecuteSwayingPhysics();
        }
        else if (currentState == PlayerState.Launched)
        {
            // (中略...既存のLaunched処理はそのまま)
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

        // 直感操作モードONの時は、ExecuteSwayingPhysics内でpowerが上書きされるため減衰させない
        if (!useDirectControl)
        {
            float currentDecayRate = debugMode ? 0f : swayDecayRate;
            swayPower = Mathf.Max(0, swayPower - currentDecayRate * Time.fixedDeltaTime);
        }
    }

    private void LateUpdate()
    {
        if (playerAnim != null)
        {
            playerAnim.SetInteger("State", (int)currentState);

            if (currentState == PlayerState.Swaying || currentState == PlayerState.Grabbing)
            {
                // 1. 角度の取得
                float currentAngle = transform.eulerAngles.z;
                if (currentAngle > 180f) currentAngle -= 360f;

                // 2. 基本の計算 (左向きの時はこれで正解)
                // ※ もし左向きも逆になっている場合は、ここの引数を逆にしてください
                float normalizedTime = Mathf.InverseLerp(swayMaxAngle, -swayMaxAngle, currentAngle);

                // 右向きの補正
                // 右を向いている場合(lastFacingDirectionが正)は、アニメーションの進行を反転させる
                if (lastFacingDirection > 0)
                {
                    normalizedTime = 1f - normalizedTime;
                }

                playerAnim.SetFloat("SwayTime", normalizedTime);
            }
        }

        HandleDirection();
    }

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
        // 物理的につり革を掴んでいる間は、向きの更新処理を一切行わない
        if (currentStrap != null)
        {
            return;
        }

        // Launched状態では、向きの更新を行わない
        if (currentState != PlayerState.Launched)
        {
            // キャラクターの水平方向の速度を取得
            float horizontalVelocity = rb.velocity.x;

            // わずかな動きは無視し、一定以上の速度が出ている時だけ向きを更新
            if (Mathf.Abs(horizontalVelocity) > 0.1f)
            {
                // 速度の方向に応じて、向き(-1か1)を決定
                lastFacingDirection = Mathf.Sign(horizontalVelocity);
            }
        }

        // Animatorに現在の向きを伝える
        if (playerAnim != null)
        {
            playerAnim.SetFloat("Direction", lastFacingDirection);
        }
    }

    private void ExecuteSwayingPhysics()
    {
        // =================================================================
        // 直感操作モード
        // =================================================================
        if (useDirectControl)
        {
            if (joycon == null && !debugMode) return;

            float inputRatio = 0f;

            if (debugMode)
            {
                // デバッグモード: キーボード入力 (-1 ~ 1)
                inputRatio = Input.GetAxisRaw("Horizontal");
            }
            else
            {
                // Joy-Conの仕様上、ケーブル側(下)を180度とし、そこからの差分を取る
                Quaternion orientation = joycon.GetVector();
                float currentYaw = orientation.eulerAngles.y;

                // 180度を基準とした角度差分を取得 (-180 ~ 180)
                float angleDifference = Mathf.DeltaAngle(180f, currentYaw);

                // 設定した範囲(directControlInputRange)で割って -1 ~ 1 に正規化
                // rangeが90の場合、90度傾けると1.0(MAX)になる
                // rangeを大きくする(120など)と、より大きく傾けないとMAXにならず、感度が下がる
                inputRatio = Mathf.Clamp(angleDifference / directControlInputRange, -1f, 1f);
            }

            // 反転フラグがONなら入力を逆にする
            if (invertDirectControl)
            {
                inputRatio *= -1f;
            }

            // 1. 目標角度の計算
            // inputRatio (-1 ~ 1) に 最大角度(swayMaxAngle) を掛ける
            // Unityの2D回転は「左回転(反時計回り)がプラス」。
            // 右に入力(+1)した時、キャラは右(時計回り、マイナス角)に行ってほしいのでマイナスを掛ける。
            float targetAngle = -inputRatio * swayMaxAngle;

            // 2. 現在の角度から目標角度へスムーズに回転させる
            float currentZ = transform.eulerAngles.z;
            if (currentZ > 180f) currentZ -= 360f; // -180~180表現に変換

            // Lerpで補間移動。Speedの値で追従性を調整
            float newZ = Mathf.Lerp(currentZ, targetAngle, Time.fixedDeltaTime * directControlSmoothSpeed);
            rb.MoveRotation(newZ);

            // 3. 物理挙動の干渉を防ぐ（慣性で揺れ戻しが起きないようにする）
            rb.angularVelocity = 0f;

            // 4. パワーの自動計算
            // 直感操作モードでは「振って溜める」ことができないため、
            // 「傾きが大きい＝パワーが溜まっている」とみなして発射力を確保する
            float anglePowerRatio = Mathf.Abs(newZ) / swayMaxAngle;
            swayPower = anglePowerRatio * maxSwayPower;

            // 物理モードの処理は行わずに終了
            return;
        }

        // =================================================================
        // 通常モード: 物理演算によるスイング
        // =================================================================

        float inputTorque = 0f;

        // --- 1. プレイヤー入力によるトルクの計算 ---
        if (debugMode)
        {
            float horizontalInput = Input.GetAxisRaw("Horizontal");

            // キー入力がある場合のみ処理
            if (Mathf.Abs(horizontalInput) > 0.1f)
            {
                ChangeState(PlayerState.Swaying);

                float powerIncrease = swayIncreaseRate * Time.fixedDeltaTime;
                swayPower = Mathf.Min(maxSwayPower, swayPower + powerIncrease);
                Debug.Log($"<color=yellow>[DEBUG]</color> <color=green>パワー増加</color> => 現在のパワー: <b>{swayPower.ToString("F1")}</b>");

                inputTorque = horizontalInput * swayForceByAngle * (1f + swayPower * powerToSwingMultiplier);
            }
        }
        else // Joy-Con操作
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

        // --- 2. 慣性によるトルクの計算 ---
        float inertiaTorque = 0f;
        if (StageManager.CurrentInertia.x != 0)
        {
            inertiaTorque = StageManager.CurrentInertia.x * inertiaSwingBonus;
        }

        // --- 3. 合算して適用 ---
        float totalTorque = inputTorque + inertiaTorque;

        if (Mathf.Abs(totalTorque) > 0.01f)
        {
            rb.AddTorque(totalTorque);
        }
    }

    // アイドル時の移動不可
    private void ExecuteIdleMovement()
    {
        // 何もしない
    }

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

    private void ReleaseStrap()
    {
        // 掴み->スイング移行中のコルーチンが動いていれば止める
        if (grabToSwayCoroutine != null)
        {
            StopCoroutine(grabToSwayCoroutine);
            grabToSwayCoroutine = null;
        }

        // 物理的な接続を解除 (共通処理)
        Destroy(activeHingeJoint);
        activeHingeJoint = null;
        currentStrap = null;

        // 状態に応じた処理
        switch (currentState)
        {
            case PlayerState.Grabbing:
                ChangeState(PlayerState.Idle);
                swayPower = 0f;
                break;

            case PlayerState.Swaying:
                // パワーが不十分な場合
                if (swayPower < MinLaunchPower)
                {
                    // ただし、空中にいる場合は落下中も姿勢を制御させたいのでLaunched状態にする
                    if (!IsGrounded())
                    {
                        ChangeState(PlayerState.Launched);
                    }
                    // 地面にいる場合は、そのままIdle状態になる
                    else
                    {
                        ChangeState(PlayerState.Idle);
                    }

                    swayPower = 0f;
                    return; // 発射処理は行わずに終了
                }

                // パワーが十分なら発射
                Debug.Log($"<color=orange><b>発射！</b></color> 使用パワー: {swayPower.ToString("F1")}");
                ChangeState(PlayerState.Launched);
                rb.constraints = RigidbodyConstraints2D.None;

                // 発射前にスイングの回転をリセットし、体勢を安定させる
                rb.angularVelocity = 0f;

                Vector2 currentVelocity = rb.velocity.normalized;
                if (currentVelocity.sqrMagnitude < 0.1f) { currentVelocity = Vector2.up; }

                // 発射の向きから、着地するまでのキャラクターの向きを決定する
                lastFacingDirection = Mathf.Sign(currentVelocity.x);
                // もし真上に飛んだなどで水平方向の向きがない場合は、以前の向きを維持する
                if (Mathf.Abs(currentVelocity.x) < 0.01f)
                {
                    // lastFacingDirection は以前の値のまま
                }

                Vector2 launchBoost = currentVelocity * swayPower * launchMultiplier;
                rb.AddForce(launchBoost, ForceMode2D.Impulse);

                swayPower = 0f;
                break;
        }
    }

    private IEnumerator TransitionToSwayingRoutine()
    {
        // アニメーションの再生が終わる想定時間まで待機
        yield return new WaitForSeconds(grabToSwayTransitionTime);

        // 待機後もまだ掴み状態(Grabbing)が継続している場合のみ、Swayingに移行する
        // (待っている間にプレイヤーが掴むのをやめたケースに対応)
        if (currentState == PlayerState.Grabbing)
        {
            ChangeState(PlayerState.Swaying);
        }
        // コルーチン自身の参照をクリア
        grabToSwayCoroutine = null;
    }

    public void HandleNpcCollision(Collider2D other)
    {
        if (other.gameObject.CompareTag("NPC"))
        {
            NPCController npc = other.gameObject.GetComponentInParent<NPCController>();

            if (npc != null)
            {
                // 威力の計算用に、まず現在の速度をベースとする
                Vector2 impactVelocity = rb.velocity;

                // もしスイング中なら、SwayPowerを威力に加算する（この部分は維持）
                if (currentState == PlayerState.Swaying)
                {
                    Vector2 powerBonus = impactVelocity.normalized * swayPower * swayImpactPowerBonus;
                    impactVelocity += powerBonus;
                    Debug.Log($"<color=red>スイングインパクト！</color> パワーボーナス: {powerBonus.magnitude}");
                }

                // 最終的に計算された威力でNPCにインパクトを与える
                npc.TakeImpact(impactVelocity, knockbackMultiplier);
            }
        }
    }

    private bool IsGrounded()
    {
        RaycastHit2D hit = Physics2D.Raycast(feetPosition.position, Vector2.down, groundCheckDistance, groundLayer);

        // Sceneビューにレイを可視化する（緑 = ヒット, 赤 = ミス）
        Color rayColor = hit ? Color.green : Color.red;
        Debug.DrawRay(feetPosition.position, Vector2.down * groundCheckDistance, rayColor);

        if (hit.collider != null)
        {
            //Debug.Log($"<color=green>IsGrounded SUCCESS:</color> レイがオブジェクト「{hit.collider.name}」（レイヤー: {LayerMask.LayerToName(hit.collider.gameObject.layer)}）にヒットしました。IsGroundedは true を返します。");
        }
        else
        {
            //Debug.Log($"<color=red>IsGrounded FAILURE:</color> レイは地面を検知しませんでした。IsGroundedは false を返します。");
        }

        return hit.collider != null;
    }
}