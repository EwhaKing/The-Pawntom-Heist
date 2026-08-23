using Fusion;
using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 로컬 플레이어의 입력을 수집하여
/// Fusion에 전달할 NetworkInputData를 생성합니다.
///
/// 현재 구현 기능:
/// - WASD 이동
/// - 달리기
/// - 점프(감도 수정 필요)
/// *싱글톤으로 만들어졌기 때문에 게임 내내 살아있습니다. 맵 씬에서 입력을 받는 경우 / 그 외 씬들에서 입력을 받는 경우를 구분해두어야 합니다.
///
/// TODO:
/// - 아이템과의 상호작용(획득 및 사용)
/// </summary>
public class InputManager : PawntomSingleton<InputManager>
{
    private InputSystemActions _inputActions;
    private bool _isGameplayInputEnabled;

    protected override void Awake()
    {
        base.Awake();

        _inputActions = new InputSystemActions();
    }

    /// <summary>
    /// 이동, 시점 회전 등 게임 플레이 입력을 활성화합니다.
    /// </summary>
    public void EnableGameplayInput()
    {
        _inputActions.Player.Enable();
        _isGameplayInputEnabled = true;
    }

    /// <summary>
    /// 게임 플레이 입력을 비활성화합니다.
    /// </summary>
    public void DisableGameplayInput()
    {
        _inputActions.Player.Disable();
        _isGameplayInputEnabled = false;
    }

    /// <summary>
    /// 현재 입력을 수집하여 Fusion 입력 데이터로 반환
    /// </summary>
    public NetworkInputData GetNetworkInput()
    {
        NetworkInputData data = new NetworkInputData();

        // 기본값 초기화
        // -1 = 숫자키 슬롯 선택 입력 없음
        data.SelectedSlotIndex = -1;

        // 0 = 마우스 휠 입력 없음
        data.SlotScrollDirection = 0;

        // 입력 시스템이 꺼져 있거나 InputActions가 없으면 빈 입력 반환
        if (!_isGameplayInputEnabled || _inputActions == null)
        {
            return data;
        }

        // CCTV / MainbaseControlUI / 해킹 팝업 등 UI 조작 중이면
        // 이 클라이언트의 플레이어 입력만 막음
        if (GameplayInputBlocker.IsBlocked)
        {
            data.Move = Vector2.zero;
            data.Look = Vector2.zero;

            // 버튼 입력도 모두 보내지 않음
            // Jump / Sprint / Interact / 슬롯 선택 / 휠 입력 전부 차단
            data.SelectedSlotIndex = -1;
            data.SlotScrollDirection = 0;

            return data;
        }

        data.Move = _inputActions.Player.Move.ReadValue<Vector2>();
        data.Look = _inputActions.Player.Look.ReadValue<Vector2>();
        
        data.Buttons.Set((int)InputButton.Jump, _inputActions.Player.Jump.IsPressed());
        data.Buttons.Set((int)InputButton.Sprint, _inputActions.Player.Sprint.IsPressed());
        data.Buttons.Set((int)InputButton.Interact, _inputActions.Player.Interact.IsPressed());

        if (Keyboard.current != null)
        {
            if (Keyboard.current.digit1Key.wasPressedThisFrame || Keyboard.current.numpad1Key.wasPressedThisFrame)
            {
                data.SelectedSlotIndex = 0;
            }
            else if (Keyboard.current.digit2Key.wasPressedThisFrame || Keyboard.current.numpad2Key.wasPressedThisFrame)
            {
                data.SelectedSlotIndex = 1;
            }
            else if (Keyboard.current.digit3Key.wasPressedThisFrame || Keyboard.current.numpad3Key.wasPressedThisFrame)
            {
                data.SelectedSlotIndex = 2;
            }
            else if (Keyboard.current.digit4Key.wasPressedThisFrame || Keyboard.current.numpad4Key.wasPressedThisFrame)
            {
                data.SelectedSlotIndex = 3;
            }
        }

        if (Mouse.current != null)
        {
            float scrollY = Mouse.current.scroll.ReadValue().y;

            if (scrollY > 0f)
            {
                data.SlotScrollDirection = -1;
            }
            else if (scrollY < 0f)
            {
                data.SlotScrollDirection = 1;
            }
        }

        // 아이템 상호 작용 키
        data.Buttons.Set((int)InputButton.Interact, _inputActions.Player.Interact.IsPressed());
        // data.Buttons.Set((int)InputButton.UseItem, _inputActions.Player.UseItem.IsPressed());
        // data.Buttons.Set((int)InputButton.DropItem, _inputActions.Player.DropItem.IsPressed());

        return data;
    }
}

