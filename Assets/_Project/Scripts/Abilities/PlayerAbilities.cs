using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Owns the player's action bar slots, cooldowns, GCD, casting, and action state machine.
/// Attach to the Player.
/// </summary>
[RequireComponent(typeof(PlayerStats))]
[RequireComponent(typeof(PlayerTargeting))]
public class PlayerAbilities : MonoBehaviour
{
    public const int SlotCount = 12;

    [Header("Slots")]
    [Tooltip("Assign abilities here. Slot 0 is the '1' key.")]
    public Ability[] slots = new Ability[SlotCount];

    [Header("Global Cooldown")]
    public float globalCooldown = 1.5f;

    [Header("Casting")]
    [Tooltip("Moving cancels casts unless the ability allows movement.")]
    public bool movementCancelsCast = true;
    [Tooltip("Movement above this speed counts as moving.")]
    public float moveCancelThreshold = 0.4f;

    [Header("Input")]
    [Tooltip("Key per slot, in order. Defaults to 1-9, 0, -, =.")]
    public Key[] slotKeys = new Key[]
    {
        Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4,
        Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8,
        Key.Digit9, Key.Digit0, Key.Minus, Key.Equals
    };

    [Header("Debug")]
    public bool logBlockReasons = true;
    public bool logStateTransitions = false;

    // ---- State Machine ----
    public PlayerActionState ActionState { get; private set; } = PlayerActionState.Available;
    public event Action<PlayerActionState, PlayerActionState> OnActionStateChanged; // (previous, next)

    // Backwards-compatible cast properties for existing UI
    public bool IsCasting => ActionState == PlayerActionState.Casting;
    public Ability CastingAbility { get; private set; }
    public float CastStartTime { get; private set; }
    public float CastEndTime { get; private set; }

    public float CastProgress => IsCasting && CastEndTime > CastStartTime
        ? Mathf.Clamp01((Time.time - CastStartTime) / (CastEndTime - CastStartTime))
        : 0f;

    /// <summary>True if the player is allowed to move right now.</summary>
    public bool CanMove => ActionState != PlayerActionState.Dead &&
                          (!IsCasting || (CastingAbility != null && CastingAbility.usableWhileMoving));

    // ---- Events for UI ----
    public event Action<Ability, float> OnCastStarted;    // ability, duration
    public event Action<Ability> OnCastCompleted;
    public event Action<string> OnCastCancelled;          // reason
    public event Action<string> OnAbilityBlocked;         // reason, for error text
    public event Action OnSlotsChanged;

    private PlayerStats stats;
    private PlayerTargeting targeting;
    private PlayerCombat combat;
    private CharacterController controller;
    private AnimationBridge animationBridge;
    private readonly Dictionary<Ability, float> cooldownEnd = new();
    private float gcdEnd;

    void Awake()
    {
        stats = GetComponent<PlayerStats>();
        targeting = GetComponent<PlayerTargeting>();
        combat = GetComponent<PlayerCombat>();
        controller = GetComponent<CharacterController>();
        animationBridge = GetComponent<AnimationBridge>();

        if (slots == null || slots.Length != SlotCount)
        {
            var resized = new Ability[SlotCount];
            if (slots != null)
                Array.Copy(slots, resized, Mathf.Min(slots.Length, SlotCount));
            slots = resized;
        }
    }

    void OnEnable()
    {
        targeting.OnAttackRequested += HandleRightClickAttack;
        if (stats != null) stats.OnDeath += HandleDeath;
    }

    void OnDisable()
    {
        targeting.OnAttackRequested -= HandleRightClickAttack;
        if (stats != null) stats.OnDeath -= HandleDeath;
    }

    void Update()
    {
        // Death check & state synchronization
        if (stats.IsDead)
        {
            if (ActionState != PlayerActionState.Dead)
                SetState(PlayerActionState.Dead);
            return;
        }
        else if (ActionState == PlayerActionState.Dead)
        {
            // Player was revived
            SetState(PlayerActionState.Available);
        }

        TickCast();
        ReadInput();
    }

    // ---- State Transition Core ----

    private void SetState(PlayerActionState newState)
    {
        if (ActionState == newState) return;

        PlayerActionState previous = ActionState;
        ActionState = newState;

        if (logStateTransitions)
            Debug.Log($"ActionState: {previous} -> {newState}");

        OnActionStateChanged?.Invoke(previous, newState);
    }

    private void HandleDeath()
    {
        if (IsCasting)
            CancelCast("You are dead");

        SetState(PlayerActionState.Dead);
    }

    // ---- Input ----

    private void ReadInput()
    {
        if (Keyboard.current == null || ActionState == PlayerActionState.Dead) return;

        int count = Mathf.Min(slotKeys.Length, SlotCount);
        for (int i = 0; i < count; i++)
        {
            if (Keyboard.current[slotKeys[i]].wasPressedThisFrame)
                TryUseSlot(i);
        }
    }

    private void HandleRightClickAttack(Health target)
    {
        if (combat != null && ActionState != PlayerActionState.Dead)
            combat.EngageAutoAttack();
    }

    // ---- Use & Casting ----

    public bool TryUseSlot(int slot)
    {
        if (slot < 0 || slot >= SlotCount) return false;
        Ability ability = slots[slot];
        return ability != null && TryUse(ability);
    }

