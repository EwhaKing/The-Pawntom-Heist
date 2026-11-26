using Fusion;
using Pawntom.Network;
using UnityEngine;

/// <summary>
/// RuleManager
///
/// 담당:
/// - 탈출 관련 게임 규칙 관리
/// - 조건 만족시 GameManager에 레벨 클리어 요청
///
/// 특징:
/// - Map Scene 안의 빈 오브젝트에 NetworkObject와 같이 붙임
/// 클리어 조건:
/// 1. 본부 공간 안에 살아있는 플레이어가 최소 1명 이상 있음
/// 2. 팀원 중 최소 1명 이상이 전설의 캣닢을 소지 중
/// 3. 출발 버튼 상호작용 성공
///
/// 네트워크:
/// - 판정은 상태 권한(호스트)에서만(<see cref="DepartureRule"/>). 요청자(RpcInfo.Source)의 생존·쿨다운도 다시 본다.
/// - 클리어 결과는 [Networked] <see cref="IsLevelCleared"/> 하나로 알린다. 모든 피어(늦은 참가자 포함)가 Render 에서 값을 보고
///   <see cref="GameManager.EscapeSuccess"/> 를 정확히 1회 부른다(<see cref="OneShotLatch"/>). RPC 로만 알리면 늦은 참가자가 받지 못한다.
/// - 출발 거부 사유는 요청자에게만 RPC 로 통지하고, 그 수신 쪽에서 <see cref="DepartureRejected"/>/<see cref="DepartureRejectedNoExit"/> 가 발생한다.
/// </summary>
public class RuleManager : NetworkBehaviour
{
    /// <summary>
    /// 씬에서 접근하기 위한 간단한 싱글톤 참조
    /// 단, 네트워크 상태 자체는 [Networked] 값으로 동기화됨
    /// </summary>
    public static RuleManager Instance { get; private set; }

    /// <summary>
    /// 내가 누른 출발이 캣닢이 없어 거부됐을 때(출발 버튼을 누른 피어에서만). 튜토리얼 안내 등 외부 관찰용.
    /// </summary>
    public static event System.Action DepartureRejectedNoExit;

