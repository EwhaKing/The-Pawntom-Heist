using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Pawntom.Enemy.Core;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using ConstraintIs = UnityEngine.TestTools.Constraints.Is;

namespace Pawntom.Enemy.Tests
{
    /// <summary>
    /// TASK-002 5.3 수용 기준 1~6 의 기계 검증.
    /// 테스트 이름의 Transition 번호는 TASK-002 5.2 상태 전환표의 행 번호와 1:1 대응한다.
    /// </summary>
    public sealed class K9BrainTests
    {
        // ── 기준 1: Core 는 엔진 타입에 의존하지 않는다 ──────────────

        [Test]
        [Description("기준1 - Core 하위 스크립트가 금지된 엔진 타입을 참조하지 않는다")]
        public void Criterion1_CoreScripts_DoNotReferenceEngineTypes()
        {
            string[] anchors = Directory.GetFiles(
                Application.dataPath, "K9Brain.cs", SearchOption.AllDirectories);

            Assert.AreEqual(1, anchors.Length, "K9Brain.cs 를 정확히 하나 찾지 못했다");

            string coreDirectory = Path.GetDirectoryName(anchors[0]);
            string[] files = Directory.GetFiles(coreDirectory, "*.cs", SearchOption.AllDirectories);

            Assert.Greater(files.Length, 0, "Core 폴더에서 스크립트를 찾지 못했다: " + coreDirectory);

            string[] banned =
            {
                "MonoBehaviour",
                "GameObject",
                "Component",
                "NavMeshAgent",
                "Fusion"
            };

            List<string> violations = new List<string>();

            for (int i = 0; i < files.Length; i++)
            {
                string text = File.ReadAllText(files[i]);

                for (int b = 0; b < banned.Length; b++)
                {
                    if (text.Contains(banned[b]))
                    {
                        violations.Add(Path.GetFileName(files[i]) + " -> " + banned[b]);
                    }
                }
            }

            Assert.IsEmpty(violations, "금지 타입 참조: " + string.Join(" / ", violations));
        }

        // ── 기준 2: 상태별 유지 행동 4종 ────────────────────────────

        [Test]
        [Description("기준2 Patrol - 웨이포인트를 지정한 순서대로 순회하고 loop 면 처음으로 돌아온다")]
        public void Criterion2_Patrol_VisitsWaypointsInOrder_WhenLooping()
        {
            Rig rig = new Rig();
            List<Vector3> route = MakeRoute(3);
            rig.Brain.SetPatrolRoute(route, true);
            rig.Motor.Arrived = true;

            for (int i = 0; i < 5; i++)
            {
                rig.Brain.Tick(1f);
            }

            CollectionAssert.AreEqual(
                new[] { route[0], route[1], route[2], route[0], route[1] },
                rig.Motor.Destinations);

            Assert.AreEqual(K9State.Patrol, rig.Brain.State);
            Assert.AreEqual(rig.Settings.PatrolSpeed, rig.Motor.LastSpeed, 0.0001f);
        }

        [Test]
        [Description("기준2 Patrol - loop 가 false 면 마지막 지점에서 멈춘다")]
        public void Criterion2_Patrol_StopsAtLastWaypoint_WhenNotLooping()
        {
            Rig rig = new Rig();
            List<Vector3> route = MakeRoute(3);
            rig.Brain.SetPatrolRoute(route, false);
            rig.Motor.Arrived = true;

            for (int i = 0; i < 8; i++)
            {
                rig.Brain.Tick(1f);
            }

            CollectionAssert.AreEqual(
                new[] { route[0], route[1], route[2] },
                rig.Motor.Destinations);

            Assert.AreEqual(1, rig.Motor.StopCount, "정지는 한 번만 나가야 한다");
        }

        [Test]
        [Description("기준2 Patrol - 웨이포인트가 0개여도 예외 없이 제자리 대기한다")]
        public void Criterion2_Patrol_WithZeroWaypoints_StaysPutWithoutException()
        {
            Rig rig = new Rig();
            rig.Brain.SetPatrolRoute(new List<Vector3>(), true);

            Assert.DoesNotThrow(delegate
            {
                for (int i = 0; i < 5; i++)
                {
                    rig.Brain.Tick(1f);
                }
            });

            Assert.AreEqual(0, rig.Motor.MoveToCount);
            Assert.AreEqual(1, rig.Motor.StopCount);
            Assert.AreEqual(K9State.Patrol, rig.Brain.State);
            Assert.IsNull(rig.Brain.CurrentTarget);
        }

        [Test]
        [Description("기준2 Patrol - 웨이포인트가 1개여도 예외 없이 순회한다")]
        public void Criterion2_Patrol_WithSingleWaypoint_DoesNotThrow()
        {
            Rig rig = new Rig();
            List<Vector3> route = MakeRoute(1);
            rig.Brain.SetPatrolRoute(route, true);
            rig.Motor.Arrived = true;

            Assert.DoesNotThrow(delegate
            {
                for (int i = 0; i < 5; i++)
                {
                    rig.Brain.Tick(1f);
                }
            });

            Assert.AreEqual(route[0], rig.Motor.LastDestination);
            Assert.AreEqual(K9State.Patrol, rig.Brain.State);
        }

        [Test]
        [Description("기준2 Alert - 하울링 시간 내 시야 미확보면 상태가 유지되고 정지 이후 이동 명령이 없다")]
        public void Criterion2_Alert_HoldsPosition_AndIssuesNoMoveCommand()
        {
            Rig rig = new Rig();
            rig.Source.Report(K9Detection.Contact(new Vector3(3f, 0f, 0f)));
            rig.Brain.Tick(0.1f);

            Assert.AreEqual(K9State.Alert, rig.Brain.State);
            Assert.AreEqual(1, rig.Motor.StopCount);
            Assert.AreEqual(0, rig.Motor.MoveToCount);

            // 접촉이 계속돼도 하울링은 다시 나가지 않는다.
            rig.Brain.Tick(0.5f);
            rig.Brain.Tick(0.5f);

            Assert.AreEqual(K9State.Alert, rig.Brain.State);
            Assert.AreEqual(0, rig.Motor.MoveToCount, "Alert 유지 중에는 이동 명령이 없어야 한다");
            Assert.AreEqual(1, rig.Alert.BroadcastCount, "하울링은 진입 시 1회뿐이어야 한다");
        }

        [Test]
        [Description("기준2 Chase - 시야 유지 중 매 틱 목표가 갱신되고 추격 속도가 쓰인다")]
        public void Criterion2_Chase_UpdatesTargetEachTick_WithChaseSpeed()
        {
            Rig rig = new Rig();

            // Alert 를 거쳐 Chase 로 들어간다. 도우미가 그 절차를 담는다.
            Vector3 first = new Vector3(5f, 0f, 0f);
            EnterChase(rig, first);

            int moveCountAfterEnter = rig.Motor.MoveToCount;

            Vector3 second = new Vector3(7f, 0f, 2f);
            rig.Source.Report(K9Detection.Sight(second));
            rig.Brain.Tick(0.1f);

            Vector3 third = new Vector3(9f, 0f, 4f);
            rig.Source.Report(K9Detection.Sight(third));
            rig.Brain.Tick(0.1f);

            Assert.AreEqual(moveCountAfterEnter + 2, rig.Motor.MoveToCount, "시야 유지 중 매 틱 갱신되어야 한다");
            Assert.AreEqual(third, rig.Motor.LastDestination);
            Assert.AreEqual(rig.Settings.ChaseSpeed, rig.Motor.LastSpeed, 0.0001f);
            Assert.AreNotEqual(rig.Settings.PatrolSpeed, rig.Motor.LastSpeed);
        }

        [Test]
        [Description("기준2 Investigate - 목표에 도달해도 무감지 타이머는 초기화되지 않는다")]
        public void Criterion2_Investigate_ArrivalDoesNotResetGiveUpTimer()
        {
            Rig rig = new Rig();
            EnterInvestigate(rig);

            // 도달 상태로 두고 시간을 흘려보낸다.
            rig.Motor.Arrived = true;
            rig.Brain.Tick(rig.Settings.InvestigateGiveUpSeconds);

            Assert.AreEqual(K9State.Patrol, rig.Brain.State, "도달로 타이머가 초기화되면 안 된다");
        }

        // ── 기준 3: 전환 8개 ───────────────────────────────────────

        [Test]
        [Description("전환1 Patrol -> Investigate (감지)")]
        public void Transition1_Patrol_ToInvestigate_OnDetection()
        {
            Rig rig = new Rig();
            Vector3 trace = new Vector3(4f, 0f, 1f);
            rig.Source.Report(K9Detection.Trace(trace));

            bool changed = rig.Brain.Tick(0.1f);

            Assert.IsTrue(changed);
            Assert.AreEqual(K9State.Investigate, rig.Brain.State);
            Assert.AreEqual(trace, rig.Motor.LastDestination);
            Assert.AreEqual(trace, rig.Brain.CurrentTarget);
        }

        [Test]
        [Description("전환2 Patrol -> Investigate (하울링 소집)")]
        public void Transition2_Patrol_ToInvestigate_OnSummon()
        {
            Rig rig = new Rig();
            Vector3 summon = new Vector3(-6f, 0f, 2f);
            rig.Alert.HasSummon = true;
            rig.Alert.SummonTarget = summon;

            bool changed = rig.Brain.Tick(0.1f);

            Assert.IsTrue(changed);
            Assert.AreEqual(K9State.Investigate, rig.Brain.State);
            Assert.AreEqual(summon, rig.Motor.LastDestination);
        }

        [Test]
        [Description("전환3 Patrol -> Alert (접촉) + 진입 시 하울링 1회")]
        public void Transition3_Patrol_ToAlert_OnContact_BroadcastsOnce()
        {
            Rig rig = new Rig();
            rig.Motor.CurrentPosition = new Vector3(1f, 2f, 3f);
            rig.Source.Report(K9Detection.Contact(new Vector3(1.4f, 2f, 3f)));

            bool changed = rig.Brain.Tick(0.1f);

            Assert.IsTrue(changed);
            Assert.AreEqual(K9State.Alert, rig.Brain.State);
            Assert.AreEqual(1, rig.Alert.BroadcastCount);
            Assert.AreEqual(new Vector3(1f, 2f, 3f), rig.Alert.LastOrigin, "하울링 원점은 자기 위치여야 한다");
            Assert.AreEqual(30f, rig.Alert.LastRadius, 0.0001f);
            Assert.AreEqual(1, rig.Motor.StopCount);
        }

        [Test]
        [Description("전환4 Investigate -> Patrol (20초 무감지)")]
        public void Transition4_Investigate_ToPatrol_After20Seconds()
        {
            Rig rig = new Rig();
            EnterInvestigate(rig);

            bool changed = rig.Brain.Tick(20f);

            Assert.IsTrue(changed);
            Assert.AreEqual(K9State.Patrol, rig.Brain.State);
        }

        [Test]
        [Description("전환5 Investigate -> Alert (접촉) + 진입 시 하울링 1회")]
        public void Transition5_Investigate_ToAlert_OnContact_BroadcastsOnce()
        {
            Rig rig = new Rig();
            EnterInvestigate(rig);
            Assert.AreEqual(0, rig.Alert.BroadcastCount);

            rig.Motor.CurrentPosition = new Vector3(0f, 0f, 8f);
            rig.Source.Report(K9Detection.Contact(new Vector3(0f, 0f, 9f)));

            bool changed = rig.Brain.Tick(0.1f);

            Assert.IsTrue(changed);
            Assert.AreEqual(K9State.Alert, rig.Brain.State);
            Assert.AreEqual(1, rig.Alert.BroadcastCount);
            Assert.AreEqual(new Vector3(0f, 0f, 8f), rig.Alert.LastOrigin);
            Assert.AreEqual(30f, rig.Alert.LastRadius, 0.0001f);
        }

