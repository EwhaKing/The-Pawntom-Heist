using Hacking;
using Pawntom.Stage;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 게임 금고의 최종 해킹 흐름. GAME_DESIGN §3.8 부분 개방 — 열쇠로 잠금을 모두 풀면(<see cref="VaultChest.IsUnlocked"/>)
/// 상호작용으로 미니게임(§3.5 공통 호출 인터페이스 <see cref="HackingManager"/>, 보안 등급 금고)을 띄우고,
/// 성공하면 <see cref="VaultChest.RequestOpenAfterMinigame"/> 로 개봉을 <b>요청</b>한다.
/// <para>
/// 금고가 열렸는지는 상태 권한이 쓴 <see cref="VaultChest.IsOpened"/> 로만 판단한다. 개봉 RPC 가 거부되면(거리 초과 등)
/// 열림으로 처리하지 않고 <see cref="LocalOpenRejected"/> 를 알린다(fusion-network.md 과거 결함). 규칙은 <see cref="VaultHackSession"/>.
/// 실패 시 3초 쿨타임(§3.5). 사망으로 취소되면 쿨타임·실패 처리를 하지 않는다.
/// </para>
/// <para>
/// 사용: 게임 금고(VaultChest, Open By Minigame 켬)와 같은 GameObject 에 붙인다. PlayerInteraction 이 이 컴포넌트가 있는 금고만
/// 해킹으로 보낸다 — 튜토리얼 금고(TutorialVault)는 이것을 붙이지 않으므로 기존 흐름 그대로다.
/// </para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(VaultChest))]
[AddComponentMenu("Pawntom/Gameplay/Vault Hack Flow")]
public sealed class VaultHackFlow : MonoBehaviour
{
    [Header("상호작용")]
    [Tooltip("잠금이 모두 풀린 금고 앞에서 \"E | 이름\" 으로 보일 이름")]
    [SerializeField] private string _hackName = "금고 해킹";

    [Header("해킹")]
    [SerializeField] private SecurityLevel _securityLevel = SecurityLevel.VaultFinal;

    [Tooltip("HackingManager 풀에 위 등급이 없으면 Normal 로 대신 띄운다(현재 Map_Scene 풀은 Normal 만 있다)")]
    [SerializeField] private bool _fallbackToNormal = true;

    [Tooltip("GAME_DESIGN §3.5 실패 시 3초 쿨타임")]
    [SerializeField, Min(0f)] private float _failCooldownSeconds = 3f;

    [Tooltip("미니게임 성공 뒤 이 시간(초) 안에 금고가 열리지 않으면 거부된 것으로 본다")]
    [SerializeField, Min(0.2f)] private float _openResultTimeoutSeconds = 2f;

    [Header("이벤트 (씬에서 연결, 이 피어에서만)")]
    [Tooltip("해킹 실패(사망 취소 제외). 현장 경보음 등을 연결한다")]
    [SerializeField] private UnityEvent _onHackFailed = new UnityEvent();

    [Tooltip("해킹은 성공했지만 상태 권한이 개봉을 거부했다")]
    [SerializeField] private UnityEvent _onOpenRejected = new UnityEvent();

    /// <summary>내 플레이어가 이 금고의 해킹 팝업을 띄웠다.</summary>
    public static event System.Action<VaultHackFlow> LocalHackStarted;

    /// <summary>내 해킹이 끝났다(성공 여부). 성공은 개봉 요청을 보냈다는 뜻이지 아직 열림이 아니다.</summary>
    public static event System.Action<VaultHackFlow, bool> LocalHackFinished;

    /// <summary>상태 권한이 금고를 열었음을 이 피어에서 확인했다(내가 해킹한 경우).</summary>
    public static event System.Action<VaultHackFlow> LocalOpenConfirmed;

