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

    public string InteractName
    {
        get { return interactName; }
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
