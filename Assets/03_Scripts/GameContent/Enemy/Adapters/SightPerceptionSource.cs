using System.Collections.Generic;
using Pawntom.Enemy.Authoring;
using Pawntom.Enemy.Core;
using UnityEngine;

namespace Pawntom.Enemy.Adapters
{
    /// <summary>
    /// 시야 감지. 거리·각도·차폐를 확인해 가장 가까운 대상을 보고한다.
    /// 보고 종류는 <see cref="K9DetectionKind.Sight"/> 이며, 추격 전환의 유일한 근거다.
    /// </summary>
    [AddComponentMenu("Pawntom/Enemy/K9 Sight Perception Source")]
    public sealed class SightPerceptionSource : K9PerceptionSourceBehaviour
    {
        [Header("시점")]
        [Tooltip("시야의 기준이 되는 지점. 비우면 자기 자신을 쓴다(머리 위치를 권장)")]
        [SerializeField] private Transform _eye;

        [Header("차폐")]
        [Tooltip("시야를 가리는 레이어. 비워 두면(Nothing) 차폐 판정을 하지 않는다")]
        [SerializeField] private LayerMask _obstacleMask = 0;

        [Tooltip("대상 바로 앞에서 레이를 끊는 여유 거리(m). 대상 자신의 충돌체에 걸리는 것을 막는다")]
        [SerializeField] private float _obstacleSkin = 0.2f;

        private Transform _eyeTransform;

        /// <inheritdoc/>
        public override bool TryDetect(out K9Detection detection)
        {
            detection = default(K9Detection);

            if (!IsReady || _eyeTransform == null)
            {
                return false;
            }

            IReadOnlyList<Transform> targets = TargetProvider.Targets;
            if (targets == null || targets.Count == 0)
            {
                return false;
            }

            float range = Settings.Perception.SightRange;
            float sqrRange = range * range;
            float halfAngle = Settings.Perception.SightAngle * 0.5f;

            Vector3 eyePosition = _eyeTransform.position;
            Vector3 forward = _eyeTransform.forward;

            bool found = false;
            float bestSqr = float.MaxValue;
            Vector3 bestPosition = Vector3.zero;

            for (int i = 0; i < targets.Count; i++)
            {
                Transform target = targets[i];
                if (target == null)
                {
                    continue;
                }

                Vector3 targetPosition = target.position;
                Vector3 toTarget = targetPosition - eyePosition;
                float sqrDistance = toTarget.sqrMagnitude;

                if (sqrDistance > sqrRange || sqrDistance >= bestSqr)
                {
                    continue;
                }

                // 각도는 월드 수평면 기준이다(TASK-007 2.3 원인 C).
                // 3D 각도로 재면 눈이 대상보다 위에 있을 때 높이 차가 각도로 새어 들어와,
                // 씬 뷰 부채꼴 정중앙에 있는 대상이 근거리에서 탈락한다.
                // 거리·차폐 판정은 3D 그대로다.
                if (sqrDistance > 0.0001f
                    && !K9SightGeometry.IsWithinHorizontalCone(forward, toTarget, halfAngle))
                {
                    continue;
                }

                if (IsBlocked(eyePosition, toTarget, sqrDistance))
                {
                    continue;
                }

                bestSqr = sqrDistance;
                bestPosition = targetPosition;
                found = true;
            }

            if (found)
            {
                detection = K9Detection.Sight(bestPosition);
            }

            return found;
        }

        private bool IsBlocked(Vector3 eyePosition, Vector3 toTarget, float sqrDistance)
        {
            if (_obstacleMask.value == 0)
            {
                return false;
            }

            float distance = Mathf.Sqrt(sqrDistance) - _obstacleSkin;
            if (distance <= 0f)
            {
                return false;
            }

            return Physics.Raycast(
                eyePosition,
                toTarget.normalized,
                distance,
                _obstacleMask,
                QueryTriggerInteraction.Ignore);
        }

        private void Awake()
        {
            _eyeTransform = _eye != null ? _eye : transform;
        }

#if UNITY_EDITOR
        // 연한 빨강 — TASK-003 2.2 C-1. 채움과 외곽선을 구분해 그린다.
        private static readonly Color GizmoFill = new Color(1f, 0.3f, 0.3f, 0.08f);
        private static readonly Color GizmoOutline = new Color(1f, 0.3f, 0.3f, 0.5f);

