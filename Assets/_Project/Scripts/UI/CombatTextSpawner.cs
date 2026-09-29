using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Listens for combat results and creates floating text.
/// Attach this to the Canvas or another persistent UI object.
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

    private void HandleCombatText(CombatTextEvent combatEvent)
    {
        if (floatingTextPrefab == null)
        {
            Debug.LogWarning(
                "CombatTextSpawner: Floating Text Prefab is not assigned.",
                this);

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
