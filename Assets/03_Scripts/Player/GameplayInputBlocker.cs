using UnityEngine;

/// <summary>
/// GameplayInputBlocker
///
/// 담당:
/// - UI가 열려 있을 때 플레이어 이동/시점/상호작용 입력을 막기 위한 전역 플래그
///
/// 사용 예:
/// - MainbaseControlUI가 열리면 SetBlocked(true)
/// - MainbaseControlUI가 닫히면 SetBlocked(false)
/// - PlayerController, CameraController, PlayerInteraction, InputManager에서 IsBlocked를 확인
/// </summary>
public static class GameplayInputBlocker
{
    public static bool IsBlocked { get; private set; }

    public static void SetBlocked(bool isBlocked)
    {
        IsBlocked = isBlocked;

        if (isBlocked)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            Debug.Log("[GameplayInputBlocker] 게임 입력 차단 / 마우스 UI 모드");
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            Debug.Log("[GameplayInputBlocker] 게임 입력 복구 / 마우스 잠금");
        }
    }
}
