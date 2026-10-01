using UnityEngine;

/// <summary>
/// Converts gameplay movement and ability events into Animator parameters.
/// Turns the visual model toward the direction of travel and plays a single
/// forward or backward clip instead of strafe and diagonal clips.
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

    [Header("Travel Facing")]
    [Tooltip("While walking forward or sideways, turn the model toward travel direction.")]
    public bool faceWalkDirection = true;

    [Tooltip("While running forward or sideways, turn the model toward travel direction.")]
    public bool faceRunDirection = true;

    [Tooltip("While backing up diagonally, turn the model so its back faces travel direction.")]
    public bool faceBackwardDirection = true;

    [Tooltip("The object to rotate. Drag the Player's 'Visual' child here.")]
    public Transform visualRoot;

    [Tooltip("Degrees per second the model turns.")]
    public float facingTurnSpeed = 720f;

    [Tooltip("Largest turn allowed when moving forward or sideways. 90 = full sideways.")]
    [Range(0f, 90f)]
    public float maxFacingAngle = 90f;

    [Tooltip("Largest turn allowed when backing up. 45 covers the diagonals.")]
    [Range(0f, 90f)]
    public float maxBackwardAngle = 45f;

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

        // Remember the authored rotation, e.g. if Visual is set to 180.
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

    void Update()
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
                // Point the model's back toward travel direction.
                // S = 0, S + E = -45, S + Q = +45.
                targetAngle = Mathf.Atan2(-input.x, -input.y) * Mathf.Rad2Deg;
                targetAngle = Mathf.Clamp(targetAngle, -maxBackwardAngle, maxBackwardAngle);

                // Play the straight backpedal clip.
                animInput = new Vector2(0f, -amount);
            }
            else if (!backward && useForwardFacing)
            {
                // Point the model's front toward travel direction.
                // W = 0, E = +90, Q = -90.
                targetAngle = Mathf.Atan2(input.x, input.y) * Mathf.Rad2Deg;
                targetAngle = Mathf.Clamp(targetAngle, -maxFacingAngle, maxFacingAngle);

                // Play the straight forward clip.
                animInput = new Vector2(0f, amount);
            }
        }

        UpdateVisualFacing(targetAngle);

        SetFloatIfPresent(speedParameter, speed);
        SetFloatIfPresent(moveXParameter, animInput.x * speed);
        SetFloatIfPresent(moveYParameter, animInput.y * speed);

        bool grounded = controller == null || controller.isGrounded;
        SetBoolIfPresent(groundedParameter, grounded);
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
