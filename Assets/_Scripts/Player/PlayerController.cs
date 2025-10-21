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

    [Header("接地判定")]
    [Tooltip("接地判定レイキャストの発射地点")]
    public Transform feetPosition;
    [Tooltip("地面とみなすレイヤー")]
    public LayerMask groundLayer;
    [Tooltip("地面を検知するレイキャストの距離")]
    public float groundCheckDistance = 0.1f;

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

    // --- Component References ---
    private Rigidbody2D rb;
    private Animator playerAnim;
    private StageManager stageManager;
    private HingeJoint2D activeHingeJoint;
    private List<Joycon> joycons;
    private Joycon joycon;

    // --- Player State ---
    private PlayerState currentState = PlayerState.Idle;
    private float swayPower = 0f;
    private HangingStrap currentStrap = null;
    private float lastFacingDirection = 1f; // 最後に見ていた向き(1=右, -1=左)

    // --- Input Handling ---
    private bool wasGripInputActiveLastFrame = false;
    private float lastYaw = 0f; // Joy-Conの前回のヨー角
    private float timeSinceGripLow = 0f; // 握力が弱い状態が続いている時間

    // --- Coroutine Management ---
    private Coroutine grabToSwayCoroutine;


    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        playerAnim = GetComponentInChildren<Animator>();

        // --- このデバッグログを追加 ---
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
        if (stageManager != null && stageManager.CurrentState != StageManager.GameState.Playing)
        {
            return;
        }

        // 1. センサーやキーボードからの「生」の入力状態を読み取る
        bool isRawGripSignalActive = Input.GetKey(KeyCode.Space) || (ArduinoInputManager.GripValue > gripThreshold);

        // 2. 生の入力状態に基づいて、猶予タイマーを更新する
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

        // 3. 安定化された最終的な「掴んでいる」状態を決定する
        //    信号がONか、または信号がOFFでも猶予期間内なら「掴んでいる」とみなす
        bool isGripInputActive = isRawGripSignalActive || (timeSinceGripLow < gripReleaseGracePeriod);

        // 4. 前フレームとの状態比較から「掴んだ瞬間」「離した瞬間」を判定する（この部分は変更なし）
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
        // [ここから新しいロジック] 発射(Launched)状態の時、滑らかに体を直立させる
        else if (currentState == PlayerState.Launched)
        {
            // 1. 目標の回転を定義する (Z軸回転0度、つまり直立)
            Quaternion targetRotation = Quaternion.identity;

            // 2. 現在の回転から目標の回転まで、指定した速度で滑らかに補間する
            Quaternion newRotation = Quaternion.Slerp(
                rb.transform.rotation,      // 現在の回転
                targetRotation,             // 目標の回転
                aerialRotationSpeed * Time.fixedDeltaTime // 回転の速さ
            );

            // 3. 物理エンジンと協調して、計算した新しい回転を適用する
            rb.MoveRotation(newRotation);
        }

        // デバッグモード中は減衰率を0に、そうでなければ通常の値を使用する
        float currentDecayRate = debugMode ? 0f : swayDecayRate;
        swayPower = Mathf.Max(0, swayPower - currentDecayRate * Time.fixedDeltaTime);
    }

    private void LateUpdate()
    {
        // --- アニメーションの状態を同期 ---
        if (playerAnim != null)
        {
            playerAnim.SetInteger("State", (int)currentState);
        }

        // --- キャラクターの向きを同期 ---
        HandleDirection();
    }

