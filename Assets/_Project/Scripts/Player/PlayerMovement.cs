using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// WoW-style movement. Runs by default; WalkToggle switches to walking.
/// AutoRun toggles hands-free forward movement, cancelled by W, S, or
/// starting a both-mouse-button run. Movement and jumping cost no stamina.
/// Attach to the Player.
/// </summary>
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerInput))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Speed")]
    public float walkSpeed = 2.5f;
    public float runSpeed = 6f;
    public float backSpeedMultiplier = 0.65f;
    public float turnRate = 180f;

    [Header("Walk Toggle")]
    [Tooltip("Start in walk mode instead of run mode.")]
    public bool startWalking = false;

    [Header("Controls")]
    [Tooltip("On: A/D strafe. Off: A/D turn the character.")]
    public bool adStrafes = false;

    [Tooltip("Holding both mouse buttons moves forward.")]
    public bool bothButtonsRunForward = true;

    [Tooltip("Seconds both mouse buttons must be held before moving.")]
    public float bothButtonsDelay = 0.06f;

    [Header("Jump / Gravity")]
    public float jumpHeight = 1.5f;
    public float gravity = -20f;
    public float coyoteTime = 0.12f;
    public float jumpBuffer = 0.15f;

    [Header("Fall Damage")]
    public bool enableFallDamage = true;
    public float safeLandingSpeed = 12f;
    public float fallDamagePerUnitSpeed = 4f;

    [Header("Debug")]
    public bool logJumpPresses = false;

    /// <summary>True while walk mode is toggled on.</summary>
    public bool IsWalking { get; private set; }

    /// <summary>True while autorun is active.</summary>
    public bool IsAutoRunning { get; private set; }

    // Read by AnimationBridge for immediate directional animation response.
    public Vector2 AnimationMoveInput { get; private set; }
    public float AnimationMoveSpeed { get; private set; }

    private CharacterController controller;
    private PlayerStats stats;
    private AnimationBridge animationBridge;

    private InputAction moveAction;
    private InputAction strafeAction;
    private InputAction jumpAction;
    private InputAction walkToggleAction;
    private InputAction autoRunAction;
    private InputAction orbitAction;
    private InputAction steerAction;

    private float verticalVelocity;
    private float previousVerticalVelocity;
    private float lastGroundedTime = -99f;
    private float lastJumpPressedTime = -99f;
    private float bothButtonsSince = -1f;
    private float previousRawForward;
    private bool bothButtonsWereEngaged;
    private bool groundedNow;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        stats = GetComponent<PlayerStats>();
        animationBridge = GetComponent<AnimationBridge>();

        PlayerInput playerInput = GetComponent<PlayerInput>();

        moveAction   = playerInput.actions["Move"];
        strafeAction = playerInput.actions["Strafe"];
        jumpAction   = playerInput.actions["Jump"];
        orbitAction  = playerInput.actions["OrbitCamera"];
        steerAction  = playerInput.actions["SteerCharacter"];

        // FindAction returns null instead of throwing if the action is missing.
        walkToggleAction = playerInput.actions.FindAction("WalkToggle");
        if (walkToggleAction == null)
            Debug.LogWarning("PlayerMovement: no 'WalkToggle' action found. " +
                             "Add it to PlayerControls and click Save Asset.", this);

        autoRunAction = playerInput.actions.FindAction("AutoRun");
        if (autoRunAction == null)
            Debug.LogWarning("PlayerMovement: no 'AutoRun' action found. " +
                             "Add it to PlayerControls and click Save Asset.", this);

        IsWalking = startWalking;

        // Prevents the intermittent-jump bug when standing still.
        controller.minMoveDistance = 0f;
    }

    void Update()
    {
        bool dead = stats != null && stats.IsDead;

        // Walk toggle.
        if (walkToggleAction != null && walkToggleAction.WasPressedThisFrame())
            IsWalking = !IsWalking;

        // Autorun toggle.
        if (autoRunAction != null && autoRunAction.WasPressedThisFrame())
            IsAutoRunning = !IsAutoRunning;

        Vector2 move = moveAction.ReadValue<Vector2>();
        float strafe = strafeAction.ReadValue<float>();

        // Both mouse buttons: work out whether they are moving us forward this frame.
        bool bothDown =
            bothButtonsRunForward &&
            orbitAction.IsPressed() &&
            steerAction.IsPressed();

        bool bothEngaged = false;

        if (bothDown)
        {
            if (bothButtonsSince < 0f)
                bothButtonsSince = Time.time;

            bothEngaged =
                Time.time - bothButtonsSince >= bothButtonsDelay &&
                move.y > -0.01f;
        }
        else
        {
            bothButtonsSince = -1f;
        }

        // Cancel autorun on a NEW W/S press or the moment a both-button run starts.
        // Only the start counts, so holding either while turning autorun on doesn't
        // instantly cancel it.
        bool forwardPressedNow =
            Mathf.Abs(move.y) > 0.01f && Mathf.Abs(previousRawForward) <= 0.01f;
        previousRawForward = move.y;

        bool bothStartedNow = bothEngaged && !bothButtonsWereEngaged;
        bothButtonsWereEngaged = bothEngaged;

        if (IsAutoRunning && (forwardPressedNow || bothStartedNow))
            IsAutoRunning = false;

        if (dead)
            IsAutoRunning = false;

        // Apply forward movement from autorun or the mouse buttons.
        if (IsAutoRunning || bothEngaged)
            move.y = 1f;

        float turn = 0f;
        if (adStrafes) strafe += move.x;
        else           turn = move.x;

        // 1. Turn the character.
        if (!dead && Mathf.Abs(turn) > 0.01f)
            transform.Rotate(0f, turn * turnRate * Time.deltaTime, 0f);

        // 2. Horizontal direction along the character's own axes.
        Vector3 moveDir = transform.forward * move.y + transform.right * strafe;
        if (moveDir.sqrMagnitude > 1f) moveDir.Normalize();
        if (dead) moveDir = Vector3.zero;

        // 3. Speed: run by default, walk when toggled. No stamina cost.
        float baseSpeed = IsWalking ? walkSpeed : runSpeed;

        // Animation values use the base speed so the blend tree positions stay simple.
        Vector2 animationInput = new Vector2(strafe, move.y);
        if (animationInput.sqrMagnitude > 1f) animationInput.Normalize();

        AnimationMoveInput = animationInput;
        AnimationMoveSpeed = (dead || moveDir.sqrMagnitude <= 0.01f) ? 0f : baseSpeed;

        float speed = baseSpeed;
        if (move.y < -0.01f) speed *= backSpeedMultiplier;

        // 4. Always poll jump so a press on a bad frame is remembered.
        if (jumpAction.WasPressedThisFrame())
        {
            lastJumpPressedTime = Time.time;

            if (logJumpPresses)
                Debug.Log($"Jump pressed. grounded={groundedNow} " +
                          $"vertVel={verticalVelocity:F2} frame={Time.frameCount}");
        }

        if (groundedNow)
            lastGroundedTime = Time.time;

        // 5. Gravity, with a small downward bias while grounded.
        if (groundedNow && verticalVelocity < 0f)
            verticalVelocity = -2f;
        else
            verticalVelocity += gravity * Time.deltaTime;

        // 6. Jump if pressed recently and grounded recently. No stamina cost.
        bool wantsJump = Time.time - lastJumpPressedTime <= jumpBuffer;
        bool canJump   = Time.time - lastGroundedTime    <= coyoteTime;

        if (wantsJump && canJump && !dead)
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            lastJumpPressedTime = -99f;
            lastGroundedTime    = -99f;

            animationBridge?.NotifyJump();
        }

        // 7. One Move call per frame.
        Vector3 velocity = moveDir * speed + Vector3.up * verticalVelocity;
        controller.Move(velocity * Time.deltaTime);

        // 8. Read grounded once, then check for a hard landing.
        bool wasAirborne = !groundedNow;
        groundedNow = controller.isGrounded;

        if (enableFallDamage && stats != null && groundedNow && wasAirborne)
        {
            float impact = -previousVerticalVelocity;
            if (impact > safeLandingSpeed)
                stats.TakeDamage((impact - safeLandingSpeed) * fallDamagePerUnitSpeed, null);
        }

        previousVerticalVelocity = verticalVelocity;
    }
}
