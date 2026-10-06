using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// An NPC that offers quests and accepts turn-ins. Shows ! (available),
/// grey ? (in progress) or yellow ? (ready). Attach to the NPC root.
/// </summary>
public class QuestGiver : MonoBehaviour
{
    [Header("NPC")]
    public string npcName = "Quartermaster";

    [Header("Quests (offered in order)")]
    public List<QuestDefinition> quests = new();

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
    }

    void Update()
    {
        UpdateIndicator();

        if (indicator != null && cam != null)
            indicator.transform.rotation = cam.transform.rotation;
    }

    /// <summary>Turn in a finished quest first, then offer the next available one.</summary>
    public void Interact(QuestLog log)
    {
        if (log == null)
            return;

        foreach (var q in quests)
        {
            if (log.IsReadyToTurnIn(q))
            {
                Debug.Log($"{npcName}: {q.completionText}");
                log.TurnIn(q);
                return;
            }
        }

        foreach (var q in quests)
        {
            if (log.CanAccept(q))
            {
                Debug.Log($"{npcName}: {q.description}");
                log.Accept(q);
                return;
            }
        }

        foreach (var q in quests)
        {
            if (log.IsActive(q))
            {
                Debug.Log($"{npcName}: Come back when you've finished \"{q.title}\".");
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
