using TMPro;
using UnityEngine;

/// <summary>
/// Shows a short "Level X" banner when the player levels up. Attach to the Canvas.
/// </summary>
public class LevelUpBannerUI : MonoBehaviour
{
    [Header("Source")]
    public PlayerStats stats;

    [Header("UI")]
    [Tooltip("Controls the banner's fade. Lives on the banner root.")]
    public CanvasGroup group;
    public TMP_Text title;
    public TMP_Text subtitle;

    [Header("Timing (seconds)")]
    public float fadeIn = 0.2f;
    public float hold = 1.8f;
    public float fadeOut = 0.8f;

    [Header("Pop")]
    [Tooltip("Starting scale. Shrinks to 1 while fading in.")]
    public float popScale = 1.35f;

    private float shownAt = -99f;

    void OnEnable()
    {
        if (group != null) group.alpha = 0f;

        if (stats == null)
        {
            Debug.LogWarning("LevelUpBannerUI: Stats is not assigned.", this);
            return;
        }

        stats.OnLevelUp += HandleLevelUp;
    }

    void OnDisable()
    {
        if (stats != null) stats.OnLevelUp -= HandleLevelUp;
    }

    private void HandleLevelUp(int newLevel)
    {
        if (title != null)
            title.text = $"Level {newLevel}";

        if (subtitle != null)
            subtitle.text = $"+{stats.healthPerLevel:F0} Health   +{stats.powerPerLevel:F0} {stats.powerType}";

        shownAt = Time.time;
    }

    void Update()
    {
        if (group == null)
            return;

        float t = Time.time - shownAt;
        float total = fadeIn + hold + fadeOut;

        if (t < 0f || t > total)
        {
            group.alpha = 0f;
            return;
        }

        if (t < fadeIn)
            group.alpha = t / fadeIn;
        else if (t < fadeIn + hold)
            group.alpha = 1f;
        else
            group.alpha = 1f - (t - fadeIn - hold) / fadeOut;

        // Pop in: start large and settle to normal size during the fade-in.
        float scale = Mathf.Lerp(popScale, 1f, Mathf.Clamp01(t / fadeIn));
        group.transform.localScale = Vector3.one * scale;
    }
}
