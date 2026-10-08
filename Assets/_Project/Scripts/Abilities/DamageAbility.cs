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
    [Tooltip("Seconds before damage lands (or the projectile launches) if no Impact event arrives.")]
    public float impactFallback = 0.35f;

    [Header("Projectile (optional)")]
    [Tooltip("Leave empty for instant damage, e.g. Strike.")]
    public Projectile projectilePrefab;
    [Tooltip("Metres per second.")]
    public float projectileSpeed = 18f;
    [Tooltip("Launch from the left hand instead of the right.")]
    public bool launchFromLeftHand = false;
    [Tooltip("Offset from the hand, in the caster's local space.")]
    public Vector3 launchOffset = new Vector3(0f, 0f, 0.25f);

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

        // On the impact frame: launch a projectile, or hit straight away.
        System.Action onImpact = Apply;

        if (projectilePrefab != null)
        {
            onImpact = () =>
            {
                if (target == null || target.IsDead || caster == null) return;

                Vector3 origin = GetLaunchPoint(caster);
                Vector3 dir = target.AimPosition - origin;
                Quaternion rot = dir.sqrMagnitude > 0.001f ? Quaternion.LookRotation(dir) : caster.transform.rotation;

                Projectile p = Instantiate(projectilePrefab, origin, rot);
                p.Launch(target, projectileSpeed, Apply);
            };
        }

        if (ctx.combat != null)
            ctx.combat.QueueHit(onImpact, impactFallback);
        else
            onImpact();
    }

    /// <summary>The casting hand, or chest height in front of the caster if there's no humanoid model.</summary>
    private Vector3 GetLaunchPoint(GameObject caster)
    {
        Animator animator = null;

        var bridge = caster.GetComponent<AnimationBridge>();
        if (bridge != null && bridge.animator != null)
            animator = bridge.animator;
        if (animator == null)
            animator = caster.GetComponentInChildren<Animator>();

        Vector3 offset = caster.transform.TransformVector(launchOffset);

        if (animator != null && animator.isHuman)
        {
            Transform hand = animator.GetBoneTransform(
                launchFromLeftHand ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            if (hand != null)
                return hand.position + offset;
        }

        return caster.transform.position + Vector3.up * 1.4f + caster.transform.forward * 0.5f + offset;
    }
}
