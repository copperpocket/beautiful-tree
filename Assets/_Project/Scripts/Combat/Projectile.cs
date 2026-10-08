using System;
using UnityEngine;

/// <summary>
/// A homing spell projectile. Flies to the target's aim point and calls onHit
/// on arrival. If the target dies mid-flight it fizzles where the target was.
/// Spawned by DamageAbility. Attach to the projectile prefab root.
/// </summary>
public class Projectile : MonoBehaviour
{
    [Tooltip("Spawned where the projectile lands or fizzles. Optional.")]
    public GameObject impactPrefab;

    [Tooltip("Counts as arrived within this distance of the target.")]
    public float hitRadius = 0.3f;

    [Tooltip("Safety limit. The projectile fizzles if it flies longer than this.")]
    public float maxLifetime = 5f;

    private Health target;
    private Action onHit;
    private float speed;
    private float age;
    private bool targetLost;
    private bool done;
    private Vector3 lastTargetPosition;

    public void Launch(Health newTarget, float flySpeed, Action hitCallback)
    {
        target = newTarget;
        speed = Mathf.Max(0.1f, flySpeed);
        onHit = hitCallback;
        lastTargetPosition = target != null ? target.AimPosition : transform.position + transform.forward * 5f;
    }

    void Update()
    {
        if (done)
            return;

        age += Time.deltaTime;

        // Track the target while it's alive. Afterwards, fly to where it was.
        if (!targetLost && target != null && !target.IsDead)
            lastTargetPosition = target.AimPosition;
        else
            targetLost = true;

        Vector3 toTarget = lastTargetPosition - transform.position;
        float step = speed * Time.deltaTime;

        if (toTarget.magnitude <= Mathf.Max(hitRadius, step))
        {
            transform.position = lastTargetPosition;
            Finish(hit: !targetLost);
            return;
        }

        transform.position += toTarget.normalized * step;
        transform.rotation = Quaternion.LookRotation(toTarget);

        if (age >= maxLifetime)
            Finish(hit: false);
    }

    private void Finish(bool hit)
    {
        done = true;

        if (hit)
            onHit?.Invoke();

        if (impactPrefab != null)
            Instantiate(impactPrefab, transform.position, Quaternion.identity);

        // Let the trail fade out on its own instead of vanishing.
        var trail = GetComponentInChildren<TrailRenderer>();
        if (trail != null)
        {
            trail.transform.SetParent(null, true);
            trail.emitting = false;
            trail.autodestruct = true;
        }

        Destroy(gameObject);
    }
}
