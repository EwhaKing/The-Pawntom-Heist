namespace Pawntom.Enemy.Core
{
    /// <summary>
    /// 하울링 이후의 경과 시계. <b>"짖은 적이 있는가"</b> 와 <b>"짖은 지 얼마나 됐는가"</b> 를 분리한다.
    /// <para>
    /// <c>float.PositiveInfinity</c> 로 "아직 안 짖었음" 을 표현하지 않는다 —
    /// 무한대에 흐른 시간을 계속 더하는 누적을 만들지 않기 위해 플래그로 나눠 둔다.
    /// </para>
    /// <para>
    /// <see cref="K9SightMemory"/> 와 같은 모양(타이머 + 유효 플래그)이다.
    /// 두뇌가 필드로 하나만 들고 재사용하므로 힙 할당이 없다.
    /// </para>
    /// </summary>
    public sealed class K9HowlClock
    {
        private float _secondsSinceHowl;
        private bool _hasHowled;

        /// <summary>한 번이라도 짖은 적이 있는가.</summary>
        public bool HasHowled
        {
            get { return _hasHowled; }
        }

        /// <summary>마지막 하울링 이후 흐른 시간(초). 짖은 적이 없으면 값의 의미가 없다.</summary>
        public float SecondsSinceHowl
        {
            get { return _secondsSinceHowl; }
        }

        /// <summary>
        /// 매 틱 한 번 호출한다. 경과 시간은 상태와 무관하게 흐른다.
        /// <para>
        /// <b>한 번도 짖지 않았으면 누적하지 않는다.</b> 짖기 전의 경과 시간은 의미가 없고,
        /// 누적해 두면 첫 하울링 전에 이미 쿨다운이 지난 것처럼 읽힐 여지가 생긴다.
        /// </para>
        /// <para>
        /// 0 이하의 시간은 호출부(두뇌)가 이미 걸러 낸다.
        /// </para>
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (!_hasHowled)
            {
                return;
            }

            _secondsSinceHowl += deltaTime;
        }

        /// <summary>
        /// 방금 짖었다고 표시한다.
        /// <para>
        /// 소집을 실제로 보냈는지와 무관하게 부른다 — 이 시계가 재는 것은
        /// "동료를 불렀는가" 가 아니라 "방금 짖어서 대상을 놓쳤는가" 다.
        /// </para>
        /// </summary>
        public void MarkHowled()
        {
            _secondsSinceHowl = 0f;
            _hasHowled = true;
        }

        /// <summary>
        /// 마지막 하울링으로부터 <paramref name="seconds"/> 안쪽인가.
        /// <para>
        /// 한 번도 짖지 않았으면 언제나 false 다. 경계값에서도 false —
        /// 정확히 <paramref name="seconds"/> 가 흘렀으면 쿨다운은 끝난 것으로 본다.
        /// </para>
        /// </summary>
        public bool HowledWithin(float seconds)
        {
            return _hasHowled && _secondsSinceHowl < seconds;
        }
    }
}
