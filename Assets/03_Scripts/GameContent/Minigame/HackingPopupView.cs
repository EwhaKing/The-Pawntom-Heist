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
    /// - SignalDivide 랜덤 키 텍스트 표시
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

        [Tooltip("첫 번째 신호 버튼입니다. 예: AButton")]
        [SerializeField] private Button signalButton0Button;

        [Tooltip("두 번째 신호 버튼입니다. 예: DButton")]
        [SerializeField] private Button signalButton1Button;

        [Tooltip("첫 번째 신호 버튼의 TMP 텍스트입니다. 예: AButton 안의 Text")]
        [SerializeField] private TextMeshProUGUI signalButton0Text;

        [Tooltip("두 번째 신호 버튼의 TMP 텍스트입니다. 예: DButton 안의 Text")]
        [SerializeField] private TextMeshProUGUI signalButton1Text;

        [Header("Container")]
        [SerializeField] private Transform gameContainer;

        private HackingGameBase currentGame;
        private System.Action<bool> onResultCallback;

        private KeyCode lastDisplayedFirstKey = KeyCode.None;
        private KeyCode lastDisplayedSecondKey = KeyCode.None;

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
            AutoBindSignalDivideUI();
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
            UpdateSignalDivideTextIfNeeded();
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
        /// SignalDivide 버튼/텍스트 참조가 Inspector에 연결되지 않았을 때 자동으로 찾습니다.
        /// </summary>
        private void AutoBindSignalDivideUI()
        {
            if (signalButtonGroup == null)
            {
                return;
            }

            Button[] buttons = signalButtonGroup.GetComponentsInChildren<Button>(true);

            if (signalButton0Button == null && buttons.Length > 0)
            {
                signalButton0Button = buttons[0];
            }

            if (signalButton1Button == null && buttons.Length > 1)
            {
                signalButton1Button = buttons[1];
            }

            if (signalButton0Text == null && signalButton0Button != null)
            {
                signalButton0Text = signalButton0Button.GetComponentInChildren<TextMeshProUGUI>(true);
            }

            if (signalButton1Text == null && signalButton1Button != null)
            {
                signalButton1Text = signalButton1Button.GetComponentInChildren<TextMeshProUGUI>(true);
            }
        }

        /// <summary>
        /// 현재 실행 중인 미니게임 종류에 따라 필요한 UI만 보여줍니다.
        /// </summary>
        private void SetupUIByGameType()
        {
            bool isCommandGame = currentGame is CommandOverrideGame;
            bool isSignalGame = currentGame is SignalDivideGame;
            bool isDigitalSyncGame = currentGame is DigitalSyncGame;

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

            if (isSignalGame)
            {
                SetupSignalDivideText();
            }

            if (guideText != null)
            {
                if (isCommandGame)
                {
                    guideText.text = "왼쪽부터 순서대로 입력하세요";
                }
                else if (isSignalGame)
                {
                    guideText.text = "표시된 두 키를 번갈아 눌러 게이지를 채우세요";
                }
                else if (isDigitalSyncGame)
                {
                    guideText.text = "회전하는 바늘이 색칠된 구간에 들어왔을 때 SPACE를 누르세요";
                }
                else
                {
                    guideText.text = "미니게임을 완료하세요";
                }
            }
        }

        /// <summary>
        /// SignalDivideGame의 랜덤 키가 바뀌었거나,
        /// 초기화 타이밍 때문에 아직 텍스트 반영이 안 된 경우 다시 갱신합니다.
        /// </summary>
        private void UpdateSignalDivideTextIfNeeded()
        {
            SignalDivideGame signalGame = currentGame as SignalDivideGame;

            if (signalGame == null)
            {
                return;
            }

            if (signalGame.FirstSignalKey != lastDisplayedFirstKey ||
                signalGame.SecondSignalKey != lastDisplayedSecondKey)
            {
                SetupSignalDivideText();
            }
        }

        /// <summary>
        /// SignalDivideGame에서 랜덤으로 선택된 두 키를 버튼 텍스트에 표시합니다.
        /// </summary>
        private void SetupSignalDivideText()
        {
            SignalDivideGame signalGame = currentGame as SignalDivideGame;

            if (signalGame == null)
            {
                return;
            }

            AutoBindSignalDivideUI();

            string firstKeyText = KeyCodeToText(signalGame.FirstSignalKey);
            string secondKeyText = KeyCodeToText(signalGame.SecondSignalKey);

            if (signalButton0Text != null)
            {
                signalButton0Text.text = firstKeyText;
            }
            else
            {
                Debug.LogWarning("[HackingPopupView] Signal Button 0 Text가 연결되지 않았습니다.");
            }

            if (signalButton1Text != null)
            {
                signalButton1Text.text = secondKeyText;
            }
            else
            {
                Debug.LogWarning("[HackingPopupView] Signal Button 1 Text가 연결되지 않았습니다.");
            }

            lastDisplayedFirstKey = signalGame.FirstSignalKey;
            lastDisplayedSecondKey = signalGame.SecondSignalKey;

            Debug.Log($"[HackingPopupView] Signal 버튼 텍스트 갱신: {firstKeyText} / {secondKeyText}");
        }

        /// <summary>
        /// KeyCode를 UI 표시용 텍스트로 변환합니다.
        /// </summary>
        private string KeyCodeToText(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.W:
                    return "W";

                case KeyCode.A:
                    return "A";

                case KeyCode.S:
                    return "S";

                case KeyCode.D:
                    return "D";

                default:
                    return key.ToString();
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
        /// SignalDivideGame의 첫 번째 버튼 입력입니다.
        /// 기존 AButton OnClick과 호환하기 위해 이름을 유지합니다.
        /// </summary>
        public void PressA()
        {
            SendVirtualInput(KeyCode.A);
        }

        /// <summary>
        /// SignalDivideGame의 두 번째 버튼 입력입니다.
        /// 기존 DButton OnClick과 호환하기 위해 이름을 유지합니다.
        /// </summary>
        public void PressD()
        {
            SendVirtualInput(KeyCode.D);
        }

        /// <summary>
        /// UI 버튼 입력을 현재 미니게임에 전달합니다.
        /// SignalDivideGame에서는 KeyCode.A를 첫 번째 랜덤 키,
        /// KeyCode.D를 두 번째 랜덤 키로 해석합니다.
        /// </summary>
        private void SendVirtualInput(KeyCode key)
        {
            if (currentGame == null)
            {
                Debug.LogWarning("[HackingPopupView] 현재 실행 중인 미니게임이 없습니다.");
                return;
            }

            // 다른 화면(튜토리얼 대사)이 미니게임을 멈춰 둔 동안에는 버튼 입력도 받지 않는다.
            if (HackingGameBase.Paused)
            {
                return;
            }

            currentGame.ReceiveVirtualInput(key);
        }

        private void HandleGameEnded(bool isSuccess)
        {
            if (currentGame != null)
            {
                currentGame.OnGameEnded -= HandleGameEnded;
            }

            onResultCallback?.Invoke(isSuccess);
            ClosePopup();
        }

        public void ClosePopup()
        {
            Destroy(gameObject);
        }
    }
}