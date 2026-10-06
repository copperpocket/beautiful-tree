using TMPro;
using UnityEngine;

/// <summary>
/// Moves a single combat number or message upward and fades it out.
/// Attach to the world-space TextMeshPro prefab. Outline styling comes from
/// the prefab's TMP Material Preset.
/// </summary>
public class FloatingCombatText : MonoBehaviour
{
    [Header("Motion")]
    public float lifetime = 1.0f;
    public float riseSpeed = 0.8f;
    public float horizontalDrift = 0.25f;

    [Header("Scale")]
    public float normalScale = 1f;
    public float criticalScale = 1.25f;
    [Tooltip("Word messages such as Evade, +Combat and Low Health.")]
    public float messageScale = 0.9f;

    [Header("Colours")]
    [Tooltip("Auto attacks.")]
    public Color autoAttackColor = new Color(1f, 1f, 1f);
    [Tooltip("Abilities such as Strike, Fireball and Frost Bolt.")]
    public Color abilityColor = new Color(1f, 0.85f, 0.15f);
    public Color incomingColor = new Color(1f, 0.25f, 0.25f);
    public Color healingColor = new Color(0.35f, 1f, 0.45f);
    public Color experienceColor = new Color(0.75f, 0.5f, 1f);
    public Color evadeColor = new Color(0.8f, 0.8f, 0.8f);
    public Color combatStateColor = new Color(1f, 0.6f, 0.2f);
    public Color powerColor = new Color(0.4f, 0.65f, 1f);
    public Color warningColor = new Color(1f, 0.2f, 0.2f);

    private TMP_Text text;
    private Camera targetCamera;
    private CanvasGroup canvasGroup;

    private float age;
    private Vector3 driftDirection;

    void Awake()
    {
        text = GetComponentInChildren<TMP_Text>();

        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    public void Initialize(CombatTextEvent e)
    {
        if (text == null)
            text = GetComponentInChildren<TMP_Text>();

        targetCamera = Camera.main;

        string amount = Mathf.RoundToInt(e.amount).ToString();
        float scale = normalScale;

        switch (e.type)
        {
            case CombatTextType.CriticalDamage:
                Set($"<b>CRIT!</b> {amount}", autoAttackColor);
                scale = criticalScale;
                break;

            case CombatTextType.AbilityDamage:
                Set(amount, abilityColor);
                break;

            case CombatTextType.AbilityCritical:
                Set($"<b>CRIT!</b> {amount}", abilityColor);
                scale = criticalScale;
                break;

            case CombatTextType.IncomingDamage:
                Set($"-{amount}", incomingColor);
                break;

            case CombatTextType.Healing:
                Set($"+{amount}", healingColor);
                break;

            case CombatTextType.Experience:
                Set($"+{amount} XP", experienceColor);
                break;

            case CombatTextType.Evade:
                Set(e.label ?? "Evade", evadeColor);
                scale = messageScale;
                break;

            case CombatTextType.EnterCombat:
                Set(e.label ?? "+Combat", combatStateColor);
                scale = messageScale;
                break;

            case CombatTextType.LeaveCombat:
                Set(e.label ?? "-Combat", combatStateColor);
                scale = messageScale;
                break;

            case CombatTextType.PowerGain:
                Set($"+{amount} {e.label}", powerColor);
                break;

            case CombatTextType.LowHealth:
            case CombatTextType.LowPower:
                Set(e.label ?? "Low", warningColor);
                scale = messageScale;
                break;

            default: // Damage (auto attack)
                Set(amount, autoAttackColor);
                break;
        }

        driftDirection = new Vector3(
            Random.Range(-horizontalDrift, horizontalDrift),
            riseSpeed,
            0f);

        transform.position = e.worldPosition +
                             new Vector3(Random.Range(-0.2f, 0.2f),
                                         Random.Range(0f, 0.25f),
                                         0f);

        transform.localScale = Vector3.one * scale;

        age = 0f;
        canvasGroup.alpha = 1f;
    }

    private void Set(string value, Color color)
    {
        text.text = value;
        text.color = color;
    }

    void Update()
    {
        age += Time.deltaTime;

        if (age >= lifetime)
        {
            Destroy(gameObject);
            return;
        }

        transform.position += driftDirection * Time.deltaTime;

        // Keep the text facing the camera.
        if (targetCamera != null)
            transform.rotation = targetCamera.transform.rotation;

        // Fade during the second half.
        float t = age / lifetime;
        canvasGroup.alpha = Mathf.Clamp01(1f - Mathf.InverseLerp(0.45f, 1f, t));
    }
}
