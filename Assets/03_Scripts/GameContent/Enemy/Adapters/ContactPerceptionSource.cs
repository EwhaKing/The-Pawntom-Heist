using System.Collections.Generic;
using Pawntom.Enemy.Authoring;
using Pawntom.Enemy.Core;
using UnityEngine;

namespace Pawntom.Enemy.Adapters
{
    /// <summary>
    /// 접촉·초근접 감지. 대상이 <c>K9Settings.Perception.ContactDistance</c> 안으로 들어오면 보고한다.
    /// 보고 종류는 <see cref="K9DetectionKind.Contact"/> 이며 경계(Alert) 를 유발한다.
    /// </summary>
    [AddComponentMenu("Pawntom/Enemy/K9 Contact Perception Source")]
    public sealed class ContactPerceptionSource : K9PerceptionSourceBehaviour
    {
        [Header("기준점")]
        [Tooltip("접촉 판정의 기준 지점. 비우면 자기 자신을 쓴다")]
        [SerializeField] private Transform _origin;

        private Transform _originTransform;

        /// <inheritdoc/>
        public override bool TryDetect(out K9Detection detection)
        {
            detection = default(K9Detection);

            if (!IsReady || _originTransform == null)
            {
                return false;
            }

            IReadOnlyList<Transform> targets = TargetProvider.Targets;
            if (targets == null || targets.Count == 0)
            {
                return false;
            }

            float contactDistance = Settings.Perception.ContactDistance;
            float sqrContact = contactDistance * contactDistance;
            Vector3 origin = _originTransform.position;

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
                float sqrDistance = (targetPosition - origin).sqrMagnitude;

                if (sqrDistance > sqrContact || sqrDistance >= bestSqr)
                {
                    continue;
                }

                bestSqr = sqrDistance;
                bestPosition = targetPosition;
                found = true;
            }

            if (found)
            {
                detection = K9Detection.Contact(bestPosition);
            }

            return found;
        }

        private void Awake()
        {
            _originTransform = _origin != null ? _origin : transform;
        }

#if UNITY_EDITOR
        // 연한 빨강 — TASK-003 2.2 C-1. 채움과 외곽선을 구분해 그린다.
        private static readonly Color GizmoFill = new Color(1f, 0.3f, 0.3f, 0.08f);
        private static readonly Color GizmoOutline = new Color(1f, 0.3f, 0.3f, 0.5f);

        /// <summary>
        /// 접촉 판정 범위를 씬 뷰에 <b>상시</b> 표시한다. 선택하지 않아도, Play 를 누르지 않아도 보인다.
        /// </summary>
        private void OnDrawGizmos()
        {
            K9Settings settings = ResolveSettings();
            if (settings == null)
            {
                return;
            }

            float radius = settings.Perception.ContactDistance;
            if (radius <= 0f)
            {
                return;
            }

            Transform origin = _origin != null ? _origin : transform;
            Vector3 center = origin.position;

            Gizmos.color = GizmoFill;
            Gizmos.DrawSphere(center, radius);

            Gizmos.color = GizmoOutline;
            Gizmos.DrawWireSphere(center, radius);
        }

        /// <summary>
        /// 기즈모가 읽을 수치를 찾는다.
        /// <para>
        /// <c>Configure</c> 로 주입된 값이 있으면(Play 중) 그것을 쓰고,
        /// 없으면(에디트 모드) 같은 오브젝트의 <see cref="K9Agent"/> 에서 읽는다.
        /// 두 경로는 같은 <see cref="K9Settings"/> 인스턴스를 가리킨다.
        /// </para>
        /// <para>
        /// 여기서 <c>GetComponent</c> 를 캐싱 없이 부르는 것은 캐싱 규칙 위반이 아니다 —
        /// 규칙(unity-architecture.md 2) 의 대상은 Update/FixedUpdate/LateUpdate 이고,
        /// <c>OnDrawGizmos</c> 는 에디터 전용 콜백이라 빌드에 포함되지 않는다.
        /// 에디트 모드에서는 <c>Awake</c> 가 돌지 않아 캐싱해 둘 시점 자체가 없다.
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
#endif
    }
}
