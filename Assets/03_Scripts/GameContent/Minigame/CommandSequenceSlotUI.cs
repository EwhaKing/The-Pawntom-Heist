using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Hacking
{
    /// <summary>
    /// CommandSequenceSlotUI
    ///
    /// 담당:
    /// - CommandOverrideGame에서 사용하는 시퀀스 버튼 하나를 표시
    /// - 화살표 텍스트 표시
    /// - 현재 상태에 따라 화살표 색과 버튼 윤곽선 색 변경
    /// - 클릭되었을 때 HackingPopupView에 몇 번째 슬롯이 눌렸는지 전달
    ///
    /// 상태 색:
    /// - 기본: 검은색
    /// - 현재 눌러야 하는 슬롯: 마젠타
    /// - 완료된 슬롯: 초록색
    /// </summary>
    public class CommandSequenceSlotUI : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private Button button;
        [SerializeField] private TextMeshProUGUI arrowText;

        [Header("Border Images")]
        [SerializeField] private Image[] borderImages;

        [Header("Sprite Skin (선택)")]
        [Tooltip("슬롯 그림을 바꿔 끼울 이미지. 비우면 아래 스프라이트 스킨을 쓰지 않고 텍스트·윤곽선 색만 바꿉니다.")]
        [SerializeField] private Image iconImage;

        [Tooltip("기본 상태. 방향 순서: 위, 오른쪽, 아래, 왼쪽")]
        [SerializeField] private Sprite[] normalSprites;

        [Tooltip("이미 맞춘 칸. 방향 순서: 위, 오른쪽, 아래, 왼쪽")]
        [SerializeField] private Sprite[] completedSprites;

        [Tooltip("틀렸을 때. 방향 순서: 위, 오른쪽, 아래, 왼쪽")]
        [SerializeField] private Sprite[] errorSprites;

        [Tooltip("지금 눌러야 하는 칸을 살짝 키워 알려줍니다.")]
        [SerializeField, Min(1f)] private float currentScale = 1.08f;

        [Header("Colors")]
        [SerializeField] private Color normalColor = Color.black;
        [SerializeField] private Color currentColor = new Color(1f, 0f, 0.65f, 1f);
        [SerializeField] private Color completedColor = new Color(0.2f, 0.9f, 0.25f, 1f);

        private HackingPopupView owner;
        private int slotIndex;

        private void Awake()
        {
            if (button == null)
            {
                button = GetComponent<Button>();
            }

            if (arrowText == null)
            {
                arrowText = GetComponentInChildren<TextMeshProUGUI>();
            }

            if (borderImages == null || borderImages.Length == 0)
            {
                Transform borderTransform = transform.Find("Border");

                if (borderTransform != null)
                {
                    borderImages = borderTransform.GetComponentsInChildren<Image>(true);
                }
            }
        }

        /// <summary>
        /// 이 슬롯이 몇 번째 칸인지 초기화합니다.
        /// </summary>
        public void Initialize(HackingPopupView popupView, int index)
        {
            owner = popupView;
            slotIndex = index;

            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(OnClickSlot);
            }
        }

        /// <summary>
        /// 슬롯에 표시할 화살표와 상태를 갱신합니다.
        /// </summary>
        public void SetSlot(string arrow, bool isVisible, bool isCompleted, bool isCurrent, bool isError = false)
        {
            gameObject.SetActive(isVisible);

            if (!isVisible)
            {
                return;
            }

            ApplySpriteSkin(arrow, isCompleted, isCurrent, isError);

            Color stateColor = normalColor;

            if (isCompleted)
            {
                stateColor = completedColor;
            }
            else if (isCurrent)
            {
                stateColor = currentColor;
            }

            if (arrowText != null)
            {
                arrowText.text = arrow;
                arrowText.color = stateColor;
            }

            SetBorderColor(stateColor);
        }

        /// <summary>
        /// 스프라이트 스킨이 있으면 상태(기본/완료/오답)와 방향에 맞는 그림으로 바꿉니다.
        /// </summary>
        private void ApplySpriteSkin(string arrow, bool isCompleted, bool isCurrent, bool isError)
        {
            if (iconImage == null)
            {
                return;
            }

            int direction = ArrowToDirectionIndex(arrow);
            Sprite[] set = isError ? errorSprites : isCompleted ? completedSprites : normalSprites;

            if (direction >= 0 && set != null && direction < set.Length && set[direction] != null)
            {
                iconImage.sprite = set[direction];
            }

            float scale = isCurrent && !isCompleted && !isError ? currentScale : 1f;
            iconImage.rectTransform.localScale = new Vector3(scale, scale, 1f);
        }

        private static int ArrowToDirectionIndex(string arrow)
        {
            switch (arrow)
            {
                case "↑": return 0;
                case "→": return 1;
                case "↓": return 2;
                case "←": return 3;
                default: return -1;
            }
        }

        /// <summary>
        /// 버튼 윤곽선 색을 변경합니다.
        /// </summary>
        private void SetBorderColor(Color color)
        {
            if (borderImages == null)
            {
                return;
            }

            for (int i = 0; i < borderImages.Length; i++)
            {
                if (borderImages[i] == null)
                {
                    continue;
                }

                borderImages[i].color = color;
            }
        }

        /// <summary>
        /// 플레이어가 이 슬롯을 클릭했을 때 호출됩니다.
        /// </summary>
        private void OnClickSlot()
        {
            if (owner == null)
            {
                Debug.LogWarning("[CommandSequenceSlotUI] owner가 없습니다.");
                return;
            }

            owner.PressCommandSlot(slotIndex);
        }
    }
}