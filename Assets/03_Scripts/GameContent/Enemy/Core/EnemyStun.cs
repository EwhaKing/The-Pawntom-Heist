using UnityEngine;

namespace Pawntom.Enemy.Core
{
    /// <summary>
    /// 적 한 마리의 스턴 타이머. 공격을 한 번 한 뒤 일정 시간 동안 멈춰 서게 할 때 쓴다.
    /// <para>
    /// 시간만 센다 — 멈추고 되돌리는 일은 이 타이머를 쥔 쪽(<c>EnemyAgent</c>)이 한다.
    /// 월드가 일시정지된 동안에는 <see cref="Tick"/> 을 부르지 않으므로 그 시간은 흐르지 않는다.
    /// </para>
    /// </summary>
    public sealed class EnemyStun
    {
        private float _remainingSeconds;

        public bool IsStunned
        {
            get { return _remainingSeconds > 0f; }
        }

        public float RemainingSeconds
        {
            get { return _remainingSeconds; }
        }

        /// <summary>
        /// 스턴을 건다. 이미 걸려 있으면 더 긴 쪽을 따른다(겹쳐 맞아도 시간이 쌓이지 않는다).
        /// 0 이하 · NaN · 무한대는 무시한다.
        /// </summary>
        public void Begin(float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds <= 0f)
            {
                return;
            }

            _remainingSeconds = Mathf.Max(_remainingSeconds, seconds);
        }

        /// <summary>시간을 흘린다.</summary>
        /// <returns>이번 틱 <b>뒤에도</b> 스턴 중이면 true. 풀리는 틱에는 false.</returns>
        public bool Tick(float deltaTime)
        {
            if (_remainingSeconds <= 0f)
            {
                return false;
            }

            _remainingSeconds = Mathf.Max(0f, _remainingSeconds - Mathf.Max(0f, deltaTime));
            return _remainingSeconds > 0f;
        }

        /// <summary>스턴을 즉시 푼다(적을 처음 상태로 되돌릴 때).</summary>
        public void Clear()
        {
            _remainingSeconds = 0f;
        }
    }
}
