using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Cast bar plus a short-lived error message line. Attach to the Canvas.</summary>
public class CastBarUI : MonoBehaviour
{
    [Header("Source")]
    public PlayerAbilities abilities;

    [Header("Cast Bar")]
    public GameObject barRoot;
    public Image fill;
    public TMP_Text nameText;
    public TMP_Text timeText;

    [Header("Error Text")]
    public TMP_Text errorText;
    public float errorDuration = 2f;

    [Header("Colours")]
    public Color castingColor = new Color(1f, 0.85f, 0.35f);
    public Color cancelledColor = new Color(0.8f, 0.2f, 0.2f);
    public float cancelFlashDuration = 0.4f;

    private float hideBarAt = -1f;
    private float hideErrorAt = -1f;

    void Start()
    {
        if (abilities == null)
        {
            Debug.LogWarning("CastBarUI: Abilities not assigned.", this);
            enabled = false;
            return;
        }

        abilities.OnCastStarted += HandleStarted;
        abilities.OnCastCompleted += HandleCompleted;
        abilities.OnCastCancelled += HandleCancelled;
        abilities.OnAbilityBlocked += ShowError;

        if (barRoot) barRoot.SetActive(false);
        if (errorText) errorText.text = "";
    }

    void OnDestroy()
    {
        if (abilities == null) return;
        abilities.OnCastStarted -= HandleStarted;
        abilities.OnCastCompleted -= HandleCompleted;
        abilities.OnCastCancelled -= HandleCancelled;
        abilities.OnAbilityBlocked -= ShowError;
    }

    void Update()
    {
        if (abilities.IsCasting)
        {
            if (fill) fill.fillAmount = abilities.CastProgress;

            if (timeText)
            {
                float left = Mathf.Max(0f, abilities.CastEndTime - Time.time);
                timeText.text = $"{left:F1}s";
            }
        }
        else if (hideBarAt > 0f && Time.time >= hideBarAt)
        {
            if (barRoot) barRoot.SetActive(false);
            hideBarAt = -1f;
        }

        if (hideErrorAt > 0f && Time.time >= hideErrorAt)
        {
            if (errorText) errorText.text = "";
            hideErrorAt = -1f;
        }
    }

    private void HandleStarted(Ability ability, float duration)
    {
        if (barRoot) barRoot.SetActive(true);
        if (nameText) nameText.text = ability.displayName;
        if (fill)
        {
            fill.color = castingColor;
            fill.fillAmount = 0f;
        }
        hideBarAt = -1f;
    }

    private void HandleCompleted(Ability ability)
    {
        if (fill) fill.fillAmount = 1f;
        hideBarAt = Time.time + 0.15f;
    }

    private void HandleCancelled(string reason)
    {
        if (fill) fill.color = cancelledColor;
        if (nameText) nameText.text = "Interrupted";
        hideBarAt = Time.time + cancelFlashDuration;
        ShowError(reason);
    }

    private void ShowError(string message)
    {
        if (errorText == null) return;
        errorText.text = message;
        hideErrorAt = Time.time + errorDuration;
    }
}
