using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Pawntom.Enemy.Adapters;
using UnityEngine;

namespace Pawntom.Enemy.Tests
{
    /// <summary>
    /// TASK-014 5.4 — 대상 집합(<see cref="K9TargetSet"/>)과
    /// 등록자(<see cref="K9TargetRegistrar"/>)의 대기 드레인 검증.
    /// <para>
    /// (나) 묶음은 1차 스펙 반려 사유였던 실패 경로다 —
    /// 대기 목록 드레인이 빠지면 "등록자 먼저 → 창구 나중" 테스트가 반드시 실패한다.
    /// </para>
    /// </summary>
    public sealed class K9TargetSetTests
    {
        /// <summary>
        /// <see cref="IK9TargetRegistry"/> 의 가짜 구현. 받은 것을 담기만 한다.
        /// 등록자가 구체 제공자를 모른다는 사실 자체가 이 가짜를 끼울 수 있는 근거다.
        /// </summary>
        private sealed class FakeRegistry : IK9TargetRegistry
        {
            public readonly List<Transform> Registered = new List<Transform>(4);
            public readonly List<Transform> Unregistered = new List<Transform>(4);

            public void Register(Transform target)
            {
                Registered.Add(target);
            }

            public void Unregister(Transform target)
            {
                Unregistered.Add(target);
            }
        }

        private readonly List<GameObject> _spawned = new List<GameObject>(4);

        [SetUp]
        public void SetUp()
        {
            _spawned.Clear();
            ClearStaticState();

            Assert.IsNull(K9TargetRegistrar.Registry, "사전 조건: 창구가 비어 있다");
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _spawned.Count; i++)
            {
                GameObject spawned = _spawned[i];
                if (spawned != null)
                {
                    Object.DestroyImmediate(spawned);
                }
            }

            _spawned.Clear();
            ClearStaticState();
        }

        // ── (가) K9TargetSet ────────────────────────────────────────

        [Test]
        [Description("TASK-014 5.3-1 - 같은 Transform 을 두 번 넣어도 목록에는 하나만 남는다")]
        public void TargetSet_AddSameTargetTwice_KeepsSingleEntry()
        {
            K9TargetSet set = new K9TargetSet();
            Transform target = NewTransform("target");

            Assert.IsTrue(set.Add(target), "첫 등록은 성공한다");
            Assert.IsFalse(set.Add(target), "두 번째 등록은 거부된다");

            Assert.AreEqual(1, set.Targets.Count);
            Assert.AreSame(target, set.Targets[0]);
        }

        [Test]
        [Description("TASK-014 5.3-2 - Add(null) 은 목록을 바꾸지 않고 false 를 돌려준다")]
        public void TargetSet_AddNull_LeavesListUnchanged_AndReturnsFalse()
        {
            K9TargetSet set = new K9TargetSet();
            Transform target = NewTransform("target");
            set.Add(target);

            Assert.IsFalse(set.Add(null), "null 은 대상이 아니다");

            Assert.AreEqual(1, set.Targets.Count, "목록이 바뀌지 않았다");
            Assert.AreSame(target, set.Targets[0]);
        }

        [Test]
        [Description("TASK-014 5.3-3 - Targets 는 매 호출마다 새 목록을 만들지 않는다")]
        public void TargetSet_Targets_ReturnsSameInstance_OnRepeatedCalls()
        {
            K9TargetSet set = new K9TargetSet();

            IReadOnlyList<Transform> first = set.Targets;
            IReadOnlyList<Transform> second = set.Targets;

            // 매 틱 읽히는 경로다. 여기서 새 목록을 만들면 GC 할당이 매 프레임 쌓인다.
            Assert.AreSame(first, second, "빈 상태에서 같은 인스턴스다");

            set.Add(NewTransform("target"));
            Assert.AreSame(first, set.Targets, "내용이 바뀌어도 같은 인스턴스다");
        }

        [Test]
        [Description("TASK-014 5.3-4 - Prune 은 파괴된 대상만 걷어내고 나머지는 순서대로 남긴다")]
        public void TargetSet_Prune_RemovesDestroyedTargets_AndKeepsLivingOnesInOrder()
        {
            K9TargetSet set = new K9TargetSet();
            Transform alive1 = NewTransform("alive1");
            Transform dead = NewTransform("dead");
            Transform alive2 = NewTransform("alive2");

            set.Add(alive1);
            set.Add(dead);
            set.Add(alive2);
            Assert.AreEqual(3, set.Targets.Count, "사전 조건: 셋이 들어 있다");

            Object.DestroyImmediate(dead.gameObject);

            set.Prune();

            Assert.AreEqual(2, set.Targets.Count, "파괴된 하나만 빠진다");
            Assert.AreSame(alive1, set.Targets[0], "순서가 유지된다");
            Assert.AreSame(alive2, set.Targets[1], "순서가 유지된다");
        }

