using UnityEngine;

/// <summary>
/// Anything that can be damaged. Implemented by Health (enemies, props)
/// and by PlayerStats, so one attack path hits everything.
/// </summary>
public interface IDamageable
{
    bool IsDead { get; }
    float CurrentHealth { get; }
    float MaxHealth { get; }

    /// <summary>Display name for the target frame.</summary>
    string DisplayName { get; }

    int Level { get; }

    /// <param name="source">Who dealt it. May be null for falls, traps, etc.</param>
    void TakeDamage(float amount, GameObject source);

    /// <summary>The transform to aim at, usually chest height.</summary>
    Transform AimPoint { get; }
}
