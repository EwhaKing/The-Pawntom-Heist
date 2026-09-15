namespace Hacking
{
    /// <summary>
    /// 해킹 대상(문/금고)의 보안 등급.
    /// 등급에 따라 미니게임 풀(Pool)에서 필터링되는 기준이 됩니다.
    /// </summary>
    public enum SecurityLevel
    {
        Normal,      // 일반 문 (시퀀스 길이 4 등)
        VaultFinal   // 최종 금고 (시퀀스 길이 8 등, 고난이도)
    }

    /// <summary>
    /// 미니게임 종류 구분용. HackingGameData에서 프리팹과 짝지어 관리합니다.
    /// </summary>
    public enum HackingGameType
    {
        CommandOverride,   // A. 시퀀스 방향키 입력
        SignalDivide,       // B. A/D 고속 교차 연타
        DigitalSync  // C. 스페이스바 타이밍 적중
    }
}