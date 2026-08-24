namespace Pawntom.Enemy.Core
{
    /// <summary>
    /// K-9 순찰견의 상태. GAME_DESIGN.md 3.3 의 4종과 1:1 대응한다.
    /// </summary>
    public enum K9State
    {
        /// <summary>지정 경로를 느리게 순회한다.</summary>
        Patrol = 0,

        /// <summary>단서 좌표로 이동해 주변을 살핀다.</summary>
        Investigate = 1,

        /// <summary>제자리 하울링. 반경 내 동료를 소집한다.</summary>
        Alert = 2,

        /// <summary>시야에 잡힌 대상을 직접 추격한다.</summary>
        Chase = 3
    }
}
