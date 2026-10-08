using System.Collections.Generic;
using TMPro;
using UnityEngine;

public enum CombatTextMotion { ScrollUp, Static }

/// <summary>
/// Screen-space floating combat text in three stacked bands above the player:
///   Top    = outgoing damage (scrolls up, random sideways offset, crits static)
///   Middle = XP gains (static)
///   Bottom = everything about you (configurable)
/// Each text stays inside its band and fades before leaving it, so bands never overlap.
/// Newer text pushes older text up, so texts in a band never overlap either.
/// Attach to the HUD Canvas (Screen Space Overlay).
/// </summary>
[DefaultExecutionOrder(1000)]   // after the camera has moved
public class CombatTextSpawner : MonoBehaviour
{
    [System.Serializable]
    public class LaneSettings
    {
        [Tooltip("Bottom of the band, in canvas pixels above the anchor.")]
        public float bottom;
        [Tooltip("Band height. Text fades out before reaching the top.")]
        public float height;
        [Tooltip("Minimum vertical gap between texts in this band.")]
        public float spacing;
        [Tooltip("Random sideways start offset, plus or minus, in pixels.")]
        public float jitter;
        [Tooltip("Random rise speed in pixels per second. Ignored for static text.")]
        public Vector2 riseSpeed;
        public float lifetime;
        public float fontSize;
    }

    private enum Lane { Outgoing = 0, Experience = 1, Self = 2 }

    private class Item
    {
        public RectTransform rect;
        public TextMeshProUGUI text;
        public bool scrolls;
        public bool crit;
        public float speed;
        public float x;
        public float y;         // drawn height inside the band
        public float targetY;   // height including scrolling and pushes
        public float age;
        public float lifetime;
    }

    [Header("Anchor")]
    [Tooltip("Leave empty to find the player automatically.")]
    public Transform player;
    [Tooltip("Height above the player's feet that the bands are measured from.")]
    public float anchorHeight = 2.1f;

    [Header("Font (optional)")]
    [Tooltip("Leave empty for the TMP default font.")]
    public TMP_FontAsset font;
    [Tooltip("Optional preset, e.g. 'LiberationSans SDF - Outline'. Must belong to the font.")]
    public Material fontMaterial;

    [Header("Lanes (bottom to top, in canvas pixels)")]
    public LaneSettings selfLane = new LaneSettings
    {
        bottom = 30f, height = 120f, spacing = 26f, jitter = 10f,
        riseSpeed = new Vector2(30f, 40f), lifetime = 2.2f, fontSize = 26f
    };
    public LaneSettings experienceLane = new LaneSettings
    {
        bottom = 165f, height = 60f, spacing = 24f, jitter = 12f,
        riseSpeed = Vector2.zero, lifetime = 2.2f, fontSize = 24f
    };
    public LaneSettings outgoingLane = new LaneSettings
    {
        bottom = 240f, height = 180f, spacing = 34f, jitter = 16f,
        riseSpeed = new Vector2(60f, 95f), lifetime = 1.6f, fontSize = 32f
    };

    [Header("Scrolling Combat Text for Self")]
    public bool scrollingTextForSelf = true;
    public CombatTextMotion selfFloatMode = CombatTextMotion.ScrollUp;
    public bool showDamageTaken = true;
    public bool showHealingReceived = true;
    public bool showLowManaAndHealth = true;
    public bool showEnterLeaveCombat = true;
    [Tooltip("Rage, mana and energy gained from actions.")]
    public bool showEnergyGains = true;

    [Header("Feel")]
    public float fadeDuration = 0.4f;
    [Tooltip("Text fades over this many pixels below the top of its band.")]
    public float topFadeZone = 30f;
    public float pushSmoothing = 18f;
    public float critSizeMultiplier = 1.4f;
    public float critPopScale = 1.6f;
    public float critPopDuration = 0.15f;
    [Tooltip("Word messages (Evade, +Combat, Low Health) are drawn smaller.")]
    public float messageSizeMultiplier = 0.85f;

