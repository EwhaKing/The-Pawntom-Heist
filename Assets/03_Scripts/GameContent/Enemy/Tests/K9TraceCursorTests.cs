using System.Collections.Generic;
using NUnit.Framework;
using Pawntom.Enemy.Core;
using UnityEngine;

namespace Pawntom.Enemy.Tests
{
    /// <summary>
    /// TASK-012 5.3 수용 기준 1~5 의 기계 검증과 5.4 의 추가 3건.
    /// <para>
    /// 여기서는 <see cref="K9Brain"/> 을 한 번도 만들지 않는다. 커서는 좌표와 id 만 다루므로
    /// 두뇌도 씬도 없이 결정적으로 검증된다.
    /// </para>
    /// </summary>
    public sealed class K9TraceCursorTests
    {
        private const float Radius = 10f;
        private const float Reach = 1f;

        // TASK-013 회귀 방지용 실측 수치. K-9_Prefab 의 루트 스케일 3배가
        // NavMeshAgent.baseOffset 1 에 곱해져 개의 원점이 바닥에서 3m 뜬다.
        private const float DogY = 3f;

        // FurTrailSpawner 의 surfaceOffset. 바닥 Plane 이 y=0 이므로 흔적은 0.3m 에 놓인다.
        private const float TraceY = 0.3f;

        private static readonly Vector3 DogOrigin = new Vector3(0f, DogY, 0f);

        // ── 기준 1: 반경 밖 후보는 목표가 되지 않는다 ────────────────

        [Test]
        [Description("TASK-012 5.3-1 - 반경 밖에만 후보가 있으면 목표를 잡지 않는다")]
        public void Criterion1_CandidatesOutsideRadius_AreNotTargeted()
        {
            K9TraceCursor cursor = new K9TraceCursor();
            List<K9TraceCandidate> candidates = new List<K9TraceCandidate>
            {
                Candidate(1, 20f, 100),
                Candidate(2, 15f, 200)
            };

            K9TraceStep step = cursor.Advance(Vector3.zero, candidates, Radius, Reach);

            Assert.IsFalse(step.HasTarget, "반경 밖 후보를 목표로 잡았다");
            Assert.IsFalse(step.HasConsumed, "가 보지도 않은 흔적을 소비했다");
            Assert.IsFalse(cursor.HasTarget);
        }

        // ── 기준 2: 가장 최근 것을 고른다. 더 멀어도 고른다 ──────────

        [Test]
        [Description("TASK-012 5.3-2 - 반경 안에서 CreatedTick 이 가장 큰 후보를 고른다. 더 멀어도 고른다")]
        public void Criterion2_PicksNewestCandidate_EvenWhenFarther()
        {
            K9TraceCursor cursor = new K9TraceCursor();
            List<K9TraceCandidate> candidates = new List<K9TraceCandidate>
            {
                Candidate(1, 2f, 100),
                Candidate(2, 9f, 150),
                Candidate(3, 4f, 120)
            };

            K9TraceStep step = cursor.Advance(Vector3.zero, candidates, Radius, Reach);

            Assert.IsTrue(step.HasTarget);
            Assert.AreEqual(2, cursor.TargetId, "가까운 것이 아니라 가장 최근 것을 골라야 한다");
            Assert.AreEqual(candidates[1].Position, step.Target);
        }

        [Test]
        [Description("TASK-012 5.3-2 - CreatedTick 이 같으면 더 가까운 후보를 고른다")]
        public void Criterion2_SameTick_PicksNearer()
        {
            K9TraceCursor cursor = new K9TraceCursor();
            List<K9TraceCandidate> candidates = new List<K9TraceCandidate>
            {
                Candidate(1, 8f, 150),
                Candidate(2, 3f, 150)
            };

            K9TraceStep step = cursor.Advance(Vector3.zero, candidates, Radius, Reach);

            Assert.IsTrue(step.HasTarget);
            Assert.AreEqual(2, cursor.TargetId);
        }

        // ── 기준 3: 도달하면 소비한다 ────────────────────────────────

