using System.Collections.Generic;
using UnityEngine;

namespace Pawntom.Enemy.Core
{
    /// <summary>흔적 후보 하나. 구조체라 힙 할당이 없다.</summary>
    public readonly struct EnemyTraceCandidate
    {
        /// <summary>개체 식별자. 0 은 유효한 id 로 쓰지 않는다 — 수집하는 쪽에서 걸러 넣는다.</summary>
        public readonly int Id;

        /// <summary>흔적의 월드 좌표.</summary>
        public readonly Vector3 Position;

        /// <summary>생성 시점. 클수록 최근이다.</summary>
        public readonly int CreatedTick;

        public EnemyTraceCandidate(int id, Vector3 position, int createdTick)
        {
            Id = id;
            Position = position;
            CreatedTick = createdTick;
        }

        /// <summary>id 가 0 이 아니어야 후보로 쓸 수 있다.</summary>
        public bool IsValid
        {
            get { return Id != 0; }
        }
    }

    /// <summary>커서가 이번 틱에 내린 결정. 구조체라 힙 할당이 없다.</summary>
    public readonly struct EnemyTraceStep
    {
        /// <summary>조사할 목표가 있는가.</summary>
        public readonly bool HasTarget;

        /// <summary>목표 좌표. <see cref="HasTarget"/> 이 true 일 때만 유효하다.</summary>
        public readonly Vector3 Target;

        /// <summary>이번 틱에 도달해서 없애야 할 흔적이 있는가.</summary>
        public readonly bool HasConsumed;

        /// <summary>없앨 흔적의 id. <see cref="HasConsumed"/> 가 true 일 때만 유효하다.</summary>
        public readonly int ConsumedId;

        public EnemyTraceStep(bool hasTarget, Vector3 target, bool hasConsumed, int consumedId)
        {
            HasTarget = hasTarget;
            Target = target;
            HasConsumed = hasConsumed;
            ConsumedId = consumedId;
        }
    }

    /// <summary>
    /// 어느 흔적을 조사할지 고르고, 도달한 흔적을 소비 대상으로 알린다.
    /// <para>
    /// 이 클래스는 엔진 오브젝트도 네트워크도 모른다 — 좌표와 id 만 다룬다.
    /// 그래서 씬 없이 결정적으로 검증된다(SRP·DIP). 실제로 흔적을 지우는 일은
    /// 이 결정을 받아 가는 어댑터가 한다.
    /// </para>
    /// <para>
    /// 감지 소스가 필드로 하나만 들고 재사용한다. 틱마다 새로 만들지 않으므로 힙 할당이 없다.
    /// </para>
    /// </summary>
    public sealed class EnemyTraceCursor
    {
        // 지금 조사 중인 흔적. id 로만 들고 있으므로 좌표가 움직여도 따라간다.
        private bool _hasTarget;
        private int _targetId;

        /// <summary>조사 중인 흔적이 있는가.</summary>
        public bool HasTarget
        {
            get { return _hasTarget; }
        }

        /// <summary>조사 중인 흔적의 id. <see cref="HasTarget"/> 이 true 일 때만 유효하다.</summary>
        public int TargetId
        {
            get { return _targetId; }
        }

        /// <summary>목표를 강제로 해제한다.</summary>
        public void Clear()
        {
            _hasTarget = false;
            _targetId = 0;
        }

