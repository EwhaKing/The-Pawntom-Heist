using Fusion;
using Pawntom.Settings;
using System;
using System.Collections.Generic;
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
/// - 개인 마우스 감도·Y 반전(옵션창). 감도는 입력을 보내는 이 클라이언트에서 곱한다 — PlayerController 는
///   상태 권한(호스트)에서 시점을 돌리므로, 거기서 곱하면 호스트의 감도가 모두에게 적용된다.
/// - 키 바꾸기(옵션창). 바인딩 오버라이드를 JSON 으로 저장·복원한다.
/// *싱글톤으로 만들어졌기 때문에 게임 내내 살아있습니다. 맵 씬에서 입력을 받는 경우 / 그 외 씬들에서 입력을 받는 경우를 구분해두어야 합니다.
///
/// TODO:
/// - 아이템과의 상호작용(획득 및 사용)
/// </summary>
public class InputManager : PawntomSingleton<InputManager>
{
    private const string KeyboardMouseGroup = "Keyboard&Mouse";

    private InputSystemActions _inputActions;
    private bool _isGameplayInputEnabled;

    // .inputactions 에셋(직렬화 에셋)에 없는 동작은 코드로 만든 맵에 둔다. 지금은 Q 버리기 하나.
    private InputActionMap _extraMap;
    private InputAction _dropAction;

    // 중복 인스턴스(씬에 하나 더 있던 것)는 base.Awake 가 파괴한다. 액션을 만든 쪽만 정리한다.
    private bool _ownsActions;

