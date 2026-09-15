using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Pawntom.Enemy.Adapters;
using Pawntom.Enemy.Core;
using UnityEngine;

namespace Pawntom.Enemy.Tests
{
    /// <summary>
    /// TASK-017 5.4 — 청취 정책 검증.
    /// <para>
    /// (가) 정책 단독은 씬이 필요 없는 순수 클래스 검증이다.
    /// (나) 채널 통합은 <see cref="EnemyAlertChannelBehaviour"/> 가 거리 판정을
    /// 정책에 넘겼는지를 실제 방송 경로로 확인한다.
    /// </para>
    /// <para>
    /// 이 파일에서 가장 중요한 것은 <b>경계값</b>이다 — 옮겨 오기 전 코드가
    /// <c>if (sqrDistance &gt; sqrRadius) continue;</c> 였으므로
    /// 거리가 정확히 반경과 같으면 <b>들려야</b> 한다. 부등호를 <c>&lt;</c> 로 쓰면
    /// 이 파일에서 그 테스트 하나만 실패한다.
    /// </para>
    /// </summary>
    public sealed class EnemyHearingPolicyTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>(4);
        private readonly List<EnemyAlertChannelBehaviour> _enabled =
            new List<EnemyAlertChannelBehaviour>(4);

        [SetUp]
        public void SetUp()
        {
            _spawned.Clear();
            _enabled.Clear();
            ClearActiveChannels();

            Assert.AreEqual(0, ActiveChannels().Count, "사전 조건: 활성 채널 목록이 비어 있다");
        }

        [TearDown]
        public void TearDown()
        {
            // Unity 가 부르는 것과 같은 해제 경로를 먼저 태운다.
            for (int i = 0; i < _enabled.Count; i++)
            {
                EnemyAlertChannelBehaviour channel = _enabled[i];
                if (channel != null)
                {
                    InvokeLifecycle(channel, "OnDisable");
                }
            }

            _enabled.Clear();

            for (int i = 0; i < _spawned.Count; i++)
            {
                GameObject spawned = _spawned[i];
                if (spawned != null)
                {
                    Object.DestroyImmediate(spawned);
                }
            }

            _spawned.Clear();

            // OnDisable 이 빠진 경로가 있어도 다음 테스트로 새지 않게 한 번 더 비운다.
            ClearActiveChannels();
        }

        // ── (가) 정책 단독 — 기준 1·2 ─────────────────────────────────

        [Test]
        [Description("TASK-017 5.3-1 - 반경 안이면 들린다")]
        public void RangedHearingPolicy_DistanceInsideRadius_ReturnsTrue()
        {
            RangedHearingPolicy policy = new RangedHearingPolicy();

            Assert.IsTrue(policy.CanHear(new Vector3(3f, 0f, 4f), Vector3.zero, 30f), "거리 5, 반경 30");
            Assert.IsTrue(policy.CanHear(Vector3.zero, Vector3.zero, 30f), "같은 자리도 반경 안이다");
        }

        [Test]
        [Description("TASK-017 5.3-1 - 거리가 정확히 반경과 같으면 들린다 (현행 > sqrRadius 와 동치)")]
        public void RangedHearingPolicy_DistanceEqualsRadius_ReturnsTrue()
        {
            RangedHearingPolicy policy = new RangedHearingPolicy();

            // 3-4-5 삼각형이라 sqrMagnitude 가 정확히 25f, radius*radius 도 정확히 25f 다.
            // 부동소수 오차로 흔들리는 값이 아니므로 부등호만 검증된다.
            Assert.IsTrue(
                policy.CanHear(new Vector3(3f, 0f, 4f), Vector3.zero, 5f),
                "경계에 선 개체는 들어야 한다 - 부등호를 < 로 쓰면 여기서 실패한다");

            Assert.IsTrue(
                policy.CanHear(new Vector3(30f, 0f, 0f), Vector3.zero, 30f),
                "실제 기본 반경 30 에서도 경계는 들린다");
        }

