using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Makes an idle enemy wander around its spawn point. Only acts while
/// EnemyAI is Idle, so aggro, chase and leashing work as before.
/// Attach to the enemy root next to EnemyAI.
/// </summary>
[RequireComponent(typeof(EnemyAI))]
[RequireComponent(typeof(NavMeshAgent))]
public class EnemyPatrol : MonoBehaviour
{
    [Header("Wander")]
    [Tooltip("How far from the spawn point the enemy wanders. Keep it well inside the leash range.")]
    public float wanderRadius = 8f;

    [Tooltip("Walking speed while patrolling. 2.5 matches the walk animation.")]
    public float walkSpeed = 2.5f;

    [Tooltip("Seconds to wait at each point (random between min and max).")]
    public Vector2 pauseRange = new Vector2(2f, 5f);

    [Tooltip("Distance at which a patrol point counts as reached.")]
    public float arriveDistance = 0.5f;

    private EnemyAI ai;
    private NavMeshAgent agent;

    private Vector3 home;
    private bool hasHome;
    private bool moving;
    private float waitUntil;

    void Awake()
    {
        ai = GetComponent<EnemyAI>();
        agent = GetComponent<NavMeshAgent>();
    }

    void OnEnable()
    {
        ai.OnStateChanged += HandleStateChanged;
    }

    void OnDisable()
    {
        ai.OnStateChanged -= HandleStateChanged;
    }

    void Start()
    {
        if (wanderRadius >= ai.leashRange)
            Debug.LogWarning($"{name}: Wander Radius should be smaller than Leash Range.", this);
    }

    void Update()
    {
        if (ai.State != EnemyState.Idle)
            return;

        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            return;

        // Record the spawn point once the enemy is placed on the NavMesh.
        if (!hasHome)
        {
            home = transform.position;
            hasHome = true;
            waitUntil = Time.time + RandomPause();
            return;
        }

        if (moving)
        {
            if (!agent.pathPending && agent.remainingDistance <= arriveDistance)
            {
                moving = false;
                waitUntil = Time.time + RandomPause();
            }
            return;
        }

        if (Time.time < waitUntil)
            return;

        if (TryPickPoint(out Vector3 destination))
        {
            agent.isStopped = false;
            agent.speed = walkSpeed;
            agent.stoppingDistance = 0.1f;
            agent.SetDestination(destination);
            moving = true;
        }
        else
        {
            // No valid point this time (e.g. near a cliff). Try again shortly.
            waitUntil = Time.time + 1f;
        }
    }

    private bool TryPickPoint(out Vector3 point)
    {
        Vector2 offset = Random.insideUnitCircle * wanderRadius;
        Vector3 candidate = home + new Vector3(offset.x, 0f, offset.y);

        if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2f, NavMesh.AllAreas))
        {
            point = hit.position;
            return true;
        }

        point = home;
        return false;
    }

    private void HandleStateChanged(EnemyState previous, EnemyState next)
    {
        moving = false;

        if (next == EnemyState.Idle)
        {
            // Back home after leashing: pause, then wander again.
            waitUntil = Time.time + RandomPause();
        }
        else if (previous == EnemyState.Idle && agent != null && agent.enabled)
        {
            // Aggro: restore EnemyAI's chase settings.
            agent.speed = ai.chaseSpeed;
            agent.stoppingDistance = ai.attackRange * 0.8f;
        }
    }

    private float RandomPause() => Random.Range(pauseRange.x, pauseRange.y);

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.4f, 1f, 0.4f, 0.8f);
        Vector3 centre = hasHome ? home : transform.position;
        Gizmos.DrawWireSphere(centre, wanderRadius);
    }
}
