using UnityEngine;

namespace Hacking
{
    /// <summary>
    /// A와 D를 번갈아 눌러 게이지를 채우는 미니게임.
    /// 같은 키를 연속으로 누르면 무시되고, 다른 키로 바꿔 눌러야 게이지가 오릅니다.
    /// 게이지는 가만히 있으면 초당 일정량 감소하므로 리듬을 놓치면 실패할 수 있습니다.
    ///
    /// 게이지 UI는 이 스크립트가 직접 그리지 않고, HackingPopupView가 Progress 값을 읽어서 그립니다.
    /// (미니게임 프리팹 안에 UI를 직접 넣으면 Canvas 계층 문제가 생기기 쉬워서 이 구조로 분리했습니다.)
    /// </summary>
    public class SignalDivideGame : HackingGameBase
    {
        [Header("SignalDivide 설정")]
        [SerializeField] private float maxGauge = 100f;
        [SerializeField] private float gaugeDecreasePerSecond = 3f;
        [SerializeField] private float gaugeIncreasePerPress = 10f;

        private float progressGauge;
        private KeyCode lastPressedKey = KeyCode.None;

        /// <summary>HackingPopupView가 게이지 슬라이더를 갱신할 때 읽어가는 값 (0~1).</summary>
        public override float Progress => maxGauge > 0f ? progressGauge / maxGauge : 0f;

        public override void InitGame(SecurityLevel level)
        {
            base.InitGame(level);

            progressGauge = 0f;
            lastPressedKey = KeyCode.None;

            // 등급별 난이도 조정 예시: 금고는 게이지가 더 빨리 빠짐
            gaugeDecreasePerSecond = (level == SecurityLevel.VaultFinal) ? 6f : 4f;
        }

        protected override void UpdateTimer()
        {
            base.UpdateTimer();
            if (!isActive) return; // base.UpdateTimer()에서 시간 초과로 이미 끝났을 수 있음

            progressGauge -= gaugeDecreasePerSecond * Time.deltaTime;
            if (progressGauge < 0f) progressGauge = 0f;
        }

        protected override void HandleInput()
        {
            KeyCode pressed = KeyCode.None;
            if (Input.GetKeyDown(KeyCode.A)) pressed = KeyCode.A;
            else if (Input.GetKeyDown(KeyCode.D)) pressed = KeyCode.D;

            if (pressed == KeyCode.None) return;

            CheckSignalInput(pressed);
        }

        /// <summary>
        /// UI 버튼에서 들어온 A/D 입력을 처리합니다.
        /// </summary>
        public override void ReceiveVirtualInput(KeyCode key)
        {
            CheckSignalInput(key);
        }

        /// <summary>
        /// A/D 교차 입력을 검사하고 게이지를 증가시킵니다.
        /// </summary>
        private void CheckSignalInput(KeyCode pressed)
        {
            if (!isActive) return;

            if (pressed != KeyCode.A && pressed != KeyCode.D) return;

            // 같은 키를 연속으로 누르면 무시
            if (pressed == lastPressedKey) return;

            lastPressedKey = pressed;
            progressGauge += gaugeIncreasePerPress;

            // [TEST ONLY] 실제로 게이지가 오르는지 콘솔로 확인용. 확인 후 지워도 됩니다.
            Debug.Log($"[SignalDivideGame] 게이지: {progressGauge:F1} / {maxGauge}");

            if (progressGauge >= maxGauge)
            {
                progressGauge = maxGauge;
                FinishGame(true);
            }
        }
    }
}