using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Quest window: offer, progress and turn-in. Opened by QuestGiver.
/// Closes when the player walks away. Attach to the Canvas.
/// </summary>
public class QuestDialogUI : MonoBehaviour
{
    [Header("UI")]
    public GameObject root;
    public TMP_Text titleText;
    public TMP_Text bodyText;
    public TMP_Text objectivesText;
    public TMP_Text rewardText;
    public Button acceptButton;
    public Button declineButton;
    public Button completeButton;

    [Header("Behaviour")]
    [Tooltip("The window closes if the player is farther than this from the NPC.")]
    public float closeDistance = 6f;

    public bool IsOpen => root != null && root.activeSelf;

    private QuestGiver giver;
    private QuestDefinition quest;
    private QuestLog log;
    private TMP_Text declineLabel;

    void Awake()
    {
        if (acceptButton != null) acceptButton.onClick.AddListener(OnAccept);
        if (declineButton != null) declineButton.onClick.AddListener(Close);
        if (completeButton != null) completeButton.onClick.AddListener(OnComplete);

        if (declineButton != null)
            declineLabel = declineButton.GetComponentInChildren<TMP_Text>();

        Close();
    }

    void Update()
    {
        if (!IsOpen)
            return;

        if (giver == null || log == null)
        {
            Close();
            return;
        }

        if (Vector3.Distance(log.transform.position, giver.transform.position) > closeDistance)
            Close();
    }

    // ---- Opening ----

    public void ShowOffer(QuestGiver g, QuestDefinition q, QuestLog l)
    {
        Open(g, q, l);
        SetText(q.title, q.description, BuildObjectives(q, null, false));
        SetButtons(accept: true, complete: false, declineText: "Decline");
    }

    public void ShowProgress(QuestGiver g, QuestDefinition q, QuestLog l)
    {
        Open(g, q, l);
        SetText(q.title, "You haven't finished yet. Come back when it's done.",
                BuildObjectives(q, l.Find(q), true));
        SetButtons(accept: false, complete: false, declineText: "Goodbye");
    }

    public void ShowTurnIn(QuestGiver g, QuestDefinition q, QuestLog l)
    {
        Open(g, q, l);
        SetText(q.title, q.completionText, BuildObjectives(q, l.Find(q), true));
        SetButtons(accept: false, complete: true, declineText: "Goodbye");
    }

    public void Close()
    {
        if (root != null) root.SetActive(false);
        giver = null;
        quest = null;
        log = null;
    }

    // ---- Buttons ----

    private void OnAccept()
    {
        if (log != null && quest != null)
            log.Accept(quest);
        Close();
    }

    private void OnComplete()
    {
        if (log != null && quest != null)
            log.TurnIn(quest);
        Close();
    }

    // ---- Helpers ----

    private void Open(QuestGiver g, QuestDefinition q, QuestLog l)
    {
        giver = g;
        quest = q;
        log = l;
        if (root != null) root.SetActive(true);
    }

    private void SetText(string title, string body, string objectives)
    {
        if (titleText) titleText.text = title;
        if (bodyText) bodyText.text = body;
        if (objectivesText) objectivesText.text = objectives;
        if (rewardText) rewardText.text = quest != null ? $"Reward: {quest.xpReward} XP" : "";
    }

    private void SetButtons(bool accept, bool complete, string declineText)
    {
        if (acceptButton) acceptButton.gameObject.SetActive(accept);
        if (completeButton) completeButton.gameObject.SetActive(complete);
        if (declineLabel) declineLabel.text = declineText;
    }

    private static string BuildObjectives(QuestDefinition q, QuestLog.ActiveQuest active, bool showCounts)
    {
        var sb = new StringBuilder("<b>Objectives</b>\n");

        for (int i = 0; i < q.objectives.Count; i++)
        {
            var o = q.objectives[i];

            if (showCounts && active != null)
            {
                int count = active.counts[i];
                string line = $"- {o.targetName}: {count}/{o.requiredCount}";
                sb.AppendLine(count >= o.requiredCount ? $"<color=#9AE59A>{line}</color>" : line);
            }
            else
            {
                sb.AppendLine($"- Kill {o.requiredCount} {o.targetName}");
            }
        }

        return sb.ToString();
    }
}
