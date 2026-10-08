using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Plays visual effects for the player's cast-time abilities:
/// a looping effect on the hands while casting, and an effect on the
/// caster when the cast completes. Cancelled casts just clear the loop.
/// Attach to the Player.
/// </summary>
[RequireComponent(typeof(PlayerAbilities))]
public class AbilityVFX : MonoBehaviour
{
    [Serializable]
    public class Entry
    {
        public Ability ability;

        [Tooltip("Plays on the hands while casting. Removed when the cast ends.")]
        public GameObject castLoop;
        public bool bothHands = true;

        [Tooltip("Plays on the caster when the cast completes, e.g. a heal burst.")]
        public GameObject completeEffect;
        [Tooltip("Offset from the player's feet.")]
        public Vector3 completeOffset = Vector3.zero;
    }

    public List<Entry> entries = new();

    private PlayerAbilities abilities;
    private Animator animator;
    private readonly List<GameObject> activeLoops = new();

    void Awake()
    {
        abilities = GetComponent<PlayerAbilities>();

        var bridge = GetComponent<AnimationBridge>();
        animator = bridge != null && bridge.animator != null
            ? bridge.animator
            : GetComponentInChildren<Animator>();
    }

    void OnEnable()
    {
        abilities.OnCastStarted += HandleCastStarted;
        abilities.OnCastCompleted += HandleCastCompleted;
        abilities.OnCastCancelled += HandleCastCancelled;
    }

    void OnDisable()
    {
        abilities.OnCastStarted -= HandleCastStarted;
        abilities.OnCastCompleted -= HandleCastCompleted;
        abilities.OnCastCancelled -= HandleCastCancelled;
        ClearLoops();
    }

    private Entry Find(Ability ability) =>
        ability == null ? null : entries.Find(e => e.ability == ability);

    private void HandleCastStarted(Ability ability, float duration)
    {
        ClearLoops();

        var entry = Find(ability);
        if (entry == null || entry.castLoop == null)
            return;

        SpawnOnHand(entry.castLoop, HumanBodyBones.RightHand);
        if (entry.bothHands)
            SpawnOnHand(entry.castLoop, HumanBodyBones.LeftHand);
    }

    private void HandleCastCompleted(Ability ability)
    {
        ClearLoops();

        var entry = Find(ability);
        if (entry == null || entry.completeEffect == null)
            return;

        var fx = Instantiate(entry.completeEffect, transform);
        fx.transform.localPosition = entry.completeOffset;
        fx.transform.localRotation = Quaternion.identity;
    }

    private void HandleCastCancelled(string reason) => ClearLoops();

    private void SpawnOnHand(GameObject prefab, HumanBodyBones bone)
    {
        Transform hand = animator != null && animator.isHuman ? animator.GetBoneTransform(bone) : null;
        Transform parent = hand != null ? hand : transform;

        var go = Instantiate(prefab, parent);
        go.transform.localPosition = hand != null ? Vector3.zero : Vector3.up * 1.3f;
        go.transform.localRotation = Quaternion.identity;

        // Keep the glow its intended size even if the bone is scaled.
        float s = parent.lossyScale.x;
        if (s > 0.0001f)
            go.transform.localScale = go.transform.localScale / s;

        activeLoops.Add(go);
    }

    private void ClearLoops()
    {
        foreach (var go in activeLoops)
            if (go != null) Destroy(go);

        activeLoops.Clear();
    }
}
