using System;

namespace Pawntom.Stage
{
    /// <summary>
    /// 시드 기반 결정적 난수(xorshift64*, 초기 상태는 SplitMix64 로 섞는다).
    /// <para>
    /// 배치 판정은 이 클래스만 쓴다. 호스트가 시드 하나를 정해 공유하면 모든 피어가 같은 순서로 같은 값을 얻는다.
    /// <c>UnityEngine.Random</c>·시드 없는 <c>System.Random</c> 은 피어마다 값이 달라 판정에 쓰지 않는다(fusion-network 규칙).
    /// 알고리즘을 바꾸면 같은 시드의 배치가 바뀐다 — 저장된 시드·리플레이가 생기면 바꾸지 않는다.
    /// </para>
    /// </summary>
    public sealed class StageRandom
    {
        private const ulong GoldenGamma = 0x9E3779B97F4A7C15UL;
        private const float UnitScale = 1f / 16777216f; // 2^-24

        private ulong _state;

        public StageRandom(int seed)
        {
            _state = SplitMix64(unchecked((ulong)(uint)seed));

            // xorshift 는 상태 0 에서 영원히 0 을 낸다.
            if (_state == 0UL)
            {
                _state = GoldenGamma;
            }
        }

        /// <summary>32비트 난수.</summary>
        public uint NextUInt()
        {
            unchecked
            {
                ulong x = _state;
                x ^= x >> 12;
                x ^= x << 25;
                x ^= x >> 27;
                _state = x;
                return (uint)((x * 0x2545F4914F6CDD1DUL) >> 32);
            }
        }

        /// <summary>[0, <paramref name="maxExclusive"/>) 정수.</summary>
        public int NextInt(int maxExclusive)
        {
            if (maxExclusive <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxExclusive), maxExclusive, "maxExclusive 는 1 이상이어야 한다.");
            }

            // 곱셈-시프트 축소: 나머지 연산보다 치우침이 작고 분기 없이 결정적이다.
            return (int)(((ulong)NextUInt() * (ulong)maxExclusive) >> 32);
        }

        /// <summary>[0, 1) 실수. 상위 24비트를 쓰므로 float 정밀도 안에서 균등하다.</summary>
        public float NextFloat01()
        {
            return (NextUInt() >> 8) * UnitScale;
        }

        private static ulong SplitMix64(ulong x)
        {
            unchecked
            {
                x += GoldenGamma;
                x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL;
                x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL;
                return x ^ (x >> 31);
            }
        }
    }
}