        [Test]
        [Description("전환6 Alert -> Chase (경계 유지 시간이 지난 시점에 시야 유지)")]
        public void Transition6_Alert_ToChase_WhenSightRemains_AfterAlertHoldElapses()
        {
            Rig rig = new Rig();

            // 유지 시간이 0(기본값)이면 시야가 들어온 첫 틱에 이미 Chase 라 "직전/직후"가 나뉘지 않는다.
            // 두 구간을 구분하기 위해 유지 시간을 명시한다(TASK-008 4.1).
            SetSettingsField(rig.Settings, "_alertHoldSeconds", rig.Settings.HowlDurationSeconds);

            EnterAlert(rig);

            Vector3 seen = new Vector3(2f, 0f, 5f);
            rig.Source.Report(K9Detection.Sight(seen));

            // 유지 시간 안에는 시야가 있어도 Chase 로 가지 않는다.
            bool early = rig.Brain.Tick(rig.Settings.AlertHoldSeconds * 0.5f);

            Assert.IsFalse(early, "유지 시간 안의 시야는 상태를 바꾸지 않는다");
            Assert.AreEqual(K9State.Alert, rig.Brain.State);
            Assert.AreEqual(0, rig.Motor.MoveToCount);

            // 유지 시간이 경과하는 틱에 시야가 있으면 Chase 로 간다.
            bool changed = rig.Brain.Tick(rig.Settings.AlertHoldSeconds);

            Assert.IsTrue(changed);
            Assert.AreEqual(K9State.Chase, rig.Brain.State);
            Assert.AreEqual(seen, rig.Motor.LastDestination);
            Assert.AreEqual(rig.Settings.ChaseSpeed, rig.Motor.LastSpeed, 0.0001f);
        }

        [Test]
        [Description("전환6 음성 케이스 - 시야가 없으면 Alert 에서 Chase 로 가지 않는다")]
        public void Transition6_Negative_Alert_DoesNotChase_WithoutSight()
        {
            Rig rig = new Rig();
            EnterAlert(rig);

            // 접촉은 계속되지만 시야는 없다. 하울링 시간 안이므로 Alert 이 유지된다.
            rig.Brain.Tick(0.5f);
            rig.Brain.Tick(0.5f);

            Assert.AreEqual(K9State.Alert, rig.Brain.State, "시야 없이 Chase 로 가면 안 된다");
        }

        [Test]
        [Description("전환7 Alert -> Investigate (하울링 시간 경과 + 시야 미확보)")]
        public void Transition7_Alert_ToInvestigate_AfterHowlWithoutSight()
        {
            Rig rig = new Rig();
            Vector3 contact = new Vector3(3f, 0f, -2f);
            rig.Source.Report(K9Detection.Contact(contact));
            rig.Brain.Tick(0.1f);
            Assert.AreEqual(K9State.Alert, rig.Brain.State);

            rig.Source.Clear();
            bool changed = rig.Brain.Tick(1.5f);

            Assert.IsTrue(changed);
            Assert.AreEqual(K9State.Investigate, rig.Brain.State);
            Assert.AreEqual(contact, rig.Motor.LastDestination, "마지막 접촉 좌표로 조사해야 한다");
        }

        [Test]
        [Description("전환8 Chase -> Investigate (5초 시야 소실)")]
        public void Transition8_Chase_ToInvestigate_AfterLosingSightFor5Seconds()
        {
            Rig rig = new Rig();
            EnterChase(rig, new Vector3(6f, 0f, 6f));

            rig.Source.Clear();
            bool changed = rig.Brain.Tick(5f);

            Assert.IsTrue(changed);
            Assert.AreEqual(K9State.Investigate, rig.Brain.State);
            Assert.AreEqual(new Vector3(6f, 0f, 6f), rig.Motor.LastDestination, "마지막 목격 좌표로 조사해야 한다");
        }

        // ── TASK-003 기준 1~4: 하울링 소비 범위 ──────────────────────

        [Test]
        [Description("TASK-003 기준1 - Chase 중 소집이 도착해도 상태와 목표가 바뀌지 않는다")]
        public void Task003_Chase_IgnoresSummon_KeepsStateAndTarget()
        {
            Rig rig = new Rig();
            Vector3 seen = new Vector3(6f, 0f, 6f);
            EnterChase(rig, seen);

            Vector3 summon = new Vector3(-30f, 0f, -30f);
            rig.Alert.HasSummon = true;
            rig.Alert.SummonTarget = summon;

            bool changed = rig.Brain.Tick(0.1f);

            Assert.IsFalse(changed, "소집으로 상태가 바뀌면 안 된다");
            Assert.AreEqual(K9State.Chase, rig.Brain.State);
            Assert.AreEqual(seen, rig.Motor.LastDestination, "목표는 시야 좌표 그대로여야 한다");
            Assert.AreEqual(seen, rig.Brain.CurrentTarget);
        }

        [Test]
        [Description("TASK-003 기준2 - Chase 중 받은 소집은 폐기되어 Patrol 복귀 뒤에도 발동하지 않는다")]
        public void Task003_Chase_DiscardsSummon_DoesNotFireAfterReturningToPatrol()
        {
            Rig rig = new Rig();
            EnterChase(rig, new Vector3(6f, 0f, 6f));

            rig.Alert.HasSummon = true;
            rig.Alert.SummonTarget = new Vector3(-30f, 0f, -30f);

            rig.Brain.Tick(0.1f);
            Assert.IsFalse(rig.Alert.HasSummon, "Chase 는 소집을 읽어 채널을 비워야 한다");

            // 시야 상실 → 전환8 → Investigate
            rig.Source.Clear();
            rig.Brain.Tick(5f);
            Assert.AreEqual(K9State.Investigate, rig.Brain.State);

            // 무감지 20초 → 전환4 → Patrol
            rig.Brain.Tick(20f);
            Assert.AreEqual(K9State.Patrol, rig.Brain.State);

            // 소집이 채널에 남아 있었다면 여기서 Investigate 로 끌려간다.
            bool changed = rig.Brain.Tick(0.1f);

            Assert.IsFalse(changed, "폐기된 소집이 뒤늦게 발동하면 안 된다");
            Assert.AreEqual(K9State.Patrol, rig.Brain.State);
        }

        [Test]
        [Description("TASK-003 기준3 - Investigate 중 소집을 받으면 목표가 교체되고 무감지 타이머가 초기화된다")]
        public void Task003_Investigate_OnSummon_ReplacesTarget_AndResetsGiveUpTimer()
        {
            Rig rig = new Rig();
            EnterInvestigate(rig);

            rig.Brain.Tick(19f);
            Assert.AreEqual(K9State.Investigate, rig.Brain.State, "사전 조건: 아직 포기 전");

            Vector3 summon = new Vector3(-14f, 0f, 7f);
            rig.Alert.HasSummon = true;
            rig.Alert.SummonTarget = summon;

            bool changed = rig.Brain.Tick(19f);

            Assert.IsFalse(changed, "상태는 Investigate 로 유지된다");
            Assert.AreEqual(K9State.Investigate, rig.Brain.State);
            Assert.AreEqual(summon, rig.Motor.LastDestination, "목표가 소집 좌표로 교체되어야 한다");
            Assert.AreEqual(summon, rig.Brain.CurrentTarget);

            // 리셋이 없었다면 누적 38초로 이미 Patrol 이다. 리셋 후 19 < 20 이라 유지된다.
            rig.Brain.Tick(19f);

            Assert.AreEqual(K9State.Investigate, rig.Brain.State, "무감지 타이머가 0 으로 되돌아가야 한다");
        }

        [Test]
        [Description("TASK-003 기준3 보조 - 같은 틱에 감지와 소집이 겹치면 감지가 이기고 소집은 소비된다")]
        public void Task003_Investigate_DetectionWinsOverSummon_ButSummonIsStillConsumed()
        {
            Rig rig = new Rig();
            EnterInvestigate(rig);

            Vector3 trace = new Vector3(2f, 0f, 3f);
            rig.Source.Report(K9Detection.Trace(trace));
            rig.Alert.HasSummon = true;
            rig.Alert.SummonTarget = new Vector3(-25f, 0f, 1f);

            rig.Brain.Tick(0.1f);

            Assert.AreEqual(K9State.Investigate, rig.Brain.State);
            Assert.AreEqual(trace, rig.Motor.LastDestination, "직접 본 좌표가 우선이다");
            Assert.IsFalse(rig.Alert.HasSummon, "감지가 이겨도 소집은 채널에 남지 않는다");
        }

        [Test]
        [Description("TASK-003 기준4 - Alert 중 소집은 상태를 바꾸지 않고, 전환7 의 목표가 소집 좌표가 된다")]
        public void Task003_Alert_KeepsState_OnSummon_AndUsesItAsInvestigateTarget()
        {
            Rig rig = new Rig();
            Vector3 contact = new Vector3(3f, 0f, -2f);
            rig.Source.Report(K9Detection.Contact(contact));
            rig.Brain.Tick(0.1f);
            Assert.AreEqual(K9State.Alert, rig.Brain.State, "사전 조건: Alert 진입");
            rig.Source.Clear();

            Vector3 summon = new Vector3(-8f, 0f, 12f);
            rig.Alert.HasSummon = true;
            rig.Alert.SummonTarget = summon;

            bool changed = rig.Brain.Tick(0.5f);

            Assert.IsFalse(changed, "소집으로 Alert 을 벗어나면 안 된다");
            Assert.AreEqual(K9State.Alert, rig.Brain.State);
            Assert.AreEqual(0, rig.Motor.MoveToCount, "Alert 유지 중에는 이동 명령이 없어야 한다");

            // 전환7: 하울링 시간 경과 + 시야 미확보
            bool left = rig.Brain.Tick(1f);

            Assert.IsTrue(left);
            Assert.AreEqual(K9State.Investigate, rig.Brain.State);
            Assert.AreEqual(summon, rig.Motor.LastDestination, "보관해 둔 소집 좌표로 조사해야 한다");
        }

        [Test]
        [Description("TASK-003 기준4 보조 - Alert 중 소집이 없으면 전환7 목표는 마지막 접촉 좌표 그대로다")]
        public void Task003_Alert_WithoutSummon_StillUsesLastContactPoint()
        {
            Rig rig = new Rig();
            Vector3 contact = new Vector3(3f, 0f, -2f);
            rig.Source.Report(K9Detection.Contact(contact));
            rig.Brain.Tick(0.1f);
            rig.Source.Clear();

            rig.Brain.Tick(1.5f);

            Assert.AreEqual(K9State.Investigate, rig.Brain.State);
            Assert.AreEqual(contact, rig.Motor.LastDestination);
        }

        // ── 기준 4: 시간 임계값 (각각 단일 Tick 호출) ────────────────

        [Test]
        [Description("기준4 - Investigate 는 19.9초에서 전환되지 않는다")]
        public void Criterion4_Investigate_DoesNotGiveUp_At19Point9()
        {
            Rig rig = new Rig();
            EnterInvestigate(rig);

            bool changed = rig.Brain.Tick(19.9f);

            Assert.IsFalse(changed);
            Assert.AreEqual(K9State.Investigate, rig.Brain.State);
        }

        [Test]
        [Description("기준4 - Chase 는 4.9초에서 전환되지 않는다")]
        public void Criterion4_Chase_DoesNotGiveUp_At4Point9()
        {
            Rig rig = new Rig();
            EnterChase(rig, new Vector3(1f, 0f, 1f));
            rig.Source.Clear();

            bool changed = rig.Brain.Tick(4.9f);

            Assert.IsFalse(changed);
            Assert.AreEqual(K9State.Chase, rig.Brain.State);
        }

