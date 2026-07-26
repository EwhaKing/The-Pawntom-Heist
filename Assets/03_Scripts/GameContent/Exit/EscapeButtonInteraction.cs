using Fusion;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// EscapeButtonInteraction
///
/// 담당:
/// - 본부 출발 버튼 상호작용 처리
/// - 팀원 중 전설의 캣닢을 가진 사람이 있는지 확인
/// - 조건 만족 시 레벨 클리어 처리
///
/// 클리어 조건:
/// 1. 본부 공간 안에 살아있는 플레이어가 최소 1명 이상 있음
/// 2. 팀원 중 최소 1명 이상이 전설의 캣닢을 소지 중
/// 3. 출발 버튼 상호작용 성공
/// </summary>
public class EscapeButtonInteraction : NetworkBehaviour
{
    [Header("Required")]
    [SerializeField] private HeadquartersZone headquartersZone;

    [Header("Item")]
    [SerializeField] private int ExitItemId = 1;

    /// <summary>
    /// 출발 버튼 상호작용 요청
    /// PlayerInteraction에서 버튼을 감지했을 때 호출
    /// </summary>
    public void TryInteract()
    {
        if (Object.HasStateAuthority)
            TryEscape();
        else
            RPC_RequestEscape();
    }

    /// <summary>
    /// 클라이언트가 출발 버튼을 눌렀을 때
    /// StateAuthority에게 탈출 판정을 요청
    /// </summary>
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestEscape()
    {
        TryEscape();
    }

    /// <summary>
    /// 실제 탈출 조건 판정
    /// </summary>
    private void TryEscape()
    {
        if (ExitItemId <= InventoryManager.EmptySlot)
        {
            Debug.LogError("[EscapeButtonInteraction] Exit Item Id가 잘못 설정되었습니다.");
            return;
        }
        if (headquartersZone == null)
        {
            Debug.LogError("[EscapeButtonInteraction] HeadquartersZone이 연결되지 않았습니다.");
            return;
        }

        bool hasPlayerHeadquarters = headquartersZone.HasAnyAlivePlayerInZone();
        bool hasCatnip = InventoryManager.HasAnyPlayerItem(ExitItemId);

        Debug.Log($"[EscapeButtonInteraction] 본부 플레이어 여부 : {hasPlayerHeadquarters}");
        Debug.Log($"[EscapeButtonInteraction] 전설의 캣닢 보유 여부 ItemId={ExitItemId}: {hasCatnip}");

        if (!hasPlayerHeadquarters)
        {
            Debug.Log("[EscapeButtonInteraction] 본부 안에 살아있는 플레이어가 없어 출발할 수 없습니다.");
            return;
        }

        if (!hasCatnip)
        {
            Debug.Log("[EscapeButtonInteraction] 전설의 캣닢을 소지한 플레이어가 없어 출발할 수 없습니다.");
            return;
        }

        Debug.Log("[EscapeButtonInteraction] 조건 만족. 레벨 클리어!");

        if (GameManager.Instance != null)
            GameManager.Instance.EscapeSuccess();
        else
            Debug.LogError("[EscapeButtonInteraction] GameManager.Instance가 없습니다.");
    }
}
