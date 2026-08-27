using System.Collections.Generic;
using NUnit.Framework;
using Pawntom.Enemy.Core;
using UnityEngine;

namespace Pawntom.Enemy.Tests
{
    /// <summary>
    /// TASK-009 5.7 기준6 — 두뇌에서 뽑아낸 세 부품의 <b>단독</b> 검증.
    /// <para>
    /// 여기서는 <see cref="K9Brain"/> 을 한 번도 만들지 않는다.
    /// 두뇌를 거쳐야만 검증되는 부품이라면 분해한 의미가 없다.
    /// </para>
    /// </summary>
    public sealed class K9CursorTests
    {
        // ── K9PatrolCursor ─────────────────────────────────────────

        [Test]
        [Description("TASK-009 5.3-1 - SetRoute 는 번호를 0 으로 되돌리고 Reset 은 가던 번호를 유지한다")]
        public void PatrolCursor_SetRoute_RewindsIndex_ButReset_KeepsIt()
        {
            K9PatrolCursor cursor = new K9PatrolCursor();
            List<Vector3> route = MakeRoute(3);
            cursor.SetRoute(route, true);

            Vector3 destination;

            // 0번으로 출발한 뒤 1번까지 전진시킨다.
            Assert.AreEqual(K9PatrolAction.MoveTo, cursor.Advance(1f, false, 1f, out destination));
            Assert.AreEqual(route[0], destination);
            Assert.AreEqual(K9PatrolAction.MoveTo, cursor.Advance(1f, true, 1f, out destination));
            Assert.AreEqual(route[1], destination);
            Assert.AreEqual(1, cursor.WaypointIndex, "사전 조건: 1번 지점을 향하고 있다");

            // 순찰 재진입 — 번호는 유지된다. 매번 앞머리로 돌아가면 경로 뒤쪽을 영영 못 밟는다.
            cursor.Reset();
            Assert.AreEqual(1, cursor.WaypointIndex, "Reset 은 번호를 되돌리지 않는다");
            Assert.AreEqual(K9PatrolAction.MoveTo, cursor.Advance(1f, false, 1f, out destination));
            Assert.AreEqual(route[1], destination, "복귀한 개는 가던 지점부터 이어 간다");

            // 경로 교체 — 이때는 처음부터 돈다.
            cursor.SetRoute(route, true);
            Assert.AreEqual(0, cursor.WaypointIndex, "SetRoute 는 번호를 0 으로 되돌린다");
            Assert.AreEqual(K9PatrolAction.MoveTo, cursor.Advance(1f, false, 1f, out destination));
            Assert.AreEqual(route[0], destination);
        }

        [Test]
        [Description("TASK-009 5.3-2 - 한 번의 Advance 에서 나가는 행동은 최대 하나다")]
        public void PatrolCursor_Advance_YieldsAtMostOneAction_PerCall()
        {
            K9PatrolCursor cursor = new K9PatrolCursor();
            List<Vector3> route = MakeRoute(3);
            cursor.SetRoute(route, true);

            Vector3 destination;

            Assert.AreEqual(K9PatrolAction.MoveTo, cursor.Advance(0.5f, true, 2f, out destination));
            Assert.AreEqual(route[0], destination, "첫 호출은 이동 명령 하나뿐이다");

            // 도착했지만 대기 시간을 못 채운 틱에서는 아무 것도 하지 않는다.
            Assert.AreEqual(K9PatrolAction.None, cursor.Advance(0.5f, true, 2f, out destination));
            Assert.AreEqual(K9PatrolAction.None, cursor.Advance(0.5f, true, 2f, out destination));
            Assert.AreEqual(0, cursor.WaypointIndex, "대기 중에는 번호가 넘어가지 않는다");

            // 대기 시간을 채운 틱에서만 다음 지점으로 넘어간다.
            Assert.AreEqual(K9PatrolAction.MoveTo, cursor.Advance(1f, true, 2f, out destination));
            Assert.AreEqual(route[1], destination);
            Assert.AreEqual(1, cursor.WaypointIndex);

            // 아직 가는 중이면 대기 시간을 세지 않는다.
            Assert.AreEqual(K9PatrolAction.None, cursor.Advance(5f, false, 2f, out destination));
            Assert.AreEqual(K9PatrolAction.None, cursor.Advance(1f, true, 2f, out destination),
                "이동 중에 흐른 시간은 대기 시간으로 쳐 주지 않는다");
        }

