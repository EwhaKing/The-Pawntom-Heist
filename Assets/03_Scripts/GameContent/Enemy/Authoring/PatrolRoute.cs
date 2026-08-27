using System.Collections.Generic;
using UnityEngine;

namespace Pawntom.Enemy.Authoring
{
    /// <summary>
    /// 씬에 배치하는 순찰 경로.
    /// <para>
    /// 경로는 코드에 박지 않는다. 빈 오브젝트를 웨이포인트로 놓고 순서대로 끌어다 넣으면
    /// 그 경로가 이 컴포넌트의 값이 된다. K-9 개체마다 서로 다른 경로를 지정할 수 있다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Pawntom/Enemy/Patrol Route")]
    public sealed class PatrolRoute : MonoBehaviour
    {
        [Header("경로")]
        [Tooltip("순찰 지점을 지나갈 순서대로 넣는다. 비워 두면 K-9 은 제자리에 대기한다")]
        [SerializeField] private List<Transform> _waypoints = new List<Transform>();

        [Tooltip("마지막 지점 다음에 첫 지점으로 돌아갈지 여부")]
        [SerializeField] private bool _loop = true;

        [Header("씬 뷰 표시")]
        [SerializeField] private Color _lineColor = new Color(0.2f, 0.9f, 1f, 1f);
        [SerializeField] private float _handleRadius = 0.25f;

        private readonly List<Vector3> _positionCache = new List<Vector3>(8);

        /// <summary>마지막 지점에서 첫 지점으로 돌아가는가.</summary>
        public bool Loop
        {
            get { return _loop; }
        }

        /// <summary>등록된 웨이포인트 개수(빈 슬롯 포함).</summary>
        public int WaypointCount
        {
            get { return _waypoints.Count; }
        }

        /// <summary>씬 뷰 표시용 색.</summary>
        public Color LineColor
        {
            get { return _lineColor; }
        }

        /// <summary>씬 뷰 표시용 반경.</summary>
        public float HandleRadius
        {
            get { return _handleRadius; }
        }

        /// <summary>인스펙터에 꽂힌 웨이포인트들. 편집기 표시에 쓴다.</summary>
        public IReadOnlyList<Transform> Waypoints
        {
            get { return _waypoints; }
        }

        /// <summary>
        /// 웨이포인트 좌표를 순서대로 돌려준다.
        /// 내부 목록을 재사용하므로 반환값을 보관해 두고 써도 된다.
        /// </summary>
        public IReadOnlyList<Vector3> GetWaypointPositions()
        {
            RebuildCache();
            return _positionCache;
        }

        /// <summary>웨이포인트가 움직였을 때 좌표 캐시를 다시 만든다.</summary>
        public void RebuildCache()
        {
            _positionCache.Clear();

            for (int i = 0; i < _waypoints.Count; i++)
            {
                Transform waypoint = _waypoints[i];
                if (waypoint == null)
                {
                    continue;
                }

                _positionCache.Add(waypoint.position);
            }
        }

        private void OnDrawGizmos()
        {
            if (_waypoints.Count == 0)
            {
                return;
            }

            Gizmos.color = _lineColor;

            for (int i = 0; i < _waypoints.Count; i++)
            {
                Transform current = _waypoints[i];
                if (current == null)
                {
                    continue;
                }

                Gizmos.DrawWireSphere(current.position, _handleRadius);

                int nextIndex = i + 1;
                if (nextIndex >= _waypoints.Count)
                {
                    if (!_loop)
                    {
                        continue;
                    }

                    nextIndex = 0;
                }

                Transform next = _waypoints[nextIndex];
                if (next == null)
                {
                    continue;
                }

                Gizmos.DrawLine(current.position, next.position);
            }
        }
    }
}
