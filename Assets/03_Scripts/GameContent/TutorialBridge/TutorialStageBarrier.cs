using System.Collections.Generic;
using Pawntom.Tutorial.Core;
using UnityEngine;
using UnityEngine.AI;

namespace Pawntom.TutorialBridge
{
    /// <summary>
    /// 아직 갈 때가 아닌 곳을 막는 보이지 않는 벽. 정해진 메인 비트 차례가 오기 전에는 플레이어를 막고,
    /// 벽 가까이 오면 <see cref="TutorialSignal.EarlyAreaBlocked"/> 를 보내 깜냥이 혼잣말(R-early)을 띄운다.
    /// <para>
    /// 지금은 금고 쪽 복도 입구에 하나 둔다(6-1 차례부터 열림) — 튜토리얼은 가운데 전리품 창고가 먼저라서,
    /// 경비견이 순찰하는 금고 구역으로 먼저 가 버리면 순서가 꼬이고 위험하다.
    /// 씬에 직접 배치해도 되고(BoxCollider 크기가 벽), 없으면 디렉터가 금고 복도 구역(Zone_VaultCorridor) 앞에 자동으로 만든다.
    /// </para>
    /// <para>
    /// 벽은 플레이어만 막는다. 적(경비견)과는 충돌을 끄고, 길 안내 화살표도 이 벽을 지형으로 보지 않는다(<see cref="PathGuideIgnore"/>).
    /// </para>
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class TutorialStageBarrier : MonoBehaviour
    {
        [Tooltip("이 메인 비트 차례가 오면 벽이 열린다(그 전까지 막는다)")]
        [SerializeField] private string unlockBeatId = "6-1";

        [Tooltip("벽에서 이 거리(m) 안으로 오면 혼잣말을 띄운다")]
        [SerializeField] private float warnDistance = 1.2f;

        private const float SignalInterval = 0.5f;     // 신호를 보내는 최소 간격(반복 간격은 대본의 AsRepeatable 이 정한다)
        private const float EnemyScanInterval = 2f;    // 새로 생긴 적과의 충돌을 끄는 주기

        private BoxCollider _wall;
        private float _nextSignal;
        private float _nextEnemyScan;
        private readonly HashSet<Collider> _ignored = new HashSet<Collider>();

        /// <summary>디렉터가 자동으로 만들 때 쓴다. <paramref name="wallBounds"/> 가 벽의 크기·위치(축 정렬).</summary>
        public static TutorialStageBarrier Create(UnityEngine.SceneManagement.Scene scene, Bounds wallBounds, string unlockBeatId)
        {
            GameObject host = new GameObject("TutorialStageBarrier_" + unlockBeatId);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(host, scene);
            host.transform.position = wallBounds.center;

            BoxCollider box = host.AddComponent<BoxCollider>();
            host.AddComponent<PathGuideIgnore>();
            box.size = wallBounds.size;

            TutorialStageBarrier barrier = host.AddComponent<TutorialStageBarrier>();
            barrier.unlockBeatId = unlockBeatId;
            return barrier;
        }

        private void Awake()
        {
            _wall = GetComponent<BoxCollider>();
            _wall.isTrigger = false;

            // 길 안내 화살표는 이 벽을 지나가야 한다(열릴 문 자리라서). 씬에 직접 놓았을 때도 붙인다.
            if (GetComponent<PathGuideIgnore>() == null)
            {
                gameObject.AddComponent<PathGuideIgnore>();
            }
        }

        private void Update()
        {
            TutorialDirector director = TutorialDirector.Active;

            if (director == null)
            {
                return;
            }

            bool locked = !director.HasReachedMain(unlockBeatId);

            if (_wall.enabled != locked)
            {
                _wall.enabled = locked;
            }

            if (!locked)
            {
                return;
            }

            IgnoreEnemies();

            Transform player = director.LocalPlayerTransform;

            if (player == null || Time.unscaledTime < _nextSignal)
            {
                return;
            }

            Vector3 closest = _wall.bounds.ClosestPoint(player.position);
            Vector2 flat = new Vector2(closest.x - player.position.x, closest.z - player.position.z);

            if (flat.sqrMagnitude <= warnDistance * warnDistance)
            {
                _nextSignal = Time.unscaledTime + SignalInterval;
                director.Raise(TutorialSignal.EarlyAreaBlocked);
            }
        }

        // 경비견은 이 벽에 막히면 안 된다(추격·순찰 경로가 이 복도를 지날 수 있다).
        private void IgnoreEnemies()
        {
            if (Time.unscaledTime < _nextEnemyScan)
            {
                return;
            }

            _nextEnemyScan = Time.unscaledTime + EnemyScanInterval;
            NavMeshAgent[] agents = FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None);

            for (int i = 0; i < agents.Length; i++)
            {
                Collider[] colliders = agents[i].GetComponentsInChildren<Collider>(true);

                for (int k = 0; k < colliders.Length; k++)
                {
                    if (colliders[k] != null && _ignored.Add(colliders[k]))
                    {
                        Physics.IgnoreCollision(_wall, colliders[k], true);
                    }
                }
            }
        }

        private void OnDrawGizmos()
        {
            BoxCollider box = GetComponent<BoxCollider>();

            if (box == null)
            {
                return;
            }

            Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.35f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.center, box.size);
        }
    }
}