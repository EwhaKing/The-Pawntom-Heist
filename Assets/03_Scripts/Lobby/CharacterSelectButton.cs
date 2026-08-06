using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 캐릭터 선택 팝업에 표시되는 고양이 선택 버튼.
/// </summary>
public class CharacterSelectButton : MonoBehaviour
{
    [SerializeField] private CatType catType;
    [SerializeField] private Button button;

    private CharacterSelectPopup _popup;

    /// <summary>
    /// 이 버튼이 선택 결과를 전달할 팝업을 연결합니다.
    /// </summary>
    public void Initialize(CharacterSelectPopup popup)
    {
        _popup = popup;
    }

    private void Awake()
    {
        if (button == null)
        {
            button = GetComponent<Button>();
        }

        button.onClick.AddListener(HandleClick);
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(HandleClick);
        }
    }

    private void HandleClick()
    {
        if (_popup == null)
        {
            Debug.LogError(
                "[CharacterSelectButton] CharacterSelectPopup이 연결되지 않았습니다."
            );
            return;
        }

        _popup.PreviewCharacter(catType);
    }
}
