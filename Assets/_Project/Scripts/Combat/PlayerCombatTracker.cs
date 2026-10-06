using System;
using UnityEngine;

/// <summary>
/// Tracks whether the player is in combat (any enemy is engaged) and warns on
/// low health or power. Publishes floating text for each. Attach to the Player.
/// </summary>
[RequireComponent(typeof(PlayerStats))]
public class PlayerCombatTracker : MonoBehaviour
{
    [Header("Combat State")]
    [Tooltip("Seconds between checks for engaged enemies.")]
    public float checkInterval = 0.25f;

    [Header("Low Warnings")]
    [Range(0f, 1f)] public float lowHealthThreshold = 0.35f;
    [Range(0f, 1f)] public float lowPowerThreshold = 0.2f;
    [Tooltip("Must recover this far above the threshold before warning again.")]
    public float rearmMargin = 0.05f;

    [Header("Text")]
    [Tooltip("Height above the player for these messages.")]
    public float textHeight = 0.9f;

    public bool InCombat { get; private set; }
    public event Action<bool> OnCombatChanged;

    private PlayerStats stats;
    private float nextCheck;
    private bool lowHealthShown;
    private bool lowPowerShown;

    void Awake()
    {
        stats = GetComponent<PlayerStats>();
    }

    void OnEnable()
    {
        stats.OnHealthChanged += HandleHealthChanged;
        stats.OnPowerChanged += HandlePowerChanged;
    }

    void OnDisable()
    {
        stats.OnHealthChanged -= HandleHealthChanged;
        stats.OnPowerChanged -= HandlePowerChanged;
    }

    void Update()
    {
        if (Time.time < nextCheck)
            return;

        nextCheck = Time.time + checkInterval;

        bool engaged = !stats.IsDead && AnyEnemyEngaged();
        if (engaged == InCombat)
            return;

        InCombat = engaged;
        OnCombatChanged?.Invoke(engaged);

        Publish(engaged ? CombatTextType.EnterCombat : CombatTextType.LeaveCombat,
                engaged ? "+Combat" : "-Combat");
    }

    private static bool AnyEnemyEngaged()
    {
        // Fine at this scale. Swap for a registry once there are many enemies.
        foreach (var enemy in FindObjectsByType<EnemyAI>(FindObjectsSortMode.None))
            if (enemy.IsInCombat)
                return true;

        return false;
    }

    private void HandleHealthChanged(float pct)
    {
        if (!lowHealthShown && pct > 0f && pct <= lowHealthThreshold)
        {
            lowHealthShown = true;
            Publish(CombatTextType.LowHealth, "Low Health");
        }
        else if (lowHealthShown && pct > lowHealthThreshold + rearmMargin)
        {
            lowHealthShown = false;
        }
    }

    private void HandlePowerChanged(float pct)
    {
        // Rage starts empty and fills in combat, so "low rage" isn't meaningful.
        if (stats.powerType == PowerType.Rage)
            return;

        if (!lowPowerShown && pct <= lowPowerThreshold)
        {
            lowPowerShown = true;
            Publish(CombatTextType.LowPower, $"Low {stats.powerType}");
        }
        else if (lowPowerShown && pct > lowPowerThreshold + rearmMargin)
        {
            lowPowerShown = false;
        }
    }

    private void Publish(CombatTextType type, string label)
    {
        CombatTextBus.Publish(new CombatTextEvent(
            type, 0f, stats.AimPosition + Vector3.up * textHeight, gameObject, label));
    }
}