        [Test]
        [Description("TASK-009 - 웨이포인트가 0개면 정지는 한 번만 나가고 그 뒤로는 조용하다")]
        public void PatrolCursor_WithZeroWaypoints_StopsOnce_ThenStaysSilent()
        {
            K9PatrolCursor cursor = new K9PatrolCursor();
            cursor.SetRoute(new List<Vector3>(), true);

            Vector3 destination;

            Assert.AreEqual(K9PatrolAction.Stop, cursor.Advance(1f, false, 1f, out destination));

            for (int i = 0; i < 5; i++)
            {
                Assert.AreEqual(
                    K9PatrolAction.None, cursor.Advance(1f, false, 1f, out destination),
                    "이미 선 개에게 정지 명령을 반복해서 내지 않는다");
            }
        }

        [Test]
        [Description("TASK-009 - loop 가 false 면 마지막 지점에서 순회를 끝내고 한 번만 정지한다")]
        public void PatrolCursor_WithoutLoop_FinishesRoute_AndStopsOnce()
        {
            K9PatrolCursor cursor = new K9PatrolCursor();
            List<Vector3> route = MakeRoute(3);
            cursor.SetRoute(route, false);

            Vector3 destination;

            Assert.AreEqual(K9PatrolAction.MoveTo, cursor.Advance(1f, true, 1f, out destination));
            Assert.AreEqual(route[0], destination);
            Assert.AreEqual(K9PatrolAction.MoveTo, cursor.Advance(1f, true, 1f, out destination));
            Assert.AreEqual(route[1], destination);
            Assert.AreEqual(K9PatrolAction.MoveTo, cursor.Advance(1f, true, 1f, out destination));
            Assert.AreEqual(route[2], destination);

            Assert.AreEqual(K9PatrolAction.Stop, cursor.Advance(1f, true, 1f, out destination),
                "마지막 지점을 지나면 처음으로 돌아가지 않고 선다");

            for (int i = 0; i < 3; i++)
            {
                Assert.AreEqual(K9PatrolAction.None, cursor.Advance(1f, true, 1f, out destination));
            }
        }

        [Test]
        [Description("TASK-009 5.3-3 - 두뇌가 직접 세운 뒤에는 커서가 정지 명령을 다시 내지 않는다")]
        public void PatrolCursor_NotifyStopped_SuppressesRedundantStop()
        {
            K9PatrolCursor cursor = new K9PatrolCursor();
            cursor.SetRoute(new List<Vector3>(), true);

            Vector3 destination;

            // 경계 진입처럼 두뇌가 커서 밖에서 세운 상황.
            cursor.NotifyStopped();
            Assert.AreEqual(K9PatrolAction.None, cursor.Advance(1f, false, 1f, out destination),
                "이미 서 있으므로 정지 명령이 또 나가면 안 된다");

            // 이동 명령이 한 번 나가면 정지 래치가 다시 열린다.
            cursor.SetRoute(MakeRoute(1), false);
            Assert.AreEqual(K9PatrolAction.MoveTo, cursor.Advance(1f, false, 1f, out destination));

            cursor.SetRoute(new List<Vector3>(), false);
            Assert.AreEqual(K9PatrolAction.Stop, cursor.Advance(1f, false, 1f, out destination),
                "움직인 뒤에는 다시 한 번 설 수 있어야 한다");
        }

        // ── K9WanderCursor ─────────────────────────────────────────

