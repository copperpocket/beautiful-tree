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
    [Tooltip("Seconds the corpse stays before the object is destroyed or disabled.")]
    public float corpseDuration = 3f;

    [Header("Feedback")]
    [Tooltip("Flashes this renderer's colour when hit. Optional.")]
    public Renderer flashRenderer;
    public Color flashColor = Color.red;
    public float flashDuration = 0.12f;

    // Existing events used by other systems.
    public event Action<Health, float, GameObject> OnDamaged;
    public event Action<Health, GameObject> OnDied;
    public event Action<float> OnHealthPercentChanged;

    public bool IsDead { get; private set; }
    public float CurrentHealth { get; private set; }
    public float MaxHealth => maxHealth;
    public string DisplayName => displayName;
    public int Level => level;

    public Transform AimPoint => aimPoint != null ? aimPoint : transform;

    public Vector3 AimPosition =>
        aimPoint != null
            ? aimPoint.position
            : transform.position + Vector3.up * aimHeight;

    private Material flashMaterial;
    private Color originalColor;
    private float flashUntil = -1f;

    void Awake()
    {
        CurrentHealth = maxHealth;

        if (flashRenderer != null)
        {
            // Instance the material so one enemy does not flash every enemy.
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

    /// <summary>
    /// Required by IDamageable. Normal damage is not critical.
    /// </summary>
    public void TakeDamage(float amount, GameObject source)
    {
        TakeDamage(amount, source, false);
    }

    /// <summary>
    /// Damage entry point that can identify critical hits.
    /// </summary>
    public void TakeDamage(float amount, GameObject source, bool critical)
    {
        if (IsDead || amount <= 0f)
            return;

        CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);

        OnHealthPercentChanged?.Invoke(CurrentHealth / maxHealth);
        OnDamaged?.Invoke(this, amount, source);

        CombatTextBus.Publish(new CombatTextEvent(
            critical ? CombatTextType.CriticalDamage : CombatTextType.Damage,
            amount,
            AimPosition,
            source));

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

            if (stats != null)
                stats.GainXP(xpReward);
        }

        OnDied?.Invoke(this, killer);

        // Temporary death feedback until animations exist.
        transform.Rotate(90f, 0f, 0f);

        foreach (var collider in GetComponentsInChildren<Collider>())
            collider.enabled = false;

        Destroy(gameObject, corpseDuration);
    }

    /// <summary>
    /// Used by a spawner that reuses the object instead of destroying it.
    /// </summary>
    public void ResetHealth()
    {
        IsDead = false;
        CurrentHealth = maxHealth;

        OnHealthPercentChanged?.Invoke(1f);

        foreach (var collider in GetComponentsInChildren<Collider>())
            collider.enabled = true;
    }

    private void SetFlash(bool on)
    {
        if (flashMaterial == null ||
            !flashMaterial.HasProperty("_BaseColor"))
            return;

        flashMaterial.SetColor(
            "_BaseColor",
            on ? flashColor : originalColor);
    }
}