        [Test]
        [Description("기준4 - Alert 는 1.4초에서 전환되지 않는다")]
        public void Criterion4_Alert_DoesNotLeave_At1Point4()
        {
            Rig rig = new Rig();
            EnterAlert(rig);
            rig.Source.Clear();

            bool changed = rig.Brain.Tick(1.4f);

            Assert.IsFalse(changed);
            Assert.AreEqual(K9State.Alert, rig.Brain.State);
        }

        // ── 기준 5: OCP — 감지 소스 추가만으로 확장된다 ───────────────

        [Test]
        [Description("기준5 - 새로 만든 감지 소스를 주입해도 K9Brain 수정 없이 Investigate 로 들어간다")]
        public void Criterion5_NewPerceptionSource_DrivesInvestigate_WithoutBrainChange()
        {
            // K9Brain 은 이 타입의 존재를 모른다. 리스트에 넣기만 한다.
            FurballTraceStubSource newSource = new FurballTraceStubSource();
            newSource.TracePosition = new Vector3(12f, 0f, -3f);
            newSource.Active = true;

            K9Settings settings = new K9Settings();
            FakeMotor motor = new FakeMotor();
            SpyAlertChannel alert = new SpyAlertChannel();
            FakePerceptionSource existing = new FakePerceptionSource();

            List<IK9PerceptionSource> sources = new List<IK9PerceptionSource>();
            sources.Add(existing);
            sources.Add(newSource);

            K9Brain brain = new K9Brain(settings, motor, sources, alert);

            bool changed = brain.Tick(0.1f);

            Assert.IsTrue(changed);
            Assert.AreEqual(K9State.Investigate, brain.State);
            Assert.AreEqual(newSource.TracePosition, motor.LastDestination);
            Assert.AreEqual(1, existing.TryDetectCount, "기존 소스도 그대로 호출되어야 한다");
            Assert.AreEqual(1, newSource.TryDetectCount);
        }

        // ── 기준 6: 0/음수 Tick, GC 할당 ────────────────────────────

        [Test]
        [Description("기준6 - Tick(0) 은 감지도 전환도 하지 않는다")]
        public void Criterion6_TickZero_ChangesNothing()
        {
            Rig rig = new Rig();
            rig.Source.Report(K9Detection.Contact(new Vector3(1f, 0f, 0f)));

            bool changed = rig.Brain.Tick(0f);

            Assert.IsFalse(changed);
            Assert.AreEqual(K9State.Patrol, rig.Brain.State);
            Assert.AreEqual(0, rig.Motor.MoveToCount);
            Assert.AreEqual(0, rig.Motor.StopCount);
            Assert.AreEqual(0, rig.Source.TryDetectCount);
        }

        [Test]
        [Description("기준6 - Tick(음수) 는 감지도 전환도 하지 않는다")]
        public void Criterion6_TickNegative_ChangesNothing()
        {
            Rig rig = new Rig();
            rig.Source.Report(K9Detection.Sight(new Vector3(1f, 0f, 0f)));

            bool changed = rig.Brain.Tick(-1f);

            Assert.IsFalse(changed);
            Assert.AreEqual(K9State.Patrol, rig.Brain.State);
            Assert.AreEqual(0, rig.Source.TryDetectCount);
        }

        [Test]
        [Description("기준6 - 순찰 중 Tick 이 GC 할당을 만들지 않는다")]
        public void Criterion6_Tick_DoesNotAllocate_WhilePatrolling()
        {
            Rig rig = new Rig();
            rig.Motor.RecordCalls = false;
            rig.Brain.SetPatrolRoute(MakeRoute(3), true);

            for (int i = 0; i < 20; i++)
            {
                rig.Brain.Tick(0.016f);
            }

            K9Brain brain = rig.Brain;
            Assert.That(delegate { brain.Tick(0.016f); }, ConstraintIs.Not.AllocatingGCMemory());
        }

        [Test]
        [Description("기준6 - 추격 중 Tick 이 GC 할당을 만들지 않는다 (시야 없이 트래커로 쫓는 경로)")]
        public void Criterion6_Tick_DoesNotAllocate_WhileChasing()
        {
            Rig rig = new Rig();
            rig.Motor.RecordCalls = false;
            EnterChase(rig, new Vector3(2f, 0f, 2f));

            // 시야를 잃고 트래커로 쫓는 경로까지 함께 잰다(TASK-005 요구 3).
            rig.Source.Clear();
            rig.Tracker.Active = true;
            rig.Tracker.TrackedPosition = new Vector3(3f, 0f, 3f);

            for (int i = 0; i < 20; i++)
            {
                rig.Brain.Tick(0.016f);
            }

            K9Brain brain = rig.Brain;
            Assert.That(delegate { brain.Tick(0.016f); }, ConstraintIs.Not.AllocatingGCMemory());
        }

        [Test]
        [Description("기준6 - 조사 배회 중 Tick 이 GC 할당을 만들지 않는다")]
        public void Criterion6_Tick_DoesNotAllocate_WhileInvestigateWandering()
        {
            Rig rig = new Rig();
            rig.Motor.RecordCalls = false;
            EnterInvestigate(rig);

            rig.Motor.Arrived = true;
            rig.Wander.Active = true;
            rig.Wander.Point = new Vector3(1f, 0f, 1f);

            // 측정 틱에서 실제로 배회 지점을 고르도록 주기와 같은 간격으로 흘린다.
            // 5회 x 2초 = 10초라 무감지 제한 시간(20초) 안에 머무른다.
            float interval = rig.Settings.InvestigateWanderIntervalSeconds;
            for (int i = 0; i < 4; i++)
            {
                rig.Brain.Tick(interval);
            }

            K9Brain brain = rig.Brain;
            Assert.That(delegate { brain.Tick(interval); }, ConstraintIs.Not.AllocatingGCMemory());
        }

        // ── TASK-005: 전환 3b/5b · 하울링 이후 Chase · 시야 밖 추적 · 조사 배회 ──

        [Test]
        [Description("TASK-005 기준1 전환3b - Patrol 에서 시야 포착만으로 Alert 로 가고 하울링이 1회 나간다")]
        public void Transition3b_Patrol_ToAlert_OnSight_BroadcastsOnce()
        {
            Rig rig = new Rig();
            rig.Motor.CurrentPosition = new Vector3(1f, 0f, 2f);

            Vector3 seen = new Vector3(5f, 0f, 5f);
            rig.Source.Report(K9Detection.Sight(seen));

            bool changed = rig.Brain.Tick(0.1f);

            Assert.IsTrue(changed);
            Assert.AreEqual(K9State.Alert, rig.Brain.State, "시야 포착은 Investigate 가 아니라 Alert 로 간다");
            Assert.AreEqual(1, rig.Alert.BroadcastCount, "하울링은 진입 시 1회뿐이어야 한다");
            Assert.AreEqual(new Vector3(1f, 0f, 2f), rig.Alert.LastOrigin, "하울링 원점은 자기 위치여야 한다");
            Assert.AreEqual(1, rig.Motor.StopCount, "진입 시 정지는 1회다");
            Assert.AreEqual(0, rig.Motor.MoveToCount, "Alert 진입 틱에는 이동 명령이 없어야 한다");
        }

        [Test]
        [Description("TASK-008 기준4 전환6 - 경계 유지 시간이 지나기 전에는 시야가 있어도 Chase 로 가지 않는다")]
        public void Transition6_Alert_DoesNotChase_BeforeAlertHoldElapses_EvenWithSight()
        {
            Rig rig = new Rig();

            // TASK-005 에서는 하울링 시간이 이 구속을 만들었다. 이제는 경계 유지 시간이 만든다(TASK-008 2.4).
            SetSettingsField(rig.Settings, "_alertHoldSeconds", rig.Settings.HowlDurationSeconds);
            Assert.Greater(rig.Settings.AlertHoldSeconds, 0f, "사전 조건: 유지 시간이 0보다 커야 한다");

            EnterAlert(rig);

            // 유지 시간 내내 시야가 유지되는 상태로 둔다.
            rig.Source.Report(K9Detection.Sight(new Vector3(2f, 0f, 5f)));

            float slice = rig.Settings.AlertHoldSeconds * 0.4f;
            bool first = rig.Brain.Tick(slice);
            bool second = rig.Brain.Tick(slice);

            Assert.IsFalse(first);
            Assert.IsFalse(second);
            Assert.AreEqual(K9State.Alert, rig.Brain.State, "유지 시간 안의 시야는 상태를 바꾸지 않는다");
            Assert.AreEqual(0, rig.Motor.MoveToCount, "유지 구간에는 이동 명령이 없어야 한다");
        }

        [Test]
        [Description("TASK-005 기준4 전환5b - Investigate 중 시야 포착 시 Alert 로 가고 하울링이 1회 더 나간다")]
        public void Transition5b_Investigate_ToAlert_OnSight_BroadcastsOnce()
        {
            Rig rig = new Rig();
            EnterInvestigate(rig);
            Assert.AreEqual(0, rig.Alert.BroadcastCount, "사전 조건: 아직 하울링이 없다");

            rig.Motor.CurrentPosition = new Vector3(0f, 0f, 4f);
            Vector3 seen = new Vector3(0f, 0f, 9f);
            rig.Source.Report(K9Detection.Sight(seen));

            bool changed = rig.Brain.Tick(0.1f);

            Assert.IsTrue(changed);
            Assert.AreEqual(K9State.Alert, rig.Brain.State);
            Assert.AreEqual(1, rig.Alert.BroadcastCount, "재발견 시 하울링이 다시 나가는 것은 의도된 결과다");
            Assert.AreEqual(new Vector3(0f, 0f, 4f), rig.Alert.LastOrigin);
        }

        [Test]
        [Description("TASK-005 기준5 - Chase 는 시야가 없어도 소실 제한 시간까지 추적 좌표를 매 틱 따라간다")]
        public void Chase_WithoutSight_KeepsFollowingTrackedTarget_UntilLoseSightSeconds()
        {
            Rig rig = new Rig();
            EnterChase(rig, new Vector3(6f, 0f, 6f));

            rig.Source.Clear();
            rig.Tracker.Active = true;

            int moveCountAfterEnter = rig.Motor.MoveToCount;

            for (int i = 1; i <= 3; i++)
            {
                rig.Tracker.TrackedPosition = new Vector3(6f + i, 0f, 6f);
                rig.Brain.Tick(1f);

                Assert.AreEqual(K9State.Chase, rig.Brain.State, "소실 제한 시간 전에는 Chase 를 유지한다");
                Assert.AreEqual(rig.Tracker.TrackedPosition, rig.Motor.LastDestination, "매 틱 추적 좌표로 갱신되어야 한다");
                Assert.AreEqual(rig.Settings.ChaseSpeed, rig.Motor.LastSpeed, 0.0001f);
            }

            Assert.AreEqual(moveCountAfterEnter + 3, rig.Motor.MoveToCount, "매 틱 이동 명령이 나가야 한다");
        }