        [Test]
        [Description("TASK-017 5.3-1 - 반경 밖이면 들리지 않는다")]
        public void RangedHearingPolicy_DistanceOutsideRadius_ReturnsFalse()
        {
            RangedHearingPolicy policy = new RangedHearingPolicy();

            Assert.IsFalse(policy.CanHear(new Vector3(0f, 0f, 30.5f), Vector3.zero, 30f), "거리 30.5, 반경 30");
            Assert.IsFalse(policy.CanHear(new Vector3(1000f, 0f, 0f), Vector3.zero, 30f), "멀면 못 듣는다");
        }

        [Test]
        [Description("TASK-017 5.3-1 - 원점이 아닌 곳에서 난 하울링도 상대 거리로 판단한다")]
        public void RangedHearingPolicy_OriginAwayFromWorldZero_UsesRelativeDistance()
        {
            RangedHearingPolicy policy = new RangedHearingPolicy();
            Vector3 origin = new Vector3(100f, 0f, 100f);

            Assert.IsTrue(policy.CanHear(new Vector3(110f, 0f, 100f), origin, 30f), "상대 거리 10");
            Assert.IsFalse(policy.CanHear(new Vector3(140f, 0f, 100f), origin, 30f), "상대 거리 40");
        }

        [Test]
        [Description("TASK-017 5.3-2 - 반경이 0 이어도 들린다")]
        public void GlobalHearingPolicy_ZeroRadius_ReturnsTrue()
        {
            GlobalHearingPolicy policy = new GlobalHearingPolicy();

            Assert.IsTrue(policy.CanHear(new Vector3(500f, 0f, 0f), Vector3.zero, 0f));
        }

        [Test]
        [Description("TASK-017 5.3-2 - 반경이 음수여도 들린다")]
        public void GlobalHearingPolicy_NegativeRadius_ReturnsTrue()
        {
            GlobalHearingPolicy policy = new GlobalHearingPolicy();

            // 제곱하면 양수가 되므로, 반경 비교가 조금이라도 남아 있으면 여기가 흔들린다.
            Assert.IsTrue(policy.CanHear(new Vector3(500f, 0f, 0f), Vector3.zero, -1f));
        }

        [Test]
        [Description("TASK-017 5.3-2 - 거리가 10000 이어도 들린다")]
        public void GlobalHearingPolicy_DistanceFarBeyondRadius_ReturnsTrue()
        {
            GlobalHearingPolicy policy = new GlobalHearingPolicy();

            Assert.IsTrue(policy.CanHear(new Vector3(10000f, 0f, 0f), Vector3.zero, 30f));
        }

        // ── (나) 채널 통합 — 기준 4·5·6 ───────────────────────────────

        [Test]
        [Description("TASK-017 5.3-4 - 청취 정책 부품이 없는 채널은 반경 안에서만 받는다")]
        public void AlertChannel_ListenerWithoutPolicy_ReceivesOnlyInsideRadius()
        {
            EnemyAlertChannelBehaviour howler = NewChannel("howler", Vector3.zero, false);
            EnemyAlertChannelBehaviour near = NewChannel("near", new Vector3(0f, 0f, 20f), false);
            EnemyAlertChannelBehaviour far = NewChannel("far", new Vector3(0f, 0f, 100f), false);

            howler.Broadcast(Vector3.zero, 30f);

            Vector3 target;
            Assert.IsTrue(near.TryConsumeSummon(out target), "반경 안의 동료는 받는다");
            Assert.AreEqual(Vector3.zero, target, "받은 좌표는 하울링이 난 좌표다");

            Assert.IsFalse(far.TryConsumeSummon(out target), "반경 밖의 동료는 받지 않는다 - 기존 30m 제한 유지");
        }

        [Test]
        [Description("TASK-017 5.3-4 - 부품이 없는 채널도 경계값에서는 받는다")]
        public void AlertChannel_ListenerWithoutPolicy_AtExactRadius_Receives()
        {
            EnemyAlertChannelBehaviour howler = NewChannel("howler", Vector3.zero, false);
            EnemyAlertChannelBehaviour edge = NewChannel("edge", new Vector3(30f, 0f, 0f), false);

            howler.Broadcast(Vector3.zero, 30f);

            Vector3 target;
            Assert.IsTrue(edge.TryConsumeSummon(out target), "경계에 선 동료는 받는다");
        }

