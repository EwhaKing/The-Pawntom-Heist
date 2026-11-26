using Fusion;
using Pawntom.Network;
using UnityEngine;

/// <summary>
/// ControllableLight
///
/// 담당:
/// - 실제 맵의 전등 ON/OFF 상태 관리
/// - 해킹 전에는 조작 불가
/// - 해킹 성공 시 해금 + 소등, <see cref="autoRelightSeconds"/> 뒤 자동 점등(GAME_DESIGN §3.4 전등)
/// - 해금 후에는 본부에서 켜고 끌 수 있음
///
/// 네트워크:
/// - 해금·소등·재점등 타이머는 [Networked] — 상태 권한(호스트)만 쓴다. 늦게 들어온 참가자도 같은 상태를 받는다.
/// - 공개 조작 메서드(Unlock/UnlockAndTurnOff/TurnOn/TurnOff/ToggleLight)는 "요청"이다. 클라이언트는 RPC 로 보내고,
///   상태 권한이 요청자(RpcInfo.Source)의 생존·단말기 위치·구역 발견·상태 전이·쿨다운을 다시 판정한다(<see cref="LightCommandRule"/>).
///   거부되면 요청자에게만 <see cref="LocalRequestRejected"/> 로 알린다. 호출 직후 상태가 바뀌었다고 가정하지 않는다.
/// - 조명·램프 색·어둠 오브젝트는 상태 변경 감지(OnChangedRender)와 Spawned 에서 모든 피어가 반영한다.
///
/// 사용 위치:
/// - 실제 맵의 RoomLight_01 같은 Empty Object에 붙임 (같은 오브젝트에 NetworkObject 필요)
///
/// 주의:
/// - 구역 발견 여부는 여기서 관리하지 않음 — DiscoverableArea 가 담당
/// </summary>
public class ControllableLight : NetworkBehaviour
{
    [Header("State (시작 값 — 세션 중 실제 값은 상태 권한이 동기화)")]
    [SerializeField] private bool isUnlocked;
    [SerializeField] private bool isOn = true;

    [Header("Rule")]
    [Tooltip("소등 후 자동으로 다시 켜질 때까지의 시간(초). GAME_DESIGN §3.4: 5초. 0 이면 자동 점등하지 않음")]
    [SerializeField, Min(0f)] private float autoRelightSeconds = 5f;

    [Tooltip("이 전등이 속한 구역. 연결하면 상태 권한이 '발견된 구역인지'도 검증한다. 비우면 검사하지 않음")]
    [SerializeField] private DiscoverableArea area;

    [Header("Actual Lights")]
    [Tooltip("이 구역에서 실제로 켜고 끌 Light 컴포넌트들")]
    [SerializeField] private Light[] targetLights;

    [Header("Optional Visual")]
    [Tooltip("전등 모델 색을 바꾸고 싶을 때만 연결합니다. 색 변경이 싫으면 비워둬도 됩니다.")]
    [SerializeField] private Renderer[] lampRenderers;

    [Header("Optional Area Overlay")]
    [Tooltip("전등이 꺼졌을 때 실제 월드에 어두운 오브젝트를 켜고 싶으면 연결합니다.")]
    [SerializeField] private GameObject darknessObject;

    [Header("Visual Colors")]
    [SerializeField] private Color lampOnColor = Color.white;
    [SerializeField] private Color lampOffColor = Color.gray;

    [Networked, OnChangedRender(nameof(OnLightStateChanged))]
    private NetworkBool NetUnlocked { get; set; }

    // 기본값(false)이 "켜짐"이 되도록 꺼짐을 저장한다.
    [Networked, OnChangedRender(nameof(OnLightStateChanged))]
    private NetworkBool NetOff { get; set; }

    [Networked] private TickTimer RelightTimer { get; set; }

    /// <summary>상태가 이 피어에 반영될 때(원격 포함).</summary>
    public event System.Action<ControllableLight> StateChanged;

