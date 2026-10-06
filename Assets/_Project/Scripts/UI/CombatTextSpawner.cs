using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Listens for combat results and creates floating text. The toggles below
/// work like an options menu: unticking a type hides it everywhere.
/// Attach to the Canvas or another persistent UI object.
/// </summary>
public class CombatTextSpawner : MonoBehaviour
{
    [Header("Prefab")]
    [Tooltip("A world-space TextMeshPro prefab with FloatingCombatText attached.")]
    public FloatingCombatText floatingTextPrefab;

    [Header("Position")]
    public Vector3 worldOffset = new Vector3(0f, 0.35f, 0f);

    [Header("Limits")]
    public int maxActiveText = 30;

    [Header("Over Target")]
    public bool showDamageDealt = true;
    public bool showEvade = true;

    [Header("Over You")]
    public bool showDamageTaken = true;
    public bool showHealingReceived = true;
    public bool showExperience = true;
    public bool showCombatState = true;
    public bool showPowerGains = true;
    public bool showLowWarnings = true;

    private readonly List<FloatingCombatText> activeText = new();

    void OnEnable()
    {
        CombatTextBus.OnCombatText += HandleCombatText;
    }

    void OnDisable()
    {
        CombatTextBus.OnCombatText -= HandleCombatText;
    }

    void Update()
    {
        activeText.RemoveAll(item => item == null);
    }

    private bool IsEnabled(CombatTextType type)
    {
        switch (type)
        {
            case CombatTextType.Damage:
            case CombatTextType.CriticalDamage:
            case CombatTextType.AbilityDamage:
            case CombatTextType.AbilityCritical: return showDamageDealt;
            case CombatTextType.Evade:           return showEvade;
            case CombatTextType.IncomingDamage:  return showDamageTaken;
            case CombatTextType.Healing:         return showHealingReceived;
            case CombatTextType.Experience:      return showExperience;
            case CombatTextType.EnterCombat:
            case CombatTextType.LeaveCombat:     return showCombatState;
            case CombatTextType.PowerGain:       return showPowerGains;
            case CombatTextType.LowHealth:
            case CombatTextType.LowPower:        return showLowWarnings;
            default:                             return true;
        }
    }

    private void HandleCombatText(CombatTextEvent combatEvent)
    {
        if (!IsEnabled(combatEvent.type))
            return;

        if (floatingTextPrefab == null)
        {
            Debug.LogWarning("CombatTextSpawner: Floating Text Prefab is not assigned.", this);
            return;
        }

        if (activeText.Count >= maxActiveText)
            return;

        FloatingCombatText instance = Instantiate(
            floatingTextPrefab,
            combatEvent.worldPosition + worldOffset,
            Quaternion.identity);

        instance.Initialize(combatEvent);
        activeText.Add(instance);
    }
}