        [Test]
        [Description("TASK-005 기준6 - 추적 좌표가 없으면 Chase 중 이동 명령이 없고 마지막 목표로 조사에 들어간다")]
        public void Chase_WithoutTracker_IssuesNoMove_AndFallsBackToLastTarget()
        {
            Rig rig = new Rig();
            Vector3 seen = new Vector3(6f, 0f, 6f);
            EnterChase(rig, seen);

            // 트래커는 기본 비활성이다 — TryTrack 이 false 를 돌려준다.
            rig.Source.Clear();
            int moveCountAfterEnter = rig.Motor.MoveToCount;

            bool early = rig.Brain.Tick(rig.Settings.ChaseLoseSightSeconds - 1f);

            Assert.IsFalse(early);
            Assert.AreEqual(K9State.Chase, rig.Brain.State);
            Assert.AreEqual(moveCountAfterEnter, rig.Motor.MoveToCount, "추적 좌표가 없으면 이동 명령을 내지 않는다");

            bool changed = rig.Brain.Tick(1f);

            Assert.IsTrue(changed);
            Assert.AreEqual(K9State.Investigate, rig.Brain.State);
            Assert.AreEqual(seen, rig.Motor.LastDestination, "마지막 목격 좌표로 조사해야 한다");
        }

        [Test]
        [Description("TASK-005 기준7 - 조사 중 도달 후 주기마다 배회하고, 배회가 무감지 타이머를 초기화하지 않는다")]
        public void Investigate_AfterArrival_WandersEachInterval_WithoutResettingGiveUpTimer()
        {
            Rig rig = new Rig();
            EnterInvestigate(rig);

            rig.Motor.Arrived = true;
            rig.Wander.Active = true;

            int moveCountAfterEnter = rig.Motor.MoveToCount;
            float interval = rig.Settings.InvestigateWanderIntervalSeconds;
            float elapsed = 0f;

            for (int i = 1; i <= 3; i++)
            {
                rig.Wander.Point = new Vector3(i, 0f, i);
                rig.Brain.Tick(interval);
                elapsed += interval;

                Assert.AreEqual(K9State.Investigate, rig.Brain.State);
                Assert.AreEqual(rig.Wander.Point, rig.Motor.LastDestination, "배회 지점으로 이동해야 한다");
                Assert.AreEqual(rig.Settings.InvestigateSpeed, rig.Motor.LastSpeed, 0.0001f);
            }

            Assert.AreEqual(moveCountAfterEnter + 3, rig.Motor.MoveToCount, "주기마다 한 번씩만 이동 명령이 나가야 한다");
            Assert.AreEqual(new Vector3(0f, 0f, 10f), rig.Wander.LastAnchor, "배회 기준점은 조사 목표다");
            Assert.AreEqual(rig.Settings.InvestigateWanderRadius, rig.Wander.LastRadius, 0.0001f);

            // 배회가 무감지 타이머를 초기화했다면 여기서 Patrol 로 가지 못한다.
            bool changed = rig.Brain.Tick(rig.Settings.InvestigateGiveUpSeconds - elapsed);

            Assert.IsTrue(changed);
            Assert.AreEqual(K9State.Patrol, rig.Brain.State, "배회는 무감지 타이머를 건드리면 안 된다");
        }

        // ── TASK-006: 경계 조준 · 추격 판정 지연 · 소집 분산 · 조사 속도 ──

        [Test]
        [Description("TASK-006 기준1 - 경계 유지 중 매 틱 시야 좌표를 향해 조준하고 이동 명령은 없다")]
        public void Alert_FacesSightTarget_EachTick_WithoutMoving()
        {
            Rig rig = new Rig();
            EnterAlert(rig);
            rig.Source.Clear();

            Assert.AreEqual(0, rig.Motor.FaceCount, "사전 조건: 진입 틱에는 아직 조준이 없다");

            Vector3 seen = new Vector3(2f, 0f, 5f);
            rig.Source.Report(K9Detection.Sight(seen));

            // 유지 시간이 0(기본값)이면 첫 틱에 Chase 로 나가 조준을 여러 틱 볼 수 없다.
            // 조준만 떼어 보기 위해 유지 시간을 하울링 시간과 같게 올린다(TASK-008 4.1).
            SetSettingsField(rig.Settings, "_alertHoldSeconds", rig.Settings.HowlDurationSeconds);

            // 유지 시간 안에 머무르도록 그 시간의 일부씩만 흘린다(0.3 x 3 = 0.9 < 1).
            float slice = rig.Settings.AlertHoldSeconds * 0.3f;

            for (int i = 1; i <= 3; i++)
            {
                bool changed = rig.Brain.Tick(slice);

                Assert.IsFalse(changed, "유지 시간 안에는 상태가 바뀌지 않는다");
                Assert.AreEqual(i, rig.Motor.FaceCount, "경계 유지 중 매 틱 조준해야 한다");
                Assert.AreEqual(seen, rig.Motor.LastFaceTarget, "시야가 있으면 시야 좌표를 향한다");
                Assert.AreEqual(
                    rig.Settings.AlertTurnSpeedDegrees, rig.Motor.LastFaceDegreesPerSecond, 0.0001f);
                Assert.AreEqual(slice, rig.Motor.LastFaceDeltaTime, 0.0001f);
            }

            Assert.AreEqual(K9State.Alert, rig.Brain.State);
            Assert.AreEqual(0, rig.Motor.MoveToCount, "조준은 이동 명령이 아니다");
        }

        [Test]
        [Description("TASK-006 기준1 - 시야가 없으면 경보 지점을 향해 조준한다")]
        public void Alert_FacesLastContactPoint_WhenSightLost()
        {
            Rig rig = new Rig();
            Vector3 contact = new Vector3(3f, 0f, -2f);
            rig.Source.Report(K9Detection.Contact(contact));
            rig.Brain.Tick(0.1f);
            Assert.AreEqual(K9State.Alert, rig.Brain.State, "사전 조건: Alert 진입");
            rig.Source.Clear();

            rig.Brain.Tick(rig.Settings.HowlDurationSeconds * 0.5f);

            Assert.AreEqual(1, rig.Motor.FaceCount);
            Assert.AreEqual(contact, rig.Motor.LastFaceTarget, "시야가 없으면 경보 지점을 향한다");
            Assert.AreEqual(
                rig.Settings.AlertTurnSpeedDegrees, rig.Motor.LastFaceDegreesPerSecond, 0.0001f);
            Assert.AreEqual(0, rig.Motor.MoveToCount, "조준 중에도 이동 명령은 없다");
        }

        [Test]
        [Description("TASK-008 기준4 - 경계 유지 시간이 남아 있으면 하울링이 끝나도 Chase 로 가지 않는다")]
        public void Alert_DoesNotChase_UntilAlertHoldElapses()
        {
            Rig rig = new Rig();

            // 유지 시간을 하울링 시간보다 길게 준다 — 하울링 경과만으로는 풀리지 않아야 한다.
            SetSettingsField(rig.Settings, "_alertHoldSeconds", rig.Settings.HowlDurationSeconds * 2f);
            Assert.Greater(
                rig.Settings.AlertHoldSeconds, rig.Settings.HowlDurationSeconds,
                "사전 조건: 유지 시간이 하울링 시간보다 길어야 한다");

            EnterAlert(rig);
            rig.Source.Clear();
            rig.Source.Report(K9Detection.Sight(new Vector3(2f, 0f, 5f)));

            bool changed = rig.Brain.Tick(rig.Settings.HowlDurationSeconds);

            Assert.IsFalse(changed, "하울링 경과만으로는 유지 시간이 풀리지 않는다");
            Assert.AreEqual(K9State.Alert, rig.Brain.State, "유지 시간이 남았는데 Chase 로 가면 안 된다");
            Assert.AreEqual(0, rig.Motor.MoveToCount, "유지 구간에도 이동 명령은 없다");
            Assert.AreEqual(1, rig.Motor.FaceCount, "유지 구간에도 조준은 계속된다");
        }

        [Test]
        [Description("TASK-008 기준4 - 경계 유지 시간이 지나는 틱에 시야가 있으면 추격 속도로 Chase 로 간다")]
        public void Alert_ToChase_WhenAlertHoldElapses_WithSight()
        {
            Rig rig = new Rig();
            SetSettingsField(rig.Settings, "_alertHoldSeconds", rig.Settings.HowlDurationSeconds * 2f);

            EnterAlert(rig);
            rig.Source.Clear();

            Vector3 seen = new Vector3(2f, 0f, 5f);
            rig.Source.Report(K9Detection.Sight(seen));

            bool early = rig.Brain.Tick(rig.Settings.HowlDurationSeconds);
            Assert.IsFalse(early, "사전 조건: 아직 유지 시간 안");

            bool changed = rig.Brain.Tick(rig.Settings.AlertHoldSeconds - rig.Settings.HowlDurationSeconds);

            Assert.IsTrue(changed);
            Assert.AreEqual(K9State.Chase, rig.Brain.State);
            Assert.AreEqual(seen, rig.Motor.LastDestination);
            Assert.AreEqual(rig.Settings.ChaseSpeed, rig.Motor.LastSpeed, 0.0001f);
        }

        [Test]
        [Description("TASK-008 기준3 - 유지 시간 기본값 0 이면 경계 진입 다음 틱에 곧바로 Chase 로 간다")]
        public void Alert_ToChase_OnNextTick_WhenAlertHoldIsZero()
        {
            Rig rig = new Rig();
            Assert.AreEqual(
                0f, rig.Settings.AlertHoldSeconds, 0.0001f,
                "경계 유지 시간의 기본값은 0 이어야 한다(TASK-008 2.4)");

            EnterAlert(rig);
            rig.Source.Clear();

            Vector3 seen = new Vector3(2f, 0f, 5f);
            rig.Source.Report(K9Detection.Sight(seen));

            // 하울링 시간의 아주 일부만 흘린다 — 종전 규칙이었다면 여기서 Alert 를 유지했다.
            bool changed = rig.Brain.Tick(rig.Settings.HowlDurationSeconds * 0.1f);

            Assert.IsTrue(changed);
            Assert.AreEqual(K9State.Chase, rig.Brain.State);
            Assert.AreEqual(seen, rig.Motor.LastDestination);
            Assert.AreEqual(rig.Settings.ChaseSpeed, rig.Motor.LastSpeed, 0.0001f);
        }

        [TestCase("Patrol")]
        [TestCase("Investigate")]
        [TestCase("Alert")]
        [Description("TASK-006 기준5·6 - 소집 좌표가 목적지가 되는 세 경로 모두에서 분산 지점이 목적지가 된다")]
        public void Summon_SpreadsDestination_OnAllThreeEntryPaths(string path)
        {
            Rig rig = new Rig();
            Assert.Greater(rig.Settings.SummonSpreadRadius, 0f, "사전 조건: 분산 반경이 0보다 커야 한다");

            Vector3 summon = new Vector3(-6f, 0f, 2f);
            Vector3 spread = new Vector3(-4.5f, 0f, 3.5f);
            rig.Wander.Active = true;
            rig.Wander.Point = spread;

            if (path == "Patrol")
            {
                rig.Alert.HasSummon = true;
                rig.Alert.SummonTarget = summon;

                bool changed = rig.Brain.Tick(0.1f);

                Assert.IsTrue(changed);
                Assert.AreEqual(K9State.Investigate, rig.Brain.State);
            }
            else if (path == "Investigate")
            {
                EnterInvestigate(rig);
                rig.Alert.HasSummon = true;
                rig.Alert.SummonTarget = summon;

                bool changed = rig.Brain.Tick(0.1f);

                Assert.IsFalse(changed, "소집으로 조사를 벗어나지 않는다");
                Assert.AreEqual(K9State.Investigate, rig.Brain.State);
            }
            else
            {
                EnterAlert(rig);
                rig.Source.Clear();
                rig.Alert.HasSummon = true;
                rig.Alert.SummonTarget = summon;

                rig.Brain.Tick(rig.Settings.HowlDurationSeconds * 0.5f);
                Assert.AreEqual(K9State.Alert, rig.Brain.State, "사전 조건: 소집을 보관한 채 경계 유지");

                bool changed = rig.Brain.Tick(rig.Settings.HowlDurationSeconds);

                Assert.IsTrue(changed);
                Assert.AreEqual(K9State.Investigate, rig.Brain.State);
            }

            Assert.AreEqual(spread, rig.Motor.LastDestination, "목적지는 분산 지점이어야 한다");
            Assert.AreEqual(spread, rig.Brain.CurrentTarget);
            Assert.AreEqual(summon, rig.Wander.LastAnchor, "분산 기준점은 소집 좌표다");
            Assert.AreEqual(rig.Settings.SummonSpreadRadius, rig.Wander.LastRadius, 0.0001f);
        }

