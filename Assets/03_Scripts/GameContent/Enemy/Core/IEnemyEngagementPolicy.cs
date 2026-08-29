namespace Pawntom.Enemy.Core
{
    /// <summary>
    /// 위협을 감지했을 때 어느 상태로 넘어갈지 정한다.
    /// <para>
    /// 유닛마다 다른 것은 이 판단 하나뿐이다 — 순찰·흔적 추적·배회·추격 유지는 모두 같다.
    /// 새 유닛은 이 인터페이스를 구현해 주입하면 되고 <c>EnemyBrain</c> 은 바뀌지 않는다(OCP).
    /// </para>
    /// </summary>
    public interface IEnemyEngagementPolicy
    {
        /// <summary>접촉·초근접을 감지했을 때 진입할 상태.</summary>
        /// <param name="current">지금 상태. 같은 감지라도 어디서 왔는지에 따라 달라질 수 있다.</param>
        EnemyState OnContact(EnemyState current);

        /// <summary>시야에 대상을 확인했을 때 진입할 상태.</summary>
        /// <param name="current">지금 상태.</param>
        /// <param name="howledRecently">하울링 쿨다운 안에 있는가.</param>
        EnemyState OnSighted(EnemyState current, bool howledRecently);
    }
}