        // 부채꼴을 선분으로 근사할 때의 분할 수. 지시서 하한은 20 이다.
        private const int GizmoSegments = 24;

        /// <summary>
        /// 시야 범위를 씬 뷰에 <b>상시</b> 표시한다. 선택하지 않아도, Play 를 누르지 않아도 보인다.
        /// 배치 작업 중에 보려는 것이므로 <c>OnDrawGizmosSelected</c> 가 아니라 여기에 둔다.
        /// </summary>
        private void OnDrawGizmos()
        {
            K9Settings settings = ResolveSettings();
            if (settings == null)
            {
                return;
            }

            Transform eye = _eye != null ? _eye : transform;
            DrawSightCone(eye, settings.Perception.SightRange, settings.Perception.SightAngle);
        }

        /// <summary>
        /// 기즈모가 읽을 수치를 찾는다.
        /// <para>
        /// <c>Configure</c> 로 주입된 값이 있으면(Play 중) 그것을 쓰고,
        /// 없으면(에디트 모드) 같은 오브젝트의 <see cref="K9Agent"/> 에서 읽는다.
        /// 두 경로는 같은 <see cref="K9Settings"/> 인스턴스를 가리키므로 값이 어긋날 여지가 없다.
        /// </para>
        /// <para>
        /// 여기서 <c>GetComponent</c> 를 캐싱 없이 부르는 것은 캐싱 규칙 위반이 아니다 —
        /// 규칙(unity-architecture.md 2) 의 대상은 Update/FixedUpdate/LateUpdate 이고,
        /// <c>OnDrawGizmos</c> 는 에디터 전용 콜백이라 빌드에 포함되지 않는다.
        /// 애초에 에디트 모드에서는 <c>Awake</c> 가 돌지 않아 캐싱해 둘 시점 자체가 없다.
        /// </para>
        /// </summary>
        private K9Settings ResolveSettings()
        {
            if (Settings != null)
            {
                return Settings;
            }

            K9Agent agent = GetComponent<K9Agent>();
            return agent == null ? null : agent.Settings;
        }

        /// <summary>
        /// 눈 위치를 꼭짓점으로 하는 수평 부채꼴을 그린다.
        /// <c>UnityEditor.Handles</c> 를 쓰지 않는다 — 런타임 어셈블리라 참조할 수 없다.
        /// </summary>
        private static void DrawSightCone(Transform eye, float range, float angle)
        {
            if (range <= 0f)
            {
                return;
            }

            Vector3 apex = eye.position;
            Quaternion rotation = eye.rotation;
            float halfAngle = angle * 0.5f;
            float step = angle / GizmoSegments;

            // 채움 — 꼭짓점에서 호까지 선을 촘촘히 그어 반투명 면처럼 보이게 한다.
            Gizmos.color = GizmoFill;
            for (int i = 0; i <= GizmoSegments; i++)
            {
                Gizmos.DrawLine(apex, apex + ConeDirection(rotation, -halfAngle + step * i) * range);
            }

            // 외곽선 — 양쪽 모서리와 호.
            Gizmos.color = GizmoOutline;

            Vector3 previous = apex + ConeDirection(rotation, -halfAngle) * range;
            Gizmos.DrawLine(apex, previous);

            for (int i = 1; i <= GizmoSegments; i++)
            {
                Vector3 current = apex + ConeDirection(rotation, -halfAngle + step * i) * range;
                Gizmos.DrawLine(previous, current);
                previous = current;
            }

            Gizmos.DrawLine(apex, previous);
        }

        /// <summary>정면에서 <paramref name="degrees"/> 만큼 좌우로 튼 방향. 눈의 수평면 기준이다.</summary>
        private static Vector3 ConeDirection(Quaternion rotation, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            return rotation * new Vector3(Mathf.Sin(radians), 0f, Mathf.Cos(radians));
        }

        private void OnDrawGizmosSelected()
        {
            Transform eye = _eye != null ? _eye : transform;
            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.9f);
            Gizmos.DrawLine(eye.position, eye.position + eye.forward * 3f);
        }
#endif
    }
}
