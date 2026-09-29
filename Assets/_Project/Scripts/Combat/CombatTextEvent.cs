using System;
using UnityEngine;

public enum CombatTextType
{
    Damage,
    CriticalDamage,
    Healing,
    Experience
}

public readonly struct CombatTextEvent
{
    public readonly CombatTextType type;
    public readonly float amount;
    public readonly Vector3 worldPosition;
    public readonly GameObject source;

    public CombatTextEvent(
        CombatTextType type,
        float amount,
        Vector3 worldPosition,
        GameObject source = null)
    {
        this.type = type;
        this.amount = amount;
        this.worldPosition = worldPosition;
        this.source = source;
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
