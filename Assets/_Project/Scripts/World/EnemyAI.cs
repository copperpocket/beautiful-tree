using System;
using UnityEngine;
using UnityEngine.AI;

public enum EnemyState
{
    Idle,
    Chase,
    Attack,
    Return,
    Dead
}

/// <summary>
/// Basic melee enemy: idles at home, aggroes on proximity or damage, chases,
/// attacks on a swing timer, and evades home (full heal, immune) if pulled
/// too far. Damage lands on the attack animation's Impact event.
/// Attach to the enemy root alongside Health.
/// </summary>
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(NavMeshAgent))]
public class EnemyAI : MonoBehaviour
{
    [Header("Detection")]
    [Tooltip("Player inside this distance triggers aggro.")]
    public float aggroRange = 10f;
    [Tooltip("Getting hit triggers aggro, even from outside aggro range.")]
    public bool aggroOnDamage = true;

    [Header("Leash")]
    [Tooltip("Distance from home before the enemy gives up and returns.")]
    public float leashRange = 30f;
    [Tooltip("Speed multiplier while returning home.")]
    public float returnSpeedMultiplier = 1.5f;

    [Header("Movement")]
    public float chaseSpeed = 4.5f;
    [Tooltip("Degrees per second when turning to face the target.")]
    public float turnSpeed = 540f;
    [Tooltip("Seconds between path updates while chasing.")]
    public float repathInterval = 0.2f;

    [Header("NavMesh Placement")]
    [Tooltip("How far to search for the NavMesh when placing the enemy.")]
    public float navMeshSearchRadius = 10f;
    [Tooltip("Seconds between placement retries if the enemy is off the NavMesh.")]
    public float navMeshRetryInterval = 1f;

    [Header("Attack")]
    public float attackRange = 2.2f;
    [Tooltip("Seconds between swings.")]
    public float attackSpeed = 2f;
    public float attackDamage = 6f;
    [Tooltip("Damage varies by plus or minus this fraction.")]
    public float damageVariance = 0.15f;
    [Tooltip("Degrees off-centre the player can be and still be hit.")]
    public float facingTolerance = 60f;

    [Header("Impact Timing")]
    [Tooltip("Seconds after the swing before damage lands if no Impact event arrives.")]
    public float impactFallback = 0.35f;
    [Tooltip("The hit misses if the player is farther than attackRange times this at impact.")]
    public float impactRangeTolerance = 1.5f;

    [Header("Debug")]
    public bool logStateChanges = false;

    public EnemyState State { get; private set; } = EnemyState.Idle;

    /// <summary>(previous, next)</summary>
    public event Action<EnemyState, EnemyState> OnStateChanged;

    /// <summary>Raised at the start of every swing. Animation listens to this.</summary>
    public event Action<PlayerStats> OnAttack;

    public bool IsInCombat => State == EnemyState.Chase || State == EnemyState.Attack;

    public float CurrentSpeed =>
        AgentReady ? new Vector3(agent.velocity.x, 0f, agent.velocity.z).magnitude : 0f;

    /// <summary>True only when the agent can safely be moved or stopped.</summary>
    private bool AgentReady => agent != null && agent.enabled && agent.isOnNavMesh;

    private Health health;
    private NavMeshAgent agent;
    private AnimationImpactRelay impactRelay;
    private readonly DeferredHitQueue pendingHits = new();

    private PlayerStats player;
    private PlayerStats target;

    private Vector3 homePosition;
    private Quaternion homeRotation;
    private float nextAttackTime;
    private float nextRepathTime;
    private float nextPlacementTry;
    private bool warnedOffNavMesh;

    void Awake()
    {
        health = GetComponent<Health>();
        agent = GetComponent<NavMeshAgent>();
        impactRelay = GetComponentInChildren<AnimationImpactRelay>();
    }

    void OnEnable()
    {
        health.OnDamaged += HandleDamaged;
        health.OnDied += HandleDied;
        if (impactRelay != null) impactRelay.OnImpact += HandleImpact;
    }

    void OnDisable()
    {
        health.OnDamaged -= HandleDamaged;
        health.OnDied -= HandleDied;
        if (impactRelay != null) impactRelay.OnImpact -= HandleImpact;
    }

    void Start()
    {
        TryPlaceOnNavMesh();

        homePosition = transform.position;
        homeRotation = transform.rotation;

        agent.speed = chaseSpeed;
        agent.angularSpeed = turnSpeed;
        agent.stoppingDistance = attackRange * 0.8f;

        // One player for now. Swap for a target list when you add multiplayer or pets.
        player = FindFirstObjectByType<PlayerStats>();
    }

    void Update()
    {
        if (State == EnemyState.Dead)
            return;

        pendingHits.Tick();

        // Never touch the agent while it's off the NavMesh. Keep retrying instead.
        if (!AgentReady)
        {
            if (Time.time >= nextPlacementTry)
            {
                nextPlacementTry = Time.time + navMeshRetryInterval;
                if (TryPlaceOnNavMesh())
                    homePosition = transform.position;
            }
            return;
        }

        switch (State)
        {
            case EnemyState.Idle:   TickIdle();   break;
            case EnemyState.Chase:  TickChase();  break;
            case EnemyState.Attack: TickAttack(); break;
            case EnemyState.Return: TickReturn(); break;
        }
    }