        [Test]
        [Description("TASK-012 5.3-3 - 목표가 reachDistance 안에 들어오면 그 목표의 Id 를 소비로 알린다")]
        public void Criterion3_ReachingTarget_ReportsConsumedId()
        {
            K9TraceCursor cursor = new K9TraceCursor();
            List<K9TraceCandidate> candidates = new List<K9TraceCandidate>
            {
                Candidate(7, 5f, 150)
            };

            // 첫 호출 — 목표만 잡는다.
            K9TraceStep first = cursor.Advance(Vector3.zero, candidates, Radius, Reach);
            Assert.IsTrue(first.HasTarget);
            Assert.IsFalse(first.HasConsumed, "아직 멀리 있는데 소비했다");

            // 개가 목표 코앞까지 갔다.
            K9TraceStep second = cursor.Advance(new Vector3(4.5f, 0f, 0f), candidates, Radius, Reach);

            Assert.IsTrue(second.HasConsumed);
            Assert.AreEqual(7, second.ConsumedId);
            Assert.IsFalse(cursor.HasTarget, "소비한 뒤에는 목표를 놓아야 한다");
        }

        // ── 기준 4: 소비한 후보를 같은 틱에 다시 고르지 않는다 ───────

        [Test]
        [Description("TASK-012 5.3-4 - 소비한 후보가 같은 호출의 목록에 남아 있어도 그 틱에 다시 고르지 않는다")]
        public void Criterion4_ConsumedCandidate_IsNotRetargetedInSameCall()
        {
            K9TraceCursor cursor = new K9TraceCursor();

            // 삭제는 어댑터가 Advance 뒤에 하므로, 소비한 항목이 같은 목록에 그대로 남아 있다.
            List<K9TraceCandidate> candidates = new List<K9TraceCandidate>
            {
                Candidate(1, 0.5f, 150),
                Candidate(2, 6f, 120)
            };

            K9TraceStep first = cursor.Advance(Vector3.zero, candidates, Radius, Reach);
            Assert.AreEqual(1, cursor.TargetId, "사전 조건: 가장 최근 흔적을 목표로 잡는다");
            Assert.IsFalse(first.HasConsumed, "사전 조건: 첫 호출은 목표만 잡는다");

            K9TraceStep second = cursor.Advance(Vector3.zero, candidates, Radius, Reach);

            Assert.IsTrue(second.HasConsumed);
            Assert.AreEqual(1, second.ConsumedId);
            Assert.AreNotEqual(1, cursor.TargetId, "방금 먹은 흔적을 같은 틱에 다시 목표로 골랐다");
            Assert.AreEqual(2, cursor.TargetId, "남은 후보 중 가장 최근 것으로 넘어가야 한다");
        }

        [Test]
        [Description("TASK-012 5.3-4 - 소비한 후보 하나만 남아 있으면 그 틱에는 목표가 없다")]
        public void Criterion4_OnlyConsumedCandidateRemains_LeavesNoTarget()
        {
            K9TraceCursor cursor = new K9TraceCursor();
            List<K9TraceCandidate> candidates = new List<K9TraceCandidate>
            {
                Candidate(9, 0.5f, 150)
            };

            cursor.Advance(Vector3.zero, candidates, Radius, Reach);
            K9TraceStep step = cursor.Advance(Vector3.zero, candidates, Radius, Reach);

            Assert.IsTrue(step.HasConsumed);
            Assert.AreEqual(9, step.ConsumedId);
            Assert.IsFalse(step.HasTarget, "소비한 흔적 하나뿐인데 목표가 남았다");
            Assert.IsFalse(cursor.HasTarget);
        }

        // ── 기준 5: 목표가 목록에서 사라지면 놓고 다음을 고른다 ──────

        [Test]
        [Description("TASK-012 5.3-5 - 목표가 다음 호출의 목록에서 사라지면 남은 후보 중 가장 최근 것으로 옮긴다")]
        public void Criterion5_MissingTarget_FallsBackToNewestRemaining()
        {
            K9TraceCursor cursor = new K9TraceCursor();
            List<K9TraceCandidate> candidates = new List<K9TraceCandidate>
            {
                Candidate(1, 5f, 100),
                Candidate(2, 6f, 150),
                Candidate(3, 7f, 120)
            };

            cursor.Advance(Vector3.zero, candidates, Radius, Reach);
            Assert.AreEqual(2, cursor.TargetId, "사전 조건: tick 150 을 목표로 잡는다");

            // 다른 개가 먼저 먹었거나 수명이 다해 목록에서 빠졌다.
            candidates.RemoveAt(1);

            K9TraceStep step = cursor.Advance(Vector3.zero, candidates, Radius, Reach);

            Assert.IsTrue(step.HasTarget);
            Assert.IsFalse(step.HasConsumed, "사라진 목표를 소비로 보고했다");
            Assert.AreEqual(3, cursor.TargetId, "남은 것 중 tick 120 으로 옮겨야 한다");
        }

