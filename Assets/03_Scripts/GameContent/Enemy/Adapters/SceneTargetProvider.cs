using System.Collections.Generic;
using UnityEngine;

namespace Pawntom.Enemy.Adapters
{
    /// <summary>
    /// 씬에 하나 두고 쓰는 기본 대상 제공자.
    /// <para>
    /// 인스펙터에 미리 넣어 둔 대상 + 런타임에 등록된 대상을 합쳐서 돌려준다.
    /// 플레이어가 네트워크로 스폰되는 경우 스폰 쪽에서
    /// <see cref="RegisterTarget"/> 를 호출하면 된다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Pawntom/Enemy/Scene Target Provider")]
    public sealed class SceneTargetProvider : MonoBehaviour, IK9TargetProvider
    {
        private static SceneTargetProvider _instance;

        [Header("고정 대상")]
        [Tooltip("씬에 이미 존재하는 감지 대상. 테스트 씬에서 더미 플레이어를 넣어 쓴다")]
        [SerializeField] private List<Transform> _initialTargets = new List<Transform>();

        private readonly List<Transform> _targets = new List<Transform>(8);

        /// <summary>가장 먼저 활성화된 제공자. 없으면 null.</summary>
        public static SceneTargetProvider Instance
        {
            get { return _instance; }
        }

        /// <inheritdoc/>
        public IReadOnlyList<Transform> Targets
        {
            get { return _targets; }
        }

        /// <summary>씬에 제공자가 있으면 대상을 등록한다.</summary>
        public static void RegisterTarget(Transform target)
        {
            if (_instance != null)
            {
                _instance.Register(target);
            }
        }

        /// <summary>씬에 제공자가 있으면 대상을 해제한다.</summary>
        public static void UnregisterTarget(Transform target)
        {
            if (_instance != null)
            {
                _instance.Unregister(target);
            }
        }

        public void Register(Transform target)
        {
            if (target == null || _targets.Contains(target))
            {
                return;
            }

            _targets.Add(target);
        }

        public void Unregister(Transform target)
        {
            if (target == null)
            {
                return;
            }

            _targets.Remove(target);
        }

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
            }

            for (int i = 0; i < _initialTargets.Count; i++)
            {
                Register(_initialTargets[i]);
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }
    }
}
