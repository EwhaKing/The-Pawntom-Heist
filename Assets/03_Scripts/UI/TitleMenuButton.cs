using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 타이틀 메뉴 버튼 하나. 기본(검은 막대)·선택(흰 막대) 두 이미지를 바꿔 끼우고,
/// 마우스를 올리면 메뉴에 "이 버튼을 선택"이라고 알린다. 실제 동작은 같은 오브젝트의 <see cref="Button"/> onClick.
/// </summary>
[RequireComponent(typeof(Image))]
[RequireComponent(typeof(Button))]
public class TitleMenuButton : MonoBehaviour, IPointerEnterHandler
{
    [SerializeField] private Sprite normalSprite;
    [SerializeField] private Sprite highlightSprite;

    [Tooltip("임시 잠금. 켜면 클릭·Enter 모두 막고 메뉴에서 흐리게 보인다. 개발이 끝나면 끈다.")]
    [SerializeField] private bool underDevelopment;

    public bool UnderDevelopment => underDevelopment;

    private Image _image;
    private Button _button;
    private TitleMenuUI _menu;

    private bool _initialized;

    public Button Button
    {
        get
        {
            Initialize();
            return _button;
        }
    }

    private void Awake()
    {
        Initialize();
    }

    // TitleMenuUI.Awake 가 이 버튼의 Awake 보다 먼저 실행되면 SetHighlighted 가 먼저 불린다(오브젝트 사이 Awake 순서는 정해져 있지 않다).
    // 그때 _image 가 비어 NullReferenceException 이 나고, 콘솔 Error Pause 가 켜져 있으면 플레이가 2프레임째에 멈춘다.
    // 그래서 필요한 참조를 쓰기 직전에 한 번만 채운다.
    private void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        _image = GetComponent<Image>();
        _button = GetComponent<Button>();

        // 상태 표시는 이 스크립트가 맡는다(Unity 기본 색 변화와 겹치지 않게).
        _button.transition = Selectable.Transition.None;
        Navigation none = new Navigation { mode = Navigation.Mode.None };
        _button.navigation = none;
        _button.interactable = !underDevelopment;
    }

    public void Bind(TitleMenuUI menu)
    {
        _menu = menu;
    }

    public void SetHighlighted(bool highlighted)
    {
        Initialize();

        Sprite sprite = highlighted ? highlightSprite : normalSprite;

        if (_image.sprite != sprite)
        {
            _image.sprite = sprite;

            // 두 이미지 폭이 1px 다르다. 원본 크기로 맞춰 1:1 픽셀로 그린다(늘이거나 줄이면 글자가 흐려진다).
            _image.SetNativeSize();
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_menu != null)
        {
            _menu.Highlight(this);
        }
    }
}
