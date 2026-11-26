using Fusion;
using Pawntom.Network;
using Pawntom.Stage;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// ControllableWall
///
/// 담당:
/// - 실제 맵에 있는 격벽의 해금 상태 관리
/// - 실제 격벽 열림/닫힘 처리
/// - 열린 상태에서는 실제 벽 Renderer/Collider를 끔
/// - 닫힌 상태에서는 실제 벽 Renderer/Collider를 켬
/// - CCTV/미니맵용 격벽 표시만 빨강/파랑으로 변경
///
/// 특징:
/// - 상태(IsUnlocked/IsOpen)는 [Networked] 값으로 StateAuthority가 권위를 가진다
/// - 공개 조작 메서드(Unlock/TryFieldOpen/OpenWall/CloseWall/ToggleWall)는 "요청"이다. 클라이언트는 RPC 로 보내고,
///   상태 권한이 요청자(RpcInfo.Source)를 다시 판정한다(<see cref="WallCommandRule"/>):
///   생존, 위치(현장 개방 = 문 근처 / 본부 조작 = 단말기 앞·본부 안), 상태 전이 유효성, 짧은 쿨다운.
///   거부되면 요청자에게만 <see cref="LocalRequestRejected"/> 로 알린다. 호출 직후 상태가 바뀌었다고 가정하지 않는다.
/// - 초기 해금 상태: 상태 권한이 Spawned 에서 레벨 데이터(StageLayoutProvider) → 같은 오브젝트의 DoorLayoutMarker →
///   기본값(잠김) 순으로 정한다.
/// - 연출 세션(튜토리얼)의 체크포인트 복원(<see cref="RestoreCheckpointState"/>)은 검증 없이 상태 권한에서 바로 쓴다.
///
/// 사용 위치:
/// - 실제 맵의 Wall_01, Wall_02 같은 격벽 오브젝트에 붙임
/// </summary>
public class ControllableWall : NetworkBehaviour
{
    [Header("Actual Wall")]
    [Tooltip("실제 맵에서 보이는 벽 Renderer")]
    [SerializeField] private Renderer[] actualWallRenderers;

    [Tooltip("실제 플레이어를 막는 Collider")]
    [SerializeField] private Collider[] actualWallColliders;

    [Header("Navigation")]
    [Tooltip("닫힘 상태에서만 켜지는 NavMeshObstacle(Carve). 비워 두면 자기/자식에서 자동 탐색한다. " +
             "이 격벽은 NavMesh 굽기에서 제외(NavMeshModifier의 Ignore From Build)해야 열렸을 때 적이 지나간다")]
    [SerializeField] private NavMeshObstacle navMeshObstacle;

    [Header("Minimap / CCTV Visual")]
    [Tooltip("CCTV/미니맵에 보이는 격벽 표시용 Renderer")]
    [SerializeField] private Renderer minimapWallRenderer;

    // 세 상태를 색(빨강·파랑·초록)과 모양(꽉 찬 막대 / 얇은 막대 / 자물쇠)으로 이중 구분한다.
    // 해금 안 된 격벽은 열 수 없으므로 실제 상태는 이 셋뿐이다(UI_SPEC 3.2 참고).
    [Header("Minimap Colors")]
    [Tooltip("잠김(해금 안 됨, 닫힘) = 코랄 #FF4E56")]
    [SerializeField] private Color minimapClosedColor = new Color(1f, 0.306f, 0.337f, 1f);

    [Tooltip("열림 = 민트 #10FFB8. 막대가 얇아져 통로가 뚫린 모양으로 보인다")]
    [SerializeField] private Color minimapOpenColor = new Color(0.063f, 1f, 0.722f, 1f);

    [Tooltip("해금됐지만 닫혀 있을 때 = 파랑 #2F8CFF (대본의 \"푸른색이 됐습니다\")")]
    [SerializeField] private Color minimapUnlockedColor = new Color(0.184f, 0.549f, 1f, 1f);

    [Header("Minimap Shape")]
    [Tooltip("열림일 때 표시용 막대의 두께 비율. 작을수록 가늘다. 1이면 두께 변화 없음")]
    [SerializeField, Range(0.1f, 1f)] private float openThicknessRatio = 0.3f;

    [Tooltip("미니맵 격벽 막대를 둥근 알약 모양으로 그린다. 끄면 예전 각진 막대")]
    [SerializeField] private bool roundedMinimapShape = true;

