using UnityEngine;

/// <summary>
/// Toggles the auto-attack swing loop. Every class has this and slots it manually,
/// so auto-attack is not special-cased anywhere in the ability system.
/// </summary>
[CreateAssetMenu(menuName = "RPG/Ability/Auto Attack Toggle", fileName = "AB_AutoAttack")]
public class AutoAttackAbility : Ability
{
    public override void Execute(AbilityContext ctx)
    {
        if (ctx.combat == null) return;
        ctx.combat.ToggleAutoAttack();
    }

    /// <summary>
    /// Auto-attack ignores range and facing: engaging from far away is allowed,
    /// the swing loop simply waits until you close the distance.
    /// </summary>
    public override string GetBlockReason(AbilityContext ctx)
    {
        if (ctx.stats != null && ctx.stats.IsDead) return "You are dead";
        if (ctx.target == null || ctx.target.IsDead) return "No valid target";
        return null;
    }
}
