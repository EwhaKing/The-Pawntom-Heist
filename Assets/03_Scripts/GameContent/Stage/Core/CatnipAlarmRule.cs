namespace Pawntom.Stage
{
    /// <summary>
    /// 캣닢 경보 발동 규칙(엔진 비의존). GAME_DESIGN §3.7 "캣닢 획득 즉시 경보 → 브루투스 스폰".
    /// 누군가의 인벤토리에 캣닢이 처음 들어온 순간 한 번만 발동한다. 인계·드롭 후 재획득은 무시한다.
    /// </summary>
    public sealed class CatnipAlarmRule
    {
        public bool IsTriggered { get; private set; }

        /// <summary>이번 관찰에서 경보가 처음 발동했으면 true.</summary>
        /// <param name="anyPlayerHoldsCatnip">지금 살아 있는 플레이어 중 한 명이라도 캣닢을 들고 있다.</param>
        public bool Observe(bool anyPlayerHoldsCatnip)
        {
            if (IsTriggered || !anyPlayerHoldsCatnip)
            {
                return false;
            }

            IsTriggered = true;
            return true;
        }

        /// <summary>네트워크 상태(이미 발동했는가)로 되돌린다.</summary>
        public void Restore(bool triggered)
        {
            IsTriggered = triggered;
        }
    }
}
