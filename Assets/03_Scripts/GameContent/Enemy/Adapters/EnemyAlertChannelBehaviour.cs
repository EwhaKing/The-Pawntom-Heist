using System.Collections.Generic;
using Pawntom.Enemy.Core;
using UnityEngine;

namespace Pawntom.Enemy.Adapters
{
    /// <summary>
    /// 하울링 송수신 어댑터.
    /// <para>
    /// 활성화된 채널끼리 정적 목록으로 서로를 알며, 소집 좌표를 <b>듣겠다고 답한</b> 동료에게 넘긴다.
    /// 소집을 받은 쪽은 다음 <c>Tick</c> 에서 한 번만 소비한다.
    /// </para>
    /// <para>
    /// <b>거리 판정은 여기 없다.</b> "얼마나 멀리까지 들리는가"는 듣는 쪽의 속성이므로
    /// <see cref="IEnemyHearingPolicy"/> 가 답한다. 브루투스처럼 반경을 무시하는 청취자가
    /// 필요하면 이 클래스를 고치지 말고 <see cref="EnemyHearingPolicyBehaviour"/> 를 상속한
    /// 부품을 같은 오브젝트에 붙인다(OCP).
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Pawntom/Enemy/Alert Channel")]
    public sealed class EnemyAlertChannelBehaviour : MonoBehaviour, IEnemyAlertChannel
    {
        private static readonly List<EnemyAlertChannelBehaviour> ActiveChannels =
            new List<EnemyAlertChannelBehaviour>(16);

        // 기본 청력을 만드는 유일한 자리. 상태가 없으므로 모든 채널이 하나를 나눠 쓴다.
        private static readonly RangedHearingPolicy DefaultHearing = new RangedHearingPolicy();

        private Transform _transform;
        private IEnemyHearingPolicy _hearing;
        private bool _hasSummon;
        private Vector3 _summonTarget;

        /// <inheritdoc/>
        public void Broadcast(Vector3 origin, float radius)
        {
            for (int i = 0; i < ActiveChannels.Count; i++)
            {
                EnemyAlertChannelBehaviour channel = ActiveChannels[i];
                if (channel == null || ReferenceEquals(channel, this))
                {
                    continue;
                }

                // 들리는지 아닌지는 듣는 쪽이 답한다. 말하는 쪽은 반경을 제시만 한다.
                if (!channel.CanHear(origin, radius))
                {
                    continue;
                }

                channel.Receive(origin);
            }
        }

        /// <inheritdoc/>
        public bool TryConsumeSummon(out Vector3 target)
        {
            if (_hasSummon)
            {
                target = _summonTarget;
                _hasSummon = false;
                return true;
            }

            target = Vector3.zero;
            return false;
        }

        /// <summary>소집 좌표를 받아 둔다. 여러 번 오면 마지막 것이 남는다.</summary>
        public void Receive(Vector3 target)
        {
            _hasSummon = true;
            _summonTarget = target;
        }

        /// <summary>이 채널에게 소집이 들리는가. 판단은 전부 정책에 맡긴다.</summary>
        private bool CanHear(Vector3 origin, float radius)
        {
            return _hearing.CanHear(CurrentPosition, origin, radius);
        }

        private Vector3 CurrentPosition
        {
            get { return _transform != null ? _transform.position : transform.position; }
        }

        private void Awake()
        {
            _transform = transform;

            // 채널은 정적 목록에 OnEnable 로만 들어가고 Awake 는 그보다 먼저 돈다.
            // 따라서 Broadcast 가 순회하는 채널은 이미 청력이 정해져 있다.
            EnemyHearingPolicyBehaviour provider = GetComponent<EnemyHearingPolicyBehaviour>();
            _hearing = provider != null && provider.Policy != null ? provider.Policy : DefaultHearing;
        }

        private void OnEnable()
        {
            if (!ActiveChannels.Contains(this))
            {
                ActiveChannels.Add(this);
            }
        }

        private void OnDisable()
        {
            ActiveChannels.Remove(this);
        }
    }
}
