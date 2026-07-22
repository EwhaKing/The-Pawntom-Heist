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
    /// 현재 입력을 수집하여 Fusion 입력 데이터로 반환합니다.
    /// </summary>
    public NetworkInputData GetNetworkInput()
    {
        NetworkInputData data = new NetworkInputData();

        if (!_isGameplayInputEnabled || _inputActions == null)
        {
            return data;
        }

        data.Move = _inputActions.Player.Move.ReadValue<Vector2>();
        data.Look = _inputActions.Player.Look.ReadValue<Vector2>();
        data.Buttons.Set((int)InputButton.Jump, _inputActions.Player.Jump.IsPressed());
        data.Buttons.Set((int)InputButton.Sprint, _inputActions.Player.Sprint.IsPressed());
        
        // 아이템 상호 작용 키
        data.Buttons.Set((int)InputButton.Interact, _inputActions.Player.Interact.IsPressed());
        // data.Buttons.Set((int)InputButton.UseItem, _inputActions.Player.UseItem.IsPressed());
        // data.Buttons.Set((int)InputButton.DropItem, _inputActions.Player.DropItem.IsPressed());

        return data;
    }
}

