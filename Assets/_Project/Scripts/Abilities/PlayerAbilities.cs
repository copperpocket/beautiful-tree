using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Owns the player's action bar slots, cooldowns, the global cooldown, and
/// casting. Attach to the Player.
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

    // ---- Events for UI ----
    public event Action<Ability, float> OnCastStarted;    // ability, duration
    public event Action<Ability> OnCastCompleted;
    public event Action<string> OnCastCancelled;          // reason
    public event Action<string> OnAbilityBlocked;         // reason, for error text
    public event Action OnSlotsChanged;

    public bool IsCasting { get; private set; }
    public Ability CastingAbility { get; private set; }
    public float CastStartTime { get; private set; }
    public float CastEndTime { get; private set; }

    public float CastProgress => IsCasting && CastEndTime > CastStartTime
        ? Mathf.Clamp01((Time.time - CastStartTime) / (CastEndTime - CastStartTime))
        : 0f;

    private PlayerStats stats;
    private PlayerTargeting targeting;
    private PlayerCombat combat;
    private CharacterController controller;

    // Cooldowns keyed by the asset, so the same ability in two slots shares one timer.
    private readonly Dictionary<Ability, float> cooldownEnd = new();
    private float gcdEnd;

    void Awake()
    {
        stats = GetComponent<PlayerStats>();
        targeting = GetComponent<PlayerTargeting>();
        combat = GetComponent<PlayerCombat>();
        controller = GetComponent<CharacterController>();

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
    }

    void OnDisable()
    {
        targeting.OnAttackRequested -= HandleRightClickAttack;
    }

    void Update()
    {
        if (stats.IsDead)
        {
            if (IsCasting) CancelCast("You are dead");
            return;
        }

        TickCast();
        ReadInput();
    }

    // ---- Input ----

    private void ReadInput()
    {
        if (Keyboard.current == null) return;

        int count = Mathf.Min(slotKeys.Length, SlotCount);
        for (int i = 0; i < count; i++)
        {
            if (Keyboard.current[slotKeys[i]].wasPressedThisFrame)
                TryUseSlot(i);
        }
    }

    private void HandleRightClickAttack(Health target)
    {
        // Right-click engages auto-attack directly, bypassing the bar,
        // so it works even if the player has not slotted it.
        if (combat != null) combat.EngageAutoAttack();
    }

    // ---- Use ----

    public bool TryUseSlot(int slot)
    {
        if (slot < 0 || slot >= SlotCount) return false;

        Ability ability = slots[slot];
        if (ability == null) return false;

        return TryUse(ability);
    }

    public bool TryUse(Ability ability)
    {
        if (ability == null) return false;

        var ctx = BuildContext();

        // Global cooldown.
        if (ability.triggersGCD && Time.time < gcdEnd)
            return false;                      // silent, spamming during GCD is normal

        // Already casting.
        if (IsCasting)
        {
            Block("Already casting");
            return false;
        }

        // Ability cooldown.
        if (GetCooldownRemaining(ability) > 0f)
            return false;                      // silent

        // Ability-specific validation.
        string reason = ability.GetBlockReason(ctx);
        if (reason != null)
        {
            Block(reason);
            return false;
        }

        // Instant abilities fire now. Cast-time abilities start a cast.
        if (ability.castTime <= 0f)
            Commit(ability, ctx);
        else
            StartCast(ability);

        return true;
    }

    private void StartCast(Ability ability)
    {
        IsCasting = true;
        CastingAbility = ability;
        CastStartTime = Time.time;
        CastEndTime = Time.time + ability.castTime;

        // GCD applies at the start of the cast, as in WoW.
        if (ability.triggersGCD)
            gcdEnd = Time.time + globalCooldown;

        OnCastStarted?.Invoke(ability, ability.castTime);
    }

    private void TickCast()
    {
        if (!IsCasting) return;

        var ctx = BuildContext();

        // Target died or wandered out of range mid-cast.
        string reason = CastingAbility.GetBlockReason(ctx);
        if (reason != null && reason != $"Not enough {stats.powerType}")
        {
            CancelCast(reason);
            return;
        }

        if (movementCancelsCast && !CastingAbility.usableWhileMoving && IsMoving())
        {
            CancelCast("Interrupted by movement");
            return;
        }

        if (Time.time < CastEndTime) return;

        Ability finished = CastingAbility;
        ClearCast();
        Commit(finished, BuildContext());
        OnCastCompleted?.Invoke(finished);
    }

    /// <summary>Spends power, starts the cooldown, and runs the effect.</summary>
    private void Commit(Ability ability, AbilityContext ctx)
    {
        if (ability.powerCost > 0f && !stats.TrySpendPower(ability.powerCost))
        {
            Block($"Not enough {stats.powerType}");
            return;
        }

        if (ability.cooldown > 0f)
            cooldownEnd[ability] = Time.time + ability.cooldown;

        // Instants take the GCD here; casts already took it at cast start.
        if (ability.triggersGCD && ability.castTime <= 0f)
            gcdEnd = Time.time + globalCooldown;

        ability.Execute(ctx);
    }

    public void CancelCast(string reason)
    {
        if (!IsCasting) return;

        ClearCast();
        OnCastCancelled?.Invoke(reason);
        if (logBlockReasons) Debug.Log($"Cast cancelled: {reason}");
    }

    private void ClearCast()
    {
        IsCasting = false;
        CastingAbility = null;
        CastStartTime = 0f;
        CastEndTime = 0f;
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

    // ---- Queries for UI ----

    public float GetCooldownRemaining(Ability ability)
    {
        if (ability == null) return 0f;
        return cooldownEnd.TryGetValue(ability, out float end)
            ? Mathf.Max(0f, end - Time.time)
            : 0f;
    }

    public float GCDRemaining => Mathf.Max(0f, gcdEnd - Time.time);

    /// <summary>0 = ready, 1 = just started. Includes the GCD sweep.</summary>
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

    /// <summary>True when the slot could be used right now, for the UI tint.</summary>
    public bool IsSlotUsable(int slot)
    {
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
