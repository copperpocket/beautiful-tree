using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Builds the quest log window. Select a Canvas, then Tools > UI > Build Quest Log.
/// Delete the old QuestLogWindow before re-running.
/// </summary>
public static class QuestLogBuilder
{
    [MenuItem("Tools/UI/Build Quest Log")]
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

        if (canvas.GetComponent<GraphicRaycaster>() == null)
            Undo.AddComponent<GraphicRaycaster>(canvas.gameObject);
        if (Object.FindFirstObjectByType<EventSystem>() == null)
            Debug.LogWarning("No EventSystem in the scene. Run Tools > UI > Build Quest UI first, " +
                             "or buttons won't click.");

        var sprite = UISpriteUtility.GetOrCreate();

        // Window, centred on screen.
        var window = MakeRect("QuestLogWindow", canvas.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(640f, 480f));

        // Background blocks clicks so the camera and targeting ignore the window.
        var bg = MakeImage("WindowBG", window, sprite, new Color32(18, 16, 14, 240), raycast: true);
        Stretch(bg.rectTransform);

        var header = MakeText("Header", window, "Quest Log", 24f, new Color32(255, 215, 90, 255), false);
        TopLeft(header.rectTransform, new Vector2(16f, -12f), new Vector2(400f, 36f));

        // Left: quest list.
        var listBG = MakeImage("ListBG", window, sprite, new Color32(28, 25, 21, 255), raycast: false);
        TopLeft(listBG.rectTransform, new Vector2(16f, -56f), new Vector2(220f, 360f));

        var list = MakeRect("ListContent", listBG.transform,
            new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(220f, 360f));
        var layout = list.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(6, 6, 6, 6);
        layout.spacing = 4f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var empty = MakeText("EmptyText", listBG.transform, "No active quests", 15f,
            new Color32(150, 145, 135, 255), false);
        TopLeft(empty.rectTransform, new Vector2(10f, -10f), new Vector2(200f, 26f));

        // Right: details.
        var title = MakeText("DetailTitle", window, "Training Day", 20f, new Color32(255, 215, 90, 255), false);
        TopLeft(title.rectTransform, new Vector2(256f, -56f), new Vector2(368f, 30f));

        var body = MakeText("DetailBody", window, "Quest description.", 15f, new Color32(230, 225, 210, 255), true);
        TopLeft(body.rectTransform, new Vector2(256f, -92f), new Vector2(368f, 160f));
        body.verticalAlignment = VerticalAlignmentOptions.Top;

        var objectives = MakeText("DetailObjectives", window, "<b>Objectives</b>", 15f, Color.white, true);
        TopLeft(objectives.rectTransform, new Vector2(256f, -260f), new Vector2(368f, 110f));
        objectives.verticalAlignment = VerticalAlignmentOptions.Top;

        var reward = MakeText("DetailReward", window, "Reward: 100 XP", 15f, new Color32(190, 150, 255, 255), false);
        TopLeft(reward.rectTransform, new Vector2(256f, -376f), new Vector2(368f, 26f));

        var abandon = MakeButton("AbandonButton", window, sprite, "Abandon",
            new Color32(110, 40, 40, 255), new Vector2(256f, 16f));
        var close = MakeButton("CloseButton", window, sprite, "Close",
            new Color32(60, 55, 48, 255), new Vector2(464f, 16f));

        var ui = canvas.GetComponent<QuestLogUI>();
        if (ui == null) ui = Undo.AddComponent<QuestLogUI>(canvas.gameObject);
        ui.questLog = Object.FindFirstObjectByType<QuestLog>();
        ui.root = window.gameObject;
        ui.listContent = list;
        ui.emptyText = empty;
        ui.titleText = title;
        ui.bodyText = body;
        ui.objectivesText = objectives;
        ui.rewardText = reward;
        ui.abandonButton = abandon;
        ui.closeButton = close;
        ui.rowSprite = sprite;
        EditorUtility.SetDirty(ui);

        Selection.activeGameObject = window.gameObject;
        Debug.Log("Quest log built. Save the scene.");
    }

    // ---- helpers ----

    private static RectTransform MakeRect(string name, Transform parent,
        Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Build Quest Log");
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
        Undo.RegisterCreatedObjectUndo(go, "Build Quest Log");
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = raycast;
        return img;
    }

    private static TextMeshProUGUI MakeText(string name, Transform parent, string text,
        float size, Color color, bool wrap)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        Undo.RegisterCreatedObjectUndo(go, "Build Quest Log");
        go.transform.SetParent(parent, false);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.horizontalAlignment = HorizontalAlignmentOptions.Left;
        tmp.verticalAlignment = VerticalAlignmentOptions.Middle;
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

        var button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = img;

        var text = MakeText("Label", rt, label, 16f, Color.white, false);
        Stretch(text.rectTransform);
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

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
