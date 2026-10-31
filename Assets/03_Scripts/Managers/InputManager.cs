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

    /// <summary>
    /// 게임 플레이 입력이 켜져 있는가. 입력 액션을 거치지 않고 키를 직접 읽는 곳(E 상호작용 등)이
    /// 연출 중 잠금(튜토리얼 대사·오프닝)을 따르도록 확인한다.
    /// </summary>
    public bool IsGameplayInputEnabled => _isGameplayInputEnabled;

    // 프레임 단위로 모아 두는 "한 번만" 입력. Fusion 은 입력을 틱마다(초당 64회) 걷어 가는데 프레임과 박자가 다르다 —
    // 수집 순간에만 wasPressedThisFrame 을 읽으면 틱이 없는 프레임의 눌림은 사라지고, 한 프레임에 틱이 두 번 돌면
    // 같은 눌림이 두 번 들어가 슬롯 선택이 켜졌다 바로 꺼졌다(토글). 그래서 Update 에서 모으고, 수집 때 한 번 내보내고 비운다.
    private int _pendingSlot = -1;
    private int _pendingScroll;
    private bool _pendingDrop;

    protected override void Awake()
    {
        base.Awake();

        _inputActions = new InputSystemActions();
    }

    private void Update()
    {
        if (!_isGameplayInputEnabled || GameplayInputBlocker.IsBlocked || Hacking.HackingManager.AnyHackingActive)
        {
            ClearPending();
            return;
        }

        Keyboard keyboard = Keyboard.current;

        if (keyboard != null)
        {
            if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame) _pendingSlot = 0;
            else if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame) _pendingSlot = 1;
            else if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame) _pendingSlot = 2;
            else if (keyboard.digit4Key.wasPressedThisFrame || keyboard.numpad4Key.wasPressedThisFrame) _pendingSlot = 3;

            if (keyboard.qKey.wasPressedThisFrame)
            {
                _pendingDrop = true;
            }
        }

        Mouse mouse = Mouse.current;

        if (mouse != null)
        {
            // 휠 한 칸 = 한 프레임의 스크롤 값. 위로 굴리면 이전 칸, 아래로 굴리면 다음 칸(기존 방향 유지).
            float scrollY = mouse.scroll.ReadValue().y;

            if (scrollY > 0f)
            {
                _pendingScroll = Mathf.Max(_pendingScroll - 1, -MaxScrollSteps);
            }
            else if (scrollY < 0f)
            {
                _pendingScroll = Mathf.Min(_pendingScroll + 1, MaxScrollSteps);
            }
        }
    }

    private const int MaxScrollSteps = 4;

    private void ClearPending()
    {
        _pendingSlot = -1;
        _pendingScroll = 0;
        _pendingDrop = false;
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
        if (!_isGameplayInputEnabled || _inputActions == null || PlayerData.IsLocalPlayerDead)
        {
            return data;
        }

        // CCTV / MainbaseControlUI / 해킹 팝업 등 UI 조작 중이면
        // 이 클라이언트의 플레이어 입력만 막음
        // 해킹 미니게임 중에도 막는다 — 이동(Move)에 방향키가 묶여 있어 순서 입력(방향키)·타이밍(Space)이 이동·점프로 샌다.
        // (GameplayInputBlocker 를 쓰지 않는 이유: 그쪽은 커서 잠금까지 바꿔, 단말기 위에서 해킹이 끝날 때 커서가 잠겨 버린다)
        if (GameplayInputBlocker.IsBlocked || Hacking.HackingManager.AnyHackingActive)
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

        // 모아 둔 한 번짜리 입력을 내보내고 비운다(같은 프레임에 틱이 두 번 돌아도 두 번째 틱에는 들어가지 않는다).
        data.SelectedSlotIndex = _pendingSlot;
        data.SlotScrollDirection = _pendingScroll;
        data.DropItem = _pendingDrop;
        ClearPending();

        // 아이템 상호 작용 키
        data.Buttons.Set((int)InputButton.Interact, _inputActions.Player.Interact.IsPressed());
        // data.Buttons.Set((int)InputButton.UseItem, _inputActions.Player.UseItem.IsPressed());
        // data.Buttons.Set((int)InputButton.DropItem, _inputActions.Player.DropItem.IsPressed());

        return data;
    }
}

