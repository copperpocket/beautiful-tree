using UnityEngine;

/// <summary>
/// Auto-attack swing loop. Engaged and disengaged by AutoAttackAbility or by
/// right-clicking an enemy. Attach to the Player.
/// </summary>
[RequireComponent(typeof(PlayerTargeting))]
public class PlayerCombat : MonoBehaviour
{
    [Header("Damage")]
    public float baseDamage = 8f;
    public float damageVariance = 0.15f;
    public float critChance = 0.1f;
    public float critMultiplier = 2f;

    [Header("Swing")]
    public float attackSpeed = 1.8f;
    public float attackRange = 2.5f;
    public float facingTolerance = 90f;

    [Header("State")]
    [Tooltip("Read-only. True while auto-attacking.")]
    public bool inCombat;

    private PlayerTargeting targeting;
    private PlayerStats stats;
    private float nextSwingTime;

    void Awake()
    {
        targeting = GetComponent<PlayerTargeting>();
        stats = GetComponent<PlayerStats>();
    }

    void OnEnable()
    {
        targeting.OnTargetChanged += HandleTargetChanged;
    }

    void OnDisable()
    {
        targeting.OnTargetChanged -= HandleTargetChanged;
    }

    void Update()
    {
        if (stats != null && stats.IsDead)
        {
            inCombat = false;
            return;
        }

        if (!inCombat) return;

        var target = targeting.CurrentTarget;
        if (target == null || target.IsDead)
        {
            inCombat = false;
            return;
        }

        if (Time.time < nextSwingTime) return;
        if (!InRange(target) || !Facing(target)) return;   // wait, don't disengage

        Swing(target);
    }

    /// <summary>Called by AutoAttackAbility.</summary>
    public void ToggleAutoAttack()
    {
        if (targeting.CurrentTarget == null)
        {
            Debug.Log("No target.");
            return;
        }

        inCombat = !inCombat;
        if (inCombat) nextSwingTime = Time.time;
    }

    /// <summary>Called by right-click. Engages, never disengages.</summary>
    public void EngageAutoAttack()
    {
        if (targeting.CurrentTarget == null) return;
        if (stats != null && stats.IsDead) return;

        if (!inCombat)
        {
            inCombat = true;
            nextSwingTime = Time.time;
        }
    }

    private bool InRange(Health target)
    {
        Vector3 d = target.AimPosition - transform.position; d.y = 0f;
        return d.sqrMagnitude <= attackRange * attackRange;
    }

    private bool Facing(Health target)
    {
        Vector3 d = target.AimPosition - transform.position; d.y = 0f;
        return Vector3.Angle(transform.forward, d) <= facingTolerance;
    }

    private void Swing(Health target)
    {
        nextSwingTime = Time.time + attackSpeed;

        float damage = baseDamage * Random.Range(1f - damageVariance, 1f + damageVariance);
        bool crit = Random.value < critChance;
        if (crit) damage *= critMultiplier;

        target.TakeDamage(damage, gameObject, crit);

        // Rage classes build power by swinging.
        if (stats != null) stats.OnAutoAttackSwing();

        Debug.Log($"Swing hit {target.DisplayName} for {damage:F0}{(crit ? " CRIT" : "")}. " +
                  $"{target.CurrentHealth:F0}/{target.MaxHealth:F0} left");
    }

    private void HandleTargetChanged(Health newTarget)
    {
        if (newTarget == null) inCombat = false;
    }
}
