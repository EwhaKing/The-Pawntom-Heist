using Fusion;
using UnityEngine;

/// <summary>
/// 플레이어가 일정 거리를 이동할 때마다 바닥에 털공 흔적을 생성합니다.
/// 생성 판정과 네트워크 스폰은 State Authority에서만 수행합니다.
/// </summary>
[RequireComponent(typeof(PlayerData))]
public class FurTrailSpawner : NetworkBehaviour
{
    [Header("Fur Ball Prefab")]
    [Tooltip("바닥에 생성할 네트워크 털공 프리팹입니다.")]
    [SerializeField] private NetworkObject furBallPrefab;

    [Header("Spawn Distance")]
    [Tooltip("걷는 동안 털공 하나가 생성되기까지 필요한 수평 이동 거리입니다.")]
    [SerializeField, Min(0.1f)] private float walkSpawnDistance = 15f;

    [Tooltip("달리는 동안 털공 하나가 생성되기까지 필요한 수평 이동 거리입니다. 넓게 퍼지도록 걷기 값보다 크게 설정하세요.")]
    [SerializeField, Min(0.1f)] private float sprintSpawnDistance = 25f;

    [Header("Random Scatter")]
    [Tooltip("플레이어 진행 방향을 기준으로 좌우에 무작위로 흩어지는 최대 거리입니다.")]
    [SerializeField, Min(0f)] private float horizontalSpread = 5f;

    [Tooltip("플레이어 진행 방향을 기준으로 앞뒤에 무작위로 흩어지는 최대 거리입니다.")]
    [SerializeField, Min(0f)] private float longitudinalSpread = 5f;

    [Header("Ground Detection")]
    [Tooltip("플레이어 위치에서 레이캐스트를 시작할 높이입니다.")]
    [SerializeField, Min(0f)] private float rayStartHeight = 5f;

    [Tooltip("바닥을 찾기 위해 아래로 검사할 최대 거리입니다.")]
    [SerializeField, Min(0.1f)] private float groundCheckDistance = 40f;

    [Tooltip("털공을 생성할 수 있는 바닥 레이어입니다.")]
    [SerializeField] private LayerMask groundLayers = ~0;

    [Header("Placement")]
    [Tooltip("털공이 바닥에 파묻히는 것을 방지하기 위한 표면 방향 오프셋입니다.")]
    [SerializeField, Min(0f)] private float surfaceOffset = 0.1f;

    [Tooltip("활성화하면 털공의 위쪽 방향을 경사진 바닥의 노멀에 맞춥니다.")]
    [SerializeField] private bool alignToGroundNormal = true;

    [Tooltip("활성화하면 생성될 때마다 바닥 방향을 기준으로 임의의 회전을 적용합니다.")]
    [SerializeField] private bool randomizeRotation = true;

    [Networked] private int NextSequence { get; set; }

    private PlayerData _playerData;
    private Vector3 _lastSpawnPosition;

    private void Awake()
    {
        _playerData = GetComponent<PlayerData>();
    }

    public override void Spawned()
    {
        _lastSpawnPosition = transform.position;

        if (Object.HasStateAuthority)
        {
            NextSequence = 1;
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority || furBallPrefab == null)
        {
            return;
        }

        float requiredDistance = _playerData.IsSprinting
            ? sprintSpawnDistance
            : walkSpawnDistance;

        Vector3 currentPosition = transform.position;
        Vector2 horizontalDelta = new(
            currentPosition.x - _lastSpawnPosition.x,
            currentPosition.z - _lastSpawnPosition.z
        );

        if (horizontalDelta.sqrMagnitude < requiredDistance * requiredDistance)
        {
            return;
        }

        if (TrySpawnFurBall(currentPosition))
        {
            _lastSpawnPosition = currentPosition;
        }
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

        Runner.Spawn(
            furBallPrefab,
            spawnPosition,
            spawnRotation,
            onBeforeSpawned: (runner, spawnedObject) =>
            {
                FurBallTrace trace = spawnedObject.GetComponent<FurBallTrace>();

                if (trace != null)
                {
                    trace.Initialize(creator, sequence, spawnTick);
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

    private Quaternion GetSpawnRotation(Vector3 groundNormal)
    {
        Quaternion rotation = alignToGroundNormal
            ? Quaternion.FromToRotation(Vector3.up, groundNormal)
            : Quaternion.identity;

        if (randomizeRotation)
        {
            rotation = Quaternion.AngleAxis(
                Random.Range(0f, 360f),
                groundNormal
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
