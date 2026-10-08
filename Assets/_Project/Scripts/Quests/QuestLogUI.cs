using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Quest log window, toggled with the QuestLog action (L). Lists active quests
/// on the left, details on the right, and lets the player abandon a quest
/// (click Abandon twice to confirm). Attach to the Canvas.
/// </summary>
public class QuestLogUI : MonoBehaviour
{
    [Header("Source")]
    public QuestLog questLog;
    public string toggleActionName = "QuestLog";

    [Header("UI")]
    public GameObject root;
    public RectTransform listContent;
    public TMP_Text emptyText;
    public TMP_Text titleText;
    public TMP_Text bodyText;
    public TMP_Text objectivesText;
    public TMP_Text rewardText;
    public Button abandonButton;
    public Button closeButton;

    [Header("Rows")]
    public Sprite rowSprite;
    public Color rowColor = new Color32(40, 36, 30, 255);
    public Color rowSelectedColor = new Color32(110, 85, 35, 255);
    public Color readyColor = new Color(0.6f, 0.9f, 0.6f);

    public bool IsOpen => root != null && root.activeSelf;

    private InputAction toggleAction;
    private QuestDefinition selected;
    private bool confirmPending;
    private TMP_Text abandonLabel;
    private readonly List<GameObject> rows = new();

    void Awake()
    {
        if (abandonButton != null)
        {
            abandonButton.onClick.AddListener(OnAbandonClicked);
            abandonLabel = abandonButton.GetComponentInChildren<TMP_Text>();
        }

        if (closeButton != null)
            closeButton.onClick.AddListener(Close);

        if (root != null) root.SetActive(false);
    }

    void Start()
    {
        if (questLog == null)
            questLog = FindFirstObjectByType<QuestLog>();

        if (questLog == null)
        {
            Debug.LogWarning("QuestLogUI: no QuestLog found.", this);
            enabled = false;
            return;
        }

        var input = questLog.GetComponent<PlayerInput>();
        toggleAction = input != null ? input.actions.FindAction(toggleActionName) : null;
        if (toggleAction == null)
            Debug.LogWarning($"QuestLogUI: no '{toggleActionName}' action. Add it to " +
                             "PlayerControls (binding L) and click Save Asset.", this);

        questLog.OnQuestAccepted += HandleChanged;
        questLog.OnQuestProgress += HandleChanged;
        questLog.OnQuestReady += HandleChanged;
        questLog.OnQuestTurnedIn += HandleRemoved;
        questLog.OnQuestAbandoned += HandleRemoved;
    }

    void OnDestroy()
    {
        if (questLog == null) return;

        questLog.OnQuestAccepted -= HandleChanged;
        questLog.OnQuestProgress -= HandleChanged;
        questLog.OnQuestReady -= HandleChanged;
        questLog.OnQuestTurnedIn -= HandleRemoved;
        questLog.OnQuestAbandoned -= HandleRemoved;
    }

    void Update()
    {
        if (toggleAction != null && toggleAction.WasPressedThisFrame())
        {
            if (IsOpen) Close();
            else Open();
        }

        if (IsOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            Close();
    }

    // ---- Open / close ----

    public void Open()
    {
        if (root == null) return;
        root.SetActive(true);
        Refresh();
    }

    public void Close()
    {
        if (root != null) root.SetActive(false);
        confirmPending = false;
    }

    private void HandleChanged(QuestLog.ActiveQuest q) { if (IsOpen) Refresh(); }
    private void HandleRemoved(QuestDefinition q) { if (IsOpen) Refresh(); }

    // ---- Content ----

    private void Refresh()
    {
        foreach (var row in rows)
            if (row != null) Destroy(row);
        rows.Clear();

        var active = questLog.Active;

        // Keep the selection if it's still active, otherwise pick the first quest.
        if (selected == null || !questLog.IsActive(selected))
            selected = active.Count > 0 ? active[0].definition : null;

        foreach (var q in active)
            rows.Add(MakeRow(q));

        if (emptyText) emptyText.gameObject.SetActive(active.Count == 0);
        ShowDetails();
    }

    private GameObject MakeRow(QuestLog.ActiveQuest q)
    {
        var go = new GameObject(q.definition.title,
            typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(listContent, false);
        go.GetComponent<LayoutElement>().preferredHeight = 34f;

        var img = go.GetComponent<Image>();
        img.sprite = rowSprite;
        img.color = q.definition == selected ? rowSelectedColor : rowColor;

        var button = go.GetComponent<Button>();
        button.targetGraphic = img;
        var def = q.definition;
        button.onClick.AddListener(() => Select(def));

        var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelGo.transform.SetParent(go.transform, false);
        var rt = (RectTransform)labelGo.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(10f, 0f);
        rt.offsetMax = new Vector2(-6f, 0f);

        var label = labelGo.GetComponent<TextMeshProUGUI>();
        string ready = ColorUtility.ToHtmlStringRGB(readyColor);
        label.text = q.IsComplete
            ? $"{q.definition.title} <color=#{ready}>(Ready)</color>"
            : q.definition.title;
        label.fontSize = 16f;
        label.verticalAlignment = VerticalAlignmentOptions.Middle;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = false;

        return go;
    }

    private void Select(QuestDefinition def)
    {
        selected = def;
        confirmPending = false;
        Refresh();
    }

    private void ShowDetails()
    {
        bool has = selected != null;

        if (abandonButton) abandonButton.gameObject.SetActive(has);
        SetAbandonLabel();

        if (!has)
        {
            if (titleText) titleText.text = "";
            if (bodyText) bodyText.text = "";
            if (objectivesText) objectivesText.text = "";
            if (rewardText) rewardText.text = "";
            return;
        }

        var active = questLog.Find(selected);

        if (titleText) titleText.text = selected.title;
        if (bodyText) bodyText.text = selected.description;
        if (rewardText) rewardText.text = $"Reward: {selected.xpReward} XP";

        if (objectivesText)
        {
            string done = ColorUtility.ToHtmlStringRGB(readyColor);
            var sb = new StringBuilder("<b>Objectives</b>\n");

            for (int i = 0; i < selected.objectives.Count; i++)
            {
                var o = selected.objectives[i];
                int count = active != null ? active.counts[i] : 0;
                string line = $"- {o.targetName}: {count}/{o.requiredCount}";
                sb.AppendLine(count >= o.requiredCount ? $"<color=#{done}>{line}</color>" : line);
            }

            if (active != null && active.IsComplete)
                sb.AppendLine($"\n<color=#{done}>Return to the quest giver.</color>");

            objectivesText.text = sb.ToString();
        }
    }

    // ---- Abandon ----

    private void OnAbandonClicked()
    {
        if (selected == null) return;

        // First click asks for confirmation, second click abandons.
        if (!confirmPending)
        {
            confirmPending = true;
            SetAbandonLabel();
            return;
        }

        questLog.Abandon(selected);
        selected = null;
        confirmPending = false;
        Refresh();
    }

    private void SetAbandonLabel()
    {
        if (abandonLabel) abandonLabel.text = confirmPending ? "Confirm Abandon" : "Abandon";
    }
}
