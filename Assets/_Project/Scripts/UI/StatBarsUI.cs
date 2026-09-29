using UnityEngine;
using UnityEngine.UI;

public class StatBarsUI : MonoBehaviour
{
    [Header("Source")]
    public PlayerStats stats;

    [Header("Fill Images (Source Image set, Image Type = Filled)")]
    public Image healthFill;
    public Image staminaFill;
    public Image powerFill;

    [Tooltip("Bars ease toward the new value instead of snapping.")]
    public float smooth = 10f;

    private float healthTarget = 1f, staminaTarget = 1f, powerTarget = 1f;

    void OnEnable()
    {
        if (stats == null)
        {
            Debug.LogWarning("StatBarsUI: Stats is not assigned. Drag the Player " +
                             "object into the Stats field.", this);
            return;
        }

        stats.OnHealthChanged  += SetHealth;
        stats.OnStaminaChanged += SetStamina;
        stats.OnPowerChanged    += SetPower;
    }

    // Always unsubscribe, or destroyed UI keeps receiving events and throws.
    void OnDisable()
    {
        if (stats == null) return;

        stats.OnHealthChanged  -= SetHealth;
        stats.OnStaminaChanged -= SetStamina;
        stats.OnPowerChanged     -= SetPower;
    }

    void Update()
    {
        float t = smooth * Time.deltaTime;

        if (healthFill)  healthFill.fillAmount  = Mathf.Lerp(healthFill.fillAmount,  healthTarget,  t);
        if (staminaFill) staminaFill.fillAmount = Mathf.Lerp(staminaFill.fillAmount, staminaTarget, t);
        if (powerFill)   powerFill.fillAmount   = Mathf.Lerp(powerFill.fillAmount, powerTarget,     t);
    }

    private void SetHealth(float pct)  => healthTarget  = pct;
    private void SetStamina(float pct) => staminaTarget = pct;
    private void SetPower(float pct)   => powerTarget   = pct;
}
