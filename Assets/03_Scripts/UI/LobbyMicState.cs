/// <summary>
/// 로비 슬롯 마이크 아이콘의 표시 상태. 음성 기능이 붙기 전에는 항상 Idle입니다.
/// </summary>
public enum LobbyMicState
{
    Idle,     // 평시 (회색)
    Speaking, // 말하는 중 (초록)
    Muted     // 차단 (빨강 + 취소선)
}