    /// <summary>해킹은 성공했지만 제한 시간 안에 금고가 열리지 않았다(요청 거부).</summary>
    public static event System.Action<VaultHackFlow> LocalOpenRejected;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        LocalHackStarted = null;
        LocalHackFinished = null;
        LocalOpenConfirmed = null;
        LocalOpenRejected = null;
    }

    private VaultChest _chest;
    private HackingManager _hacking;
    private VaultHackSession _session;
    private Fusion.NetworkObject _hackingPlayer;

    public VaultChest Chest
    {
        get { return _chest; }
    }

    private bool ChestReady
    {
        get { return _chest != null && _chest.Object != null && _chest.Object.IsValid; }
    }

    /// <summary>열쇠가 모두 들어가 이제 해킹으로 열 차례인가(PlayerInteraction 이 E 를 해킹으로 보낼지 정한다).</summary>
    public bool WantsHack
    {
        get { return ChestReady && _chest.OpenByMinigame && !_chest.IsOpened && _chest.IsUnlocked; }
    }

    public VaultHackGate Gate
    {
        get
        {
            if (!ChestReady)
            {
                return VaultHackGate.Locked;
            }

            return _session.Evaluate(Time.unscaledTime, _chest.IsOpened, _chest.IsUnlocked, HackingManager.AnyHackingActive);
        }
    }

    private void Awake()
    {
        _chest = GetComponent<VaultChest>();
        _session = new VaultHackSession(_failCooldownSeconds, _openResultTimeoutSeconds);
    }

    private void Update()
    {
        if (!_session.IsAwaitingOpen || !ChestReady)
        {
            return;
        }

        // 개봉은 상태 권한의 네트워크 상태로만 확인한다(로컬 시간은 결과 대기 제한에만 쓴다).
        switch (_session.Poll(Time.unscaledTime, _chest.IsOpened))
        {
            case VaultHackPoll.Opened:
                _hackingPlayer = null;
                LocalOpenConfirmed?.Invoke(this);
                break;

            case VaultHackPoll.Rejected:
                _hackingPlayer = null;
                Debug.LogWarning("[VaultHackFlow] 해킹은 성공했지만 금고가 개봉 요청을 거부했습니다(거리·권한). 다시 시도하세요.", this);
                LocalOpenRejected?.Invoke(this);
                _onOpenRejected.Invoke();
                break;
        }
    }

    /// <summary>
    /// 겨눴을 때 띄울 안내. 잠김이면 열쇠 진행도, 풀렸으면 해킹, 쿨타임·대기 중이면 그 상태. 열렸으면 null.
    /// </summary>
    public string GetPromptName(bool hasSelectedKey)
    {
        if (!ChestReady || _chest.IsOpened)
        {
            return null;
        }

        switch (Gate)
        {
            case VaultHackGate.Locked:
                return hasSelectedKey
                    ? $"열쇠 사용 ({_chest.UsedKeyCount}/{_chest.RequiredKeyCount})"
                    : $"잠김 — 열쇠 필요 ({_chest.UsedKeyCount}/{_chest.RequiredKeyCount})";

            case VaultHackGate.CoolingDown:
                return "해킹 재시도 대기";

            case VaultHackGate.AwaitingOpen:
                return "금고 여는 중…";

            case VaultHackGate.HackInProgress:
                return null;

            default:
                return _hackName;
        }
    }

    /// <summary>
    /// 로컬 플레이어가 잠금이 풀린 금고에서 상호작용했다. 해킹 팝업을 띄웠으면 true.
    /// </summary>
    public bool TryStartHack(Fusion.NetworkObject localPlayer)
    {
        if (!ChestReady || localPlayer == null || !localPlayer.IsValid || !localPlayer.HasInputAuthority)
        {
            return false;
        }

        if (!_session.TryBegin(Time.unscaledTime, _chest.IsOpened, _chest.IsUnlocked, HackingManager.AnyHackingActive))
        {
            return false;
        }

        if (_hacking == null)
        {
            _hacking = FindFirstObjectByType<HackingManager>();
        }

        if (_hacking == null)
        {
            Debug.LogWarning("[VaultHackFlow] HackingManager 가 씬에 없어 금고 해킹을 띄울 수 없습니다.", this);
            _session.CancelBegin();
            return false;
        }

        _hackingPlayer = localPlayer;
        _hacking.OpenHackingPopup(_securityLevel, HandleHackFinished);

        if (!_hacking.IsHackingActive && _fallbackToNormal && _securityLevel != SecurityLevel.Normal)
        {
            Debug.LogWarning($"[VaultHackFlow] 해킹 풀에 {_securityLevel} 등급이 없어 Normal 등급으로 띄웁니다.", this);
            _hacking.OpenHackingPopup(SecurityLevel.Normal, HandleHackFinished);
        }

        if (!_hacking.IsHackingActive)
        {
            _hackingPlayer = null;
            _session.CancelBegin();
            return false;
        }

        LocalHackStarted?.Invoke(this);
        return true;
    }

    private void HandleHackFinished(bool success)
    {
        bool canceled = !success && HackingManager.IsCancellingForPlayerDeath;
        _session.Finish(success, canceled, Time.unscaledTime);
        LocalHackFinished?.Invoke(this, success);

        if (!success)
        {
            _hackingPlayer = null;

            if (!canceled)
            {
                _onHackFailed.Invoke();
            }

            return;
        }

        if (!ChestReady || _hackingPlayer == null || !_hackingPlayer.IsValid)
        {
            // 요청을 보내지 못했다. 다음 Poll 이 제한 시간 뒤 거부로 알린다.
            return;
        }

        // 개봉은 상태 권한이 다시 검증한다(소유·거리·잠금). 여기서는 요청만 보낸다.
        _chest.RequestOpenAfterMinigame(_hackingPlayer, true);
    }
}
