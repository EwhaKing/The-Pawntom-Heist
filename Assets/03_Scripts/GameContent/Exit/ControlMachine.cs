using UnityEngine;

/// <summary>
/// ControlMachine
///
/// 담당:
/// - Mainbase 안의 조작 기계 상호작용
/// - 플레이어가 E키로 상호작용하면 MainbaseControlUI를 엶
///
/// 사용 위치:
/// - MainbaseControlMachine 오브젝트에 붙임
/// </summary>
public class ControlMachine : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private MainbaseControlUI controlUI;

    private void Awake()
    {
        if (controlUI == null)
        {
            controlUI = FindFirstObjectByType<MainbaseControlUI>();
        }
    }

    public void TryInteract()
    {
        Debug.Log("[ControlMachine] 조작 기계 상호작용");

        if (controlUI == null)
        {
            controlUI = FindFirstObjectByType<MainbaseControlUI>();
        }

        if (controlUI == null)
        {
            Debug.LogWarning("[ControlMachine] MainbaseControlUI를 찾지 못했습니다.");
            return;
        }

        controlUI.Open();
    }
}