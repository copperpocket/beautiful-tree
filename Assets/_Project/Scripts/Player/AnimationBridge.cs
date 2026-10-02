using UnityEngine;

/// <summary>
/// Converts gameplay movement, combat and ability events into Animator parameters.
/// Turns the visual model toward the direction of travel and plays a single
/// forward or backward clip instead of strafe and diagonal clips.
/// Filters grounded flicker so ramps and small steps don't trigger falling.
/// Holds a combat stance while fighting. Attach to the Player root.
/// </summary>
[RequireComponent(typeof(PlayerAbilities))]
[RequireComponent(typeof(PlayerStats))]
public class AnimationBridge : MonoBehaviour
{
    [Header("Animator")]
    public Animator animator;

    [Header("Movement Parameters")]
    public string speedParameter = "Speed";
    public string moveXParameter = "MoveX";
    public string moveYParameter = "MoveY";
    public string groundedParameter = "IsGrounded";

    [Header("Triggers")]
    public string jumpTrigger = "Jump";
    public string dieTrigger = "Die";

    [Header("Combat Parameters")]
    [Tooltip("Trigger. Fired on every auto-attack swing.")]
    public string autoAttackTrigger = "AutoAttack";
    [Tooltip("Trigger. Instant melee abilities.")]
    public string meleeStrikeTrigger = "MeleeStrike";
    [Tooltip("Trigger. Start of a cast-time ability.")]
    public string spellCastTrigger = "SpellCast";
    [Tooltip("Bool. True while a cast is in progress.")]
    public string isCastingParameter = "IsCasting";
    [Tooltip("Trigger. A cast completed and should be thrown.")]
    public string castReleaseTrigger = "CastRelease";
    [Tooltip("Trigger. Instant spells.")]
    public string spellInstantTrigger = "SpellInstant";

    [Header("Combat Stance")]
    [Tooltip("Bool. True while fighting, plus a short linger afterwards.")]
    public string inCombatParameter = "InCombat";
    [Tooltip("Seconds the stance is held after combat stops.")]
    public float combatStanceLinger = 3f;

    [Header("Movement")]
    public float speedMultiplier = 1f;

    [Header("Jump")]
    [Tooltip("Seconds after takeoff that the Animator is told we're airborne, " +
             "even if the controller still reports grounded.")]
    public float jumpAirborneGrace = 0.15f;

    [Header("Falling")]
    [Tooltip("Seconds off the ground before the Animator treats it as a fall.")]
    public float fallGraceTime = 0.15f;

    [Tooltip("If ground is within this distance below the feet, still count as grounded.")]
    public float groundProbeDistance = 0.4f;

    [Tooltip("Layers the ground probe can hit. Set to Environment.")]
    public LayerMask groundProbeMask = ~0;

    [Header("Travel Facing")]
    public bool faceWalkDirection = true;
    public bool faceRunDirection = true;
    public bool faceBackwardDirection = true;

    [Tooltip("The object to rotate. Drag the Player's 'Visual' child here.")]
    public Transform visualRoot;

    public float facingTurnSpeed = 720f;

    [Range(0f, 90f)] public float maxFacingAngle = 90f;
    [Range(0f, 90f)] public float maxBackwardAngle = 45f;

    private PlayerAbilities abilities;
    private PlayerStats stats;
    private PlayerMovement movement;
    private PlayerCombat combat;
    private CharacterController controller;

    private Quaternion visualBaseRotation = Quaternion.identity;
    private float currentFacingAngle;
    private float lastCombatTime = -99f;

    void Awake()
    {
        abilities = GetComponent<PlayerAbilities>();
        stats = GetComponent<PlayerStats>();
        movement = GetComponent<PlayerMovement>();
        combat = GetComponent<PlayerCombat>();
        controller = GetComponent<CharacterController>();

        if (visualRoot != null)
            visualBaseRotation = visualRoot.localRotation;
    }

    void OnEnable()
    {
        if (abilities != null)
        {
            abilities.OnCastStarted += HandleCastStarted;
            abilities.OnCastCompleted += HandleCastCompleted;
        }

        if (combat != null)
            combat.OnSwing += HandleSwing;

        if (stats != null)
            stats.OnDeath += HandleDeath;
    }

    void OnDisable()
    {
        if (abilities != null)
        {
            abilities.OnCastStarted -= HandleCastStarted;
            abilities.OnCastCompleted -= HandleCastCompleted;
        }

        if (combat != null)
            combat.OnSwing -= HandleSwing;

        if (stats != null)
            stats.OnDeath -= HandleDeath;
    }

    // LateUpdate so we read movement state after PlayerMovement has moved this frame.
    void LateUpdate()
    {
        if (animator == null)
            return;

        UpdateMovementParameters();

        bool casting = abilities != null && abilities.IsCasting;

        // Holds the casting pose. Going false (cancel or completion) lowers the hands,
        // unless CastRelease fired first and the throw plays instead.
        SetBoolIfPresent(isCastingParameter, casting);

        UpdateCombatStance(casting);
    }

    private void UpdateCombatStance(bool casting)
    {
        bool dead = stats != null && stats.IsDead;
        bool fighting = !dead && ((combat != null && combat.inCombat) || casting);

        if (fighting)
            lastCombatTime = Time.time;

        bool inStance = !dead && Time.time - lastCombatTime < combatStanceLinger;
        SetBoolIfPresent(inCombatParameter, inStance);
    }

