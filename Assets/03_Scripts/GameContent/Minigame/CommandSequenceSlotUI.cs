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
        public void SetSlot(string arrow, bool isVisible, bool isCompleted, bool isCurrent)
        {
            gameObject.SetActive(isVisible);

            if (!isVisible)
            {
                return;
            }

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