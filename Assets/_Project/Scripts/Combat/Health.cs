using System;
using UnityEngine;

/// <summary>
/// Generic health for enemies and destructibles. Attach to the enemy root.
/// Raises OnDied so a spawner can handle the respawn.
/// </summary>
public class Health : MonoBehaviour, IDamageable
{
    [Header("Identity")]
    public string displayName = "Training Dummy";
    public int level = 1;

    [Header("Health")]
    public float maxHealth = 50f;

    [Header("Rewards")]
    public int xpReward = 25;

    [Header("Aim")]
    [Tooltip("Where attacks and the camera aim. Leave empty to use this transform plus aimHeight.")]
    public Transform aimPoint;
    public float aimHeight = 1.0f;

    [Header("Death")]
    [Tooltip("Seconds the corpse stays before the object is destroyed.")]
    public float corpseDuration = 3f;

    [Header("Feedback")]
    [Tooltip("Flashes this renderer's colour when hit. Optional.")]
    public Renderer flashRenderer;
    public Color flashColor = Color.red;
    public float flashDuration = 0.12f;

    // (victim, damage, source)
    public event Action<Health, float, GameObject> OnDamaged;
    public event Action<Health, GameObject> OnDied;
    public event Action<float> OnHealthPercentChanged;

    public bool IsDead { get; private set; }
    public float CurrentHealth { get; private set; }
    public float MaxHealth => maxHealth;
    public string DisplayName => displayName;
    public int Level => level;

    /// <summary>When true, all damage is ignored. Used while an enemy evades home.</summary>
    public bool IsInvulnerable { get; set; }

    public Transform AimPoint => aimPoint != null ? aimPoint : transform;

    public Vector3 AimPosition =>
        aimPoint != null ? aimPoint.position : transform.position + Vector3.up * aimHeight;

    private Material flashMaterial;
    private Color originalColor;
    private float flashUntil = -1f;

    void Awake()
    {
        CurrentHealth = maxHealth;

        if (flashRenderer != null)
        {
            // Instance the material so flashing one enemy doesn't flash them all.
            flashMaterial = flashRenderer.material;
            if (flashMaterial.HasProperty("_BaseColor"))
                originalColor = flashMaterial.GetColor("_BaseColor");
        }
    }

    void Update()
    {
        if (flashUntil > 0f && Time.time >= flashUntil)
        {
            SetFlash(false);
            flashUntil = -1f;
        }
    }

    /// <summary>Required by IDamageable. Normal, non-critical, non-ability damage.</summary>
    public void TakeDamage(float amount, GameObject source)
    {
        TakeDamage(amount, source, false, false);
    }

    /// <summary>Auto-attack style damage that may be critical.</summary>
    public void TakeDamage(float amount, GameObject source, bool critical)
    {
        TakeDamage(amount, source, critical, false);
    }

    /// <summary>Full damage entry point. fromAbility colours the combat text yellow.</summary>
    public void TakeDamage(float amount, GameObject source, bool critical, bool fromAbility)
    {
        if (IsDead || amount <= 0f)
            return;

        // Resetting enemies ignore damage. Say so instead of showing nothing.
        if (IsInvulnerable)
        {
            CombatTextBus.Publish(new CombatTextEvent(
                CombatTextType.Evade, 0f, AimPosition, source, "Evade"));
            return;
        }

        CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);

        OnHealthPercentChanged?.Invoke(CurrentHealth / maxHealth);
        OnDamaged?.Invoke(this, amount, source);

        CombatTextType textType = fromAbility
            ? (critical ? CombatTextType.AbilityCritical : CombatTextType.AbilityDamage)
            : (critical ? CombatTextType.CriticalDamage : CombatTextType.Damage);

        CombatTextBus.Publish(new CombatTextEvent(textType, amount, AimPosition, source));

        SetFlash(true);
        flashUntil = Time.time + flashDuration;

        if (CurrentHealth <= 0f)
            Die(source);
    }

    private void Die(GameObject killer)
    {
        IsDead = true;
        SetFlash(false);

        // Award XP to the killer if it has PlayerStats.
        if (killer != null && xpReward > 0)
        {
            var stats = killer.GetComponentInParent<PlayerStats>();
            if (stats != null) stats.GainXP(xpReward);
        }

        OnDied?.Invoke(this, killer);

        // Temporary death feedback until enemies have a death animation.
        transform.Rotate(90f, 0f, 0f);

        foreach (var c in GetComponentsInChildren<Collider>())
            c.enabled = false;

        Destroy(gameObject, corpseDuration);
    }

    /// <summary>Restores full health. Used by spawners and by evading enemies.</summary>
    public void ResetHealth()
    {
        IsDead = false;
        CurrentHealth = maxHealth;
        OnHealthPercentChanged?.Invoke(1f);

        foreach (var c in GetComponentsInChildren<Collider>())
            c.enabled = true;
    }

    private void SetFlash(bool on)
    {
        if (flashMaterial == null || !flashMaterial.HasProperty("_BaseColor")) return;
        flashMaterial.SetColor("_BaseColor", on ? flashColor : originalColor);
    }
}
