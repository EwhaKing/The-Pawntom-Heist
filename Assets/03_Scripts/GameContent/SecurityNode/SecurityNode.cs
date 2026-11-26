using System.Collections.Generic;
using Fusion;
using Pawntom.Stage;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 분전반(보안 노드). GAME_DESIGN §3.4 CCTV 링크 트리거 — 침투조가 분전반 앞에서 상호작용 키를 <b>길게</b> 눌러
/// 3초 캐스팅(이동 불가)을 마치면 연결된 구역(<see cref="DiscoverableArea"/>)이 해금된다.
/// <para>
/// 흐름: PlayerInteraction 이 분전반을 겨누고 누르면 <see cref="TryBeginLocalCast"/> → 이 컴포넌트가 로컬에서 진행도를 채우고
/// (손 떼기·이동·범위 이탈 시 취소, 규칙은 <see cref="CastProgressModel"/>) 시작/완료를 RPC 로 상태 권한에 보낸다.
/// 상태 권한은 거리·소유·사망·중복·캐스팅 시간·이동을 다시 검증(<see cref="SecurityNodeConnectRule"/>)한 뒤
/// <see cref="IsConnected"/> 를 켜고 연결된 구역의 <c>Discover()</c> 를 부른다.
/// </para>
/// <para>
/// 늦게 들어온 참가자는 <see cref="IsConnected"/> 만 받아도 같은 상태가 된다(Spawned/Render 에서 반영).
/// 캐스팅 바 UI 는 정적 이벤트(로컬 플레이어 기준)를 구독해 그린다.
/// </para>
/// 필요 컴포넌트: NetworkObject(씬 오브젝트), 상호작용 대상 Collider.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Pawntom/Gameplay/Security Node")]
public sealed class SecurityNode : NetworkBehaviour, ILevelLayoutSource
{
    [Header("레벨 데이터")]
    [Tooltip("분전반 Id. 비우면 GameObject 이름")]
    [SerializeField] private string _id;

    [Tooltip("레벨 데이터에 기록할 해금 구역 Id. 비우면 연결된 첫 구역의 AreaId")]
    [SerializeField] private string _unlockZoneId;

    [Header("해금 구역")]
    [Tooltip("접속 완료 시 Discover() 할 구역들")]
    [SerializeField] private DiscoverableArea[] _linkedAreas = new DiscoverableArea[0];

    [Header("상호작용")]
    [Tooltip("분전반 앞에서 \"E | 이름\" 으로 보일 이름")]
    [SerializeField] private string _interactName = "분전반 접속 (길게)";

    [Tooltip("캐스팅 바에 보일 문구")]
    [SerializeField] private string _castLabel = "분전반 접속 중";

    [Tooltip("GAME_DESIGN §3.4 분전반 캐스팅 3초")]
    [SerializeField, Min(0.1f)] private float _castSeconds = 3f;

    [Tooltip("캐스팅 시작 위치에서 이만큼(m) 밀려나면 로컬에서 취소한다")]
    [SerializeField, Min(0.05f)] private float _localMoveCancelDistance = 0.25f;

    [Header("상태 권한 검증")]
    [Tooltip("호스트가 보는 위치는 조금 늦다. 표준 상호작용 거리에 더해 허용하는 여유(m)")]
    [SerializeField, Min(0f)] private float _hostRangeTolerance = 0.75f;

    [Tooltip("시작·완료 RPC 지연 차이로 허용하는 캐스팅 시간 부족분(초)")]
    [SerializeField, Min(0f)] private float _castTimeTolerance = 0.3f;

    [Tooltip("캐스팅 중 호스트 기준으로 허용하는 위치 변화(m)")]
    [SerializeField, Min(0f)] private float _maxCastDisplacement = 0.6f;

    [Tooltip("완료 요청 뒤 이 시간(초) 안에 접속 상태가 오지 않으면 거부된 것으로 알린다")]
    [SerializeField, Min(0.2f)] private float _connectResultTimeout = 2f;

    [Header("이벤트 (씬에서 연결, 모든 피어에서 1회)")]
    [Tooltip("접속 완료가 이 피어에 반영된 순간. LED·링크음 등. 늦은 참가자에게는 오지 않는다(상태로 반영)")]
    [SerializeField] private UnityEvent _onConnected = new UnityEvent();

    [Tooltip("접속 상태가 반영될 때 켤 오브젝트(늦은 참가자 포함). 없으면 무시")]
    [SerializeField] private GameObject _connectedIndicator;

