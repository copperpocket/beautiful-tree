using UnityEngine;

/// <summary>
/// Converts gameplay movement and ability events into Animator parameters.
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

    private PlayerAbilities abilities;
    private PlayerStats stats;
    private PlayerMovement movement;
    private CharacterController controller;

    void Awake()
    {
        abilities = GetComponent<PlayerAbilities>();
        stats = GetComponent<PlayerStats>();
        movement = GetComponent<PlayerMovement>();
        controller = GetComponent<CharacterController>();
    }

    void OnEnable()
    {
        if (abilities != null)
        {
            abilities.OnActionStateChanged +=
                HandleActionStateChanged;

            abilities.OnCastStarted += HandleCastStarted;
        }

        if (stats != null)
            stats.OnDeath += HandleDeath;
    }

    void OnDisable()
    {
        if (abilities != null)
        {
            abilities.OnActionStateChanged -=
                HandleActionStateChanged;

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
        float movementSpeed = movement.AnimationMoveSpeed * speedMultiplier;

        SetFloatIfPresent(speedParameter, movementSpeed);
        SetFloatIfPresent(moveXParameter, input.x * movementSpeed);
        SetFloatIfPresent(moveYParameter, input.y * movementSpeed);


        bool grounded =
            controller == null ||
            controller.isGrounded;

        SetBoolIfPresent(
            groundedParameter,
            grounded);
    }

    private void HandleActionStateChanged(
        PlayerActionState previous,
        PlayerActionState next)
    {
        if (animator == null)
            return;

        if (next == PlayerActionState.Casting &&
            abilities.CastingAbility != null)
        {
            PlayAbilityAnimation(
                abilities.CastingAbility);
        }
    }

    private void HandleCastStarted(
        Ability ability,
        float duration)
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
        string trigger =
            string.IsNullOrWhiteSpace(ability.animatorTrigger)
                ? GetTriggerFor(ability)
                : ability.animatorTrigger;

        TriggerIfPresent(trigger);
    }

    private string GetTriggerFor(Ability ability)
    {
        switch (ability.animationType)
        {
            case AbilityAnimation.AutoAttack:
                return autoAttackTrigger;

            case AbilityAnimation.MeleeStrike:
                return meleeStrikeTrigger;

            case AbilityAnimation.SpellCast:
                return spellCastTrigger;

            case AbilityAnimation.Heal:
                return healTrigger;

            default:
                return "";
        }
    }

    private void TriggerIfPresent(string parameterName)
    {
        if (animator == null ||
            string.IsNullOrWhiteSpace(parameterName))
        {
            return;
        }

        if (HasParameter(
                parameterName,
                AnimatorControllerParameterType.Trigger))
        {
            animator.SetTrigger(parameterName);
        }
    }

    private void SetFloatIfPresent(
        string parameterName,
        float value)
    {
        if (string.IsNullOrWhiteSpace(parameterName))
            return;

        if (HasParameter(
                parameterName,
                AnimatorControllerParameterType.Float))
        {
            animator.SetFloat(parameterName, value);
        }
    }

    private void SetBoolIfPresent(
        string parameterName,
        bool value)
    {
        if (string.IsNullOrWhiteSpace(parameterName))
            return;

        if (HasParameter(
                parameterName,
                AnimatorControllerParameterType.Bool))
        {
            animator.SetBool(parameterName, value);
        }
    }

    private bool HasParameter(
        string parameterName,
        AnimatorControllerParameterType expectedType)
    {
        if (animator == null)
            return false;

        foreach (
            AnimatorControllerParameter parameter
            in animator.parameters)
        {
            if (parameter.name == parameterName &&
                parameter.type == expectedType)
            {
                return true;
            }
        }

        return false;
    }
}