    /// <summary>내(이 피어의) 요청이 상태 권한에서 거부됐다.</summary>
    public static event System.Action<ControllableLight, LightCommand, RequestVerdict> LocalRequestRejected;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        LocalRequestRejected = null;
    }

    private RequestCooldown _cooldown;

    private bool IsNetworkReady => Object != null && Object.IsValid;

    public bool IsUnlocked => IsNetworkReady ? (bool)NetUnlocked : isUnlocked;
    public bool IsOn => IsNetworkReady ? !(bool)NetOff : isOn;

    private void Awake()
    {
        if (targetLights == null || targetLights.Length == 0)
        {
            targetLights = GetComponentsInChildren<Light>(true);
        }

        ApplyLightState();
    }

    public override void Spawned()
    {
        _cooldown = new RequestCooldown(HostRequesters.SecondsToTicks(Runner, HostRequesters.DefaultCooldownSeconds));

        if (Object.HasStateAuthority)
        {
            // 인스펙터의 시작 값을 네트워크 상태로 옮긴다(호스트 1회).
            NetUnlocked = isUnlocked;
            NetOff = !isOn;
            RelightTimer = TickTimer.None;
        }

        ApplyLightState();
        StateChanged?.Invoke(this);
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        _cooldown?.Clear();
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority || !NetOff || !RelightTimer.Expired(Runner))
        {
            return;
        }

        RelightTimer = TickTimer.None;
        NetOff = false;
        Debug.Log($"[ControllableLight] 자동 점등: {gameObject.name}");
    }

    // ───────── 요청 (모든 피어에서 호출 가능) ─────────

    /// <summary>전등 조작을 해금(요청).</summary>
    public void Unlock()
    {
        Request(LightCommand.Unlock);
    }

    /// <summary>미니게임 성공: 해금과 소등을 한 요청으로 보낸다.</summary>
    public void UnlockAndTurnOff()
    {
        Request(LightCommand.UnlockAndTurnOff);
    }

    /// <summary>전등 ON(요청)</summary>
    public void TurnOn()
    {
        Request(LightCommand.TurnOn);
    }

    /// <summary>전등 OFF(요청). 꺼지면 자동 점등 타이머가 걸린다.</summary>
    public void TurnOff()
    {
        Request(LightCommand.TurnOff);
    }

    /// <summary>
    /// 해금된 전등의 ON/OFF 전환(요청). 지금 보이는 상태의 반대를 명시적으로 요청한다 —
    /// 상태가 이미 바뀌어 있으면 상태 권한이 중복으로 거부한다(엇갈린 두 번의 토글이 원래대로 되돌리지 않게).
    /// </summary>
    public void ToggleLight()
    {
        if (!IsUnlocked)
        {
            Debug.Log($"[ControllableLight] 아직 해금되지 않은 전등입니다: {gameObject.name}");
            return;
        }

        Request(IsOn ? LightCommand.TurnOff : LightCommand.TurnOn);
    }

    private void Request(LightCommand command)
    {
        if (!IsNetworkReady)
        {
            ApplyOffline(command);
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
        HandleRequest(info.Source, (LightCommand)command);
    }

    // ───────── 상태 권한 ─────────

    private void HandleRequest(PlayerRef source, LightCommand command)
    {
        if (!Object.HasStateAuthority)
        {
            return;
        }

        int tick = Runner.Tick.Raw;
        RequesterFacts requester = HostRequesters.ForTerminal(source, _cooldown, tick);
        bool discovered = area == null || area.IsDiscovered;
        RequestVerdict verdict = LightCommandRule.Evaluate(command, NetUnlocked, !(bool)NetOff, discovered, requester);

        if (verdict != RequestVerdict.Accepted)
        {
            Reject(source, command, verdict);
            return;
        }

        _cooldown.Record(HostRequesters.Key(source), tick);
        Apply(command);
    }

    private void Apply(LightCommand command)
    {
        switch (command)
        {
            case LightCommand.Unlock:
                NetUnlocked = true;
                Debug.Log($"[ControllableLight] 전등 해금 완료: {gameObject.name}");
                break;

            case LightCommand.UnlockAndTurnOff:
                NetUnlocked = true;
                SetOffOnAuthority();
                break;

            case LightCommand.TurnOff:
                SetOffOnAuthority();
                break;

            case LightCommand.TurnOn:
                RelightTimer = TickTimer.None;
                NetOff = false;
                Debug.Log($"[ControllableLight] 전등 켜짐: {gameObject.name}");
                break;
        }
    }

    private void SetOffOnAuthority()
    {
        NetOff = true;
        RelightTimer = autoRelightSeconds > 0f
            ? TickTimer.CreateFromSeconds(Runner, autoRelightSeconds)
            : TickTimer.None;
        Debug.Log($"[ControllableLight] 전등 꺼짐: {gameObject.name} (자동 점등 {autoRelightSeconds}초)");
    }

    private void Reject(PlayerRef source, LightCommand command, RequestVerdict verdict)
    {
        Debug.Log($"[ControllableLight] {gameObject.name} {command} 요청 거부: {verdict}");

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
        Debug.Log($"[ControllableLight] {gameObject.name} 내 요청 거부됨: {(LightCommand)command} / {(RequestVerdict)verdict}");
        LocalRequestRejected?.Invoke(this, (LightCommand)command, (RequestVerdict)verdict);
    }

    // ───────── 보여주기 (모든 피어) ─────────

    private void OnLightStateChanged()
    {
        ApplyLightState();
        StateChanged?.Invoke(this);
    }

    // NetworkObject 가 없는 씬(전환기)·세션 없는 미리보기에서는 예전처럼 로컬로 바로 반영한다(동기화 없음).
    private void ApplyOffline(LightCommand command)
    {
        switch (command)
        {
            case LightCommand.Unlock:
                isUnlocked = true;
                break;

            case LightCommand.UnlockAndTurnOff:
                isUnlocked = true;
                isOn = false;
                break;

            case LightCommand.TurnOff:
                if (isUnlocked) isOn = false;
                break;

            case LightCommand.TurnOn:
                if (isUnlocked) isOn = true;
                break;
        }

        ApplyLightState();
        StateChanged?.Invoke(this);
    }

    /// <summary>
    /// 현재 상태를 실제 Light와 선택적 시각 요소에 반영
    /// </summary>
    private void ApplyLightState()
    {
        bool on = IsOn;

        if (targetLights != null)
        {
            for (int i = 0; i < targetLights.Length; i++)
            {
                if (targetLights[i] == null)
                {
                    continue;
                }

                targetLights[i].enabled = on;
            }
        }

        // 실제 전등 모델 색을 바꾸고 싶을 때만 사용
        if (lampRenderers != null)
        {
            for (int i = 0; i < lampRenderers.Length; i++)
            {
                if (lampRenderers[i] == null)
                {
                    continue;
                }

                lampRenderers[i].material.color = on ? lampOnColor : lampOffColor;
            }
        }

        // 전등이 꺼졌을 때 월드에 어두운 영역 표시용 오브젝트
        if (darknessObject != null)
        {
            darknessObject.SetActive(!on);
        }
    }

    [ContextMenu("TEST Toggle Light")]
    private void TestToggleLight()
    {
        if (IsNetworkReady)
        {
            // 테스트 단축: 상태 권한에서만, 검증 없이 바로 전환.
            if (!Object.HasStateAuthority) return;
            NetUnlocked = true;
            if (NetOff) Apply(LightCommand.TurnOn); else SetOffOnAuthority();
            return;
        }

        isUnlocked = true;
        ApplyOffline(isOn ? LightCommand.TurnOff : LightCommand.TurnOn);
    }
}
