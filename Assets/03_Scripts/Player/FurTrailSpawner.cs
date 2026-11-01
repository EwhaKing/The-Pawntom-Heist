using Fusion;
using UnityEngine;

/// <summary>
/// 플레이어가 일정 거리를 이동할 때마다 바닥에 털공 흔적을 생성합니다.
/// 각 플레이어의 Input Authority가 생성을 요청하고, State Authority가 검증 후 스폰합니다.
/// </summary>
[RequireComponent(typeof(PlayerData))]
public class FurTrailSpawner : NetworkBehaviour
{
    [Header("Fur Ball Prefab")]
    [Tooltip("바닥에 생성할 네트워크 털공 프리팹입니다.")]
    [SerializeField] private NetworkObject furBallPrefab;

    // 수치는 플레이어 스케일 0.05(키 1.55m, 걷기 1m/s·달리기 2.25m/s) 기준이다.
    // 걷기 약 1.8초, 달리기 약 1.2초마다 하나(예전 0.7m/1.3m 는 너무 촘촘하다는 플레이 피드백으로 늘렸다).
    [Header("Spawn Distance")]
    [Tooltip("걷는 동안 털공 하나가 생성되기까지 필요한 수평 이동 거리입니다.")]
    [SerializeField, Min(0.1f)] private float walkSpawnDistance = 1.8f;

    [Tooltip("달리는 동안 털공 하나가 생성되기까지 필요한 수평 이동 거리입니다. 넓게 퍼지도록 걷기 값보다 크게 설정하세요.")]
    [SerializeField, Min(0.1f)] private float sprintSpawnDistance = 2.7f;

    [Header("Random Scatter")]
    [Tooltip("플레이어 진행 방향을 기준으로 좌우에 무작위로 흩어지는 최대 거리입니다.")]
    [SerializeField, Min(0f)] private float horizontalSpread = 0.35f;

    [Tooltip("플레이어 진행 방향을 기준으로 앞뒤에 무작위로 흩어지는 최대 거리입니다.")]
    [SerializeField, Min(0f)] private float longitudinalSpread = 0.35f;

    [Header("Ground Detection")]
    [Tooltip("플레이어 위치에서 레이캐스트를 시작할 높이입니다.")]
    [SerializeField, Min(0f)] private float rayStartHeight = 0.5f;

    [Tooltip("바닥을 찾기 위해 아래로 검사할 최대 거리입니다.")]
    [SerializeField, Min(0.1f)] private float groundCheckDistance = 2f;

    [Tooltip("털공을 생성할 수 있는 바닥 레이어입니다.")]
    [SerializeField] private LayerMask groundLayers = ~0;

    [Header("Placement")]
    [Tooltip("털공이 바닥에 파묻히는 것을 방지하기 위한 표면 방향 오프셋입니다.")]
    [SerializeField, Min(0f)] private float surfaceOffset = 0.03f;

    [Tooltip("활성화하면 털공의 위쪽 방향을 경사진 바닥의 노멀에 맞춥니다.")]
    [SerializeField] private bool alignToGroundNormal = true;

    [Tooltip("발자국이 플레이어가 바라보는 방향을 향하게 하되, 이 각도(도) 안에서 좌우로 조금씩 비틀어 자연스럽게 보이게 합니다.")]
    [SerializeField, Range(0f, 45f)] private float headingJitterDegrees = 12f;

    [Header("Footprint Size")]
    [Tooltip("걸을 때 찍히는 발자국 크기 배율입니다. 프리팹에 그려진 기본 크기가 1입니다.")]
    [SerializeField, Min(0.1f)] private float walkFootprintScale = 1f;

    [Tooltip("달릴 때 찍히는 발자국 크기 배율입니다. 걷기보다 크게 찍힙니다.")]
    [SerializeField, Min(0.1f)] private float sprintFootprintScale = 1.7f;

    [Networked] private int NextSequence { get; set; }

    private PlayerData _playerData;
    [Networked] private Vector3 LastSpawnPosition { get; set; }

    // 요청 후 승인 상태가 도착하기 전까지 매 틱 RPC를 보내지 않도록 제한합니다.
    private TickTimer _requestCooldown;

    private void Awake()
    {
        _playerData = GetComponent<PlayerData>();
    }

    public override void Spawned()
    {
        _requestCooldown = TickTimer.None;

        if (Object.HasStateAuthority)
        {
            LastSpawnPosition = transform.position;
            NextSequence = 1;
        }
    }

