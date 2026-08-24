using System.Collections.Generic;
using Pawntom.Enemy.Core;
using UnityEngine;

namespace Pawntom.Enemy.Adapters
{
    /// <summary>
    /// 하울링 송수신 어댑터.
    /// <para>
    /// 활성화된 채널끼리 정적 목록으로 서로를 알며, 반경 안의 동료에게만 소집 좌표를 넘긴다.
    /// 소집을 받은 쪽은 다음 <c>Tick</c> 에서 한 번만 소비한다.
    /// </para>
    /// <para>
    /// 브루투스 등 다른 청취자가 생기면 이 클래스를 고치지 말고
    /// <see cref="IK9AlertChannel"/> 의 다른 구현을 붙이거나 청취자 등록을 확장한다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Pawntom/Enemy/K9 Alert Channel")]
    public sealed class K9AlertChannelBehaviour : MonoBehaviour, IK9AlertChannel
    {
        private static readonly List<K9AlertChannelBehaviour> ActiveChannels =
            new List<K9AlertChannelBehaviour>(16);

        private Transform _transform;
        private bool _hasSummon;
        private Vector3 _summonTarget;

        /// <inheritdoc/>
        public void Broadcast(Vector3 origin, float radius)
        {
            float sqrRadius = radius * radius;

            for (int i = 0; i < ActiveChannels.Count; i++)
            {
                K9AlertChannelBehaviour channel = ActiveChannels[i];
                if (channel == null || ReferenceEquals(channel, this))
                {
                    continue;
                }

                if ((channel.CurrentPosition - origin).sqrMagnitude > sqrRadius)
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

        private Vector3 CurrentPosition
        {
            get { return _transform != null ? _transform.position : transform.position; }
        }

        private void Awake()
        {
            _transform = transform;
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
