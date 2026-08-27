using Pawntom.Enemy.Core;
using UnityEngine;

namespace Pawntom.Enemy.Adapters
{
    /// <summary>
    /// 씬에 붙여서 쓰는 감지 소스의 공통 밑판.
    /// <para>
    /// <b>확장 지점(OCP)</b> — 새 감지 수단(예: 털공 흔적 추적)은
    /// 이 클래스를 상속한 새 파일 하나를 만들어 K-9 오브젝트에 붙이기만 하면 된다.
    /// <c>K9Agent</c> 는 붙어 있는 것을 전부 모아 주입하므로 수정할 필요가 없고,
    /// <c>K9Brain</c> 도 마찬가지다.
    /// </para>
    /// <para>
    /// 수치 설정과 대상 목록은 <see cref="Configure"/> 로 K9Agent 가 넣어 준다.
    /// 자기만의 값이 더 필요하면 상속한 쪽에서 직렬화 필드를 추가하면 된다.
    /// </para>
    /// </summary>
    public abstract class K9PerceptionSourceBehaviour : MonoBehaviour, IK9PerceptionSource
    {
        private K9Settings _settings;
        private IK9TargetProvider _targetProvider;

        /// <summary>K9Agent 가 넣어 준 수치 설정. 주입 전에는 null 일 수 있다.</summary>
        protected K9Settings Settings
        {
            get { return _settings; }
        }

        /// <summary>K9Agent 가 넣어 준 대상 제공자. 주입 전에는 null 일 수 있다.</summary>
        protected IK9TargetProvider TargetProvider
        {
            get { return _targetProvider; }
        }

        /// <summary>K9Agent 가 조립 시점에 한 번 호출한다.</summary>
        public void Configure(K9Settings settings, IK9TargetProvider targetProvider)
        {
            _settings = settings;
            _targetProvider = targetProvider;
            OnConfigured();
        }

        /// <summary>주입이 끝난 뒤 필요한 준비가 있으면 여기서 한다.</summary>
        protected virtual void OnConfigured()
        {
        }

        /// <inheritdoc/>
        public abstract bool TryDetect(out K9Detection detection);

        /// <summary>주입이 끝나 감지를 시도할 수 있는 상태인가.</summary>
        protected bool IsReady
        {
            get { return _settings != null && _targetProvider != null; }
        }
    }
}
