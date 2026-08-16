using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Hacking
{
    /// <summary>
    /// 정해진 방향키 순서(시퀀스)를 그대로 입력해야 하는 미니게임.
    /// 화면에 목표 시퀀스가 표시되고, 맞게 누르면 다음 칸으로 넘어가며,
    /// 순서를 틀리면 처음(0번째)부터 다시 시작합니다.
    /// 보안 등급에 따라 시퀀스 길이가 달라집니다 (일반: 4개, 금고: 8개).
    /// </summary>
    public class CommandOverrideGame : HackingGameBase
    {
        [Header("CommandOverride 설정")]
        [SerializeField] private int normalSequenceLength = 4;
        [SerializeField] private int vaultSequenceLength = 8;

        private static readonly KeyCode[] PossibleKeys =
        {
            KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.LeftArrow, KeyCode.RightArrow
        };

        private static readonly Dictionary<KeyCode, string> KeyToArrow = new Dictionary<KeyCode, string>
        {
            { KeyCode.UpArrow, "↑" },
            { KeyCode.DownArrow, "↓" },
            { KeyCode.LeftArrow, "←" },
            { KeyCode.RightArrow, "→" }
        };

        private KeyCode[] targetSequence;
        private int currentIdx;

        /// <summary>현재까지 맞춘 칸 수 / 전체 칸 수. 게이지 슬라이더 연결 시 진행률로 표시됩니다.</summary>
        public override float Progress =>
            (targetSequence == null || targetSequence.Length == 0) ? 0f : (float)currentIdx / targetSequence.Length;

        /// <summary>
        /// 목표 시퀀스를 화살표 문자열로 반환. 이미 맞춘 칸은 초록색, 다음에 맞춰야 할 칸은 노란색으로 표시합니다.
        /// TextMeshPro의 리치 텍스트(color 태그)를 사용하므로 sequenceText가 TMP 컴포넌트여야 합니다.
        /// </summary>
        public override string DisplayText
        {
            get
            {
                if (targetSequence == null) return string.Empty;

                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < targetSequence.Length; i++)
                {
                    string arrow = KeyToArrow[targetSequence[i]];

                    if (i < currentIdx)
                        sb.Append($"<color=#4CAF50>{arrow}</color>"); // 이미 맞춘 칸: 초록
                    else if (i == currentIdx)
                        sb.Append($"<color=#FFEB3B>{arrow}</color>"); // 지금 눌러야 할 칸: 노랑
                    else
                        sb.Append(arrow); // 아직 남은 칸: 기본색

                    sb.Append(' ');
                }
                return sb.ToString();
            }
        }

        public override void InitGame(SecurityLevel level)
        {
            base.InitGame(level);

            int length = (level == SecurityLevel.VaultFinal) ? vaultSequenceLength : normalSequenceLength;
            targetSequence = new KeyCode[length];
            for (int i = 0; i < length; i++)
            {
                targetSequence[i] = PossibleKeys[Random.Range(0, PossibleKeys.Length)];
            }

            currentIdx = 0;
        }

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

            if (pressed == KeyCode.None) return;

            CheckCommandInput(pressed);
        }

        /// <summary>
        /// UI 버튼에서 들어온 방향키 입력 처리
        /// </summary>
        public override void ReceiveVirtualInput(KeyCode key)
        {
            CheckCommandInput(key);
        }

        private void CheckCommandInput(KeyCode pressed)
        {
            if (!isActive)
                return;
            
            if (targetSequence == null || targetSequence.Length == 0)
                return;
            
            if (currentIdx < 0 || currentIdx >= targetSequence.Length)
                return;
            
            if (pressed == targetSequence[currentIdx])
            {
                currentIdx++;
                
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