    [Networked] public NetworkBool IsConnected { get; private set; }

    /// <summary>접속을 완료한 플레이어. 접속 전에는 PlayerRef.None.</summary>
    [Networked] public PlayerRef ConnectedBy { get; private set; }

    // ───────── 로컬 플레이어 기준 정적 이벤트 (캐스팅 바 UI 용) ─────────

    /// <summary>내 플레이어가 캐스팅을 시작했다.</summary>
    public static event System.Action<SecurityNode> LocalCastStarted;

    /// <summary>내 캐스팅 진행도(0~1). 캐스팅 중 매 프레임.</summary>
    public static event System.Action<SecurityNode, float> LocalCastProgress;

    /// <summary>내 캐스팅이 취소됐다(손 떼기·이동·범위 이탈·사망·다른 사람이 먼저 완료).</summary>
    public static event System.Action<SecurityNode> LocalCastCanceled;

    /// <summary>내 캐스팅을 끝까지 채워 접속 요청을 보냈다(아직 확정 아님 — 확정은 <see cref="Connected"/>).</summary>
    public static event System.Action<SecurityNode> LocalCastCompleted;

    /// <summary>내 접속 요청이 제한 시간 안에 반영되지 않았다(상태 권한이 거부).</summary>
    public static event System.Action<SecurityNode> LocalConnectRejected;

    /// <summary>모든 피어: 접속 상태가 이 피어에 처음 반영됐다(변경 감지 1회). 늦은 참가자는 <see cref="IsConnected"/> 로 안다.</summary>
    public static event System.Action<SecurityNode> Connected;

    private static readonly List<SecurityNode> ActiveNodes = new List<SecurityNode>(8);

    /// <summary>씬에 스폰된 분전반들.</summary>
    public static IReadOnlyList<SecurityNode> All
    {
        get { return ActiveNodes; }
    }

    /// <summary>내 플레이어가 지금 캐스팅 중인 분전반. 없으면 null.</summary>
    public static SecurityNode LocalCastingNode { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        LocalCastStarted = null;
        LocalCastProgress = null;
        LocalCastCanceled = null;
        LocalCastCompleted = null;
        LocalConnectRejected = null;
        Connected = null;
        ActiveNodes.Clear();
        LocalCastingNode = null;
        SharedCastInput.ResetStatics();
    }

    // ───────── 상태 ─────────

    private struct HostCast
    {
        public int StartTick;
        public Vector3 StartPosition;
    }

    // 상태 권한 쪽에서만 쓰는 캐스팅 시작 기록(네트워크 상태가 아니다 — 호스트만 판정한다).
    private readonly Dictionary<PlayerRef, HostCast> _hostCasts = new Dictionary<PlayerRef, HostCast>(4);

    private CastProgressModel _model;
    private SecurityNodeConnectRule _rule;
    private PlayerData _caster;
    private Vector3 _castStartPosition;
    private float _lastClock;
    private bool _awaitingResult;
    private float _awaitUntil;
    private bool _connectedApplied;
    private bool _inputAcquired;

    public string InteractName
    {
        get { return _interactName; }
    }

    public string CastLabel
    {
        get { return _castLabel; }
    }

    public float CastSeconds
    {
        get { return _castSeconds; }
    }

    public bool IsLocalCasting
    {
        get { return _caster != null && _model != null && _model.IsCasting; }
    }

    /// <summary>내 캐스팅 진행도(0~1). 캐스팅 중이 아니면 0.</summary>
    public float LocalCastProgressValue
    {
        get { return IsLocalCasting ? _model.Progress : 0f; }
    }

    public IReadOnlyList<DiscoverableArea> LinkedAreas
    {
        get { return _linkedAreas; }
    }

    public string LayoutId
    {
        get { return LevelLayoutIds.Resolve(_id, this); }
    }

    private bool IsReady
    {
        get { return Object != null && Object.IsValid; }
    }

    private void Awake()
    {
        _model = new CastProgressModel(_castSeconds);
        _rule = new SecurityNodeConnectRule(_castSeconds, _castTimeTolerance, _maxCastDisplacement);
    }