        [TestCase("RadiusZero")]
        [TestCase("NoProvider")]
        [TestCase("ProviderFails")]
        [Description("TASK-006 기준7 - 반경 0·제공자 없음·지점 선택 실패면 소집 좌표를 그대로 쓴다")]
        public void Summon_UsesExactCoordinate_WhenSpreadDisabledOrFails(string mode)
        {
            Vector3 summon = new Vector3(-6f, 0f, 2f);

            if (mode == "NoProvider")
            {
                // 4인자 생성자 — 지점 제공자가 아예 없다.
                K9Settings settings = new K9Settings();
                FakeMotor motor = new FakeMotor();
                SpyAlertChannel alert = new SpyAlertChannel();
                List<IK9PerceptionSource> sources = new List<IK9PerceptionSource>(1);
                sources.Add(new FakePerceptionSource());
                K9Brain brain = new K9Brain(settings, motor, sources, alert);

                Assert.Greater(settings.SummonSpreadRadius, 0f, "사전 조건: 반경은 0보다 크다");
                alert.HasSummon = true;
                alert.SummonTarget = summon;

                bool noProviderChanged = brain.Tick(0.1f);

                Assert.IsTrue(noProviderChanged);
                Assert.AreEqual(K9State.Investigate, brain.State);
                Assert.AreEqual(summon, motor.LastDestination, "제공자가 없으면 소집 좌표 그대로다");
                return;
            }

            Rig rig = new Rig();
            rig.Wander.Point = new Vector3(-4.5f, 0f, 3.5f);

            if (mode == "RadiusZero")
            {
                rig.Wander.Active = true;
                SetSettingsField(rig.Settings, "_summonSpreadRadius", 0f);
                Assert.AreEqual(0f, rig.Settings.SummonSpreadRadius, 0.0001f, "사전 조건: 반경 0");
            }
            else
            {
                Assert.Greater(rig.Settings.SummonSpreadRadius, 0f, "사전 조건: 반경은 0보다 크다");
                Assert.IsFalse(rig.Wander.Active, "사전 조건: 지점 선택이 실패한다");
            }

            rig.Alert.HasSummon = true;
            rig.Alert.SummonTarget = summon;

            bool changed = rig.Brain.Tick(0.1f);

            Assert.IsTrue(changed);
            Assert.AreEqual(K9State.Investigate, rig.Brain.State);
            Assert.AreEqual(summon, rig.Motor.LastDestination, "분산이 안 되면 소집 좌표 그대로다");

            if (mode == "RadiusZero")
            {
                Assert.AreEqual(0, rig.Wander.TryGetPointCount, "반경이 0이면 제공자를 부르지도 않는다");
            }
            else
            {
                Assert.AreEqual(1, rig.Wander.TryGetPointCount, "선택 실패는 호출한 뒤의 결과다");
            }
        }

        [Test]
        [Description("TASK-006 기준8 - 조사 진입·목표 갱신·배회의 이동이 모두 조사 속도를 쓴다")]
        public void Investigate_UsesInvestigateSpeed_OnEnterUpdateAndWander()
        {
            Rig rig = new Rig();
            Assert.AreNotEqual(
                rig.Settings.PatrolSpeed, rig.Settings.InvestigateSpeed,
                "사전 조건: 두 속도가 달라야 구분된다");

            EnterInvestigate(rig);
            Assert.AreEqual(
                rig.Settings.InvestigateSpeed, rig.Motor.LastSpeed, 0.0001f, "조사 진입 이동");

            Vector3 trace = new Vector3(2f, 0f, 3f);
            rig.Source.Report(K9Detection.Trace(trace));
            rig.Brain.Tick(0.1f);
            rig.Source.Clear();

            Assert.AreEqual(trace, rig.Motor.LastDestination, "사전 조건: 목표가 갱신되었다");
            Assert.AreEqual(
                rig.Settings.InvestigateSpeed, rig.Motor.LastSpeed, 0.0001f, "조사 목표 갱신 이동");

            rig.Motor.Arrived = true;
            rig.Wander.Active = true;
            rig.Wander.Point = new Vector3(4f, 0f, 4f);
            rig.Brain.Tick(rig.Settings.InvestigateWanderIntervalSeconds);

            Assert.AreEqual(rig.Wander.Point, rig.Motor.LastDestination, "사전 조건: 배회 지점으로 이동했다");
            Assert.AreEqual(
                rig.Settings.InvestigateSpeed, rig.Motor.LastSpeed, 0.0001f, "조사 배회 이동");

            // 순찰 속도는 그대로다 — 조사 속도가 순찰 경로까지 번지면 안 된다.
            Rig patrolRig = new Rig();
            patrolRig.Brain.SetPatrolRoute(MakeRoute(2), true);
            patrolRig.Brain.Tick(0.1f);

            Assert.AreEqual(
                patrolRig.Settings.PatrolSpeed, patrolRig.Motor.LastSpeed, 0.0001f, "순찰 속도는 바뀌지 않는다");
        }

        [Test]
        [Description("TASK-006 기준7 음성 - 소집이 없으면 경계의 마지막 접촉 좌표에는 분산이 붙지 않는다")]
        public void Alert_WithoutSummon_DoesNotSpread_LastContactPoint()
        {
            Rig rig = new Rig();

            // 분산이 가능한 조건을 전부 켜 둔다 — 그런데도 붙으면 안 된다.
            rig.Wander.Active = true;
            rig.Wander.Point = new Vector3(-4.5f, 0f, 3.5f);
            Assert.Greater(rig.Settings.SummonSpreadRadius, 0f, "사전 조건: 분산 반경이 0보다 커야 한다");

            Vector3 contact = new Vector3(3f, 0f, -2f);
            rig.Source.Report(K9Detection.Contact(contact));
            rig.Brain.Tick(0.1f);
            Assert.AreEqual(K9State.Alert, rig.Brain.State, "사전 조건: Alert 진입");
            rig.Source.Clear();

            bool changed = rig.Brain.Tick(rig.Settings.HowlDurationSeconds);

            Assert.IsTrue(changed);
            Assert.AreEqual(K9State.Investigate, rig.Brain.State);
            Assert.AreEqual(contact, rig.Motor.LastDestination, "마지막 접촉 좌표 그대로여야 한다");
            Assert.AreEqual(
                0, rig.Wander.TryGetPointCount, "소집이 아닌 목적지에는 분산 제공자를 부르지 않는다");
        }

        // ── TASK-007: 시야 기억 · 추격 판정 창 · 하울링 쿨다운 · 수평 시야각 ──

        [TestCase(0f)]
        [TestCase(1f)]
        [TestCase(1000f)]
        [Description("TASK-007 기준1 - 한 번도 보지 못했으면 어떤 기억 시간에도 따뜻하지 않다")]
        public void SightMemory_NeverSeen_IsNeverWarm(float withinSeconds)
        {
            K9SightMemory memory = new K9SightMemory();

            Assert.IsFalse(memory.IsWarm(withinSeconds));
            Assert.IsFalse(memory.EverSeen, "본 적이 없어야 한다");
            Assert.AreEqual(Vector3.zero, memory.LastSeenPosition, "본 적이 없으면 좌표는 default 다");
        }

        [Test]
        [Description("TASK-007 기준1 - 본 직후에는 기억 시간 0 에서도 따뜻하고 좌표를 기억한다")]
        public void SightMemory_JustSeen_IsWarmAtZero_AndKeepsPosition()
        {
            K9SightMemory memory = new K9SightMemory();
            Vector3 seen = new Vector3(2f, 0f, 5f);

            memory.Tick(0.1f, true, seen);

            Assert.IsTrue(memory.IsWarm(0f), "이번 틱에 실제로 보였으면 기억 시간 0 에서도 따뜻하다");
            Assert.AreEqual(seen, memory.LastSeenPosition);
            Assert.AreEqual(0f, memory.SecondsSinceSeen, 0.0001f);
        }

        [Test]
        [Description("TASK-007 기준1 - 보이지 않는 동안 경과 시간이 쌓이고 기억 시간을 넘기면 식는다")]
        public void SightMemory_WhileUnseen_AccumulatesTime_AndCoolsDown()
        {
            K9SightMemory memory = new K9SightMemory();
            Vector3 seen = new Vector3(2f, 0f, 5f);
            memory.Tick(0.1f, true, seen);

            memory.Tick(1f, false, Vector3.zero);

            Assert.AreEqual(1f, memory.SecondsSinceSeen, 0.0001f);
            Assert.IsTrue(memory.IsWarm(1f), "경계값은 따뜻한 쪽이다");
            Assert.IsFalse(memory.IsWarm(0.5f), "기억 시간을 넘기면 식는다");
            Assert.AreEqual(seen, memory.LastSeenPosition, "식어도 마지막 좌표는 남는다");
        }

        [Test]
        [Description("TASK-007 기준1 - Forget 은 본 적 자체를 지운다")]
        public void SightMemory_Forget_ErasesEverything()
        {
            K9SightMemory memory = new K9SightMemory();
            memory.Tick(0.1f, true, new Vector3(2f, 0f, 5f));

            memory.Forget();

            Assert.IsFalse(memory.EverSeen);
            Assert.IsFalse(memory.IsWarm(1000f));
            Assert.AreEqual(Vector3.zero, memory.LastSeenPosition);
        }

        [Test]
        [Description("TASK-007 기준8 - 3D 각도로는 탈락하는 배치를 수평 부채꼴은 통과시킨다")]
        public void SightGeometry_HorizontalCone_AcceptsTarget_WhereThreeDimensionalAngleRejects()
        {
            // TASK-007 2.3 원인 C 의 검증 수치.
            // 눈 피치 13.816° 아래, 대상은 눈보다 2.5m 아래 · 수평 1.5m 앞 → 수평 각도는 0°.
            Vector3 forward = new Vector3(0f, -0.23875f, 0.97108f);
            Vector3 toTarget = new Vector3(0f, -2.5f, 1.5f);
            float halfAngleDegrees = 37.9f;

            Assert.Greater(
                Vector3.Angle(forward, toTarget), halfAngleDegrees,
                "사전 조건: 3D 각도 판정이라면 이 배치는 탈락한다");

            Assert.IsTrue(
                K9SightGeometry.IsWithinHorizontalCone(forward, toTarget, halfAngleDegrees),
                "높이 차는 각도에 반영되지 않아야 한다");
        }

        [Test]
        [Description("TASK-007 기준8 보조 - 수평으로 벗어난 대상은 부채꼴 밖이다")]
        public void SightGeometry_HorizontalCone_RejectsTarget_OutsideFan()
        {
            Vector3 forward = new Vector3(0f, -0.23875f, 0.97108f);

            // 수평 성분 (-3, 1.5) → 정면에서 63.4° 어긋난다.
            Vector3 toTarget = new Vector3(-3f, -2.5f, 1.5f);
            float halfAngleDegrees = 37.9f;

            Assert.IsFalse(K9SightGeometry.IsWithinHorizontalCone(forward, toTarget, halfAngleDegrees));
        }

