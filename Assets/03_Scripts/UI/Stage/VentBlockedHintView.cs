using TMPro;
using UnityEngine;

/// <summary>
/// 환풍구 막힘 안내. 카오스 삼색이 아닌 내 플레이어가 환풍구 앞에서 막히면 <see cref="VentPassage.BlockedMessage"/> 를 띄우고,
/// 벗어나면 사라진다. <see cref="VentPassage"/> 의 로컬 정적 이벤트만 구독한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class VentBlockedHintView : MonoBehaviour
{
    private CanvasGroup _group;
    private TextMeshProUGUI _text;
    private string _lastMessage;
    private float _targetAlpha;

    public void Build(RuntimeUIFactory factory, Transform parent)
    {
        RectTransform root = factory.CreateRect("VentBlockedHint", parent);
        RuntimeUIFactory.Place(root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), StageHudTheme.HintPosition, new Vector2(1200f, 60f));
        _group = root.gameObject.AddComponent<CanvasGroup>();
        _group.alpha = 0f;
        _group.blocksRaycasts = false;
        _group.interactable = false;

        _text = StageHudText.Create(factory, "Message", root, StageHudTheme.HintFontSize, StageHudTheme.NoticeColor, TextAlignmentOptions.Center);
        RuntimeUIFactory.Stretch(_text.rectTransform);

        // 만들어진 시점에 이미 막혀 있으면 바로 보인다.
        if (VentPassage.LocalBlockedPassage != null)
        {
            HandleBlocked(VentPassage.LocalBlockedPassage);
        }
    }

    private void OnEnable()
    {
        VentPassage.LocalPlayerBlocked += HandleBlocked;
        VentPassage.LocalPlayerBlockCleared += HandleCleared;
    }

    private void OnDisable()
    {
        VentPassage.LocalPlayerBlocked -= HandleBlocked;
        VentPassage.LocalPlayerBlockCleared -= HandleCleared;
    }

    private void HandleBlocked(VentPassage passage)
    {
        string message = passage.BlockedMessage;

        if (message != _lastMessage)
        {
            _lastMessage = message;
            _text.text = message;
        }

        _targetAlpha = string.IsNullOrEmpty(message) ? 0f : 1f;
    }

    private void HandleCleared(VentPassage passage)
    {
        // 다른 환풍구에 막혀 있는 중이면 유지한다(한 통로의 해제가 다른 통로의 안내를 지우지 않게).
        if (VentPassage.LocalBlockedPassage == null)
        {
            _targetAlpha = 0f;
        }
    }

    private void Update()
    {
        _group.alpha = Mathf.MoveTowards(_group.alpha, _targetAlpha, Time.unscaledDeltaTime / StageHudTheme.HintFadeSeconds);
    }
}
