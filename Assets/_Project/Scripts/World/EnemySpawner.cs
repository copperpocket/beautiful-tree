using UnityEngine;

/// <summary>
/// Spawns one enemy and respawns it after it dies. Place an empty GameObject
/// where the enemy should stand and attach this.
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    [Header("Spawn")]
    [Tooltip("Prefab must have a Health component on its root.")]
    public GameObject enemyPrefab;

    [Tooltip("Seconds after death before respawning. Corpse lingers separately.")]
    public float respawnDelay = 10f;

    [Tooltip("Random offset from this point, so respawns aren't pixel-identical.")]
    public float spawnRadius = 0f;

    [Header("Overrides (0 = use prefab value)")]
    public int levelOverride = 0;
    public float maxHealthOverride = 0f;
    public int xpRewardOverride = 0;
    public string nameOverride = "";

    [Header("Debug")]
    public bool logSpawns = true;

    private float respawnAt = -1f;
    private Health current;

    void Start()
    {
        Spawn();
    }

    void Update()
    {
        if (respawnAt > 0f && Time.time >= respawnAt)
        {
            respawnAt = -1f;
            Spawn();
        }
    }

    private void Spawn()
    {
        if (enemyPrefab == null)
        {
            Debug.LogError("EnemySpawner: No enemy prefab assigned.", this);
            return;
        }

        Vector2 offset = spawnRadius > 0f
            ? Random.insideUnitCircle * spawnRadius
            : Vector2.zero;

        Vector3 pos = transform.position + new Vector3(offset.x, 0f, offset.y);

        GameObject go = Instantiate(enemyPrefab, pos, transform.rotation);
        go.name = $"{enemyPrefab.name} (spawned by {name})";

        current = go.GetComponent<Health>();
        if (current == null)
        {
            Debug.LogError("EnemySpawner: prefab has no Health component on its root.", this);
            return;
        }

        if (levelOverride > 0)      current.level = levelOverride;
        if (maxHealthOverride > 0f) current.maxHealth = maxHealthOverride;
        if (xpRewardOverride > 0)   current.xpReward = xpRewardOverride;
        if (!string.IsNullOrEmpty(nameOverride)) current.displayName = nameOverride;

        // Overrides applied after Awake set CurrentHealth, so refresh it.
        current.ResetHealth();

        current.OnDied += HandleDeath;

        if (logSpawns) Debug.Log($"{name}: spawned {current.displayName}", this);
    }

    private void HandleDeath(Health victim, GameObject killer)
    {
        victim.OnDied -= HandleDeath;
        respawnAt = Time.time + respawnDelay;

        if (logSpawns)
            Debug.Log($"{name}: {victim.displayName} died, respawning in {respawnDelay}s", this);
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position + Vector3.up, 0.5f);
        if (spawnRadius > 0f)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, spawnRadius);
        }
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 2f);
    }
}
