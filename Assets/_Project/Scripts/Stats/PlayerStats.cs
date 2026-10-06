using System;
using UnityEngine;

public class PlayerStats : MonoBehaviour, IDamageable
{
    [Header("Identity")]
    public string characterName = "Player";

    [Header("Health")]
    public float maxHealth = 100f;

    [Header("Power (class resource)")]
    public PowerType powerType = PowerType.Mana;
    public float maxPower = 100f;

    [Tooltip("Mana/Focus: regen per second after the delay below.")]
    public float manaRegenPerSecond = 4f;
    [Tooltip("Mana/Focus: seconds after spending before regen resumes.")]
    public float manaRegenDelay = 3f;

    [Tooltip("Energy: constant regen per second, no delay.")]
    public float energyRegenPerSecond = 10f;

    [Tooltip("Rage: generated per auto-attack swing.")]
    public float ragePerSwing = 12f;
    [Tooltip("Rage: decay per second once out of combat.")]
    public float rageDecayPerSecond = 3f;
    [Tooltip("Rage: seconds of no generation before decay starts.")]
    public float rageDecayDelay = 5f;

    [Header("Stamina (movement only)")]
    public float maxStamina = 100f;
    public float staminaRegenPerSecond = 18f;
    public float staminaRegenDelay = 1f;
    public float sprintResumeThreshold = 15f;

    [Header("Level and XP")]
    public int level = 1;
    public int maxLevel = 60;
    public int baseXPToLevel = 100;
    public float xpCurveExponent = 1.5f;

    [Header("Per-Level Gains")]
    public float healthPerLevel = 10f;
    public float powerPerLevel = 5f;

    [Header("Aim")]
    public Transform aimPoint;
    public float aimHeight = 1f;

    [Header("Combat Text")]
    [Tooltip("Show floating numbers when the player takes damage, heals, or gains power.")]
    public bool showCombatText = true;
    [Tooltip("Show a floating +XP number on every XP gain.")]
    public bool showXPText = true;
    [Tooltip("Extra height for XP and power text, so it doesn't overlap damage numbers.")]
    public float xpTextHeight = 0.6f;

    [Header("Debug")]
    public bool godMode = false;
    public bool logDamage = true;

    // Normalized 0..1 so UI never needs the max values.
    public event Action<float> OnHealthChanged;
    public event Action<float> OnPowerChanged;
    public event Action<float> OnStaminaChanged;
    public event Action<int, int> OnXPChanged;
    public event Action<int> OnLevelUp;
    public event Action OnDeath;

    public float Health  { get; private set; }
    public float Power   { get; private set; }
    public float Stamina { get; private set; }
    public bool IsDead   { get; private set; }
    public bool StaminaExhausted { get; private set; }

    public int CurrentXP { get; private set; }
    public int XPToNextLevel { get; private set; }

    // IDamageable
    public float CurrentHealth => Health;
    public float MaxHealth => maxHealth;
    public string DisplayName => characterName;
    public int Level => level;
    public Transform AimPoint => aimPoint != null ? aimPoint : transform;
    public Vector3 AimPosition =>
        aimPoint != null ? aimPoint.position : transform.position + Vector3.up * aimHeight;

    private float lastStaminaDrain = -99f;
    private float lastPowerSpend = -99f;
    private float lastPowerGain = -99f;

    void Awake()
    {
        Health  = maxHealth;
        Stamina = maxStamina;

        // Rage starts empty and is built in combat. Others start full.
        Power = powerType == PowerType.Rage ? 0f : maxPower;

        XPToNextLevel = XPRequiredFor(level);
    }

    void Start()
    {
        OnHealthChanged?.Invoke(Health / maxHealth);
        OnPowerChanged?.Invoke(Power / maxPower);
        OnStaminaChanged?.Invoke(Stamina / maxStamina);
        OnXPChanged?.Invoke(CurrentXP, XPToNextLevel);
    }

    void Update()
    {
        if (IsDead) return;

        RegenStamina();
        RegenPower();
    }

    private void RegenStamina()
    {
        if (Stamina < maxStamina && Time.time - lastStaminaDrain >= staminaRegenDelay)
            SetStamina(Stamina + staminaRegenPerSecond * Time.deltaTime);

        if (StaminaExhausted && Stamina >= sprintResumeThreshold)
            StaminaExhausted = false;
    }

    private void RegenPower()
    {
        switch (powerType)
        {
            case PowerType.Energy:
                if (Power < maxPower)
                    SetPower(Power + energyRegenPerSecond * Time.deltaTime);
                break;

            case PowerType.Rage:
                // Decays rather than regenerates, once nothing has been generated recently.
                if (Power > 0f && Time.time - lastPowerGain >= rageDecayDelay)
                    SetPower(Power - rageDecayPerSecond * Time.deltaTime);
                break;

            default: // Mana, Focus
                if (Power < maxPower && Time.time - lastPowerSpend >= manaRegenDelay)
                    SetPower(Power + manaRegenPerSecond * Time.deltaTime);
                break;
        }
    }

    // ---- Power ----

    public bool HasPower(float amount) => Power >= amount;

    /// <summary>Spend power. Returns false and spends nothing if short.</summary>
    public bool TrySpendPower(float amount)
    {
        if (IsDead) return false;
        if (amount <= 0f) return true;
        if (Power < amount) return false;

        SetPower(Power - amount);
        lastPowerSpend = Time.time;
        return true;
    }

