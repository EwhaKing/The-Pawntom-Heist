using Fusion;
using UnityEngine;

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

    [Header("Minimap / CCTV Visual")]
    [Tooltip("CCTV/미니맵에 보이는 격벽 표시용 Renderer")]
    [SerializeField] private Renderer minimapWallRenderer;

    [Header("Minimap Colors")]
    [SerializeField] private Color minimapClosedColor = new Color(1f, 0.15f, 0.1f, 1f);
    [SerializeField] private Color minimapOpenColor = new Color(0.1f, 0.55f, 1f, 1f);

    [Networked, OnChangedRender(nameof(OnWallStateChanged))]
    public NetworkBool IsUnlocked { get; private set; }

    [Networked, OnChangedRender(nameof(OnWallStateChanged))]
    public NetworkBool IsOpen { get; private set; }

    /// <summary>상태가 바뀔 때마다(원격 포함) 호출된다. UI가 구독해 새로고침한다.</summary>
    public event System.Action OnStateChanged;

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

        // 미니맵/CCTV 표시용 벽만 색상 변경
        if (minimapWallRenderer != null)
        {
            minimapWallRenderer.material.color = IsOpen ? minimapOpenColor : minimapClosedColor;
        }
    }
}