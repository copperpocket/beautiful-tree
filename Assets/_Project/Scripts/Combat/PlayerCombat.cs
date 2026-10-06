using System;
using UnityEngine;

/// <summary>
/// Auto-attack swing loop. Engaged and disengaged by AutoAttackAbility,
/// right-clicking an enemy, or abilities that start auto attack.
/// Damage is queued at the swing and lands on the animation's Impact event,
/// or after a fallback delay. Also queues ability hits. Attach to the Player.
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
    public float attackRange = 3f;
    public float facingTolerance = 90f;

    [Header("Impact Timing")]
    [Tooltip("Seconds after the swing before damage lands if no Impact event arrives.")]
    public float impactFallback = 0.35f;

    [Header("State")]
    [Tooltip("Read-only. True while auto-attacking.")]
    public bool inCombat;

    /// <summary>Raised at the start of every auto-attack swing.</summary>
    public event Action<Health> OnSwing;

    public float SwingTimeRemaining => Mathf.Max(0f, nextSwingTime - Time.time);

    private PlayerTargeting targeting;
    private PlayerStats stats;
    private AnimationImpactRelay impactRelay;
    private readonly DeferredHitQueue pendingHits = new();

    // Only Swing() pushes this forward, so toggling never grants a free swing.
    private float nextSwingTime;

    void Awake()
    {
        targeting = GetComponent<PlayerTargeting>();
        stats = GetComponent<PlayerStats>();

        // The active model under Visual. Inactive models (old Quaternius) are skipped.
        impactRelay = GetComponentInChildren<AnimationImpactRelay>();
        if (impactRelay == null)
            Debug.LogWarning("PlayerCombat: no AnimationImpactRelay on the player model. " +
                             "Hits will land on the fallback timer.", this);
    }

    void OnEnable()
    {
        targeting.OnTargetChanged += HandleTargetChanged;
        if (impactRelay != null) impactRelay.OnImpact += HandleImpact;
    }

    void OnDisable()
    {
        targeting.OnTargetChanged -= HandleTargetChanged;
        if (impactRelay != null) impactRelay.OnImpact -= HandleImpact;
    }

    void Update()
    {
        if (stats != null && stats.IsDead)
        {
            inCombat = false;
            pendingHits.Clear();
            return;
        }

        pendingHits.Tick();

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
    }

    /// <summary>Called by right-click and by abilities like Strike. Engages, never disengages.</summary>
    public void EngageAutoAttack()
    {
        if (targeting.CurrentTarget == null) return;
        if (stats != null && stats.IsDead) return;

        inCombat = true;
    }

    /// <summary>Queue a hit to land on the next Impact event, or after fallbackDelay.</summary>
    public void QueueHit(Action apply, float fallbackDelay)
    {
        pendingHits.Add(apply, fallbackDelay);
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

        // Animation starts now. Damage waits for the impact frame.
        OnSwing?.Invoke(target);

        float damage = baseDamage * UnityEngine.Random.Range(1f - damageVariance, 1f + damageVariance);
        bool crit = UnityEngine.Random.value < critChance;
        if (crit) damage *= critMultiplier;

        // Rage classes build power by swinging.
        if (stats != null) stats.OnAutoAttackSwing();

        QueueHit(() =>
        {
            if (target == null || target.IsDead) return;
            if (stats != null && stats.IsDead) return;

            target.TakeDamage(damage, gameObject, crit);
        }, impactFallback);
    }

    private void HandleImpact()
    {
        pendingHits.ResolveNext();
    }

    private void HandleTargetChanged(Health newTarget)
    {
        if (newTarget == null) inCombat = false;
    }
}
