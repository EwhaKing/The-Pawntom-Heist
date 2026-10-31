using Fusion;
using Pawntom.Enemy.Authoring;
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
    [Tooltip("점프 최고 높이(월드 단위 m). 플레이어 키(CharacterController 31 × 스케일 0.05 = 1.55m) 기준으로 잡는다.\n" +
             "기본값 0.9 는 스케일을 줄이기 전 손맛(키의 약 58%)을 그대로 옮긴 값이다.")]
    [SerializeField, Min(0.01f)] private float jumpHeight = 0.9f;

    [Tooltip("최고점까지 올라가는 시간(초). 짧을수록 빠르고 묵직하다. 기본 0.6 은 스케일 다운 전과 같다.")]
    [SerializeField, Min(0.05f)] private float jumpRiseSeconds = 0.6f;

    [Tooltip("켜면 위 두 값에서 중력을 계산해 NetworkCharacterController.gravity 를 덮어쓴다(낙하도 같은 비율로 맞춰진다).\n" +
             "끄면 NetworkCharacterController 의 중력을 그대로 두고 초기 속도만 높이에 맞춘다.")]
    [SerializeField] private bool deriveGravityFromJump = true;

    [SerializeField, Range(0f, 1f)]
    private float jumpCutMultiplier = 0.45f;

    [Header("Enemy Contact Damage")]
    [Tooltip("기획 수치 확정 전 임시값. 적 접촉 1회당 피해량")]
    [SerializeField, Min(0f)] private float _enemyContactDamage = 25f;
    [SerializeField, Min(0.01f)] private float _contactDamageInterval = 1f;
    [Networked] private TickTimer ContactDamageCooldown { get; set; }

    private NetworkCharacterController _controller;
    private PlayerData _playerData;

    public float WalkSpeed => walkSpeed;
    public float SprintSpeed => sprintSpeed;

    [Networked] public float ViewPitch { get; private set; }
    public Camera ViewCamera => playerCamera;
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
        // 플레이어 스케일을 0.05 로 줄인 뒤에도 NetworkCharacterController 의 중력(-100)·점프 임펄스(60)는
        // 예전 스케일 값 그대로라 최고점이 60²/(2×100) = 18m 였다(키 1.55m). 점프를 "높이·상승 시간"으로 정의하고
        // 거기서 중력과 초기 속도를 계산해 스케일과 무관하게 맞춘다. 모든 피어의 Spawned 에서 같은 값이 들어간다.
        if (deriveGravityFromJump)
        {
            _controller.gravity = -JumpGravity(jumpHeight, jumpRiseSeconds);
        }

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
        if (_playerData.IsDead)
        {
            if (Object.HasStateAuthority || Object.HasInputAuthority)
            {
                _controller.Velocity = Vector3.zero;
            }
            return;
        }

        if (!GetInput(out NetworkInputData input)) { return; }

        HandleLook(input.Look);

        bool wantsToSprint =
            input.Buttons.IsSet((int)InputButton.Sprint) &&
            input.Move.sqrMagnitude > 0f;
        bool isSprinting = CanSprint(wantsToSprint);

        UpdateStamina(isSprinting);
        HandleMove(input.Move, isSprinting);

        // Move 안의 충돌 콜백에서 사망했으면 같은 틱의 점프도 차단합니다.
        if (_playerData.IsDead)
        {
            _controller.Velocity = Vector3.zero;
            return;
        }

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

    public override void Render()
    {
        // 다른 클라이언트의 비활성 카메라도 관전용 1인칭 시점을 유지합니다.
        if (playerCamera != null && !Object.HasInputAuthority)
            playerCamera.transform.localRotation = Quaternion.Euler(ViewPitch, 0f, 0f);
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        TryApplyEnemyContactDamage(hit.collider);
    }

    private void OnTriggerStay(Collider other)
    {
        TryApplyEnemyContactDamage(other);
    }

    private void TryApplyEnemyContactDamage(Collider other)
    {
        if (Object == null || !Object.IsValid || !Object.HasStateAuthority ||
            _playerData.IsDead || !ContactDamageCooldown.ExpiredOrNotRunning(Runner))
        {
            return;
        }

        EnemyAgent enemy = other.GetComponentInParent<EnemyAgent>();
        if (enemy == null || !enemy.isActiveAndEnabled)
        {
            return;
        }

        _playerData.ApplyDamage(_enemyContactDamage);
        ContactDamageCooldown = TickTimer.CreateFromSeconds(
            Runner, Mathf.Max(0.01f, _contactDamageInterval));
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

        // NetworkCharacterController.Move 는 이동 후 속도를 "실제 이동량 × TickRate"로 다시 계산한다.
        // 그래서 바닥 겹침 해소·계단 오르기처럼 CharacterController 가 몸을 밀어 올린 거리까지 위쪽 속도가 된다
        // (스폰 직후 바닥에 13cm 겹친 것을 밀어내면 0.13×64 ≈ 8m/s 로 튀어 올라 공중에서 떨어졌다).
        // 이동 전 세로 속도(점프 중이면 점프 속도)보다 큰 위쪽 속도는 입력이 만든 것이 아니므로 잘라 낸다.
        float verticalBeforeMove = _controller.Velocity.y;
        _controller.Move(moveDirection);

        Vector3 velocity = _controller.Velocity;
        float maxUpward = Mathf.Max(verticalBeforeMove, 0f);

        if (velocity.y > maxUpward)
        {
            velocity.y = maxUpward;
            _controller.Velocity = velocity;
        }
    }

    private void HandleLook(Vector2 lookInput)
    {
        float yaw = lookInput.x * lookSensitivity;
        float pitchDelta = lookInput.y * lookSensitivity;

        // 좌우 회전은 플레이어 몸 전체에 적용
        transform.Rotate(Vector3.up * yaw);

        // 위아래 회전은 카메라에만 적용
        ViewPitch = Mathf.Clamp(ViewPitch - pitchDelta, minPitch, maxPitch);

        if (playerCamera != null)
        {
            playerCamera.transform.localRotation = Quaternion.Euler(ViewPitch, 0f, 0f);
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

        // NetworkCharacterController.jumpImpulse(예전 스케일 값)는 쓰지 않는다. 목표 높이에 필요한 초기 속도를 넘긴다.
        // v0 = sqrt(2·g·h). 중력을 직접 계산했으면 2h/t 와 같다.
        float impulse = Mathf.Sqrt(2f * Mathf.Abs(_controller.gravity) * jumpHeight);
        _controller.Jump(false, impulse);
    }

    /// <summary>높이 h 를 t 초 만에 오르는 등감속 운동의 중력 크기: g = 2h / t².</summary>
    private static float JumpGravity(float height, float riseSeconds)
    {
        return 2f * height / (riseSeconds * riseSeconds);
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
