using System.Collections.Generic;
using NUnit.Framework;
using Pawntom.Enemy.Core;
using UnityEngine;

namespace Pawntom.Enemy.Tests
{
    /// <summary>
    /// TASK-015 5.3 수용 기준 2~5 의 기계 검증.
    /// <para>
    /// (가) 정책 구현체를 <b>단독</b>으로 호출해 반환 상태를 본다.
    /// (나) 그 정책을 <see cref="EnemyBrain"/> 에 주입해 실제 전이가 일어나는지 본다.
    /// </para>
    /// <para>
    /// 스텁(<c>FakeMotor</c> 등)은 <c>EnemyBrainTests.cs</c> 의 것을 그대로 쓴다 —
    /// <c>internal</c> 은 어셈블리 단위라 같은 어셈블리의 이 파일에서 보인다.
    /// 기준 1 이 그 파일의 무수정 통과를 요구하므로 그쪽은 한 줄도 고치지 않는다.
    /// </para>
    /// </summary>
    public sealed class EnemyEngagementPolicyTests
    {
        // ── (가) 정책 단독 ──────────────────────────────────────────

        [Test]
        [Description("기준5 - 하울링 정책은 순찰 중 시야에서 하울링 이력을 보지 않고 언제나 Alert 다")]
        public void HowlingPolicy_SightedWhilePatrolling_ReturnsAlert_EvenAfterRecentHowl()
        {
            HowlingEngagementPolicy policy = new HowlingEngagementPolicy();

            // 순찰 중에는 이력이 있든 없든 결과가 같아야 한다.
            // 이 구분이 무너지면 순찰하던 K-9 이 짖지도 않고 달려든다.
            Assert.AreEqual(
                EnemyState.Alert, policy.OnSighted(EnemyState.Patrol, true),
                "순찰 + 시야 + 최근 하울링 → Alert");

            Assert.AreEqual(
                EnemyState.Alert, policy.OnSighted(EnemyState.Patrol, false),
                "순찰 + 시야 + 하울링 없음 → Alert");
        }

        [Test]
        [Description("기준5 - 하울링 정책은 조사 중 시야에서만 하울링 이력을 본다")]
        public void HowlingPolicy_SightedWhileInvestigating_ReturnsChase_OnlyAfterRecentHowl()
        {
            HowlingEngagementPolicy policy = new HowlingEngagementPolicy();

            // 위 테스트와 나란히 둔다 — 같은 howledRecently: true 인데 current 만 다르다.
            Assert.AreEqual(
                EnemyState.Chase, policy.OnSighted(EnemyState.Investigate, true),
                "조사 + 시야 + 최근 하울링 → Chase (다시 짖지 않는다)");

            Assert.AreEqual(
                EnemyState.Alert, policy.OnSighted(EnemyState.Investigate, false),
                "조사 + 시야 + 하울링 없음 → Alert (하울링부터 다시 낸다)");
        }

        [Test]
        [Description("기준5 - 하울링 정책의 접촉은 어느 상태에서든 Alert 다")]
        public void HowlingPolicy_Contact_ReturnsAlert_InEveryState()
        {
            HowlingEngagementPolicy policy = new HowlingEngagementPolicy();

            Assert.AreEqual(EnemyState.Alert, policy.OnContact(EnemyState.Patrol), "순찰");
            Assert.AreEqual(EnemyState.Alert, policy.OnContact(EnemyState.Investigate), "조사");
            Assert.AreEqual(EnemyState.Alert, policy.OnContact(EnemyState.Alert), "경계");
            Assert.AreEqual(EnemyState.Alert, policy.OnContact(EnemyState.Chase), "추격");
        }

        [Test]
        [Description("기준2·3·4 - 직행 정책은 상태·하울링 이력과 무관하게 언제나 Chase 다")]
        public void DirectChasePolicy_AnyDetection_ReturnsChase_RegardlessOfStateOrHowl()
        {
            DirectChaseEngagementPolicy policy = new DirectChaseEngagementPolicy();

            EnemyState[] states =
            {
                EnemyState.Patrol,
                EnemyState.Investigate,
                EnemyState.Alert,
                EnemyState.Chase
            };

            for (int i = 0; i < states.Length; i++)
            {
                Assert.AreEqual(
                    EnemyState.Chase, policy.OnContact(states[i]),
                    "접촉 - " + states[i]);

                Assert.AreEqual(
                    EnemyState.Chase, policy.OnSighted(states[i], false),
                    "시야(하울링 없음) - " + states[i]);

                Assert.AreEqual(
                    EnemyState.Chase, policy.OnSighted(states[i], true),
                    "시야(최근 하울링) - " + states[i]);
            }
        }

        // ── (나) 두뇌에 주입한 통합 동작 ────────────────────────────

        [Test]
        [Description("기준2 - 직행 정책을 주입한 두뇌는 순찰 중 시야 포착에서 Alert 를 건너뛰고 Chase 로 간다")]
        public void Brain_DirectChasePolicy_SightedWhilePatrolling_EntersChase()
        {
            PolicyRig rig = new PolicyRig(new DirectChaseEngagementPolicy());
            Assert.AreEqual(EnemyState.Patrol, rig.Brain.State, "사전 조건: 순찰");

            Vector3 seen = new Vector3(4f, 0f, 0f);
            rig.Source.Report(EnemyDetection.Sight(seen));
            rig.Brain.Tick(0.1f);

            Assert.AreEqual(EnemyState.Chase, rig.Brain.State, "시야 포착 즉시 Chase");
            Assert.AreEqual(0, rig.Alert.BroadcastCount, "하울링을 거치지 않는다");
            Assert.AreEqual(seen, rig.Brain.CurrentTarget, "본 좌표로 달린다");
        }

