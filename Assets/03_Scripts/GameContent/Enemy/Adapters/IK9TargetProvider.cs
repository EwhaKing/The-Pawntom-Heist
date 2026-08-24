using System.Collections.Generic;
using UnityEngine;

namespace Pawntom.Enemy.Adapters
{
    /// <summary>
    /// 감지 대상(플레이어 등)의 목록을 알려 준다.
    /// <para>
    /// 이 프로젝트는 태그를 쓰지 않고 상황마다 다른 방식으로 플레이어를 찾는다.
    /// 그 방식을 감지 소스가 직접 알지 않도록 여기서 한 겹 끊는다(DIP).
    /// 나중에 네트워크 스폰과 연결할 때도 이 인터페이스의 다른 구현을 넣으면 된다.
    /// </para>
    /// </summary>
    public interface IK9TargetProvider
    {
        /// <summary>
        /// 현재 유효한 감지 대상들. 매 프레임 호출되므로
        /// 구현은 새 목록을 만들지 말고 보관 중인 목록을 그대로 돌려줘야 한다.
        /// </summary>
        IReadOnlyList<Transform> Targets { get; }
    }
}
