using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shows the player's XP progress. Fills smoothly, and on level-up fills to
/// the end before resetting. Attach to the Canvas.
/// </summary>
public class XPBarUI : MonoBehaviour
{
    [Header("Source")]
    public PlayerStats stats;

    [Header("UI")]
    public Image fill;
    public TMP_Text label;

    [Tooltip("How fast the bar moves, in bar-widths per second.")]
    public float fillSpeed = 1.5f;

    private float target;
    private float display;
    private int pendingRollovers;   // level-ups still to animate

    void OnEnable()
    {
        if (stats == null)
        {
            Debug.LogWarning("XPBarUI: Stats is not assigned.", this);
            return;
        }

        stats.OnXPChanged += HandleXPChanged;
        stats.OnLevelUp += HandleLevelUp;

        // Show the current values straight away, without animating.
        Refresh(stats.CurrentXP, stats.XPToNextLevel);
        display = target;
        ApplyFill();
    }

    void OnDisable()
    {
        if (stats == null) return;
        stats.OnXPChanged -= HandleXPChanged;
        stats.OnLevelUp -= HandleLevelUp;
    }

    void Update()
    {
        float step = fillSpeed * Time.deltaTime;

        if (pendingRollovers > 0)
        {
            // Fill to the end, then start again from empty.
            display = Mathf.MoveTowards(display, 1f, step);
            if (display >= 1f)
            {
                display = 0f;
                pendingRollovers--;
            }
        }
        else
        {
            display = Mathf.MoveTowards(display, target, step);
        }

        ApplyFill();
    }

    private void HandleXPChanged(int current, int required)
    {
        Refresh(current, required);
    }

    private void HandleLevelUp(int newLevel)
    {
        pendingRollovers++;
    }

    private void Refresh(int current, int required)
    {
        bool maxLevel = stats.level >= stats.maxLevel;

        target = maxLevel ? 1f : (required > 0 ? (float)current / required : 0f);

        if (label != null)
            label.text = maxLevel
                ? $"Level {stats.level}   (max)"
                : $"Level {stats.level}   {current} / {required} XP";
    }

    private void ApplyFill()
    {
        if (fill != null)
            fill.fillAmount = display;
    }
}