    public override void Spawned()
    {
        if (!ActiveNodes.Contains(this))
        {
            ActiveNodes.Add(this);
        }

        SharedCastInput.Retain();

        // 늦게 들어온 참가자·재진입: 이미 접속된 상태면 이벤트 없이 상태만 맞춘다.
        _connectedApplied = false;

        if (IsConnected)
        {
            _connectedApplied = true;
            ApplyConnectedLocally();
        }
        else if (_connectedIndicator != null)
        {
            _connectedIndicator.SetActive(false);
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        CancelLocalCast(true);
        _hostCasts.Clear();
        ActiveNodes.Remove(this);
        SharedCastInput.Release();
    }

    private void OnDisable()
    {
        CancelLocalCast(true);
    }

    public override void Render()
    {
        // 변경 감지: 접속 상태가 이 피어에 처음 반영되는 순간 한 번만 부작용을 낸다.
        if (IsConnected && !_connectedApplied)
        {
            _connectedApplied = true;
            ApplyConnectedLocally();
            Connected?.Invoke(this);
            _onConnected.Invoke();
        }
    }

    /// <summary>플레이어(발 위치)가 분전반 상호작용 범위 안에 있는가. 모든 E 상호작용과 같은 표준 거리.</summary>
    public bool IsPlayerInRange(Vector3 playerPosition)
    {
        return InteractRange.IsInRange(this, playerPosition, InteractRange.StandardMargin);
    }

    /// <summary>겨눴을 때 띄울 안내. 이미 접속됐거나 캐스팅 중이면 null.</summary>
    public string GetPromptName(Vector3 playerPosition)
    {
        if (!IsReady || IsConnected || IsLocalCasting || LocalCastingNode != null)
        {
            return null;
        }

        return IsPlayerInRange(playerPosition) ? _interactName : null;
    }

    /// <summary>
    /// 로컬 플레이어가 상호작용 키를 눌러 캐스팅을 시작한다(PlayerInteraction 이 부른다).
    /// 시작했으면 true. 이후 진행·취소·완료는 이 컴포넌트가 키를 누르고 있는 동안 처리한다.
    /// </summary>
    public bool TryBeginLocalCast(PlayerData localPlayer)
    {
        if (!IsReady || localPlayer == null || localPlayer.Object == null || !localPlayer.Object.IsValid ||
            !localPlayer.Object.HasInputAuthority || LocalCastingNode != null || _awaitingResult)
        {
            return false;
        }

        Vector3 position = localPlayer.transform.position;
        SecurityNodeVerdict verdict = _rule.CanBegin(IsConnected, true, localPlayer.IsDead, IsPlayerInRange(position));

        if (verdict != SecurityNodeVerdict.Accepted)
        {
            return false;
        }

        _model.Reset();

        if (_model.Tick(0f, true, false, true) != CastStep.Started)
        {
            return false;
        }

        _caster = localPlayer;
        _castStartPosition = position;
        _lastClock = Runner.SimulationTime;
        LocalCastingNode = this;

        // GAME_DESIGN §3.4 "이동 불가": 캐스팅 동안 이동·시점 입력을 잠근다. 키 유지 여부는 별도 입력 인스턴스로 읽는다.
        ScriptedInputLock.Acquire(this);
        _inputAcquired = true;

        RPC_BeginCast(localPlayer.Object);
        LocalCastStarted?.Invoke(this);
        LocalCastProgress?.Invoke(this, 0f);
        return true;
    }

    private void Update()
    {
        if (!IsReady)
        {
            return;
        }

        if (_awaitingResult)
        {
            PollConnectResult();
        }

        if (_caster == null || !_model.IsCasting)
        {
            return;
        }

        float clock = Runner.SimulationTime;
        float delta = Mathf.Clamp(clock - _lastClock, 0f, 0.25f);
        _lastClock = clock;

        bool alive = _caster != null && _caster.Object != null && _caster.Object.IsValid && !_caster.IsDead;

        if (!alive || IsConnected)
        {
            // 사망했거나 다른 사람이 먼저 접속을 마쳤다.
            CancelLocalCast(true);
            return;
        }

        Vector3 position = _caster.transform.position;
        bool blocked = GameplayInputBlocker.IsBlocked || Hacking.HackingManager.AnyHackingActive;
        bool holding = !blocked && SharedCastInput.InteractHeld;
        bool moved = SharedCastInput.MoveRequested ||
                     (position - _castStartPosition).sqrMagnitude > _localMoveCancelDistance * _localMoveCancelDistance;
        bool inRange = IsPlayerInRange(position);

        switch (_model.Tick(delta, holding, moved, inRange))
        {
            case CastStep.Progressed:
                LocalCastProgress?.Invoke(this, _model.Progress);
                break;

            case CastStep.Canceled:
                EndLocalCast();
                LocalCastCanceled?.Invoke(this);
                break;

            case CastStep.Completed:
                NetworkObject casterObject = _caster.Object;
                EndLocalCast();
                LocalCastProgress?.Invoke(this, 1f);
                _awaitingResult = true;
                _awaitUntil = Runner.SimulationTime + _connectResultTimeout;
                RPC_RequestConnect(casterObject);
                LocalCastCompleted?.Invoke(this);
                break;
        }
    }

    // 완료 요청을 보낸 뒤 접속 상태가 반영되는지 본다. 거부되면 IsConnected 가 바뀌지 않는다 — 성공으로 처리하지 않는다.
    private void PollConnectResult()
    {
        if (IsConnected)
        {
            _awaitingResult = false;
            return;
        }

        if (Runner.SimulationTime < _awaitUntil)
        {
            return;
        }

        _awaitingResult = false;
        LocalConnectRejected?.Invoke(this);
    }

    private void CancelLocalCast(bool notify)
    {
        if (_caster == null)
        {
            ReleaseInputLock();
            return;
        }

        bool wasCasting = _model != null && _model.Cancel();
        EndLocalCast();

        if (notify && wasCasting)
        {
            LocalCastCanceled?.Invoke(this);
        }
    }

    private void EndLocalCast()
    {
        _caster = null;

        if (LocalCastingNode == this)
        {
            LocalCastingNode = null;
        }

        ReleaseInputLock();
    }

    private void ReleaseInputLock()
    {
        if (!_inputAcquired)
        {
            return;
        }

        _inputAcquired = false;
        ScriptedInputLock.Release(this);
    }

    // ───────── 상태 권한 ─────────

    [Rpc(RpcSources.All, RpcTargets.StateAuthority, HostMode = RpcHostMode.SourceIsHostPlayer)]
    private void RPC_BeginCast(NetworkObject playerObject, RpcInfo info = default)
    {
        bool valid = TryResolvePlayer(playerObject, info.Source, out PlayerData player);
        SecurityNodeVerdict verdict = _rule.CanBegin(IsConnected, valid, valid && player.IsDead,
            valid && IsInHostRange(player.transform.position));

        if (verdict != SecurityNodeVerdict.Accepted)
        {
            Debug.Log($"[SecurityNode] {LayoutId} 캐스팅 시작 거부: {verdict}", this);
            return;
        }

        // 다시 시작하면 이전 기록을 덮는다(취소 RPC 는 따로 두지 않는다).
        _hostCasts[info.Source] = new HostCast
        {
            StartTick = Runner.Tick.Raw,
            StartPosition = player.transform.position,
        };
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority, HostMode = RpcHostMode.SourceIsHostPlayer)]
    private void RPC_RequestConnect(NetworkObject playerObject, RpcInfo info = default)
    {
        bool valid = TryResolvePlayer(playerObject, info.Source, out PlayerData player);
        bool hasRecord = _hostCasts.TryGetValue(info.Source, out HostCast cast);

        var request = new SecurityNodeConnectRequest
        {
            AlreadyConnected = IsConnected,
            PlayerValid = valid,
            PlayerDead = valid && player.IsDead,
            InRange = valid && IsInHostRange(player.transform.position),
            HasCastRecord = hasRecord,
            ElapsedSeconds = hasRecord ? (Runner.Tick.Raw - cast.StartTick) * Runner.DeltaTime : 0f,
            Displacement = hasRecord && valid ? Vector3.Distance(player.transform.position, cast.StartPosition) : 0f,
        };

        SecurityNodeVerdict verdict = _rule.Evaluate(request);
        _hostCasts.Remove(info.Source);

        if (verdict != SecurityNodeVerdict.Accepted)
        {
            Debug.Log($"[SecurityNode] {LayoutId} 접속 요청 거부: {verdict} (경과 {request.ElapsedSeconds:F2}s, 이동 {request.Displacement:F2}m)", this);
            return;
        }

        ConnectOnAuthority(info.Source);
    }

