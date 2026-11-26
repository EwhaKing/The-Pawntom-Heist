using Fusion;
using Pawntom.Stage;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 캣닢 경보(씬에 1개). GAME_DESIGN §3.7 "전설의 캣닢 획득 즉시 경보 → 브루투스 스폰".
/// <para>
/// 상태 권한(호스트)이 매 틱 팀 인벤토리를 조회해(<see cref="InventoryManager.HasAnyPlayerItem"/>) 캣닢이 처음 들어온 순간
/// [Networked] <see cref="IsTriggered"/>=true, <see cref="TriggerTick"/> 를 기록한다. 규칙은 <see cref="CatnipAlarmRule"/>
/// (첫 획득에만, 인계·재획득은 무시).
/// </para>
/// <para>
/// 부작용은 한 번씩만: <see cref="Triggered"/> 는 모든 피어에서 상태 변화를 처음 본 순간(Render) 1회,
/// <see cref="BrutusSpawnRequested"/> 는 호스트에서만 1회. 늦게 들어온 참가자는 이벤트 없이 <see cref="IsTriggered"/> 로 안다
/// (경보음 루프는 상태로 켠다). 실제 브루투스 스폰은 SpawnManager 가 <see cref="BrutusSpawnRequested"/> 를 구독해 한다.
/// </para>
/// 필요 컴포넌트: NetworkObject(씬 오브젝트).
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Pawntom/Gameplay/Catnip Alarm")]
public sealed class CatnipAlarm : NetworkBehaviour
{
    [Header("캣닢")]
    [Tooltip("캣닢 보상을 내는 금고. 지정하면 그 금고의 보상 아이템 ID 를 쓴다")]
    [SerializeField] private VaultChest _vault;

    [Tooltip("금고를 지정하지 않았을 때 쓰는 캣닢 아이템 ID(ItemData_Catleaf, 기본 1)")]
    [SerializeField] private int _catnipItemId = 1;

    [Header("경보음 (선택)")]
    [Tooltip("경보 동안 반복 재생할 소리. 비워 두면 소리 없이 이벤트만 보낸다")]
    [SerializeField] private AudioSource _alarmSource;

    [Header("이벤트 (씬에서 연결, 이 피어에서 1회)")]
    [SerializeField] private UnityEvent _onTriggered = new UnityEvent();

    [Networked] public NetworkBool IsTriggered { get; private set; }

    /// <summary>경보가 발동한 틱. 발동 전에는 0.</summary>
    [Networked] public int TriggerTick { get; private set; }

    /// <summary>모든 피어: 경보 발동을 이 피어에서 처음 본 순간 1회(늦은 참가자 제외 — 상태로 안다).</summary>
    public static event System.Action<CatnipAlarm> Triggered;

    /// <summary>호스트(상태 권한)에서만 1회: 브루투스를 스폰해야 한다. SpawnManager 가 구독한다.</summary>
    public static event System.Action<CatnipAlarm> BrutusSpawnRequested;

    /// <summary>씬의 경보(1개). 스폰 전·씬 밖이면 null.</summary>
    public static CatnipAlarm Current { get; private set; }

    /// <summary>지금 경보가 울린 상태인가(늦은 참가자 포함).</summary>
    public static bool IsActive
    {
        get { return Current != null && Current.Object != null && Current.Object.IsValid && Current.IsTriggered; }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Triggered = null;
        BrutusSpawnRequested = null;
        Current = null;
    }

    private readonly CatnipAlarmRule _rule = new CatnipAlarmRule();
    private bool _notified;

    public int CatnipItemId
    {
        get { return _vault != null ? _vault.RewardItemId : _catnipItemId; }
    }

    public override void Spawned()
    {
        if (Current != null && Current != this)
        {
            Debug.LogWarning("[CatnipAlarm] 씬에 경보가 둘 이상입니다. 마지막으로 스폰된 것을 씁니다.", this);
        }

        Current = this;
        _rule.Restore(IsTriggered);

        // 늦은 참가자: 이미 울린 경보는 이벤트 없이 상태만 맞춘다.
        _notified = IsTriggered;

        if (IsTriggered)
        {
            StartAlarmSound();
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (Current == this)
        {
            Current = null;
        }

        if (_alarmSource != null)
        {
            _alarmSource.Stop();
        }
    }

    public override void FixedUpdateNetwork()
    {
        // 판정·상태 쓰기는 상태 권한에서만, 재시뮬레이션이 아닌 틱에서만.
        if (!Object.HasStateAuthority || !Runner.IsForward || IsTriggered)
        {
            return;
        }

        if (!_rule.Observe(InventoryManager.HasAnyPlayerItem(CatnipItemId)))
        {
            return;
        }

        IsTriggered = true;
        TriggerTick = Runner.Tick.Raw;
        Debug.Log($"[CatnipAlarm] 캣닢 획득 — 경보 발동 (tick {TriggerTick})", this);
        BrutusSpawnRequested?.Invoke(this);
    }

    public override void Render()
    {
        // 변경 감지: 발동 상태를 처음 본 프레임에 한 번만.
        if (!IsTriggered || _notified)
        {
            return;
        }

        _notified = true;
        StartAlarmSound();
        Triggered?.Invoke(this);
        _onTriggered.Invoke();
    }

    private void StartAlarmSound()
    {
        if (_alarmSource == null || _alarmSource.isPlaying)
        {
            return;
        }

        _alarmSource.loop = true;
        _alarmSource.Play();
    }
}