        /// <summary>
        /// 이번 틱의 후보로 목표를 갱신한다.
        /// <para>
        /// <paramref name="candidates"/> 는 호출부가 재사용하는 목록이며 이 메서드는 보관하지 않는다.
        /// <c>foreach</c>·LINQ·목록 생성을 쓰지 않는다 — 매 틱 도는 경로라 GC 할당을 만들지 않는다.
        /// </para>
        /// </summary>
        /// <param name="origin">개의 현재 위치.</param>
        /// <param name="candidates">이번 틱에 살아 있는 흔적 전부. 반경 판정은 이 안에서 한다.</param>
        /// <param name="detectionRadius">흔적을 발견하는 반경(m). 0 이하면 아무것도 고르지 않는다.</param>
        /// <param name="reachDistance">도달로 인정하는 거리(m).</param>
        public EnemyTraceStep Advance(
            Vector3 origin,
            IReadOnlyList<EnemyTraceCandidate> candidates,
            float detectionRadius,
            float reachDistance)
        {
            // 볼 수 있는 후보가 하나도 없으면 들고 있던 목표도 놓는다(판정 2 의 극단값).
            if (candidates == null || candidates.Count == 0 || detectionRadius <= 0f)
            {
                Clear();
                return default(EnemyTraceStep);
            }

            float sqrRadius = detectionRadius * detectionRadius;
            int count = candidates.Count;

            // 판정 1·2 — 반경 안에서 지금 목표를 다시 찾는다. 못 찾으면 목표를 해제한다.
            // 목표가 반경 밖으로 나간 경우도 여기서 같이 걸린다. 별도 분기를 두지 않는다.
            int targetIndex = -1;

            if (_hasTarget)
            {
                for (int i = 0; i < count; i++)
                {
                    EnemyTraceCandidate candidate = candidates[i];

                    if (!candidate.IsValid || candidate.Id != _targetId)
                    {
                        continue;
                    }

                    if (HorizontalSqrDistance(candidate.Position, origin) <= sqrRadius)
                    {
                        targetIndex = i;
                    }

                    break;
                }

                if (targetIndex < 0)
                {
                    Clear();
                }
            }

            // 판정 3 — 목표에 도달했으면 소비 대상으로 알리고 목표를 놓는다.
            // 실제 삭제는 어댑터가 이 호출 뒤에 하므로, 소비한 항목은 아직 candidates 에 남아 있다.
            bool hasConsumed = false;
            int consumedId = 0;

            if (targetIndex >= 0)
            {
                float sqrReach = reachDistance * reachDistance;

                if (HorizontalSqrDistance(candidates[targetIndex].Position, origin) <= sqrReach)
                {
                    hasConsumed = true;
                    consumedId = candidates[targetIndex].Id;
                    targetIndex = -1;
                    Clear();
                }
            }

            // 판정 4 — 목표가 없으면 반경 안에서 가장 최근 것을 고른다. 같으면 더 가까운 것.
            // 이번 호출에서 소비한 id 하나만 제외한다. 과거에 소비한 id 는 누적하지 않는다 —
            // 삭제되면 다음 틱의 후보 목록에서 알아서 빠진다.
            if (targetIndex < 0)
            {
                int bestTick = int.MinValue;
                float bestSqr = float.MaxValue;

                for (int i = 0; i < count; i++)
                {
                    EnemyTraceCandidate candidate = candidates[i];

                    if (!candidate.IsValid)
                    {
                        continue;
                    }

                    if (hasConsumed && candidate.Id == consumedId)
                    {
                        continue;
                    }

                    float sqrDistance = HorizontalSqrDistance(candidate.Position, origin);
                    if (sqrDistance > sqrRadius)
                    {
                        continue;
                    }

                    if (candidate.CreatedTick < bestTick)
                    {
                        continue;
                    }

                    if (candidate.CreatedTick == bestTick && sqrDistance >= bestSqr)
                    {
                        continue;
                    }

                    bestTick = candidate.CreatedTick;
                    bestSqr = sqrDistance;
                    targetIndex = i;
                }

                if (targetIndex >= 0)
                {
                    _hasTarget = true;
                    _targetId = candidates[targetIndex].Id;
                }
            }

            // 판정 5 — 목표가 있으면 좌표를 내보낸다.
            if (targetIndex < 0)
            {
                return new EnemyTraceStep(false, Vector3.zero, hasConsumed, consumedId);
            }

            return new EnemyTraceStep(true, candidates[targetIndex].Position, hasConsumed, consumedId);
        }

        /// <summary>
        /// 수평(XZ) 제곱 거리. 높이차는 버린다.
        /// <para>
        /// 흔적은 늘 바닥에 붙어 있고 개의 원점은 늘 그 위에 있다 —
        /// 내비메시 에이전트의 baseOffset 이 트랜스폼 스케일만큼 곱해져 뜨기 때문이다.
        /// (엔진 타입 이름을 그대로 적지 않는다 — Core 폴더는 엔진 타입 참조 금지 검사를 받는다)
        /// (K-9_Prefab 기준 실효 3m, 털공은 0.3m → 높이차 2.7m)
        /// 이 높이차를 거리에 섞으면 도달 판정이 성립하지 못한다.
        /// </para>
        /// </summary>
        private static float HorizontalSqrDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return dx * dx + dz * dz;
        }
    }
}