        [Test]
        [Description("TASK-017 5.3-5 - Global Hearing 을 붙인 청취자는 1000m 밖에서도 받는다")]
        public void AlertChannel_ListenerWithGlobalHearing_ReceivesFarBeyondRadius()
        {
            EnemyAlertChannelBehaviour howler = NewChannel("howler", Vector3.zero, false);
            EnemyAlertChannelBehaviour brute = NewChannel("brute", new Vector3(1000f, 0f, 0f), true);

            howler.Broadcast(Vector3.zero, 30f);

            Vector3 target;
            Assert.IsTrue(brute.TryConsumeSummon(out target), "반경을 무시하고 받는다");
            Assert.AreEqual(Vector3.zero, target, "받은 좌표는 하울링이 난 좌표다");
        }

        [Test]
        [Description("TASK-017 5.3-6 - 하울러 자신은 언제나 듣는 정책을 달고 있어도 자기 소집을 받지 않는다")]
        public void AlertChannel_Broadcaster_DoesNotReceiveOwnSummon()
        {
            // 자기 제외가 정책보다 앞선다는 것을 보이려고, 하울러에게 일부러 전역 청력을 붙인다.
            // 자기 제외를 지우면 이 테스트만 실패한다.
            EnemyAlertChannelBehaviour howler = NewChannel("howler", Vector3.zero, true);
            NewChannel("listener", new Vector3(0f, 0f, 10f), false);

            howler.Broadcast(Vector3.zero, 30f);

            Vector3 target;
            Assert.IsFalse(howler.TryConsumeSummon(out target), "하울러 자신은 제외된다");
        }

        // ── 코드 검사 — 기준 3·7 ──────────────────────────────────────

        [Test]
        [Description("TASK-017 5.3-3 - Broadcast 본문에 좌표 뺄셈·sqrMagnitude·거리 비교가 남아 있지 않다")]
        public void AlertChannel_BroadcastBody_ContainsNoDistanceComparison()
        {
            string body = ExtractMethodBody(ReadChannelSource(), "public void Broadcast(");

            string[] banned =
            {
                "sqrMagnitude",
                "magnitude",
                "Distance",
                "radius * radius",
                "- origin",
                "origin -"
            };

            List<string> violations = new List<string>();

            for (int i = 0; i < banned.Length; i++)
            {
                if (body.Contains(banned[i]))
                {
                    violations.Add(banned[i]);
                }
            }

            Assert.IsEmpty(violations, "Broadcast 에 거리 판정이 남아 있다: " + string.Join(" / ", violations));
        }

        [Test]
        [Description("TASK-017 5.3-7 - 채널에 유닛 종류를 가르는 분기가 없다")]
        public void AlertChannel_Source_ContainsNoUnitTypeBranching()
        {
            string source = ReadChannelSource();

            string[] banned =
            {
                "enum ",
                "CompareTag",
                "gameObject.tag",
                "GetType()",
                "GlobalHearingBehaviour",
                "Brutus"
            };

            List<string> violations = new List<string>();

            for (int i = 0; i < banned.Length; i++)
            {
                if (source.Contains(banned[i]))
                {
                    violations.Add(banned[i]);
                }
            }

            Assert.IsEmpty(
                violations,
                "채널이 유닛 종류를 알고 있다: " + string.Join(" / ", violations));
        }

        [Test]
        [Description("TASK-017 5.2 - 전역 청력 부품은 매 조회마다 새 정책을 만들지 않는다")]
        public void GlobalHearingBehaviour_Policy_ReturnsSameInstanceOnRepeatedCalls()
        {
            GameObject host = NewObject("policy-holder");
            GlobalHearingBehaviour behaviour = host.AddComponent<GlobalHearingBehaviour>();

            IEnemyHearingPolicy first = behaviour.Policy;
            IEnemyHearingPolicy second = behaviour.Policy;

            // 하울링마다 읽히는 경로다. getter 에서 new 하면 GC 할당이 쌓인다.
            Assert.AreSame(first, second);
            Assert.IsInstanceOf<GlobalHearingPolicy>(first);
        }

