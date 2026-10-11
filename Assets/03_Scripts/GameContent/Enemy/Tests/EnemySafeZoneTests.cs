using System.Collections.Generic;
using NUnit.Framework;
using Pawntom.Enemy.Core;
using UnityEngine;

namespace Pawntom.Enemy.Tests
{
    /// <summary>
    /// 본부(출입 금지 구역) 규칙: 구역 안을 가리키는 단서·추적 좌표는 두뇌가 버린다.
    /// </summary>
    public sealed class EnemySafeZoneTests
    {
        /// <summary>x ≥ 10 인 반공간을 본부로 친다.</summary>
        private sealed class FakeSafeZone : IEnemySafeZone
        {
            public bool Contains(Vector3 worldPoint)
            {
                return worldPoint.x >= 10f;
            }
        }

        private static readonly Vector3 Outside = new Vector3(5f, 0f, 0f);
        private static readonly Vector3 Inside = new Vector3(20f, 0f, 0f);

        private sealed class Rig
        {
            public readonly EnemySettings Settings = new EnemySettings();
            public readonly FakeMotor Motor = new FakeMotor();
            public readonly SpyAlertChannel Alert = new SpyAlertChannel();
            public readonly FakePerceptionSource Source = new FakePerceptionSource();
            public readonly FakeTargetTracker Tracker = new FakeTargetTracker();
            public readonly FakeWanderPointProvider Wander = new FakeWanderPointProvider();
            public readonly List<IEnemyPerceptionSource> Sources = new List<IEnemyPerceptionSource>(4);
            public readonly EnemyBrain Brain;

            public Rig(bool withSafeZone)
            {
                Sources.Add(Source);
                Brain = new EnemyBrain(Settings, Motor, Sources, Alert, Tracker, Wander);

                if (withSafeZone)
                {
                    Brain.SetSafeZone(new FakeSafeZone());
                }
            }

            /// <summary>바깥에서 접촉 → Alert → 바깥에서 시야 → Chase.</summary>
            public void EnterChaseOutside()
            {
                Source.Report(EnemyDetection.Contact(new Vector3(1f, 0f, 0f)));
                Brain.Tick(0.1f);
                Assert.AreEqual(EnemyState.Alert, Brain.State, "사전 조건: Alert 진입");

                Source.Report(EnemyDetection.Sight(Outside));
                Brain.Tick(Settings.Alert.HowlDurationSeconds);
                Assert.AreEqual(EnemyState.Chase, Brain.State, "사전 조건: Chase 진입");
            }
        }

        // ── 상자 판정 ──

        [Test]
        public void SafeBox_PointInside_ReturnsTrue()
        {
            Assert.IsTrue(EnemySafeBox.Contains(new Vector3(0.4f, -0.4f, 0.4f), Vector3.zero, Vector3.one, 0f));
        }

        [Test]
        public void SafeBox_PointOutside_ReturnsFalse()
        {
            Assert.IsFalse(EnemySafeBox.Contains(new Vector3(0.6f, 0f, 0f), Vector3.zero, Vector3.one, 0f));
        }

        [Test]
        public void SafeBox_WithMargin_IncludesPointJustOutside()
        {
            Assert.IsTrue(EnemySafeBox.Contains(new Vector3(0.6f, 0f, 0f), Vector3.zero, Vector3.one, 0.2f));
        }

        [Test]
        public void SafeBox_OffsetCenter_UsesCenter()
        {
            Assert.IsTrue(EnemySafeBox.Contains(new Vector3(5f, 0f, 0f), new Vector3(5f, 0f, 0f), Vector3.one, 0f));
        }

        // ── 두뇌 ──

        [Test]
        public void Perceive_SightInsideSafeZone_StaysOnPatrol()
        {
            Rig rig = new Rig(true);
            rig.Source.Report(EnemyDetection.Sight(Inside));

            rig.Brain.Tick(0.1f);

            Assert.AreEqual(EnemyState.Patrol, rig.Brain.State);
        }

        [Test]
        public void Perceive_TraceInsideSafeZone_StaysOnPatrol()
        {
            Rig rig = new Rig(true);
            rig.Source.Report(EnemyDetection.Trace(Inside));

            rig.Brain.Tick(0.1f);

            Assert.AreEqual(EnemyState.Patrol, rig.Brain.State);
        }

        [Test]
        public void Perceive_NoSafeZone_SightAnywhereLeavesPatrol()
        {
            Rig rig = new Rig(false);
            rig.Source.Report(EnemyDetection.Sight(Inside));

            rig.Brain.Tick(0.1f);

            Assert.AreNotEqual(EnemyState.Patrol, rig.Brain.State);
        }

        [Test]
        public void Chase_TrackedPositionInsideSafeZone_DoesNotMoveIntoIt()
        {
            Rig rig = new Rig(true);
            rig.EnterChaseOutside();
            rig.Source.Clear();
            rig.Tracker.Active = true;
            rig.Tracker.TrackedPosition = Inside;

            rig.Brain.Tick(0.1f);

            Assert.AreNotEqual(Inside, rig.Motor.LastDestination);
        }

        [Test]
        public void Chase_TargetHidesInSafeZone_EndsChaseAfterLoseSight()
        {
            // 3-6 "본부 안까지는 들어오지 못합니다": 안으로 들어간 대상은 보이지 않는 것과 같다.
            Rig rig = new Rig(true);
            rig.EnterChaseOutside();
            rig.Source.Report(EnemyDetection.Sight(Inside));
            rig.Tracker.Active = true;
            rig.Tracker.TrackedPosition = Inside;

            rig.Brain.Tick(rig.Settings.Chase.LoseSightSeconds + 0.1f);

            Assert.AreEqual(EnemyState.Investigate, rig.Brain.State);
        }
    }
}
