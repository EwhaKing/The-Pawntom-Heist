namespace Pawntom.Stage
{
    /// <summary>
    /// 소형 환풍구 통과 규칙(엔진 비의존). GAME_DESIGN §3.2 카오스 삼색 "소형 환풍구 등 특수 통로 통과".
    /// <para>
    /// 이 어셈블리는 Assembly-CSharp 의 <c>CatType</c> 을 참조할 수 없으므로 정수 값으로 받는다.
    /// 호출하는 쪽이 <c>(int)CatType.CalicoCat</c> 을 넘긴다. 상태가 없으므로 캐릭터가 바뀌면 같은 규칙으로 다시 묻는다.
    /// </para>
    /// </summary>
    public sealed class VentAccessRule
    {
        private const int MaxTypeValue = 31;

        private readonly uint _allowedMask;

        /// <param name="allowedCatTypeValue">통과할 수 있는 캐릭터 값(0~31). 범위 밖이면 아무도 통과하지 못한다.</param>
        public VentAccessRule(int allowedCatTypeValue)
        {
            _allowedMask = Bit(allowedCatTypeValue);
        }

        /// <summary>이 캐릭터 값이 통로를 지나갈 수 있는가.</summary>
        public bool CanPass(int catTypeValue)
        {
            return (_allowedMask & Bit(catTypeValue)) != 0u;
        }

        /// <summary>
        /// 통로 차단 충돌체와 이 플레이어의 충돌을 무시해야 하는가. 플레이어 데이터를 아직 모르면 막아 둔다.
        /// </summary>
        public bool ShouldIgnoreCollision(bool hasPlayerData, int catTypeValue)
        {
            return hasPlayerData && CanPass(catTypeValue);
        }

        private static uint Bit(int value)
        {
            return value >= 0 && value <= MaxTypeValue ? 1u << value : 0u;
        }
    }
}