        [Test]
        [Description("기준3 - 직행 정책을 주입한 두뇌는 순찰 중 접촉에서 곧바로 Chase 로 간다")]
        public void Brain_DirectChasePolicy_ContactWhilePatrolling_EntersChase()
        {
            PolicyRig rig = new PolicyRig(new DirectChaseEngagementPolicy());

            Vector3 touched = new Vector3(1f, 0f, 2f);
            rig.Source.Report(EnemyDetection.Contact(touched));
            rig.Brain.Tick(0.1f);

            Assert.AreEqual(EnemyState.Chase, rig.Brain.State, "접촉 즉시 Chase");
            Assert.AreEqual(0, rig.Alert.BroadcastCount, "하울링을 거치지 않는다");
        }

        [Test]
        [Description("기준4 - 직행 정책을 주입한 두뇌는 조사 중 시야 포착에서 하울링 이력 없이도 Chase 로 간다")]
        public void Brain_DirectChasePolicy_SightedWhileInvestigating_EntersChase_WithoutHowl()
        {
            PolicyRig rig = new PolicyRig(new DirectChaseEngagementPolicy());

            // 흔적으로 조사에 내려간다. 이 경로에서는 하울링이 한 번도 나가지 않는다.
            rig.Source.Report(EnemyDetection.Trace(new Vector3(3f, 0f, 0f)));
            rig.Brain.Tick(0.1f);
            Assert.AreEqual(EnemyState.Investigate, rig.Brain.State, "사전 조건: 조사");
            Assert.AreEqual(0, rig.Alert.BroadcastCount, "사전 조건: 하울링 이력 없음");

            Vector3 seen = new Vector3(6f, 0f, 1f);
            rig.Source.Report(EnemyDetection.Sight(seen));
            rig.Brain.Tick(0.1f);

            Assert.AreEqual(EnemyState.Chase, rig.Brain.State, "하울링 이력과 무관하게 Chase");
            Assert.AreEqual(0, rig.Alert.BroadcastCount, "끝까지 짖지 않는다");
        }

        [Test]
        [Description("기준1·6 - 정책을 넣지 않은(null) 두뇌는 하울링 정책과 같은 전이를 한다")]
        public void Brain_NullPolicy_SightedWhilePatrolling_EntersAlert_LikeHowlingDefault()
        {
            // 정책 부품이 없으면 조립부는 null 을 그대로 넘긴다.
            // 그때 두뇌가 K-9 기본으로 채우는지를 본다 — 기존 76건의 안전망과 같은 경로다.
            PolicyRig implicitDefault = new PolicyRig(null);
            PolicyRig explicitHowling = new PolicyRig(new HowlingEngagementPolicy());

            Vector3 seen = new Vector3(4f, 0f, 0f);

            implicitDefault.Source.Report(EnemyDetection.Sight(seen));
            implicitDefault.Brain.Tick(0.1f);

            explicitHowling.Source.Report(EnemyDetection.Sight(seen));
            explicitHowling.Brain.Tick(0.1f);

            Assert.AreEqual(EnemyState.Alert, implicitDefault.Brain.State, "null → 하울링 기본");
            Assert.AreEqual(
                explicitHowling.Brain.State, implicitDefault.Brain.State,
                "명시 주입과 결과가 같다");
            Assert.AreEqual(
                explicitHowling.Alert.BroadcastCount, implicitDefault.Alert.BroadcastCount,
                "하울링 횟수도 같다");
        }

        [Test]
        [Description("기준1 - 정책을 넣지 않은 두뇌는 조사 중 접촉에서 여전히 Alert 로 간다")]
        public void Brain_NullPolicy_ContactWhileInvestigating_EntersAlert()
        {
            PolicyRig rig = new PolicyRig(null);

            rig.Source.Report(EnemyDetection.Trace(new Vector3(3f, 0f, 0f)));
            rig.Brain.Tick(0.1f);
            Assert.AreEqual(EnemyState.Investigate, rig.Brain.State, "사전 조건: 조사");

            rig.Source.Report(EnemyDetection.Contact(new Vector3(3.2f, 0f, 0f)));
            rig.Brain.Tick(0.1f);

            Assert.AreEqual(EnemyState.Alert, rig.Brain.State, "접촉은 언제나 Alert 다");
            Assert.AreEqual(1, rig.Alert.BroadcastCount, "하울링 1회");
        }

        /// <summary>
        /// 정책만 갈아 끼울 수 있는 최소 조립대.
        /// 나머지 부품은 <c>EnemyBrainTests.cs</c> 의 스텁을 그대로 쓴다.
        /// </summary>
        private sealed class PolicyRig
        {
            public readonly EnemySettings Settings = new EnemySettings();
            public readonly FakeMotor Motor = new FakeMotor();
            public readonly SpyAlertChannel Alert = new SpyAlertChannel();
            public readonly FakePerceptionSource Source = new FakePerceptionSource();
            public readonly FakeTargetTracker Tracker = new FakeTargetTracker();
            public readonly FakeWanderPointProvider Wander = new FakeWanderPointProvider();
            public readonly List<IEnemyPerceptionSource> Sources = new List<IEnemyPerceptionSource>(4);
            public readonly EnemyBrain Brain;

            public PolicyRig(IEnemyEngagementPolicy engagement)
            {
                Sources.Add(Source);
                Brain = new EnemyBrain(
                    Settings, Motor, Sources, Alert, Tracker, Wander, engagement);
            }
        }
    }
}
