using UnityEngine;

/// <summary>
/// Drives the shared AC_Player parameters from EnemyAI, so enemies can use
/// the same humanoid controller as the player. Attach to the enemy root.
/// </summary>
[RequireComponent(typeof(EnemyAI))]
[RequireComponent(typeof(Health))]
public class EnemyAnimationBridge : MonoBehaviour
{
    [Header("Animator")]
    public Animator animator;

    [Header("Parameters")]
    public string speedParameter = "Speed";
    public string moveXParameter = "MoveX";
    public string moveYParameter = "MoveY";
    public string groundedParameter = "IsGrounded";
    public string inCombatParameter = "InCombat";
    public string attackTrigger = "AutoAttack";
    public string dieTrigger = "Die";

    [Header("Full Body Attacks")]
    [Tooltip("Synced unmasked layer. Faded in while standing still, like the player.")]
    public string fullBodyLayerName = "FullBodyCast";
    public float fullBodyBlendSpeed = 8f;

    private EnemyAI ai;
    private Health health;
    private int fullBodyLayerIndex = -2;   // -2 = not looked up yet, -1 = not found

    void Awake()
    {
        ai = GetComponent<EnemyAI>();
        health = GetComponent<Health>();
    }

    void OnEnable()
    {
        ai.OnAttack += HandleAttack;
        health.OnDied += HandleDied;
    }

    void OnDisable()
    {
        ai.OnAttack -= HandleAttack;
        health.OnDied -= HandleDied;
    }

    void LateUpdate()
    {
        if (animator == null)
            return;

        float speed = ai.CurrentSpeed;

        // Enemies always face where they move, so only forward motion is sent.
        SetFloatIfPresent(speedParameter, speed);
        SetFloatIfPresent(moveXParameter, 0f);
        SetFloatIfPresent(moveYParameter, speed);
        SetBoolIfPresent(groundedParameter, true);
        SetBoolIfPresent(inCombatParameter, ai.IsInCombat);

        UpdateFullBodyLayer(speed);
    }

    private void UpdateFullBodyLayer(float speed)
    {
        if (fullBodyLayerIndex == -2)
            fullBodyLayerIndex = animator.GetLayerIndex(fullBodyLayerName);

        if (fullBodyLayerIndex < 0)
            return;

        float target = speed < 0.1f ? 1f : 0f;
        float current = animator.GetLayerWeight(fullBodyLayerIndex);
        animator.SetLayerWeight(fullBodyLayerIndex,
            Mathf.MoveTowards(current, target, fullBodyBlendSpeed * Time.deltaTime));
    }

    private void HandleAttack(PlayerStats target)
    {
        TriggerIfPresent(attackTrigger);
    }

    private void HandleDied(Health victim, GameObject killer)
    {
        TriggerIfPresent(dieTrigger);
    }

    // ---- Animator helpers ----

    private void TriggerIfPresent(string name)
    {
        if (animator != null && HasParameter(name, AnimatorControllerParameterType.Trigger))
            animator.SetTrigger(name);
    }

    private void SetFloatIfPresent(string name, float value)
    {
        if (HasParameter(name, AnimatorControllerParameterType.Float))
            animator.SetFloat(name, value);
    }

    private void SetBoolIfPresent(string name, bool value)
    {
        if (HasParameter(name, AnimatorControllerParameterType.Bool))
            animator.SetBool(name, value);
    }

    private bool HasParameter(string name, AnimatorControllerParameterType type)
    {
        if (animator == null || string.IsNullOrWhiteSpace(name))
            return false;

        foreach (AnimatorControllerParameter p in animator.parameters)
            if (p.name == name && p.type == type)
                return true;

        return false;
    }
}
