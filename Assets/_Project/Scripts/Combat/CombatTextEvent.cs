using System;
using UnityEngine;

public enum CombatTextType
{
    Damage,          // auto attack
    CriticalDamage,  // auto attack crit
    AbilityDamage,   // ability hit
    AbilityCritical, // ability crit
    IncomingDamage,  // damage you take
    Healing,
    Experience,
    Evade,           // hit an enemy that's resetting
    EnterCombat,
    LeaveCombat,
    PowerGain,       // e.g. +12 Rage
    LowHealth,
    LowPower
}

public readonly struct CombatTextEvent
{
    public readonly CombatTextType type;
    public readonly float amount;
    public readonly Vector3 worldPosition;
    public readonly GameObject source;
    /// <summary>Optional display text, e.g. "Rage" or "Low Mana".</summary>
    public readonly string label;
    /// <summary>Optional object the text belongs to (the damaged target). Text follows it.</summary>
    public readonly Transform anchor;

    public CombatTextEvent(
        CombatTextType type,
        float amount,
        Vector3 worldPosition,
        GameObject source = null,
        string label = null,
        Transform anchor = null)
    {
        this.type = type;
        this.amount = amount;
        this.worldPosition = worldPosition;
        this.source = source;
        this.label = label;
        this.anchor = anchor;
    }
}

/// <summary>
/// Lightweight global event channel for combat feedback.
/// Gameplay systems publish results; UI systems display them.
/// </summary>
public static class CombatTextBus
{
    public static event Action<CombatTextEvent> OnCombatText;

    public static void Publish(CombatTextEvent combatEvent)
    {
        OnCombatText?.Invoke(combatEvent);
    }
}
