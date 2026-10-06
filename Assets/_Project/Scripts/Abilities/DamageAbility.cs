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

    [Header("Impact Timing")]
    [Tooltip("Seconds before damage lands if no Impact event arrives.")]
    public float impactFallback = 0.35f;

    public override void Execute(AbilityContext ctx)
    {
        if (ctx.target == null || ctx.target.IsDead)
            return;

        float dealt = damage * Random.Range(1f - variance, 1f + variance);
        bool critical = Random.value < critChance;
        if (critical) dealt *= critMultiplier;

        Health target = ctx.target;
        GameObject caster = ctx.caster;

        void Apply()
        {
            if (target == null || target.IsDead) return;

            // fromAbility = true, so the number shows in yellow.
            target.TakeDamage(dealt, caster, critical, true);
            Debug.Log($"{displayName} hit {target.DisplayName} for " +
                      $"{dealt:F0}{(critical ? " CRIT" : "")}");
        }

        // Swinging starts now, like WoW. The damage waits for the impact frame.
        if (startsAutoAttack && ctx.combat != null)
            ctx.combat.EngageAutoAttack();

        if (ctx.combat != null)
            ctx.combat.QueueHit(Apply, impactFallback);
        else
            Apply();
    }
}
