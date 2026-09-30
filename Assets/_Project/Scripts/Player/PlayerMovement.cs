using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerInput))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Speed")]
    public float walkSpeed = 4f;
    public float runSpeed = 8f;
    public float backSpeedMultiplier = 0.6f;
    public float turnRate = 180f;

    [Header("Controls")]
    [Tooltip("On: A/D strafe. Off: A/D turn the character.")]
    public bool adStrafes = true;

    [Tooltip("Holding both mouse buttons runs forward.")]
    public bool bothButtonsRunForward = true;

    [Tooltip("Seconds both mouse buttons must be held before running.")]
    public float bothButtonsDelay = 0.06f;

    [Header("Jump / Gravity")]
    public float jumpHeight = 1.5f;
    public float gravity = -20f;
    public float coyoteTime = 0.12f;
    public float jumpBuffer = 0.15f;

    [Header("Stamina Cost")]
    public float sprintStaminaPerSecond = 22f;
    public float jumpStaminaCost = 12f;

    [Header("Fall Damage")]
    public bool enableFallDamage = true;
    public float safeLandingSpeed = 12f;
    public float fallDamagePerUnitSpeed = 4f;

    [Header("Debug")]
    public bool logJumpPresses = false;

    // Read by AnimationBridge for immediate directional animation response.
    public Vector2 AnimationMoveInput { get; private set; }
    public float AnimationMoveSpeed { get; private set; }

    private CharacterController controller;
    private PlayerStats stats;
    private AnimationBridge animationBridge;

    private InputAction moveAction;
    private InputAction strafeAction;
    private InputAction jumpAction;
    private InputAction sprintAction;
    private InputAction orbitAction;
    private InputAction steerAction;

    private float verticalVelocity;
    private float previousVerticalVelocity;
    private float lastGroundedTime = -99f;
    private float lastJumpPressedTime = -99f;
    private float bothButtonsSince = -1f;
    private bool groundedNow;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        stats = GetComponent<PlayerStats>();
        animationBridge = GetComponent<AnimationBridge>();

        PlayerInput playerInput = GetComponent<PlayerInput>();

        moveAction = playerInput.actions["Move"];
        strafeAction = playerInput.actions["Strafe"];
        jumpAction = playerInput.actions["Jump"];
        sprintAction = playerInput.actions["Sprint"];
        orbitAction = playerInput.actions["OrbitCamera"];
        steerAction = playerInput.actions["SteerCharacter"];

        controller.minMoveDistance = 0f;
    }

    void Update()
    {
        bool dead = stats != null && stats.IsDead;

        Vector2 move = moveAction.ReadValue<Vector2>();
        float strafe = strafeAction.ReadValue<float>();

        bool bothDown =
            bothButtonsRunForward &&
            orbitAction.IsPressed() &&
            steerAction.IsPressed();

        if (bothDown)
        {
            if (bothButtonsSince < 0f)
                bothButtonsSince = Time.time;

            if (Time.time - bothButtonsSince >= bothButtonsDelay &&
                move.y > -0.01f)
            {
                move.y = 1f;
            }
        }
        else
        {
            bothButtonsSince = -1f;
        }

        float turn = 0f;

        if (adStrafes)
        {
            // A/D strafe.
            strafe += move.x;
        }
        else
        {
            // A/D turn.
            turn = move.x;
        }

        if (!dead && Mathf.Abs(turn) > 0.01f)
        {
            transform.Rotate(
                0f,
                turn * turnRate * Time.deltaTime,
                0f);
        }

        Vector3 moveDir =
            transform.forward * move.y +
            transform.right * strafe;

        if (moveDir.sqrMagnitude > 1f)
            moveDir.Normalize();

        if (dead)
            moveDir = Vector3.zero;

        // Record directional intent immediately for the Animator.
        Vector2 animationInput = new Vector2(strafe, move.y);

        if (animationInput.sqrMagnitude > 1f)
            animationInput.Normalize();

        AnimationMoveInput = animationInput;

        if (dead || moveDir.sqrMagnitude <= 0.01f)
            AnimationMoveSpeed = 0f;
        else
            AnimationMoveSpeed =
                sprintAction.IsPressed() ? runSpeed : walkSpeed;

        if (jumpAction.WasPressedThisFrame())
        {
            lastJumpPressedTime = Time.time;

            if (logJumpPresses)
            {
                Debug.Log(
                    $"Jump pressed. grounded={groundedNow} " +
                    $"vertVel={verticalVelocity:F2} " +
                    $"frame={Time.frameCount}");
            }
        }

        if (groundedNow)
            lastGroundedTime = Time.time;

        if (groundedNow && verticalVelocity < 0f)
            verticalVelocity = -2f;
        else
            verticalVelocity += gravity * Time.deltaTime;

        bool wantsJump =
            Time.time - lastJumpPressedTime <= jumpBuffer;

        bool canJump =
            Time.time - lastGroundedTime <= coyoteTime;

        if (wantsJump && canJump && !dead)
        {
            bool paid =
                stats == null ||
                jumpStaminaCost <= 0f ||
                stats.TryDrainStamina(jumpStaminaCost);

            if (paid)
            {
                verticalVelocity =
                    Mathf.Sqrt(jumpHeight * -2f * gravity);

                lastJumpPressedTime = -99f;
                lastGroundedTime = -99f;

                animationBridge?.NotifyJump();
            }
            else
            {
                lastJumpPressedTime = -99f;
            }
        }

        bool wantsSprint =
            !dead &&
            sprintAction.IsPressed() &&
            moveDir.sqrMagnitude > 0.01f;

        bool sprinting = false;

        if (wantsSprint)
        {
            sprinting =
                stats == null ||
                stats.TryDrainStamina(
                    sprintStaminaPerSecond * Time.deltaTime);
        }

        float speed = sprinting ? runSpeed : walkSpeed;

        if (move.y < -0.01f)
            speed *= backSpeedMultiplier;

        Vector3 velocity =
            moveDir * speed +
            Vector3.up * verticalVelocity;

        controller.Move(velocity * Time.deltaTime);

        bool wasAirborne = !groundedNow;
        groundedNow = controller.isGrounded;

        if (enableFallDamage &&
            stats != null &&
            groundedNow &&
            wasAirborne)
        {
            float impact = -previousVerticalVelocity;

            if (impact > safeLandingSpeed)
            {
                stats.TakeDamage(
                    (impact - safeLandingSpeed) *
                    fallDamagePerUnitSpeed,
                    null);
            }
        }

        previousVerticalVelocity = verticalVelocity;
    }
}
