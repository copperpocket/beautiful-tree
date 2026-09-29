using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Teleports the player back to a spawn point on death, on falling below the
/// world, or on demand. Attach to the Player.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class Respawn : MonoBehaviour
{
    [Header("Spawn")]
    [Tooltip("Leave empty to use the player's position at scene start.")]
    public Transform spawnPoint;

    [Header("Safety Net")]
    [Tooltip("Respawn if the player falls below this Y.")]
    public float killPlaneY = -20f;

    [Tooltip("Seconds to wait after death before respawning. 0 disables auto-respawn.")]
    public float deathRespawnDelay = 2f;

    [Header("Debug")]
    [Tooltip("Key that respawns instantly while prototyping.")]
    public Key manualRespawnKey = Key.R;

    private CharacterController controller;
    private PlayerStats stats;
    private Vector3 spawnPosition;
    private Quaternion spawnRotation;
    private float deathTime = -1f;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        stats = GetComponent<PlayerStats>();

        spawnPosition = spawnPoint ? spawnPoint.position : transform.position;
        spawnRotation = spawnPoint ? spawnPoint.rotation : transform.rotation;
    }

    void OnEnable()
    {
        if (stats != null) stats.OnDeath += HandleDeath;
    }

    void OnDisable()
    {
        if (stats != null) stats.OnDeath -= HandleDeath;
    }

    void Update()
    {
        // Fell off the world.
        if (transform.position.y < killPlaneY)
        {
            DoRespawn();
            return;
        }

        // Auto-respawn after death.
        if (deathTime > 0f && deathRespawnDelay > 0f &&
            Time.time - deathTime >= deathRespawnDelay)
        {
            DoRespawn();
            return;
        }

        // Manual respawn for testing.
        if (Keyboard.current != null &&
            Keyboard.current[manualRespawnKey].wasPressedThisFrame)
            DoRespawn();
    }

    private void HandleDeath() => deathTime = Time.time;

    public void DoRespawn()
    {
        // The CharacterController overrides transform writes, so disable it briefly.
        controller.enabled = false;
        transform.SetPositionAndRotation(spawnPosition, spawnRotation);
        controller.enabled = true;

        deathTime = -1f;

        if (stats != null) stats.Revive();

        Debug.Log($"Respawned at {spawnPosition}.", this);
    }
}
