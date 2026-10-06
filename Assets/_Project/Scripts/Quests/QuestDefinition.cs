using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A quest as data. Create via Assets > Create > RPG > Quest.
/// </summary>
[CreateAssetMenu(menuName = "RPG/Quest", fileName = "QD_NewQuest")]
public class QuestDefinition : ScriptableObject
{
    [System.Serializable]
    public class KillObjective
    {
        [Tooltip("Must match the enemy's Health > Display Name exactly.")]
        public string targetName = "Training Dummy";
        public int requiredCount = 5;
    }

    [Header("Identity")]
    [Tooltip("Unique id. Leave empty to use the asset's file name.")]
    public string questId = "";
    public string title = "New Quest";

    [Header("Text")]
    [TextArea(3, 6)] public string description = "";
    [TextArea(2, 4)] public string completionText = "";

    [Header("Requirements")]
    public int requiredLevel = 1;

    [Header("Objectives")]
    public List<KillObjective> objectives = new() { new KillObjective() };

    [Header("Rewards")]
    public int xpReward = 100;

    public string Id => string.IsNullOrEmpty(questId) ? name : questId;
}
