using UnityEngine;

/// <summary>
/// 붙여 두면 길 안내 화살표(<see cref="PathGuide"/>)가 이 오브젝트(와 자식)의 콜라이더를 지형으로 보지 않는다.
/// 보이지 않는 벽처럼 "플레이어는 막지만 화살표는 지나가야 하는 것"에 쓴다. 아무 기능도 없는 표시용 컴포넌트.
/// </summary>
[DisallowMultipleComponent]
public sealed class PathGuideIgnore : MonoBehaviour
{
}
