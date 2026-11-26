using System.Collections.Generic;
using Fusion;
using Pawntom.Network;
using UnityEngine;

/// <summary>
/// SprinklerZone
///
/// 담당:
/// - 실제 맵의 스프링클러 효과 구역 관리
/// - 해킹 성공 시 스프링클러를 일정 시간 작동(GAME_DESIGN §3.4: 5초)
/// - 작동 중 구역 안의 대상에게 감속 효과 적용, 구역을 벗어난 대상에게도 일정 시간 감속 유지
/// - 작동 중 구역 안의 털공 흔적 정화(영역 내 털공 전부 파괴)
/// - 한 번 사용한 스프링클러는 재사용 불가
///
/// 네트워크:
/// - 사용됨·작동 중·작동 타이머는 [Networked] — 상태 권한(호스트)만 쓴다. 늦게 들어온 참가자도 같은 상태를 받는다.
/// - <see cref="ActivateSprinkler"/> 는 "요청"이다. 클라이언트는 RPC 로 보내고, 상태 권한이 요청자의 생존·단말기 위치·
///   구역 발견·1회용 여부·쿨다운을 다시 판정한다(<see cref="SprinklerRule"/>). 거부되면 요청자에게만 알린다.
/// - 감속·흔적 정화는 상태 권한의 FixedUpdateNetwork 에서만 한다. 물리 트리거 이벤트는 쓰지 않는다(재시뮬레이션 중복 방지) —
///   대상 목록(<see cref="SprinklerSlowTarget.All"/>, <see cref="FurBallTrace.Active"/>)의 위치를 박스 안인지로 본다.
/// - 소음 이벤트(적 어그로)는 아직 받는 시스템이 없다. 상태 권한에서 <see cref="AuthorityActivated"/> 를 1회 발행한다.
/// - 작동 연출(<see cref="activeEffect"/>)은 상태 변경 감지와 Spawned 에서 모든 피어가 반영한다.
///
/// 주의:
/// - 구역 발견 여부는 이 스크립트가 관리하지 않음 — DiscoverableArea 가 담당
///
/// 사용 위치:
/// - Sprinkler_01 > SprinklerTrigger 오브젝트에 붙임 (같은 오브젝트에 NetworkObject 필요)
///
/// 필요 컴포넌트:
/// - BoxCollider
/// - Is Trigger ON
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class SprinklerZone : NetworkBehaviour
{
    [Header("State (세션 없는 미리보기용 — 세션 중 실제 값은 상태 권한이 동기화)")]
    [SerializeField] private bool isUsed;
    [SerializeField] private bool isRunning;

    [Header("Sprinkler Option")]
    [SerializeField] private float activeDuration = 5f;
    [SerializeField] private float slowMultiplier = 0.5f;
    [SerializeField] private float afterExitSlowDuration = 3f;

    [Tooltip("작동 중 구역 안 털공 흔적을 없앤다(GAME_DESIGN §3.4 흔적 정화)")]
    [SerializeField] private bool purgeFurTraces = true;

    [Header("Rule")]
    [Tooltip("이 스프링클러가 속한 구역. 연결하면 상태 권한이 '발견된 구역인지'도 검증한다. 비우면 검사하지 않음")]
    [SerializeField] private DiscoverableArea area;

    [Header("Optional Visual")]
    [Tooltip("작동 중에만 켤 오브젝트(천장 파티클 등). 비우면 무시")]
    [SerializeField] private GameObject activeEffect;

    [Networked, OnChangedRender(nameof(OnSprinklerStateChanged))]
    private NetworkBool NetUsed { get; set; }

    [Networked, OnChangedRender(nameof(OnSprinklerStateChanged))]
    private NetworkBool NetRunning { get; set; }

    [Networked] private TickTimer RunTimer { get; set; }

    /// <summary>상태가 이 피어에 반영될 때(원격 포함).</summary>
    public event System.Action<SprinklerZone> StateChanged;

    /// <summary>내(이 피어의) 가동 요청이 상태 권한에서 거부됐다.</summary>
    public static event System.Action<SprinklerZone, RequestVerdict> LocalRequestRejected;

    /// <summary>상태 권한에서만, 가동이 확정된 순간 1회. 소음 이벤트(적 Investigate 유도) 연결 지점. 인자 = 필드 중심.</summary>
    public static event System.Action<SprinklerZone, Vector3> AuthorityActivated;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        LocalRequestRejected = null;
        AuthorityActivated = null;
        TraceBuffer.Clear();
    }

    // 디스폰이 목록을 바꾸므로 먼저 복사한다(사전 할당, 매 틱 new 없음).
    private static readonly List<FurBallTrace> TraceBuffer = new List<FurBallTrace>(64);

    private BoxCollider _zoneCollider;
    private RequestCooldown _cooldown;

    private bool IsNetworkReady => Object != null && Object.IsValid;

    public bool IsUsed => IsNetworkReady ? (bool)NetUsed : isUsed;
    public bool IsRunning => IsNetworkReady ? (bool)NetRunning : isRunning;

    private void Awake()
    {
        _zoneCollider = GetComponent<BoxCollider>();
        _zoneCollider.isTrigger = true;
        ApplyVisual();
    }

    public override void Spawned()
    {
        _cooldown = new RequestCooldown(HostRequesters.SecondsToTicks(Runner, HostRequesters.DefaultCooldownSeconds));

        if (Object.HasStateAuthority)
        {
            NetUsed = isUsed;
            NetRunning = false;
            RunTimer = TickTimer.None;
        }

        ApplyVisual();
        StateChanged?.Invoke(this);
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        _cooldown?.Clear();
    }

    // ───────── 요청 ─────────

    /// <summary>
    /// 해킹 성공 후 스프링클러 작동을 요청. 한 번 작동한 스프링클러는 다시 작동할 수 없음.
    /// 결과는 <see cref="IsUsed"/>/<see cref="IsRunning"/> 변화로 확인한다(호출 직후 가정하지 않는다).
    /// </summary>
    public void ActivateSprinkler()
    {
        if (!IsNetworkReady)
        {
            Debug.LogWarning($"[SprinklerZone] {gameObject.name} 가 네트워크에 스폰되지 않아 작동할 수 없습니다(NetworkObject 확인).", this);
            return;
        }

        if (Object.HasStateAuthority)
        {
            HandleActivate(Runner.LocalPlayer);
        }
        else
        {
            RPC_RequestActivate();
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestActivate(RpcInfo info = default)
    {
        HandleActivate(info.Source);
    }

    // ───────── 상태 권한 ─────────

    private void HandleActivate(PlayerRef source)
    {
        if (!Object.HasStateAuthority)
        {
            return;
        }

        int tick = Runner.Tick.Raw;
        RequesterFacts requester = HostRequesters.ForTerminal(source, _cooldown, tick);
        bool discovered = area == null || area.IsDiscovered;
        RequestVerdict verdict = SprinklerRule.Evaluate(NetUsed, discovered, requester);

        if (verdict != RequestVerdict.Accepted)
        {
            Reject(source, verdict);
            return;
        }

        _cooldown.Record(HostRequesters.Key(source), tick);

        NetUsed = true;
        NetRunning = true;
        RunTimer = TickTimer.CreateFromSeconds(Runner, Mathf.Max(0.1f, activeDuration));

        Debug.Log($"[SprinklerZone] 스프링클러 작동 시작: {gameObject.name}");

        // 가동 즉시 한 번: 안의 대상 감속·흔적 정화, 소음 이벤트.
        TickEffectsOnAuthority();
        AuthorityActivated?.Invoke(this, _zoneCollider.bounds.center);
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority || !NetRunning)
        {
            return;
        }

        if (RunTimer.ExpiredOrNotRunning(Runner))
        {
            RunTimer = TickTimer.None;
            NetRunning = false;
            Debug.Log($"[SprinklerZone] 스프링클러 작동 종료: {gameObject.name}");
            return;
        }

        if (Runner.IsForward)
        {
            TickEffectsOnAuthority();
        }
    }

    // 작동 중 매 틱: 구역 안 대상의 감속 시간을 계속 갱신한다 → 벗어나면 afterExitSlowDuration 동안 유지(예전 Stay/Exit 동작과 같다).
    private void TickEffectsOnAuthority()
    {
        IReadOnlyList<SprinklerSlowTarget> targets = SprinklerSlowTarget.All;

        for (int i = 0; i < targets.Count; i++)
        {
            SprinklerSlowTarget target = targets[i];

            if (target != null && Contains(target.transform.position))
            {
                target.ApplySlow(Runner, slowMultiplier, afterExitSlowDuration);
            }
        }

        if (purgeFurTraces)
        {
            PurgeFurTraces();
        }
    }

    private void PurgeFurTraces()
    {
        IReadOnlyList<FurBallTrace> traces = FurBallTrace.Active;

        if (traces == null || traces.Count == 0)
        {
            return;
        }

        Bounds bounds = _zoneCollider.bounds;
        TraceBuffer.Clear();

        for (int i = 0; i < traces.Count; i++)
        {
            FurBallTrace trace = traces[i];

            if (trace == null || trace.Object == null || !trace.Object.IsValid)
            {
                continue;
            }

            Vector3 position = trace.transform.position;

            if (bounds.Contains(position) && Contains(position))
            {
                TraceBuffer.Add(trace);
            }
        }

        for (int i = 0; i < TraceBuffer.Count; i++)
        {
            Runner.Despawn(TraceBuffer[i].Object);
        }

        TraceBuffer.Clear();
    }

    private bool Contains(Vector3 point)
    {
        if (_zoneCollider == null || !_zoneCollider.enabled)
        {
            return false;
        }

        // 발 위치가 박스 바닥 경계에 걸칠 수 있어 조금 올려서 본다.
        Vector3 probe = point + Vector3.up * 0.1f;
        return (_zoneCollider.ClosestPoint(probe) - probe).sqrMagnitude < 0.0001f;
    }

    private void Reject(PlayerRef source, RequestVerdict verdict)
    {
        Debug.Log($"[SprinklerZone] {gameObject.name} 가동 요청 거부: {verdict}");

        if (source == Runner.LocalPlayer)
        {
            LocalRequestRejected?.Invoke(this, verdict);
        }
        else if (source != PlayerRef.None)
        {
            RPC_NotifyRejected(source, (int)verdict);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_NotifyRejected([RpcTarget] PlayerRef target, int verdict)
    {
        Debug.Log($"[SprinklerZone] {gameObject.name} 내 가동 요청 거부됨: {(RequestVerdict)verdict}");
        LocalRequestRejected?.Invoke(this, (RequestVerdict)verdict);
    }

    // ───────── 보여주기 (모든 피어) ─────────

    private void OnSprinklerStateChanged()
    {
        ApplyVisual();
        StateChanged?.Invoke(this);
    }

    private void ApplyVisual()
    {
        if (activeEffect != null)
        {
            activeEffect.SetActive(IsRunning);
        }
    }

    [ContextMenu("TEST Activate")]
    private void TestActivate()
    {
        ActivateSprinkler();
    }
}