    /// <summary>Grant power from an action, e.g. Rage from swinging. Not used by regen.</summary>
    public void GainPower(float amount)
    {
        if (IsDead || amount <= 0f) return;

        float before = Power;
        SetPower(Power + amount);
        lastPowerGain = Time.time;

        float gained = Power - before;

        // "+12 Rage" above the player. Nothing shows when already full.
        if (showCombatText && gained >= 0.5f)
        {
            CombatTextBus.Publish(new CombatTextEvent(
                CombatTextType.PowerGain,
                gained,
                AimPosition + Vector3.up * xpTextHeight,
                gameObject,
                powerType.ToString()));
        }
    }

    /// <summary>Called by PlayerCombat on each auto-attack swing.</summary>
    public void OnAutoAttackSwing()
    {
        if (powerType == PowerType.Rage) GainPower(ragePerSwing);
    }

    // ---- Stamina ----

    public bool TryDrainStamina(float amount)
    {
        if (IsDead || StaminaExhausted || amount <= 0f) return false;

        if (Stamina < amount)
        {
            SetStamina(0f);
            StaminaExhausted = true;
            lastStaminaDrain = Time.time;
            return false;
        }

        SetStamina(Stamina - amount);
        lastStaminaDrain = Time.time;
        return true;
    }

    // ---- Health ----

    public void TakeDamage(float amount, GameObject source = null)
    {
        if (IsDead || godMode || amount <= 0f) return;

        SetHealth(Health - amount);

        // Red floating number above the player.
        if (showCombatText)
        {
            CombatTextBus.Publish(new CombatTextEvent(
                CombatTextType.IncomingDamage,
                amount,
                AimPosition,
                source));
        }

        if (logDamage)
        {
            string from = source != null ? source.name : "unknown";
            Debug.Log($"Took {amount:F1} from {from}. Health {Health:F1}/{maxHealth:F0}");
        }

        if (Health <= 0f)
        {
            IsDead = true;
            Debug.LogWarning("PLAYER DIED. Press R to respawn.", this);
            OnDeath?.Invoke();
        }
    }

    public void Heal(float amount)
    {
        if (IsDead || amount <= 0f) return;

        float before = Health;
        SetHealth(Health + amount);
        float healed = Health - before;

        // Only show what was actually restored, so no "+35" at full health.
        if (showCombatText && healed > 0f)
        {
            CombatTextBus.Publish(new CombatTextEvent(
                CombatTextType.Healing,
                healed,
                AimPosition,
                gameObject));
        }
    }

    [ContextMenu("Revive Player")]
    public void Revive()
    {
        IsDead = false;
        StaminaExhausted = false;
        SetHealth(maxHealth);
        SetStamina(maxStamina);
        SetPower(powerType == PowerType.Rage ? 0f : maxPower);
        Debug.Log("Player revived.", this);
    }

    [ContextMenu("Take 25 Damage")]
    private void DebugDamage() => TakeDamage(25f, null);

    [ContextMenu("Gain 25 XP")]
    private void DebugXP() => GainXP(25);

    [ContextMenu("Gain 12 Power")]
    private void DebugPower() => GainPower(12f);

    // ---- XP ----

    public void GainXP(int amount)
    {
        if (amount <= 0 || level >= maxLevel) return;

        CurrentXP += amount;
        Debug.Log($"Gained {amount} XP. {CurrentXP}/{XPToNextLevel}");

        // Purple "+25 XP" above the player, a little higher than damage numbers.
        if (showXPText)
        {
            CombatTextBus.Publish(new CombatTextEvent(
                CombatTextType.Experience,
                amount,
                AimPosition + Vector3.up * xpTextHeight,
                gameObject));
        }

        while (level < maxLevel && CurrentXP >= XPToNextLevel)
        {
            CurrentXP -= XPToNextLevel;
            LevelUp();
        }

        OnXPChanged?.Invoke(CurrentXP, XPToNextLevel);
    }

    private void LevelUp()
    {
        level++;
        maxHealth += healthPerLevel;
        maxPower  += powerPerLevel;
        XPToNextLevel = XPRequiredFor(level);

        SetHealth(maxHealth);
        SetStamina(maxStamina);
        if (powerType != PowerType.Rage) SetPower(maxPower);

        Debug.Log($"LEVEL UP. Now {level}. Next needs {XPToNextLevel} XP.");
        OnLevelUp?.Invoke(level);
    }

    public int XPRequiredFor(int fromLevel) =>
        Mathf.RoundToInt(baseXPToLevel * Mathf.Pow(fromLevel, xpCurveExponent));

    // ---- Setters ----

    private void SetHealth(float v)
    {
        Health = Mathf.Clamp(v, 0f, maxHealth);
        OnHealthChanged?.Invoke(Health / maxHealth);
    }

    private void SetPower(float v)
    {
        Power = Mathf.Clamp(v, 0f, maxPower);
        OnPowerChanged?.Invoke(Power / maxPower);
    }

    private void SetStamina(float v)
    {
        Stamina = Mathf.Clamp(v, 0f, maxStamina);
        OnStaminaChanged?.Invoke(Stamina / maxStamina);
    }
}
