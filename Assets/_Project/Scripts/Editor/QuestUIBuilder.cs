using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Builds the quest window and quest tracker. Select a Canvas, then
/// Tools > UI > Build Quest UI. Delete QuestDialog and QuestTracker before re-running.
/// </summary>
public static class QuestUIBuilder
{
    [MenuItem("Tools/UI/Build Quest UI")]
    private static void Build()
    {
        var canvas = Selection.activeGameObject != null
            ? Selection.activeGameObject.GetComponentInParent<Canvas>()
            : Object.FindFirstObjectByType<Canvas>();

        if (canvas == null)
        {
            EditorUtility.DisplayDialog("No Canvas", "Select a Canvas first.", "OK");
            return;
        }

        EnsureClickable(canvas);

        var sprite = UISpriteUtility.GetOrCreate();
        var questLog = Object.FindFirstObjectByType<QuestLog>();

        // ---------------- Quest window ----------------
        var dialog = MakeRect("QuestDialog", canvas.transform,
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(40f, 40f), new Vector2(380f, 460f));

        // Blocks clicks, so the world and camera ignore clicks on the window.
        var bg = MakeImage("DialogBG", dialog, sprite, new Color32(18, 16, 14, 240), raycast: true);
        Stretch(bg.rectTransform, 0f);

        var title = MakeText("QuestTitle", dialog, "Training Day", 22f,
            new Color32(255, 215, 90, 255), wrap: false);
        TopLeft(title.rectTransform, new Vector2(20f, -16f), new Vector2(340f, 34f));

        var body = MakeText("QuestBody", dialog, "Quest description goes here.", 15f,
            new Color32(230, 225, 210, 255), wrap: true);
        TopLeft(body.rectTransform, new Vector2(20f, -58f), new Vector2(340f, 200f));
        body.verticalAlignment = VerticalAlignmentOptions.Top;

        var objectives = MakeText("QuestObjectives", dialog, "<b>Objectives</b>\n- Kill 5 Training Dummy", 15f,
            Color.white, wrap: true);
        TopLeft(objectives.rectTransform, new Vector2(20f, -268f), new Vector2(340f, 90f));
        objectives.verticalAlignment = VerticalAlignmentOptions.Top;

        var reward = MakeText("QuestReward", dialog, "Reward: 100 XP", 15f,
            new Color32(190, 150, 255, 255), wrap: false);
        TopLeft(reward.rectTransform, new Vector2(20f, -362f), new Vector2(340f, 26f));

        var accept = MakeButton("AcceptButton", dialog, sprite, "Accept",
            new Color32(60, 110, 50, 255), new Vector2(20f, 16f));
        var complete = MakeButton("CompleteButton", dialog, sprite, "Complete",
            new Color32(150, 120, 40, 255), new Vector2(20f, 16f));
        var decline = MakeButton("DeclineButton", dialog, sprite, "Decline",
            new Color32(90, 40, 40, 255), new Vector2(200f, 16f));

        var dialogUI = canvas.GetComponent<QuestDialogUI>();
        if (dialogUI == null) dialogUI = Undo.AddComponent<QuestDialogUI>(canvas.gameObject);
        dialogUI.root = dialog.gameObject;
        dialogUI.titleText = title;
        dialogUI.bodyText = body;
        dialogUI.objectivesText = objectives;
        dialogUI.rewardText = reward;
        dialogUI.acceptButton = accept;
        dialogUI.completeButton = complete;
        dialogUI.declineButton = decline;
        EditorUtility.SetDirty(dialogUI);

        // ---------------- Quest tracker ----------------
        var tracker = MakeRect("QuestTracker", canvas.transform,
            new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-20f, -260f), new Vector2(300f, 220f));

        var trackerText = MakeText("TrackerText", tracker,
            "<b>Training Day</b>\n  Training Dummy: 2/5", 15f, Color.white, wrap: true);
        Stretch(trackerText.rectTransform, 0f);
        trackerText.verticalAlignment = VerticalAlignmentOptions.Top;

        var trackerUI = canvas.GetComponent<QuestTrackerUI>();
        if (trackerUI == null) trackerUI = Undo.AddComponent<QuestTrackerUI>(canvas.gameObject);
        trackerUI.questLog = questLog;
        trackerUI.root = tracker.gameObject;
        trackerUI.text = trackerText;
        EditorUtility.SetDirty(trackerUI);

        Selection.activeGameObject = dialog.gameObject;

        if (questLog == null)
            Debug.LogWarning("Quest UI built, but no QuestLog was found. Add it to the Player.");
        else
            Debug.Log("Quest window and tracker built.");
    }

    /// <summary>Buttons need a GraphicRaycaster on the Canvas and an EventSystem in the scene.</summary>
    private static void EnsureClickable(Canvas canvas)
    {
        if (canvas.GetComponent<GraphicRaycaster>() == null)
            Undo.AddComponent<GraphicRaycaster>(canvas.gameObject);

        var es = Object.FindFirstObjectByType<EventSystem>();
        if (es == null)
        {
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            Undo.RegisterCreatedObjectUndo(go, "Build Quest UI");
        }
        else if (es.GetComponent<InputSystemUIInputModule>() == null)
        {
            Debug.LogWarning("The EventSystem isn't using InputSystemUIInputModule. Select it " +
                             "and click 'Replace with InputSystemUIInputModule', or buttons won't click.", es);
        }
    }

    // ---- helpers ----

    private static RectTransform MakeRect(string name, Transform parent,
        Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Build Quest UI");
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        rt.localScale = Vector3.one;
        return rt;
    }

    private static Image MakeImage(string name, Transform parent, Sprite sprite, Color color, bool raycast)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        Undo.RegisterCreatedObjectUndo(go, "Build Quest UI");
        go.transform.SetParent(parent, false);

        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.type = Image.Type.Simple;
        img.color = color;
        img.raycastTarget = raycast;
        return img;
    }

    private static TextMeshProUGUI MakeText(string name, Transform parent, string text,
        float size, Color color, bool wrap)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        Undo.RegisterCreatedObjectUndo(go, "Build Quest UI");
        go.transform.SetParent(parent, false);

        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.horizontalAlignment = HorizontalAlignmentOptions.Left;
        tmp.verticalAlignment = VerticalAlignmentOptions.Middle;
        tmp.color = color;
        tmp.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static Button MakeButton(string name, Transform parent, Sprite sprite,
        string label, Color color, Vector2 pos)
    {
        var rt = MakeRect(name, parent, Vector2.zero, Vector2.zero, pos, new Vector2(160f, 40f));

        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = true;

        var button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = img;

        var text = MakeText("Label", rt, label, 16f, Color.white, wrap: false);
        Stretch(text.rectTransform, 0f);
        text.horizontalAlignment = HorizontalAlignmentOptions.Center;

        return button;
    }

    private static void TopLeft(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
    }

    private static void Stretch(RectTransform rt, float inset)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
        rt.localScale = Vector3.one;
    }
}
