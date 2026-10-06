using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds the XP bar under the action bar, and the level-up banner.
/// Select a Canvas, then Tools > UI > Build XP Bar and Level-Up Banner.
/// Delete the old XPBar and LevelUpBanner objects before re-running.
/// </summary>
public static class XPBarBuilder
{
    [MenuItem("Tools/UI/Build XP Bar and Level-Up Banner")]
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

        var sprite = UISpriteUtility.GetOrCreate();
        var stats = Object.FindFirstObjectByType<PlayerStats>();

        // ---- XP bar: same width as the action bar, sitting just below it ----
        const float slotSize = 46f;
        const float gap = 4f;
        float width = PlayerAbilities.SlotCount * slotSize + (PlayerAbilities.SlotCount - 1) * gap;

        var bar = MakeRect("XPBar", canvas.transform,
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 2f), new Vector2(width, 14f));

        var bg = MakeImage("XPBG", bar, sprite, new Color32(16, 16, 19, 235));
        Stretch(bg.rectTransform, 0f);

        var fill = MakeImage("XPFill", bar, sprite, new Color32(140, 80, 200, 255));
        Stretch(fill.rectTransform, 2f);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        fill.fillAmount = 0.35f;   // preview only; the script drives it

        var label = MakeText("XPText", bar, "Level 1   35 / 100 XP", 10f, Color.white);
        Stretch(label.rectTransform, 0f);

        var barUI = canvas.GetComponent<XPBarUI>();
        if (barUI == null) barUI = Undo.AddComponent<XPBarUI>(canvas.gameObject);
        barUI.stats = stats;
        barUI.fill = fill;
        barUI.label = label;
        EditorUtility.SetDirty(barUI);

        // ---- Level-up banner: upper middle of the screen ----
        var banner = MakeRect("LevelUpBanner", canvas.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 180f), new Vector2(600f, 110f));

        var group = Undo.AddComponent<CanvasGroup>(banner.gameObject);
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        var title = MakeText("LevelUpTitle", banner, "Level 2", 48f, new Color32(255, 215, 90, 255));
        SetRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                Vector2.zero, new Vector2(600f, 64f));

        var subtitle = MakeText("LevelUpSubtitle", banner, "+10 Health   +5 Mana", 20f,
                                new Color32(235, 235, 235, 255));
        SetRect(subtitle.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                Vector2.zero, new Vector2(600f, 36f));

        var bannerUI = canvas.GetComponent<LevelUpBannerUI>();
        if (bannerUI == null) bannerUI = Undo.AddComponent<LevelUpBannerUI>(canvas.gameObject);
        bannerUI.stats = stats;
        bannerUI.group = group;
        bannerUI.title = title;
        bannerUI.subtitle = subtitle;
        EditorUtility.SetDirty(bannerUI);

        Selection.activeGameObject = bar.gameObject;

        if (stats == null)
            Debug.LogWarning("XP bar built, but no PlayerStats was found. Assign Stats on " +
                             "XPBarUI and LevelUpBannerUI manually.");
        else
            Debug.Log("XP bar and level-up banner built.");
    }

    // ---- helpers ----

    private static RectTransform MakeRect(string name, Transform parent,
        Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Build XP Bar");
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        SetRect(rt, anchor, pivot, pos, size);
        return rt;
    }

    private static Image MakeImage(string name, Transform parent, Sprite sprite, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        Undo.RegisterCreatedObjectUndo(go, "Build XP Bar");
        go.transform.SetParent(parent, false);

        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.type = Image.Type.Simple;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private static TextMeshProUGUI MakeText(string name, Transform parent, string text,
        float size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        Undo.RegisterCreatedObjectUndo(go, "Build XP Bar");
        go.transform.SetParent(parent, false);

        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.horizontalAlignment = HorizontalAlignmentOptions.Center;
        tmp.verticalAlignment = VerticalAlignmentOptions.Middle;
        tmp.color = color;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.raycastTarget = false;
        return tmp;
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
}