    public bool TryUse(Ability ability)
    {
        if (ability == null || ActionState == PlayerActionState.Dead) return false;

        var ctx = BuildContext();

        // 1. Global cooldown
        if (ability.triggersGCD && Time.time < gcdEnd)
            return false;

        // 2. State-based validation (already busy)
        if (ActionState == PlayerActionState.Casting)
        {
            Block("Already casting");
            return false;
        }

        // 3. Ability specific cooldown
        if (GetCooldownRemaining(ability) > 0f)
            return false;

        // 4. Ability condition validation (range, facing, resource)
        string reason = ability.GetBlockReason(ctx);
        if (reason != null)
        {
            Block(reason);
            return false;
        }

        // 5. Fire instant or begin cast
        if (ability.castTime <= 0f)
            Commit(ability, ctx);
        else
            StartCast(ability);

        return true;
    }

    private void StartCast(Ability ability)
    {
        SetState(PlayerActionState.Casting);

        CastingAbility = ability;
        CastStartTime = Time.time;
        CastEndTime = Time.time + ability.castTime;

        // GCD applies at the start of the cast
        if (ability.triggersGCD)
            gcdEnd = Time.time + globalCooldown;

        OnCastStarted?.Invoke(ability, ability.castTime);
    }

    private void TickCast()
    {
        if (ActionState != PlayerActionState.Casting) return;

        var ctx = BuildContext();

        // Target died or wandered out of range mid-cast
        string reason = CastingAbility.GetBlockReason(ctx);
        if (reason != null && reason != $"Not enough {stats.powerType}")
        {
            CancelCast(reason);
            return;
        }

        // Movement interrupt check
        if (movementCancelsCast && !CastingAbility.usableWhileMoving && IsMoving())
        {
            CancelCast("Interrupted by movement");
            return;
        }

        if (Time.time < CastEndTime) return;

        // Cast complete
        Ability finished = CastingAbility;
        ClearCast();
        Commit(finished, BuildContext());
        OnCastCompleted?.Invoke(finished);
    }

    private void Commit(Ability ability, AbilityContext ctx)
    {
        if (ability.powerCost > 0f && !stats.TrySpendPower(ability.powerCost))
        {
            Block($"Not enough {stats.powerType}");
            return;
        }

        if (ability.cooldown > 0f)
            cooldownEnd[ability] = Time.time + ability.cooldown;

        if (ability.triggersGCD && ability.castTime <= 0f)
            gcdEnd = Time.time + globalCooldown;
            
        if (ability.castTime <= 0f)
            animationBridge?.NotifyAbility(ability);

        ability.Execute(ctx);

        // If this ability doesn't transition to a channel/performance state, remain Available
        if (ActionState != PlayerActionState.Dead && ActionState != PlayerActionState.Casting)
            SetState(PlayerActionState.Available);
    }

    public void CancelCast(string reason)
    {
        if (ActionState != PlayerActionState.Casting) return;

        ClearCast();
        OnCastCancelled?.Invoke(reason);
        if (logBlockReasons) Debug.Log($"Cast cancelled: {reason}");
    }

    private void ClearCast()
    {
        CastingAbility = null;
        CastStartTime = 0f;
        CastEndTime = 0f;

        if (ActionState != PlayerActionState.Dead)
            SetState(PlayerActionState.Available);
    }

    private bool IsMoving()
    {
        if (controller == null) return false;
        Vector3 v = controller.velocity; v.y = 0f;
        return v.magnitude > moveCancelThreshold;
    }

    private AbilityContext BuildContext() => new AbilityContext
    {
        caster = gameObject,
        stats = stats,
        abilities = this,
        combat = combat,
        target = targeting.CurrentTarget
    };

    private void Block(string reason)
    {
        OnAbilityBlocked?.Invoke(reason);
        if (logBlockReasons) Debug.Log($"Cannot use: {reason}");
    }

    // ---- Queries for UI & Other Systems ----

    public float GetCooldownRemaining(Ability ability)
    {
        if (ability == null) return 0f;
        return cooldownEnd.TryGetValue(ability, out float end)
            ? Mathf.Max(0f, end - Time.time)
            : 0f;
    }

    public float GCDRemaining => Mathf.Max(0f, gcdEnd - Time.time);

    public float GetSweepFraction(int slot)
    {
        Ability a = slots[slot];
        if (a == null) return 0f;

        float abilityFrac = a.cooldown > 0f
            ? GetCooldownRemaining(a) / a.cooldown
            : 0f;

        float gcdFrac = a.triggersGCD && globalCooldown > 0f
            ? GCDRemaining / globalCooldown
            : 0f;

        return Mathf.Clamp01(Mathf.Max(abilityFrac, gcdFrac));
    }

    public bool IsSlotUsable(int slot)
    {
        if (ActionState == PlayerActionState.Dead) return false;

        Ability a = slots[slot];
        if (a == null) return false;
        if (GetCooldownRemaining(a) > 0f) return false;
        if (!stats.HasPower(a.powerCost)) return false;
        return a.GetBlockReason(BuildContext()) == null;
    }

    public void SetSlot(int slot, Ability ability)
    {
        if (slot < 0 || slot >= SlotCount) return;
        slots[slot] = ability;
        OnSlotsChanged?.Invoke();
    }
}
