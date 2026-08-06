using Fusion;
using UnityEngine;

[RequireComponent(typeof(NetworkCharacterController))]
[RequireComponent(typeof(PlayerData))]
public class PlayerController : NetworkBehaviour
{
    [Header("Camera")]
    [SerializeField] private Camera playerCamera;

    [Header("Look")]
    [SerializeField] private float lookSensitivity = 0.1f;
    [SerializeField] private float minPitch = -20f;
    [SerializeField] private float maxPitch = 20f;

    [Header("Movement")]
    [SerializeField] private float walkSpeed = 30f; //걷기 속도
    [SerializeField] private float sprintSpeed = 60f; //달리기 속도

    [Header("Jump")]
    [SerializeField, Range(0f, 1f)]
    private float jumpCutMultiplier = 0.45f;

    private NetworkCharacterController _controller;
    private PlayerData _playerData;

    private float _pitch; // 카메라의 위아래 회전값
    private NetworkButtons _previousButtons; // 이전 프레임의 버튼 상태를 저장하는 변수

    private void Awake()
    {
        _controller = GetComponent<NetworkCharacterController>();
        _playerData = GetComponent<PlayerData>();
    }

    /// <summary>
    /// 플레이어가 생성될 때 호출되는 메서드
    /// </summary>
    public override void Spawned()
    {
        bool isLocalPlayer = Object.HasInputAuthority;

        if (playerCamera != null)
        {
            playerCamera.enabled = isLocalPlayer;

            AudioListener listener = playerCamera.GetComponent<AudioListener>();

            if (listener != null)
            {
                listener.enabled = isLocalPlayer;
            }
        }

        if (isLocalPlayer)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!GetInput(out NetworkInputData input)) { return; }

        if (_playerData.IsDead)
        {
            return;
        }

        HandleLook(input.Look);

        bool wantsToSprint =
            input.Buttons.IsSet((int)InputButton.Sprint) &&
            input.Move.sqrMagnitude > 0f;
        bool isSprinting = CanSprint(wantsToSprint);

        UpdateStamina(isSprinting);
        HandleMove(input.Move, isSprinting);

        NetworkButtons pressed =
            input.Buttons.GetPressed(_previousButtons);

        NetworkButtons released =
            input.Buttons.GetReleased(_previousButtons);

        if (pressed.IsSet(InputButton.Jump))
        {
            HandleJump();
        }

        if (released.IsSet(InputButton.Jump))
        {
            HandleJumpReleased();
        }

        _previousButtons = input.Buttons;
    }

    private void HandleMove(Vector2 moveInput, bool isSprinting)
    {
        if (Object.HasStateAuthority)
        {
            _playerData.IsSprinting = isSprinting;
        }

        moveInput = Vector2.ClampMagnitude(moveInput, 1f); //벡터가 1을 넘지 않도록 제한

        Vector3 moveDirection = transform.forward * moveInput.y + transform.right * moveInput.x;

        moveDirection.y = 0f;

        float moveSpeed = isSprinting ? sprintSpeed : walkSpeed;

        _controller.maxSpeed = moveSpeed;
        _controller.Move(moveDirection);
    }

    private void HandleLook(Vector2 lookInput)
    {
        float yaw = lookInput.x * lookSensitivity;
        float pitchDelta = lookInput.y * lookSensitivity;

        // 좌우 회전은 플레이어 몸 전체에 적용
        transform.Rotate(Vector3.up * yaw);

        // 위아래 회전은 카메라에만 적용
        _pitch -= pitchDelta;
        _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);

        if (playerCamera != null)
        {
            playerCamera.transform.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }
    }

    private void HandleJumpReleased()
    {
        _controller.CutJump(jumpCutMultiplier);
    }

    private void HandleJump()
    {
        if (_playerData.IsExhausted ||
            !_controller.Grounded ||
            !_playerData.HasStamina(_playerData.JumpStaminaCost))
        {
            return;
        }

        if (Object.HasStateAuthority &&
            !_playerData.TryConsumeStamina(_playerData.JumpStaminaCost))
        {
            return;
        }

        Debug.Log($"점프 전: {_controller.Velocity.y}");

        _controller.Jump();

        Debug.Log(
        $"점프 후: {_controller.Velocity.y}, " +
        $"Impulse: {_controller.jumpImpulse}, " +
        $"Grounded: {_controller.Grounded}"
    );
    }

    private bool CanSprint(bool wantsToSprint)
    {
        if (!wantsToSprint ||
            _playerData.IsParkouring ||
            _playerData.IsExhausted)
        {
            return false;
        }

        return _playerData.CurrentStamina > 0f;
    }

    private void UpdateStamina(bool isSprinting)
    {
        if (!Object.HasStateAuthority)
        {
            return;
        }

        if (_playerData.IsParkouring)
        {
            float parkourCost = _playerData.ParkourStaminaCost * Runner.DeltaTime;

            if (!_playerData.TryConsumeStamina(parkourCost))
            {
                _playerData.StopParkour();
            }

            return;
        }

        if (isSprinting)
        {
            _playerData.TryConsumeStamina(
                _playerData.SprintStaminaCost * Runner.DeltaTime
            );
            return;
        }

        if (!_controller.Grounded)
        {
            return;
        }

        _playerData.RestoreStamina(
            _playerData.StaminaRegenRate * Runner.DeltaTime
        );
    }

    private void HandleParkour()
    {
        // TODO: 벽 감지와 파쿠르 이동이 구현되면
        // PlayerData.TryStartParkour / StopParkour를 이곳에서 호출합니다.
    }
}
