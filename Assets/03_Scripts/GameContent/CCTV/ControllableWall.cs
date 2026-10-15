using Fusion;
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
/// - 비권위 피어의 호출은 RPC로 StateAuthority에 요청되고, 실제 반영은 StateAuthority에서만 일어난다
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

    [Header("Minimap Colors")]
    [SerializeField] private Color minimapClosedColor = new Color(1f, 0.15f, 0.1f, 1f);
    [SerializeField] private Color minimapOpenColor = new Color(0.1f, 0.55f, 1f, 1f);

    [Tooltip("해금됐지만 닫혀 있을 때(UI_SPEC: 열림·해금 = 푸른색)")]
    [SerializeField] private Color minimapUnlockedColor = new Color(0.1f, 0.45f, 0.9f, 1f);

    [Header("Field Open (현장 상호작용)")]
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
    /// 현장(침투조) 상호작용. 해금된(푸른) 격벽만 열리고, <see cref="fieldOpenSeconds"/> 뒤 자동으로 닫힌다.
    /// 잠긴(붉은) 격벽이면 false — 본부 해킹으로 먼저 해금해야 한다.
    /// </summary>
    public bool TryFieldOpen()
    {
        if (!IsUnlocked)
        {
            Debug.Log($"[ControllableWall] {gameObject.name} 잠김 — 접근 거부. 본부에서 해킹으로 해금해야 합니다.");
            return false;
        }

        if (IsOpen)
        {
            return false;
        }

        if (Object.HasStateAuthority)
        {
            DoFieldOpen();
        }
        else
        {
            RPC_RequestFieldOpen();
        }

        return true;
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestFieldOpen()
    {
        DoFieldOpen();
    }

    private void DoFieldOpen()
    {
        if (!IsUnlocked || IsOpen)
        {
            return;
        }

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
        ApplyWallState();
    }

    /// <summary>
    /// 격벽을 해금 상태로 변경
    /// </summary>
    public void Unlock()
    {
        if (Object.HasStateAuthority)
        {
            DoUnlock();
        }
        else
        {
            RPC_RequestUnlock();
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestUnlock()
    {
        DoUnlock();
    }

    private void DoUnlock()
    {
        IsUnlocked = true;
        Debug.Log($"[ControllableWall] {gameObject.name} 해금 완료");
    }

    /// <summary>
    /// 격벽을 엽니다.
    /// 실제 벽은 사라지고, 미니맵/CCTV 표시는 파란색
    /// </summary>
    public void OpenWall()
    {
        RequestSetOpen(true);
    }

    /// <summary>
    /// 격벽을 닫습니다.
    /// 실제 벽은 다시 보이고, 미니맵/CCTV 표시는 빨간색
    /// </summary>
    public void CloseWall()
    {
        RequestSetOpen(false);
    }

    private void RequestSetOpen(bool open)
    {
        if (Object.HasStateAuthority)
        {
            DoSetOpen(open);
        }
        else
        {
            RPC_RequestSetOpen(open);
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestSetOpen(NetworkBool open)
    {
        DoSetOpen(open);
    }

    private void DoSetOpen(bool open)
    {
        // 본부에서 직접 열고 닫는 것은 수동 조작이다 — 현장 개방의 자동 닫힘 예약을 지운다.
        AutoCloseTimer = TickTimer.None;
        IsOpen = open;
        Debug.Log($"[ControllableWall] {gameObject.name} {(open ? "열림" : "닫힘")}");
    }

    /// <summary>
    /// 해금된 격벽의 열림/닫힘을 전환
    /// </summary>
    public void ToggleWall()
    {
        if (!IsUnlocked)
        {
            Debug.Log($"[ControllableWall] {gameObject.name} 아직 해금되지 않음");
            return;
        }

        RequestSetOpen(!IsOpen);
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

        // 미니맵/CCTV 표시용 벽만 색상 변경
        if (minimapWallRenderer != null)
        {
            // 잠김 = 빨강, 해금(닫힘) = 파랑, 열림 = 밝은 파랑 (UI_SPEC 색 의미 규칙)
            minimapWallRenderer.material.color = !IsUnlocked
                ? minimapClosedColor
                : (IsOpen ? minimapOpenColor : minimapUnlockedColor);
        }
    }
}