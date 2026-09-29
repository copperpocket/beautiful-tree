using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Selects an enemy by clicking it, or by cycling with Tab. Attach to the Player.
/// Left click selects. Right click selects and requests auto-attack.
/// A press that drags orbits or steers the camera instead, matching WoW.
/// </summary>
public class PlayerTargeting : MonoBehaviour
{
    [Header("Selection")]
    public LayerMask targetableMask;          // set to Enemy in the Inspector
    public float maxSelectDistance = 100f;

    [Header("Tab Cycling")]
    public float tabRange = 30f;
    [Tooltip("Only cycle to targets roughly in front of the camera.")]
    public bool tabRequiresInFront = true;

    [Header("Click vs Drag")]
    [Tooltip("A press shorter than this that barely moves counts as a click.")]
    public float clickMaxDuration = 0.25f;
    public float clickMaxPixelDrift = 12f;

    [Header("Right Click")]
    [Tooltip("Right-clicking an enemy engages auto-attack as well as selecting it.")]
    public bool rightClickAttacks = true;

    public Health CurrentTarget { get; private set; }

    public event Action<Health> OnTargetChanged;    // null when cleared
    /// <summary>Raised when the player right-clicks an enemy. PlayerCombat listens.</summary>
    public event Action<Health> OnAttackRequested;

    private InputAction orbitAction, steerAction, tabAction, clearAction;
    private Camera cam;

    // One tracker per mouse button, so the two never interfere.
    private struct ClickTracker
    {
        public float pressTime;
        public Vector2 pressPos;
        public float drift;
    }

    private ClickTracker leftClick, rightClick;
    private int tabIndex;

    void Awake()
    {
        var playerInput = GetComponent<PlayerInput>();
        orbitAction = playerInput.actions["OrbitCamera"];
        steerAction = playerInput.actions["SteerCharacter"];
        tabAction   = playerInput.actions["TargetNearest"];
        clearAction = playerInput.actions["ClearTarget"];
    }

    void Start()
    {
        cam = Camera.main;
    }

    void Update()
    {
        // Target died or was destroyed while selected.
        if (CurrentTarget != null && CurrentTarget.IsDead)
            SetTarget(null);

        // Left click: select, and deselect when clicking empty space.
        if (TrackClick(orbitAction, ref leftClick, out Vector2 leftPos))
            ResolveClick(leftPos, deselectOnMiss: true, requestAttack: false);

        // Right click: select and engage. Clicking empty space does nothing.
        if (TrackClick(steerAction, ref rightClick, out Vector2 rightPos))
            ResolveClick(rightPos, deselectOnMiss: false, requestAttack: rightClickAttacks);

        if (tabAction.WasPressedThisFrame())
            CycleNearest();

        if (clearAction.WasPressedThisFrame())
            SetTarget(null);
    }

    /// <summary>
    /// Returns true on the frame a button is released after a press that counts
    /// as a click rather than a drag. Outputs the screen position of the press.
    /// </summary>
    private bool TrackClick(InputAction action, ref ClickTracker tracker, out Vector2 pressPos)
    {
        pressPos = tracker.pressPos;

        if (action.WasPressedThisFrame())
        {
            tracker.pressTime = Time.time;
            tracker.pressPos = Mouse.current.position.ReadValue();
            tracker.drift = 0f;
            pressPos = tracker.pressPos;
            return false;
        }

        // The cursor is locked while dragging, so accumulate delta, not position.
        if (action.IsPressed())
            tracker.drift += Mouse.current.delta.ReadValue().magnitude;

        if (!action.WasReleasedThisFrame()) return false;

        return Time.time - tracker.pressTime <= clickMaxDuration &&
               tracker.drift <= clickMaxPixelDrift;
    }

    private void ResolveClick(Vector2 screenPos, bool deselectOnMiss, bool requestAttack)
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        Ray ray = cam.ScreenPointToRay(screenPos);

        if (Physics.Raycast(ray, out RaycastHit hit, maxSelectDistance,
                            targetableMask, QueryTriggerInteraction.Ignore))
        {
            var health = hit.collider.GetComponentInParent<Health>();

            if (health != null && !health.IsDead)
            {
                SetTarget(health);
                if (requestAttack) OnAttackRequested?.Invoke(health);
                return;
            }
        }

        if (deselectOnMiss) SetTarget(null);
    }

    private void CycleNearest()
    {
        var candidates = new List<Health>();

        // FindObjectsByType is fine at this scale. Swap for a registry that
        // enemies add themselves to once you have hundreds.
        foreach (var h in FindObjectsByType<Health>(FindObjectsSortMode.None))
        {
            if (h.IsDead) continue;

            Vector3 toTarget = h.AimPosition - transform.position;
            if (toTarget.sqrMagnitude > tabRange * tabRange) continue;

            if (tabRequiresInFront && cam != null)
            {
                Vector3 flat = toTarget; flat.y = 0f;
                Vector3 fwd = cam.transform.forward; fwd.y = 0f;
                if (Vector3.Angle(fwd, flat) > 75f) continue;
            }

            candidates.Add(h);
        }

        if (candidates.Count == 0)
        {
            SetTarget(null);
            return;
        }

        candidates.Sort((a, b) =>
            Vector3.SqrMagnitude(a.transform.position - transform.position)
            .CompareTo(Vector3.SqrMagnitude(b.transform.position - transform.position)));

        if (CurrentTarget == null) tabIndex = 0;
        else
        {
            int existing = candidates.IndexOf(CurrentTarget);
            tabIndex = existing >= 0 ? (existing + 1) % candidates.Count : 0;
        }

        SetTarget(candidates[tabIndex]);
    }

    public void SetTarget(Health target)
    {
        if (CurrentTarget == target) return;

        CurrentTarget = target;
        OnTargetChanged?.Invoke(target);

        if (target != null)
            Debug.Log($"Targeted {target.DisplayName} (level {target.Level})");
    }
}
