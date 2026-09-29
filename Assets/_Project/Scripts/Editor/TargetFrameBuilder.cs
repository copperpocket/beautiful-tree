using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds the target frame with exact values. Select a Canvas, then
/// Tools > UI > Build Target Frame. Delete the old TargetFrame before re-running.
/// </summary>
public static class TargetFrameBuilder
{
    [MenuItem("Tools/UI/Build Target Frame")]
    private static void Build()
    {
        var canvas = Selection.activeGameObject != null
            ? Selection.activeGameObject.GetComponentInParent<Canvas>()
            : Object.FindFirstObjectByType<Canvas>();

        if (canvas == null)
        {
            EditorUtility.DisplayDialog("No Canvas",
                "Select a Canvas in the Hierarchy first.", "OK");
            return;
        }

        // Plain white square. Never use UISprite for Filled bars.
        var sprite = UISpriteUtility.GetOrCreate();

        var root = MakeRect("TargetFrame", canvas.transform,
                            new Vector2(0f, 1f), new Vector2(0f, 1f),
                            new Vector2(290f, -20f), new Vector2(360f, 72f));

        var border = MakeImage("FrameBorder", root, sprite, new Color32(16, 16, 19, 235));
        Stretch(border.rectTransform, 0f);

        var name = MakeText("TargetName", root, "Training Dummy", 18f,
                            HorizontalAlignmentOptions.Left, Color.white);
        SetRect(name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(10f, -4f), new Vector2(270f, 24f));

        // Health bar
        var healthBG = MakeImage("HealthBG", root, sprite, new Color32(34, 34, 38, 255));
        SetRect(healthBG.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(10f, -30f), new Vector2(264f, 26f));

        var healthDelay = MakeImage("HealthDelay", healthBG.transform, sprite,
                                    new Color32(150, 62, 62, 255));
        Stretch(healthDelay.rectTransform, 2f);
        MakeFilled(healthDelay);

        var healthFill = MakeImage("HealthFill", healthBG.transform, sprite,
                                   new Color32(192, 42, 42, 255));
        Stretch(healthFill.rectTransform, 2f);
        MakeFilled(healthFill);

        // Numbers are children of the bar, so they cannot drift outside it
        var percent = MakeText("PercentText", healthBG.transform, "100%", 14f,
                               HorizontalAlignmentOptions.Left, new Color32(225, 225, 225, 255));
        Stretch(percent.rectTransform, 0f);
        SetStretchInset(percent.rectTransform, 8f, 0f, 8f, 0f);

        var healthText = MakeText("HealthText", healthBG.transform, "50 / 50", 14f,
                                  HorizontalAlignmentOptions.Right, Color.white);
        Stretch(healthText.rectTransform, 0f);
        SetStretchInset(healthText.rectTransform, 8f, 0f, 8f, 0f);

        // Thin mana bar, hidden at runtime for targets with no mana
        var manaBG = MakeImage("ManaBG", root, sprite, new Color32(34, 34, 38, 255));
        SetRect(manaBG.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(10f, -60f), new Vector2(264f, 8f));

        var manaFill = MakeImage("ManaFill", manaBG.transform, sprite,
                                 new Color32(44, 96, 208, 255));
        Stretch(manaFill.rectTransform, 1f);
        MakeFilled(manaFill);

        // Portrait
        var portrait = MakeImage("PortraitBox", root, sprite, new Color32(8, 8, 10, 255));
        SetRect(portrait.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-8f, -8f), new Vector2(56f, 56f));

        var portraitFill = MakeImage("PortraitFill", portrait.transform, sprite,
                                     new Color32(70, 90, 60, 255));
        Stretch(portraitFill.rectTransform, 3f);

        // Level badge, centred on the portrait's bottom-LEFT corner so it stays inside
        var badge = MakeImage("LevelBadge", portrait.transform, sprite,
                              new Color32(0, 0, 0, 215));
        SetRect(badge.rectTransform, new Vector2(0f, 0f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(30f, 22f));

        var levelText = MakeText("TargetLevel", badge.transform, "1", 14f,
                                 HorizontalAlignmentOptions.Center, new Color32(255, 220, 120, 255));
        Stretch(levelText.rectTransform, 0f);

        // Wire the component
        var ui = canvas.GetComponent<TargetFrameUI>();
        if (ui == null) ui = Undo.AddComponent<TargetFrameUI>(canvas.gameObject);

        ui.frameRoot    = root.gameObject;
        ui.healthFill   = healthFill;
        ui.healthDelay  = healthDelay;
        ui.manaFill     = manaFill;
        ui.manaGroup    = manaBG.gameObject;
        ui.nameText     = name;
        ui.levelText    = levelText;
        ui.healthText   = healthText;
        ui.percentText  = percent;
        ui.portraitFill = portraitFill;

        var player = Object.FindFirstObjectByType<PlayerTargeting>();
        if (player != null)
        {
            ui.targeting = player;
            ui.playerStats = player.GetComponent<PlayerStats>();
        }

        EditorUtility.SetDirty(ui);
        Selection.activeGameObject = root.gameObject;

        Debug.Log("Target frame built with T_White. Add a TMP Underlay to TargetName " +
                  "and HealthText for readability over the bar.");
    }

    // ---- helpers ----

    private static RectTransform MakeRect(string name, Transform parent,
        Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Build Target Frame");
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        SetRect(rt, anchor, pivot, pos, size);
        return rt;
    }

    private static Image MakeImage(string name, Transform parent, Sprite sprite, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        Undo.RegisterCreatedObjectUndo(go, "Build Target Frame");
        go.transform.SetParent(parent, false);

        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.type = Image.Type.Simple;   // Simple by default; MakeFilled overrides
        img.color = color;
        img.raycastTarget = false;      // UI must never eat the click-to-target ray
        return img;
    }

    private static TextMeshProUGUI MakeText(string name, Transform parent, string text,
        float size, HorizontalAlignmentOptions align, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        Undo.RegisterCreatedObjectUndo(go, "Build Target Frame");
        go.transform.SetParent(parent, false);

        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.horizontalAlignment = align;
        tmp.verticalAlignment = VerticalAlignmentOptions.Middle;
        tmp.color = color;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static void MakeFilled(Image img)
    {
        img.type = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Horizontal;
        img.fillOrigin = (int)Image.OriginHorizontal.Left;
        img.fillAmount = 1f;
    }

    private static void SetRect(RectTransform rt, Vector2 anchor, Vector2 pivot,
                                Vector2 pos, Vector2 size)
    {
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        rt.localScale = Vector3.one;
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

    private static void SetStretchInset(RectTransform rt, float left, float bottom,
                                        float right, float top)
    {
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(-right, -top);
    }
}
