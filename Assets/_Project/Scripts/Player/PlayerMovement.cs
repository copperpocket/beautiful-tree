using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerInput))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Speed")]
    public float walkSpeed = 4f;
    public float runSpeed = 8f;
    public float backSpeedMultiplier = 0.6f;   // backs up slower than it runs
    public float turnRate = 180f;              // degrees per second for A/D

    [Header("Controls")]
    [Tooltip("On: A/D strafe. Off: A/D turn the character, WoW default.")]
    public bool adStrafes = false;

    [Tooltip("Holding both mouse buttons runs forward, WoW style.")]
    public bool bothButtonsRunForward = true;

    [Tooltip("Seconds both buttons must be held before running, stops stray clicks lurching.")]
    public float bothButtonsDelay = 0.06f;

    [Header("Jump / Gravity")]
    public float jumpHeight = 1.5f;
    public float gravity = -20f;
    public float coyoteTime = 0.12f;           // grace period after leaving ground
    public float jumpBuffer = 0.15f;           // grace period before landing

    [Header("Stamina Cost (ignored if no PlayerStats)")]
    public float sprintStaminaPerSecond = 22f;
    public float jumpStaminaCost = 12f;

    [Header("Fall Damage")]
    public bool enableFallDamage = true;
    [Tooltip("Downward speed below which a landing is harmless.")]
    public float safeLandingSpeed = 12f;
    public float fallDamagePerUnitSpeed = 4f;

    [Header("Debug")]
    public bool logJumpPresses = false;

    private CharacterController controller;
    private PlayerStats stats;                 // optional
    private InputAction moveAction, strafeAction, jumpAction, sprintAction;
    private InputAction orbitAction, steerAction;

    private float verticalVelocity;
    private float previousVerticalVelocity;
    private float lastGroundedTime = -99f;
    private float lastJumpPressedTime = -99f;
    private float bothButtonsSince = -1f;
    private bool groundedNow;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        stats = GetComponent<PlayerStats>();   // null is fine

        var playerInput = GetComponent<PlayerInput>();
        moveAction   = playerInput.actions["Move"];
        strafeAction = playerInput.actions["Strafe"];
        jumpAction   = playerInput.actions["Jump"];
        sprintAction = playerInput.actions["Sprint"];
        orbitAction  = playerInput.actions["OrbitCamera"];
        steerAction  = playerInput.actions["SteerCharacter"];

        // Min Move Distance > 0 silently discards zero-length moves, which breaks
        // isGrounded when standing still. Force it off so an Inspector value
        // can't reintroduce the intermittent-jump bug.
        controller.minMoveDistance = 0f;
    }

    void Update()
    {
        bool dead = stats != null && stats.IsDead;

        Vector2 move = moveAction.ReadValue<Vector2>();   // x = A/D, y = W/S
        float strafe = strafeAction.ReadValue<float>();   // Q/E

        // Both mouse buttons held = run forward.
        bool bothDown = bothButtonsRunForward &&
                        orbitAction.IsPressed() && steerAction.IsPressed();

        if (bothDown)
        {
            if (bothButtonsSince < 0f) bothButtonsSince = Time.time;

            if (Time.time - bothButtonsSince >= bothButtonsDelay && move.y > -0.01f)
                move.y = 1f;
        }
        else
        {
            bothButtonsSince = -1f;
        }

        float turn = 0f;
        if (adStrafes) strafe += move.x;
        else           turn = move.x;

        // 1. Turn the character. ThirdPersonCamera picks this up via Mathf.DeltaAngle.
        if (!dead && Mathf.Abs(turn) > 0.01f)
            transform.Rotate(0f, turn * turnRate * Time.deltaTime, 0f);

        // 2. Horizontal direction, along the character's own axes.
        Vector3 moveDir = transform.forward * move.y + transform.right * strafe;
        if (moveDir.sqrMagnitude > 1f) moveDir.Normalize();
        if (dead) moveDir = Vector3.zero;

        // 3. ALWAYS poll jump, grounded or not, so a press on a bad frame survives.
        if (jumpAction.WasPressedThisFrame())
        {
            lastJumpPressedTime = Time.time;

            if (logJumpPresses)
                Debug.Log($"Jump pressed. grounded={groundedNow} " +
                          $"vertVel={verticalVelocity:F2} frame={Time.frameCount}");
        }

        // 4. Grounded state from last frame's single Move call.
        if (groundedNow)
            lastGroundedTime = Time.time;

        // 5. Gravity, with a small downward bias while grounded and not rising.
        if (groundedNow && verticalVelocity < 0f)
            verticalVelocity = -2f;
        else
            verticalVelocity += gravity * Time.deltaTime;

        // 6. Jump if pressed recently AND grounded recently. Stamina charged only
        //    once the jump is certain, so a buffered press never wastes stamina.
        bool wantsJump = Time.time - lastJumpPressedTime <= jumpBuffer;
        bool canJump   = Time.time - lastGroundedTime    <= coyoteTime;

        if (wantsJump && canJump && !dead)
        {
            bool paid = stats == null || jumpStaminaCost <= 0f ||
                        stats.TryDrainStamina(jumpStaminaCost);

            if (paid)
            {
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
                lastJumpPressedTime = -99f;
                lastGroundedTime    = -99f;
            }
            else
            {
                lastJumpPressedTime = -99f;   // consume it, don't retry every frame
            }
        }

        // 7. Speed. Sprinting drains stamina and drops to walk when empty.
        bool wantsSprint = !dead && sprintAction.IsPressed() &&
                           moveDir.sqrMagnitude > 0.01f;
        bool sprinting = false;

        if (wantsSprint)
            sprinting = stats == null ||
                        stats.TryDrainStamina(sprintStaminaPerSecond * Time.deltaTime);

        float speed = sprinting ? runSpeed : walkSpeed;
        if (move.y < -0.01f) speed *= backSpeedMultiplier;

        // 8. ONE Move call per frame, horizontal and vertical combined.
        Vector3 velocity = moveDir * speed + Vector3.up * verticalVelocity;
        controller.Move(velocity * Time.deltaTime);

        // 9. Read grounded once, immediately after the move that produced it,
        //    then check for a hard landing. null source = no attacker, it was terrain.
        bool wasAirborne = !groundedNow;
        groundedNow = controller.isGrounded;

        if (enableFallDamage && stats != null && groundedNow && wasAirborne)
        {
            float impact = -previousVerticalVelocity;   // positive when falling
            if (impact > safeLandingSpeed)
                stats.TakeDamage((impact - safeLandingSpeed) * fallDamagePerUnitSpeed, null);
        }

        previousVerticalVelocity = verticalVelocity;
    }
}
