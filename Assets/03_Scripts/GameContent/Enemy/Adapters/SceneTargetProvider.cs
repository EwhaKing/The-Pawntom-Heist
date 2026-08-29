using System.Collections.Generic;
using UnityEngine;

namespace Pawntom.Enemy.Adapters
{
    /// <summary>
    /// 씬에 하나 두고 쓰는 기본 대상 제공자.
    /// <para>
    /// 인스펙터에 미리 넣어 둔 대상 + 런타임에 등록된 대상을 합쳐서 돌려준다.
    /// </para>
    /// <para>
    /// 런타임에 스폰되는 대상(플레이어 등)은 <see cref="K9TargetRegistrar"/> 컴포넌트를
    /// 붙여 <b>스스로</b> 등록한다. 제공자는 누가 등록하는지 알지 않고, 스폰 코드는
    /// 적 AI 를 알지 않는다. <see cref="RegisterTarget"/> 계열은 수동 등록 경로로 남겨 둔다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Pawntom/Enemy/Scene Target Provider")]
    public sealed class SceneTargetProvider : MonoBehaviour, IK9TargetProvider, IK9TargetRegistry
    {
        private static SceneTargetProvider _instance;

        [Header("고정 대상")]
        [Tooltip("씬에 이미 존재하는 감지 대상. 테스트 씬에서 더미 플레이어를 넣어 쓴다")]
        [SerializeField] private List<Transform> _initialTargets = new List<Transform>();

        private readonly K9TargetSet _set = new K9TargetSet(8);

        /// <summary>가장 먼저 활성화된 제공자. 없으면 null.</summary>
        public static SceneTargetProvider Instance
        {
            get { return _instance; }
        }

        /// <inheritdoc/>
        public IReadOnlyList<Transform> Targets
        {
            get { return _set.Targets; }
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

        /// <inheritdoc/>
        public void Register(Transform target)
        {
            // 정리는 목록이 바뀌는 이 시점에만 한다. Targets getter 는 매 틱 읽히므로
            // 거기서 Prune 을 돌리면 프레임마다 순회 비용이 붙는다.
            _set.Prune();
            _set.Add(target);
        }

        /// <inheritdoc/>
        public void Unregister(Transform target)
        {
            _set.Remove(target);
            _set.Prune();
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

            // 창구를 꽂는다. 제공자보다 먼저 깨어난 등록자들의 대기분도 이때 함께 들어온다.
            K9TargetRegistrar.AttachRegistry(this);
        }

        private void OnDestroy()
        {
            // 자기가 꽂은 경우에만 뺀다 — 정적 Instance 처리와 같은 방식이다.
            K9TargetRegistrar.DetachRegistry(this);

            if (_instance == this)
            {
                _instance = null;
            }
        }
    }
}