        [Test]
        [Description("TASK-007 기준2 - 판정 직전에 시야를 잃어도 기억이 따뜻하면 마지막 목격 좌표로 추격한다")]
        public void Alert_ToChase_OnWarmSightMemory_WhenSightLostBeforeDecision()
        {
            Rig rig = new Rig();
            float memorySeconds = rig.Settings.SightMemorySeconds;
            Assert.Greater(memorySeconds, 0f, "사전 조건: 시야 기억 기본값이 0보다 커야 한다");

            Vector3 seen = new Vector3(2f, 0f, 5f);
            HowlThenLoseSightAtDecision(rig, seen, memorySeconds * 0.5f);

            Assert.AreEqual(K9State.Chase, rig.Brain.State, "기억이 따뜻하면 추격한다");
            Assert.AreEqual(seen, rig.Brain.CurrentTarget, "마지막 목격 좌표를 쫓아야 한다");
            Assert.AreEqual(seen, rig.Motor.LastDestination);
            Assert.AreEqual(rig.Settings.ChaseSpeed, rig.Motor.LastSpeed, 0.0001f);
        }

        [Test]
        [Description("TASK-007 기준3 - 기억 시간을 넘겨 시야를 잃었으면 조사로 간다")]
        public void Alert_ToInvestigate_WhenSightMemoryExpired()
        {
            Rig rig = new Rig();
            float memorySeconds = rig.Settings.SightMemorySeconds;

            HowlThenLoseSightAtDecision(rig, new Vector3(2f, 0f, 5f), memorySeconds * 2f);

            Assert.AreEqual(K9State.Investigate, rig.Brain.State, "기억이 식었으면 추격하지 않는다");
        }

        [Test]
        [Description("TASK-007 기준4 - 기억 시간이 0 이면 기준2 와 같은 절차에서도 조사로 간다(종전 동작)")]
        public void Alert_ToInvestigate_WhenSightMemoryDisabled()
        {
            Rig rig = new Rig();

            // 기준2 와 같은 간격을 쓰기 위해 기본값을 먼저 읽고 끈다.
            float memorySeconds = rig.Settings.SightMemorySeconds;
            SetSettingsField(rig.Settings, "_sightMemorySeconds", 0f);
            Assert.AreEqual(0f, rig.Settings.SightMemorySeconds, 0.0001f, "사전 조건: 기억이 꺼져 있다");

            HowlThenLoseSightAtDecision(rig, new Vector3(2f, 0f, 5f), memorySeconds * 0.5f);

            Assert.AreEqual(K9State.Investigate, rig.Brain.State, "기억 0 은 기존 동작과 같아야 한다");
        }

        [Test]
        [Description("TASK-007 기준5 - 쿨다운 안에 시야를 다시 잡으면 다시 짖지 않고 곧바로 추격한다")]
        public void Investigate_ToChase_WithoutRehowl_WhileHowlCooldownAlive()
        {
            Rig rig = new Rig();
            float sinceHowl = HowlThenDropToInvestigate(rig);
            Assert.Less(sinceHowl, rig.Settings.HowlCooldownSeconds, "사전 조건: 쿨다운이 아직 살아 있다");

            int broadcasts = rig.Alert.BroadcastCount;
            Vector3 seen = new Vector3(0f, 0f, 9f);
            rig.Source.Report(K9Detection.Sight(seen));

            bool changed = rig.Brain.Tick(0.1f);

            Assert.IsTrue(changed);
            Assert.AreEqual(K9State.Chase, rig.Brain.State, "Alert 을 거치지 않고 바로 추격한다");
            Assert.AreEqual(broadcasts, rig.Alert.BroadcastCount, "쿨다운 중에는 하울링이 늘지 않는다");
            Assert.AreEqual(seen, rig.Motor.LastDestination);
            Assert.AreEqual(rig.Settings.ChaseSpeed, rig.Motor.LastSpeed, 0.0001f);
        }

        [Test]
        [Description("TASK-007 기준6 - 쿨다운이 지난 뒤 시야를 잡으면 경계로 가고 하울링이 1회 더 나간다")]
        public void Investigate_ToAlert_AndRehowls_AfterHowlCooldownElapses()
        {
            Rig rig = new Rig();
            HowlThenDropToInvestigate(rig);

            float cooldown = rig.Settings.HowlCooldownSeconds;
            Assert.Less(
                cooldown, rig.Settings.InvestigateGiveUpSeconds,
                "사전 조건: 조사를 포기하기 전에 쿨다운이 끝나야 한다");

            rig.Brain.Tick(cooldown);
            Assert.AreEqual(K9State.Investigate, rig.Brain.State, "사전 조건: 아직 조사 중이다");

            int broadcasts = rig.Alert.BroadcastCount;
            rig.Motor.CurrentPosition = new Vector3(0f, 0f, 4f);
            rig.Source.Report(K9Detection.Sight(new Vector3(0f, 0f, 9f)));

            bool changed = rig.Brain.Tick(0.1f);

            Assert.IsTrue(changed);
            Assert.AreEqual(K9State.Alert, rig.Brain.State, "쿨다운이 끝났으면 하울링부터 다시 나간다");
            Assert.AreEqual(broadcasts + 1, rig.Alert.BroadcastCount);
            Assert.AreEqual(new Vector3(0f, 0f, 4f), rig.Alert.LastOrigin);
        }

        [Test]
        [Description("TASK-007 기준7 - 접촉은 쿨다운이 살아 있어도 언제나 경계로 가고 하울링이 1회 더 나간다")]
        public void Investigate_ToAlert_OnContact_EvenWhileHowlCooldownAlive()
        {
            Rig rig = new Rig();
            float sinceHowl = HowlThenDropToInvestigate(rig);
            Assert.Less(sinceHowl, rig.Settings.HowlCooldownSeconds, "사전 조건: 쿨다운이 아직 살아 있다");

            int broadcasts = rig.Alert.BroadcastCount;
            rig.Source.Report(K9Detection.Contact(new Vector3(0f, 0f, 9f)));

            bool changed = rig.Brain.Tick(0.1f);

            Assert.IsTrue(changed);
            Assert.AreEqual(K9State.Alert, rig.Brain.State, "접촉은 쿨다운과 무관하다");
            Assert.AreEqual(broadcasts + 1, rig.Alert.BroadcastCount);
        }

        // ── TASK-008: 경계 즉시 추격 · 상태별 이동 프로파일 ──────────────

        [Test]
        [Description("TASK-008 기준1·2 - 추격 진입은 목적지 제동을 끄고 설정된 가속도·회전 속도를 그대로 넘긴다")]
        public void Task008_EnterChase_AppliesMotionProfile_WithoutBrakingNearDestination()
        {
            Rig rig = new Rig();

            // 기본값(가속도 30 · 회전 540)과 비교하면 SetMotionProfile 에 상수를 박은 구현도 통과한다.
            // 비기본값을 주입해 "설정을 그대로 넘겼는가"를 실제로 묻는다(TASK-009 5.7 기준4).
            SetSettingsField(rig.Settings, "_acceleration", 17.25f);
            SetSettingsField(rig.Settings, "_moveTurnSpeedDegrees", 213.5f);
            Assert.AreNotEqual(30f, rig.Settings.Acceleration, "사전 조건: 가속도가 기본값이 아니다");
            Assert.AreNotEqual(
                540f, rig.Settings.MoveTurnSpeedDegrees, "사전 조건: 회전 속도가 기본값이 아니다");

            EnterChase(rig, new Vector3(6f, 0f, 6f));

            Assert.AreEqual(1, rig.Motor.SetMotionProfileCount, "추격 진입에서 1회 나가야 한다");
            Assert.AreEqual(rig.Settings.Acceleration, rig.Motor.LastAcceleration, 0.0001f);
            Assert.AreEqual(rig.Settings.MoveTurnSpeedDegrees, rig.Motor.LastTurnSpeedDegrees, 0.0001f);
            Assert.IsFalse(
                rig.Motor.LastBrakeNearDestination,
                "추격의 목적지는 대상 본인이라 제동을 켜면 내내 브레이크가 걸린다");
        }

        [Test]
        [Description("TASK-008 기준1·2 - 조사 진입은 목적지 제동을 켜고 설정된 가속도·회전 속도를 그대로 넘긴다")]
        public void Task008_EnterInvestigate_AppliesMotionProfile_WithBrakingNearDestination()
        {
            Rig rig = new Rig();

            // 기본값(가속도 30 · 회전 540)과 비교하면 SetMotionProfile 에 상수를 박은 구현도 통과한다.
            // 비기본값을 주입해 "설정을 그대로 넘겼는가"를 실제로 묻는다(TASK-009 5.7 기준4).
            SetSettingsField(rig.Settings, "_acceleration", 17.25f);
            SetSettingsField(rig.Settings, "_moveTurnSpeedDegrees", 213.5f);
            Assert.AreNotEqual(30f, rig.Settings.Acceleration, "사전 조건: 가속도가 기본값이 아니다");
            Assert.AreNotEqual(
                540f, rig.Settings.MoveTurnSpeedDegrees, "사전 조건: 회전 속도가 기본값이 아니다");

            EnterInvestigate(rig);

            Assert.AreEqual(1, rig.Motor.SetMotionProfileCount, "조사 진입에서 1회 나가야 한다");
            Assert.AreEqual(rig.Settings.Acceleration, rig.Motor.LastAcceleration, 0.0001f);
            Assert.AreEqual(rig.Settings.MoveTurnSpeedDegrees, rig.Motor.LastTurnSpeedDegrees, 0.0001f);
            Assert.IsTrue(rig.Motor.LastBrakeNearDestination, "조사 지점에는 곱게 서야 한다");
        }

        [Test]
        [Description("TASK-008 기준1·2 - 순찰 복귀는 목적지 제동을 켜고 설정된 가속도·회전 속도를 그대로 넘긴다")]
        public void Task008_EnterPatrol_AppliesMotionProfile_WithBrakingNearDestination()
        {
            Rig rig = new Rig();

            // 기본값(가속도 30 · 회전 540)과 비교하면 SetMotionProfile 에 상수를 박은 구현도 통과한다.
            // 비기본값을 주입해 "설정을 그대로 넘겼는가"를 실제로 묻는다(TASK-009 5.7 기준4).
            SetSettingsField(rig.Settings, "_acceleration", 17.25f);
            SetSettingsField(rig.Settings, "_moveTurnSpeedDegrees", 213.5f);
            Assert.AreNotEqual(30f, rig.Settings.Acceleration, "사전 조건: 가속도가 기본값이 아니다");
            Assert.AreNotEqual(
                540f, rig.Settings.MoveTurnSpeedDegrees, "사전 조건: 회전 속도가 기본값이 아니다");

            EnterInvestigate(rig);

            int beforeReturn = rig.Motor.SetMotionProfileCount;

            // 무감지 20초 → 전환4 → Patrol
            rig.Brain.Tick(rig.Settings.InvestigateGiveUpSeconds);
            Assert.AreEqual(K9State.Patrol, rig.Brain.State, "사전 조건: 순찰 복귀");

            Assert.AreEqual(beforeReturn + 1, rig.Motor.SetMotionProfileCount, "순찰 진입에서 1회 더 나가야 한다");
            Assert.AreEqual(rig.Settings.Acceleration, rig.Motor.LastAcceleration, 0.0001f);
            Assert.AreEqual(rig.Settings.MoveTurnSpeedDegrees, rig.Motor.LastTurnSpeedDegrees, 0.0001f);
            Assert.IsTrue(rig.Motor.LastBrakeNearDestination, "웨이포인트에는 곱게 서야 한다");
        }

