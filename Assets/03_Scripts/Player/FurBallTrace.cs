using Fusion;

/// <summary>
/// 적 AI 등 외부 시스템이 털공의 생성 주체와 순서를 읽을 수 있게 합니다.
/// </summary>
public class FurBallTrace : NetworkBehaviour
{
    [Networked] public PlayerRef Creator { get; private set; }
    [Networked] public int Sequence { get; private set; }
    [Networked] public int SpawnTick { get; private set; }

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
