using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    private enum PlayerState { Idle, Grabbing, Swaying, Launched }
    private PlayerState currentState = PlayerState.Idle;

    [Header("必須コンポーネント")]
    public Transform handPoint;

    [Header("Arduino設定")]
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
    [Tooltip("パワーを発射の力に変換する際の倍率")]
    public float launchMultiplier = 50f;
    [Tooltip("スイング成功と判定されるJoy-Conの回転速度のしきい値")]
    public float swingVelocityThreshold = 15f;
    [Tooltip("溜まったパワーがスイングの見た目の大きさに影響を与える度合い")]
    public float powerToSwingMultiplier = 0.1f;

    [Header("スイング角度設定")]
    [Tooltip("左スイングと認識される最大角度")]
    public float leftSwingAngleMax = 90f;
    [Tooltip("左スイングと認識される最小角度")]
    public float leftSwingAngleMin = 150f;
    [Tooltip("右スイングと認識される最小角度")]
    public float rightSwingAngleMin = 210f;
    [Tooltip("右スイングと認識される最大角度")]
    public float rightSwingAngleMax = 270f;

    [Header("バランス調整")]
    [Tooltip("掴める最大距離")]
    public float maxGrabDistance = 8.0f;
    [Tooltip("空中で再掴みした際に、既存の速度をどのくらい減衰させるか (0.5 = 50%に)")]
    [Range(0f, 1f)]
    public float aerialRecatchDampener = 0.5f;
    [Tooltip("発射後、体をまっすぐに戻すのにかかる時間")]
    public float straightenDuration = 0.5f;

    [Header("NPCインタラクション設定")]
    public float knockbackMultiplier = 5.0f;

    [Header("慣性設定")]
    [Tooltip("慣性力が発射に与えるボーナスの大きさ")]
    public float inertiaBonusMultiplier = 1.5f;
    [Tooltip("慣性がスイングに与える影響の大きさ")]
    public float inertiaSwingBonus = 5f;


    [Header("デバッグ用")]
    [SerializeField] private bool debugMode = false;

    // --- 内部変数 ---
    private Rigidbody2D rb;
    private float swayPower = 0f;
    private HangingStrap currentStrap = null;
    private HingeJoint2D activeHingeJoint;
    private Coroutine straighteningCoroutine;
    private Coroutine grabToSwayCoroutine;
    private float lastYaw = 0f;
    private bool wasGripInputActiveLastFrame = false;
    private Animator playerAnim;
    private StageManager stageManager;
    private float lastFacingDirection = 1f; // 最後に見ていた方向(1=右, -1=左)最初は右向き。

    // --- Joy-Con関連の内部変数 ---
    private List<Joycon> joycons;
    private Joycon joycon;


    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        playerAnim = GetComponentInChildren<Animator>();

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
            return; // 即座にメソッドを抜ける
        }

        // --- ステップ1: 全ての入力を1つの状態に統一する ---
        bool isGripInputActive = Input.GetKey(KeyCode.Space) || (ArduinoInputManager.GripValue > gripThreshold);

        // --- ステップ2: 状態が切り替わった「瞬間」を検知する ---
        // もし今フレームで「掴み始めた」なら
        if (isGripInputActive && !wasGripInputActiveLastFrame)
        {
            if (currentState == PlayerState.Idle || currentState == PlayerState.Launched)
            {
                GrabNearestStrap();
            }
        }
        // もし今フレームで「離し始めた」なら
        else if (!isGripInputActive && wasGripInputActiveLastFrame)
        {
            if (currentState == PlayerState.Grabbing || currentState == PlayerState.Swaying)
            {
                ReleaseStrap();
            }
        }

        // --- ステップ3: 来フレームのために、今の状態を記録しておく ---
        wasGripInputActiveLastFrame = isGripInputActive;

        // DEBUG
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
        currentState = newState;
    }

    private void HandleDirection()
    {
        // 物理的につり革を掴んでいる間は、向きの更新処理を一切行わない
        if (currentStrap != null)
        {
            return;
        }

        // キャラクターの水平方向の速度を取得
        float horizontalVelocity = rb.velocity.x;

        // わずかな動きは無視し、一定以上の速度が出ている時だけ向きを更新
        if (Mathf.Abs(horizontalVelocity) > 0.1f)
        {
            // 速度の方向に応じて、向き(-1か1)を決定
            lastFacingDirection = Mathf.Sign(horizontalVelocity);
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
            // 空中(Launched)から再掴みした場合、既存の速度を減衰させて無限加速を防ぐ
            if (currentState == PlayerState.Launched)
            {
                if (straighteningCoroutine != null) StopCoroutine(straighteningCoroutine);
                rb.velocity *= aerialRecatchDampener;
                rb.angularVelocity = 0f;
            }

            ChangeState(PlayerState.Grabbing);
            currentStrap = nearestStrap;
            rb.constraints = RigidbodyConstraints2D.None; // 回転を許可

            activeHingeJoint = gameObject.AddComponent<HingeJoint2D>();
            activeHingeJoint.connectedBody = currentStrap.GetComponent<Rigidbody2D>();
            activeHingeJoint.autoConfigureConnectedAnchor = false;
            activeHingeJoint.anchor = transform.InverseTransformPoint(handPoint.position);
            activeHingeJoint.connectedAnchor = currentStrap.grabPoint.localPosition;

            // 掴んだ瞬間のヨー角を初期値として記録
            if (joycon != null)
            {
                lastYaw = joycon.GetVector().eulerAngles.y;
            }

            if (grabToSwayCoroutine != null) StopCoroutine(grabToSwayCoroutine);
            grabToSwayCoroutine = StartCoroutine(TransitionToSwayingRoutine());
        }
    }

    private void ReleaseStrap()
    {
        if (grabToSwayCoroutine != null)
        {
            StopCoroutine(grabToSwayCoroutine);
            grabToSwayCoroutine = null;
        }

        if (currentState == PlayerState.Idle || currentState == PlayerState.Launched) return;

        // まず、どの状態からでも共通の物理的な接続解除を行う
        Destroy(activeHingeJoint);
        activeHingeJoint = null;
        currentStrap = null;

        // ジャンプ中に離した場合 (素早い入力)
        if (currentState == PlayerState.Grabbing)
        {
            ChangeState(PlayerState.Idle);
            swayPower = 0f;
        }
        // スイング中に離した場合
        else if (currentState == PlayerState.Swaying)
        {
            // パワーがほとんど無い場合は、Idle状態に戻して落下させる
            if (swayPower < 1f)
            {
                ChangeState(PlayerState.Idle);
                swayPower = 0f;
            }
            // パワーが十分にある場合は、発射処理を行う
            else
            {
                Debug.Log($"<color=orange><b>発射！</b></color> 使用パワー: {swayPower.ToString("F1")}");

                ChangeState(PlayerState.Launched);
                rb.constraints = RigidbodyConstraints2D.None;

                Vector2 currentVelocity = rb.velocity.normalized;
                if (currentVelocity.sqrMagnitude < 0.1f) { currentVelocity = Vector2.up; }

                Vector2 launchBoost = currentVelocity * swayPower * launchMultiplier;
                rb.AddForce(launchBoost, ForceMode2D.Impulse);

                swayPower = 0f;

                if (straighteningCoroutine != null) StopCoroutine(straighteningCoroutine);
                straighteningCoroutine = StartCoroutine(StraightenUpInAir());
            }
        }
    }

    /// <summary>
    /// Jumpアニメーションが終了した時に、アニメーションイベントから呼び出される
    /// </summary>
    public void OnJumpAnimationFinished()
    {
        // イベントが呼ばれたことをログに出力
        Debug.Log($"<color=yellow>Frame {Time.frameCount}: OnJumpAnimationFinished event received. Current state is {currentState}.</color>");
        if (currentState == PlayerState.Grabbing)
        {
            ChangeState(PlayerState.Swaying);
        }
    }

    private IEnumerator TransitionToSwayingRoutine()
    {
        // Jumpアニメーションが再生されるのを少しだけ待つ
        yield return new WaitForSeconds(0.2f);

        // もし、待った後もまだ掴んでいる状態(Grabbing)なら、Swayingに移行する
        if (currentState == PlayerState.Grabbing)
        {
            ChangeState(PlayerState.Swaying);
        }
        grabToSwayCoroutine = null;
    }

    private IEnumerator StraightenUpInAir()
    {
        float startRotation = rb.rotation;
        float elapsedTime = 0f;

        while (elapsedTime < straightenDuration)
        {
            if (currentState != PlayerState.Launched) yield break; // もし着地などしたら中断
            elapsedTime += Time.deltaTime;
            rb.rotation = Mathf.LerpAngle(startRotation, 0f, elapsedTime / straightenDuration);
            yield return null;
        }
        if (currentState == PlayerState.Launched) rb.rotation = 0f;
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("NPC"))
        {
            NPCController npc = collision.gameObject.GetComponent<NPCController>();
            if (npc != null && (currentState == PlayerState.Launched || currentState == PlayerState.Swaying || currentState == PlayerState.Grabbing))
            {
                npc.TakeImpact(rb.velocity, knockbackMultiplier);
            }
        }

        // 地面との衝突判定
        if (collision.gameObject.CompareTag("Ground"))
        {
            // 物理的につり革に接続されていない場合、地面にいるなら必ずアイドル状態になる
            if (activeHingeJoint == null)
            {
                ChangeState(PlayerState.Idle);
                if (straighteningCoroutine != null)
                {
                    StopCoroutine(straighteningCoroutine);
                    straighteningCoroutine = null;
                }
                rb.rotation = 0f;
                rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            }
        }
    }
}