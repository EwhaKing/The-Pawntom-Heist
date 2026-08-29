using System.Collections.Generic;
using UnityEngine;

namespace Pawntom.Enemy.Adapters
{
    /// <summary>
    /// 자기 자신을 감지 대상으로 등록하는 컴포넌트. 런타임에 스폰되는 대상에 붙인다.
    /// <para>
    /// 붙는 쪽(플레이어 프리팹 등)은 <b>C# 코드를 한 줄도 늘리지 않는다.</b>
    /// 컴포넌트를 얹기만 하면 되고, 이 클래스는 구체 제공자가 아니라
    /// <see cref="IEnemyTargetRegistry"/> 에만 의존한다(DIP).
    /// </para>
    /// <para>
    /// 스크립트 실행 순서상 제공자보다 먼저 깨어날 수 있으므로,
    /// 창구가 비어 있는 동안의 등록분은 정적 대기 목록에 쌓아 두었다가
    /// <see cref="AttachRegistry"/> 시점에 한꺼번에 넘긴다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Pawntom/Enemy/Target Registrar")]
    public sealed class EnemyTargetRegistrar : MonoBehaviour
    {
        /// <summary>창구가 꽂히기 전에 활성화된 대상들. 드레인되면 비워진다.</summary>
        private static readonly List<Transform> Pending = new List<Transform>(8);

        [Header("기준점")]
        [Tooltip("감지 기준 지점. 비우면 이 오브젝트의 Transform 을 쓴다")]
        [SerializeField] private Transform _target;

        /// <summary>등록한 대상. 해제할 때 같은 것을 빼야 하므로 들고 있는다.</summary>
        private Transform _registered;

        /// <summary>
        /// 등록 대상을 받아 갈 창구.
        /// <b>setter 를 열지 마라</b> — 대입은 <see cref="AttachRegistry"/> /
        /// <see cref="DetachRegistry"/> 안에서만 일어난다.
        /// 대입만 하고 대기 목록 드레인을 건너뛰면 먼저 깨어난 등록자가 조용히 누락된다.
        /// </summary>
        public static IEnemyTargetRegistry Registry { get; private set; }

        /// <summary>
        /// 제공자가 준비되면 자기를 꽂는다. 대기 중이던 등록분을 함께 넘긴다.
        /// <para><paramref name="registry"/> 가 null 이면 아무것도 하지 않는다.</para>
        /// </summary>
        public static void AttachRegistry(IEnemyTargetRegistry registry)
        {
            if (registry == null)
            {
                return;
            }

            Registry = registry;

            for (int i = 0; i < Pending.Count; i++)
            {
                Transform pending = Pending[i];
                if (pending == null)
                {
                    continue;
                }

                registry.Register(pending);
            }

            Pending.Clear();
        }

        /// <summary>
        /// 제공자가 사라질 때 창구를 비운다.
        /// <para>꽂아 둔 당사자만 뺄 수 있다 — 다른 제공자가 이미 꽂혀 있으면 건드리지 않는다.</para>
        /// </summary>
        public static void DetachRegistry(IEnemyTargetRegistry registry)
        {
            if (ReferenceEquals(Registry, registry))
            {
                Registry = null;
            }
        }

        /// <summary>
        /// 정적 상태를 플레이 세션마다 초기화한다.
        /// <para>
        /// 도메인 리로드가 꺼진 환경에서는 이전 세션의 창구와 대기 목록이 그대로 남는다.
        /// 파괴된 오브젝트를 가리키는 잔재가 새 세션으로 새어 들어가는 것을 막는다.
        /// </para>
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Registry = null;
            Pending.Clear();
        }

        private void OnEnable()
        {
            _registered = _target != null ? _target : transform;

            if (Registry != null)
            {
                Registry.Register(_registered);
                return;
            }

            // 창구가 아직 없다. 제공자가 깨어날 때 받아 가도록 쌓아 둔다.
            if (!PendingContains(_registered))
            {
                Pending.Add(_registered);
            }
        }

        private void OnDisable()
        {
            if (_registered == null)
            {
                return;
            }

            PendingRemove(_registered);

            // 씬 언로드 시 제공자의 OnDestroy 가 먼저 돌아 창구가 이미 비어 있을 수 있다.
            // 가드가 없으면 여기서 NullReferenceException 이 난다.
            if (Registry != null)
            {
                Registry.Unregister(_registered);
            }

            _registered = null;
        }

        private static bool PendingContains(Transform target)
        {
            for (int i = 0; i < Pending.Count; i++)
            {
                if (ReferenceEquals(Pending[i], target))
                {
                    return true;
                }
            }

            return false;
        }

        private static void PendingRemove(Transform target)
        {
            for (int i = Pending.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(Pending[i], target))
                {
                    Pending.RemoveAt(i);
                }
            }
        }
    }
}