        [Test]
        [Description("TASK-014 5.2 - Remove 는 지정한 대상만 빼고 나머지 순서를 유지한다")]
        public void TargetSet_Remove_DropsOnlyGivenTarget()
        {
            K9TargetSet set = new K9TargetSet();
            Transform first = NewTransform("first");
            Transform second = NewTransform("second");
            set.Add(first);
            set.Add(second);

            Assert.IsTrue(set.Remove(first));
            Assert.IsFalse(set.Remove(first), "이미 빠진 대상은 다시 빠지지 않는다");

            Assert.AreEqual(1, set.Targets.Count);
            Assert.AreSame(second, set.Targets[0]);
        }

        // ── (나) K9TargetRegistrar 대기 드레인 ──────────────────────

        [Test]
        [Description("TASK-014 5.3-5 - 창구가 꽂히기 전에 활성화된 등록자도 누락되지 않는다")]
        public void TargetRegistrar_EnabledBeforeAttach_IsDrainedIntoRegistry()
        {
            // ① 등록자 먼저 활성화.
            GameObject host = NewObject("registrar-first");
            EnableRegistrar(host);
            Assert.IsNull(K9TargetRegistrar.Registry, "사전 조건: 아직 창구가 없다");

            // ② 창구를 나중에 꽂는다.
            FakeRegistry fake = new FakeRegistry();
            K9TargetRegistrar.AttachRegistry(fake);

            // ③ 대기분이 그대로 넘어와야 한다. 드레인이 빠지면 여기서 0 건이 된다.
            Assert.AreEqual(1, fake.Registered.Count, "대기 중이던 등록분이 넘어온다");
            Assert.AreSame(host.transform, fake.Registered[0]);
        }

        [Test]
        [Description("TASK-014 5.3-5 - 창구가 먼저 꽂혀 있으면 등록자는 활성화 즉시 등록된다")]
        public void TargetRegistrar_EnabledAfterAttach_RegistersImmediately()
        {
            FakeRegistry fake = new FakeRegistry();
            K9TargetRegistrar.AttachRegistry(fake);

            GameObject host = NewObject("registrar-second");
            EnableRegistrar(host);

            Assert.AreEqual(1, fake.Registered.Count, "활성화 시점에 바로 등록된다");
            Assert.AreSame(host.transform, fake.Registered[0]);
        }

        [Test]
        [Description("TASK-014 5.2 - 드레인된 대기분은 다음 창구에 다시 넘어가지 않는다")]
        public void TargetRegistrar_AttachTwice_DoesNotReplayDrainedPending()
        {
            GameObject host = NewObject("registrar-drained");
            EnableRegistrar(host);

            FakeRegistry first = new FakeRegistry();
            K9TargetRegistrar.AttachRegistry(first);
            Assert.AreEqual(1, first.Registered.Count, "사전 조건: 첫 창구가 대기분을 받았다");

            FakeRegistry second = new FakeRegistry();
            K9TargetRegistrar.AttachRegistry(second);

            Assert.AreEqual(0, second.Registered.Count, "대기 목록은 드레인 후 비어 있다");
        }

        [Test]
        [Description("TASK-014 5.2-3 - 비활성화되면 꽂혀 있는 창구에서 해제된다")]
        public void TargetRegistrar_Disabled_UnregistersFromRegistry()
        {
            FakeRegistry fake = new FakeRegistry();
            K9TargetRegistrar.AttachRegistry(fake);

            GameObject host = NewObject("registrar-disable");
            K9TargetRegistrar registrar = EnableRegistrar(host);

            DisableRegistrar(registrar);

            Assert.AreEqual(1, fake.Unregistered.Count, "해제가 한 번 일어난다");
            Assert.AreSame(host.transform, fake.Unregistered[0]);
        }

        [Test]
        [Description("TASK-014 5.2-3 - 창구가 없는 상태에서 비활성화돼도 예외가 나지 않는다")]
        public void TargetRegistrar_DisabledWithoutRegistry_DoesNotThrow()
        {
            GameObject host = NewObject("registrar-no-registry");
            K9TargetRegistrar registrar = EnableRegistrar(host);
            Assert.IsNull(K9TargetRegistrar.Registry, "사전 조건: 창구가 없다");

            // 씬 언로드 시 제공자 OnDestroy 가 먼저 도는 경로. 가드가 없으면 NRE 가 난다.
            Assert.DoesNotThrow(delegate { DisableRegistrar(registrar); });

            // 대기 목록에서도 빠졌어야 한다 — 뒤늦게 꽂힌 창구가 유령을 받으면 안 된다.
            FakeRegistry fake = new FakeRegistry();
            K9TargetRegistrar.AttachRegistry(fake);
            Assert.AreEqual(0, fake.Registered.Count, "비활성화된 등록자는 대기 목록에 없다");
        }

