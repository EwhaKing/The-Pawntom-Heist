namespace Pawntom.TutorialBridge
{
    /// <summary>
    /// 튜토리얼 연출 중 게임플레이 입력(이동·시점·버튼)을 막는다. 소유자 여러 명이 동시에 걸 수 있다
    /// (오프닝, 분전반 캐스팅, 정산·엔딩). 마지막 소유자가 풀 때만 입력을 되살린다.
    /// <para>
    /// 실제 잠금은 게임 쪽 <see cref="ScriptedInputLock"/> 이 가진다 — 게임 코드(PlayerData 의 피해 무시 등)가
    /// 튜토리얼 클래스를 직접 보지 않도록 옮겼다. 튜토리얼 코드는 이 이름을 그대로 쓴다.
    /// </para>
    /// <para>
    /// <see cref="GameplayInputBlocker"/>와 다르다 — 그쪽은 UI 조작용으로 커서를 풀지만, 이쪽은 커서를 건드리지 않는다.
    /// </para>
    /// </summary>
    public static class TutorialInputLock
    {
        public static bool IsLocked
        {
            get { return ScriptedInputLock.IsLocked; }
        }

        public static void Acquire(object owner)
        {
            ScriptedInputLock.Acquire(owner);
        }

        public static void Release(object owner)
        {
            ScriptedInputLock.Release(owner);
        }

        /// <summary>다른 코드(데모 진입 등)가 입력을 다시 켰을 수 있으므로, 잠겨 있으면 한 번 더 끈다.</summary>
        public static void Reassert()
        {
            ScriptedInputLock.Reassert();
        }

        /// <summary>씬을 떠날 때. 입력은 되살리지 않는다 — 씬 전환 쪽(GameManager)이 입력 상태를 정한다.</summary>
        public static void ForgetOwner(object owner)
        {
            ScriptedInputLock.ForgetOwner(owner);
        }
    }
}
