using System.Collections.Generic;
using Pawntom.Tutorial.Core;
using UnityEngine;

namespace Pawntom.TutorialBridge
{
    /// <summary>
    /// 튜토리얼 대본의 "~ 도착 시 / 진입 시" 지점을 씬에 표시하는 영역.
    /// 로컬 플레이어가 콜라이더 안에 있는 동안 지정한 신호를 계속 보낸다(상태형 신호).
    /// <para>
    /// 물리 트리거 이벤트를 쓰지 않는다 — Fusion 의 NetworkCharacterController 는 재시뮬레이션 중 위치가 앞뒤로 움직여
    /// OnTriggerEnter 가 여러 번 울릴 수 있다. 대신 수집기가 주기적으로 <see cref="Contains"/>를 묻는다.
    /// </para>
    /// <para>
    /// 쓰는 신호: ObstacleAhead(2-9 상자 앞), CorridorAEndReached(2-10 복도 A 끝 격벽), NearSecurityNode(3-1),
    /// NearTerminal(4-1), BlueDoorAhead(5-1), LootRoomEntered(5-2), LootRoomExiting(5-8),
    /// VaultCorridorEntered(6-1), VaultRoomEntered(6-5), VaultFront(7-1), PlayerInMainbase(본부 범위 덮어쓰기).
    /// 해당 신호용 영역이 하나라도 있으면 수집기의 자동 근접 감지(격벽·단말기 거리)는 그 신호에 대해 꺼진다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    [AddComponentMenu("Pawntom/Tutorial/Tutorial Zone")]
    public sealed class TutorialZone : MonoBehaviour
    {
        private static readonly List<TutorialZone> ActiveZones = new List<TutorialZone>(16);

        [Tooltip("플레이어가 이 영역 안에 있을 때 보낼 신호")]
        [SerializeField] private TutorialSignal signal = TutorialSignal.ObstacleAhead;

        [Tooltip("씬 뷰 기즈모 색")]
        [SerializeField] private Color gizmoColor = new Color(0.49f, 1f, 0.7f, 0.25f);

        private Collider _collider;

        public static IReadOnlyList<TutorialZone> All
        {
            get { return ActiveZones; }
        }

        public TutorialSignal Signal
        {
            get { return signal; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            ActiveZones.Clear();
        }

        /// <summary>이 신호를 맡은 영역이 씬에 있는가.</summary>
        public static bool HasZoneFor(TutorialSignal target)
        {
            for (int i = 0; i < ActiveZones.Count; i++)
            {
                if (ActiveZones[i].signal == target)
                {
                    return true;
                }
            }

            return false;
        }

        private void Awake()
        {
            _collider = GetComponent<Collider>();

            MeshCollider mesh = _collider as MeshCollider;

            if (mesh != null && !mesh.convex)
            {
                Debug.LogWarning($"[TutorialZone] {name}: 볼록하지 않은 MeshCollider 는 안쪽 판정이 부정확합니다. Box/Sphere/Capsule 을 쓰세요.", this);
            }
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

        /// <summary>월드 경계 상자. 콜라이더가 꺼져 있으면 false.</summary>
        public bool TryGetBounds(out Bounds bounds)
        {
            if (_collider == null || !_collider.enabled)
            {
                bounds = default;
                return false;
            }

            bounds = _collider.bounds;
            return true;
        }

        public bool Contains(Vector3 worldPoint)
        {
            if (_collider == null || !_collider.enabled)
            {
                return false;
            }

            // 안쪽 점이면 ClosestPoint 가 그 점을 그대로 돌려준다(Box/Sphere/Capsule/볼록 Mesh).
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

            Gizmos.color = gizmoColor;
            Bounds bounds = own.bounds;
            Gizmos.DrawCube(bounds.center, bounds.size);
            Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 1f);
            Gizmos.DrawWireCube(bounds.center, bounds.size);
        }
    }
}