    [Tooltip("잠김일 때 격벽 위에 띄울 자물쇠 아이콘(위에서 본 모양). 비우면 표시하지 않는다")]
    [SerializeField] private Sprite lockIcon;

    [Tooltip("자물쇠 아이콘의 월드 크기(m)")]
    [SerializeField, Min(0.2f)] private float lockIconSize = 1.4f;

    // 월드(3D)에서도 육안으로 상태를 구분할 수 있게 한다. 실제 벽은 열리면 사라지므로 표시는 벽과 따로 둔다.
    [Header("World Indicator (현장 육안 구분)")]
    [Tooltip("바닥 문턱 띠에 쓰는 단색 재질(URP Unlit). 비우면 띠는 만들지 않고 벽 틴트만 적용한다")]
    [SerializeField] private Material indicatorMaterial;

    [Tooltip("닫힌 실제 벽에 상태색을 섞는 비율. 0 이면 벽 색은 그대로")]
    [SerializeField, Range(0f, 0.8f)] private float worldTintStrength = 0.35f;

    [Header("Field Open (현장 상호작용)")]
    [Tooltip("끄면 현장에서 상호작용 키로 격벽을 열 수 없다. 본부(OpenWall/CloseWall)에서만 열고 닫으며, 한 번 열린 격벽은 본부에서 닫기 전까지 열려 있다")]
    [SerializeField] private bool allowFieldOpen = true;


    [Tooltip("현장에서 상호작용 키로 연 뒤 자동으로 닫힐 때까지의 시간. GAME_DESIGN 격벽: Open → 5초 후 Unlocked 회귀")]
    [SerializeField, Min(0.5f)] private float fieldOpenSeconds = 5f;

    [Tooltip("닫힐 차례에 문 자리에 플레이어가 있으면 이만큼 미룬다(끼임 방지)")]
    [SerializeField, Min(0.1f)] private float closeRetrySeconds = 0.5f;

    [Networked, OnChangedRender(nameof(OnWallStateChanged))]
    public NetworkBool IsUnlocked { get; private set; }

    [Networked, OnChangedRender(nameof(OnWallStateChanged))]
    public NetworkBool IsOpen { get; private set; }

    /// <summary>현장에서 연 경우 자동으로 닫힐 시각. 본부에서 연 경우(수동)는 None.</summary>
    [Networked] private TickTimer AutoCloseTimer { get; set; }

    /// <summary>상태가 바뀔 때마다(원격 포함) 호출된다. UI가 구독해 새로고침한다.</summary>
    public event System.Action OnStateChanged;