private void ChangeState(PlayerState newState)
{
    if (currentState == newState) return;

    // [提案] 状態がいつ、どのように変わったかログに出力する
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

        // 空中(Launched)状態では、向きの更新を行わない
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
        // --- デバッグモード時のキーボード操作 ---
        if (debugMode)
        {
            float horizontalInput = Input.GetAxisRaw("Horizontal");

            // キー入力がなければ、トルクをかけずに処理を終える(Swaying状態は維持)
            if (Mathf.Abs(horizontalInput) < 0.1f)
            {
                return;
            }

            // スイング入力があるので、状態をSwayingに設定する
            ChangeState(PlayerState.Swaying);

            // キーが押されている間は常にパワーが増加する
            float powerIncrease = swayIncreaseRate * Time.fixedDeltaTime;
            swayPower = Mathf.Min(maxSwayPower, swayPower + powerIncrease);
            Debug.Log($"<color=yellow>[DEBUG]</color> <color=green>パワー増加</color> => 現在のパワー: <b>{swayPower.ToString("F1")}</b>");

            // 基本となるトルクを計算
            float totalTorque = horizontalInput * swayForceByAngle * (1f + swayPower * powerToSwingMultiplier);

            // スイング方向と慣性の方向が一致しているかチェック
            if (Mathf.Sign(horizontalInput) == Mathf.Sign(StageManager.CurrentInertia.x))
            {
                // 方向が一致していれば、慣性ボーナスを加える
                totalTorque += StageManager.CurrentInertia.x * inertiaSwingBonus;
            }

            rb.AddTorque(totalTorque);
        }
        // --- 通常時のJoy-Con操作 ---
        else
        {
            if (joycon == null) return;

            // --- 1. Joy-Conから角度と角速度を取得 ---
            Quaternion orientation = joycon.GetVector();
            Vector3 eulerAngles = orientation.eulerAngles;
            float currentYaw = eulerAngles.y;
            float yawVelocity = Mathf.DeltaAngle(lastYaw, currentYaw) / Time.fixedDeltaTime;
            lastYaw = currentYaw;

            // --- 2. 角度に基づいて力の方向を計算し、Stateを更新 ---
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

            // --- 3. トルクをかけ、タイミングが良い時だけパワーを溜める ---
            // スイング入力がある場合(デッドゾーン外の場合)
            if (normalizedForce != 0)
            {
                // 基本となるトルクを計算
                float totalTorque = normalizedForce * swayForceByAngle * (1f + swayPower * powerToSwingMultiplier);

                // スイング方向と慣性の方向が一致しているかチェック
                if (Mathf.Sign(normalizedForce) == Mathf.Sign(StageManager.CurrentInertia.x))
                {
                    // 方向が一致していれば、慣性ボーナスを加える
                    totalTorque += StageManager.CurrentInertia.x * inertiaSwingBonus;
                }

                rb.AddTorque(totalTorque);
            }

            // タイミングが良いか判定
            bool isTimingGood = (yawVelocity > swingVelocityThreshold && normalizedForce > 0) ||
                                (yawVelocity < -swingVelocityThreshold && normalizedForce < 0);

            // タイミングが良い時だけパワーを蓄積
            if (isTimingGood)
            {
                float powerIncrease = (Mathf.Abs(normalizedForce) + Mathf.Abs(yawVelocity) * swayForceByVelocity)
                                    * swayIncreaseRate
                                    * Time.fixedDeltaTime;
                swayPower = Mathf.Min(maxSwayPower, swayPower + powerIncrease);
                Debug.Log($"<color=green>パワー増加</color> => 現在のパワー: <b>{swayPower.ToString("F1")}</b>");
            }
        }
    }

    // アイドル時の移動はできなくなったので、このメソッドは空にする
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

        // コンソールに判定結果を詳しく出力する
        if (hit.collider != null)
        {
            Debug.Log($"<color=green>IsGrounded SUCCESS:</color> レイがオブジェクト「{hit.collider.name}」（レイヤー: {LayerMask.LayerToName(hit.collider.gameObject.layer)}）にヒットしました。IsGroundedは true を返します。");
        }
        else
        {
            Debug.Log($"<color=red>IsGrounded FAILURE:</color> レイは地面を検知しませんでした。IsGroundedは false を返します。");
        }

        return hit.collider != null;
    }

    // 地面との衝突判定は、トリガーではなく物理的な衝突で行うため、
    // このメソッドは削除せず、そのまま残してください。
    void OnCollisionEnter2D(Collision2D collision)
    {
        // 地面との衝突判定
        if (collision.gameObject.CompareTag("Ground"))
        {
            // 物理的につり革に接続されていない場合、地面にいるなら必ずアイドル状態になる
            if (activeHingeJoint == null)
            {
                ChangeState(PlayerState.Idle);
                rb.rotation = 0f;
                rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            }
        }
    }
}