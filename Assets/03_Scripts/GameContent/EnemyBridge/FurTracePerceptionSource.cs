using System.Collections.Generic;
using Fusion;
using Pawntom.Enemy.Adapters;
using Pawntom.Enemy.Authoring;
using Pawntom.Enemy.Core;
using UnityEngine;

namespace Pawntom.EnemyBridge
{
    /// <summary>
    /// 털공 흔적 감지. 반경 안의 털공 중 가장 최근에 생긴 것을
    /// <see cref="K9DetectionKind.Trace"/> 단서로 보고해 조사(Investigate) 를 유발한다.
    /// 목표에 도달하면 그 털공을 네트워크에서 없앤다.
    /// <para>
    /// 이 클래스는 <b>얇은 어댑터</b>다. 어느 흔적을 고르고 언제 없앨지는 전부
    /// <see cref="K9TraceCursor"/> 가 판단한다 — 이 파일은 Fusion 타입을 만지므로
    /// <c>Pawntom.Enemy</c> 어셈블리에 들어갈 수 없고, 따라서 EditMode 로 검증할 수 없다.
    /// 판단 로직을 여기로 옮기지 마라.
    /// </para>
    /// </summary>
    [AddComponentMenu("Pawntom/Enemy/K9 Fur Trace Perception Source")]
    public sealed class FurTracePerceptionSource : K9PerceptionSourceBehaviour
    {
        [Header("기준점")]
        [Tooltip("흔적 탐색의 기준 지점. 비우면 자기 자신을 쓴다")]
        [SerializeField] private Transform _origin;

        // 매 틱 재사용한다. 틱마다 새 목록을 만들면 GC 할당이 쌓인다.
        private readonly List<K9TraceCandidate> _candidates = new List<K9TraceCandidate>(64);

        private readonly K9TraceCursor _cursor = new K9TraceCursor();

        private Transform _originTransform;

        /// <inheritdoc/>
        public override bool TryDetect(out K9Detection detection)
        {
            detection = default(K9Detection);

            if (!IsReady || _originTransform == null)
            {
                return false;
            }

            Vector3 origin = _originTransform.position;

            // 후보 수집 — 목록은 비우고 다시 채운다. foreach·LINQ·목록 생성을 쓰지 않는다.
            IReadOnlyList<FurBallTrace> traces = FurBallTrace.Active;
            _candidates.Clear();

            for (int i = 0; i < traces.Count; i++)
            {
                FurBallTrace trace = traces[i];

                // 파괴 중인 항목은 == null 이 true 가 된다.
                if (trace == null)
                {
                    continue;
                }

                NetworkObject networkObject = trace.Object;

                // Id 가 유효하지 않으면(Raw == 0) 개체를 식별할 수 없으므로 후보로 쓰지 않는다.
                if (networkObject == null || !networkObject.Id.IsValid)
                {
                    continue;
                }

                _candidates.Add(new K9TraceCandidate(
                    unchecked((int)networkObject.Id.Raw),
                    trace.transform.position,
                    trace.SpawnTick));
            }

            K9TraceStep step = _cursor.Advance(
                origin,
                _candidates,
                Settings.Trace.DetectionRadius,
                Settings.Trace.ReachDistance);

            // 도달한 흔적을 없앤다. 커서는 삭제를 모르므로 여기서만 일어난다.
            if (step.HasConsumed)
            {
                for (int i = 0; i < traces.Count; i++)
                {
                    FurBallTrace trace = traces[i];

                    if (trace == null)
                    {
                        continue;
                    }

                    NetworkObject networkObject = trace.Object;

                    if (networkObject == null || !networkObject.Id.IsValid)
                    {
                        continue;
                    }

                    if (unchecked((int)networkObject.Id.Raw) != step.ConsumedId)
                    {
                        continue;
                    }

                    // 권한이 없는 쪽에서 Despawn 을 부르면 예외가 난다. 상태 권한이 있을 때만 부른다.
                    NetworkRunner runner = trace.Runner;

                    if (runner != null && networkObject.HasStateAuthority)
                    {
                        runner.Despawn(networkObject);
                    }

                    break;
                }
            }

            if (!step.HasTarget)
            {
                return false;
            }

            detection = K9Detection.Trace(step.Target);
            return true;
        }

        private void Awake()
        {
            _originTransform = _origin != null ? _origin : transform;
        }

#if UNITY_EDITOR
        // 청록 — 접촉·시야 기즈모(빨강) 와 겹치지 않게 고른다.
        private static readonly Color GizmoFill = new Color(0.2f, 0.9f, 0.8f, 0.06f);
        private static readonly Color GizmoOutline = new Color(0.2f, 0.9f, 0.8f, 0.5f);

        /// <summary>
        /// 흔적 탐색 반경을 씬 뷰에 <b>상시</b> 표시한다. 선택하지 않아도, Play 를 누르지 않아도 보인다.
        /// </summary>
        private void OnDrawGizmos()
        {
            K9Settings settings = ResolveSettings();
            if (settings == null)
            {
                return;
            }

            float radius = settings.Trace.DetectionRadius;
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
        /// 기즈모가 읽을 수치를 찾는다. <c>ContactPerceptionSource</c> 와 같은 방식이다 —
        /// 주입된 값이 있으면(Play 중) 그것을, 없으면(에디트 모드) 같은 오브젝트의
        /// <see cref="K9Agent"/> 에서 읽는다.
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
