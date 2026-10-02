using UnityEngine;

[CreateAssetMenu(menuName = "RPG/Ability/Damage", fileName = "AB_Damage")]
public class DamageAbility : Ability
{
    [Header("Damage")]
    public float damage = 20f;

    [Tooltip("Damage varies by plus or minus this fraction.")]
    public float variance = 0.12f;

    public float critChance = 0.1f;
    public float critMultiplier = 2f;

    [Header("Auto Attack")]
    [Tooltip("Using this ability also starts auto attack, like melee abilities in WoW. " +
             "Leave off for spells.")]
    public bool startsAutoAttack = false;

    public override void Execute(AbilityContext ctx)
    {
        if (ctx.target == null || ctx.target.IsDead)
            return;

        float dealt = damage * Random.Range(1f - variance, 1f + variance);
        bool critical = Random.value < critChance;

        if (critical)
            dealt *= critMultiplier;

        ctx.target.TakeDamage(dealt, ctx.caster, critical);

        Debug.Log($"{displayName} hit {ctx.target.DisplayName} for " +
                  $"{dealt:F0}{(critical ? " CRIT" : "")}");

        // Start swinging, unless the target died from this hit.
        if (startsAutoAttack && ctx.combat != null && !ctx.target.IsDead)
            ctx.combat.EngageAutoAttack();
    }
}
