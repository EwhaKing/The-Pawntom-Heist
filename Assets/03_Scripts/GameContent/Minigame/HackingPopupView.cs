using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Hacking
{
    /// <summary>
    /// 화면을 채우는 팝업의 껍데기 역할.
    /// HackingManager가 생성한 미니게임 인스턴스를 gameContainer에 붙이고,
    /// 타이머 UI를 매 프레임 갱신하다가 결과가 나오면 스스로를 파괴합니다.
    ///
    /// 지금 단계에서는 Canvas 세부 설정(Screen Space 종류, Sort Order, Dim 배경 등)은
    /// 신경 쓰지 않고 "화면 꽉 채워서 뜨는지"만 확인하면 됩니다. 그 부분은 나중 몫입니다.
    /// </summary>
    public class HackingPopupView : MonoBehaviour
    {
        [Header("UI 요소")]
        [SerializeField] private TextMeshProUGUI timerText;
        [SerializeField] private Slider timerSlider;
        [SerializeField] private Slider gaugeSlider; // 게이지형 미니게임(SignalDivide 등)의 진행도 표시용. 없어도 동작함
        [SerializeField] private TextMeshProUGUI sequenceText; // 시퀀스형 미니게임(CommandOverride 등)의 진행 표시용. 없어도 동작함
        [SerializeField] private Transform gameContainer;

        // [추후 연결 예정]
        // [SerializeField] private Image dimBackground; // 3D 배경이 비치는 반투명 딤 처리

        private HackingGameBase currentGame;
        private System.Action<bool> onResultCallback;

        /// <summary>
        /// HackingManager가 팝업을 띄우면서 미니게임 인스턴스와 결과 콜백을 넘겨줍니다.
        /// </summary>
        public void InitializePopup(HackingGameBase gameInstance, System.Action<bool> resultCallback)
        {
            currentGame = gameInstance;
            onResultCallback = resultCallback;

            currentGame.transform.SetParent(gameContainer, false);
            currentGame.OnGameEnded += HandleGameEnded;
        }

        private void Update()
        {
            if (currentGame == null || !currentGame.IsActive) return;
            UpdateTimerUI(currentGame.CurrentTime, currentGame.TimeLimit);

            if (gaugeSlider != null)
            {
                gaugeSlider.value = currentGame.Progress;
            }

            if (sequenceText != null)
            {
                sequenceText.text = currentGame.DisplayText;
            }
        }

        private void UpdateTimerUI(float currentTime, float maxTime)
        {
            if (timerText != null)
            {
                timerText.text = Mathf.CeilToInt(currentTime).ToString();
            }

            if (timerSlider != null)
            {
                timerSlider.value = maxTime > 0f ? currentTime / maxTime : 0f;
            }
        }

        private void HandleGameEnded(bool isSuccess)
        {
            currentGame.OnGameEnded -= HandleGameEnded;
            onResultCallback?.Invoke(isSuccess);
            ClosePopup();
        }

        public void ClosePopup()
        {
            Destroy(gameObject);
        }

        // <summary>
        /// UI 버튼에서 위 방향 입력을 전달
        /// </summary>
        public void PressUp()
        {
            SendVirtualInput(KeyCode.UpArrow);
        }

        /// <summary>
        /// UI 버튼에서 아래 방향 입력을 전달
        /// </summary>
        public void PressDown()
        {
            SendVirtualInput(KeyCode.DownArrow);
        }

        /// <summary>
        /// UI 버튼에서 왼쪽 방향 입력을 전달
        /// </summary>
        public void PressLeft()
        {
            SendVirtualInput(KeyCode.LeftArrow);
        }

        /// <summary>
        /// UI 버튼에서 오른쪽 방향 입력을 전달
        /// </summary>
        public void PressRight()
        {
            SendVirtualInput(KeyCode.RightArrow);
        }

        /// <summary>
        /// UI 버튼에서 A 입력을 전달
        /// </summary>
        public void PressA()
        {
            SendVirtualInput(KeyCode.A);
        }

        /// <summary>
        /// UI 버튼에서 D 입력을 전달
        /// </summary>
        public void PressD()
        {
            SendVirtualInput(KeyCode.D);
        }

        /// <summary>
        /// UI 버튼 입력을 현재 실행 중인 미니게임에 전달
        /// </summary>
        private void SendVirtualInput(KeyCode key)
        {
            if (currentGame == null)
            {
                Debug.LogWarning("[HackingPopupView] 현재 실행 중인 미니게임이 없습니다.");
                return;
            }

            currentGame.ReceiveVirtualInput(key);
        }
    }
}