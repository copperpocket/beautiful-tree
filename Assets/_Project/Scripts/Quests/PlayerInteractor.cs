using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Press Interact (F) to talk to the nearest quest giver in range.
/// Attach to the Player.
/// </summary>
[RequireComponent(typeof(PlayerInput))]
[RequireComponent(typeof(QuestLog))]
public class PlayerInteractor : MonoBehaviour
{
    public float interactRange = 3f;
    [Tooltip("Set to Interactable.")]
    public LayerMask interactableMask;

    private InputAction interactAction;
    private QuestLog questLog;
    private PlayerStats stats;

    void Awake()
    {
        questLog = GetComponent<QuestLog>();
        stats = GetComponent<PlayerStats>();

        interactAction = GetComponent<PlayerInput>().actions.FindAction("Interact");
        if (interactAction == null)
            Debug.LogWarning("PlayerInteractor: no 'Interact' action found. " +
                             "Add it to PlayerControls and click Save Asset.", this);
    }

    void Update()
    {
        if (interactAction == null || !interactAction.WasPressedThisFrame())
            return;

        if (stats != null && stats.IsDead)
            return;

        TryInteract();
    }

    private void TryInteract()
    {
        Collider[] hits = Physics.OverlapSphere(
            transform.position, interactRange, interactableMask, QueryTriggerInteraction.Collide);

        QuestGiver nearest = null;
        float nearestDist = float.MaxValue;

        foreach (var hit in hits)
        {
            var giver = hit.GetComponentInParent<QuestGiver>();
            if (giver == null)
                continue;

            float dist = Vector3.Distance(transform.position, giver.transform.position);
            if (dist < nearestDist)
            {
                nearest = giver;
                nearestDist = dist;
            }
        }

        if (nearest == null)
        {
            Debug.Log("Nothing to interact with.");
            return;
        }

        nearest.Interact(questLog);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, interactRange);
    }
}
