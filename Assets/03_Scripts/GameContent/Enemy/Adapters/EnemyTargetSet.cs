using System.Collections.Generic;
using UnityEngine;

namespace Pawntom.Enemy.Adapters
{
    /// <summary>
    /// 감지 대상 집합. 인스펙터 고정 대상과 런타임 등록 대상을 한 목록으로 합쳐 들고 있는다.
    /// <para>
    /// <b>MonoBehaviour 가 아니다.</b> 제공자(<see cref="SceneTargetProvider"/>)가 들고 쓰는
    /// 순수 클래스라 EditMode 테스트에서 단독으로 검증할 수 있다.
    /// </para>
    /// </summary>
    public sealed class EnemyTargetSet
    {
        private readonly List<Transform> _targets;

        public EnemyTargetSet() : this(8)
        {
        }

        public EnemyTargetSet(int capacity)
        {
            _targets = new List<Transform>(capacity);
        }

        /// <summary>
        /// 현재 유효한 대상들.
        /// <para>
        /// 호출부가 매 틱 읽으므로(<c>ContactPerceptionSource.TryDetect</c>)
        /// <b>보관 중인 목록을 그대로 돌려준다.</b> 새 목록을 만들면 GC 할당이 매 프레임 쌓인다.
        /// </para>
        /// </summary>
        public IReadOnlyList<Transform> Targets
        {
            get { return _targets; }
        }

        /// <summary>없으면 추가한다. 이미 있거나 null 이면 아무것도 하지 않고 false 를 돌려준다.</summary>
        public bool Add(Transform target)
        {
            if (target == null || Contains(target))
            {
                return false;
            }

            _targets.Add(target);
            return true;
        }

        /// <summary>있으면 제거한다. 제거했으면 true.</summary>
        public bool Remove(Transform target)
        {
            if (target == null)
            {
                return false;
            }

            for (int i = 0; i < _targets.Count; i++)
            {
                if (ReferenceEquals(_targets[i], target))
                {
                    _targets.RemoveAt(i);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 파괴된 대상을 목록에서 걷어낸다.
        /// <para>
        /// Unity 의 가짜 null(파괴된 오브젝트) 도 걸러내야 하므로 <c>== null</c> 비교를 쓴다.
        /// 뒤에서 앞으로 돌아 <c>RemoveAt</c> 이 남은 인덱스를 흔들지 않게 한다.
        /// </para>
        /// </summary>
        public void Prune()
        {
            for (int i = _targets.Count - 1; i >= 0; i--)
            {
                if (_targets[i] == null)
                {
                    _targets.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// 같은 인스턴스가 이미 들어 있는지 본다.
        /// <para>
        /// <c>List.Contains</c> 는 <c>UnityEngine.Object</c> 의 <c>==</c> 오버로드를 타지 않고
        /// <c>Equals</c> 를 쓰므로, 의도를 분명히 하려고 참조 비교로 직접 돈다.
        /// </para>
        /// </summary>
        private bool Contains(Transform target)
        {
            for (int i = 0; i < _targets.Count; i++)
            {
                if (ReferenceEquals(_targets[i], target))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
