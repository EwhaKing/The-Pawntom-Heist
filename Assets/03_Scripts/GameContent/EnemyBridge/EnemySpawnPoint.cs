using Fusion;
using Pawntom.Enemy.Authoring;
using UnityEngine;

namespace Pawntom.EnemyBridge
{
    /// <summary>
    /// 씬에 놓는 적 스폰 마커.
    /// <para>
    /// 적 배치를 바꾸려고 개발자가 만지는 것은 <b>이 컴포넌트 하나여야 한다.</b>
    /// 마커를 옮기면 스폰 위치가, 마커를 돌리면 시작 방향이 바뀐다.
    /// 적을 늘리려면 마커를 복제한다 — 코드도 인스펙터 목록도 고칠 필요가 없다.
    /// </para>
    /// <para>
    /// 이 파일은 Fusion 타입(<see cref="NetworkPrefabRef"/>)을 만지므로
    /// <c>Pawntom.Enemy</c> 어셈블리에 들어갈 수 없다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Pawntom/Enemy/Enemy Spawn Point")]
    public sealed class EnemySpawnPoint : MonoBehaviour
    {
        [Header("스폰 대상")]
        [Tooltip("여기서 스폰할 적 프리팹. NetworkObject 가 붙어 있어야 한다")]
        [SerializeField] private NetworkPrefabRef _enemyPrefab;

        [Tooltip("이 개체가 걸어갈 경로. 비워 두면 제자리에 대기한다")]
        [SerializeField] private PatrolRoute _patrolRoute;

        [Header("씬 뷰 표시")]
        [Tooltip("기즈모로 그릴 적의 대략적인 반경. 프리팹 값을 에디트 모드에 읽을 수 없어 여기서 근사한다")]
        [SerializeField] private float _gizmoRadius = 0.5f;

        [Tooltip("기즈모로 그릴 적의 대략적인 키")]
        [SerializeField] private float _gizmoHeight = 2f;

        // 경로가 꽂힌 마커. 배치가 끝난 상태다.
        private static readonly Color RoutedColor = new Color(0.3f, 1f, 0.5f, 1f);

        // 경로가 비어 있는 마커. 제자리 대기하므로 눈에 띄어야 한다.
        private static readonly Color UnroutedColor = new Color(1f, 0.55f, 0.15f, 1f);

        // 시작 방향 화살표.
        private static readonly Color FacingColor = new Color(1f, 0.95f, 0.4f, 1f);

        /// <summary>여기서 스폰할 적 프리팹.</summary>
        public NetworkPrefabRef EnemyPrefab
        {
            get { return _enemyPrefab; }
        }

        /// <summary>스폰된 개체에 주입할 순찰 경로. 비어 있을 수 있다.</summary>
        public PatrolRoute PatrolRoute
        {
            get { return _patrolRoute; }
        }

        /// <summary>
        /// 배치를 눈으로 확인하는 것이 이 컴포넌트의 핵심 가치다.
        /// <para>
        /// 에디터 전용 경로라 할당 규칙을 엄격히 적용하지 않는다. 다만
        /// <c>UnityEditor.Handles</c> 는 쓰지 않는다 — 이 파일은 런타임 어셈블리에 있다.
        /// </para>
        /// </summary>
        private void OnDrawGizmos()
        {
            Vector3 origin = transform.position;
            float radius = Mathf.Max(0.01f, _gizmoRadius);
            float height = Mathf.Max(radius * 2f, _gizmoHeight);

            Gizmos.color = _patrolRoute == null ? UnroutedColor : RoutedColor;
            DrawWireCapsule(origin, radius, height);

            Gizmos.color = FacingColor;
            DrawFacingArrow(origin, radius, height);

            if (_patrolRoute == null)
            {
                return;
            }

            DrawRouteLink(origin, height);
        }

        /// <summary>위·아래 와이어 스피어와 그것을 잇는 네 개의 세로선으로 몸집을 그린다.</summary>
        private void DrawWireCapsule(Vector3 origin, float radius, float height)
        {
            Vector3 bottom = origin + Vector3.up * radius;
            Vector3 top = origin + Vector3.up * (height - radius);

            Gizmos.DrawWireSphere(bottom, radius);
            Gizmos.DrawWireSphere(top, radius);

            Vector3 right = transform.right * radius;
            Vector3 forward = transform.forward * radius;

            Gizmos.DrawLine(bottom + right, top + right);
            Gizmos.DrawLine(bottom - right, top - right);
            Gizmos.DrawLine(bottom + forward, top + forward);
            Gizmos.DrawLine(bottom - forward, top - forward);
        }

        /// <summary>시작 방향을 보여 주는 화살표. 마커를 돌린 결과가 눈에 보여야 한다.</summary>
        private void DrawFacingArrow(Vector3 origin, float radius, float height)
        {
            Vector3 start = origin + Vector3.up * (height * 0.5f);
            float length = Mathf.Max(radius * 3f, 1f);
            Vector3 tip = start + transform.forward * length;

            Gizmos.DrawLine(start, tip);

            float barbLength = length * 0.25f;
            Vector3 back = -transform.forward * barbLength;
            Vector3 side = transform.right * barbLength * 0.5f;

            Gizmos.DrawLine(tip, tip + back + side);
            Gizmos.DrawLine(tip, tip + back - side);
        }

        /// <summary>어느 경로에 물려 있는지 눈으로 확인하는 선. 첫 웨이포인트까지 잇는다.</summary>
        private void DrawRouteLink(Vector3 origin, float height)
        {
            var waypoints = _patrolRoute.Waypoints;
            if (waypoints == null)
            {
                return;
            }

            for (int i = 0; i < waypoints.Count; i++)
            {
                Transform waypoint = waypoints[i];
                if (waypoint == null)
                {
                    continue;
                }

                Gizmos.color = _patrolRoute.LineColor;
                Gizmos.DrawLine(origin + Vector3.up * (height * 0.5f), waypoint.position);
                return;
            }
        }
    }
}