        [Test]
        [Description("TASK-014 5.2-5 - 꽂아 둔 당사자가 아니면 창구를 뺄 수 없다")]
        public void TargetRegistrar_DetachRegistry_WithForeignRegistry_KeepsCurrent()
        {
            FakeRegistry owner = new FakeRegistry();
            FakeRegistry stranger = new FakeRegistry();

            K9TargetRegistrar.AttachRegistry(owner);
            K9TargetRegistrar.DetachRegistry(stranger);

            Assert.AreSame(owner, K9TargetRegistrar.Registry, "남의 창구는 뺄 수 없다");

            K9TargetRegistrar.DetachRegistry(owner);
            Assert.IsNull(K9TargetRegistrar.Registry, "당사자는 뺄 수 있다");
        }

        [Test]
        [Description("TASK-014 5.2-5 - AttachRegistry(null) 은 현재 창구를 밀어내지 않는다")]
        public void TargetRegistrar_AttachNull_KeepsCurrentRegistry()
        {
            FakeRegistry owner = new FakeRegistry();
            K9TargetRegistrar.AttachRegistry(owner);

            K9TargetRegistrar.AttachRegistry(null);

            Assert.AreSame(owner, K9TargetRegistrar.Registry, "null 은 아무것도 하지 않는다");
        }

        [Test]
        [Description("TASK-014 5.3-5 - Registry 의 setter 는 외부에 열려 있지 않다")]
        public void TargetRegistrar_RegistrySetter_IsNotPubliclyAssignable()
        {
            PropertyInfo property = typeof(K9TargetRegistrar).GetProperty(
                "Registry", BindingFlags.Static | BindingFlags.Public);

            Assert.IsNotNull(property, "Registry 프로퍼티를 찾지 못했다");
            Assert.IsNull(
                property.GetSetMethod(false),
                "public setter 가 열려 있다 - 제공자가 드레인을 건너뛰고 대입만 할 수 있게 된다");
        }

        [Test]
        [Description("TASK-014 5.3-5 - SceneTargetProvider 가 등록 이음매를 구현한다")]
        public void SceneTargetProvider_ImplementsTargetRegistry()
        {
            Assert.IsTrue(
                typeof(IK9TargetRegistry).IsAssignableFrom(typeof(SceneTargetProvider)),
                "제공자가 IK9TargetRegistry 를 구현하지 않는다");
        }

        // ── 도우미 ─────────────────────────────────────────────────

        /// <summary>
        /// 등록자를 붙이고 활성화 콜백을 돌린다.
        /// <para>
        /// EditMode 에서는 <c>AddComponent</c> 만으로 <c>OnEnable</c> 이 돌지 않는다 —
        /// 플레이 중이 아닌 에디터에서는 <c>ExecuteAlways</c> 가 붙은 스크립트만
        /// 생명주기 콜백을 받는다. 그래서 Unity 가 부르는 것과 같은 메서드를
        /// 리플렉션으로 직접 부른다. 검증 대상 경로 자체는 실제 런타임과 동일하다.
        /// </para>
        /// </summary>
        private static K9TargetRegistrar EnableRegistrar(GameObject host)
        {
            K9TargetRegistrar registrar = host.AddComponent<K9TargetRegistrar>();
            InvokeLifecycle(registrar, "OnEnable");
            return registrar;
        }

        private static void DisableRegistrar(K9TargetRegistrar registrar)
        {
            InvokeLifecycle(registrar, "OnDisable");
        }

        private static void InvokeLifecycle(K9TargetRegistrar registrar, string methodName)
        {
            MethodInfo method = typeof(K9TargetRegistrar).GetMethod(
                methodName, BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.IsNotNull(method, "생명주기 메서드를 찾지 못했다: " + methodName);

            try
            {
                method.Invoke(registrar, null);
            }
            catch (TargetInvocationException exception)
            {
                // 리플렉션 포장을 벗겨 원래 예외를 그대로 드러낸다.
                throw exception.InnerException;
            }
        }

        /// <summary>
        /// 창구와 대기 목록을 함께 비운다.
        /// 대기 목록은 private 이라, 버리는 창구를 한 번 꽂아 드레인을 유발한 뒤 뗀다.
        /// </summary>
        private static void ClearStaticState()
        {
            K9TargetRegistrar.DetachRegistry(K9TargetRegistrar.Registry);

            FakeRegistry drain = new FakeRegistry();
            K9TargetRegistrar.AttachRegistry(drain);
            K9TargetRegistrar.DetachRegistry(drain);
        }

        private GameObject NewObject(string name)
        {
            GameObject created = new GameObject(name);
            _spawned.Add(created);
            return created;
        }

        private Transform NewTransform(string name)
        {
            return NewObject(name).transform;
        }
    }
}