    /// <summary>내(이 피어의) 요청이 상태 권한에서 거부됐다. 요청한 피어에서만 발생한다.</summary>
    public static event System.Action<ControllableWall, WallCommand, RequestVerdict> LocalRequestRejected;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        LocalRequestRejected = null;
    }

    private RequestCooldown _cooldown;

    // 닫혀 있을 때의 문 자리(콜라이더 경계). 열리면 콜라이더가 꺼지므로 미리 잡아 둔다.
    private Bounds _doorwayBounds;
    private bool _hasDoorwayBounds;

    private void Awake()
    {
        // Inspector에 직접 연결하지 않았을 때를 위한 자동 탐색
        if (actualWallRenderers == null || actualWallRenderers.Length == 0)
        {
            actualWallRenderers = GetComponentsInChildren<Renderer>();
        }

        if (actualWallColliders == null || actualWallColliders.Length == 0)
        {
            actualWallColliders = GetComponentsInChildren<Collider>();
        }

        if (navMeshObstacle == null)
        {
            navMeshObstacle = GetComponentInChildren<NavMeshObstacle>();
        }

        CacheDoorwayBounds();
    }

    private void CacheDoorwayBounds()
    {
        if (actualWallColliders == null)
        {
            return;
        }

        for (int i = 0; i < actualWallColliders.Length; i++)
        {
            Collider wallCollider = actualWallColliders[i];

            if (wallCollider == null || !wallCollider.enabled || wallCollider.isTrigger)
            {
                continue;
            }

            if (!_hasDoorwayBounds)
            {
                _doorwayBounds = wallCollider.bounds;
                _hasDoorwayBounds = true;
            }
            else
            {
                _doorwayBounds.Encapsulate(wallCollider.bounds);
            }
        }

        // 문 앞뒤로 서 있는 플레이어도 "문 자리"로 본다.
        _doorwayBounds.Expand(0.6f);
    }

    /// <summary>
    /// 현장에서 E 를 눌렀을 때 열 수 있는 상태면 안내에 띄울 이름, 아니면 null.
    /// 현장 개방이 꺼져 있거나, 잠겼거나, 이미 열려 있으면 안내를 띄우지 않는다.
    /// </summary>
    public string FieldInteractName
    {
        get
        {
            if (!allowFieldOpen || Object == null || !Object.IsValid || !IsUnlocked || IsOpen)
            {
                return null;
            }

            return "격벽 열기";
        }
    }

    private bool IsNetworkReady => Object != null && Object.IsValid;

    /// <summary>
    /// 현장(침투조) 상호작용. 해금된(푸른) 격벽만 열리고, <see cref="fieldOpenSeconds"/> 뒤 자동으로 닫힌다.
    /// 잠긴(붉은) 격벽이면 false — 본부 해킹으로 먼저 해금해야 한다.
    /// true 는 "요청을 보냈다"는 뜻이다. 열림은 상태 권한이 요청자 위치를 확인한 뒤 <see cref="IsOpen"/> 으로 반영된다.
    /// </summary>
    public bool TryFieldOpen()
    {
        if (!allowFieldOpen || !IsNetworkReady)
        {
            return false;
        }

        if (!IsUnlocked)
        {
            Debug.Log($"[ControllableWall] {gameObject.name} 잠김 — 접근 거부. 본부에서 해킹으로 해금해야 합니다.");
            return false;
        }

        if (IsOpen)
        {
            return false;
        }

        Request(WallCommand.FieldOpen);
        return true;
    }

    // ───────── 요청 → 상태 권한 재검증 ─────────

    private void Request(WallCommand command)
    {
        if (!IsNetworkReady)
        {
            Debug.LogWarning($"[ControllableWall] {gameObject.name} 가 네트워크에 스폰되지 않아 {command} 요청을 보낼 수 없습니다.", this);
            return;
        }

        if (Object.HasStateAuthority)
        {
            HandleRequest(Runner.LocalPlayer, command);
        }
        else
        {
            RPC_Request((int)command);
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_Request(int command, RpcInfo info = default)
    {
        HandleRequest(info.Source, (WallCommand)command);
    }

    private void HandleRequest(PlayerRef source, WallCommand command)
    {
        if (!Object.HasStateAuthority)
        {
            return;
        }

        int tick = Runner.Tick.Raw;
        RequesterFacts requester = WallCommandRule.IsField(command)
            ? HostRequesters.ForField(source, this, _cooldown, tick)
            : HostRequesters.ForTerminal(source, _cooldown, tick);

        RequestVerdict verdict = WallCommandRule.Evaluate(command, IsUnlocked, IsOpen, allowFieldOpen, requester);

        if (verdict != RequestVerdict.Accepted)
        {
            Reject(source, command, verdict);
            return;
        }

        _cooldown?.Record(HostRequesters.Key(source), tick);

        switch (command)
        {
            case WallCommand.Unlock:
                DoUnlock();
                break;

            case WallCommand.FieldOpen:
                DoFieldOpen();
                break;

            case WallCommand.RemoteOpen:
                DoSetOpen(true);
                break;

            case WallCommand.RemoteClose:
                DoSetOpen(false);
                break;
        }
    }

    private void Reject(PlayerRef source, WallCommand command, RequestVerdict verdict)
    {
        Debug.Log($"[ControllableWall] {gameObject.name} {command} 요청 거부: {verdict}");

        if (source == Runner.LocalPlayer)
        {
            LocalRequestRejected?.Invoke(this, command, verdict);
        }
        else if (source != PlayerRef.None)
        {
            RPC_NotifyRejected(source, (int)command, (int)verdict);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_NotifyRejected([RpcTarget] PlayerRef target, int command, int verdict)
    {
        Debug.Log($"[ControllableWall] {gameObject.name} 내 요청 거부됨: {(WallCommand)command} / {(RequestVerdict)verdict}");
        LocalRequestRejected?.Invoke(this, (WallCommand)command, (RequestVerdict)verdict);
    }

    private void DoFieldOpen()
    {
        IsOpen = true;
        AutoCloseTimer = TickTimer.CreateFromSeconds(Runner, fieldOpenSeconds);
        Debug.Log($"[ControllableWall] {gameObject.name} 현장 개방 — {fieldOpenSeconds}초 뒤 자동으로 닫힘");
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority || !IsOpen || !AutoCloseTimer.Expired(Runner))
        {
            return;
        }

        if (IsDoorwayOccupied())
        {
            AutoCloseTimer = TickTimer.CreateFromSeconds(Runner, closeRetrySeconds);
            return;
        }

        AutoCloseTimer = TickTimer.None;
        IsOpen = false;
        Debug.Log($"[ControllableWall] {gameObject.name} 자동으로 닫힘(해금 유지)");
    }

    private bool IsDoorwayOccupied()
    {
        if (!_hasDoorwayBounds)
        {
            return false;
        }

        for (int i = 0; i < PlayerTarget.AllTargets.Count; i++)
        {
            PlayerTarget target = PlayerTarget.AllTargets[i];

            if (target != null && _doorwayBounds.Contains(target.transform.position))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Fusion에서 NetworkObject가 Spawn된 뒤 호출.
    /// [Networked] 값은 이 시점에야 유효하므로 여기서 초기 상태를 반영한다.
    /// </summary>
    public override void Spawned()
    {
        _cooldown = new RequestCooldown(HostRequesters.SecondsToTicks(Runner, HostRequesters.DefaultCooldownSeconds));

        if (Object.HasStateAuthority)
        {
            ApplyInitialStateFromLevelData();
        }

        ApplyWallState();
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        _cooldown?.Clear();
    }

    /// <summary>
    /// 상태 권한 1회: 레벨 데이터의 이 문 항목 → 같은 오브젝트의 DoorLayoutMarker → 기본값(잠김, 건드리지 않음).
    /// 문 Id 는 굽기 도구와 같다(마커가 있으면 마커 Id, 없으면 GameObject 이름).
    /// </summary>
    private void ApplyInitialStateFromLevelData()
    {
        DoorLayoutMarker marker = GetComponent<DoorLayoutMarker>();
        string doorId = marker != null ? marker.Id : gameObject.name;

        bool hasLayoutEntry = TryFindLayoutDoor(doorId, out DoorInitialState layoutState);
        bool hasMarker = TryReadMarkerState(marker, out DoorInitialState markerState);

        if (!DoorInitialStateResolver.TryResolve(hasLayoutEntry, layoutState == DoorInitialState.Unlocked,
                hasMarker, markerState == DoorInitialState.Unlocked, out bool unlocked))
        {
            return;
        }

        IsUnlocked = unlocked;
        IsOpen = false;
        AutoCloseTimer = TickTimer.None;
    }

    private static bool TryFindLayoutDoor(string doorId, out DoorInitialState state)
    {
        state = DoorInitialState.Locked;
        StageLayoutProvider provider = StageLayoutProvider.Current;
        StageLayout layout = provider != null ? provider.Layout : null;

        if (layout == null || string.IsNullOrEmpty(doorId))
        {
            return false;
        }

        for (int i = 0; i < layout.Doors.Count; i++)
        {
            DoorEntry door = layout.Doors[i];

            if (door != null && door.Id == doorId)
            {
                state = door.InitialState;
                return true;
            }
        }

        return false;
    }

    // 마커는 편집용 컴포넌트라 초기 상태를 공개하지 않는다. 굽기와 같은 경로(AppendTo)로 항목을 만들어 읽는다(Spawned 1회).
    private static bool TryReadMarkerState(DoorLayoutMarker marker, out DoorInitialState state)
    {
        state = DoorInitialState.Locked;

        if (marker == null)
        {
            return false;
        }

        var scratch = new StageLayout();
        marker.AppendTo(scratch);

        if (scratch.Doors.Count == 0 || scratch.Doors[0] == null)
        {
            return false;
        }

        state = scratch.Doors[0].InitialState;
        return true;
    }

    /// <summary>
    /// 격벽 해금을 요청(본부 해킹 성공). 상태 권한이 요청자가 단말기 앞인지 확인한다.
    /// </summary>
    public void Unlock()
    {
        Request(WallCommand.Unlock);
    }

    private void DoUnlock()
    {
        IsUnlocked = true;
        Debug.Log($"[ControllableWall] {gameObject.name} 해금 완료");
    }

    /// <summary>
    /// 격벽 열기를 요청(본부). 해금된 격벽만 열린다.
    /// 실제 벽은 사라지고, 미니맵/CCTV 표시는 초록색
    /// </summary>
    public void OpenWall()
    {
        Request(WallCommand.RemoteOpen);
    }

    /// <summary>
    /// 격벽 닫기를 요청(본부).
    /// 실제 벽은 다시 보이고, 미니맵/CCTV 표시는 파란색(해금) 또는 빨간색(잠김)
    /// </summary>
    public void CloseWall()
    {
        Request(WallCommand.RemoteClose);
    }

    private void DoSetOpen(bool open)
    {
        // 본부에서 직접 열고 닫는 것은 수동 조작이다 — 현장 개방의 자동 닫힘 예약을 지운다.
        AutoCloseTimer = TickTimer.None;
        IsOpen = open;
        Debug.Log($"[ControllableWall] {gameObject.name} {(open ? "열림" : "닫힘")}");
    }

    /// <summary>
    /// 해금된 격벽의 열림/닫힘 전환을 요청. 지금 보이는 상태의 반대를 명시적으로 요청한다 —
    /// 그사이 상태가 바뀌었으면 상태 권한이 중복으로 거부한다(엇갈린 두 토글이 원래대로 되돌리지 않게).
    /// </summary>
    public void ToggleWall()
    {
        if (!IsNetworkReady)
        {
            return;
        }

        if (!IsUnlocked)
        {
            Debug.Log($"[ControllableWall] {gameObject.name} 아직 해금되지 않음");
            return;
        }

        Request(IsOpen ? WallCommand.RemoteClose : WallCommand.RemoteOpen);
    }

    /// <summary>
    /// [Networked] 상태가 바뀔 때마다(로컬·원격 무관) 호출된다.
    /// </summary>
    private void OnWallStateChanged()
    {
        ApplyWallState();
        OnStateChanged?.Invoke();
    }

    /// <summary>
    /// 연출 세션(<see cref="ScriptedSessionMode"/>, 지금은 튜토리얼)의 체크포인트 재시도에서 벽 상태를 되돌린다. 상태 권한에서만.
    /// </summary>
    public void RestoreCheckpointState(bool unlocked, bool open)
    {
        if (!ScriptedSessionMode.IsActive || !Object.HasStateAuthority) return;
        IsUnlocked = unlocked;
        IsOpen = open;
        AutoCloseTimer = TickTimer.None;
        ApplyWallState();
        OnStateChanged?.Invoke();
    }

    /// <summary>
    /// 현재 상태를 실제 벽과 미니맵/CCTV 표시용 벽에 반영
    /// </summary>
    private void ApplyWallState()
    {
        // 실제 벽은 색을 바꾸지 않고, 보이기/숨기기만 처리
        bool shouldShowActualWall = !IsOpen;

        if (actualWallRenderers != null)
        {
            for (int i = 0; i < actualWallRenderers.Length; i++)
            {
                if (actualWallRenderers[i] == null)
                {
                    continue;
                }

                actualWallRenderers[i].enabled = shouldShowActualWall;
            }
        }

        // 실제 벽 Collider도 열리면 OFF, 닫히면 ON
        if (actualWallColliders != null)
        {
            for (int i = 0; i < actualWallColliders.Length; i++)
            {
                if (actualWallColliders[i] == null)
                {
                    continue;
                }

                actualWallColliders[i].enabled = !IsOpen;
            }
        }

        // 열림/닫힘은 렌더러와 콜라이더만 바꾸고 굽힌 NavMesh는 그대로다. 닫힌 채로 구우면
        // 열어도 적의 경로가 막혀 있으므로, 굽기에서는 빼고 닫힘 동안만 장애물로 판 자리를 막는다.
        if (navMeshObstacle != null)
        {
            navMeshObstacle.enabled = !IsOpen;
        }

        // 미니맵/CCTV 표시용 벽만 색상·모양 변경
        if (minimapWallRenderer != null)
        {
            // 잠김 = 빨강, 해금(닫힘) = 파랑, 열림 = 초록 (UI_SPEC 색 의미 규칙)
            minimapWallRenderer.material.color = !IsUnlocked
                ? minimapClosedColor
                : (IsOpen ? minimapOpenColor : minimapUnlockedColor);

            ApplyMinimapShape();
        }

        ApplyWorldIndicator(IsUnlocked, IsOpen);
    }

    private Color StateColor(bool unlocked, bool open)
    {
        return !unlocked ? minimapClosedColor : (open ? minimapOpenColor : minimapUnlockedColor);
    }

    private MaterialPropertyBlock _worldBlock;
    private Transform _indicatorRoot;
    private Renderer[] _indicatorRenderers;

    /// <summary>
    /// 월드에서 상태를 구분한다: 닫힌 벽에 상태색 틴트 + 바닥 문턱 띠(열려서 벽이 사라져도 남는다).
    /// 색은 미니맵·CCTV 버튼과 같다(잠김 빨강 / 해금 파랑 / 열림 초록).
    /// </summary>
    private void ApplyWorldIndicator(bool unlocked, bool open)
    {
        Color state = StateColor(unlocked, open);

        if (_worldBlock == null)
        {
            _worldBlock = new MaterialPropertyBlock();
        }

        // 1) 닫힌 실제 벽에 상태색을 섞는다. 원래 색(재질)은 건드리지 않고 인스턴스별 값만 바꾼다.
        if (actualWallRenderers != null && worldTintStrength > 0f)
        {
            Color tint = Color.Lerp(Color.white, state, worldTintStrength);

            for (int i = 0; i < actualWallRenderers.Length; i++)
            {
                Renderer wallRenderer = actualWallRenderers[i];

                if (wallRenderer == null || wallRenderer.sharedMaterial == null || !wallRenderer.sharedMaterial.HasProperty("_BaseColor"))
                {
                    continue;
                }

                wallRenderer.GetPropertyBlock(_worldBlock);
                _worldBlock.SetColor("_BaseColor", tint);
                wallRenderer.SetPropertyBlock(_worldBlock);
            }
        }

        // 2) 문 위 램프와 바닥 문턱 띠.
        EnsureIndicators();

        if (_indicatorRenderers == null)
        {
            return;
        }

        for (int i = 0; i < _indicatorRenderers.Length; i++)
        {
            _worldBlock.Clear();
            _worldBlock.SetColor("_BaseColor", state);
            _indicatorRenderers[i].SetPropertyBlock(_worldBlock);
        }
    }

    // 벽의 자식으로 두지 않는다 — Awake 의 자식 Renderer 자동 탐색에 섞이면 벽이 열릴 때 같이 사라진다.
    private void EnsureIndicators()
    {
        if (_indicatorRoot != null || indicatorMaterial == null)
        {
            return;
        }

        Vector3 lossy = transform.lossyScale;
        bool alongZ = lossy.z >= lossy.x;
        float length = alongZ ? lossy.z : lossy.x;
        float thickness = alongZ ? lossy.x : lossy.z;

        GameObject root = new GameObject("GateIndicators_" + gameObject.name);
        root.transform.SetPositionAndRotation(transform.position, transform.rotation);
        _indicatorRoot = root.transform;

        // 바닥 문턱 띠: 문이 열려 벽이 사라져도 어디가 격벽인지, 어떤 상태인지 남는다.
        // (문 위 램프도 시도했지만 문틀 윗부분에 가려서 보이지 않아 뺐다.) 벽보다 조금만 넓게 해서 양면에서 보이게 한다.
        Vector3 stripSize = alongZ
            ? new Vector3(thickness + 0.15f, 0.04f, length * 0.96f)
            : new Vector3(length * 0.96f, 0.04f, thickness + 0.15f);
        Renderer strip = CreateIndicatorBar("Threshold", new Vector3(0f, -lossy.y * 0.5f + 0.03f, 0f), stripSize);

        _indicatorRenderers = new[] { strip };
    }

    private Renderer CreateIndicatorBar(string barName, Vector3 localPosition, Vector3 size)
    {
        GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bar.name = barName;

        Collider barCollider = bar.GetComponent<Collider>();

        if (barCollider != null)
        {
            // 표시용이라 충돌·길찾기에 관여하지 않는다. (에디터 모드 검증에서도 호출되므로 Play 여부로 가른다)
            if (Application.isPlaying)
            {
                Destroy(barCollider);
            }
            else
            {
                DestroyImmediate(barCollider);
            }
        }

        bar.transform.SetParent(_indicatorRoot, false);
        bar.transform.localPosition = localPosition;
        bar.transform.localScale = size;

        Renderer barRenderer = bar.GetComponent<Renderer>();
        barRenderer.sharedMaterial = indicatorMaterial;
        barRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        barRenderer.receiveShadows = false;

        return barRenderer;
    }

    private Vector3 _minimapBaseScale;
    private bool _minimapScaleCached;
    private Transform _lockIconTransform;

    /// <summary>
    /// 색만으로 구분하지 않도록 모양도 바꾼다. 열림 = 얇은 막대(통로가 뚫린 느낌), 잠김 = 자물쇠 아이콘.
    /// </summary>
    private void ApplyMinimapShape()
    {
        Transform bar = minimapWallRenderer.transform;

        if (!_minimapScaleCached)
        {
            _minimapBaseScale = bar.localScale;
            _minimapScaleCached = true;
        }

        bar.localScale = _minimapBaseScale;

        // 각진 큐브 대신 모서리가 둥근 알약 모양 평면으로 바꾼다. 열림이면 가운데를 비워
        // 양 끝 문틀 조각만 남긴다(통로가 뚫린 느낌). 닫힘은 한 덩어리 알약.
        if (roundedMinimapShape)
        {
            MeshFilter meshFilter = minimapWallRenderer.GetComponent<MeshFilter>();

            if (meshFilter != null)
            {
                Mesh previous = _minimapPillMesh;
                _minimapPillMesh = RoundedShapes.BuildBarMesh(bar.lossyScale, IsUnlocked && IsOpen, 0.25f);
                meshFilter.sharedMesh = _minimapPillMesh;

                if (previous != null)
                {
                    DestroyMesh(previous);
                }
            }
        }
        else if (IsUnlocked && IsOpen)
        {
            Vector3 scale = _minimapBaseScale;

            // 두께 축 = 수평 두 축 중 작은 쪽. 길이(문 폭)는 그대로 둔다.
            if (scale.x <= scale.z)
            {
                scale.x *= openThicknessRatio;
            }
            else
            {
                scale.z *= openThicknessRatio;
            }

            bar.localScale = scale;
        }

        UpdateLockIcon(!IsUnlocked);
    }

    private Mesh _minimapPillMesh;

    private static void DestroyMesh(Mesh mesh)
    {
        // 에디터 모드 테스트에서는 Destroy 가 허용되지 않는다.
        if (Application.isPlaying)
        {
            Destroy(mesh);
        }
        else
        {
            DestroyImmediate(mesh);
        }
    }

    private void UpdateLockIcon(bool show)
    {
        if (lockIcon == null)
        {
            return;
        }

        if (_lockIconTransform == null)
        {
            if (!show)
            {
                return;
            }

            // 표시용 막대의 부모는 비균일 스케일이라 아이콘이 찌그러진다. 씬 루트에 따로 둔다.
            // 격벽의 자식으로도 두지 않는다(Awake 의 자식 Renderer 자동 탐색에 섞이지 않게).
            GameObject icon = new GameObject("GateLockIcon_" + gameObject.name);
            icon.layer = minimapWallRenderer.gameObject.layer;

            SpriteRenderer spriteRenderer = icon.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = lockIcon;
            spriteRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            spriteRenderer.receiveShadows = false;
            spriteRenderer.sortingOrder = 5;

            float spriteWidth = Mathf.Max(0.01f, lockIcon.bounds.size.x);
            icon.transform.localScale = Vector3.one * (lockIconSize / spriteWidth);

            _lockIconTransform = icon.transform;
        }

        if (show)
        {
            Bounds bounds = minimapWallRenderer.bounds;
            _lockIconTransform.position = new Vector3(bounds.center.x, bounds.max.y + 0.05f, bounds.center.z);
            _lockIconTransform.rotation = Quaternion.Euler(90f, 0f, 0f); // 위에서 내려다보는 미니맵 카메라를 향한다
        }

        _lockIconTransform.gameObject.SetActive(show);
    }

    private void OnDestroy()
    {
        if (_lockIconTransform != null)
        {
            Destroy(_lockIconTransform.gameObject);
        }

        if (_minimapPillMesh != null)
        {
            DestroyMesh(_minimapPillMesh);
        }

        if (_indicatorRoot != null)
        {
            Destroy(_indicatorRoot.gameObject);
        }
    }
}
