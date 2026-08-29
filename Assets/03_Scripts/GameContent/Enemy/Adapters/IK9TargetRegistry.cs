using UnityEngine;

namespace Pawntom.Enemy.Adapters
{
    /// <summary>
    /// 감지 대상을 등록·해제하는 이음매.
    /// <para>
    /// 읽기(<see cref="IK9TargetProvider"/>)와 쓰기를 분리한다 —
    /// 감지 소스는 읽기만, 등록자는 쓰기만 필요하다(ISP).
    /// </para>
    /// <para>
    /// 등록하는 쪽은 구체 제공자를 모른다. 제공자는 누가 등록하는지 모르고
    /// <see cref="Transform"/> 만 받는다. 새 감지 대상은 등록 컴포넌트를 붙이기만 하면
    /// 되므로 기존 코드를 고치지 않는다(OCP).
    /// </para>
    /// </summary>
    public interface IK9TargetRegistry
    {
        /// <summary>대상을 감지 목록에 넣는다. null 이거나 이미 있으면 아무 일도 없다.</summary>
        void Register(Transform target);

        /// <summary>대상을 감지 목록에서 뺀다. null 이거나 없으면 아무 일도 없다.</summary>
        void Unregister(Transform target);
    }
}
