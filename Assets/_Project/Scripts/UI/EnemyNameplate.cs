using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// World-space nameplate that floats above an enemy, faces the camera, and
/// tracks its Health. Attach to the nameplate prefab root (the world-space Canvas).
/// Place the prefab as a child of the enemy, or let it find Health on a parent.
/// </summary>
public class EnemyNameplate : MonoBehaviour
{
    [Header("Source")]
    [Tooltip("Leave empty to find Health on a parent object.")]
    public Health health;

    [Header("Placement")]
    [Tooltip("Height above the enemy's root position, in metres.")]
    public float heightOffset = 1.6f;

    [Tooltip("Extra offset applied in the plate's own space after facing the camera. " +
             "Use X to correct sideways misalignment, Y for fine height.")]
    public Vector2 localNudge = Vector2.zero;

    [Header("Scaling")]
    [Tooltip("On: stays the same apparent size at any distance, like WoW.")]
    public bool constantScreenSize = true;
    [Tooltip("Local scale at the reference distance.")]
    public float baseScale = 0.006f;
    public float referenceDistance = 12f;
    [Tooltip("Clamps how large and small the plate can get.")]
    public float minScaleMultiplier = 0.6f;
    public float maxScaleMultiplier = 2.5f;

    [Header("Visibility")]
    [Tooltip("Hidden beyond this distance from the camera.")]
    public float maxVisibleDistance = 45f;
    [Tooltip("Fades out over this distance before maxVisibleDistance.")]
    public float fadeDistance = 8f;
    [Tooltip("On: only shows when damaged or selected, like WoW's default.")]
    public bool hideWhenUndamaged = false;

    [Header("Colours")]
    public Color hostileFill = new Color(0.78f, 0.14f, 0.14f);
    public Color targetedFill = new Color(1f, 0.35f, 0.25f);
    public Color targetedNameColor = new Color(1f, 0.95f, 0.5f);
    public Color normalNameColor = Color.white;

    [Header("Wiring")]
    public CanvasGroup canvasGroup;
    public Image healthFill;
    public Image barFrame;
    public TMP_Text nameText;
    public TMP_Text levelText;

    private Transform anchor;          // what we float above
    private Camera cam;
    private PlayerTargeting targeting;
    private bool isTargeted;
    private bool everDamaged;

    void Awake()
    {
        if (health == null)
            health = GetComponentInParent<Health>();

        if (health == null)
        {
            Debug.LogError("EnemyNameplate: no Health found. Disabling.", this);
            enabled = false;
            return;
        }

        anchor = health.transform;

        // A CanvasGroup lets us fade and hide the whole plate with one value.
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();

        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        // Detach from the enemy so tipping the corpse over doesn't rotate the plate.
        transform.SetParent(null, true);
    }

    void OnEnable()
    {
        if (health != null)
            health.OnHealthPercentChanged += HandleHealthChanged;

        targeting = FindFirstObjectByType<PlayerTargeting>();
        if (targeting != null)
        {
            targeting.OnTargetChanged += HandleTargetChanged;
            HandleTargetChanged(targeting.CurrentTarget);
        }
    }

    void OnDisable()
    {
        if (health != null)
            health.OnHealthPercentChanged -= HandleHealthChanged;

        if (targeting != null)
            targeting.OnTargetChanged -= HandleTargetChanged;
    }

    void Start()
    {
        cam = Camera.main;

        if (nameText)  nameText.text  = health.DisplayName;
        if (levelText) levelText.text = health.Level.ToString();
        if (healthFill) healthFill.fillAmount = 1f;

        ApplyTargetStyling();
    }

    void LateUpdate()
    {
        // The enemy was destroyed (corpse cleanup), so clean ourselves up too.
        if (health == null || anchor == null)
        {
            Destroy(gameObject);
            return;
        }

        if (cam == null)
        {
            cam = Camera.main;
            if (cam == null) return;
        }

        // 1. Float above the enemy.
        Vector3 basePos = anchor.position + Vector3.up * heightOffset;

        // 2. Face the camera. Copying the camera's rotation rather than LookAt keeps
        //    every plate on screen perfectly parallel, which reads much cleaner.
        transform.rotation = cam.transform.rotation;

        // 3. Apply the nudge in the plate's own right/up axes, so it stays correct
        //    from every camera angle.
        transform.position = basePos
                           + transform.right * localNudge.x
                           + transform.up    * localNudge.y;

        // 3. Distance-based scale and fade.
        float dist = Vector3.Distance(cam.transform.position, transform.position);

        if (constantScreenSize)
        {
            float mult = Mathf.Clamp(dist / referenceDistance,
                                     minScaleMultiplier, maxScaleMultiplier);
            transform.localScale = Vector3.one * baseScale * mult;
        }
        else
        {
            transform.localScale = Vector3.one * baseScale;
        }

        canvasGroup.alpha = ComputeAlpha(dist);
    }

    private float ComputeAlpha(float dist)
    {
        if (health.IsDead) return 0f;
        if (dist > maxVisibleDistance) return 0f;

        // Behind the camera.
        if (Vector3.Dot(cam.transform.forward, transform.position - cam.transform.position) <= 0f)
            return 0f;

        if (hideWhenUndamaged && !everDamaged && !isTargeted) return 0f;

        // Fade in over the last stretch of visible range.
        float fadeStart = maxVisibleDistance - fadeDistance;
        if (dist <= fadeStart) return 1f;

        return Mathf.InverseLerp(maxVisibleDistance, fadeStart, dist);
    }

    private void HandleHealthChanged(float pct)
    {
        if (healthFill) healthFill.fillAmount = Mathf.Clamp01(pct);
        if (pct < 1f) everDamaged = true;
    }

    private void HandleTargetChanged(Health newTarget)
    {
        isTargeted = newTarget != null && newTarget == health;
        ApplyTargetStyling();
    }

    private void ApplyTargetStyling()
    {
        if (healthFill) healthFill.color = isTargeted ? targetedFill : hostileFill;
        if (nameText)   nameText.color   = isTargeted ? targetedNameColor : normalNameColor;

        // Brighten the border on the selected enemy.
        if (barFrame)
            barFrame.color = isTargeted
                ? new Color(0.9f, 0.8f, 0.3f, 0.95f)
                : new Color(0.08f, 0.08f, 0.08f, 0.92f);
    }
}