        [Test]
        [Description("TASK-008 기준3 - 유지 시간 0 이면 하울링 시간이 훨씬 길어도 경계 다음 틱에 추격한다")]
        public void Task008_Alert_ToChase_OnNextTick_EvenWhenHowlDurationIsMuchLonger()
        {
            Rig rig = new Rig();

            // 씬 실측값처럼 하울링을 길게 잡아 둔다. 종전 규칙이라면 이 시간 내내 제자리였다(TASK-008 2.4).
            SetSettingsField(rig.Settings, "_howlDurationSeconds", rig.Settings.HowlDurationSeconds * 5f);
            Assert.AreEqual(0f, rig.Settings.AlertHoldSeconds, 0.0001f, "사전 조건: 유지 시간 기본값 0");

            Vector3 seen = new Vector3(2f, 0f, 5f);
            rig.Source.Report(K9Detection.Sight(seen));

            bool entered = rig.Brain.Tick(0.1f);
            Assert.IsTrue(entered);
            Assert.AreEqual(K9State.Alert, rig.Brain.State, "사전 조건: 시야 포착은 언제나 Alert 를 거친다");
            Assert.AreEqual(1, rig.Alert.BroadcastCount, "진입 시 하울링 1회");

            bool changed = rig.Brain.Tick(0.1f);

            Assert.IsTrue(changed);
            Assert.AreEqual(K9State.Chase, rig.Brain.State, "하울링이 남아 있어도 시야가 있으면 즉시 추격이다");
            Assert.AreEqual(seen, rig.Motor.LastDestination);
            Assert.AreEqual(rig.Settings.ChaseSpeed, rig.Motor.LastSpeed, 0.0001f);
            Assert.AreEqual(1, rig.Alert.BroadcastCount, "추격으로 넘어가며 다시 짖지 않는다");
        }

        [Test]
        [Description("TASK-008 기준5 - 시야가 없으면 유지 시간이 짧아도 하울링 시간을 다 채운 뒤 조사로 간다")]
        public void Task008_Alert_ToInvestigate_AfterHowlDuration_EvenWhenAlertHoldIsShorter()
        {
            Rig rig = new Rig();

            float hold = rig.Settings.HowlDurationSeconds * 0.25f;
            SetSettingsField(rig.Settings, "_alertHoldSeconds", hold);
            Assert.Less(
                rig.Settings.AlertHoldSeconds, rig.Settings.HowlDurationSeconds,
                "사전 조건: 유지 시간이 하울링 시간보다 짧다");

            EnterAlert(rig);
            rig.Source.Clear();

            bool early = rig.Brain.Tick(rig.Settings.AlertHoldSeconds);

            Assert.IsFalse(early, "유지 시간이 지나도 조사 전환이 앞당겨지면 안 된다");
            Assert.AreEqual(K9State.Alert, rig.Brain.State);

            bool changed = rig.Brain.Tick(
                rig.Settings.HowlDurationSeconds - rig.Settings.AlertHoldSeconds);

            Assert.IsTrue(changed);
            Assert.AreEqual(K9State.Investigate, rig.Brain.State, "하울링 시간을 다 채운 뒤에 조사로 간다");
        }

        [Test]
        [Description("TASK-008 기준6 - 시야는 없지만 기억이 따뜻하면 유지 시간 경과 시점에 마지막 목격 좌표로 추격한다")]
        public void Task008_Alert_ToChase_OnWarmSightMemory_WhenAlertHoldElapses()
        {
            Rig rig = new Rig();

            float memorySeconds = rig.Settings.SightMemorySeconds;
            Assert.Greater(memorySeconds, 0f, "사전 조건: 시야 기억 기본값이 0보다 커야 한다");

            float hold = rig.Settings.HowlDurationSeconds * 2f;
            SetSettingsField(rig.Settings, "_alertHoldSeconds", hold);

            Vector3 seen = new Vector3(2f, 0f, 5f);
            rig.Source.Report(K9Detection.Sight(seen));
            rig.Brain.Tick(0.1f);
            Assert.AreEqual(K9State.Alert, rig.Brain.State, "사전 조건: 시야로 Alert 진입");

            // 유지 시간이 끝나기 직전까지 시야를 유지한다.
            rig.Brain.Tick(hold - memorySeconds * 0.5f);
            Assert.AreEqual(K9State.Alert, rig.Brain.State, "사전 조건: 아직 유지 시간 안");

            // 여기서 시야가 끊긴다. 다음 틱이 유지 시간 경과 시점이다.
            rig.Source.Clear();
            bool changed = rig.Brain.Tick(memorySeconds * 0.5f);

            Assert.IsTrue(changed);
            Assert.AreEqual(K9State.Chase, rig.Brain.State, "기억이 따뜻하면 추격한다(TASK-007 규칙 유지)");
            Assert.AreEqual(seen, rig.Motor.LastDestination, "마지막 목격 좌표를 쫓아야 한다");
        }

        [Test]
        [Description("TASK-008 기준7 - 경계는 이동하지 않으므로 진입에서 이동 프로파일을 지정하지 않는다")]
        public void Task008_EnterAlert_DoesNotApplyMotionProfile()
        {
            Rig rig = new Rig();
            Assert.AreEqual(0, rig.Motor.SetMotionProfileCount, "사전 조건: 아직 한 번도 지정하지 않았다");

            EnterAlert(rig);

            Assert.AreEqual(
                0, rig.Motor.SetMotionProfileCount,
                "경계 진입은 이동 명령이 없으므로 프로파일도 건드리지 않는다");
        }

        [Test]
        [Description("TASK-008 기준8 - 같은 상태에 머무는 동안 이동 프로파일이 매 틱 반복 지정되지 않는다")]
        public void Task008_MotionProfile_IsAppliedOncePerStateEntry_NotEveryTick()
        {
            Rig rig = new Rig();

            Vector3 seen = new Vector3(6f, 0f, 6f);
            EnterChase(rig, seen);
            Assert.AreEqual(1, rig.Motor.SetMotionProfileCount, "사전 조건: 추격 진입에서 1회");

            // 시야를 유지한 채 여러 틱 추격한다 — 매 틱 MoveTo 는 나가지만 프로파일은 그대로여야 한다.
            for (int i = 0; i < 5; i++)
            {
                rig.Source.Report(K9Detection.Sight(seen));
                rig.Brain.Tick(0.1f);
            }

            Assert.AreEqual(K9State.Chase, rig.Brain.State, "사전 조건: 추격을 유지했다");
            Assert.Greater(rig.Motor.MoveToCount, 1, "사전 조건: 이동 명령은 매 틱 나갔다");
            Assert.AreEqual(1, rig.Motor.SetMotionProfileCount, "상태 진입당 정확히 1회다");
        }

        // ── TASK-009: 상태 체류 중의 규칙 ────────────────────────────

        [Test]
        [Description("TASK-009 기준5 - 조사에 머무는 동안 배회는 기준점 주변에서만 일어나고 포기 시간은 그대로 온다")]
        public void Task009_Investigate_WhileDwelling_WandersAroundAnchor_AndStillGivesUpOnTime()
        {
            Rig rig = new Rig();
            rig.Wander.Active = true;
            rig.Wander.Point = new Vector3(1f, 0f, 11f);

            Vector3 trace = new Vector3(0f, 0f, 10f);
            rig.Source.Report(K9Detection.Trace(trace));
            rig.Brain.Tick(0.1f);
            rig.Source.Clear();
            Assert.AreEqual(K9State.Investigate, rig.Brain.State, "사전 조건: 조사 진입");

            // 진입 틱은 무감지 타이머를 세지 않는다. 여기서부터 흐른 시간만 센다.
            rig.Motor.Arrived = true;
            int beforePicks = rig.Wander.TryGetPointCount;
            float interval = rig.Settings.InvestigateWanderIntervalSeconds;
            float elapsed = 0f;

            for (int i = 0; i < 5; i++)
            {
                rig.Brain.Tick(interval);
                elapsed += interval;

                Assert.AreEqual(K9State.Investigate, rig.Brain.State, "아직 포기 시간 전이다");
                Assert.AreEqual(trace, rig.Wander.LastAnchor, "배회 기준점은 조사 지점에 머문다");
                Assert.AreEqual(
                    rig.Settings.InvestigateWanderRadius, rig.Wander.LastRadius, 0.0001f,
                    "배회 반경은 설정값 그대로다");
            }

            Assert.Greater(rig.Wander.TryGetPointCount, beforePicks, "체류하는 동안 실제로 배회했다");
            Assert.AreEqual(rig.Wander.Point, rig.Motor.LastDestination, "배회 지점으로 이동했다");
            Assert.AreEqual(
                rig.Settings.InvestigateSpeed, rig.Motor.LastSpeed, 0.0001f, "배회도 조사 속도로 움직인다");

            // 배회는 무감지 타이머를 되돌리지 않는다 — 남은 시간을 채우면 그대로 순찰로 복귀한다.
            rig.Brain.Tick(rig.Settings.InvestigateGiveUpSeconds - elapsed);
            Assert.AreEqual(K9State.Patrol, rig.Brain.State, "배회했다고 조사에 갇히지 않는다");
        }

        [Test]
        [Description("TASK-009 기준5 - 순찰에 머무는 동안 도착 전과 대기 중에는 다음 지점으로 넘어가지 않는다")]
        public void Task009_Patrol_WhileDwelling_HoldsWaypoint_UntilArrivalAndWaitElapse()
        {
            Rig rig = new Rig();
            List<Vector3> route = MakeRoute(3);
            rig.Brain.SetPatrolRoute(route, true);

            rig.Brain.Tick(0.1f);
            Assert.AreEqual(1, rig.Motor.MoveToCount, "사전 조건: 첫 지점으로 출발했다");
            Assert.AreEqual(route[0], rig.Motor.LastDestination);

            // 아직 도착하지 않았다 — 여러 틱을 흘려도 명령이 반복되거나 번호가 넘어가지 않는다.
            float wait = rig.Settings.WaypointWaitSeconds;
            for (int i = 0; i < 5; i++)
            {
                rig.Brain.Tick(wait);
            }

            Assert.AreEqual(1, rig.Motor.MoveToCount, "도착 전에는 다음 지점으로 넘어가지 않는다");
            Assert.AreEqual(0, rig.Motor.StopCount, "가는 중에 정지 명령이 나가면 안 된다");
            Assert.AreEqual(0, rig.Brain.WaypointIndex);

            // 도착했지만 대기 시간을 못 채운 동안에도 그대로다.
            rig.Motor.Arrived = true;
            rig.Brain.Tick(wait * 0.25f);
            rig.Brain.Tick(wait * 0.25f);
            Assert.AreEqual(1, rig.Motor.MoveToCount, "대기 시간을 못 채우면 넘어가지 않는다");
            Assert.AreEqual(0, rig.Brain.WaypointIndex);

            // 남은 대기 시간을 채운 틱에서 다음 지점으로 넘어간다.
            rig.Brain.Tick(wait * 0.5f);
            Assert.AreEqual(2, rig.Motor.MoveToCount);
            Assert.AreEqual(1, rig.Brain.WaypointIndex);
            Assert.AreEqual(route[1], rig.Motor.LastDestination);
            Assert.AreEqual(rig.Settings.PatrolSpeed, rig.Motor.LastSpeed, 0.0001f);
            Assert.AreEqual(K9State.Patrol, rig.Brain.State);
        }

        // ── 조립 도우미 ─────────────────────────────────────────────

