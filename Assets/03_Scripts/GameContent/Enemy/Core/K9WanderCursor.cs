using UnityEngine;

namespace Pawntom.Enemy.Core
{
    /// <summary>
    /// 조사 중의 배회 커서. <b>기준점</b>과 <b>다음 지점을 고를 때가 됐는가</b> 만 안다.
    /// <para>
    /// 지점을 실제로 고르는 무작위성과 이동 가능 판정은
    /// <see cref="IK9WanderPointProvider"/> 뒤에 있다. 커서는 시점만 판단하므로
    /// 두뇌 없이도 결정적으로 검증된다(SRP·DIP).
    /// </para>
    /// <para>
    /// 두뇌가 필드로 하나만 들고 재사용한다. 틱마다 새로 만들지 않으므로 힙 할당이 없다.
    /// </para>
    /// </summary>
    public sealed class K9WanderCursor
    {
        // 지점 제공자. null 이면 배회하지 않고 제자리에 선다.
        private readonly IK9WanderPointProvider _provider;

        // 조사 배회의 기준점. 배회는 이 점 주변에서만 일어난다.
        private Vector3 _anchor;

        private float _waitTimer;

        /// <param name="provider">배회 지점 제공자. null 이면 배회하지 않는다.</param>
        public K9WanderCursor(IK9WanderPointProvider provider)
        {
            _provider = provider;
        }

        /// <summary>현재 배회 기준점.</summary>
        public Vector3 Anchor
        {
            get { return _anchor; }
        }

        /// <summary>기준점을 옮기고 대기 타이머를 되돌린다. 조사 목표가 바뀔 때마다 부른다.</summary>
        public void SetAnchor(Vector3 anchor)
        {
            _anchor = anchor;
            _waitTimer = 0f;
        }

        /// <summary>
        /// 다음 배회 지점을 골랐으면 true.
        /// <para>
        /// <b>무감지 타이머와 무관하다.</b> 배회가 조사 상태의 체류 시간을 늘리면
        /// 개가 조사 상태를 영원히 떠나지 못한다 — 그 타이머는 두뇌만 만진다.
        /// </para>
        /// </summary>
        /// <param name="deltaTime">흐른 시간(초).</param>
        /// <param name="hasArrived">이동 담당이 목적지에 닿았는가. 가는 중이면 다음 지점을 고르지 않는다.</param>
        /// <param name="intervalSeconds">지점에 도달한 뒤 다음 지점을 고르기까지의 대기 시간(초).</param>
        /// <param name="radius">기준점에서 배회할 수 있는 반경(m).</param>
        /// <param name="point">고른 지점. true 일 때만 유효하다.</param>
        public bool TryAdvance(
            float deltaTime, bool hasArrived, float intervalSeconds, float radius, out Vector3 point)
        {
            point = default(Vector3);

            if (_provider == null)
            {
                return false;
            }

            // 아직 가는 중이면 다음 지점을 고르지 않는다. 대기는 도착한 뒤부터 센다.
            if (!hasArrived)
            {
                _waitTimer = 0f;
                return false;
            }

            _waitTimer += deltaTime;
            if (_waitTimer < intervalSeconds)
            {
                return false;
            }

            _waitTimer = 0f;

            return _provider.TryGetPoint(_anchor, radius, out point);
        }

        /// <summary>
        /// 소집 좌표를 그대로 쓰지 않고 반경 안으로 흩어 준다.
        /// <para>
        /// 같은 좌표를 받은 동료들이 한 점에 몰려 서로 막는 것을 피한다.
        /// 반경이 0 이하거나 지점 제공자가 없거나 지점을 못 고르면 받은 좌표를 그대로 돌려준다.
        /// </para>
        /// <para>
        /// <b>소집 좌표에만 쓴다.</b> 흔적·시야·마지막 접촉 좌표는 이미 개별 좌표라 흩을 이유가 없다.
        /// </para>
        /// </summary>
        public Vector3 Spread(Vector3 summon, float radius)
        {
            if (radius <= 0f || _provider == null)
            {
                return summon;
            }

            Vector3 point;
            if (!_provider.TryGetPoint(summon, radius, out point))
            {
                return summon;
            }

            return point;
        }
    }
}