        [Test]
        [Description("TASK-009 - 배회 지점은 도착한 뒤 대기 시간을 채웠을 때만 고른다")]
        public void WanderCursor_TryAdvance_RequiresArrival_AndInterval()
        {
            FakeWanderPointProvider provider = new FakeWanderPointProvider();
            provider.Active = true;
            provider.Point = new Vector3(3f, 0f, 4f);

            K9WanderCursor cursor = new K9WanderCursor(provider);
            Vector3 anchor = new Vector3(1f, 0f, 1f);
            cursor.SetAnchor(anchor);
            Assert.AreEqual(anchor, cursor.Anchor);

            Vector3 point;

            Assert.IsFalse(cursor.TryAdvance(1f, false, 2f, 3f, out point), "가는 중에는 고르지 않는다");
            Assert.AreEqual(0, provider.TryGetPointCount, "가는 중에는 제공자를 부르지도 않는다");

            Assert.IsFalse(cursor.TryAdvance(1f, true, 2f, 3f, out point), "대기 시간을 못 채웠다");
            Assert.AreEqual(0, provider.TryGetPointCount);

            Assert.IsTrue(cursor.TryAdvance(1f, true, 2f, 3f, out point));
            Assert.AreEqual(provider.Point, point);
            Assert.AreEqual(1, provider.TryGetPointCount);
            Assert.AreEqual(anchor, provider.LastAnchor, "배회는 기준점 주변에서만 일어난다");
            Assert.AreEqual(3f, provider.LastRadius, 0.0001f);

            // 지점을 못 고르면 false 이고, 대기 시간은 이미 소모됐다.
            provider.Active = false;
            Assert.IsFalse(cursor.TryAdvance(2f, true, 2f, 3f, out point));
            Assert.AreEqual(2, provider.TryGetPointCount);
        }

        [Test]
        [Description("TASK-009 - 이동 중이면 배회 대기 시간이 되돌아가고, 기준점을 옮겨도 되돌아간다")]
        public void WanderCursor_WaitTimer_RewindsWhileMoving_AndOnAnchorChange()
        {
            FakeWanderPointProvider provider = new FakeWanderPointProvider();
            provider.Active = true;
            provider.Point = new Vector3(9f, 0f, 9f);

            K9WanderCursor cursor = new K9WanderCursor(provider);
            cursor.SetAnchor(Vector3.zero);

            Vector3 point;

            cursor.TryAdvance(1f, true, 2f, 3f, out point);
            Assert.IsFalse(cursor.TryAdvance(1f, false, 2f, 3f, out point), "다시 움직이기 시작했다");
            Assert.IsFalse(cursor.TryAdvance(1f, true, 2f, 3f, out point), "대기는 처음부터 다시 센다");
            Assert.IsTrue(cursor.TryAdvance(1f, true, 2f, 3f, out point));

            // 기준점 교체도 대기를 되돌린다 — 새 조사 지점에 도착하기 전에 곧바로 배회하면 안 된다.
            cursor.TryAdvance(1f, true, 2f, 3f, out point);
            Vector3 moved = new Vector3(5f, 0f, 5f);
            cursor.SetAnchor(moved);
            Assert.AreEqual(moved, cursor.Anchor);
            Assert.IsFalse(cursor.TryAdvance(1f, true, 2f, 3f, out point), "기준점을 옮기면 대기도 되돌아간다");
            Assert.IsTrue(cursor.TryAdvance(1f, true, 2f, 3f, out point));
            Assert.AreEqual(moved, provider.LastAnchor);
        }

        [Test]
        [Description("TASK-009 5.3-7 - 분산에 실패하면 받은 소집 좌표를 그대로 돌려준다")]
        public void WanderCursor_Spread_FallsBackToSummonPoint_OnEveryFailure()
        {
            FakeWanderPointProvider provider = new FakeWanderPointProvider();
            K9WanderCursor cursor = new K9WanderCursor(provider);

            Vector3 summon = new Vector3(7f, 0f, 2f);

            // 1) 지점 선택 실패.
            provider.Active = false;
            Assert.AreEqual(summon, cursor.Spread(summon, 3f));
            Assert.AreEqual(1, provider.TryGetPointCount);

            // 2) 반경이 0 이하 — 제공자를 부르지도 않는다.
            provider.Active = true;
            provider.Point = new Vector3(8f, 0f, 3f);
            Assert.AreEqual(summon, cursor.Spread(summon, 0f));
            Assert.AreEqual(summon, cursor.Spread(summon, -1f));
            Assert.AreEqual(1, provider.TryGetPointCount, "반경이 없으면 분산을 시도하지 않는다");

            // 3) 성공하면 흩어진 좌표.
            Assert.AreEqual(provider.Point, cursor.Spread(summon, 3f));
            Assert.AreEqual(summon, provider.LastAnchor, "분산의 기준은 소집 좌표다");
        }

