using Fusion;
using Pawntom.Enemy.Authoring;
using Pawntom.Enemy.Core;
using UnityEngine;
using UnityEngine.AI;

namespace Pawntom.EnemyBridge
{
    /// <summary>
    /// 적 한 마리를 네트워크에 얹는 어댑터.
    /// <para>
    /// 하는 일은 <b>권한 분기 하나</b>다 — 두뇌는 StateAuthority 한 곳에서만 돌고,
    /// 나머지 클라이언트는 <c>NetworkTransform</c> 이 넣어 주는 좌표와
    /// 아래 <see cref="ReplicatedState"/> 복제값만 본다.
    /// </para>
    /// <para>
    /// <b>이 클래스에 상태 분기(<c>switch (state)</c>)를 넣지 마라.</b>
    /// 판단은 전부 <see cref="Pawntom.Enemy.Core.EnemyBrain"/> 안에 있다.
    /// 이 파일은 Fusion 타입을 만지므로 <c>Pawntom.Enemy</c> 어셈블리에 들어갈 수 없고,
    /// 따라서 EditMode 로 검증할 수 없다. 판단 로직을 여기로 옮기지 마라.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyAgent))]
    [AddComponentMenu("Pawntom/Enemy/Network Enemy Agent")]
    public sealed class NetworkEnemyAgent : NetworkBehaviour
    {
        // 권한이 없는 쪽이 보는 값. 권한자가 매 틱 두뇌의 상태로 덮어쓴다.
        [Networked] private EnemyState ReplicatedState { get; set; }

        private EnemyAgent _agent;
        private NavMeshAgent _navMeshAgent;

        /// <summary>
        /// 이 개체의 현재 상태. 권한이 있으면 두뇌 값, 없으면 복제값이다.
        /// </summary>
        public EnemyState State
        {
            get
            {
                if (_agent == null)
                {
                    return ReplicatedState;
                }

                return HasStateAuthority ? _agent.State : ReplicatedState;
            }
        }

        /// <summary>
        /// 부품을 캐싱하고 <see cref="EnemyAgent"/> 의 자체 틱을 내린다.
        /// <para>
        /// <b><see cref="Spawned"/> 가 아니라 여기여야 한다.</b> <c>Spawned</c> 는 첫 <c>Update</c>
        /// 보다 늦게 올 수 있고, 그 사이 프레임에서 클라이언트가 두뇌를 한 번이라도 돌리면
        /// 복제된 좌표와 어긋나 적이 튄다.
        /// </para>
        /// </summary>
        private void Awake()
        {
            _agent = GetComponent<EnemyAgent>();
            _navMeshAgent = GetComponent<NavMeshAgent>();

            if (_agent != null)
            {
                _agent.SelfTick = false;
            }
        }

        /// <summary>
        /// 권한에 따라 이동 컴포넌트를 가른다.
        /// <para>
        /// 권한이 없는 쪽에서 <c>NavMeshAgent</c> 가 살아 있으면
        /// <c>NetworkTransform</c> 이 넣는 좌표와 서로 밀어내 적이 떨린다. 그래서 꺼 둔다.
        /// NavMesh 가 구워지지 않은 씬에서도 이 경로는 조용히 넘어간다 — 예외를 만들지 않는다.
        /// </para>
        /// </summary>
        public override void Spawned()
        {
            if (HasStateAuthority)
            {
                return;
            }

            if (_navMeshAgent != null)
            {
                _navMeshAgent.enabled = false;
            }
        }

        /// <summary>
        /// 권한자만 두뇌에 시간을 흘려보내고, 그 결과 상태를 복제한다.
        /// <para>
        /// <c>Time.deltaTime</c> 이 아니라 <see cref="NetworkRunner.DeltaTime"/> 을 쓴다 —
        /// Fusion 의 시간 기준은 렌더 프레임이 아니라 틱이다.
        /// </para>
        /// </summary>
        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority)
            {
                return;
            }

            if (_agent == null)
            {
                return;
            }

            _agent.Tick(Runner.DeltaTime);
            ReplicatedState = _agent.State;
        }
    }
}
