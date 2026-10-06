using System;
using UnityEngine;

/// <summary>
/// Reports this enemy's death to a global event that quests listen to.
/// Attach to the enemy root next to Health.
/// </summary>
[RequireComponent(typeof(Health))]
public class KillReporter : MonoBehaviour
{
    /// <summary>(victim, killer). Raised for every enemy with a KillReporter.</summary>
    public static event Action<Health, GameObject> OnAnyKilled;

    private Health health;

    void Awake() => health = GetComponent<Health>();
    void OnEnable() => health.OnDied += Report;
    void OnDisable() => health.OnDied -= Report;

    private void Report(Health victim, GameObject killer) => OnAnyKilled?.Invoke(victim, killer);
}
