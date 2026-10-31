using UnityEngine;
using UnityEngine.UI;

using TMPro;

/// <summary>
/// InventorySlotUI
/// 
/// 담당:
/// - 인벤토리 슬롯 하나의 UI 표시
/// - 아이템 아이콘 표시
/// - 빈 슬롯 처리
/// - 선택된 슬롯 강조 표시
/// 
/// 위치:
/// - Slot_1,2,3,4 오브젝트에 붙임
/// </summary>
public class InventorySlotUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Image slotImage;
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text slotNumberText;

    [Header("Seleted Border")]
    [SerializeField] private GameObject selectedBorder;

    [Header("Selection Motion")]
    [Tooltip("선택된 슬롯이 커지는 배율. 1 이면 크기 변화 없음")]
    [SerializeField, Min(1f)] private float selectedScale = 1.12f;

    [Tooltip("크기가 따라가는 빠르기(1/초). 클수록 빠르게 바뀐다")]
    [SerializeField, Min(1f)] private float scaleSpeed = 18f;

    private bool _isSelected;
    private float _currentScale = 1f;

    private void Update()
    {
        // 선택 칸이 순간 이동하지 않고 부드럽게 커지고 작아지게 한다(휠로 넘길 때 끊겨 보이지 않도록).
        float target = _isSelected ? selectedScale : 1f;

        if (Mathf.Approximately(_currentScale, target))
        {
            return;
        }

        _currentScale = Mathf.Lerp(_currentScale, target, 1f - Mathf.Exp(-scaleSpeed * Time.unscaledDeltaTime));

        if (Mathf.Abs(_currentScale - target) < 0.001f)
        {
            _currentScale = target;
        }

        transform.localScale = new Vector3(_currentScale, _currentScale, 1f);
    }

    private void Awake()
    {
        if (slotImage != null)
        {
            slotImage.color = new Color(1f, 1f, 1f, 0.1f);
        }

        if (selectedBorder != null)
        {
            selectedBorder.SetActive(false);
        }
    }

    /// <summary>
    /// 슬롯 번호 설정
    /// </summary>
    public void SetSlotNumber(int number)
    {
        slotNumberText.text = number.ToString();
    }

    /// <summary>
    /// 슬롯 아이템 아이콘 표시
    /// </summary>
    public void SetItem(Sprite icon)
    {
        if (iconImage == null)
            return;
        
        if (icon == null)
        {
            ClearItem();
            return;
        }

        iconImage.sprite = icon;
        iconImage.enabled = true;
    }

    /// <summary>
    /// 슬롯을 빈 슬롯 상태로 표시
    /// </summary>
    public void ClearItem()
    {
        if (iconImage == null)
            return;

        iconImage.sprite = null;
        iconImage.enabled = false;
    }

    /// <summary>
    /// 선택 여부에 따라 선택 테두리 표시
    /// </summary>
    public void SetSelected(bool isSelected)
    {
        _isSelected = isSelected;

        if (selectedBorder != null)
        {
            selectedBorder.SetActive(isSelected);
        }
    }
}