    private void UpdateMovementParameters()
    {
        if (movement == null)
            return;

        Vector2 input = movement.AnimationMoveInput;
        float rawSpeed = movement.AnimationMoveSpeed;
        float speed = rawSpeed * speedMultiplier;

        bool moving = rawSpeed > 0.01f && input.sqrMagnitude > 0.01f;
        bool walking = moving && rawSpeed < movement.runSpeed - 0.01f;
        bool running = moving && !walking;
        bool backward = input.y < -0.01f;

        bool useForwardFacing =
            (faceWalkDirection && walking) ||
            (faceRunDirection && running);

        float targetAngle = 0f;
        Vector2 animInput = input;
        float amount = Mathf.Min(1f, input.magnitude);

        if (visualRoot != null && moving)
        {
            if (backward && faceBackwardDirection)
            {
                targetAngle = Mathf.Atan2(-input.x, -input.y) * Mathf.Rad2Deg;
                targetAngle = Mathf.Clamp(targetAngle, -maxBackwardAngle, maxBackwardAngle);
                animInput = new Vector2(0f, -amount);
            }
            else if (!backward && useForwardFacing)
            {
                targetAngle = Mathf.Atan2(input.x, input.y) * Mathf.Rad2Deg;
                targetAngle = Mathf.Clamp(targetAngle, -maxFacingAngle, maxFacingAngle);
                animInput = new Vector2(0f, amount);
            }
        }

        UpdateVisualFacing(targetAngle);

        SetFloatIfPresent(speedParameter, speed);
        SetFloatIfPresent(moveXParameter, animInput.x * speed);
        SetFloatIfPresent(moveYParameter, animInput.y * speed);

        bool justJumped = Time.time - movement.LastJumpTime < jumpAirborneGrace;
        bool recentlyGrounded = Time.time - movement.LastTimeGrounded < fallGraceTime;

        bool grounded = !justJumped &&
                        (movement.IsGrounded || recentlyGrounded || ProbeGround());

        SetBoolIfPresent(groundedParameter, grounded);
    }

    /// <summary>Short ray down from the feet. True if ground is close below.</summary>
    private bool ProbeGround()
    {
        if (controller == null)
            return false;

        Vector3 centre = transform.TransformPoint(controller.center);
        Vector3 feet = centre - Vector3.up * (controller.height * 0.5f);
        Vector3 origin = feet + Vector3.up * 0.1f;

        return Physics.Raycast(
            origin,
            Vector3.down,
            groundProbeDistance + 0.1f,
            groundProbeMask,
            QueryTriggerInteraction.Ignore);
    }

    private void UpdateVisualFacing(float targetAngle)
    {
        if (visualRoot == null)
            return;

        currentFacingAngle = Mathf.MoveTowardsAngle(
            currentFacingAngle,
            targetAngle,
            facingTurnSpeed * Time.deltaTime);

        visualRoot.localRotation =
            visualBaseRotation * Quaternion.Euler(0f, currentFacingAngle, 0f);
    }

    // ---- Combat and ability events ----

    private void HandleSwing(Health target)
    {
        TriggerIfPresent(autoAttackTrigger);
    }

    private void HandleCastStarted(Ability ability, float duration)
    {
        // Every cast-time ability raises the hands and holds.
        TriggerIfPresent(spellCastTrigger);
    }

    private void HandleCastCompleted(Ability ability)
    {
        // Offensive spells throw on completion. Heals just lower the hands,
        // which happens automatically when IsCasting goes false.
        if (ability != null && ability.animationType == AbilityAnimation.SpellCast)
            TriggerIfPresent(castReleaseTrigger);
    }

    private void HandleDeath()
    {
        TriggerIfPresent(dieTrigger);
    }

    public void NotifyJump()
    {
        TriggerIfPresent(jumpTrigger);
    }

    /// <summary>Called by PlayerAbilities for instant abilities only.</summary>
    public void NotifyAbility(Ability ability)
    {
        if (ability == null)
            return;

        // Using any ability counts as fighting for the stance.
        lastCombatTime = Time.time;

        string trigger = string.IsNullOrWhiteSpace(ability.animatorTrigger)
            ? GetInstantTriggerFor(ability)
            : ability.animatorTrigger;

        TriggerIfPresent(trigger);
    }

    private string GetInstantTriggerFor(Ability ability)
    {
        switch (ability.animationType)
        {
            case AbilityAnimation.MeleeStrike: return meleeStrikeTrigger;
            case AbilityAnimation.SpellCast:   return spellInstantTrigger;
            case AbilityAnimation.Heal:        return spellInstantTrigger;
            default:                           return "";   // AutoAttack animates per swing instead
        }
    }

    // ---- Animator helpers ----

    private void TriggerIfPresent(string parameterName)
    {
        if (animator == null || string.IsNullOrWhiteSpace(parameterName))
            return;

        if (HasParameter(parameterName, AnimatorControllerParameterType.Trigger))
            animator.SetTrigger(parameterName);
    }

    private void SetFloatIfPresent(string parameterName, float value)
    {
        if (string.IsNullOrWhiteSpace(parameterName))
            return;

        if (HasParameter(parameterName, AnimatorControllerParameterType.Float))
            animator.SetFloat(parameterName, value);
    }

    private void SetBoolIfPresent(string parameterName, bool value)
    {
        if (string.IsNullOrWhiteSpace(parameterName))
            return;

        if (HasParameter(parameterName, AnimatorControllerParameterType.Bool))
            animator.SetBool(parameterName, value);
    }

    private bool HasParameter(string parameterName, AnimatorControllerParameterType expectedType)
    {
        if (animator == null)
            return false;

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.name == parameterName && parameter.type == expectedType)
                return true;
        }

        return false;
    }
}
