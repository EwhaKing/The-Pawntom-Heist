using UnityEngine;

/// <summary>
/// 표식 컴포넌트. 이것이 붙은 캔버스는 사망 연출(PlayerDeathPresentation)이 흑백 후처리 아래로 옮기지 않는다 —
/// 실패 문구처럼 흑백 화면 위에 원래 색으로 보여야 하는 UI 에 붙인다(지금은 튜토리얼 실패 화면).
/// </summary>
[DisallowMultipleComponent]
public sealed class DeathOverlayExempt : MonoBehaviour
{
}
