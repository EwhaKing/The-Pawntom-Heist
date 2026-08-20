using UnityEngine;

namespace Hacking
{
    /// <summary>
    /// 모든 해킹 미니게임의 부모 클래스.
    /// 제한 시간 타이머와 종료 이벤트(OnGameEnded)를 공통으로 처리하고,
    /// 실제 입력 판정 로직(HandleInput)은 자식 클래스가 구현합니다.
    /// </summary>
    public abstract class HackingGameBase : MonoBehaviour
    {
        [Header("공통 설정")]
        [SerializeField] protected float timeLimit = 5f;

        protected float currentTime;
        protected bool isActive;

        /// <summary>HackingPopupView가 타이머 UI를 갱신할 때 참조합니다.</summary>
        public float CurrentTime => currentTime;
        public float TimeLimit => timeLimit;
        public bool IsActive => isActive;

        /// <summary>
        /// 0~1 사이의 진행도. 게이지형 미니게임(SignalDivide 등)만 override해서 의미있는 값을 반환합니다.
        /// 시퀀스형처럼 게이지 개념이 없는 게임은 기본값 0을 그대로 써도 됩니다.
        /// </summary>
        public virtual float Progress => 0f;

        /// <summary>
        /// 팝업 화면에 표시할 텍스트. 시퀀스형(CommandOverride 등)이 진행 상황을 보여줄 때 override합니다.
        /// 필요 없는 게임은 기본값 빈 문자열을 그대로 써도 됩니다.
        /// </summary>
        public virtual string DisplayText => string.Empty;

        /// <summary>
        /// UI 버튼에서 들어온 가상 입력을 처리합니다.
        /// 기본적으로는 아무 동작도 하지 않고,
        /// 필요한 미니게임에서 override해서 사용합니다.
        /// </summary>
        public virtual void ReceiveVirtualInput(KeyCode key)
        {
        }

        /// <summary>게임이 끝나면(성공/실패 무관) 호출되는 콜백. 파라미터는 성공 여부.</summary>
        public System.Action<bool> OnGameEnded;

        /// <summary>
        /// 매니저가 프리팹을 생성한 직후 호출합니다.
        /// 자식 클래스에서 override할 때는 반드시 base.InitGame(level)을 먼저 호출하세요.
        /// </summary>
        public virtual void InitGame(SecurityLevel level)
        {
            currentTime = timeLimit;
            isActive = true;
        }

        protected virtual void Update()
        {
            if (!isActive) return;

            UpdateTimer();
            if (!isActive) return; // 타이머 종료로 이미 FinishGame이 호출됐을 수 있음

            HandleInput();
        }

        /// <summary>
        /// 매 프레임 남은 시간을 갱신하고 0 이하가 되면 실패 처리합니다.
        /// 자식 클래스가 게이지 감소 등 추가 로직이 필요하면 override 후 base.UpdateTimer() 호출.
        /// </summary>
        protected virtual void UpdateTimer()
        {
            currentTime -= Time.deltaTime;
            // [추후 연결 예정] Time.timeScale = 0으로 배경을 멈추게 되면
            // 위 줄을 Time.unscaledDeltaTime으로 교체해야 타이머가 같이 멈추지 않습니다.

            if (currentTime <= 0f)
            {
                currentTime = 0f;
                FinishGame(false);
            }
        }

        /// <summary>각 미니게임 고유의 입력 판정 로직. 자식 클래스에서 반드시 구현.</summary>
        protected abstract void HandleInput();

        /// <summary>
        /// 게임 종료 처리. 중복 호출을 막기 위해 isActive를 먼저 검사합니다.
        /// </summary>
        protected virtual void FinishGame(bool isSuccess)
        {
            if (!isActive) return;
            isActive = false;
            OnGameEnded?.Invoke(isSuccess);
        }
    }
}