using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Hits waiting for their animation impact. Resolve one per Impact event;
/// Tick applies any that waited past their fallback time.
/// </summary>
public class DeferredHitQueue
{
    private struct PendingHit
    {
        public Action apply;
        public float deadline;
    }

    private readonly List<PendingHit> hits = new();

    public int Count => hits.Count;

    public void Add(Action apply, float fallbackDelay)
    {
        hits.Add(new PendingHit { apply = apply, deadline = Time.time + fallbackDelay });
    }

    /// <summary>Applies the oldest pending hit. Called on an Impact event.</summary>
    public void ResolveNext()
    {
        if (hits.Count == 0)
            return;

        PendingHit hit = hits[0];
        hits.RemoveAt(0);
        hit.apply?.Invoke();
    }

    /// <summary>Applies any hit whose fallback time has passed. Call every frame.</summary>
    public void Tick()
    {
        int i = 0;
        while (i < hits.Count)
        {
            if (Time.time >= hits[i].deadline)
            {
                PendingHit hit = hits[i];
                hits.RemoveAt(i);
                hit.apply?.Invoke();
            }
            else
            {
                i++;
            }
        }
    }

    public void Clear() => hits.Clear();
}
