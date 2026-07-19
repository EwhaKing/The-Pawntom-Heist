using Fusion;
using UnityEngine;

[RequireComponent(typeof(NetworkCharacterController))]
public class PlayerController : NetworkBehaviour
{
    [Header("Camera")]
    [SerializeField] private Camera playerCamera;

    [Header("Look")]
    [SerializeField] private float lookSensitivity = 0.1f;
    [SerializeField] private float minPitch = -20f;
    [SerializeField] private float maxPitch = 20f;
    [Header("Movement")]
    [SerializeField] private float walkSpeed = 20f; //걷기 속도
    [SerializeField] private float sprintSpeed = 40f; //달리기 속도

    private NetworkCharacterController _controller;

    private float _pitch; // 카메라의 위아래 회전값
    private NetworkButtons _previousButtons; // 이전 프레임의 버튼 상태를 저장하는 변수

    private void Awake()
    {
        _controller = GetComponent<NetworkCharacterController>();
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

        HandleLook(input.Look);

        bool isSprinting = input.Buttons.IsSet((int)InputButton.Sprint);
        HandleMove(input.Move, isSprinting);

        NetworkButtons pressedButtons = input.Buttons.GetPressed(_previousButtons);
        _previousButtons = input.Buttons;
        HandleButtons(pressedButtons);
    }

    private void HandleMove(Vector2 moveInput, bool isSprinting)
    {
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

    private void HandleButtons(NetworkButtons buttons)
    {
        if (buttons.IsSet((int)InputButton.Jump))
        {
            HandleJump();
        }
    }

    private void HandleJump()
    {
        _controller.Jump();
    }
}