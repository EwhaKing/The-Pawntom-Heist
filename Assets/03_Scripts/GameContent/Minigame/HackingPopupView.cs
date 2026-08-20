using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Hacking
{
    /// <summary>
    /// HackingPopupView
    ///
    /// 담당:
    /// - 해킹 미니게임 팝업 UI 표시
    /// - 타이머 UI 갱신
    /// - SignalDivide 게이지 UI 갱신
    /// - CommandOverride의 8개 슬롯 UI 갱신
    /// - 현재 미니게임 종류에 따라 필요한 UI만 표시
    /// </summary>
    public class HackingPopupView : MonoBehaviour
    {
        [Header("Text UI")]
        [SerializeField] private TextMeshProUGUI guideText;
        [SerializeField] private TextMeshProUGUI timerText;

        [Header("Slider UI")]
        [SerializeField] private Slider timerSlider;
        [SerializeField] private Slider gaugeSlider;

        [Header("Command Override UI")]
        [SerializeField] private GameObject commandSequenceGroup;
        [SerializeField] private CommandSequenceSlotUI[] commandSlots;

        [Header("Signal Divide UI")]
        [SerializeField] private GameObject signalButtonGroup;

        [Header("Container")]
        [SerializeField] private Transform gameContainer;

        private HackingGameBase currentGame;
        private System.Action<bool> onResultCallback;

        /// <summary>
        /// HackingManager가 팝업을 띄우면서 미니게임 인스턴스와 결과 콜백을 넘겨줍니다.
        /// </summary>
        public void InitializePopup(HackingGameBase gameInstance, System.Action<bool> resultCallback)
        {
            currentGame = gameInstance;
            onResultCallback = resultCallback;

            if (currentGame == null)
            {
                Debug.LogError("[HackingPopupView] currentGame이 null입니다.");
                return;
            }

            if (gameContainer != null)
            {
                currentGame.transform.SetParent(gameContainer, false);
            }

            currentGame.OnGameEnded += HandleGameEnded;

            InitializeCommandSlots();
            SetupUIByGameType();
        }

        private void Update()
        {
            if (currentGame == null || !currentGame.IsActive)
            {
                return;
            }

            UpdateTimerUI(currentGame.CurrentTime, currentGame.TimeLimit);
            UpdateGaugeUI();
            UpdateCommandSequenceUI();
        }

        /// <summary>
        /// CommandSlot_0~7을 초기화합니다.
        /// 각 슬롯이 자기 인덱스를 알고 HackingPopupView에 클릭을 전달하게 합니다.
        /// </summary>
        private void InitializeCommandSlots()
        {
            if (commandSlots == null)
            {
                return;
            }

            for (int i = 0; i < commandSlots.Length; i++)
            {
                if (commandSlots[i] == null)
                {
                    continue;
                }

                commandSlots[i].Initialize(this, i);
            }
        }

        /// <summary>
        /// 현재 실행 중인 미니게임 종류에 따라 필요한 UI만 보여줍니다.
        /// </summary>
        private void SetupUIByGameType()
        {
            bool isCommandGame = currentGame is CommandOverrideGame;
            bool isSignalGame = currentGame is SignalDivideGame;

            if (commandSequenceGroup != null)
            {
                commandSequenceGroup.SetActive(isCommandGame);
            }

            if (signalButtonGroup != null)
            {
                signalButtonGroup.SetActive(isSignalGame);
            }

            if (gaugeSlider != null)
            {
                gaugeSlider.gameObject.SetActive(isSignalGame);
            }

            if (guideText != null)
            {
                if (isCommandGame)
                {
                    guideText.text = "왼쪽부터 순서대로 입력하세요";
                }
                else if (isSignalGame)
                {
                    guideText.text = "A와 D를 번갈아 눌러 게이지를 채우세요";
                }
                else
                {
                    guideText.text = "미니게임을 완료하세요";
                }
            }
        }

        /// <summary>
        /// 남은 시간 UI를 갱신합니다.
        /// </summary>
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

        /// <summary>
        /// SignalDivideGame의 게이지 UI를 갱신합니다.
        /// </summary>
        private void UpdateGaugeUI()
        {
            if (gaugeSlider == null)
            {
                return;
            }

            if (currentGame is SignalDivideGame)
            {
                gaugeSlider.value = currentGame.Progress;
            }
        }

        /// <summary>
        /// CommandOverrideGame의 현재 시퀀스를 8개 버튼 UI에 표시합니다.
        /// </summary>
        private void UpdateCommandSequenceUI()
        {
            CommandOverrideGame commandGame = currentGame as CommandOverrideGame;

            if (commandGame == null)
            {
                return;
            }

            if (commandSlots == null)
            {
                return;
            }

            for (int i = 0; i < commandSlots.Length; i++)
            {
                if (commandSlots[i] == null)
                {
                    continue;
                }

                bool isVisible = i < commandGame.SequenceLength;
                bool isCompleted = i < commandGame.CurrentIndex;
                bool isCurrent = i == commandGame.CurrentIndex;

                string arrow = isVisible ? commandGame.GetArrowAt(i) : string.Empty;

                commandSlots[i].SetSlot(arrow, isVisible, isCompleted, isCurrent);
            }
        }

        /// <summary>
        /// CommandSequenceSlotUI에서 특정 슬롯을 클릭했을 때 호출됩니다.
        /// </summary>
        public void PressCommandSlot(int slotIndex)
        {
            CommandOverrideGame commandGame = currentGame as CommandOverrideGame;

            if (commandGame == null)
            {
                Debug.LogWarning("[HackingPopupView] 현재 미니게임은 CommandOverrideGame이 아닙니다.");
                return;
            }

            commandGame.PressSlot(slotIndex);
        }

        /// <summary>
        /// SignalDivideGame의 A 버튼 입력입니다.
        /// </summary>
        public void PressA()
        {
            SendVirtualInput(KeyCode.A);
        }

        /// <summary>
        /// SignalDivideGame의 D 버튼 입력입니다.
        /// </summary>
        public void PressD()
        {
            SendVirtualInput(KeyCode.D);
        }

        /// <summary>
        /// UI 버튼 입력을 현재 미니게임에 전달합니다.
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
    }
}