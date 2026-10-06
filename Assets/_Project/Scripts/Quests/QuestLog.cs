using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The player's quests: accepts, tracks kills, and turns in for XP.
/// Attach to the Player.
/// </summary>
[RequireComponent(typeof(PlayerStats))]
public class QuestLog : MonoBehaviour
{
    public class ActiveQuest
    {
        public QuestDefinition definition;
        public int[] counts;

        public bool IsComplete
        {
            get
            {
                for (int i = 0; i < counts.Length; i++)
                    if (counts[i] < definition.objectives[i].requiredCount)
                        return false;
                return true;
            }
        }
    }

    [Header("Debug")]
    public bool logProgress = true;

    public event Action<ActiveQuest> OnQuestAccepted;
    public event Action<ActiveQuest> OnQuestProgress;
    public event Action<ActiveQuest> OnQuestReady;
    public event Action<QuestDefinition> OnQuestTurnedIn;

    public IReadOnlyList<ActiveQuest> Active => active;

    private readonly List<ActiveQuest> active = new();
    private readonly HashSet<string> completed = new();
    private PlayerStats stats;

    void Awake() => stats = GetComponent<PlayerStats>();
    void OnEnable() => KillReporter.OnAnyKilled += HandleKill;
    void OnDisable() => KillReporter.OnAnyKilled -= HandleKill;

    // ---- Queries ----

    public ActiveQuest Find(QuestDefinition def) =>
        def == null ? null : active.Find(q => q.definition.Id == def.Id);

    public bool IsActive(QuestDefinition def) => Find(def) != null;

    public bool HasCompleted(QuestDefinition def) => def != null && completed.Contains(def.Id);

    public bool IsReadyToTurnIn(QuestDefinition def)
    {
        var q = Find(def);
        return q != null && q.IsComplete;
    }

    public bool CanAccept(QuestDefinition def) =>
        def != null && !IsActive(def) && !HasCompleted(def) && stats.level >= def.requiredLevel;

    // ---- Actions ----

    public bool Accept(QuestDefinition def)
    {
        if (!CanAccept(def))
            return false;

        var quest = new ActiveQuest
        {
            definition = def,
            counts = new int[def.objectives.Count]
        };

        active.Add(quest);

        if (logProgress)
            Debug.Log($"Quest accepted: {def.title}");

        OnQuestAccepted?.Invoke(quest);
        return true;
    }

    public bool TurnIn(QuestDefinition def)
    {
        var quest = Find(def);
        if (quest == null || !quest.IsComplete)
            return false;

        active.Remove(quest);
        completed.Add(def.Id);

        if (def.xpReward > 0)
            stats.GainXP(def.xpReward);

        if (logProgress)
            Debug.Log($"Quest complete: {def.title} (+{def.xpReward} XP)");

        OnQuestTurnedIn?.Invoke(def);
        return true;
    }

    // ---- Kill tracking ----

    private void HandleKill(Health victim, GameObject killer)
    {
        if (victim == null || killer == null)
            return;

        // Only count kills made by this player.
        if (killer.GetComponentInParent<QuestLog>() != this)
            return;

        foreach (var quest in active)
        {
            bool wasComplete = quest.IsComplete;
            bool changed = false;

            for (int i = 0; i < quest.counts.Length; i++)
            {
                var objective = quest.definition.objectives[i];

                if (objective.targetName != victim.DisplayName)
                    continue;

                if (quest.counts[i] >= objective.requiredCount)
                    continue;

                quest.counts[i]++;
                changed = true;

                if (logProgress)
                    Debug.Log($"{quest.definition.title}: {objective.targetName} " +
                              $"{quest.counts[i]}/{objective.requiredCount}");
            }

            if (!changed)
                continue;

            OnQuestProgress?.Invoke(quest);

            if (!wasComplete && quest.IsComplete)
            {
                if (logProgress)
                    Debug.Log($"{quest.definition.title}: ready to turn in");

                OnQuestReady?.Invoke(quest);
            }
        }
    }
}
