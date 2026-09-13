using Fusion;
using UnityEngine;

/// <summary>
/// 털공의 생성 정보와 고정 위치를 동기화하고, 네트워크 시간으로 수명을 관리합니다.
/// </summary>
public class FurBallTrace : NetworkBehaviour
{
    public const float LifetimeSeconds = 10f;

    [Networked] private TickTimer DespawnTimer { get; set; }
    [Networked] private Vector3 SpawnPosition { get; set; }
    [Networked] private Quaternion SpawnRotation { get; set; }

    [Networked] public PlayerRef Creator { get; private set; }
    [Networked] public int Sequence { get; private set; }
    [Networked] public int SpawnTick { get; private set; }

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            DespawnTimer = TickTimer.CreateFromSeconds(Runner, LifetimeSeconds);
            SpawnPosition = transform.position;
            SpawnRotation = transform.rotation;
        }

        // Runner.Spawn의 위치/회전 인자는 원격 클라이언트에 자동 전송되지 않습니다.
        // 생성 후 움직이지 않는 흔적이므로 최초 배치만 복원합니다.
        transform.SetPositionAndRotation(SpawnPosition, SpawnRotation);
    }

    public override void FixedUpdateNetwork()
    {
        if (Object.HasStateAuthority && DespawnTimer.Expired(Runner))
        {
            Runner.Despawn(Object);
        }
    }

    /// <summary>
    /// State Authority가 털공을 네트워크 스폰하기 직전에 호출합니다.
    /// </summary>
    public void Initialize(PlayerRef creator, int sequence, int spawnTick)
    {
        Creator = creator;
        Sequence = sequence;
        SpawnTick = spawnTick;
    }

    /// <summary>
    /// 이 털공이 지정한 플레이어가 생성한 흔적인지 반환합니다.
    /// </summary>
    public bool WasCreatedBy(PlayerRef player)
    {
        return Creator == player;
    }

    /// <summary>
    /// 같은 플레이어의 다른 털공보다 나중에 생성됐는지 반환합니다.
    /// </summary>
    public bool IsNewerThan(FurBallTrace other)
    {
        return TryCompareOrder(other, out int comparison) && comparison > 0;
    }

    /// <summary>
    /// 같은 플레이어가 만든 두 털공의 생성 순서를 비교합니다.
    /// 성공 시 comparison은 이전이면 음수, 같으면 0, 이후면 양수입니다.
    /// </summary>
    public bool TryCompareOrder(FurBallTrace other, out int comparison)
    {
        comparison = 0;

        if (other == null || Creator != other.Creator)
        {
            return false;
        }

        comparison = Sequence.CompareTo(other.Sequence);
        return true;
    }
}
