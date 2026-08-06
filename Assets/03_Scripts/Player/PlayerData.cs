using System;
using Fusion;
using UnityEngine;

/// <summary>
/// 플레이어 한 명의 네트워크 상태를 보관합니다.
///
/// 담당:
/// - 선택한 고양이 종류
/// - 체력과 스태미나
/// - 현재 행동 상태
/// </summary>
public class PlayerData : NetworkBehaviour
{
    private const float DefaultMaxHealth = 100f;
    private const float DefaultMaxStamina = 100f;
    private const float DefaultStaminaRegenRate = 20f;
    private const float DefaultSprintStaminaCost = 10f;
    private const float DefaultJumpStaminaCost = 15f;
    private const float DefaultParkourStaminaCost = 35f;
    private const float DefaultExhaustedDuration = 3f;

    [Header("Cat Data")]
    [SerializeField] private CatData[] catDataList;

    [Networked] public CatType SelectedCatType { get; set; }

    [Networked] public NetworkBool IsSprinting { get; set; }
    [Networked] public NetworkBool IsParkouring { get; set; }
    [Networked] public NetworkBool IsExhausted { get; private set; }
    [Networked] public NetworkBool IsDead { get; set; }
    [Networked] private TickTimer ExhaustedTimer { get; set; }

    [Networked, OnChangedRender(nameof(OnHealthChanged))]
    public float CurrentHealth { get; private set; }

    [Networked, OnChangedRender(nameof(OnStaminaChanged))]
    public float CurrentStamina { get; private set; }

    [Networked] public float MaxHealth { get; private set; }
    [Networked] public float MaxStamina { get; private set; }
    [Networked] public float StaminaRegenRate { get; private set; }
    [Networked] public float SprintStaminaCost { get; private set; }
    [Networked] public float JumpStaminaCost { get; private set; }
    [Networked] public float ParkourStaminaCost { get; private set; }
    [Networked] public float ExhaustedDuration { get; private set; }

    public float HealthRatio => MaxHealth > 0f ? CurrentHealth / MaxHealth : 0f;
    public float StaminaRatio => MaxStamina > 0f ? CurrentStamina / MaxStamina : 0f;

    public event Action<float, float> HealthChanged;
    public event Action<float, float> StaminaChanged;

    public override void Spawned()
    {
        if (!Object.HasStateAuthority)
        {
            return;
        }

        InitializeStats();
        IsSprinting = false;
        IsParkouring = false;
        IsExhausted = false;
        ExhaustedTimer = TickTimer.None;
        IsDead = false;
    }

    public override void FixedUpdateNetwork()
    {
        if (Object.HasStateAuthority &&
            IsExhausted &&
            ExhaustedTimer.Expired(Runner))
        {
            IsExhausted = false;
            ExhaustedTimer = TickTimer.None;
        }
    }

    public void ApplyDamage(float amount)
    {
        if (!Object.HasStateAuthority || IsDead || amount <= 0f)
        {
            return;
        }

        CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);

        if (CurrentHealth <= 0f)
        {
            IsDead = true;
            IsSprinting = false;
            IsParkouring = false;
            IsExhausted = false;
            ExhaustedTimer = TickTimer.None;
        }
    }

    public void RestoreHealth(float amount)
    {
        if (!Object.HasStateAuthority || IsDead || amount <= 0f)
        {
            return;
        }

        CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + amount);
    }

    public bool HasStamina(float amount)
    {
        return amount >= 0f && CurrentStamina >= amount;
    }

    public bool TryConsumeStamina(float amount)
    {
        if (!Object.HasStateAuthority ||
            IsDead ||
            IsExhausted ||
            amount <= 0f ||
            CurrentStamina <= 0f)
        {
            return false;
        }

        CurrentStamina = Mathf.Max(0f, CurrentStamina - amount);

        if (CurrentStamina <= 0f)
        {
            StartExhaustion();
        }

        return true;
    }

    public void RestoreStamina(float amount)
    {
        if (!Object.HasStateAuthority || IsDead || amount <= 0f)
        {
            return;
        }

        CurrentStamina = Mathf.Min(MaxStamina, CurrentStamina + amount);
    }

    public bool TryStartParkour()
    {
        if (!Object.HasStateAuthority ||
            IsDead ||
            IsExhausted ||
            CurrentStamina <= 0f)
        {
            return false;
        }

        IsParkouring = true;
        IsSprinting = false;
        return true;
    }

    public void StopParkour()
    {
        if (Object.HasStateAuthority)
        {
            IsParkouring = false;
        }
    }

    private void InitializeStats()
    {
        CatData selectedCatData = FindSelectedCatData();

        MaxHealth = selectedCatData != null
            ? selectedCatData.MaxHealth
            : DefaultMaxHealth;
        MaxStamina = selectedCatData != null
            ? selectedCatData.MaxStamina
            : DefaultMaxStamina;
        StaminaRegenRate = selectedCatData != null
            ? selectedCatData.StaminaRegenRate
            : DefaultStaminaRegenRate;
        SprintStaminaCost = selectedCatData != null
            ? selectedCatData.SprintStaminaCost
            : DefaultSprintStaminaCost;
        JumpStaminaCost = selectedCatData != null
            ? selectedCatData.JumpStaminaCost
            : DefaultJumpStaminaCost;
        ParkourStaminaCost = selectedCatData != null
            ? selectedCatData.ParkourStaminaCost
            : DefaultParkourStaminaCost;
        ExhaustedDuration = selectedCatData != null
            ? selectedCatData.ExhaustedDuration
            : DefaultExhaustedDuration;

        CurrentHealth = MaxHealth;
        CurrentStamina = MaxStamina;

        if (selectedCatData == null)
        {
            Debug.LogWarning(
                $"[PlayerData] {SelectedCatType}에 해당하는 CatData가 없어 기본 능력치를 사용합니다."
            );
        }
    }

    private CatData FindSelectedCatData()
    {
        if (catDataList == null)
        {
            return null;
        }

        foreach (CatData catData in catDataList)
        {
            if (catData != null && catData.CatType == SelectedCatType)
            {
                return catData;
            }
        }

        return null;
    }

    private void StartExhaustion()
    {
        IsExhausted = true;
        IsSprinting = false;
        IsParkouring = false;
        ExhaustedTimer = TickTimer.CreateFromSeconds(
            Runner,
            ExhaustedDuration
        );
    }

    private void OnHealthChanged()
    {
        HealthChanged?.Invoke(CurrentHealth, MaxHealth);
    }

    private void OnStaminaChanged()
    {
        StaminaChanged?.Invoke(CurrentStamina, MaxStamina);
    }
}
