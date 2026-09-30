using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class HudBuilder
{
    private const string AbilityFolder = "Assets/_Project/ScriptableObjects/Abilities";

    // ---------------- Action bar + cast bar ----------------

    [MenuItem("Tools/UI/Build Action Bar and Cast Bar")]
    private static void BuildHud()
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

        // ---- Action bar ----
        const int slots = PlayerAbilities.SlotCount;
        const float slotSize = 46f;
        const float gap = 4f;
        float barWidth = slots * slotSize + (slots - 1) * gap;

        var barRoot = MakeRect("ActionBar", canvas.transform,
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 18f), new Vector2(barWidth, slotSize));

        var barUI = canvas.GetComponent<ActionBarUI>();
        if (barUI == null) barUI = Undo.AddComponent<ActionBarUI>(canvas.gameObject);
        barUI.slotWidgets = new ActionBarUI.SlotWidgets[slots];

        for (int i = 0; i < slots; i++)
        {
            float x = i * (slotSize + gap);

            var slot = MakeRect($"Slot{i + 1:00}", barRoot,
                new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(x, 0f), new Vector2(slotSize, slotSize));

            var bg = MakeImage("SlotBG", slot, sprite, new Color32(10, 10, 12, 235));
            Stretch(bg.rectTransform, 0f);

            var icon = MakeImage("Icon", slot, sprite, new Color32(30, 30, 34, 255));
            Stretch(icon.rectTransform, 3f);

            // Radial sweep, top origin, counter-clockwise, drawn over the icon.
            var sweep = MakeImage("CooldownSweep", slot, sprite, new Color32(0, 0, 0, 170));
            Stretch(sweep.rectTransform, 3f);
            sweep.type = Image.Type.Filled;
            sweep.fillMethod = Image.FillMethod.Radial360;
            sweep.fillOrigin = (int)Image.Origin360.Top;
            sweep.fillClockwise = false;
            sweep.fillAmount = 0f;

            var tint = MakeImage("UnusableTint", slot, sprite, new Color32(0, 0, 0, 140));
            Stretch(tint.rectTransform, 3f);
            tint.enabled = false;

            var keybind = MakeText("Keybind", slot, $"{i + 1}", 11f,
                HorizontalAlignmentOptions.Right, new Color32(220, 220, 220, 255));
            SetRect(keybind.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-3f, -2f), new Vector2(20f, 14f));

            var cdText = MakeText("CooldownText", slot, "", 18f,
                HorizontalAlignmentOptions.Center, new Color32(255, 245, 210, 255));
            Stretch(cdText.rectTransform, 0f);

            barUI.slotWidgets[i] = new ActionBarUI.SlotWidgets
            {
                icon = icon,
                cooldownSweep = sweep,
                keybindText = keybind,
                cooldownText = cdText,
                unusableTint = tint
            };
        }

        // ---- Cast bar ----
        var castRoot = MakeRect("CastBar", canvas.transform,
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 110f), new Vector2(260f, 22f));

        var castBG = MakeImage("CastBG", castRoot, sprite, new Color32(16, 16, 19, 235));
        Stretch(castBG.rectTransform, 0f);

        var castFill = MakeImage("CastFill", castRoot, sprite, new Color32(255, 216, 90, 255));
        Stretch(castFill.rectTransform, 2f);
        castFill.type = Image.Type.Filled;
        castFill.fillMethod = Image.FillMethod.Horizontal;
        castFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        castFill.fillAmount = 0f;

        var castName = MakeText("CastName", castRoot, "Fireball", 13f,
            HorizontalAlignmentOptions.Left, Color.white);
        Stretch(castName.rectTransform, 0f);
        castName.rectTransform.offsetMin = new Vector2(8f, 0f);
        castName.rectTransform.offsetMax = new Vector2(-8f, 0f);

        var castTime = MakeText("CastTime", castRoot, "1.5s", 13f,
            HorizontalAlignmentOptions.Right, Color.white);
        Stretch(castTime.rectTransform, 0f);
        castTime.rectTransform.offsetMin = new Vector2(8f, 0f);
        castTime.rectTransform.offsetMax = new Vector2(-8f, 0f);

        // ---- Error text, above the cast bar ----
        var error = MakeText("AbilityError", canvas.transform, "", 16f,
            HorizontalAlignmentOptions.Center, new Color32(255, 90, 90, 255));
        SetRect(error.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 145f), new Vector2(420f, 24f));

        var castUI = canvas.GetComponent<CastBarUI>();
        if (castUI == null) castUI = Undo.AddComponent<CastBarUI>(canvas.gameObject);

        castUI.barRoot = castRoot.gameObject;
        castUI.fill = castFill;
        castUI.nameText = castName;
        castUI.timeText = castTime;
        castUI.errorText = error;

        // ---- Wire sources ----
        var playerAbilities = Object.FindFirstObjectByType<PlayerAbilities>();
        if (playerAbilities != null)
        {
            barUI.abilities = playerAbilities;
            barUI.combat = playerAbilities.GetComponent<PlayerCombat>();
            castUI.abilities = playerAbilities;
        }

        EditorUtility.SetDirty(barUI);
        EditorUtility.SetDirty(castUI);
        castRoot.gameObject.SetActive(false);

        Debug.Log("Action bar and cast bar built. Assign PlayerAbilities on the Canvas " +
                  "if it was not auto-found.");
    }

    // ---------------- Test abilities ----------------

    [MenuItem("Tools/RPG/Create Test Abilities")]
    private static void CreateTestAbilities()
    {
        Directory.CreateDirectory(AbilityFolder);

        // Slot 1: auto attack, free, no GCD, no cooldown.
        var auto = Make<AutoAttackAbility>("AB_AutoAttack");
        auto.displayName = "Auto Attack";
        auto.description = "Toggle melee swings against your target.";
        auto.iconTint = new Color(0.72f, 0.70f, 0.66f);
        auto.powerCost = 0f;
        auto.cooldown = 0f;
        auto.castTime = 0f;
        auto.triggersGCD = false;
        auto.requiresTarget = true;
        auto.range = 100f;
        auto.requiresFacing = false;

        // Slot 2: instant melee hit.
        var strike = Make<DamageAbility>("AB_Strike");
        strike.displayName = "Strike";
        strike.description = "A quick melee blow.";
        strike.iconTint = new Color(0.85f, 0.35f, 0.25f);
        strike.powerCost = 15f;
        strike.cooldown = 4f;
        strike.castTime = 0f;
        strike.range = 3f;
        strike.damage = 18f;

        // Slot 3: long cast, high damage, cannot move.
        var fireball = Make<DamageAbility>("AB_Fireball");
        fireball.displayName = "Fireball";
        fireball.description = "Hurl a slow but heavy ball of flame.";
        fireball.iconTint = new Color(1f, 0.55f, 0.15f);
        fireball.powerCost = 25f;
        fireball.cooldown = 0f;
        fireball.castTime = 2f;
        fireball.usableWhileMoving = false;
        fireball.range = 30f;
        fireball.damage = 35f;
        fireball.critChance = 0.15f;

        // Slot 4: instant ranged poke, usable while moving, short cooldown.
        var bolt = Make<DamageAbility>("AB_Frostbolt");
        bolt.displayName = "Frost Bolt";
        bolt.description = "A fast shard of ice. Can be cast on the move.";
        bolt.iconTint = new Color(0.4f, 0.75f, 1f);
        bolt.powerCost = 12f;
        bolt.cooldown = 6f;
        bolt.castTime = 0f;
        bolt.usableWhileMoving = true;
        bolt.range = 25f;
        bolt.damage = 14f;

        // Slot 5: self heal, no target required.
        var heal = Make<HealAbility>("AB_Mend");
        heal.displayName = "Mend";
        heal.description = "Restore your own health. Cannot be cast while moving.";
        heal.iconTint = new Color(0.4f, 0.9f, 0.5f);
        heal.powerCost = 30f;
        heal.cooldown = 10f;
        heal.castTime = 1.5f;
        heal.requiresTarget = false;
        heal.amount = 35f;

        // Mark every asset as changed so SaveAssets actually writes it to disk.
        EditorUtility.SetDirty(auto);
        EditorUtility.SetDirty(strike);
        EditorUtility.SetDirty(fireball);
        EditorUtility.SetDirty(bolt);
        EditorUtility.SetDirty(heal);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"Created 5 test abilities in {AbilityFolder}. Assign them to " +
                  "PlayerAbilities slots 1-5 on the Player.");
    }

    private static T Make<T>(string fileName) where T : Ability
    {
        string path = $"{AbilityFolder}/{fileName}.asset";

        var existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing != null) return existing;

        var asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    // ---------------- shared helpers ----------------

    private static RectTransform MakeRect(string name, Transform parent,
        Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Build HUD");
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        SetRect(rt, anchor, pivot, pos, size);
        return rt;
    }

    private static Image MakeImage(string name, Transform parent, Sprite sprite, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        Undo.RegisterCreatedObjectUndo(go, "Build HUD");
        go.transform.SetParent(parent, false);

        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.type = Image.Type.Simple;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private static TextMeshProUGUI MakeText(string name, Transform parent, string text,
        float size, HorizontalAlignmentOptions align, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        Undo.RegisterCreatedObjectUndo(go, "Build HUD");
        go.transform.SetParent(parent, false);

        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.horizontalAlignment = align;
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
