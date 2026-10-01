using UnityEngine;

/// <summary>
/// Converts gameplay movement and ability events into Animator parameters.
/// Turns the visual model toward the direction of travel and plays a single
/// forward or backward clip instead of strafe and diagonal clips.
/// Filters grounded flicker so ramps and small steps don't trigger falling.
/// Attach to the Player root.
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

    [Header("Ability Triggers")]
    public string autoAttackTrigger = "AutoAttack";
    public string meleeStrikeTrigger = "MeleeStrike";
    public string spellCastTrigger = "SpellCast";
    public string healTrigger = "Heal";

    [Header("Movement")]
    public float speedMultiplier = 1f;

    [Header("Jump")]
    [Tooltip("Seconds after takeoff that the Animator is told we're airborne, " +
             "even if the controller still reports grounded.")]
    public float jumpAirborneGrace = 0.15f;

    [Header("Falling")]
    [Tooltip("Seconds off the ground before the Animator treats it as a fall. " +
             "Hides one-frame grounded flicker on ramps.")]
    public float fallGraceTime = 0.15f;

    [Tooltip("If ground is within this distance below the feet, still count as grounded. " +
             "Covers walking down ramps and stepping off small ledges.")]
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
    private CharacterController controller;

    private Quaternion visualBaseRotation = Quaternion.identity;
    private float currentFacingAngle;

    void Awake()
    {
        abilities = GetComponent<PlayerAbilities>();
        stats = GetComponent<PlayerStats>();
        movement = GetComponent<PlayerMovement>();
        controller = GetComponent<CharacterController>();

        if (visualRoot != null)
            visualBaseRotation = visualRoot.localRotation;
    }

    void OnEnable()
    {
        if (abilities != null)
        {
            abilities.OnActionStateChanged += HandleActionStateChanged;
            abilities.OnCastStarted += HandleCastStarted;
        }

        if (stats != null)
            stats.OnDeath += HandleDeath;
    }

    void OnDisable()
    {
        if (abilities != null)
        {
            abilities.OnActionStateChanged -= HandleActionStateChanged;
            abilities.OnCastStarted -= HandleCastStarted;
        }

        if (stats != null)
            stats.OnDeath -= HandleDeath;
    }

    // LateUpdate so we read movement state after PlayerMovement has moved this frame.
    void LateUpdate()
    {
        if (animator == null)
            return;

        UpdateMovementParameters();
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

        // Grounded as the Animator sees it:
        // - Always airborne right after a jump, so takeoff is never skipped.
        // - Otherwise grounded if the controller says so, if we left the ground
        //   only a moment ago, or if ground is just below our feet.
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

        // Bottom of the capsule in world space, nudged up so the ray starts inside it.
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

    private void HandleActionStateChanged(PlayerActionState previous, PlayerActionState next)
    {
        if (animator == null)
            return;

        if (next == PlayerActionState.Casting && abilities.CastingAbility != null)
            PlayAbilityAnimation(abilities.CastingAbility);
    }

    private void HandleCastStarted(Ability ability, float duration)
    {
        if (animator == null || ability == null)
            return;

        PlayAbilityAnimation(ability);
    }

    private void HandleDeath()
    {
        TriggerIfPresent(dieTrigger);
    }

    public void NotifyJump()
    {
        TriggerIfPresent(jumpTrigger);
    }

    public void NotifyAbility(Ability ability)
    {
        if (ability == null)
            return;

        PlayAbilityAnimation(ability);
    }

    private void PlayAbilityAnimation(Ability ability)
    {
        string trigger = string.IsNullOrWhiteSpace(ability.animatorTrigger)
            ? GetTriggerFor(ability)
            : ability.animatorTrigger;

        TriggerIfPresent(trigger);
    }

    private string GetTriggerFor(Ability ability)
    {
        switch (ability.animationType)
        {
            case AbilityAnimation.AutoAttack:  return autoAttackTrigger;
            case AbilityAnimation.MeleeStrike: return meleeStrikeTrigger;
            case AbilityAnimation.SpellCast:   return spellCastTrigger;
            case AbilityAnimation.Heal:        return healTrigger;
            default:                           return "";
        }
    }

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
