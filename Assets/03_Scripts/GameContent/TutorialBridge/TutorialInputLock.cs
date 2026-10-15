using System.Collections.Generic;
using UnityEngine;

namespace Pawntom.TutorialBridge
{
    /// <summary>
    /// 튜토리얼 연출 중 게임플레이 입력(이동·시점·버튼)을 막는다. 소유자 여러 명이 동시에 걸 수 있다
    /// (오프닝, 분전반 캐스팅, 정산·엔딩). 마지막 소유자가 풀 때만 입력을 되살린다.
    /// <para>
    /// <see cref="GameplayInputBlocker"/>와 다르다 — 그쪽은 UI 조작용으로 커서를 풀지만, 이쪽은 커서를 건드리지 않는다.
    /// </para>
    /// </summary>
    public static class TutorialInputLock
    {
        private static readonly HashSet<object> Owners = new HashSet<object>();

        public static bool IsLocked
        {
            get { return Owners.Count > 0; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Owners.Clear();
        }

        public static void Acquire(object owner)
        {
            if (owner == null || !Owners.Add(owner))
            {
                return;
            }

            InputManager.Instance.DisableGameplayInput();
        }

        public static void Release(object owner)
        {
            if (owner == null || !Owners.Remove(owner) || Owners.Count > 0)
            {
                return;
            }

            InputManager.Instance.EnableGameplayInput();
        }

        /// <summary>
        /// 다른 코드(데모 진입 등)가 입력을 다시 켰을 수 있으므로, 잠겨 있으면 한 번 더 끈다.
        /// </summary>
        public static void Reassert()
        {
            if (IsLocked)
            {
                InputManager.Instance.DisableGameplayInput();
            }
        }

        /// <summary>
        /// 씬을 떠날 때. 입력은 되살리지 않는다 — 씬 전환 쪽(GameManager)이 입력 상태를 정한다.
        /// </summary>
        public static void ForgetOwner(object owner)
        {
            if (owner != null)
            {
                Owners.Remove(owner);
            }
        }
    }
}
