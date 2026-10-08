using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// An NPC that offers quests and accepts turn-ins through the quest window.
/// Shows ! (available), grey ? (in progress) or yellow ? (ready).
/// Attach to the NPC root.
/// </summary>
public class QuestGiver : MonoBehaviour
{
    [Header("NPC")]
    public string npcName = "Quartermaster";

    [Header("Quests (offered in order)")]
    public List<QuestDefinition> quests = new();

    [Header("UI")]
    [Tooltip("Leave empty to find it automatically.")]
    public QuestDialogUI dialog;

    [Header("Marker")]
    [Tooltip("3D marker above the head.")]
    public QuestMarker marker;
    [Tooltip("Optional fallback: flat text marker, used only if Marker is empty.")]
    public TMP_Text indicator;
    public Color readyColor = new Color(1f, 0.85f, 0.15f);
    public Color inProgressColor = new Color(0.6f, 0.6f, 0.6f);

    private QuestLog playerLog;

    void Start()
    {
        playerLog = FindFirstObjectByType<QuestLog>();

        if (dialog == null)
            dialog = FindFirstObjectByType<QuestDialogUI>();
    }

    void Update()
    {
        UpdateMarker();
    }

    /// <summary>Turn-ins first, then new quests, then progress on active ones.</summary>
    public void Interact(QuestLog log)
    {
        if (log == null)
            return;

        foreach (var q in quests)
        {
            if (log.IsReadyToTurnIn(q))
            {
                if (dialog != null) dialog.ShowTurnIn(this, q, log);
                else log.TurnIn(q);
                return;
            }
        }

        foreach (var q in quests)
        {
            if (log.CanAccept(q))
            {
                if (dialog != null) dialog.ShowOffer(this, q, log);
                else log.Accept(q);
                return;
            }
        }

        foreach (var q in quests)
        {
            if (log.IsActive(q))
            {
                if (dialog != null) dialog.ShowProgress(this, q, log);
                else Debug.Log($"{npcName}: Come back when you've finished \"{q.title}\".");
                return;
            }
        }

        Debug.Log($"{npcName}: I have nothing more for you.");
    }

    private QuestMarkerState GetState()
    {
        if (playerLog == null)
            return QuestMarkerState.None;

        foreach (var q in quests)
            if (playerLog.IsReadyToTurnIn(q)) return QuestMarkerState.Ready;

        foreach (var q in quests)
            if (playerLog.CanAccept(q)) return QuestMarkerState.Available;

        foreach (var q in quests)
            if (playerLog.IsActive(q)) return QuestMarkerState.InProgress;

        return QuestMarkerState.None;
    }

    private void UpdateMarker()
    {
        QuestMarkerState state = GetState();

        if (marker != null)
        {
            marker.SetState(state);
            return;
        }

        if (indicator == null)
            return;

        switch (state)
        {
            case QuestMarkerState.Ready:      Show("?", readyColor); break;
            case QuestMarkerState.Available:  Show("!", readyColor); break;
            case QuestMarkerState.InProgress: Show("?", inProgressColor); break;
            default:                          indicator.text = ""; break;
        }
    }

    private void Show(string symbol, Color color)
    {
        indicator.text = symbol;
        indicator.color = color;
    }
}
