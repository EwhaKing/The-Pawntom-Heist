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
