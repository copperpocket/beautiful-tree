using System;
using UnityEngine;

/// <summary>
/// Receives "Impact" animation events and forwards them. Attach to every
/// object with an Animator that plays attack clips (player and enemy models).
/// </summary>
public class AnimationImpactRelay : MonoBehaviour
{
    public event Action OnImpact;

    private int lastImpactFrame = -1;

    /// <summary>Called by animation events whose Function is "Impact".</summary>
    public void Impact()
    {
        // Synced layers play the same clip, so the event can fire twice in one frame.
        if (Time.frameCount == lastImpactFrame)
            return;

        lastImpactFrame = Time.frameCount;
        OnImpact?.Invoke();
    }
}