    private void ConnectOnAuthority(PlayerRef source)
    {
        if (!Object.HasStateAuthority || IsConnected)
        {
            return;
        }

        IsConnected = true;
        ConnectedBy = source;
        _hostCasts.Clear();

        // 구역 해금은 상태 권한 쪽에서 부른다. (DiscoverableArea 네트워크화는 network-dev 후속 작업)
        DiscoverLinkedAreas();
        Debug.Log($"[SecurityNode] {LayoutId} 접속 완료 — 구역 {_linkedAreas.Length}곳 해금", this);
    }

    private bool TryResolvePlayer(NetworkObject playerObject, PlayerRef source, out PlayerData player)
    {
        player = null;

        if (playerObject == null || !playerObject.IsValid || source == PlayerRef.None ||
            playerObject.InputAuthority != source)
        {
            return false;
        }

        player = playerObject.GetComponent<PlayerData>();
        return player != null;
    }

    private bool IsInHostRange(Vector3 playerPosition)
    {
        return InteractRange.IsInRange(this, playerPosition, InteractRange.StandardMargin + _hostRangeTolerance);
    }

    // ───────── 접속 상태 반영 (모든 피어) ─────────

    private void ApplyConnectedLocally()
    {
        if (_connectedIndicator != null)
        {
            _connectedIndicator.SetActive(true);
        }

        // DiscoverableArea 가 아직 네트워크 상태가 아니므로, 클라이언트·늦은 참가자도 IsConnected 로 같은 해금을 맞춘다.
        // network-dev 가 DiscoverableArea 를 [Networked] 로 바꾸면 권한 쪽 호출(ConnectOnAuthority)만 남기고 이 줄을 지운다.
        DiscoverLinkedAreas();
    }

