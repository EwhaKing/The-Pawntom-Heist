using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Hacking
{
    /// <summary>
    /// CommandOverrideGame
    ///
    /// 담당:
    /// - 정해진 방향키 시퀀스를 왼쪽부터 순서대로 입력하는 미니게임
    /// - 키보드 방향키 입력 지원
    /// - UI에 나열된 CommandSlot_0~7 버튼 클릭 입력 지원
    ///
    /// 동작 방식:
    /// - targetSequence에 방향키 순서가 랜덤 생성됨
    /// - 현재 눌러야 하는 칸은 currentIdx로 관리
    /// - 올바른 순서로 누르면 currentIdx 증가
    /// - 틀리면 currentIdx가 0으로 초기화되어 처음부터 다시 시작
    /// - 마지막 칸까지 맞추면 성공 처리
    /// </summary>
    public class CommandOverrideGame : HackingGameBase
    {
        [Header("CommandOverride 설정")]
        [SerializeField] private int normalSequenceLength = 8;
        [SerializeField] private int vaultSequenceLength = 8;

        /// <summary>
        /// 이 미니게임에서 사용할 수 있는 방향키 목록입니다.
        /// </summary>
        private static readonly KeyCode[] PossibleKeys =
        {
            KeyCode.UpArrow,
            KeyCode.DownArrow,
            KeyCode.LeftArrow,
            KeyCode.RightArrow
        };

        /// <summary>
        /// KeyCode를 UI에 표시할 화살표 문자로 변환하기 위한 딕셔너리입니다.
        /// </summary>
        private static readonly Dictionary<KeyCode, string> KeyToArrow = new Dictionary<KeyCode, string>
        {
            { KeyCode.UpArrow, "↑" },
            { KeyCode.DownArrow, "↓" },
            { KeyCode.LeftArrow, "←" },
            { KeyCode.RightArrow, "→" }
        };

        /// <summary>
        /// 이번 미니게임에서 맞춰야 하는 방향키 시퀀스입니다.
        /// </summary>
        private KeyCode[] targetSequence;

        /// <summary>
        /// 현재 플레이어가 맞춰야 하는 시퀀스 인덱스입니다.
        /// 0이면 첫 번째 칸을 눌러야 하는 상태입니다.
        /// </summary>
        private int currentIdx;

        /// <summary>
        /// 현재까지 맞춘 칸 수 / 전체 칸 수입니다.
        /// HackingPopupView가 진행도를 표시할 때 사용할 수 있습니다.
        /// </summary>
        public override float Progress =>
            targetSequence == null || targetSequence.Length == 0
                ? 0f
                : (float)currentIdx / targetSequence.Length;

        /// <summary>
        /// 현재 시퀀스 길이입니다.
        /// HackingPopupView가 몇 개의 CommandSlot을 보여줄지 판단할 때 사용합니다.
        /// </summary>
        public int SequenceLength =>
            targetSequence == null ? 0 : targetSequence.Length;

        /// <summary>
        /// 현재 눌러야 하는 칸 번호입니다.
        /// HackingPopupView가 현재 칸 강조 표시를 할 때 사용합니다.
        /// </summary>
        public int CurrentIndex => currentIdx;

        /// <summary>
        /// 기존 TMP 텍스트 표시용 프로퍼티입니다.
        ///
        /// 지금은 CommandSlot_0~7 버튼 방식으로 표시할 예정이라
        /// 꼭 사용하지 않아도 됩니다.
        /// 그래도 디버그나 기존 UI 호환용으로 남겨둡니다.
        /// </summary>
        public override string DisplayText
        {
            get
            {
                if (targetSequence == null)
                {
                    return string.Empty;
                }

                StringBuilder sb = new StringBuilder();

                for (int i = 0; i < targetSequence.Length; i++)
                {
                    string arrow = KeyToArrow[targetSequence[i]];

                    if (i < currentIdx)
                    {
                        sb.Append($"<color=#4CAF50>{arrow}</color>");
                    }
                    else if (i == currentIdx)
                    {
                        sb.Append($"<color=#FFEB3B>{arrow}</color>");
                    }
                    else
                    {
                        sb.Append(arrow);
                    }

                    sb.Append(' ');
                }

                return sb.ToString();
            }
        }

        /// <summary>
        /// 미니게임 초기화.
        ///
        /// 보안 등급에 따라 시퀀스 길이를 정하고,
        /// 랜덤 방향키 시퀀스를 생성합니다.
        /// </summary>
        public override void InitGame(SecurityLevel level)
        {
            base.InitGame(level);

            int length = level == SecurityLevel.VaultFinal
                ? vaultSequenceLength
                : normalSequenceLength;

            targetSequence = new KeyCode[length];

            for (int i = 0; i < length; i++)
            {
                targetSequence[i] = PossibleKeys[Random.Range(0, PossibleKeys.Length)];
            }

            currentIdx = 0;

            Debug.Log($"[CommandOverrideGame] 게임 시작. 시퀀스 길이: {targetSequence.Length}");
        }

        /// <summary>
        /// 키보드 방향키 입력을 처리합니다.
        ///
        /// UI 버튼 입력은 PressSlot 또는 ReceiveVirtualInput을 통해 처리됩니다.
        /// </summary>
        protected override void HandleInput()
        {
            KeyCode pressed = KeyCode.None;

            foreach (KeyCode key in PossibleKeys)
            {
                if (Input.GetKeyDown(key))
                {
                    pressed = key;
                    break;
                }
            }

            if (pressed == KeyCode.None)
            {
                return;
            }

            CheckCommandInput(pressed);
        }

        /// <summary>
        /// UI 버튼에서 들어온 방향키 입력을 처리합니다.
        ///
        /// 예:
        /// - PressUp()    -> KeyCode.UpArrow
        /// - PressDown()  -> KeyCode.DownArrow
        /// - PressLeft()  -> KeyCode.LeftArrow
        /// - PressRight() -> KeyCode.RightArrow
        ///
        /// 단, 지금 만들려는 8칸 슬롯 방식에서는
        /// PressSlot을 주로 사용합니다.
        /// </summary>
        public override void ReceiveVirtualInput(KeyCode key)
        {
            CheckCommandInput(key);
        }

        /// <summary>
        /// 특정 인덱스의 방향키를 화살표 문자열로 반환합니다.
        ///
        /// HackingPopupView가 CommandSlot_0~7에
        /// 각각 어떤 화살표를 보여줄지 가져갈 때 사용합니다.
        /// </summary>
        public string GetArrowAt(int index)
        {
            if (targetSequence == null)
            {
                return string.Empty;
            }

            if (index < 0 || index >= targetSequence.Length)
            {
                return string.Empty;
            }

            return KeyToArrow[targetSequence[index]];
        }

        /// <summary>
        /// UI에 표시된 시퀀스 슬롯을 클릭했을 때 호출됩니다.
        ///
        /// 예:
        /// - CommandSlot_0 클릭 -> PressSlot(0)
        /// - CommandSlot_1 클릭 -> PressSlot(1)
        /// - CommandSlot_2 클릭 -> PressSlot(2)
        ///
        /// 반드시 currentIdx와 같은 순서의 슬롯을 눌러야 합니다.
        /// 틀린 칸을 누르면 처음부터 다시 시작합니다.
        /// </summary>
        public void PressSlot(int slotIndex)
        {
            if (!isActive)
            {
                return;
            }

            if (targetSequence == null || targetSequence.Length == 0)
            {
                return;
            }

            if (slotIndex < 0 || slotIndex >= targetSequence.Length)
            {
                Debug.LogWarning($"[CommandOverrideGame] 잘못된 슬롯 인덱스입니다: {slotIndex}");
                return;
            }

            if (slotIndex != currentIdx)
            {
                currentIdx = 0;
                Debug.Log("[CommandOverrideGame] 순서 불일치! 처음부터 다시 시작합니다.");
                return;
            }

            CheckCommandInput(targetSequence[slotIndex]);
        }

        /// <summary>
        /// 입력된 방향키가 현재 눌러야 하는 시퀀스와 맞는지 검사합니다.
        ///
        /// 키보드 입력과 UI 슬롯 입력이 모두 이 함수를 공유합니다.
        /// </summary>
        private void CheckCommandInput(KeyCode pressed)
        {
            if (!isActive)
            {
                return;
            }

            if (targetSequence == null || targetSequence.Length == 0)
            {
                return;
            }

            if (currentIdx < 0 || currentIdx >= targetSequence.Length)
            {
                return;
            }

            if (pressed == targetSequence[currentIdx])
            {
                currentIdx++;

                Debug.Log($"[CommandOverrideGame] 입력 성공. 현재 진행도: {currentIdx} / {targetSequence.Length}");

                if (currentIdx >= targetSequence.Length)
                {
                    FinishGame(true);
                }
            }
            else
            {
                currentIdx = 0;
                Debug.Log("[CommandOverrideGame] 입력 불일치! 처음부터 다시 시작합니다.");
            }
        }
    }
}