        [Test]
        [Description("TASK-012 5.3-5 - 목표가 반경 밖으로 나가면 목표를 놓는다")]
        public void Criterion5_TargetLeavingRadius_ReleasesTarget()
        {
            K9TraceCursor cursor = new K9TraceCursor();
            List<K9TraceCandidate> candidates = new List<K9TraceCandidate>
            {
                Candidate(1, 9f, 150)
            };

            cursor.Advance(Vector3.zero, candidates, Radius, Reach);
            Assert.IsTrue(cursor.HasTarget, "사전 조건: 반경 안이라 목표를 잡는다");

            // 개가 반대쪽으로 끌려가 목표가 반경 밖이 됐다.
            K9TraceStep step = cursor.Advance(new Vector3(-5f, 0f, 0f), candidates, Radius, Reach);

            Assert.IsFalse(step.HasTarget);
            Assert.IsFalse(cursor.HasTarget);
        }

        // ── 5.4 추가 3건 ─────────────────────────────────────────────

        [Test]
        [Description("TASK-012 5.4 - 후보 목록이 비어도 예외 없이 목표 없음을 반환한다")]
        public void EmptyCandidates_ReturnsNoTarget_WithoutException()
        {
            K9TraceCursor cursor = new K9TraceCursor();
            List<K9TraceCandidate> empty = new List<K9TraceCandidate>();

            K9TraceStep step = default(K9TraceStep);

            Assert.DoesNotThrow(delegate
            {
                step = cursor.Advance(Vector3.zero, empty, Radius, Reach);
            });

            Assert.IsFalse(step.HasTarget);
            Assert.IsFalse(step.HasConsumed);
            Assert.IsFalse(cursor.HasTarget);
        }

        [Test]
        [Description("TASK-012 5.4 - detectionRadius 가 0 이면 아무것도 고르지 않는다")]
        public void ZeroDetectionRadius_PicksNothing()
        {
            K9TraceCursor cursor = new K9TraceCursor();
            List<K9TraceCandidate> candidates = new List<K9TraceCandidate>
            {
                Candidate(1, 0f, 150),
                Candidate(2, 2f, 120)
            };

            K9TraceStep step = cursor.Advance(Vector3.zero, candidates, 0f, Reach);

            Assert.IsFalse(step.HasTarget);
            Assert.IsFalse(step.HasConsumed);
            Assert.IsFalse(cursor.HasTarget);
        }

        [Test]
        [Description("TASK-012 5.4 - 여러 틱에 걸쳐 다가가면 reachDistance 에 들어가는 틱에만 소비가 일어난다")]
        public void ApproachingOverManyTicks_ConsumesOnlyOnArrivalTick()
        {
            K9TraceCursor cursor = new K9TraceCursor();
            List<K9TraceCandidate> candidates = new List<K9TraceCandidate>
            {
                Candidate(5, 5f, 150)
            };

            // 5m 지점의 흔적을 향해 1m 씩 다가간다. 4m 지점까지는 소비가 없어야 한다.
            for (int i = 0; i < 4; i++)
            {
                K9TraceStep step = cursor.Advance(
                    new Vector3(i, 0f, 0f), candidates, Radius, Reach);

                Assert.IsTrue(step.HasTarget, "가는 도중에 목표를 놓았다: x=" + i);
                Assert.IsFalse(step.HasConsumed, "도달 전인데 소비했다: x=" + i);
            }

            // 남은 거리 1m — reachDistance 안이다.
            K9TraceStep arrival = cursor.Advance(new Vector3(4f, 0f, 0f), candidates, Radius, Reach);

            Assert.IsTrue(arrival.HasConsumed, "도달했는데 소비하지 않았다");
            Assert.AreEqual(5, arrival.ConsumedId);
        }

        // ── TASK-013: 높이차가 있어도 수평(XZ)으로 판정한다 ──────────

        [Test]
        [Description("TASK-013 5.3-1 - 개 원점이 baseOffset 만큼 떠 있어도 바로 아래 흔적을 소비한다")]
        public void TraceCursor_OriginRaisedByBaseOffset_ConsumesTraceDirectlyBelow()
        {
            // K-9_Prefab 실측: 루트 스케일 3배 x NavMeshAgent.baseOffset 1 = 원점 y 3.
            // 털공은 FurTrailSpawner 의 surfaceOffset 0.3 이라 높이차가 2.7 로 고정된다.
            // 3D 거리로 재면 2.7^2 = 7.29 > reach^2 = 1 이라 도달이 성립하지 못했다.
            K9TraceCursor cursor = new K9TraceCursor();
            List<K9TraceCandidate> candidates = new List<K9TraceCandidate>
            {
                GroundTrace(11, 0f, 0f, 150)
            };

            K9TraceStep first = cursor.Advance(DogOrigin, candidates, Radius, Reach);
            Assert.IsTrue(first.HasTarget, "사전 조건: 바로 아래 흔적을 목표로 잡는다");

            K9TraceStep second = cursor.Advance(DogOrigin, candidates, Radius, Reach);

            Assert.IsTrue(second.HasConsumed, "개가 흔적 바로 위에 섰는데 소비하지 않았다");
            Assert.AreEqual(11, second.ConsumedId);
            Assert.IsFalse(cursor.HasTarget, "소비한 뒤에는 목표를 놓아야 한다");
        }