    /// <summary>Moves the agent onto the nearest NavMesh point. Returns true on success.</summary>
    private bool TryPlaceOnNavMesh()
    {
        if (agent == null || !agent.enabled)
            return false;

        if (agent.isOnNavMesh)
            return true;

        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit,
                                   navMeshSearchRadius, NavMesh.AllAreas))
        {
            agent.Warp(hit.position);
            warnedOffNavMesh = false;
            return agent.isOnNavMesh;
        }

        if (!warnedOffNavMesh)
        {
            Debug.LogWarning($"{name}: no NavMesh within {navMeshSearchRadius} m. " +
                             "Bake the NavMeshSurface, or move the spawner onto the blue area.", this);
            warnedOffNavMesh = true;
        }

        return false;
    }

    // ---- States ----

    private void TickIdle()
    {
        if (player == null || player.IsDead)
            return;

        if (FlatDistance(transform.position, player.transform.position) <= aggroRange)
            Engage(player);
    }

    private void TickChase()
    {
        if (ShouldGiveUp())
        {
            StartReturn();
            return;
        }

        float dist = FlatDistance(transform.position, target.transform.position);
        if (dist <= attackRange)
        {
            SetState(EnemyState.Attack);
            return;
        }

        agent.isStopped = false;

        if (Time.time >= nextRepathTime)
        {
            agent.SetDestination(target.transform.position);
            nextRepathTime = Time.time + repathInterval;
        }
    }

    private void TickAttack()
    {
        if (ShouldGiveUp())
        {
            StartReturn();
            return;
        }

        // Stand still and turn to face the player.
        agent.isStopped = true;
        FaceTowards(target.transform.position);

        // A little extra margin so the enemy doesn't flicker between chase and attack.
        float dist = FlatDistance(transform.position, target.transform.position);
        if (dist > attackRange * 1.15f)
        {
            SetState(EnemyState.Chase);
            return;
        }

        if (Time.time >= nextAttackTime && IsFacing(target.transform.position))
            Swing();
    }

    private void TickReturn()
    {
        if (agent.pathPending)
            return;

        if (agent.remainingDistance <= agent.stoppingDistance + 0.1f)
        {
            transform.rotation = homeRotation;
            agent.speed = chaseSpeed;
            agent.stoppingDistance = attackRange * 0.8f;
            health.IsInvulnerable = false;
            target = null;
            SetState(EnemyState.Idle);
        }
    }

    // ---- Transitions ----

    private void Engage(PlayerStats newTarget)
    {
        if (newTarget == null || newTarget.IsDead)
            return;

        target = newTarget;
        nextRepathTime = 0f;
        SetState(EnemyState.Chase);
    }

    private void StartReturn()
    {
        // WoW-style evade: heal fully and ignore damage until home.
        pendingHits.Clear();
        health.ResetHealth();
        health.IsInvulnerable = true;

        if (AgentReady)
        {
            agent.isStopped = false;
            agent.speed = chaseSpeed * returnSpeedMultiplier;
            agent.stoppingDistance = 0.1f;
            agent.SetDestination(homePosition);
        }

        SetState(EnemyState.Return);
    }

    private bool ShouldGiveUp()
    {
        if (target == null || target.IsDead)
            return true;

        return FlatDistance(transform.position, homePosition) > leashRange;
    }

    private void Swing()
    {
        nextAttackTime = Time.time + attackSpeed;

        // Animation starts now. Damage waits for the impact frame.
        OnAttack?.Invoke(target);

        PlayerStats victim = target;
        float damage = attackDamage *
                       UnityEngine.Random.Range(1f - damageVariance, 1f + damageVariance);

        pendingHits.Add(() =>
        {
            if (State == EnemyState.Dead || victim == null || victim.IsDead)
                return;

            // Stepping away before the impact dodges the hit.
            if (FlatDistance(transform.position, victim.transform.position) >
                attackRange * impactRangeTolerance)
                return;

            victim.TakeDamage(damage, gameObject);
        }, impactFallback);
    }

    // ---- Events ----

    private void HandleImpact()
    {
        pendingHits.ResolveNext();
    }

    private void HandleDamaged(Health victim, float amount, GameObject source)
    {
        if (!aggroOnDamage || State != EnemyState.Idle || source == null)
            return;

        var attacker = source.GetComponentInParent<PlayerStats>();
        if (attacker != null)
            Engage(attacker);
    }

    private void HandleDied(Health victim, GameObject killer)
    {
        pendingHits.Clear();
        SetState(EnemyState.Dead);

        if (AgentReady)
            agent.isStopped = true;

        // Stop the agent fighting the corpse tip-over.
        if (agent != null)
            agent.enabled = false;

        enabled = false;
    }

    // ---- Helpers ----

    private void SetState(EnemyState next)
    {
        if (State == next)
            return;

        EnemyState previous = State;
        State = next;

        if (logStateChanges)
            Debug.Log($"{name}: {previous} -> {next}", this);

        OnStateChanged?.Invoke(previous, next);
    }

    private void FaceTowards(Vector3 point)
    {
        Vector3 dir = point - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f)
            return;

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            Quaternion.LookRotation(dir),
            turnSpeed * Time.deltaTime);
    }

    private bool IsFacing(Vector3 point)
    {
        Vector3 dir = point - transform.position;
        dir.y = 0f;
        return Vector3.Angle(transform.forward, dir) <= facingTolerance;
    }

    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, aggroRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        Gizmos.color = Color.cyan;
        Vector3 home = Application.isPlaying ? homePosition : transform.position;
        Gizmos.DrawWireSphere(home, leashRange);
    }
}
