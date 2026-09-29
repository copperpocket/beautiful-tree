using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// WoW-style target frame. Shows the selected enemy's portrait, name, level,
/// health bar with exact numbers, and a trailing "ghost" bar showing recent
/// damage. Attach to the Canvas.
/// </summary>
public class TargetFrameUI : MonoBehaviour
{
    [Header("Source")]
    public PlayerTargeting targeting;
    [Tooltip("Used to colour the level badge by difficulty. Optional.")]
    public PlayerStats playerStats;

    [Header("Root")]
    [Tooltip("Toggled on and off with the selection.")]
    public GameObject frameRoot;

    [Header("Bars")]
    public Image healthFill;
    [Tooltip("Trails behind healthFill to show the size of the last hit. Optional.")]
    public Image healthDelay;
    public Image manaFill;
    [Tooltip("Parent of manaFill, hidden for targets with no mana.")]
    public GameObject manaGroup;

    [Header("Text")]
    public TMP_Text nameText;
    public TMP_Text levelText;
    public TMP_Text healthText;
    public TMP_Text percentText;

    [Header("Portrait")]
    public Image portraitFill;
    [Tooltip("Tints the portrait placeholder by level difficulty.")]
    public bool tintPortraitByDifficulty = false;

    [Header("Feel")]
    [Tooltip("How fast the main bar catches up to the real value.")]
    public float fillSmooth = 14f;
    [Tooltip("How fast the ghost bar catches up. Lower = longer damage trail.")]
    public float delaySmooth = 3.5f;
    [Tooltip("Seconds the ghost bar holds before it starts falling.")]
    public float delayHold = 0.35f;

    [Header("Health Colours")]
    public Color healthHigh = new Color(0.75f, 0.16f, 0.16f);
    public Color healthLow  = new Color(0.55f, 0.10f, 0.10f);
    [Tooltip("Below this fraction the bar shifts toward healthLow.")]
    public float lowHealthThreshold = 0.35f;

    [Header("Difficulty Colours (level badge)")]
    public Color trivialGrey  = new Color(0.60f, 0.60f, 0.60f);
    public Color easyGreen    = new Color(0.40f, 0.85f, 0.40f);
    public Color evenYellow   = new Color(1.00f, 0.90f, 0.35f);
    public Color hardOrange   = new Color(1.00f, 0.60f, 0.20f);
    public Color deadlyRed    = new Color(1.00f, 0.30f, 0.30f);

    private Health bound;
    private float targetPct = 1f;
    private float displayPct = 1f;
    private float delayPct = 1f;
    private float delayHoldUntil = -1f;

    void OnEnable()
    {
        if (targeting == null)
        {
            Debug.LogWarning("TargetFrameUI: Targeting not assigned.", this);
            return;
        }

        targeting.OnTargetChanged += Bind;
        Bind(targeting.CurrentTarget);
    }

    void OnDisable()
    {
        if (targeting != null) targeting.OnTargetChanged -= Bind;
        Unbind();
    }

    void Update()
    {
        if (bound == null) return;

        float t = fillSmooth * Time.deltaTime;
        displayPct = Mathf.Lerp(displayPct, targetPct, t);

        // Ghost bar holds, then falls slowly to meet the real value.
        if (Time.time >= delayHoldUntil)
            delayPct = Mathf.Lerp(delayPct, targetPct, delaySmooth * Time.deltaTime);

        // Never let the ghost sit below the real bar.
        delayPct = Mathf.Max(delayPct, displayPct);

        if (healthFill)
        {
            healthFill.fillAmount = displayPct;
            healthFill.color = targetPct <= lowHealthThreshold ? healthLow : healthHigh;
        }

        if (healthDelay) healthDelay.fillAmount = delayPct;

        if (healthText)
            healthText.text = $"{Mathf.CeilToInt(bound.CurrentHealth)} / " +
                              $"{Mathf.RoundToInt(bound.MaxHealth)}";

        if (percentText)
            percentText.text = $"{Mathf.RoundToInt(targetPct * 100f)}%";
    }

    private void Bind(Health target)
    {
        Unbind();

        if (target == null || target.IsDead)
        {
            if (frameRoot) frameRoot.SetActive(false);
            return;
        }

        bound = target;
        bound.OnHealthPercentChanged += HandleHealthChanged;
        bound.OnDied += HandleDied;

        if (frameRoot) frameRoot.SetActive(true);
        if (nameText)  nameText.text = target.DisplayName;

        // Snap, don't animate, when a new target is selected.
        targetPct = target.CurrentHealth / target.MaxHealth;
        displayPct = targetPct;
        delayPct = targetPct;
        delayHoldUntil = -1f;

        ApplyLevelBadge(target);

        // Enemies have no mana yet, so hide the bar. Wire it up when they do.
        if (manaGroup) manaGroup.SetActive(false);

        if (portraitFill && tintPortraitByDifficulty)
            portraitFill.color = DifficultyColor(target.Level) * 0.45f;
    }

    private void Unbind()
    {
        if (bound != null)
        {
            bound.OnHealthPercentChanged -= HandleHealthChanged;
            bound.OnDied -= HandleDied;
        }
        bound = null;
    }

    private void HandleHealthChanged(float pct)
    {
        // Losing health starts the ghost-bar hold. Gaining it just catches up.
        if (pct < targetPct) delayHoldUntil = Time.time + delayHold;
        targetPct = Mathf.Clamp01(pct);
    }

    private void HandleDied(Health victim, GameObject killer)
    {
        if (frameRoot) frameRoot.SetActive(false);
        Unbind();
    }

    private void ApplyLevelBadge(Health target)
    {
        if (levelText == null) return;

        levelText.text = target.Level.ToString();
        levelText.color = DifficultyColor(target.Level);
    }

    /// <summary>WoW-style difficulty colour based on level difference.</summary>
    private Color DifficultyColor(int targetLevel)
    {
        if (playerStats == null) return evenYellow;

        int diff = targetLevel - playerStats.level;

        if (diff <= -5) return trivialGrey;
        if (diff <= -3) return easyGreen;
        if (diff <=  2) return evenYellow;
        if (diff <=  4) return hardOrange;
        return deadlyRed;
    }
}