    [Header("Colours")]
    public Color autoAttackColor = Color.white;
    public Color abilityColor = new Color(1f, 0.85f, 0.15f);
    public Color incomingColor = new Color(1f, 0.25f, 0.25f);
    public Color healingColor = new Color(0.35f, 1f, 0.45f);
    public Color experienceColor = new Color(0.75f, 0.5f, 1f);
    public Color evadeColor = new Color(0.8f, 0.8f, 0.8f);
    public Color combatStateColor = new Color(1f, 0.6f, 0.2f);
    public Color powerColor = new Color(0.4f, 0.65f, 1f);
    public Color warningColor = new Color(1f, 0.2f, 0.2f);

    private readonly List<Item>[] lanes = { new(), new(), new() };
    private Canvas canvas;
    private RectTransform container;
    private Camera cam;

    void Awake()
    {
        canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("CombatTextSpawner must be on a Canvas.", this);
            enabled = false;
            return;
        }

        // Full-screen layer, drawn behind the other HUD elements.
        var go = new GameObject("CombatTextLayer", typeof(RectTransform));
        container = (RectTransform)go.transform;
        container.SetParent(canvas.transform, false);
        container.anchorMin = Vector2.zero;
        container.anchorMax = Vector2.one;
        container.offsetMin = Vector2.zero;
        container.offsetMax = Vector2.zero;
        container.SetAsFirstSibling();
    }

    void OnEnable() => CombatTextBus.OnCombatText += HandleCombatText;
    void OnDisable() => CombatTextBus.OnCombatText -= HandleCombatText;

    void Start()
    {
        if (player == null)
        {
            var stats = FindFirstObjectByType<PlayerStats>();
            if (stats != null) player = stats.transform;
        }
    }

    // ---- Spawning ----

    private void HandleCombatText(CombatTextEvent e)
    {
        if (!TryGetLane(e.type, out Lane lane))
            return;

        LaneSettings s = Settings(lane);
        Describe(e, out string value, out Color color, out bool crit, out bool message);

        var go = new GameObject("CombatText", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(container, false);

        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(400f, 60f);

        var text = go.GetComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        if (fontMaterial != null) text.fontSharedMaterial = fontMaterial;
        text.text = value;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        text.fontSize = s.fontSize * (crit ? critSizeMultiplier : message ? messageSizeMultiplier : 1f);
        text.alpha = 0f;   // placed and shown in LateUpdate

        lanes[(int)lane].Add(new Item
        {
            rect = rect,
            text = text,
            crit = crit,
            scrolls = !crit && LaneScrolls(lane),
            speed = Random.Range(s.riseSpeed.x, s.riseSpeed.y),
            x = Random.Range(-s.jitter, s.jitter),
            y = 0f,
            targetY = 0f,
            age = 0f,
            lifetime = s.lifetime
        });
    }

    // ---- Movement ----

    void LateUpdate()
    {
        if (!TryGetAnchor(out Vector2 anchor))
        {
            container.gameObject.SetActive(false);
            return;
        }

        container.gameObject.SetActive(true);
        float dt = Time.deltaTime;
        float smooth = 1f - Mathf.Exp(-pushSmoothing * dt);

        for (int l = 0; l < lanes.Length; l++)
        {
            var list = lanes[l];
            LaneSettings s = Settings((Lane)l);

            // 1. Age and scroll.
            foreach (var it in list)
            {
                it.age += dt;
                if (it.scrolls) it.targetY += it.speed * dt;
            }

            // 2. Keep every older text at least one line above the next newer one.
            for (int i = list.Count - 2; i >= 0; i--)
            {
                float required = list[i + 1].targetY + s.spacing;
                if (list[i].targetY < required)
                    list[i].targetY = required;
            }

            // 3. Draw, fade, and remove anything that has expired or left the band.
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var it = list[i];

                if (it.rect == null || it.age >= it.lifetime || it.targetY >= s.height)
                {
                    if (it.rect != null) Destroy(it.rect.gameObject);
                    list.RemoveAt(i);
                    continue;
                }

                it.y = Mathf.Lerp(it.y, it.targetY, smooth);

                float timeAlpha = 1f - Mathf.InverseLerp(it.lifetime - fadeDuration, it.lifetime, it.age);
                float bandAlpha = Mathf.InverseLerp(s.height, s.height - topFadeZone, it.y);
                it.text.alpha = Mathf.Min(timeAlpha, bandAlpha);

                float scale = it.crit
                    ? Mathf.Lerp(critPopScale, 1f, Mathf.Clamp01(it.age / critPopDuration))
                    : 1f;
                it.rect.localScale = Vector3.one * scale;

                it.rect.anchoredPosition = anchor + new Vector2(it.x, s.bottom + it.y);
            }
        }
    }

    /// <summary>The player's head, converted to a point on this canvas.</summary>
    private bool TryGetAnchor(out Vector2 local)
    {
        local = Vector2.zero;

        if (cam == null) cam = Camera.main;
        if (cam == null || player == null)
            return false;

        Vector3 screen = cam.WorldToScreenPoint(player.position + Vector3.up * anchorHeight);
        if (screen.z <= 0f)
            return false;   // behind the camera

        Camera uiCam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(container, screen, uiCam, out local);
    }

    // ---- Lane rules ----

    private LaneSettings Settings(Lane lane) => lane switch
    {
        Lane.Outgoing => outgoingLane,
        Lane.Experience => experienceLane,
        _ => selfLane
    };

    private bool LaneScrolls(Lane lane) => lane switch
    {
        Lane.Outgoing => true,
        Lane.Experience => false,
        _ => selfFloatMode == CombatTextMotion.ScrollUp
    };

    private bool TryGetLane(CombatTextType type, out Lane lane)
    {
        switch (type)
        {
            // Always shown.
            case CombatTextType.Damage:
            case CombatTextType.CriticalDamage:
            case CombatTextType.AbilityDamage:
            case CombatTextType.AbilityCritical:
            case CombatTextType.Evade:
                lane = Lane.Outgoing;
                return true;

            case CombatTextType.Experience:
                lane = Lane.Experience;
                return true;

            // Configurable.
            case CombatTextType.IncomingDamage:
                lane = Lane.Self;
                return scrollingTextForSelf && showDamageTaken;

            case CombatTextType.Healing:
                lane = Lane.Self;
                return scrollingTextForSelf && showHealingReceived;

            case CombatTextType.EnterCombat:
            case CombatTextType.LeaveCombat:
                lane = Lane.Self;
                return scrollingTextForSelf && showEnterLeaveCombat;

            case CombatTextType.PowerGain:
                lane = Lane.Self;
                return scrollingTextForSelf && showEnergyGains;

            case CombatTextType.LowHealth:
            case CombatTextType.LowPower:
                lane = Lane.Self;
                return scrollingTextForSelf && showLowManaAndHealth;

            default:
                lane = Lane.Self;
                return scrollingTextForSelf;
        }
    }

    private void Describe(CombatTextEvent e, out string value, out Color color,
                          out bool crit, out bool message)
    {
        string amount = Mathf.RoundToInt(e.amount).ToString();
        crit = false;
        message = false;

        switch (e.type)
        {
            case CombatTextType.CriticalDamage:  value = amount; color = autoAttackColor; crit = true; break;
            case CombatTextType.AbilityDamage:   value = amount; color = abilityColor; break;
            case CombatTextType.AbilityCritical: value = amount; color = abilityColor; crit = true; break;
            case CombatTextType.IncomingDamage:  value = $"-{amount}"; color = incomingColor; break;
            case CombatTextType.Healing:         value = $"+{amount}"; color = healingColor; break;
            case CombatTextType.Experience:      value = $"+{amount} XP"; color = experienceColor; break;
            case CombatTextType.Evade:           value = e.label ?? "Evade"; color = evadeColor; message = true; break;
            case CombatTextType.EnterCombat:     value = e.label ?? "+Combat"; color = combatStateColor; message = true; break;
            case CombatTextType.LeaveCombat:     value = e.label ?? "-Combat"; color = combatStateColor; message = true; break;
            case CombatTextType.PowerGain:       value = $"+{amount} {e.label}"; color = powerColor; message = true; break;
            case CombatTextType.LowHealth:
            case CombatTextType.LowPower:        value = e.label ?? "Low"; color = warningColor; message = true; break;
            default:                             value = amount; color = autoAttackColor; break;
        }
    }
}