        [Test]
        [Description("TASK-013 5.3-2 - 높이차가 있어도 반경 판정은 수평 거리로만 한다")]
        public void TraceCursor_HeightGap_UsesHorizontalDistanceForRadius()
        {
            List<K9TraceCandidate> candidates = new List<K9TraceCandidate>
            {
                GroundTrace(21, 5f, 0f, 150)
            };

            K9TraceStep inside = new K9TraceCursor().Advance(DogOrigin, candidates, 8f, Reach);
            Assert.IsTrue(inside.HasTarget, "수평 5m 는 반경 8 안이다");
            Assert.IsFalse(inside.HasConsumed, "수평 5m 인데 도달로 봤다");

            K9TraceStep outside = new K9TraceCursor().Advance(DogOrigin, candidates, 4f, Reach);
            Assert.IsFalse(outside.HasTarget, "수평 5m 는 반경 4 밖이다");

            // 경계 5.2 — 3D 로 재면 sqrt(5^2 + 2.7^2) = 5.68 이라 여기서 갈린다.
            K9TraceStep boundary = new K9TraceCursor().Advance(DogOrigin, candidates, 5.2f, Reach);
            Assert.IsTrue(boundary.HasTarget, "높이차가 반경 판정에 섞였다");
        }

        [Test]
        [Description("TASK-013 5.3-3 - 높이차가 있는 후보들 사이에서도 CreatedTick 최대 우선이 유지된다")]
        public void TraceCursor_HeightGap_StillPicksNewestCandidate()
        {
            K9TraceCursor cursor = new K9TraceCursor();
            List<K9TraceCandidate> candidates = new List<K9TraceCandidate>
            {
                GroundTrace(31, 2f, 0f, 100),
                GroundTrace(32, 7f, 0f, 150),
                GroundTrace(33, 0f, 4f, 120)
            };

            K9TraceStep step = cursor.Advance(DogOrigin, candidates, Radius, Reach);

            Assert.IsTrue(step.HasTarget);
            Assert.AreEqual(32, cursor.TargetId, "가까운 것이 아니라 가장 최근 것을 골라야 한다");
            Assert.AreEqual(candidates[1].Position, step.Target);
        }

        [Test]
        [Description("TASK-013 5.3-3 - CreatedTick 이 같으면 3D 가 아니라 수평으로 더 가까운 후보를 고른다")]
        public void TraceCursor_SameTickWithHeightGap_PicksHorizontallyNearer()
        {
            K9TraceCursor cursor = new K9TraceCursor();

            // 41 은 수평 3.0 / 3D 4.04, 42 는 수평 3.5 / 3D 3.5.
            // 3D 로 재면 32 가 더 가깝다고 나오므로 계산 방식이 결과를 가른다.
            List<K9TraceCandidate> candidates = new List<K9TraceCandidate>
            {
                GroundTrace(41, 3f, 0f, 150),
                new K9TraceCandidate(42, new Vector3(3.5f, DogY, 0f), 150)
            };

            K9TraceStep step = cursor.Advance(DogOrigin, candidates, Radius, Reach);

            Assert.IsTrue(step.HasTarget);
            Assert.AreEqual(41, cursor.TargetId, "동률일 때 높이차가 거리 비교에 섞였다");
        }

        // ── 도우미 ───────────────────────────────────────────────────

        /// <summary>원점에서 +X 방향으로 <paramref name="distance"/> m 떨어진 후보 하나.</summary>
        private static K9TraceCandidate Candidate(int id, float distance, int createdTick)
        {
            return new K9TraceCandidate(id, new Vector3(distance, 0f, 0f), createdTick);
        }

        /// <summary>바닥에 붙은 흔적 하나. y 는 실제 스폰 높이 <see cref="TraceY"/> 를 쓴다.</summary>
        private static K9TraceCandidate GroundTrace(int id, float x, float z, int createdTick)
        {
            return new K9TraceCandidate(id, new Vector3(x, TraceY, z), createdTick);
        }
    }
}
