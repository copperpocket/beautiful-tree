using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

/// <summary>
/// WoW-style camera. Left drag orbits, right drag steers the character, scroll
/// zooms. Orbits a fixed point measured from the feet. Scrolling in past the
/// minimum distance enters first person: the camera sits at eye level and the
/// character model is hidden (its shadow stays). Mouse presses over UI are ignored.
/// </summary>
public class ThirdPersonCamera : MonoBehaviour
{
    [Header("Target")]
    public Transform target;

    [Header("Pivot")]
    [Tooltip("Height above the character's feet that the camera orbits around. Roughly eye level.")]
    public float pivotHeightFromFeet = 1.65f;

    [Header("Rotation")]
    public float sensitivity = 0.15f;
    public float minPitch = -30f;
    public float maxPitch = 80f;

    [Header("Zoom")]
    public float distance = 8f;
    [Tooltip("Closest third-person distance. Scrolling in past this enters first person.")]
    public float minZoomDistance = 0.8f;
    public float maxZoomDistance = 25f;
    [Range(0.01f, 0.5f)]
    [Tooltip("Each scroll moves this fraction of the current distance.")]
    public float zoomStepPercent = 0.08f;
    [Tooltip("Smallest possible zoom step, in metres.")]
    public float minZoomStep = 0.15f;
    public float zoomSmooth = 12f;

    [Header("First Person")]
    public bool allowFirstPerson = true;
    [Tooltip("Pitch limits while in first person (looking down/up).")]
    public float firstPersonMinPitch = -80f;
    public float firstPersonMaxPitch = 80f;
    [Tooltip("Hide the character once the camera is closer than this.")]
    public float hideModelDistance = 0.4f;
    [Tooltip("Pushes the camera slightly forward of the eyes.")]
    public float firstPersonForwardOffset = 0.1f;
    [Tooltip("Camera near clip plane in first person, so nearby objects don't cut off.")]
    public float firstPersonNearClip = 0.05f;

    [Header("Collision")]
    [Tooltip("Exclude the Player's layer, or the cast hits the character itself.")]
    public LayerMask obstacleMask = ~0;
    public float cameraRadius = 0.3f;

    public bool IsFirstPerson => distance <= 0f;

    private InputAction lookAction, orbitAction, steerAction, zoomAction;
    private CharacterController targetController;
    private Camera cam;
    private float defaultNearClip;

    private float yaw;
    private float pitch = 20f;
    private float currentDistance;
    private float lastTargetYaw;
    private bool dragging;
    private Vector2 savedCursorPos;

    // A press that starts over UI is ignored until released.
    private bool orbitBlocked, steerBlocked;

    // Character renderers, hidden in first person.
    private readonly List<Renderer> modelRenderers = new();
    private readonly List<ShadowCastingMode> originalShadows = new();
    private bool modelHidden;

