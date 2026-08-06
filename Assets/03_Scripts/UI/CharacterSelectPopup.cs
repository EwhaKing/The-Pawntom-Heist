using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 로비에서 고양이 정보 미리보기와 선택 확정을 관리합니다.
/// </summary>
public class CharacterSelectPopup : MonoBehaviour
{
    [SerializeField] private GameObject popupPanel;
    [SerializeField] private CharacterSelectButton[] selectButtons;

    [Header("Character Data")]
    [SerializeField] private CatData[] catDataList;

    [Header("Character Preview")]
    [SerializeField] private Image characterImage;
    [SerializeField] private TMP_Text characterNameText;
    [SerializeField] private TMP_Text characterDescriptionText;

    [Header("Actions")]
    [SerializeField] private Button selectButton;
    [SerializeField] private Button closeButton;

    private LobbyPlayerSlotUI _targetSlot;
    private CatData _previewedCatData;

    private void Awake()
    {
        InitializeButtons();

        if (selectButton != null)
        {
            selectButton.onClick.AddListener(ConfirmSelection);
        }

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(Close);
        }

        if (popupPanel != null)
        {
            popupPanel.SetActive(false);
        }
    }

    private void InitializeButtons()
    {
        if (selectButtons == null)
        {
            return;
        }

        foreach (CharacterSelectButton selectButton in selectButtons)
        {
            if (selectButton != null)
            {
                selectButton.Initialize(this);
            }
        }
    }

    public void Open(LobbyPlayerSlotUI targetSlot)
    {
        if (targetSlot == null)
        {
            Debug.LogError("[CharacterSelectPopup] 팝업을 열 대상 슬롯이 없습니다.");
            return;
        }

        _targetSlot = targetSlot;
        popupPanel.SetActive(true);

        LobbyPlayerData localPlayerData = LobbyManager.Instance?.GetLocalPlayerData();

        if (localPlayerData != null)
        {
            PreviewCharacter(localPlayerData.SelectedCatType);
        }
        else if (catDataList != null && catDataList.Length > 0 && catDataList[0] != null)
        {
            PreviewCharacter(catDataList[0].CatType);
        }
    }

    public void Close()
    {
        popupPanel.SetActive(false);
        _targetSlot = null;
        _previewedCatData = null;
    }

    /// <summary>
    /// 목록에서 고양이를 눌렀을 때 상세 정보만 변경합니다.
    /// </summary>
    public void PreviewCharacter(CatType catType)
    {
        CatData catData = GetCatData(catType);

        if (catData == null)
        {
            Debug.LogError($"[CharacterSelectPopup] {catType}에 해당하는 CatData가 없습니다.");
            return;
        }

        _previewedCatData = catData;

        if (characterImage != null)
        {
            characterImage.sprite = catData.CharacterSelectIllustration;
            characterImage.gameObject.SetActive(catData.CharacterSelectIllustration != null); //TODO 수정
        }

        if (characterNameText != null)
        {
            characterNameText.text = catData.DisplayName;
        }

        if (characterDescriptionText != null)
        {
            characterDescriptionText.text = catData.Description;
        }

        if (selectButton != null)
        {
            selectButton.interactable = true;
        }
    }

    /// <summary>
    /// 현재 미리 보고 있는 고양이를 실제 선택으로 확정합니다.
    /// 팝업은 자동으로 닫지 않습니다.
    /// </summary>
    public void ConfirmSelection()
    {
        if (_targetSlot == null || _previewedCatData == null)
        {
            Debug.LogError("[CharacterSelectPopup] 확정할 고양이가 선택되지 않았습니다.");
            return;
        }

        LobbyPlayerData localPlayerData = LobbyManager.Instance?.GetLocalPlayerData();

        if (localPlayerData == null)
        {
            Debug.LogError("[CharacterSelectPopup] 로컬 플레이어 데이터를 찾지 못했습니다.");
            return;
        }

        localPlayerData.RequestSelectCat(_previewedCatData.CatType);
        Debug.Log($"[CharacterSelectPopup] 선택한 고양이: {_previewedCatData.CatType}");
    }

    private CatData GetCatData(CatType catType)
    {
        if (catDataList == null)
        {
            return null;
        }

        foreach (CatData catData in catDataList)
        {
            if (catData != null && catData.CatType == catType)
            {
                return catData;
            }
        }

        return null;
    }

    private void OnDestroy()
    {
        if (selectButton != null)
        {
            selectButton.onClick.RemoveListener(ConfirmSelection);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(Close);
        }
    }
}
