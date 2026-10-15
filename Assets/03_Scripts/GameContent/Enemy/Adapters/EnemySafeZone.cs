using System.Collections.Generic;
using Pawntom.Enemy.Core;
using UnityEngine;
using UnityEngine.AI;

namespace Pawntom.Enemy.Adapters
{
    /// <summary>
    /// 적 출입 금지 구역(본부). 같은 오브젝트의 BoxCollider 범위를 쓴다.
    /// <list type="bullet">
    /// <item><b>감지</b>: 두뇌가 이 안을 가리키는 시야·접촉·흔적·추적 좌표를 버린다(<see cref="EnemyBrain.SetSafeZone"/>).
    ///       플레이어가 들어오면 적은 입구(마지막으로 본 바깥 지점)까지 쫓다가 추격을 끝낸다.</item>
    /// <item><b>이동</b>: 같은 크기의 NavMeshObstacle(Carve)로 내비 메시를 파내 경로 자체가 안으로 나지 않는다.
    ///       조사·배회 지점도 구워진 메시 위에서만 고르므로 안쪽이 뽑히지 않는다.</item>
    /// </list>
    /// <para>
    /// GAME_DESIGN 변수 후보 "본부에 적 NPC 진입"을 위해 <see cref="SetBlocking"/>으로 끄고 켤 수 있다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Pawntom/Enemy/Enemy Safe Zone")]
    public sealed class EnemySafeZone : MonoBehaviour
    {
        private static readonly List<EnemySafeZone> ActiveZones = new List<EnemySafeZone>(4);

        [Tooltip("켜져 있는 동안만 막는다")]
        [SerializeField] private bool blocking = true;

        [Tooltip("내비 메시를 파내 적의 경로를 막는다. 끄면 감지만 막는다")]
        [SerializeField] private bool carveNavMesh = true;

        [Tooltip("판정 상자를 로컬 단위로 이만큼 키운다(입구 문턱에서 깜빡이지 않도록)")]
        [SerializeField, Min(0f)] private float margin = 0f;

        private BoxCollider _box;
        private NavMeshObstacle _obstacle;
        private bool _ownsObstacle;

        /// <summary>모든 활성 구역을 하나로 묶은 판정기. 두뇌에 넘긴다.</summary>
        public static IEnemySafeZone Registry { get; } = new RegistryZone();

        public bool IsBlocking
        {
            get { return blocking; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            ActiveZones.Clear();
        }

        /// <summary>코드에서 붙일 때 범위 상자를 지정한다(본부 MainbaseZone 이 자기 BoxCollider 로 부른다).</summary>
        public void Configure(BoxCollider box)
        {
            _box = box;
            SyncObstacle();
        }

        /// <summary>출입 금지를 켜고 끈다(변수 이벤트 "본부에 적 진입" 등).</summary>
        public void SetBlocking(bool value)
        {
            blocking = value;
            SyncObstacle();
        }

        public bool Contains(Vector3 worldPoint)
        {
            if (!blocking || _box == null)
            {
                return false;
            }

            Transform boxTransform = _box.transform;
            Vector3 local = boxTransform.InverseTransformPoint(worldPoint);
            return EnemySafeBox.Contains(local, _box.center, _box.size, margin);
        }

        private void Awake()
        {
            if (_box == null)
            {
                _box = GetComponent<BoxCollider>();
            }
        }

        private void OnEnable()
        {
            if (!ActiveZones.Contains(this))
            {
                ActiveZones.Add(this);
            }

            SyncObstacle();
        }

        private void OnDisable()
        {
            ActiveZones.Remove(this);

            if (_obstacle != null && _ownsObstacle)
            {
                _obstacle.enabled = false;
            }
        }

        private void SyncObstacle()
        {
            if (!carveNavMesh || _box == null)
            {
                return;
            }

            if (_obstacle == null)
            {
                _obstacle = _box.GetComponent<NavMeshObstacle>();

                if (_obstacle == null)
                {
                    _obstacle = _box.gameObject.AddComponent<NavMeshObstacle>();
                    _ownsObstacle = true;
                }
            }

            if (_ownsObstacle)
            {
                _obstacle.shape = NavMeshObstacleShape.Box;
                _obstacle.center = _box.center;
                _obstacle.size = _box.size;
                _obstacle.carving = true;
                _obstacle.carveOnlyStationary = true;
            }

            _obstacle.enabled = blocking && isActiveAndEnabled;
        }

        private void OnDrawGizmosSelected()
        {
            BoxCollider box = _box != null ? _box : GetComponent<BoxCollider>();

            if (box == null)
            {
                return;
            }

            Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.35f);
            Gizmos.matrix = box.transform.localToWorldMatrix;
            Gizmos.DrawCube(box.center, box.size);
        }

        private sealed class RegistryZone : IEnemySafeZone
        {
            public bool Contains(Vector3 worldPoint)
            {
                for (int i = 0; i < ActiveZones.Count; i++)
                {
                    EnemySafeZone zone = ActiveZones[i];

                    if (zone != null && zone.Contains(worldPoint))
                    {
                        return true;
                    }
                }

                return false;
            }
        }
    }
}