        /// <summary>
        /// 인스펙터 전용 수치를 테스트에서만 바꾼다.
        /// <para>
        /// <see cref="K9Settings"/> 는 직렬화 대상이라 프로덕션 코드의 접근 범위를 넓히지 않는다 —
        /// 그 대신 테스트 안에서만 리플렉션으로 값을 넣는다.
        /// 필드명이 바뀌면 조용히 넘어가지 않고 큰 소리로 깨지게 둔다.
        /// </para>
        /// </summary>
        private static void SetSettingsField(K9Settings settings, string fieldName, float value)
        {
            FieldInfo field = typeof(K9Settings).GetField(
                fieldName, BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.IsNotNull(field, "K9Settings 에서 필드를 찾지 못했다: " + fieldName);

            field.SetValue(settings, value);
        }

        private static List<Vector3> MakeRoute(int count)
        {
            List<Vector3> route = new List<Vector3>(count);
            for (int i = 0; i < count; i++)
            {
                route.Add(new Vector3(i * 10f, 0f, 0f));
            }

            return route;
        }

        private static void EnterInvestigate(Rig rig)
        {
            rig.Source.Report(K9Detection.Trace(new Vector3(0f, 0f, 10f)));
            rig.Brain.Tick(0.1f);
            rig.Source.Clear();
            Assert.AreEqual(K9State.Investigate, rig.Brain.State, "사전 조건: Investigate 진입");
        }

        private static void EnterAlert(Rig rig)
        {
            rig.Source.Report(K9Detection.Contact(new Vector3(1f, 0f, 0f)));
            rig.Brain.Tick(0.1f);
            Assert.AreEqual(K9State.Alert, rig.Brain.State, "사전 조건: Alert 진입");
        }

        /// <summary>
        /// 시야로 Alert 에 진입한 뒤, 하울링 도중 시야를 잃고
        /// <b>마지막 목격에서 <paramref name="gapSeconds"/> 가 지난 순간이 곧 추격 판정 시점</b>이 되도록
        /// 시간을 맞춰 흘린다. 마지막 Tick 이 판정 틱이다.
        /// </summary>
        private static void HowlThenLoseSightAtDecision(Rig rig, Vector3 seen, float gapSeconds)
        {
            Assert.Greater(gapSeconds, 0f, "사전 조건: 시야를 잃고 흐를 시간이 있어야 한다");

            // 판정 시점 전에 시야를 잃을 여유를 만든다. 하울링이 간격보다 짧으면 잃을 틈이 없다.
            float howlSeconds = gapSeconds * 4f;
            SetSettingsField(rig.Settings, "_howlDurationSeconds", howlSeconds);

            // TASK-008 4.2 처방 — 시야 유지 구간에 들어가기 전에 경계 유지 시간을 하울링 시간과 같게 올린다.
            // 새 규칙에서는 시야가 있으면 유지 시간이 지나는 즉시 추격하므로,
            // 유지 시간을 올려 두지 않으면 아래 "판정 시점 직전" 구간에서 이미 Chase 로 나가 버린다.
            // 유지 구간은 _alertTimer(howl - gap) < hold(howl) 로 막히고,
            // 시야를 끊은 뒤의 틱은 _alertTimer(howl) >= hold(howl) 이라 그대로 판정 시점이 된다.
            SetSettingsField(rig.Settings, "_alertHoldSeconds", howlSeconds);

            rig.Source.Report(K9Detection.Sight(seen));
            rig.Brain.Tick(0.1f);
            Assert.AreEqual(K9State.Alert, rig.Brain.State, "사전 조건: 시야로 Alert 진입");

            // 시야를 유지한 채 판정 시점 직전까지 간다.
            rig.Brain.Tick(howlSeconds - gapSeconds);
            Assert.AreEqual(K9State.Alert, rig.Brain.State, "사전 조건: 아직 판정 시점 전");

            // 여기서 시야가 끊긴다. 다음 틱이 판정 시점이다.
            rig.Source.Clear();
            rig.Brain.Tick(gapSeconds);
        }

        /// <summary>
        /// 하울링을 한 번 내고, 판정 시점에 시야가 없어 조사로 내려간다.
        /// <para>
        /// 접촉으로 경계에 든다 — 시야를 한 번도 보고하지 않으므로 시야 기억이 데워지지 않는다.
        /// 그래서 결과는 하울링 쿨다운만으로 갈린다.
        /// </para>
        /// <returns>하울링 이후 흐른 시간(초).</returns>
        /// </summary>
        private static float HowlThenDropToInvestigate(Rig rig)
        {
            rig.Source.Report(K9Detection.Contact(new Vector3(1f, 0f, 0f)));
            rig.Brain.Tick(0.1f);
            Assert.AreEqual(K9State.Alert, rig.Brain.State, "사전 조건: Alert 진입");
            Assert.AreEqual(1, rig.Alert.BroadcastCount, "사전 조건: 하울링 1회");

            rig.Source.Clear();
            float elapsed = rig.Settings.HowlDurationSeconds;
            rig.Brain.Tick(elapsed);
            Assert.AreEqual(
                K9State.Investigate, rig.Brain.State,
                "사전 조건: 판정 시점에 시야도 기억도 없어 조사로 내려간다");

            return elapsed;
        }

        private static void EnterChase(Rig rig, Vector3 seen)
        {
            EnterAlert(rig);
            rig.Source.Report(K9Detection.Sight(seen));

            // 경계 유지 시간(기본 0)이 지나면 시야가 있는 즉시 Chase 다(TASK-008 2.4).
            // 시간은 숫자로 쓰지 않고 설정값에서 읽는다.
            rig.Brain.Tick(rig.Settings.HowlDurationSeconds);
            Assert.AreEqual(K9State.Chase, rig.Brain.State, "사전 조건: Chase 진입");
        }

        private sealed class Rig
        {
            public readonly K9Settings Settings = new K9Settings();
            public readonly FakeMotor Motor = new FakeMotor();
            public readonly SpyAlertChannel Alert = new SpyAlertChannel();
            public readonly FakePerceptionSource Source = new FakePerceptionSource();
            public readonly FakeTargetTracker Tracker = new FakeTargetTracker();
            public readonly FakeWanderPointProvider Wander = new FakeWanderPointProvider();
            public readonly List<IK9PerceptionSource> Sources = new List<IK9PerceptionSource>(4);
            public readonly K9Brain Brain;

            public Rig()
            {
                Sources.Add(Source);

                // 트래커와 배회 지점 제공자는 기본 비활성이다 —
                // TryTrack / TryGetPoint 가 false 를 돌려주므로 기존 테스트의 동작은 그대로다.
                Brain = new K9Brain(Settings, Motor, Sources, Alert, Tracker, Wander);
            }
        }
    }

    /// <summary>이동 명령을 기록하는 가짜 모터.</summary>
    internal sealed class FakeMotor : IK9Motor
    {
        public readonly List<Vector3> Destinations = new List<Vector3>(64);
        public readonly List<float> Speeds = new List<float>(64);
        public readonly List<Vector3> FaceTargets = new List<Vector3>(64);

        public bool RecordCalls = true;
        public int MoveToCount;
        public int StopCount;
        public Vector3 LastDestination;
        public float LastSpeed;
        public bool Arrived;
        public Vector3 CurrentPosition;

        // 방향 전환 기록. 회전 결과가 아니라 "언제·무엇을 향해·어떤 속도로" 요청했는지만 본다.
        public int FaceCount;
        public Vector3 LastFaceTarget;
        public float LastFaceDegreesPerSecond;
        public float LastFaceDeltaTime;

        // 이동 성격 기록(TASK-008). 상태 진입당 정확히 1회 나가는지도 횟수로 본다.
        public int SetMotionProfileCount;
        public float LastAcceleration;
        public float LastTurnSpeedDegrees;
        public bool LastBrakeNearDestination;

        public void MoveTo(Vector3 destination, float speed)
        {
            MoveToCount++;
            LastDestination = destination;
            LastSpeed = speed;

            if (RecordCalls)
            {
                Destinations.Add(destination);
                Speeds.Add(speed);
            }
        }

        public void Stop()
        {
            StopCount++;
        }

        public void SetMotionProfile(float acceleration, float turnSpeedDegrees, bool brakeNearDestination)
        {
            SetMotionProfileCount++;
            LastAcceleration = acceleration;
            LastTurnSpeedDegrees = turnSpeedDegrees;
            LastBrakeNearDestination = brakeNearDestination;
        }

        public void Face(Vector3 target, float degreesPerSecond, float deltaTime)
        {
            FaceCount++;
            LastFaceTarget = target;
            LastFaceDegreesPerSecond = degreesPerSecond;
            LastFaceDeltaTime = deltaTime;

            if (RecordCalls)
            {
                FaceTargets.Add(target);
            }
        }

        public bool HasArrived
        {
            get { return Arrived; }
        }

        public Vector3 Position
        {
            get { return CurrentPosition; }
        }
    }

    /// <summary>원하는 단서를 그대로 내보내는 가짜 감지 소스.</summary>
    internal sealed class FakePerceptionSource : IK9PerceptionSource
    {
        public int TryDetectCount;

        private bool _active;
        private K9Detection _detection;

        public void Report(K9Detection detection)
        {
            _detection = detection;
            _active = true;
        }

        public void Clear()
        {
            _active = false;
        }

        public bool TryDetect(out K9Detection detection)
        {
            TryDetectCount++;
            detection = _detection;
            return _active;
        }
    }

    /// <summary>
    /// 기준5(OCP) 전용. 나중에 붙을 털공 흔적 추적 소스를 흉내 낸 <b>새 구현체</b>다.
    /// 이 타입이 생겨도 K9Brain 과 기존 소스는 한 줄도 바뀌지 않는다.
    /// </summary>
    internal sealed class FurballTraceStubSource : IK9PerceptionSource
    {
        public Vector3 TracePosition;
        public bool Active;
        public int TryDetectCount;

        public bool TryDetect(out K9Detection detection)
        {
            TryDetectCount++;
            detection = K9Detection.Trace(TracePosition);
            return Active;
        }
    }

    /// <summary>
    /// 시야 밖 추적 좌표 통로의 가짜 구현.
    /// 기본은 비활성이라 <c>TryTrack</c> 이 false 를 돌려준다.
    /// </summary>
    internal sealed class FakeTargetTracker : IK9TargetTracker
    {
        public bool Active;
        public Vector3 TrackedPosition;
        public int TryTrackCount;
        public Vector3 LastKnownPosition;

        public bool TryTrack(Vector3 lastKnownPosition, out Vector3 currentPosition)
        {
            TryTrackCount++;
            LastKnownPosition = lastKnownPosition;
            currentPosition = TrackedPosition;
            return Active;
        }
    }

    /// <summary>
    /// 배회 지점 제공자의 가짜 구현.
    /// 기본은 비활성이라 <c>TryGetPoint</c> 가 false 를 돌려준다.
    /// </summary>
    internal sealed class FakeWanderPointProvider : IK9WanderPointProvider
    {
        public bool Active;
        public Vector3 Point;
        public int TryGetPointCount;
        public Vector3 LastAnchor;
        public float LastRadius;

        public bool TryGetPoint(Vector3 anchor, float radius, out Vector3 point)
        {
            TryGetPointCount++;
            LastAnchor = anchor;
            LastRadius = radius;
            point = Point;
            return Active;
        }
    }

    /// <summary>하울링 호출 횟수와 인자를 기록하는 스파이 채널.</summary>
    internal sealed class SpyAlertChannel : IK9AlertChannel
    {
        public int BroadcastCount;
        public Vector3 LastOrigin;
        public float LastRadius;

        public bool HasSummon;
        public Vector3 SummonTarget;

        public void Broadcast(Vector3 origin, float radius)
        {
            BroadcastCount++;
            LastOrigin = origin;
            LastRadius = radius;
        }

        public bool TryConsumeSummon(out Vector3 target)
        {
            if (HasSummon)
            {
                target = SummonTarget;
                HasSummon = false;
                return true;
            }

            target = Vector3.zero;
            return false;
        }
    }
}