    public override void FixedUpdateNetwork()
    {
        // 재시뮬레이션에서는 같은 요청을 다시 보내지 않습니다.
        if (!Object.HasInputAuthority || !Runner.IsForward ||
            furBallPrefab == null || _playerData.IsDead ||
            !_requestCooldown.ExpiredOrNotRunning(Runner))
        {
            return;
        }

        if (!HasMovedSpawnDistance(transform.position)) return;

        _requestCooldown = TickTimer.CreateFromSeconds(Runner, 0.2f);
        RPC_RequestFurBall();
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestFurBall()
    {
        if (!Object.HasStateAuthority || furBallPrefab == null || _playerData.IsDead)
            return;

        // Client가 보내는 좌표 대신 Host가 알고 있는 해당 플레이어의 위치를 사용합니다.
        Vector3 currentPosition = transform.position;
        if (!HasMovedSpawnDistance(currentPosition)) return;

        if (TrySpawnFurBall(currentPosition))
        {
            LastSpawnPosition = currentPosition;
        }
    }

    private bool HasMovedSpawnDistance(Vector3 position)
    {
        float requiredDistance = _playerData.IsSprinting
            ? sprintSpawnDistance
            : walkSpawnDistance;
        Vector2 horizontalDelta = new(
            position.x - LastSpawnPosition.x,
            position.z - LastSpawnPosition.z
        );
        return horizontalDelta.sqrMagnitude >= requiredDistance * requiredDistance;
    }

    private bool TrySpawnFurBall(Vector3 playerPosition)
    {
        Vector3 scatterOffset = GetRandomScatterOffset();
        Vector3 rayOrigin = playerPosition
            + scatterOffset
            + Vector3.up * rayStartHeight;

        if (!Physics.Raycast(
                rayOrigin,
                Vector3.down,
                out RaycastHit hit,
                groundCheckDistance,
                groundLayers,
                QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        Vector3 spawnPosition = hit.point + hit.normal * surfaceOffset;
        Quaternion spawnRotation = GetSpawnRotation(hit.normal);
        int sequence = NextSequence++;
        PlayerRef creator = Object.InputAuthority;
        int spawnTick = Runner.Tick.Raw;
        float visualScale = _playerData.IsSprinting ? sprintFootprintScale : walkFootprintScale;

        Runner.Spawn(
            furBallPrefab,
            spawnPosition,
            spawnRotation,
            onBeforeSpawned: (runner, spawnedObject) =>
            {
                FurBallTrace trace = spawnedObject.GetComponent<FurBallTrace>();

                if (trace != null)
                {
                    trace.Initialize(creator, sequence, spawnTick, visualScale);
                }
            }
        );
        return true;
    }

    private Vector3 GetRandomScatterOffset()
    {
        Vector3 right = transform.right;
        Vector3 forward = transform.forward;
        right.y = 0f;
        forward.y = 0f;

        right.Normalize();
        forward.Normalize();

        float horizontalOffset = Random.Range(
            -horizontalSpread,
            horizontalSpread
        );
        float longitudinalOffset = Random.Range(
            -longitudinalSpread,
            longitudinalSpread
        );

        return right * horizontalOffset + forward * longitudinalOffset;
    }

    /// <summary>
    /// 발자국의 위쪽(발가락 쪽)이 플레이어가 바라보는 방향을 향하도록 회전을 정합니다.
    /// 오브젝트의 위(Y)는 바닥 노멀, 앞(Z)은 바닥에 눕힌 진행 방향입니다.
    /// </summary>
    private Quaternion GetSpawnRotation(Vector3 groundNormal)
    {
        Vector3 up = alignToGroundNormal ? groundNormal : Vector3.up;
        Vector3 heading = Vector3.ProjectOnPlane(transform.forward, up);

        if (heading.sqrMagnitude < 0.0001f)
        {
            heading = Vector3.ProjectOnPlane(Vector3.forward, up);
        }

        Quaternion rotation = Quaternion.LookRotation(heading.normalized, up);

        if (headingJitterDegrees > 0f)
        {
            rotation = Quaternion.AngleAxis(
                Random.Range(-headingJitterDegrees, headingJitterDegrees),
                up
            ) * rotation;
        }

        return rotation;
    }

    private void OnValidate()
    {
        walkSpawnDistance = Mathf.Max(0.1f, walkSpawnDistance);
        sprintSpawnDistance = Mathf.Max(0.1f, sprintSpawnDistance);
        horizontalSpread = Mathf.Max(0f, horizontalSpread);
        longitudinalSpread = Mathf.Max(0f, longitudinalSpread);
        rayStartHeight = Mathf.Max(0f, rayStartHeight);
        groundCheckDistance = Mathf.Max(0.1f, groundCheckDistance);
        surfaceOffset = Mathf.Max(0f, surfaceOffset);
    }
}
