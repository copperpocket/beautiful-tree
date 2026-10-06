using TMPro;
using UnityEngine;

/// <summary>
/// Moves a single combat number upward and fades it out.
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

    [Header("Colours")]
    public Color damageColor = new Color(1f, 0.9f, 0.9f);
    public Color criticalColor = new Color(1f, 0.85f, 0.2f);
    public Color incomingColor = new Color(1f, 0.25f, 0.25f);
    public Color healingColor = new Color(0.35f, 1f, 0.45f);
    public Color experienceColor = new Color(0.75f, 0.5f, 1f);

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

    public void Initialize(CombatTextEvent combatEvent)
    {
        if (text == null)
            text = GetComponentInChildren<TMP_Text>();

        targetCamera = Camera.main;

        string amount = Mathf.RoundToInt(combatEvent.amount).ToString();
        bool critical = combatEvent.type == CombatTextType.CriticalDamage;

        switch (combatEvent.type)
        {
            case CombatTextType.CriticalDamage:
                text.text = $"<b>CRIT!</b> {amount}";
                text.color = criticalColor;
                break;

            case CombatTextType.IncomingDamage:
                text.text = $"-{amount}";
                text.color = incomingColor;
                break;

            case CombatTextType.Healing:
                text.text = $"+{amount}";
                text.color = healingColor;
                break;

            case CombatTextType.Experience:
                text.text = $"+{amount} XP";
                text.color = experienceColor;
                break;

            default:
                text.text = amount;
                text.color = damageColor;
                break;
        }

        driftDirection = new Vector3(
            Random.Range(-horizontalDrift, horizontalDrift),
            riseSpeed,
            0f);

        transform.position = combatEvent.worldPosition +
                             new Vector3(Random.Range(-0.2f, 0.2f),
                                         Random.Range(0f, 0.25f),
                                         0f);

        transform.localScale = Vector3.one * (critical ? criticalScale : normalScale);

        age = 0f;
        canvasGroup.alpha = 1f;
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
