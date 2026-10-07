using UnityEngine;
using UnityEngine.InputSystem;

public class ThirdPersonCamera : MonoBehaviour
{
    [Header("Target")]
    public Transform target;
    public float pivotHeight = 1.6f;      // chest height on the character

    [Header("Rotation")]
    public float sensitivity = 0.15f;
    public float minPitch = -30f;
    public float maxPitch = 80f;

    [Header("Zoom")]
    public float distance = 8f;
    public float minDistance = 1.5f;
    public float maxDistance = 20f;
    public float zoomStep = 1.5f;
    public float zoomSmooth = 12f;

    [Header("Collision")]
    [Tooltip("Exclude the Player's layer, or the cast hits the character itself.")]
    public LayerMask obstacleMask = ~0;
    public float cameraRadius = 0.3f;

    private InputAction lookAction, orbitAction, steerAction, zoomAction;

    private float yaw;
    private float pitch = 20f;
    private float currentDistance;
    private float lastTargetYaw;
    private bool dragging;
    private Vector2 savedCursorPos;

    // A press that starts over UI is ignored until released.
    private bool orbitBlocked, steerBlocked;

    void Start()
    {
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
            yaw = target.eulerAngles.y;
            lastTargetYaw = yaw;
        }

        currentDistance = distance;

        // Cursor free by default, so you can click the world and UI.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
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
        if (orbiting || steering)
        {
            Vector2 look = lookAction.ReadValue<Vector2>();
            yaw   += look.x * sensitivity;
            pitch -= look.y * sensitivity;
            pitch  = Mathf.Clamp(pitch, minPitch, maxPitch);
        }

        // 3. Right drag only: the character's facing follows the camera.
        if (steering)
            target.rotation = Quaternion.Euler(0f, yaw, 0f);

        lastTargetYaw = target.eulerAngles.y;

        // 4. Scroll wheel zoom. Ignored while the mouse is over UI.
        float scroll = zoomAction != null ? zoomAction.ReadValue<float>() : 0f;
        if (Mathf.Abs(scroll) > 0.01f && !UIInputGuard.IsPointerOverUI())
            distance = Mathf.Clamp(distance - Mathf.Sign(scroll) * zoomStep,
                                   minDistance, maxDistance);

        currentDistance = Mathf.Lerp(currentDistance, distance,
                                     zoomSmooth * Time.deltaTime);

        // 5. Place the camera, pulling in if a wall is in the way.
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 pivot = target.position + Vector3.up * pivotHeight;
        Vector3 dir = rotation * Vector3.back;

        float wanted = currentDistance;
        if (Physics.SphereCast(pivot, cameraRadius, dir, out RaycastHit hit,
                               currentDistance, obstacleMask,
                               QueryTriggerInteraction.Ignore))
            wanted = hit.distance;

        transform.position = pivot + dir * wanted;
        transform.rotation = rotation;
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

    // Safety net: never leave the cursor trapped if play mode stops mid-drag.
    void OnDisable()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
