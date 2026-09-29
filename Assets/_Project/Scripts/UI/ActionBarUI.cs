using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays the player's 12 action bar slots: icon, keybind, cooldown sweep,
/// cooldown seconds, and an unusable tint. Attach to the Canvas.
/// </summary>
public class ActionBarUI : MonoBehaviour
{
    [System.Serializable]
    public class SlotWidgets
    {
        public Image icon;
        public Image cooldownSweep;
        public TMP_Text keybindText;
        public TMP_Text cooldownText;
        public Image unusableTint;
    }

    [Header("Source")]
    public PlayerAbilities abilities;

    [Header("Slots (index 0 = key '1')")]
    public SlotWidgets[] slotWidgets = new SlotWidgets[PlayerAbilities.SlotCount];

    [Header("Colours")]
    public Color emptySlotColor = new Color(0.12f, 0.12f, 0.14f, 0.85f);
    public Color unusableColor = new Color(0f, 0f, 0f, 0.55f);
    public Color activeGlow = new Color(1f, 0.85f, 0.3f);

    [Header("Auto-Attack Highlight")]
    [Tooltip("Pulses the auto-attack slot while swinging.")]
    public PlayerCombat combat;
    public float pulseSpeed = 4f;

    void Start()
    {
        if (abilities == null)
        {
            Debug.LogWarning("ActionBarUI: Abilities not assigned.", this);
            enabled = false;
            return;
        }

        abilities.OnSlotsChanged += RefreshIcons;
        RefreshIcons();
        RefreshKeybinds();
    }

    void OnDestroy()
    {
        if (abilities != null) abilities.OnSlotsChanged -= RefreshIcons;
    }

    void Update()
    {
        int count = Mathf.Min(slotWidgets.Length, PlayerAbilities.SlotCount);

        for (int i = 0; i < count; i++)
        {
            var w = slotWidgets[i];
            if (w == null) continue;

            Ability a = abilities.slots[i];

            if (a == null)
            {
                if (w.cooldownSweep) w.cooldownSweep.fillAmount = 0f;
                if (w.cooldownText) w.cooldownText.text = "";
                if (w.unusableTint) w.unusableTint.enabled = false;
                continue;
            }

            // Cooldown sweep, whichever of ability CD or GCD is longer.
            float sweep = abilities.GetSweepFraction(i);
            if (w.cooldownSweep) w.cooldownSweep.fillAmount = sweep;

            // Numeric countdown only for real cooldowns over 1.5s, not the GCD.
            float remaining = abilities.GetCooldownRemaining(a);
            if (w.cooldownText)
                w.cooldownText.text = remaining > 1.5f
                    ? Mathf.CeilToInt(remaining).ToString()
                    : "";

            // Dim when unusable (no power, out of range, no target).
            if (w.unusableTint)
            {
                bool usable = abilities.IsSlotUsable(i);
                w.unusableTint.enabled = !usable;
                w.unusableTint.color = unusableColor;
            }

            // Pulse the auto-attack slot while engaged.
            if (w.icon != null && a is AutoAttackAbility && combat != null)
            {
                if (combat.inCombat)
                {
                    float t = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
                    w.icon.color = Color.Lerp(a.iconTint, activeGlow, t * 0.6f);
                }
                else
                {
                    w.icon.color = a.iconTint;
                }
            }
        }
    }

    public void RefreshIcons()
    {
        int count = Mathf.Min(slotWidgets.Length, PlayerAbilities.SlotCount);

        for (int i = 0; i < count; i++)
        {
            var w = slotWidgets[i];
            if (w?.icon == null) continue;

            Ability a = abilities.slots[i];

            if (a == null)
            {
                w.icon.sprite = null;
                w.icon.color = emptySlotColor;
            }
            else if (a.icon != null)
            {
                w.icon.sprite = a.icon;
                w.icon.color = Color.white;
            }
            else
            {
                // No art yet, so the tint colour identifies the ability.
                w.icon.color = a.iconTint;
            }
        }
    }

    private void RefreshKeybinds()
    {
        string[] labels = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "0", "-", "=" };
        int count = Mathf.Min(slotWidgets.Length, labels.Length);

        for (int i = 0; i < count; i++)
            if (slotWidgets[i]?.keybindText != null)
                slotWidgets[i].keybindText.text = labels[i];
    }
}
