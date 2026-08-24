namespace Pawntom.Enemy.Core
{
    /// <summary>
    /// 감지 소스 하나.
    /// <para>
    /// 확장 지점(OCP): 새 감지 수단은 이 인터페이스를 구현해 <c>K9Brain</c> 에 주입하기만 하면 된다.
    /// <c>K9Brain</c> 과 기존 소스는 한 줄도 바뀌지 않는다.
    /// </para>
    /// </summary>
    public interface IK9PerceptionSource
    {
        /// <summary>이번 틱에 단서를 찾았으면 true 와 함께 단서를 내보낸다.</summary>
        bool TryDetect(out K9Detection detection);
    }
}
