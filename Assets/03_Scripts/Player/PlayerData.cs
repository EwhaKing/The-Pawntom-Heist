using System;
using System.Collections.Generic;
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
    public static readonly List<PlayerData> ActivePlayers = new List<PlayerData>();
    public static bool IsLocalPlayerDead
    {
        get
        {
            foreach (PlayerData player in ActivePlayers)
                if (player != null && player.Object != null && player.Object.IsValid && player.Object.HasInputAuthority)
                    return player.IsDead;
            return false;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetPlayers() => ActivePlayers.Clear();

    [Header("Death Sequence")]
    [Tooltip("애니메이션 종료 이벤트가 없거나 누락됐을 때 드롭으로 넘어가는 최대 대기 시간(초)")]
    [SerializeField, Min(0.1f)] private float _deathAnimationTimeout = 1.5f;
    [Networked] public NetworkBool DeathSequenceCompleted { get; private set; }
    [Networked] public Vector3 DeathPosition { get; private set; }
    [Networked] private TickTimer DeathTimer { get; set; }
    private bool _animationFinished;
    private TickTimer _tutorialRetryProtection;
    private InventoryManager _inventory;
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
    [Networked, OnChangedRender(nameof(OnDeathChanged))]
    public NetworkBool IsDead { get; private set; }
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

    /// <summary>각 클라이언트에서 사망 상태가 반영되면 한 번 알립니다. 연출 전용이며 게임 판정에 사용하지 않습니다.</summary>
    public event Action Died;
    private bool _deathNotified;

    public event Action<float, float> HealthChanged;
    public event Action<float, float> StaminaChanged;

    public override void Spawned()
    {
        if (!ActivePlayers.Contains(this)) ActivePlayers.Add(this);
        _inventory = GetComponent<InventoryManager>();
        _animationFinished = false;
        _deathNotified = false;
        _tutorialRetryProtection = TickTimer.None;
        if (!Object.HasStateAuthority)
        {
            OnDeathChanged();
            return;
        }

        InitializeStats();
        IsSprinting = false;
        IsParkouring = false;
        IsExhausted = false;
        ExhaustedTimer = TickTimer.None;
        IsDead = false;
        DeathSequenceCompleted = false;
        DeathTimer = TickTimer.None;
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        ActivePlayers.Remove(this);
    }

    private void OnDestroy() => ActivePlayers.Remove(this);

    /// <summary>Animation Event의 서버 측 수신점. 클라이언트가 드롭 시점을 결정하지 않습니다.</summary>
    public void NotifyDeathAnimationFinished()
    {
        if (Object != null && Object.IsValid && Object.HasStateAuthority && IsDead)
            _animationFinished = true;
    }

    public override void FixedUpdateNetwork()
    {
        if (Object.HasStateAuthority && IsDead && !DeathSequenceCompleted &&
            (_animationFinished || DeathTimer.Expired(Runner)))
        {
            // 성공한 슬롯만 지웁니다. 스폰 실패 시 남은 슬롯만 다시 시도합니다.
            _animationFinished = false;
            if (_inventory == null || _inventory.TryDropAllOnDeath(DeathPosition))
                DeathSequenceCompleted = true;
            else
                DeathTimer = TickTimer.CreateFromSeconds(Runner, 1f);
        }

        if (Object.HasStateAuthority &&
            IsExhausted &&
            ExhaustedTimer.Expired(Runner))
        {
            IsExhausted = false;
            ExhaustedTimer = TickTimer.None;
        }

        if (Object.HasStateAuthority && !IsDead)
        {
            TickEnemyContactDamage();
        }
    }

    // 적 접촉 피해(GAME_DESIGN.md 3.7 "적 AI 충돌로 HP 0 이하"). 적 두뇌는 상태 권한 쪽(호스트)에서만 돌기 때문에
    // 플레이어 상태 권한도 같은 호스트에서 판정한다. 판정 규칙은 EnemyContactDamage(순수 C#).
    // 위아래로 1m 넘게 떨어져 있으면(상자 위로 피함) 닿지 않은 것으로 본다.
    private const float EnemyContactVerticalTolerance = 1f;
    private readonly Pawntom.Enemy.Core.EnemyContactDamage _enemyContact =
        new Pawntom.Enemy.Core.EnemyContactDamage(EnemyContactVerticalTolerance);
    private readonly System.Collections.Generic.List<Pawntom.Enemy.Core.EnemyAttacker> _attackers =
        new System.Collections.Generic.List<Pawntom.Enemy.Core.EnemyAttacker>(8);

    private void TickEnemyContactDamage()
    {
        _attackers.Clear();
        System.Collections.Generic.IReadOnlyList<Pawntom.Enemy.Authoring.EnemyAgent> enemies =
            Pawntom.Enemy.Authoring.EnemyAgent.Active;

        for (int i = 0; i < enemies.Count; i++)
        {
            Pawntom.Enemy.Authoring.EnemyAgent enemy = enemies[i];

            // 쫓아와서 닿았을 때만 문다. 순찰 중 스친 접촉은 감지(경계→추격)로 이어지고, 추격이 되면 그때 문다.
            if (enemy == null || !enemy.isActiveAndEnabled || enemy.State != Pawntom.Enemy.Core.EnemyState.Chase)
            {
                continue;
            }

            Pawntom.Enemy.Core.EnemyAttackSettings attack = enemy.Settings.Attack;
            _attackers.Add(new Pawntom.Enemy.Core.EnemyAttacker(
                enemy.transform.position, attack.Range, attack.Damage, attack.CooldownSeconds));
        }

        float damage = _enemyContact.Tick(Runner.DeltaTime, transform.position, _attackers);

        if (damage > 0f)
        {
            ApplyDamage(damage);
        }
    }

    public void ApplyDamage(float amount)
    {
        if (!Object.HasStateAuthority || IsDead || amount <= 0f || float.IsNaN(amount) || float.IsInfinity(amount))
        {
            return;
        }

        if (Pawntom.TutorialBridge.TutorialDirector.Active != null &&
            (!_tutorialRetryProtection.ExpiredOrNotRunning(Runner) ||
             Pawntom.TutorialBridge.TutorialInputLock.IsLocked)) return;

        CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);

        if (CurrentHealth <= 0f)
        {
            DeathPosition = transform.position;
            DeathTimer = TickTimer.CreateFromSeconds(Runner, Mathf.Max(0.1f, _deathAnimationTimeout));
            IsDead = true;
            IsSprinting = false;
            IsParkouring = false;
            IsExhausted = false;
            ExhaustedTimer = TickTimer.None;
        }
    }

    /// <summary>혼자 진행하는 튜토리얼의 단계 재시도 전용. 일반 맵에서는 부활하지 않는다.</summary>
    public bool TryRestartTutorial(Vector3 position, Quaternion rotation)
    {
        if (Pawntom.TutorialBridge.TutorialDirector.Active == null || Object == null ||
            !Object.IsValid || !Object.HasStateAuthority || !IsDead) return false;
        NetworkCharacterController controller = GetComponent<NetworkCharacterController>();
        if (controller == null) return false;
        controller.Teleport(position, rotation);
        controller.Velocity = Vector3.zero;
        GetComponent<PlayerController>().ResetTutorialLook();
        CurrentHealth = MaxHealth;
        CurrentStamina = MaxStamina;
        IsSprinting = IsParkouring = IsExhausted = false;
        ExhaustedTimer = DeathTimer = TickTimer.None;
        DeathSequenceCompleted = false;
        _animationFinished = _deathNotified = false;
        _tutorialRetryProtection = TickTimer.CreateFromSeconds(Runner, 2f);
        IsDead = false;
        OnHealthChanged();
        OnStaminaChanged();
        return true;
    }

    public void RestoreHealth(float amount)
    {
        if (!Object.HasStateAuthority || IsDead || amount <= 0f || float.IsNaN(amount) || float.IsInfinity(amount))
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
        if (!Object.HasStateAuthority || IsDead || amount <= 0f || float.IsNaN(amount) || float.IsInfinity(amount))
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

    private void OnDeathChanged()
    {
        if (!IsDead || _deathNotified)
        {
            return;
        }

        _deathNotified = true;
        Died?.Invoke();
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
