namespace Pawntom.Stage
{
    /// <summary>호스트가 지금 스폰을 어떤 경로로 해야 하는가.</summary>
    public enum StageSpawnMode
    {
        /// <summary>레벨 데이터가 없는 씬(Tutorial_Scene·Enemy_TestScene): 기존 인스펙터·마커 경로.</summary>
        Legacy = 0,

        /// <summary>레벨 데이터가 있고 시드 계획이 준비됨(본부 도킹 적용 끝): 계획대로 스폰.</summary>
        UsePlan = 1,

        /// <summary>레벨 데이터는 있으나 스테이지 상태가 아직 Spawned 되지 않음: 계획이 준비될 때까지 미룬다.</summary>
        Wait = 2,

        /// <summary>레벨 데이터는 있으나 스테이지 상태 오브젝트가 씬에 없음(씬 작업 누락): 오류를 남기고 기존 경로.</summary>
        LegacyMissingState = 3,
    }

    /// <summary>
    /// 스폰 순서 판정. 레벨 데이터 씬에서 본부가 선택된 도킹으로 옮겨지기 전에 플레이어를 만들지 않기 위한 관문이다.
    /// </summary>
    public static class StageSpawnGate
    {
        public static StageSpawnMode Decide(bool hasLayout, bool hasStateObject, bool planReady)
        {
            if (!hasLayout)
            {
                return StageSpawnMode.Legacy;
            }

            if (!hasStateObject)
            {
                return StageSpawnMode.LegacyMissingState;
            }

            return planReady ? StageSpawnMode.UsePlan : StageSpawnMode.Wait;
        }
    }

    /// <summary>미뤄 둔 호스트 스폰 단계.</summary>
    public enum StagePendingStep
    {
        None = 0,
        Players = 1,
        Enemies = 2,
    }

    /// <summary>
    /// 계획이 준비되기 전에 들어온 스폰 요청을 모아 두었다가 정해진 순서(플레이어 → 적)로 한 번씩 내준다.
    /// 적의 감지 대상은 플레이어가 스스로 등록하므로 적이 먼저 태어나면 안 된다(SpawnManager 기존 규칙).
    /// </summary>
    public sealed class StagePendingSpawns
    {
        private bool _players;
        private bool _enemies;

        public bool HasAny
        {
            get { return _players || _enemies; }
        }

        public void RequestPlayers()
        {
            _players = true;
        }

        public void RequestEnemies()
        {
            _enemies = true;
        }

        /// <summary>다음 단계를 꺼낸다. 꺼낸 단계는 지워진다. 없으면 None.</summary>
        public StagePendingStep Next()
        {
            if (_players)
            {
                _players = false;
                return StagePendingStep.Players;
            }

            if (_enemies)
            {
                _enemies = false;
                return StagePendingStep.Enemies;
            }

            return StagePendingStep.None;
        }

        public void Clear()
        {
            _players = false;
            _enemies = false;
        }
    }

    /// <summary>한 번만 일어나야 하는 호스트 부작용(계획 아이템 스폰·브루투스 스폰)의 중복 요청을 막는다.</summary>
    public sealed class StageOnceFlag
    {
        public bool IsClaimed { get; private set; }

        public bool TryClaim()
        {
            if (IsClaimed)
            {
                return false;
            }

            IsClaimed = true;
            return true;
        }
    }
}