    private void DiscoverLinkedAreas()
    {
        for (int i = 0; i < _linkedAreas.Length; i++)
        {
            DiscoverableArea area = _linkedAreas[i];

            if (area != null && !area.IsDiscovered)
            {
                area.Discover();
            }
        }
    }

    // ───────── 레벨 데이터 ─────────

    public void AppendTo(StageLayout layout)
    {
        var zoneIds = new List<string>(_linkedAreas.Length);

        for (int i = 0; i < _linkedAreas.Length; i++)
        {
            if (_linkedAreas[i] != null)
            {
                zoneIds.Add(_linkedAreas[i].AreaId);
            }
        }

        SecurityNodeLayout.Append(layout, LayoutId, LevelLayoutData.ToStage(transform.position), _unlockZoneId, zoneIds);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.49f, 1f, 0.7f, 0.8f);
        Bounds range = InteractRange.RangeBounds(this, InteractRange.StandardMargin);
        Gizmos.DrawWireCube(range.center, range.size);

        Gizmos.color = new Color(0.49f, 1f, 0.7f, 0.4f);

        for (int i = 0; i < _linkedAreas.Length; i++)
        {
            if (_linkedAreas[i] != null)
            {
                Gizmos.DrawLine(transform.position, _linkedAreas[i].transform.position);
            }
        }
    }

    /// <summary>
    /// 캐스팅 중에는 ScriptedInputLock 이 게임 입력(InputManager)을 꺼 두므로, 키 유지·이동 의도는 따로 만든 입력 인스턴스로 읽는다.
    /// 분전반이 여러 개여도 인스턴스는 하나만 만든다(생성 비용이 크다). 옵션창 키 설정을 따른다.
    /// </summary>
    private static class SharedCastInput
    {
        private static InputSystemActions _actions;
        private static int _users;

        public static bool InteractHeld
        {
            get { return _actions != null && _actions.Player.Interact.IsPressed(); }
        }

        public static bool MoveRequested
        {
            get { return _actions != null && _actions.Player.Move.ReadValue<Vector2>().sqrMagnitude > 0.04f; }
        }

        public static void Retain()
        {
            _users++;

            if (_actions != null)
            {
                return;
            }

            _actions = new InputSystemActions();
            SyncBindings();
            _actions.Player.Enable();
            InputManager.BindingsChanged += SyncBindings;
        }

        public static void Release()
        {
            _users = Mathf.Max(0, _users - 1);

            if (_users > 0 || _actions == null)
            {
                return;
            }

            InputManager.BindingsChanged -= SyncBindings;
            _actions.Player.Disable();
            _actions.Dispose();
            _actions = null;
        }

        public static void ResetStatics()
        {
            _users = 0;
            _actions = null;
        }

        private static void SyncBindings()
        {
            InputManager input = InputManager.Live;

            if (_actions != null && input != null)
            {
                input.CopyBindingOverridesTo(_actions);
            }
        }
    }
}
