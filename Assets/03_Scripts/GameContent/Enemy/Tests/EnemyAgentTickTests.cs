using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Pawntom.Enemy.Authoring;
using UnityEngine;
using UnityEngine.AI;

namespace Pawntom.Enemy.Tests
{
    /// <summary>
    /// TASK-018 5.4 — 외부 틱 시임(<see cref="EnemyAgent.SelfTick"/> / <see cref="EnemyAgent.Tick"/>) 검증.
    /// <para>
    /// 이 시임이 깨지면 두 방향으로 사고가 난다 — 기본값이 <c>false</c> 로 바뀌면
    /// 로컬 테스트 씬의 개가 조용히 멈추고, 게이트가 안 걸리면 클라이언트가 두뇌를 돌려
    /// 복제 좌표와 싸운다.
    /// </para>
    /// <para>
    /// TASK-018 5.3 의 나머지 기준(2~7)은 <c>NetworkBehaviour</c>·<c>NetworkRunner</c> 의존이라
    /// EditMode 로 세울 수 없다. 그쪽은 코드 검사로 돌렸다(§7.2).
    /// </para>
    /// <para>
    /// <b>하네스 주의</b>: EditMode 에서는 <c>Awake</c> 도 <c>Update</c> 도 자동으로 돌지 않는다
    /// (§5.4 의 전제와 다르다 — §7.3 참조). 둘 다 리플렉션으로 직접 부른다.
    /// </para>
    /// </summary>
    public sealed class EnemyAgentTickTests
    {
        // 두 지점만 있으면 첫 틱에 MoveTo 가 나간다 — 시간이 흘렀는지 판정하는 관측점이다.
        private static readonly Vector3[] Route =
        {
            new Vector3(3f, 0f, 0f),
            new Vector3(0f, 0f, 3f)
        };

        private readonly List<GameObject> _spawned = new List<GameObject>(2);

        [TearDown]
        public void TearDown()
        {
            // EnemyAlertChannelBehaviour 가 정적 목록을 쓴다. 지우지 않으면 테스트 간에 샌다.
            for (int i = 0; i < _spawned.Count; i++)
            {
                GameObject spawned = _spawned[i];
                if (spawned != null)
                {
                    Object.DestroyImmediate(spawned);
                }
            }

            _spawned.Clear();
        }

        [Test]
        [Description("TASK-018 5.3-1 - 아무도 건드리지 않으면 스스로 시간을 흘려보낸다")]
        public void SelfTick_Default_IsTrue()
        {
            EnemyAgent agent = NewAgent();

            Assert.IsTrue(agent.SelfTick, "기본값이 false 로 바뀌면 로컬 씬의 개가 조용히 멈춘다");
        }

        [Test]
        [Description("TASK-018 5.3-1 - SelfTick 이 false 면 Update 가 두뇌를 돌리지 않는다")]
        public void Update_SelfTickDisabled_DoesNotAdvanceBrain()
        {
            EnemyAgent agent = NewAgent();
            agent.Brain.SetPatrolRoute(Route, true);
            agent.SelfTick = false;

            for (int i = 0; i < 5; i++)
            {
                InvokeNonPublic(agent, "Update");
            }

            Assert.IsNull(
                agent.Brain.CurrentTarget,
                "게이트가 열려 있으면 클라이언트가 두뇌를 돌려 복제 좌표와 싸운다");
        }

        [Test]
        [Description("TASK-018 5.3-1 - SelfTick 이 true 면 Update 가 두뇌를 돌린다")]
        public void Update_SelfTickEnabled_AdvancesBrain()
        {
            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f)
            {
                Assert.Ignore(
                    "에디트 모드의 Time.deltaTime 이 0 이라 Update 경로를 관측할 수 없다. " +
                    "시임 자체는 Tick_PositiveDeltaTime_AdvancesBrain 이 검증한다");
            }

            EnemyAgent agent = NewAgent();
            agent.Brain.SetPatrolRoute(Route, true);

            Assert.IsNull(agent.Brain.CurrentTarget, "사전 조건: 아직 목표가 없다");

            InvokeNonPublic(agent, "Update");

            Assert.IsNotNull(agent.Brain.CurrentTarget, "SelfTick 이 켜져 있으면 시간이 흐른다");
        }

        [Test]
        [Description("TASK-018 5.3-1 - 외부가 부르는 Tick 은 SelfTick 과 무관하게 두뇌에 전달된다")]
        public void Tick_PositiveDeltaTime_AdvancesBrain()
        {
            EnemyAgent agent = NewAgent();
            agent.Brain.SetPatrolRoute(Route, true);
            agent.SelfTick = false;

            Assert.IsNull(agent.Brain.CurrentTarget, "사전 조건: 아직 목표가 없다");

            agent.Tick(0.1f);

            Assert.IsNotNull(
                agent.Brain.CurrentTarget,
                "SelfTick 이 꺼져 있어도 외부 호출은 통해야 한다 - 네트워크 어댑터가 쓰는 경로다");
        }

        [Test]
        [Description("TASK-018 5.3-1 - 두뇌가 없어도 Tick 은 예외를 내지 않는다")]
        public void Tick_WithoutBrain_DoesNotThrow()
        {
            // Awake 를 부르지 않았으므로 두뇌가 없다.
            // 플레이 중 리컴파일로 두뇌가 null 이 된 상황과 같은 모양이다.
            EnemyAgent agent = NewBareAgent();

            Assert.IsNull(agent.Brain, "사전 조건: 두뇌가 아직 없다");
            Assert.DoesNotThrow(() => agent.Tick(0.1f));
        }

        // ── 하네스 ────────────────────────────────────────────────────

        /// <summary>
        /// 부품만 붙인 상태. <c>Awake</c> 는 아직 돌지 않아 두뇌가 없다.
        /// </summary>
        private EnemyAgent NewBareAgent()
        {
            GameObject host = new GameObject("EnemyAgentTick");
            _spawned.Add(host);

            EnemyAgent agent = host.AddComponent<EnemyAgent>();

            // RequireComponent 가 붙여 준다. 없으면 두뇌 조립이 예외로 죽는다.
            Assert.IsNotNull(
                host.GetComponent<NavMeshAgent>(), "사전 조건: NavMeshAgent 가 함께 붙는다");

            return agent;
        }

        /// <summary>
        /// 두뇌까지 조립된 상태.
        /// <para>
        /// EditMode 에서는 <c>AddComponent</c> 로 <c>Awake</c> 가 돌지 않으므로 직접 부른다.
        /// </para>
        /// </summary>
        private EnemyAgent NewAgent()
        {
            EnemyAgent agent = NewBareAgent();

            InvokeNonPublic(agent, "Awake");

            Assert.IsNotNull(agent.Brain, "사전 조건: Awake 가 두뇌를 조립한다");
            return agent;
        }

        /// <summary>
        /// 엔진이 대신 불러 주는 비공개 메시지를 직접 부른다.
        /// <c>EnemyTargetSetTests</c> 의 리플렉션 하네스와 같은 방식이다.
        /// </summary>
        private static void InvokeNonPublic(EnemyAgent agent, string methodName)
        {
            MethodInfo method = typeof(EnemyAgent).GetMethod(
                methodName, BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.IsNotNull(method, $"EnemyAgent.{methodName} 이 사라졌다");

            method.Invoke(agent, null);
        }
    }
}
