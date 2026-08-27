using System.Collections.Generic;
using Pawntom.Enemy.Core;
using UnityEngine;

namespace Pawntom.Enemy.Adapters
{
    /// <summary>
    /// 대상 제공자에게서 <b>마지막으로 알던 좌표에 가장 가까운</b> 대상을 골라
    /// 그 대상의 현재 좌표를 돌려준다.
    /// <para>
    /// 시야 판정을 하지 않는다. 시야 밖 추격(TASK-005 요구 3) 전용 통로이고,
    /// 추격을 끊는 판단은 두뇌의 시야 소실 타이머가 한다(SRP).
    /// </para>
    /// </summary>
    public sealed class NearestTargetTracker : IK9TargetTracker
    {
        private IK9TargetProvider _targetProvider;

        /// <summary>
        /// 대상 제공자를 갈아 끼운다.
        /// <para>
        /// 제공자는 씬 초기화 순서에 따라 뒤늦게 해결될 수 있으므로
        /// 생성자에서 요구하지 않고 이 메서드로 재주입을 받는다.
        /// </para>
        /// </summary>
        public void Configure(IK9TargetProvider targetProvider)
        {
            _targetProvider = targetProvider;
        }

        /// <inheritdoc/>
        public bool TryTrack(Vector3 lastKnownPosition, out Vector3 currentPosition)
        {
            if (_targetProvider == null)
            {
                currentPosition = default(Vector3);
                return false;
            }

            IReadOnlyList<Transform> targets = _targetProvider.Targets;
            if (targets == null || targets.Count == 0)
            {
                currentPosition = default(Vector3);
                return false;
            }

            bool found = false;
            float bestSqr = 0f;
            Vector3 best = default(Vector3);

            // foreach 를 쓰면 열거자 할당이 생기므로 인덱스로 돈다.
            for (int i = 0; i < targets.Count; i++)
            {
                Transform target = targets[i];
                if (target == null)
                {
                    continue;
                }

                Vector3 position = target.position;

                // 제곱 거리로만 비교한다. 제곱근을 구할 이유가 없다.
                float sqr = (position - lastKnownPosition).sqrMagnitude;
                if (!found || sqr < bestSqr)
                {
                    found = true;
                    bestSqr = sqr;
                    best = position;
                }
            }

            if (!found)
            {
                currentPosition = default(Vector3);
                return false;
            }

            currentPosition = best;
            return true;
        }
    }
}
