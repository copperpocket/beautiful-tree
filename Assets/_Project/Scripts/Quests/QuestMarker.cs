using UnityEngine;

public enum QuestMarkerState { None, Available, InProgress, Ready }

/// <summary>
/// 3D quest marker. Shows ! or ?, tints it, bobs, and either spins slowly
/// or turns to face the camera (upright). Attach to the marker's parent object.
/// </summary>
[DefaultExecutionOrder(1000)]   // after the camera has moved
public class QuestMarker : MonoBehaviour
{
    [Header("Meshes")]
    public GameObject exclamation;
    public GameObject question;

    [Header("Colours")]
    public Color readyColor = new Color(1f, 0.85f, 0.15f);
    public Color inProgressColor = new Color(0.6f, 0.6f, 0.6f);

    [Header("Motion")]
    [Tooltip("Degrees per second. 0 = face the camera instead of spinning.")]
    public float spinSpeed = 0f;
    public float bobHeight = 0.08f;
    public float bobSpeed = 2f;

    private Vector3 basePosition;
    private Renderer[] renderers;
    private MaterialPropertyBlock block;
    private QuestMarkerState current = (QuestMarkerState)(-1);
    private Camera cam;

    void Awake()
    {
        basePosition = transform.localPosition;
        renderers = GetComponentsInChildren<Renderer>(true);
        block = new MaterialPropertyBlock();
        SetState(QuestMarkerState.None);
    }

    public void SetState(QuestMarkerState state)
    {
        if (state == current)
            return;

        current = state;

        if (exclamation) exclamation.SetActive(state == QuestMarkerState.Available);
        if (question) question.SetActive(state == QuestMarkerState.InProgress ||
                                         state == QuestMarkerState.Ready);

        // Tint every marker mesh without creating new materials.
        Color color = state == QuestMarkerState.InProgress ? inProgressColor : readyColor;
        foreach (var r in renderers)
        {
            r.GetPropertyBlock(block);
            block.SetColor("_BaseColor", color);
            r.SetPropertyBlock(block);
        }
    }

    void LateUpdate()
    {
        if (current == QuestMarkerState.None)
            return;

        transform.localPosition = basePosition +
            Vector3.up * (Mathf.Sin(Time.time * bobSpeed) * bobHeight);

        if (spinSpeed != 0f)
        {
            transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
            return;
        }

        // Upright billboard: turn left/right to face the camera, never tilt.
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        Vector3 forward = cam.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
    }
}