        // ── 도우미 ─────────────────────────────────────────────────

        /// <summary>
        /// 채널을 붙이고 Unity 가 부르는 것과 같은 생명주기 콜백을 돌린다.
        /// <para>
        /// EditMode 에서는 <c>AddComponent</c> 만으로 <c>Awake</c>/<c>OnEnable</c> 이 돌지 않는다 —
        /// 플레이 중이 아닌 에디터에서는 <c>ExecuteAlways</c> 가 붙은 스크립트만 콜백을 받는다.
        /// 그래서 리플렉션으로 직접 부른다. 검증 대상 경로 자체는 실제 런타임과 동일하다.
        /// </para>
        /// </summary>
        private EnemyAlertChannelBehaviour NewChannel(string name, Vector3 position, bool globalHearing)
        {
            GameObject host = NewObject(name);
            host.transform.position = position;

            if (globalHearing)
            {
                // 채널의 Awake 가 GetComponent 로 찾으므로 먼저 붙여야 한다.
                host.AddComponent<GlobalHearingBehaviour>();
            }

            EnemyAlertChannelBehaviour channel = host.AddComponent<EnemyAlertChannelBehaviour>();
            InvokeLifecycle(channel, "Awake");
            InvokeLifecycle(channel, "OnEnable");
            _enabled.Add(channel);

            return channel;
        }

        private static void InvokeLifecycle(EnemyAlertChannelBehaviour channel, string methodName)
        {
            MethodInfo method = typeof(EnemyAlertChannelBehaviour).GetMethod(
                methodName, BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.IsNotNull(method, "생명주기 메서드를 찾지 못했다: " + methodName);

            try
            {
                method.Invoke(channel, null);
            }
            catch (TargetInvocationException exception)
            {
                // 리플렉션 포장을 벗겨 원래 예외를 그대로 드러낸다.
                throw exception.InnerException;
            }
        }

        private static List<EnemyAlertChannelBehaviour> ActiveChannels()
        {
            FieldInfo field = typeof(EnemyAlertChannelBehaviour).GetField(
                "ActiveChannels", BindingFlags.Static | BindingFlags.NonPublic);

            Assert.IsNotNull(field, "정적 채널 목록을 찾지 못했다");

            return (List<EnemyAlertChannelBehaviour>)field.GetValue(null);
        }

        private static void ClearActiveChannels()
        {
            ActiveChannels().Clear();
        }

        private static string ReadChannelSource()
        {
            string[] files = Directory.GetFiles(
                Application.dataPath, "EnemyAlertChannelBehaviour.cs", SearchOption.AllDirectories);

            Assert.AreEqual(1, files.Length, "EnemyAlertChannelBehaviour.cs 를 정확히 하나 찾지 못했다");

            return File.ReadAllText(files[0]);
        }

        /// <summary>서명이 나오는 자리부터 괄호 짝을 세어 메서드 본문만 잘라 낸다.</summary>
        private static string ExtractMethodBody(string source, string signature)
        {
            int start = source.IndexOf(signature);
            Assert.AreNotEqual(-1, start, "메서드를 찾지 못했다: " + signature);

            int open = source.IndexOf('{', start);
            Assert.AreNotEqual(-1, open, "본문 여는 괄호를 찾지 못했다: " + signature);

            int depth = 0;

            for (int i = open; i < source.Length; i++)
            {
                if (source[i] == '{')
                {
                    depth++;
                }
                else if (source[i] == '}')
                {
                    depth--;

                    if (depth == 0)
                    {
                        return source.Substring(open, i - open + 1);
                    }
                }
            }

            Assert.Fail("본문 닫는 괄호를 찾지 못했다: " + signature);
            return string.Empty;
        }

        private GameObject NewObject(string name)
        {
            GameObject created = new GameObject(name);
            _spawned.Add(created);
            return created;
        }
    }
}
