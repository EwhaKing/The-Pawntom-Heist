using Fusion;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// EscapeButtonInteraction
///
/// 담당:
/// - 본부 출발 버튼 상호작용 처리
/// </summary>
public class EscapeButtonInteraction : NetworkBehaviour
{
    [Header("상호작용 안내")]
    [Tooltip("가까이 가서 겨눴을 때 \"E | 이름\" 으로 보일 이름")]
    [SerializeField] private string interactName = "출발";

    // 거리는 InteractRange.StandardMargin 한 값으로 통일했다(모든 E 상호작용 대상 공통).
    private static float interactMargin
    {
        get { return InteractRange.StandardMargin; }
    }

    public string InteractName
    {
        get { return interactName; }
    }

    /// <summary>플레이어가 버튼 앞 상호작용 범위 안에 있는가.</summary>
    public bool IsPlayerInRange(Vector3 playerPosition)
    {
        return InteractRange.IsInRange(this, playerPosition, interactMargin);
    }

    /// <summary>
    /// 플레이어가 출발 버튼과 상호작용했을 때 호출
    /// </summary>
    public void TryInteract()
    {
        Debug.Log("[EscapeButtonInteraction] 출발 버튼 상호작용");

        if (RuleManager.Instance == null)
        {
            Debug.LogError("[EscapeButtonInteraction] EscapeRuleManager.Instance가 없습니다.");
            return;
        }

        RuleManager.Instance.RequestDeparture();
    }
}
