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

    [Header("Indicator (optional 3D TextMeshPro above the head)")]
    public TMP_Text indicator;
    public Color readyColor = new Color(1f, 0.85f, 0.15f);
    public Color inProgressColor = new Color(0.6f, 0.6f, 0.6f);

    private QuestLog playerLog;
    private Camera cam;

    void Start()
    {
        playerLog = FindFirstObjectByType<QuestLog>();
        cam = Camera.main;

        if (dialog == null)
            dialog = FindFirstObjectByType<QuestDialogUI>();
    }

    void Update()
    {
        UpdateIndicator();

        if (indicator != null && cam != null)
            indicator.transform.rotation = cam.transform.rotation;
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

    private void UpdateIndicator()
    {
        if (indicator == null)
            return;

        if (playerLog == null)
        {
            indicator.text = "";
            return;
        }

        foreach (var q in quests)
            if (playerLog.IsReadyToTurnIn(q)) { Show("?", readyColor); return; }

        foreach (var q in quests)
            if (playerLog.CanAccept(q)) { Show("!", readyColor); return; }

        foreach (var q in quests)
            if (playerLog.IsActive(q)) { Show("?", inProgressColor); return; }

        indicator.text = "";
    }

    private void Show(string symbol, Color color)
    {
        indicator.text = symbol;
        indicator.color = color;
    }
}
