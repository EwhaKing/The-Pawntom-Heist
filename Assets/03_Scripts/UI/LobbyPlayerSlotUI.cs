using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 로비의 플레이어 한 명에 대한 슬롯 UI를 표시합니다.
/// 상태: 빈 슬롯 / 참가·미선택 / 선택. 표시는 SetPlayer·SetEmpty·SetMicState 호출 때만 바뀝니다.
/// 새로 추가된 참조는 비어 있어도 동작합니다(프리팹 재구성 전).
/// </summary>
public class LobbyPlayerSlotUI : MonoBehaviour
{
    public int SlotIndex { get; private set; } //슬롯에 번호 배정

    [Header("Name Tag")]
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private Image nameTagImage;
    [SerializeField] private Sprite emptyNameTagSprite;
    [SerializeField] private TMP_Text roleText;

    [Header("Markers")]
    [SerializeField] private GameObject crownIcon;
    [SerializeField] private GameObject readyRing;
    [SerializeField] private GameObject readyLabel;

    [Header("Legacy (프리팹에 남아 있으면 무시/숨김)")]
    [SerializeField] private GameObject readyIndicator;
    [SerializeField] private GameObject localPlayerHighlight;

    [Header("Character")]
    [SerializeField] private Image catImage;
    [SerializeField] private Sprite emptyPortraitSprite;
    [SerializeField] private Button characterClickButton;

    [Header("Mic")]
    [SerializeField] private Image micIcon;
    [SerializeField] private Sprite micIdleSprite;
    [SerializeField] private Sprite micSpeakingSprite;
    [SerializeField] private Sprite micMutedSprite;
    [SerializeField] private Color micIdleColor = Color.white;
    [SerializeField] private Color micSpeakingColor = Color.white;
    [SerializeField] private Color micMutedColor = Color.white;

    [Header("Popup")]
    [SerializeField] private CharacterSelectPopup characterSelectPopup;

    [SerializeField] private CatData[] catDataList;

    private bool _isLocalPlayer; //로컬플레이어인지 판단해주는 부울 변수
    private bool _isOccupied;
    private LobbyMicState _micState = LobbyMicState.Idle;

    private void Awake()
    {
        if (characterClickButton != null)
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
    /// 호스트는 항상 Ready로 취급해 링을 표시합니다.
    /// </summary>
    public void SetPlayer(string playerName, bool isHost, bool isLocalPlayer, bool hasSelectedCat, CatType selectedCatType, bool isReady)
    {
        _isLocalPlayer = isLocalPlayer;
        _isOccupied = true;

        if (playerNameText != null) playerNameText.text = playerName;
        SetActive(crownIcon, isHost);
        SetActive(readyRing, isHost || isReady);
        SetActive(readyLabel, !isHost && isReady);
        SetActive(readyIndicator, false);
        SetActive(localPlayerHighlight, false);
        if (characterClickButton != null) characterClickButton.interactable = isLocalPlayer;

        CatData catData = hasSelectedCat ? GetCatData(selectedCatType) : null;
        if (catData != null && catData.LobbySprite != null)
        {
            SetPortrait(catData.LobbySprite);
            SetNameTag(catData.LobbyNameTagSprite != null ? catData.LobbyNameTagSprite : emptyNameTagSprite);
            if (roleText != null) roleText.text = catData.RoleName;
        }
        else
        {
            SetPortrait(emptyPortraitSprite);
            SetNameTag(emptyNameTagSprite);
            if (roleText != null) roleText.text = string.Empty;
        }

        ApplyMic();
    }

    /// <summary>
    /// 플레이어가 없을 때, 나갔을 때 빈 슬롯으로 초기화하는 함수.
    /// </summary>
    public void SetEmpty()
    {
        _isLocalPlayer = false;
        _isOccupied = false;

        if (playerNameText != null) playerNameText.text = string.Empty;
        if (roleText != null) roleText.text = string.Empty;
        SetActive(crownIcon, false);
        SetActive(readyRing, false);
        SetActive(readyLabel, false);
        SetActive(readyIndicator, false);
        SetActive(localPlayerHighlight, false);
        if (characterClickButton != null) characterClickButton.interactable = false;

        SetPortrait(emptyPortraitSprite);
        SetNameTag(emptyNameTagSprite);
        ApplyMic();
    }

    /// <summary>
    /// 마이크 아이콘 상태를 바꿉니다. 음성 기능이 연결되면 그쪽에서 호출합니다.
    /// </summary>
    public void SetMicState(LobbyMicState state)
    {
        _micState = state;
        ApplyMic();
    }

    private void ApplyMic()
    {
        if (micIcon == null) return;

        micIcon.gameObject.SetActive(_isOccupied);
        if (!_isOccupied) return;

        switch (_micState)
        {
            case LobbyMicState.Speaking:
                if (micSpeakingSprite != null) micIcon.sprite = micSpeakingSprite;
                micIcon.color = micSpeakingColor;
                break;
            case LobbyMicState.Muted:
                if (micMutedSprite != null) micIcon.sprite = micMutedSprite;
                micIcon.color = micMutedColor;
                break;
            default:
                if (micIdleSprite != null) micIcon.sprite = micIdleSprite;
                micIcon.color = micIdleColor;
                break;
        }
    }

    private void SetPortrait(Sprite sprite)
    {
        if (catImage == null) return;
        catImage.sprite = sprite;
        catImage.gameObject.SetActive(sprite != null);
    }

    private void SetNameTag(Sprite sprite)
    {
        if (nameTagImage == null || sprite == null) return;
        nameTagImage.sprite = sprite;
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null) target.SetActive(active);
    }

    /// <summary>
    /// CatType에 해당하는 CatData를 반환합니다.
    /// </summary>
    private CatData GetCatData(CatType catType)
    {
        if (catDataList == null) return null;

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
    /// 캐릭터 이미지(버튼)를 눌렀을 때,
    /// 로컬 플레이어의 슬롯일 때만 캐릭터 종류 선택창UI를 띄워줌.
    /// </summary>
    public void OnClickCharacterImage()
    {
        if (!_isLocalPlayer)
        {
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