    void Start()
    {
        cam = GetComponent<Camera>();
        if (cam != null) defaultNearClip = cam.nearClipPlane;

        var playerInput = target ? target.GetComponent<PlayerInput>() : null;
        if (playerInput != null)
        {
            lookAction  = playerInput.actions["Look"];
            orbitAction = playerInput.actions["OrbitCamera"];
            steerAction = playerInput.actions["SteerCharacter"];
            zoomAction  = playerInput.actions["Zoom"];
        }
        else
        {
            Debug.LogError("ThirdPersonCamera: Target is not set, or it has no " +
                           "PlayerInput component. Drag the Player into Target.", this);
        }

        if (target != null)
        {
            targetController = target.GetComponent<CharacterController>();
            yaw = target.eulerAngles.y;
            lastTargetYaw = yaw;

            // Only the character's meshes. Trails and particles (spell effects) stay visible.
            foreach (var r in target.GetComponentsInChildren<Renderer>(true))
            {
                if (r is SkinnedMeshRenderer || r is MeshRenderer)
                {
                    modelRenderers.Add(r);
                    originalShadows.Add(r.shadowCastingMode);
                }
            }
        }

        distance = Mathf.Clamp(distance, minZoomDistance, maxZoomDistance);
        currentDistance = distance;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    /// <summary>The character's feet. The Player's own position is the capsule centre.</summary>
    private Vector3 FeetPosition()
    {
        if (targetController == null)
            return target.position;

        Vector3 centre = target.TransformPoint(targetController.center);
        return centre - Vector3.up * (targetController.height * 0.5f);
    }

    void LateUpdate()
    {
        if (target == null || lookAction == null) return;

        UpdateUIBlocks();

        bool orbiting = orbitAction.IsPressed() && !orbitBlocked;
        bool steering = steerAction.IsPressed() && !steerBlocked;

        HandleCursor(orbiting || steering);

        // 1. If the character turned itself (A/D), carry the camera with it.
        float targetYaw = target.eulerAngles.y;
        if (!steering)
            yaw += Mathf.DeltaAngle(lastTargetYaw, targetYaw);

        // 2. Either mouse button drags the camera.
        float lowPitch = IsFirstPerson ? firstPersonMinPitch : minPitch;
        float highPitch = IsFirstPerson ? firstPersonMaxPitch : maxPitch;

        if (orbiting || steering)
        {
            Vector2 look = lookAction.ReadValue<Vector2>();
            yaw   += look.x * sensitivity;
            pitch -= look.y * sensitivity;
        }
        pitch = Mathf.Clamp(pitch, lowPitch, highPitch);

        // 3. Right drag only: the character's facing follows the camera.
        if (steering)
            target.rotation = Quaternion.Euler(0f, yaw, 0f);

        lastTargetYaw = target.eulerAngles.y;

        // 4. Zoom, with a snap into and out of first person.
        float scroll = zoomAction != null ? zoomAction.ReadValue<float>() : 0f;
        if (Mathf.Abs(scroll) > 0.01f && !UIInputGuard.IsPointerOverUI())
            ApplyZoom(scroll > 0f);

        currentDistance = Mathf.Lerp(currentDistance, distance,
                                     1f - Mathf.Exp(-zoomSmooth * Time.deltaTime));
        if (currentDistance < 0.01f) currentDistance = 0f;

        // 5. Place the camera around a fixed point above the feet.
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 pivot = FeetPosition() + Vector3.up * pivotHeightFromFeet;
        Vector3 dir = rotation * Vector3.back;

        float wanted = currentDistance;
        if (currentDistance > 0f &&
            Physics.SphereCast(pivot, cameraRadius, dir, out RaycastHit hit,
                               currentDistance, obstacleMask,
                               QueryTriggerInteraction.Ignore))
            wanted = hit.distance;

        Vector3 position = pivot + dir * wanted;

        // Nudge forward of the eyes as we reach first person.
        float fpBlend = 1f - Mathf.Clamp01(currentDistance / Mathf.Max(0.01f, hideModelDistance));
        Vector3 flatForward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
        position += flatForward * firstPersonForwardOffset * fpBlend;

        transform.position = position;
        transform.rotation = rotation;

        // 6. Hide the character up close, and tighten the near clip plane.
        SetModelHidden(currentDistance < hideModelDistance);
        if (cam != null)
            cam.nearClipPlane = currentDistance < hideModelDistance ? firstPersonNearClip : defaultNearClip;
    }

    private void ApplyZoom(bool zoomIn)
    {
        if (zoomIn)
        {
            if (IsFirstPerson) return;

            float step = Mathf.Max(minZoomStep, distance * zoomStepPercent);
            float next = distance - step;

            // Past the closest third-person distance: snap into first person.
            if (next < minZoomDistance)
                distance = allowFirstPerson ? 0f : minZoomDistance;
            else
                distance = next;
        }
        else
        {
            // Out of first person: back to the closest third-person distance.
            if (IsFirstPerson)
            {
                distance = minZoomDistance;
                return;
            }

            float step = Mathf.Max(minZoomStep, distance * zoomStepPercent);
            distance = Mathf.Min(distance + step, maxZoomDistance);
        }
    }

    private void SetModelHidden(bool hide)
    {
        if (hide == modelHidden) return;
        modelHidden = hide;

        for (int i = 0; i < modelRenderers.Count; i++)
        {
            if (modelRenderers[i] == null) continue;

            // ShadowsOnly hides the mesh but keeps its shadow on the ground, like WoW.
            modelRenderers[i].shadowCastingMode = hide ? ShadowCastingMode.ShadowsOnly : originalShadows[i];
        }
    }

    private void UpdateUIBlocks()
    {
        if (orbitAction.WasPressedThisFrame())
            orbitBlocked = UIInputGuard.IsPointerOverUI();
        else if (!orbitAction.IsPressed())
            orbitBlocked = false;

        if (steerAction.WasPressedThisFrame())
            steerBlocked = UIInputGuard.IsPointerOverUI();
        else if (!steerAction.IsPressed())
            steerBlocked = false;
    }

    private void HandleCursor(bool wantDrag)
    {
        if (wantDrag && !dragging)
        {
            savedCursorPos = Mouse.current.position.ReadValue();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            dragging = true;
        }
        else if (!wantDrag && dragging)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Mouse.current.WarpCursorPosition(savedCursorPos);
            dragging = false;
        }
    }

    // Safety net: never leave the cursor trapped, or the model hidden, if play mode stops.
    void OnDisable()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SetModelHidden(false);
        if (cam != null) cam.nearClipPlane = defaultNearClip;
    }
}
