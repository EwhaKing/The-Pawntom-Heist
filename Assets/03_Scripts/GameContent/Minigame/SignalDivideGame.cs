using UnityEngine;

namespace Hacking
{
    /// <summary>
    /// WASD 중 랜덤으로 선택된 2개의 키를 번갈아 눌러 게이지를 채우는 미니게임
    ///
    /// 같은 키를 연속으로 누르면 무시되고, 다른 키로 바꿔 눌러야 게이지가 오름
    /// 게이지는 가만히 있으면 초당 일정량 감소하므로 리듬을 놓치면 실패
    ///
    /// 게이지 UI는 이 스크립트가 직접 그리지 않고, HackingPopupView가 Progress 값을 읽어서 그림
    /// </summary>
    public class SignalDivideGame : HackingGameBase
    {
        [Header("SignalDivide 설정")]
        [SerializeField] private float maxGauge = 100f;
        [SerializeField] private float gaugeDecreasePerSecond = 3f;
        [SerializeField] private float gaugeIncreasePerPress = 10f;

        private readonly KeyCode[] candidateKeys =
        {
            KeyCode.W,
            KeyCode.A,
            KeyCode.S,
            KeyCode.D
        };

        private float progressGauge;

        private KeyCode firstSignalKey = KeyCode.A;
        private KeyCode secondSignalKey = KeyCode.D;

        private KeyCode lastPressedKey = KeyCode.None;

        /// <summary>
        /// HackingPopupView가 게이지 슬라이더를 갱신할 때 읽어가는 값
        /// 0~1 사이 값으로 반환
        /// </summary>
        public override float Progress => maxGauge > 0f ? progressGauge / maxGauge : 0f;

        /// <summary>
        /// 이번 미니게임에서 선택된 첫 번째 키
        /// HackingPopupView에서 버튼 텍스트를 바꿀 때 사용
        /// </summary>
        public KeyCode FirstSignalKey => firstSignalKey;

        /// <summary>
        /// 이번 미니게임에서 선택된 두 번째 키
        /// HackingPopupView에서 버튼 텍스트를 바꿀 때 사용
        /// </summary>
        public KeyCode SecondSignalKey => secondSignalKey;

        public override void InitGame(SecurityLevel level)
        {
            base.InitGame(level);

            progressGauge = 0f;
            lastPressedKey = KeyCode.None;

            SelectSignalKeys();

            // 등급별 난이도 조정
            // 일반 해킹보다 VaultFinal 해킹에서 게이지가 더 빨리 감소
            gaugeDecreasePerSecond = (level == SecurityLevel.VaultFinal) ? 6f : 4f;

            Debug.Log($"[SignalDivideGame] 선택된 키: {firstSignalKey} / {secondSignalKey}");
        }

        /// <summary>
        /// WASD 중 서로 다른 2개의 키를 랜덤으로 선택
        /// </summary>
        private void SelectSignalKeys()
        {
            int firstIndex = Random.Range(0, candidateKeys.Length);
            int secondIndex = Random.Range(0, candidateKeys.Length);

            while (secondIndex == firstIndex)
            {
                secondIndex = Random.Range(0, candidateKeys.Length);
            }

            firstSignalKey = candidateKeys[firstIndex];
            secondSignalKey = candidateKeys[secondIndex];
        }

        protected override void UpdateTimer()
        {
            base.UpdateTimer();

            // base.UpdateTimer()에서 시간 초과로 이미 게임이 끝났을 수 있음
            if (!isActive)
            {
                return;
            }

            progressGauge -= gaugeDecreasePerSecond * Time.deltaTime;

            if (progressGauge < 0f)
            {
                progressGauge = 0f;
            }
        }

        protected override void HandleInput()
        {
            KeyCode pressed = KeyCode.None;

            if (Input.GetKeyDown(firstSignalKey))
            {
                pressed = firstSignalKey;
            }
            else if (Input.GetKeyDown(secondSignalKey))
            {
                pressed = secondSignalKey;
            }

            if (pressed == KeyCode.None)
            {
                return;
            }

            CheckSignalInput(pressed);
        }

        /// <summary>
        /// UI 버튼에서 들어온 입력을 처리
        ///
        /// 현재 HackingPopupView가 기존처럼 AButton / DButton으로 되어 있을 수 있으므로,
        /// KeyCode.A 입력은 첫 번째 랜덤 키 버튼,
        /// KeyCode.D 입력은 두 번째 랜덤 키 버튼처럼 처리
        /// </summary>
        public override void ReceiveVirtualInput(KeyCode key)
        {
            if (!isActive)
            {
                return;
            }

            // 기존 AButton 클릭 대응
            if (key == KeyCode.A)
            {
                CheckSignalInput(firstSignalKey);
                return;
            }

            // 기존 DButton 클릭 대응
            if (key == KeyCode.D)
            {
                CheckSignalInput(secondSignalKey);
                return;
            }

            // 혹시 나중에 HackingPopupView에서 실제 랜덤 키를 넘기게 바꿀 경우를 대비
            CheckSignalInput(key);
        }

        /// <summary>
        /// 첫 번째 UI 버튼에서 직접 호출할 수 있는 함수
        /// 나중에 HackingPopupView를 정리할 때 사용
        /// </summary>
        public void PressFirstSignalKey()
        {
            CheckSignalInput(firstSignalKey);
        }

        /// <summary>
        /// 두 번째 UI 버튼에서 직접 호출할 수 있는 함수
        /// 나중에 HackingPopupView를 정리할 때 사용
        /// </summary>
        public void PressSecondSignalKey()
        {
            CheckSignalInput(secondSignalKey);
        }

        /// <summary>
        /// 선택된 두 키의 교차 입력을 검사하고 게이지를 증가
        /// </summary>
        private void CheckSignalInput(KeyCode pressed)
        {
            if (!isActive)
            {
                return;
            }

            // 이번 미니게임에서 선택된 두 키가 아니면 무시
            if (pressed != firstSignalKey && pressed != secondSignalKey)
            {
                return;
            }

            // 같은 키를 연속으로 누르면 무시
            if (pressed == lastPressedKey)
            {
                Debug.Log($"[SignalDivideGame] 같은 키 연속 입력 무시: {pressed}");
                return;
            }

            lastPressedKey = pressed;
            progressGauge += gaugeIncreasePerPress;

            Debug.Log($"[SignalDivideGame] 입력 키: {pressed} / 게이지: {progressGauge:F1} / {maxGauge}");

            if (progressGauge >= maxGauge)
            {
                progressGauge = maxGauge;
                FinishGame(true);
            }
        }

        /// <summary>
        /// UI에 표시할 첫 번째 키 텍스트
        /// </summary>
        public string GetFirstSignalKeyText()
        {
            return KeyCodeToText(firstSignalKey);
        }

        /// <summary>
        /// UI에 표시할 두 번째 키 텍스트
        /// </summary>
        public string GetSecondSignalKeyText()
        {
            return KeyCodeToText(secondSignalKey);
        }

        /// <summary>
        /// KeyCode를 UI에 보여주기 좋은 문자열로 바꿈
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
    }
}