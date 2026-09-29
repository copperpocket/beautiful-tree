using UnityEngine;

/// <summary>Everything an ability needs to know about who is using it and on what.</summary>
public struct AbilityContext
{
    public GameObject caster;
    public PlayerStats stats;
    public PlayerAbilities abilities;
    public PlayerCombat combat;
    public Health target;
}

/// <summary>
/// Base ability. Create concrete assets from the subclasses via
/// Assets > Create > RPG > Ability > ...
/// </summary>
public abstract class Ability : ScriptableObject
{
    [Header("Display")]
    public string displayName = "New Ability";
    [TextArea(2, 4)] public string description = "";
    public Sprite icon;
    [Tooltip("Used as the slot colour when no icon is assigned.")]
    public Color iconTint = new Color(0.35f, 0.45f, 0.65f);

    [Header("Cost and Timing")]
    public float powerCost = 0f;
    [Tooltip("Seconds before this ability can be used again. 0 = no cooldown.")]
    public float cooldown = 0f;
    [Tooltip("Seconds to cast. 0 = instant.")]
    public float castTime = 0f;
    [Tooltip("Puts the global cooldown on every ability that respects it.")]
    public bool triggersGCD = true;
    [Tooltip("Off: moving cancels the cast. Ignored for instants.")]
    public bool usableWhileMoving = false;

    [Header("Targeting")]
    public bool requiresTarget = true;
    [Tooltip("Metres. Ignored when requiresTarget is off.")]
    public float range = 5f;
    [Tooltip("Target must be roughly in front of the caster.")]
    public bool requiresFacing = true;
    [Tooltip("Degrees off-centre the target may be.")]
    public float facingTolerance = 100f;

    /// <summary>Runs when the cast completes, or immediately for instants.</summary>
    public abstract void Execute(AbilityContext ctx);

    /// <summary>
    /// Validation shared by every ability. Returns a reason string when unusable,
    /// or null when good to go.
    /// </summary>
    public virtual string GetBlockReason(AbilityContext ctx)
    {
        if (ctx.stats != null && ctx.stats.IsDead) return "You are dead";

        if (ctx.stats != null && !ctx.stats.HasPower(powerCost))
            return $"Not enough {ctx.stats.powerType}";

        if (!requiresTarget) return null;

        if (ctx.target == null || ctx.target.IsDead) return "No valid target";

        Vector3 toTarget = ctx.target.AimPosition - ctx.caster.transform.position;
        Vector3 flat = toTarget; flat.y = 0f;

        if (flat.sqrMagnitude > range * range) return "Out of range";

        if (requiresFacing &&
            Vector3.Angle(ctx.caster.transform.forward, flat) > facingTolerance)
            return "Target is not in front of you";

        return null;
    }
}
