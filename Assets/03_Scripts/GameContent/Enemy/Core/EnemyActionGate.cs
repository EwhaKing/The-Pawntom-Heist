namespace Pawntom.Enemy.Core
{
    /// <summary>
    /// 적이 지금 물 수 있는가 / 멈춰 서 있어야 하는가. 피해 판정(PlayerData)·물기 연출(EnemyAnimationView)·
    /// 이동 정지(NetworkEnemyAgent)가 같은 규칙을 쓰도록 한곳에 모은다.
    /// </summary>
    public static class EnemyActionGate
    {
        /// <summary>
        /// 물 수 있는가: 추격 중이고 스턴이 아닐 때만. 순찰 중 스친 접촉은 감지(경계→추격)로 이어질 뿐 물지 않고,
        /// 한 번 문 뒤 스턴(기본 2초) 동안은 닿아 있어도 물지 않는다 — 피해도, 물기 동작도.
        /// </summary>
        public static bool CanBite(EnemyState state, bool stunned)
        {
            return state == EnemyState.Chase && !stunned;
        }

        /// <summary>
        /// 이동을 세워 둬야 하는가: 월드 일시정지(대사·연출) 중이거나 스턴 중이면.
        /// 일시정지가 풀려도 스턴이 남아 있으면 계속 서 있어야 한다(두뇌는 멈춰 있는데 몸만 예전 목적지로 걸어가지 않게).
        /// </summary>
        public static bool ShouldHoldMotion(bool worldPaused, bool stunned)
        {
            return worldPaused || stunned;
        }
    }
}