        [Test]
        [Description("TASK-009 - 지점 제공자가 없으면 배회하지 않고 분산도 하지 않는다")]
        public void WanderCursor_WithoutProvider_NeverPicks_AndNeverSpreads()
        {
            K9WanderCursor cursor = new K9WanderCursor(null);
            cursor.SetAnchor(new Vector3(2f, 0f, 2f));

            Vector3 point;
            for (int i = 0; i < 5; i++)
            {
                Assert.IsFalse(cursor.TryAdvance(10f, true, 2f, 3f, out point));
            }

            Vector3 summon = new Vector3(4f, 0f, 4f);
            Assert.AreEqual(summon, cursor.Spread(summon, 3f));
        }

        // ── K9HowlClock ────────────────────────────────────────────

        [Test]
        [Description("TASK-009 5.3-4 - 한 번도 짖지 않았으면 경과 시간을 누적하지 않는다")]
        public void HowlClock_DoesNotAccumulate_BeforeFirstHowl()
        {
            K9HowlClock clock = new K9HowlClock();

            for (int i = 0; i < 100; i++)
            {
                clock.Tick(1f);
            }

            Assert.IsFalse(clock.HasHowled);
            Assert.AreEqual(0f, clock.SecondsSinceHowl, 0.0001f, "짖기 전의 시간은 세지 않는다");
            Assert.IsFalse(clock.HowledWithin(1000f), "짖은 적이 없으면 언제나 false 다");

            // 짖은 순간부터 0 에서 시작한다 — 짖기 전에 흘린 100초가 딸려 오면 여기서 깨진다.
            clock.MarkHowled();
            Assert.IsTrue(clock.HasHowled);
            Assert.IsTrue(clock.HowledWithin(0.5f));

            clock.Tick(0.4f);
            Assert.AreEqual(0.4f, clock.SecondsSinceHowl, 0.0001f);
            Assert.IsTrue(clock.HowledWithin(0.5f));
        }

        [Test]
        [Description("TASK-009 5.3-5 - 정확히 그 시간이 흘렀으면 쿨다운은 끝난 것으로 본다")]
        public void HowlClock_HowledWithin_IsFalseAtTheBoundary()
        {
            K9HowlClock clock = new K9HowlClock();
            clock.MarkHowled();

            clock.Tick(1f);
            clock.Tick(1f);
            Assert.AreEqual(2f, clock.SecondsSinceHowl, 0.0001f, "사전 조건: 정확히 2초");

            Assert.IsFalse(clock.HowledWithin(2f), "경계값은 false 다");
            Assert.IsTrue(clock.HowledWithin(2.5f));
            Assert.IsFalse(clock.HowledWithin(1.5f));
        }

        [Test]
        [Description("TASK-009 - 다시 짖으면 경과 시간이 0 으로 되돌아간다")]
        public void HowlClock_MarkHowled_RewindsElapsedTime()
        {
            K9HowlClock clock = new K9HowlClock();
            clock.MarkHowled();
            clock.Tick(5f);
            Assert.IsFalse(clock.HowledWithin(3f), "사전 조건: 쿨다운이 지났다");

            clock.MarkHowled();
            Assert.AreEqual(0f, clock.SecondsSinceHowl, 0.0001f);
            Assert.IsTrue(clock.HowledWithin(3f));
        }

        // ── 조립 도우미 ─────────────────────────────────────────────

        private static List<Vector3> MakeRoute(int count)
        {
            List<Vector3> route = new List<Vector3>(count);
            for (int i = 0; i < count; i++)
            {
                route.Add(new Vector3(i * 10f, 0f, 0f));
            }

            return route;
        }
    }
}
