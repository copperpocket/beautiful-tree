/// <summary>
/// High-level combat/action state for the player.
/// Serves as the single source of truth for UI, abilities, and future animations.
/// </summary>
public enum PlayerActionState
{
    /// <summary>Idle or moving, free to initiate any ability or action.</summary>
    Available,

    /// <summary>Charging a cast-time ability (e.g. Fireball). Moving interrupts unless permitted.</summary>
    Casting,

    /// <summary>Executing an instant animation, melee strike, or channel.</summary>
    Performing,

    /// <summary>Player health reached 0. All action input is rejected.</summary>
    Dead
}