    /// <summary>
    /// 살아 있는 인스턴스(없으면 null). <see cref="PawntomSingleton{T}.Instance"/> 와 달리 새로 만들지 않는다 —
    /// 종료·씬 정리 중(OnDisable/OnDestroy) 부르면 Instance 가 새 InputManager 를 만들어 "정리되지 않은 오브젝트" 경고가 난다.
    /// </summary>
    public static InputManager Live { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetLive()
    {
        Live = null;
    }

    private readonly List<RebindableBinding> _rebindables = new List<RebindableBinding>();
    private InputActionRebindingExtensions.RebindingOperation _rebindOperation;

    /// <summary>
    /// 게임 플레이 입력이 켜져 있는가. 입력 액션을 거치지 않고 키를 직접 읽는 곳(튜토리얼 등)이
    /// 연출 중 잠금(튜토리얼 대사·오프닝)을 따르도록 확인한다.
    /// </summary>
    public bool IsGameplayInputEnabled => _isGameplayInputEnabled;

    /// <summary>마우스 감도 배율(옵션창). 1 = 기존 손맛.</summary>
    public float LookScaleX { get; set; } = 1f;

    /// <summary>세로 감도 배율. Y 반전이면 음수.</summary>
    public float LookScaleY { get; set; } = 1f;

    /// <summary>옵션창에서 바꿀 수 있는 키 목록(표시 순서).</summary>
    public IReadOnlyList<RebindableBinding> Rebindables => _rebindables;

    /// <summary>키를 바꾸거나 기본값으로 되돌렸을 때. 키 안내 문구가 다시 읽는다.</summary>
    public static event Action BindingsChanged;

    /// <summary>키 입력을 기다리는 중인가(이때 Esc 는 "취소"다 — ESC 메뉴가 같이 반응하면 안 된다).</summary>
    public bool IsRebinding => _rebindOperation != null;

    /// <summary>마지막으로 키 바꾸기가 끝난 프레임. 그 프레임의 Esc 는 메뉴가 무시한다.</summary>
    public int LastRebindEndFrame { get; private set; } = -10;

    // 프레임 단위로 모아 두는 "한 번만" 입력. Fusion 은 입력을 틱마다(초당 64회) 걷어 가는데 프레임과 박자가 다르다 —
    // 수집 순간에만 wasPressedThisFrame 을 읽으면 틱이 없는 프레임의 눌림은 사라지고, 한 프레임에 틱이 두 번 돌면
    // 같은 눌림이 두 번 들어가 슬롯 선택이 켜졌다 바로 꺼졌다(토글). 그래서 Update 에서 모으고, 수집 때 한 번 내보내고 비운다.
    private int _pendingSlot = -1;
    private int _pendingScroll;
    private bool _pendingDrop;

    // 마우스 시점 이동량도 같은 이유로 프레임마다 더해 둔다. 수집 순간의 한 프레임 값만 읽으면, 프레임이 틱(64Hz)보다
    // 빠를 때 사이 프레임의 마우스 이동이 버려지고 틱마다 들쭉날쭉하게 돌아 화면이 지직거린다.
    private Vector2 _pendingLook;

    /// <summary>
    /// 켜져 있으면 Q 를 바로 보내지 않고 <see cref="DropPressed"/> 로 알린다. 1인칭 팔이 손을 뻗어 바닥에 닿는 순간
    /// <see cref="RequestDrop"/> 로 실제 버리기를 보낸다 — 손에서 놓는 순간과 월드에 아이템이 생기는 순간을 맞춘다.
    /// </summary>
    public bool DeferDropToArms { get; set; }

    /// <summary>Q 가 눌렸다(<see cref="DeferDropToArms"/> 가 켜져 있을 때만).</summary>
    public event Action DropPressed;

    /// <summary>다음 네트워크 입력에 "버리기"를 한 번 싣는다.</summary>
    public void RequestDrop()
    {
        _pendingDrop = true;
    }

    /// <summary>
    /// 이번 프레임에 상호작용 키가 눌렸는가. 게임 입력이 꺼져 있으면(연출 잠금·로비) 항상 false.
    /// 키를 직접 읽지 않고 액션을 읽으므로 옵션창에서 바꾼 키를 따른다.
    /// </summary>
    public bool InteractPressedThisFrame =>
        _isGameplayInputEnabled && _inputActions != null && _inputActions.Player.Interact.WasPressedThisFrame();

    protected override void Awake()
    {
        base.Awake();

        // 중복 인스턴스는 base.Awake 가 파괴한다. 그 경우 액션을 만들지 않는다.
        if (Instance != this)
        {
            return;
        }

        _ownsActions = true;
        Live = this;
        _inputActions = new InputSystemActions();

        _extraMap = new InputActionMap("Extra");
        _dropAction = _extraMap.AddAction("Drop", InputActionType.Button);
        _dropAction.AddBinding("<Keyboard>/q").WithGroup(KeyboardMouseGroup);

        BuildRebindables();
    }

    private void OnDestroy()
    {
        if (!_ownsActions)
        {
            return;
        }

        if (Live == this)
        {
            Live = null;
        }

        CancelRebind();
        _inputActions?.Dispose();
        _extraMap?.Dispose();
    }

    private void Update()
    {
        if (!_isGameplayInputEnabled || GameplayInputBlocker.IsBlocked || Hacking.HackingManager.AnyHackingActive)
        {
            ClearPending();
            return;
        }

        _pendingLook += _inputActions.Player.Look.ReadValue<Vector2>();

        Keyboard keyboard = Keyboard.current;

        if (keyboard != null)
        {
            if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame) _pendingSlot = 0;
            else if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame) _pendingSlot = 1;
            else if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame) _pendingSlot = 2;
            else if (keyboard.digit4Key.wasPressedThisFrame || keyboard.numpad4Key.wasPressedThisFrame) _pendingSlot = 3;
        }

        if (_dropAction.WasPressedThisFrame())
        {
            if (DeferDropToArms && DropPressed != null)
            {
                DropPressed.Invoke();
            }
            else
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
        _pendingLook = Vector2.zero;
    }

    /// <summary>
    /// 이동, 시점 회전 등 게임 플레이 입력을 활성화합니다.
    /// </summary>
    public void EnableGameplayInput()
    {
        _isGameplayInputEnabled = true;

        // 키 바꾸기 중에는 그 액션을 꺼 둬야 한다. 끝날 때 RestoreActionsAfterRebind 가 상태에 맞춰 켠다.
        if (IsRebinding)
        {
            return;
        }

        _inputActions.Player.Enable();
        _extraMap.Enable();
    }

    /// <summary>
    /// 게임 플레이 입력을 비활성화합니다.
    /// </summary>
    public void DisableGameplayInput()
    {
        _inputActions.Player.Disable();
        _extraMap.Disable();
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

        // CCTV / MainbaseControlUI / ESC 메뉴 / 해킹 팝업 등 UI 조작 중이면
        // 이 클라이언트의 플레이어 입력만 막음(시뮬레이션은 계속 돈다 — 다른 플레이어의 게임은 멈추지 않는다)
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
        // 틱 사이에 모은 이동량을 한 번에 내보낸다(ClearPending 이 비운다). 개인 감도는 여기서 곱한다.
        data.Look = new Vector2(_pendingLook.x * LookScaleX, _pendingLook.y * LookScaleY);

        data.Buttons.Set((int)InputButton.Jump, _inputActions.Player.Jump.IsPressed());
        data.Buttons.Set((int)InputButton.Sprint, _inputActions.Player.Sprint.IsPressed());
        data.Buttons.Set((int)InputButton.Interact, _inputActions.Player.Interact.IsPressed());

        // 모아 둔 한 번짜리 입력을 내보내고 비운다(같은 프레임에 틱이 두 번 돌아도 두 번째 틱에는 들어가지 않는다).
        data.SelectedSlotIndex = _pendingSlot;
        data.SlotScrollDirection = _pendingScroll;
        data.DropItem = _pendingDrop;
        ClearPending();

        // data.Buttons.Set((int)InputButton.UseItem, _inputActions.Player.UseItem.IsPressed());
        // data.Buttons.Set((int)InputButton.DropItem, _inputActions.Player.DropItem.IsPressed());

        return data;
    }

    // ==========================================
    // 키 바꾸기
    // ==========================================

    private void BuildRebindables()
    {
        _rebindables.Clear();
        AddCompositePart("MoveForward", "앞으로 이동", _inputActions.Player.Move, "up");
        AddCompositePart("MoveBack", "뒤로 이동", _inputActions.Player.Move, "down");
        AddCompositePart("MoveLeft", "왼쪽 이동", _inputActions.Player.Move, "left");
        AddCompositePart("MoveRight", "오른쪽 이동", _inputActions.Player.Move, "right");
        AddButton("Sprint", "달리기", _inputActions.Player.Sprint);
        AddButton("Jump", "점프", _inputActions.Player.Jump);
        AddButton("Interact", "상호작용", _inputActions.Player.Interact);
        AddButton("Drop", "아이템 버리기", _dropAction);
    }

    private void AddCompositePart(string id, string label, InputAction action, string part)
    {
        // 키보드·마우스 그룹의 첫 번째 같은 이름 부분(WASD 쪽). 화살표 쪽은 보조 키로 남는다.
        for (int i = 0; i < action.bindings.Count; i++)
        {
            InputBinding binding = action.bindings[i];

            if (binding.isPartOfComposite &&
                string.Equals(binding.name, part, StringComparison.OrdinalIgnoreCase) &&
                InGroup(binding, KeyboardMouseGroup))
            {
                _rebindables.Add(new RebindableBinding(id, label, action, i));
                return;
            }
        }

        Debug.LogWarning("[InputManager] 바꿀 수 있는 키를 찾지 못했습니다: " + action.name + "/" + part);
    }

    private void AddButton(string id, string label, InputAction action)
    {
        for (int i = 0; i < action.bindings.Count; i++)
        {
            InputBinding binding = action.bindings[i];

            if (!binding.isComposite && !binding.isPartOfComposite && InGroup(binding, KeyboardMouseGroup))
            {
                _rebindables.Add(new RebindableBinding(id, label, action, i));
                return;
            }
        }

        Debug.LogWarning("[InputManager] 바꿀 수 있는 키를 찾지 못했습니다: " + action.name);
    }

    private static bool InGroup(InputBinding binding, string group)
    {
        return !string.IsNullOrEmpty(binding.groups) &&
               binding.groups.IndexOf(group, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public RebindableBinding FindRebindable(string id)
    {
        for (int i = 0; i < _rebindables.Count; i++)
        {
            if (_rebindables[i].Id == id)
            {
                return _rebindables[i];
            }
        }

        return null;
    }

    /// <summary>상호작용 키 이름(안내 문구용). 못 읽으면 fallback.</summary>
    public string GetKeyDisplayName(string id, string fallback)
    {
        RebindableBinding entry = FindRebindable(id);

        if (entry == null)
        {
            return fallback;
        }

        string display = entry.DisplayName;
        return display == "-" ? fallback : display;
    }

    /// <summary>
    /// 다음에 누르는 키(또는 마우스 버튼)로 entry 를 바꾼다. Esc 는 취소. 다른 칸이 같은 키를 쓰면 두 칸의 키를 맞바꾼다.
    /// </summary>
    /// <param name="onFinished">true = 바뀜, false = 취소·거부.</param>
    public void StartRebind(RebindableBinding entry, Action<bool> onFinished)
    {
        if (entry == null || IsRebinding)
        {
            onFinished?.Invoke(false);
            return;
        }

        string previousPath = entry.EffectivePath;

        // 키 바꾸기는 액션이 꺼져 있어야 한다. 게임 중이면 잠깐 끄고 끝나면 상태에 맞춰 되살린다.
        _inputActions.Player.Disable();
        _extraMap.Disable();

        _rebindOperation = entry.Action.PerformInteractiveRebinding(entry.BindingIndex)
            .WithExpectedControlType("Button")
            .WithControlsHavingToMatchPath("<Keyboard>")
            .WithControlsHavingToMatchPath("<Mouse>")
            .WithControlsExcluding("<Mouse>/position")
            .WithControlsExcluding("<Mouse>/delta")
            .WithControlsExcluding("<Mouse>/scroll")
            .WithControlsExcluding("<Pointer>/press")
            .WithControlsExcluding("<Keyboard>/anyKey")
            .WithCancelingThrough("<Keyboard>/escape")
            .OnMatchWaitForAnother(0.1f)
            .OnCancel(operation => FinishRebind(entry, previousPath, false, onFinished))
            .OnComplete(operation => FinishRebind(entry, previousPath, true, onFinished))
            .Start();
    }

    public void CancelRebind()
    {
        if (_rebindOperation != null)
        {
            _rebindOperation.Cancel();
        }
    }

    private void FinishRebind(RebindableBinding entry, string previousPath, bool completed, Action<bool> onFinished)
    {
        if (_rebindOperation != null)
        {
            _rebindOperation.Dispose();
            _rebindOperation = null;
        }

        LastRebindEndFrame = Time.frameCount;
        bool changed = false;

        if (completed)
        {
            string newPath = entry.EffectivePath;

            if (RebindRules.IsReserved(newPath))
            {
                // 예약된 키(1~4 칸 선택 등)는 받지 않는다. 이전 키로 되돌린다.
                ApplyPath(entry, previousPath);
            }
            else
            {
                SwapConflict(entry, newPath, previousPath);
                changed = !RebindRules.SamePath(newPath, previousPath);
            }
        }

        RestoreActionsAfterRebind();

        if (changed)
        {
            BindingsChanged?.Invoke();
        }

        onFinished?.Invoke(changed);
    }

    private void SwapConflict(RebindableBinding entry, string newPath, string previousPath)
    {
        int targetIndex = _rebindables.IndexOf(entry);
        List<string> paths = new List<string>(_rebindables.Count);

        for (int i = 0; i < _rebindables.Count; i++)
        {
            paths.Add(i == targetIndex ? previousPath : _rebindables[i].EffectivePath);
        }

        int conflict = RebindRules.FindConflict(paths, targetIndex, newPath);

        if (conflict >= 0)
        {
            ApplyPath(_rebindables[conflict], previousPath);
        }
    }

    private static void ApplyPath(RebindableBinding entry, string path)
    {
        InputBinding binding = entry.Action.bindings[entry.BindingIndex];

        if (RebindRules.SamePath(binding.path, path))
        {
            entry.Action.RemoveBindingOverride(entry.BindingIndex);
        }
        else
        {
            entry.Action.ApplyBindingOverride(entry.BindingIndex, path);
        }
    }

    private void RestoreActionsAfterRebind()
    {
        if (_isGameplayInputEnabled)
        {
            _inputActions.Player.Enable();
            _extraMap.Enable();
        }
    }

    /// <summary>
    /// 따로 만든 InputSystemActions(튜토리얼이 입력 잠금 중에도 키를 읽으려고 둔 것)에 지금 키 설정을 복사한다.
    /// 복사하지 않으면 그 인스턴스는 에셋 기본 키만 알아, 키를 바꾼 플레이어의 점프·상호작용을 못 알아챈다.
    /// <see cref="BindingsChanged"/> 때 다시 부른다.
    /// </summary>
    public void CopyBindingOverridesTo(InputSystemActions target)
    {
        if (target == null || _inputActions == null || ReferenceEquals(target, _inputActions))
        {
            return;
        }

        target.asset.RemoveAllBindingOverrides();
        string json = _inputActions.asset.SaveBindingOverridesAsJson();

        if (!string.IsNullOrEmpty(json))
        {
            target.asset.LoadBindingOverridesFromJson(json);
        }
    }

    /// <summary>모든 키를 기본값으로.</summary>
    public void ResetBindings()
    {
        CancelRebind();
        _inputActions.asset.RemoveAllBindingOverrides();
        _extraMap.RemoveAllBindingOverrides();
        BindingsChanged?.Invoke();
    }

    [Serializable]
    private sealed class BindingOverridesBlob
    {
        public string player;
        public string extra;
    }

    /// <summary>현재 바인딩 오버라이드를 저장용 JSON 하나로.</summary>
    public string SaveBindingOverrides()
    {
        BindingOverridesBlob blob = new BindingOverridesBlob
        {
            player = _inputActions.asset.SaveBindingOverridesAsJson(),
            extra = _extraMap.SaveBindingOverridesAsJson(),
        };

        return JsonUtility.ToJson(blob);
    }

    /// <summary>저장해 둔 JSON 으로 바인딩을 되살린다. 비었거나 깨졌으면 기본 키.</summary>
    public void LoadBindingOverrides(string json)
    {
        _inputActions.asset.RemoveAllBindingOverrides();
        _extraMap.RemoveAllBindingOverrides();

        if (!string.IsNullOrEmpty(json))
        {
            try
            {
                BindingOverridesBlob blob = JsonUtility.FromJson<BindingOverridesBlob>(json);

                if (blob != null && !string.IsNullOrEmpty(blob.player))
                {
                    _inputActions.asset.LoadBindingOverridesFromJson(blob.player);
                }

                if (blob != null && !string.IsNullOrEmpty(blob.extra))
                {
                    _extraMap.LoadBindingOverridesFromJson(blob.extra);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[InputManager] 저장된 키 설정을 읽지 못해 기본 키를 씁니다: " + exception.Message);
                _inputActions.asset.RemoveAllBindingOverrides();
                _extraMap.RemoveAllBindingOverrides();
            }
        }

        BindingsChanged?.Invoke();
    }
}
