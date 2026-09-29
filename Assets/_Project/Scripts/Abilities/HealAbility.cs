using UnityEngine;

[CreateAssetMenu(menuName = "RPG/Ability/Heal", fileName = "AB_Heal")]
public class HealAbility : Ability
{
    [Header("Heal")]
    public float amount = 30f;

    public override void Execute(AbilityContext ctx)
    {
        if (ctx.stats == null) return;

        ctx.stats.Heal(amount);
        Debug.Log($"{displayName} healed you for {amount:F0}");
    }
}
