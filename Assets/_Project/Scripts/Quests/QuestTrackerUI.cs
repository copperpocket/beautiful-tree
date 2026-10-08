using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// On-screen list of active quests and their progress. Hides when empty.
/// Attach to the Canvas.
/// </summary>
public class QuestTrackerUI : MonoBehaviour
{
    [Header("Source")]
    public QuestLog questLog;

    [Header("UI")]
    [Tooltip("Hidden when there are no active quests.")]
    public GameObject root;
    public TMP_Text text;

    [Header("Colours")]
    public Color titleColor = new Color(1f, 0.85f, 0.3f);
    public Color doneColor = new Color(0.6f, 0.9f, 0.6f);

    void OnEnable()
    {
        if (questLog == null)
            questLog = FindFirstObjectByType<QuestLog>();

        if (questLog == null)
        {
            Debug.LogWarning("QuestTrackerUI: no QuestLog found.", this);
            return;
        }

        questLog.OnQuestAccepted += HandleChanged;
        questLog.OnQuestProgress += HandleChanged;
        questLog.OnQuestReady += HandleChanged;
        questLog.OnQuestTurnedIn += HandleRemoved;
        questLog.OnQuestAbandoned += HandleRemoved;

        Refresh();
    }

    void OnDisable()
    {
        if (questLog == null) return;

        questLog.OnQuestAccepted -= HandleChanged;
        questLog.OnQuestProgress -= HandleChanged;
        questLog.OnQuestReady -= HandleChanged;
        questLog.OnQuestTurnedIn -= HandleRemoved;
        questLog.OnQuestAbandoned -= HandleRemoved;
    }

    private void HandleChanged(QuestLog.ActiveQuest quest) => Refresh();
    private void HandleRemoved(QuestDefinition quest) => Refresh();

    public void Refresh()
    {
        bool any = questLog != null && questLog.Active.Count > 0;

        if (root) root.SetActive(any);
        if (!any || text == null) return;

        string title = ColorUtility.ToHtmlStringRGB(titleColor);
        string done = ColorUtility.ToHtmlStringRGB(doneColor);
        var sb = new StringBuilder();

        foreach (var q in questLog.Active)
        {
            if (q.IsComplete)
            {
                sb.AppendLine($"<color=#{title}><b>{q.definition.title}</b></color> " +
                              $"<color=#{done}>(Ready)</color>");
                sb.AppendLine($"<color=#{done}>  Return to the quest giver</color>");
            }
            else
            {
                sb.AppendLine($"<color=#{title}><b>{q.definition.title}</b></color>");

                for (int i = 0; i < q.counts.Length; i++)
                {
                    var o = q.definition.objectives[i];
                    string line = $"  {o.targetName}: {q.counts[i]}/{o.requiredCount}";
                    sb.AppendLine(q.counts[i] >= o.requiredCount ? $"<color=#{done}>{line}</color>" : line);
                }
            }

            sb.AppendLine();
        }

        text.text = sb.ToString();
    }
}
