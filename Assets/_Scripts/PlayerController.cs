using System.Collections;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    private enum PlayerState { Idle, Grabbing, Swaying, Launched }
    private PlayerState currentState = PlayerState.Idle;

    [Header("必須コンポーレント")]
    public Transform handPoint;

    [Header("移動設定")]
    public float moveSpeed = 3.0f;

    [Header("入力感度設定")]
    [Tooltip("バランスボードの入力が、この値より大きくならないとスイングとして認識されません")]
    public float swingDeadzone = 0.05f;

    [Header("つり革アクション設定")]
    public float swayTorque = 10.0f;
    public float swayIncreaseRate = 5.0f;
    public float swayDecayRate = 10.0f;
    public float maxSwayPower = 100.0f;
    public float launchMultiplier = 30.0f;
    public float maxGrabDistance = 8.0f;

    [Header("NPCインタラクション設定")]
    public float knockbackMultiplier = 5.0f;

    [Header("ジャンプ制御")]
    public float straightenDuration = 0.5f;

    private Rigidbody2D rb;
    private float swayPower = 0f;
    private HangingStrap currentStrap = null;
    private HingeJoint2D activeHingeJoint;
    private Coroutine straighteningCoroutine;
    private bool wasSwayingLastFrame = false;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        if (handPoint == null) { Debug.LogError("HandPointが設定されていません！"); }
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
    }

    void Update()
    {
        switch (currentState)
        {
            case PlayerState.Idle: HandleIdleInput(); break;
            case PlayerState.Grabbing:
            case PlayerState.Swaying: HandleHangingInput(); break;
            // ▼▼▼ 修正箇所：Launched状態でも入力を受け付けるように変更 ▼▼▼
            case PlayerState.Launched: HandleLaunchedInput(); break;
        }
    }

    void FixedUpdate()
    {
        if (currentState == PlayerState.Idle)
        {
            ExecuteIdleMovement();
        }
    }

    private void HandleIdleInput()
    {
        if (Input.GetKeyDown(KeyCode.Space)) { GrabNearestStrap(); }
    }

    // ▼▼▼ 新規追加：Launched状態での入力処理メソッド ▼▼▼
    private void HandleLaunchedInput()
    {
        // 空中にいるときにスペースキーが押されたら、再掴みを試みる
        if (Input.GetKeyDown(KeyCode.Space))
        {
            GrabNearestStrap();
        }
    }

    private void HandleHangingInput()
    {
        // A/Dキーと矢印キーを同等に扱うように修正
        float horizontalInput;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
        {
            horizontalInput = 1.0f;
        }
        else if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
        {
            horizontalInput = -1.0f;
        }
        else
        {
            // キーボード入力がない場合のみ、バランスボード等のアナログ入力を受け付ける
            horizontalInput = Input.GetAxis("Horizontal");
        }

        bool isSwayingThisFrame = Mathf.Abs(horizontalInput) > swingDeadzone;

        // --- スイングのコアロジック（可読性向上版） ---
        bool shouldAccelerate = (rb.angularVelocity > 0.1f && horizontalInput > 0) ||
                                (rb.angularVelocity < -0.1f && horizontalInput < 0);
        bool isNewSwayInput = isSwayingThisFrame && !wasSwayingLastFrame;
        if (shouldAccelerate && isNewSwayInput)
        {
            rb.AddTorque(Mathf.Sign(horizontalInput) * swayTorque, ForceMode2D.Impulse);
            swayPower += swayIncreaseRate;
            Debug.Log("加速成功！ 現在のパワー: " + swayPower);
        }
        
        swayPower -= swayDecayRate * Time.deltaTime;
        swayPower = Mathf.Clamp(swayPower, 0, maxSwayPower);
        currentState = isSwayingThisFrame ? PlayerState.Swaying : PlayerState.Grabbing;
        wasSwayingLastFrame = isSwayingThisFrame;
        
        // --- デバッグ用の入力処理 ---
        if (Input.GetKeyUp(KeyCode.Space))
        {
            if (Input.GetKey(KeyCode.LeftShift))
            {
                Debug.Log("デバッグ：最大パワーで発射！");
                ReleaseStrap(maxSwayPower);
            }
            else
            {
                ReleaseStrap(swayPower);
            }
        }
        if (Input.GetKeyDown(KeyCode.LeftControl))
        {
            Debug.Log("デバッグ：ゼロパワーで落下");
            ReleaseStrap(0f);
        }
    }

    private void ExecuteIdleMovement()
    {
        float moveHorizontal;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
        {
            moveHorizontal = 1.0f;
        }
        else if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
        {
            moveHorizontal = -1.0f;
        }
        else
        {
            moveHorizontal = Input.GetAxis("Horizontal");
        }
        rb.velocity = new Vector2(moveHorizontal * moveSpeed, rb.velocity.y);
    }

    // ▼▼▼ 修正箇所：空中での再掴みに対応 ▼▼▼
    private void GrabNearestStrap()
    {
        // 地上のアイドル状態から掴む場合のみ、プレイヤーの動きを止める
        if (currentState == PlayerState.Idle)
        {
            rb.velocity = Vector2.zero;
        }
        
        HangingStrap nearestStrap = HangingStrapManager.FindNearestStrap(transform.position, maxGrabDistance);
        if (nearestStrap != null)
        {
            // 空中(Launched)から掴む場合、姿勢制御を中断する
            if (currentState == PlayerState.Launched)
            {
                if (straighteningCoroutine != null)
                {
                    StopCoroutine(straighteningCoroutine);
                    straighteningCoroutine = null;
                }
                rb.rotation = 0f; // 掴む瞬間の角度をリセット
            }

            currentState = PlayerState.Grabbing;
            currentStrap = nearestStrap;
            rb.constraints = RigidbodyConstraints2D.None;

            activeHingeJoint = gameObject.AddComponent<HingeJoint2D>();
            activeHingeJoint.connectedBody = currentStrap.GetComponent<Rigidbody2D>();
            activeHingeJoint.autoConfigureConnectedAnchor = false;
            activeHingeJoint.anchor = transform.InverseTransformPoint(handPoint.position);
            activeHingeJoint.connectedAnchor = currentStrap.grabPoint.localPosition;
        }
    }

    private void ReleaseStrap(float currentSwayPower)
    {
        if (currentState == PlayerState.Idle || currentState == PlayerState.Launched) return;
        
        Debug.Log("発射！ パワー: " + currentSwayPower + ", 倍率: " + launchMultiplier);
        currentState = PlayerState.Launched;
        Destroy(activeHingeJoint);
        rb.constraints = RigidbodyConstraints2D.None;

        Vector2 currentVelocity = rb.velocity;
        float launchDirection = Mathf.Sign(currentVelocity.x);
        if (Mathf.Approximately(launchDirection, 0)) { launchDirection = 1; }
        
        if (currentSwayPower > 0)
        {
            Vector2 launchBoost = new Vector2(launchDirection, 1.0f).normalized * currentSwayPower * launchMultiplier;
            rb.AddForce(launchBoost, ForceMode2D.Impulse);
        }

        swayPower = 0f;
        currentStrap = null;
        wasSwayingLastFrame = false;

        if (straighteningCoroutine != null) StopCoroutine(straighteningCoroutine);
        straighteningCoroutine = StartCoroutine(StraightenUpInAir());
    }

    private IEnumerator StraightenUpInAir()
    {
        float startRotation = rb.rotation;
        float endRotation = 0f;
        float elapsedTime = 0f;

        while (elapsedTime < straightenDuration)
        {
            if (currentState != PlayerState.Launched) yield break;
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / straightenDuration;
            rb.rotation = Mathf.LerpAngle(startRotation, endRotation, t);
            yield return null;
        }
        rb.rotation = 0f;
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("NPC"))
        {
            NPCController npc = collision.gameObject.GetComponent<NPCController>();
            if (npc != null && (currentState == PlayerState.Launched || currentState == PlayerState.Grabbing || currentState == PlayerState.Swaying))
            {
                npc.TakeImpact(rb.velocity, knockbackMultiplier);
            }
        }

        if (currentState == PlayerState.Launched && collision.gameObject.CompareTag("Ground"))
        {
            currentState = PlayerState.Idle;
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