    /// <summary>내가 누른 출발이 거부됐을 때(출발 버튼을 누른 피어에서만). 모든 거부 사유.</summary>
    public static event System.Action<DepartureVerdict> DepartureRejected;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        DepartureRejectedNoExit = null;
        DepartureRejected = null;
    }

    [Header("Required")]
    [SerializeField] private MainbaseZone headquartersZone;

    [Header("Item")]
    [SerializeField] private int exitItemId = 1;

    [Header("Request")]
    [Tooltip("같은 플레이어의 출발 요청 사이 최소 간격(초). 연타로 판정·통지가 쌓이지 않게")]
    [SerializeField, Min(0f)] private float departureCooldownSeconds = 0.5f;

    /// <summary>
    /// 출발이 시작되었는지 여부
    ///
    /// true가 되면 모든 클라이언트에서 출발 연출, UI 변경 등을 할 수 있음
    /// </summary>
    [Networked] public NetworkBool IsDepartureStarted { get; private set; }

    /// <summary>
    /// 레벨 클리어가 확정되었는지 여부
    ///
    /// true가 되면 모든 클라이언트가 같은 결과 상태를 바라보게 됨
    /// </summary>
    [Networked] public NetworkBool IsLevelCleared { get; private set; }

    private readonly OneShotLatch _clearedLatch = new OneShotLatch();
    private RequestCooldown _cooldown;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// Fusion에서 NetworkObject가 Spawn된 뒤 호출
    /// </summary>
    public override void Spawned()
    {
        _clearedLatch.Reset();
        _cooldown = new RequestCooldown(HostRequesters.SecondsToTicks(Runner, departureCooldownSeconds));

        if (!Object.HasStateAuthority)
        {
            return;
        }

        IsDepartureStarted = false;
        IsLevelCleared = false;
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        _cooldown?.Clear();
    }

    /// <summary>
    /// 모든 피어: 클리어 상태가 이 피어에 처음 보이는 순간 1회 결과 처리(늦은 참가자는 첫 Render 에서).
    /// </summary>
    public override void Render()
    {
        if (!_clearedLatch.Observe(IsLevelCleared))
        {
            return;
        }

        Debug.Log("[RuleManager] 레벨 클리어 반영 — 결과 처리");

        if (GameManager.Instance != null)
        {
            GameManager.Instance.EscapeSuccess();
        }
        else
        {
            Debug.LogError("[RuleManager] GameManager.Instance가 없습니다.");
        }
    }

    /// <summary>
    /// 출발 버튼을 눌렀을 때 호출
    /// 조건 만족 시 팀 전체 레벨 클리어를 시도. 결과는 <see cref="IsLevelCleared"/> 또는 거부 이벤트로 온다.
    /// </summary>
    public void RequestDeparture()
    {
        Debug.Log("[RuleManager] 출발 요청");

        if (Object == null || !Object.IsValid)
        {
            Debug.LogWarning("[RuleManager] 네트워크에 스폰되지 않아 출발 요청을 보낼 수 없습니다.");
            return;
        }

        // 실제 판정은 StateAuthority에서만 실행
        if (Object.HasStateAuthority)
        {
            TryStartDeparture(Runner.LocalPlayer);
        }
        else
        {
            RPC_RequestDeparture();
        }
    }

    /// <summary>
    /// 클라이언트가 StateAuthority에게 출발 판정을 요청
    /// </summary>
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestDeparture(RpcInfo info = default)
    {
        TryStartDeparture(info.Source);
    }

    /// <summary>
    /// 실제 출발 조건을 검사(상태 권한)
    /// 성공 시 팀 전체 레벨 클리어로 처리
    /// </summary>
    private void TryStartDeparture(PlayerRef requester)
    {
        if (!Object.HasStateAuthority)
        {
            return;
        }

        bool configValid = true;

        if (exitItemId <= InventoryManager.EmptySlot)
        {
            Debug.LogError("[RuleManager] Exit Item Id가 잘못 설정되었습니다. 0은 빈 슬롯입니다.");
            configValid = false;
        }

        if (headquartersZone == null)
        {
            Debug.LogError("[RuleManager] HeadquartersZone이 연결되지 않았습니다.");
            configValid = false;
        }

        int tick = Runner.Tick.Raw;
        bool requesterValid = HostRequesters.TryResolvePlayer(requester, out PlayerData player);
        bool alreadyCleared = IsLevelCleared;
        bool evaluateConditions = configValid && !alreadyCleared;

        var facts = new DepartureFacts
        {
            AlreadyCleared = alreadyCleared,
            ConfigValid = configValid,
            RequesterValid = requesterValid,
            RequesterDead = requesterValid && player.IsDead,
            CoolingDown = _cooldown != null && _cooldown.IsCoolingDown(HostRequesters.Key(requester), tick),
            AnyAlivePlayerInHeadquarters = evaluateConditions && headquartersZone.HasAnyAlivePlayerInZone(),
            HasExitItem = evaluateConditions && InventoryManager.HasAnyPlayerItem(exitItemId),
        };

        DepartureVerdict verdict = DepartureRule.Evaluate(facts);

        if (verdict == DepartureVerdict.AlreadyCleared)
        {
            // 중복 요청: 결과는 이미 상태로 퍼져 있다. 통지하지 않는다.
            Debug.Log("[RuleManager] 이미 레벨 클리어 처리됨");
            return;
        }

        if (verdict != DepartureVerdict.CoolingDown)
        {
            _cooldown?.Record(HostRequesters.Key(requester), tick);
        }

        if (verdict != DepartureVerdict.Accepted)
        {
            Debug.Log($"[RuleManager] 출발 거부: {verdict} (본부 안 생존자 {facts.AnyAlivePlayerInHeadquarters}, 캣닢 {facts.HasExitItem})");
            NotifyRejected(requester, verdict);
            return;
        }

        IsDepartureStarted = true;
        IsLevelCleared = true;

        Debug.Log("[RuleManager] 출발 성공. 팀 전체 레벨 클리어!");
        // 결과 처리(EscapeSuccess)는 Render 의 변경 감지가 모든 피어에서 1회 한다.
    }

    private void NotifyRejected(PlayerRef requester, DepartureVerdict verdict)
    {
        if (requester == Runner.LocalPlayer)
        {
            RaiseRejected(verdict);
        }
        else if (requester != PlayerRef.None)
        {
            RPC_NotifyDepartureRejected(requester, (int)verdict);
        }
    }

    /// <summary>요청자에게만: 출발 거부 사유.</summary>
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_NotifyDepartureRejected([RpcTarget] PlayerRef target, int verdict)
    {
        RaiseRejected((DepartureVerdict)verdict);
    }

    private static void RaiseRejected(DepartureVerdict verdict)
    {
        Debug.Log($"[RuleManager] 내 출발 요청 거부됨: {verdict}");

        DepartureRejected?.Invoke(verdict);

        if (verdict == DepartureVerdict.NoExitItem)
        {
            DepartureRejectedNoExit?.Invoke();
        }
    }

    /// <summary>
    /// 새 라운드 시작 시 탈출 규칙 초기화
    /// </summary>
    public void ResetEscapeRule()
    {
        if (!Object.HasStateAuthority)
        {
            return;
        }

        IsDepartureStarted = false;
        IsLevelCleared = false;
        _cooldown?.Clear();

        Debug.Log("[RuleManager] 탈출 규칙 초기화");
    }
}
