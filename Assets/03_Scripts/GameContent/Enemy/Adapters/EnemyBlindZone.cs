using System.Collections.Generic;
using UnityEngine;

namespace Pawntom.Enemy.Adapters
{
    /// <summary>
    /// 이 영역 안에 있는 대상은 경비견의 시야·접촉 감지에 잡히지 않는다.
    /// <para>
    /// 튜토리얼 금고방의 환풍구 엿보기 자리(금고 위)에 둔다 — 환풍구 너머로 바깥을 살피는 동안
    /// 순찰 경비견이 플레이어를 인지하면 안 된다. 물리 레이로 막지 않는 이유: 경비견 시야의 차폐 레이어가
    /// 비어 있어(<c>SightPerceptionSource._obstacleMask</c>) 벽·창틀로는 시야가 끊기지 않기 때문이다.
    /// </para>
    /// <para>
    /// 물리 트리거 이벤트를 쓰지 않는다. 감지 쪽이 <see cref="IsHidden"/> 으로 주기적으로 묻는다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    [AddComponentMenu("Pawntom/Enemy/Enemy Blind Zone")]
    public sealed class EnemyBlindZone : MonoBehaviour
    {
        private static readonly List<EnemyBlindZone> ActiveZones = new List<EnemyBlindZone>(4);

        [SerializeField] private Color _gizmoColor = new Color(0.4f, 0.6f, 1f, 0.25f);

        private Collider _collider;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            ActiveZones.Clear();
        }

        /// <summary>월드 좌표 <paramref name="worldPoint"/> 가 어느 시야 차단 영역 안에 있는가.</summary>
        public static bool IsHidden(Vector3 worldPoint)
        {
            for (int i = 0; i < ActiveZones.Count; i++)
            {
                if (ActiveZones[i].Contains(worldPoint))
                {
                    return true;
                }
            }

            return false;
        }

        private void Awake()
        {
            _collider = GetComponent<Collider>();
        }

        private void Reset()
        {
            Collider own = GetComponent<Collider>();

            if (own != null)
            {
                own.isTrigger = true;
            }
        }

        private void OnEnable()
        {
            if (!ActiveZones.Contains(this))
            {
                ActiveZones.Add(this);
            }
        }

        private void OnDisable()
        {
            ActiveZones.Remove(this);
        }

        private bool Contains(Vector3 worldPoint)
        {
            if (_collider == null || !_collider.enabled)
            {
                return false;
            }

            Vector3 closest = _collider.ClosestPoint(worldPoint);
            return (closest - worldPoint).sqrMagnitude < 0.0001f;
        }

        private void OnDrawGizmos()
        {
            Collider own = _collider != null ? _collider : GetComponent<Collider>();

            if (own == null)
            {
                return;
            }

            Bounds bounds = own.bounds;
            Gizmos.color = _gizmoColor;
            Gizmos.DrawCube(bounds.center, bounds.size);
            Gizmos.color = new Color(_gizmoColor.r, _gizmoColor.g, _gizmoColor.b, 1f);
            Gizmos.DrawWireCube(bounds.center, bounds.size);
        }
    }
}
