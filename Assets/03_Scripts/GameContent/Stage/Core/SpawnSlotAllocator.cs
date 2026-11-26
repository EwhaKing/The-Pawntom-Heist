using System.Collections.Generic;

namespace Pawntom.Stage
{
    /// <summary>
    /// 플레이어 스폰 지점 배정(호스트 전용, 로컬 상태). 사용 중인 지점을 추적해 비어 있는 가장 낮은 번호를 준다.
    /// <para>
    /// 입장 수를 인덱스로 쓰면 한 명이 나간 뒤 들어온 사람이 남아 있는 사람과 같은 지점을 받는다 — 그 결함을 막는다.
    /// 같은 플레이어가 다시 요청하면(재스폰) 같은 지점을 돌려준다. 플레이어 키는 Fusion 비의존 정수(PlayerRef.PlayerId)다.
    /// </para>
    /// </summary>
    public sealed class SpawnSlotAllocator
    {
        private readonly Dictionary<int, int> _slotByPlayer = new Dictionary<int, int>(4);
        private readonly List<bool> _used = new List<bool>(4);

        /// <summary>지점을 쥐고 있는 플레이어 수.</summary>
        public int UsedCount
        {
            get { return _slotByPlayer.Count; }
        }

        /// <summary>
        /// <paramref name="playerKey"/> 에게 지점을 준다. 이미 쥐고 있으면 그 지점, 아니면 비어 있는 가장 낮은 지점.
        /// 쥐고 있던 지점이 <paramref name="slotCount"/> 밖이면 놓고 다시 고른다. 빈 지점이 없으면 -1.
        /// </summary>
        public int Acquire(int playerKey, int slotCount)
        {
            if (_slotByPlayer.TryGetValue(playerKey, out int held))
            {
                if (held < slotCount)
                {
                    return held;
                }

                Release(playerKey);
            }

            for (int i = 0; i < slotCount; i++)
            {
                if (i < _used.Count && _used[i])
                {
                    continue;
                }

                while (_used.Count <= i)
                {
                    _used.Add(false);
                }

                _used[i] = true;
                _slotByPlayer[playerKey] = i;
                return i;
            }

            return -1;
        }

        /// <summary>플레이어의 지점을 놓는다. 쥐고 있지 않으면 아무 일도 없다.</summary>
        public void Release(int playerKey)
        {
            if (!_slotByPlayer.TryGetValue(playerKey, out int slot))
            {
                return;
            }

            _slotByPlayer.Remove(playerKey);

            if (slot >= 0 && slot < _used.Count)
            {
                _used[slot] = false;
            }
        }

        public bool TryGetSlot(int playerKey, out int slot)
        {
            return _slotByPlayer.TryGetValue(playerKey, out slot);
        }

        public void Clear()
        {
            _slotByPlayer.Clear();
            _used.Clear();
        }
    }
}
