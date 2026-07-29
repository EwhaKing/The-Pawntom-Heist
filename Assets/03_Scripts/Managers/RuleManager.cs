using Fusion;
using NUnit.Framework;
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
/// </summary>
public class RuleManager : NetworkBehaviour
{
    /// <summary>
    /// 씬에서 접근하기 위한 간단한 싱글톤 참조
    /// 단, 네트워크 상태 자체는 [Networked] 값으로 동기화됨
    /// </summary>
    public static RuleManager Instance { get; private set; }

    [Header("Required")]
    [SerializeField] private HeadquartersZone headquartersZone;

    [Header("Item")]
    [SerializeField] private int exitItemId = 1;

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

    private void Awake()
    {
        Instance = this;
    }

    /// <summary>
    /// Fusion에서 NetworkObject가 Spawn된 뒤 호출
    /// </summary>
    public override void Spawned()
    {
        if (!Object.HasStateAuthority)
        {
            return;
        }

        IsDepartureStarted = false;
        IsLevelCleared = false;
    }

    /// <summary>
    /// 출발 버튼을 눌렀을 때 호출
    /// 조건 만족 시 팀 전체 레벨 클리어를 시도
    /// </summary>
    public void RequestDeparture()
    {
        Debug.Log("[RuleManager] 출발 요청");

        // 실제 판정은 StateAuthority에서만 실행
        if (Object.HasStateAuthority)
        {
            TryStartDeparture();
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
    private void RPC_RequestDeparture()
    {
        TryStartDeparture();
    }

    /// <summary>
    /// 실제 출발 조건을 검사
    /// 성공 시 팀 전체 레벨 클리어로 처리
    /// </summary>
    private void TryStartDeparture()
    {
        // 중복 실행 방지
        if (IsLevelCleared)
        {
            Debug.Log("[RuleManager] 이미 레벨 클리어 처리됨");
            return;
        }

        if (exitItemId <= InventoryManager.EmptySlot)
        {
            Debug.LogError("[RuleManager] Exit Item Id가 잘못 설정되었습니다. 0은 빈 슬롯입니다.");
            return;
        }

        if (headquartersZone == null)
        {
            Debug.LogError("[RuleManager] HeadquartersZone이 연결되지 않았습니다.");
            return;
        }

        bool hasAlivePlayerInHeadquarters = headquartersZone.HasAnyAlivePlayerInZone();

        bool hasExit = InventoryManager.HasAnyPlayerItem(exitItemId);

        Debug.Log($"[RuleManager] 본부 안 플레이어 여부: {hasAlivePlayerInHeadquarters}");
        Debug.Log($"[RuleManager] 전설의 캣닢 보유 여부: {hasExit}");

        if (!hasAlivePlayerInHeadquarters)
        {
            Debug.Log("[RuleManager] 본부 안에 살아있는 플레이어가 없어 출발할 수 없습니다.");
            return;
        }

        if (!hasExit)
        {
            Debug.Log("[RuleManager] 전설의 캣닢이 없어 출발할 수 없습니다.");
            return;
        }

        IsDepartureStarted = true;
        IsLevelCleared = true;

        Debug.Log("[RuleManager] 출발 성공. 팀 전체 레벨 클리어!");

        RPC_OnLevelCleared();
    }

    /// <summary>
    /// 모든 클라이언트에서 레벨 클리어 결과를 처리
    /// 여기서 UI 출력, 결과 화면 이동, 상점 이동 등을 실행
    /// </summary>
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_OnLevelCleared()
    {
        Debug.Log("[RuleManager] 모든 클라이언트 레벨 클리어 처리");

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

        Debug.Log("[RuleManager] 탈출 규칙 초기화");
    }
}
