using TMPro;
using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 로비의 플레이어 한 명에 대한 슬롯 UI를 표시합니다.
/// </summary>
public class LobbyPlayerSlotUI : MonoBehaviour
{
    public int SlotIndex { get; private set; } //슬롯에 번호 배정

    [Header("Player")]
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private GameObject crownIcon;
    [SerializeField] private GameObject readyIndicator;

    [Header("Local Player")]
    [SerializeField] private GameObject localPlayerHighlight;

    [Header("Character")]
    [SerializeField] private Image catImage;
    [SerializeField] private Button characterClickButton;

    [Header("Popup")]
    [SerializeField] private CharacterSelectPopup characterSelectPopup;

    [SerializeField] private CatData[] catDataList;

    private bool _isLocalPlayer; //로컬플레이어인지 판단해주는 부울 변수

    private void Awake()
    {
        characterClickButton.onClick.AddListener(OnClickCharacterImage);
    }

    /// <summary>
    /// 슬롯에 번호 지정.
    /// </summary>
    public void SetSlotIndex(int slotIndex)
    {
        SlotIndex = slotIndex;
    }

    public void Initialize(CharacterSelectPopup popup)
    {
        characterSelectPopup = popup;
    }

    /// <summary>
    /// 플레이어가 들어왔을 때, 슬롯에 캐릭터 정보 표시.
    /// </summary>
    public void SetPlayer(string playerName, bool isHost, bool isLocalPlayer, CatType selectedCatType, bool isReady)
    {
        _isLocalPlayer = isLocalPlayer;

        playerNameText.text = playerName;
        crownIcon.SetActive(isHost);
        readyIndicator.SetActive(isReady);
        localPlayerHighlight.SetActive(isLocalPlayer);
        characterClickButton.interactable = isLocalPlayer;

        SetCharacter(selectedCatType);
    }

    /// <summary>
    /// CatType에 맞는 CatData를 찾아 캐릭터 이미지를 표시합니다.
    /// </summary>
    private void SetCharacter(CatType catType)
    {
        CatData catData = GetCatData(catType);

        if (catData == null)
        {
            catImage.sprite = null;
            catImage.gameObject.SetActive(false);
            return;
        }

        catImage.sprite = catData.LobbySprite;
        catImage.gameObject.SetActive(true);
    }

    /// <summary>
    /// CatType에 해당하는 CatData를 반환합니다.
    /// </summary>
    private CatData GetCatData(CatType catType)
    {
        foreach (CatData catData in catDataList)
        {
            if (catData != null && catData.CatType == catType)
            {
                return catData;
            }
        }

        return null;
    }

    /// <summary>
    /// 플레이어가 없을 때, 나갔을 때 빈 슬롯으로 초기화하는 함수.
    /// </summary>
    public void SetEmpty()
    {
        _isLocalPlayer = false;

        playerNameText.text = string.Empty;
        crownIcon.SetActive(false);
        readyIndicator.SetActive(false);
        characterClickButton.interactable = false;
        localPlayerHighlight.SetActive(false);

        catImage.gameObject.SetActive(false);
        catImage.sprite = null;
    }

    /// <summary>
    /// 캐릭터 이미지(버튼)를 눌렀을 때, 
    /// 로컬 플레이어의 슬롯일 때만 캐릭터 종류 선택창UI를 띄워줌.
    /// </summary>
    public void OnClickCharacterImage()
    {
        if (!_isLocalPlayer)
        {
            Debug.Log("localplayer가 아닙니다.");
            return;
        } //본인 것만 누를 수 있습니다.

        if (characterSelectPopup == null)
        {
            Debug.LogError(
                "[LobbyPlayerSlotUI] CharacterSelectPopup이 연결되지 않았습니다."
            );
            return;
        }

        characterSelectPopup.Open(this);
    }
}
