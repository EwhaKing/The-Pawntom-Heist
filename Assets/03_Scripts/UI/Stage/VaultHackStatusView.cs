using Hacking;
using Pawntom.Settings;
using TMPro;
using UnityEngine;

/// <summary>
/// 금고 해킹 진행/결과 안내. 미니게임 팝업 자체는 HackingManager 가 그린다 — 이 뷰는 그 앞뒤 상태를 문구로 알린다:
/// 해킹 실패(쿨타임) · 성공 후 개봉 확인 대기 · 개봉 확인 · 개봉 거부(거리 등). 사망으로 취소된 해킹은 알리지 않는다.
/// <see cref="VaultHackFlow"/> 의 로컬 정적 이벤트만 구독한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class VaultHackStatusView : MonoBehaviour
{
    private const float PendingHoldSeconds = 6f; // 개봉 확인/거부가 오면 바뀐다. 안 와도 결국 사라진다.

    private readonly TimedMessage _message = new TimedMessage();

    private CanvasGroup _group;
    private TextMeshProUGUI _text;
    private string _shownText;

    public void Build(RuntimeUIFactory factory, Transform parent)
    {
        RectTransform root = factory.CreateRect("VaultHackStatus", parent);
        RuntimeUIFactory.Place(root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), StageHudTheme.HintPosition, new Vector2(1200f, 60f));
        _group = root.gameObject.AddComponent<CanvasGroup>();
        _group.alpha = 0f;
        _group.blocksRaycasts = false;
        _group.interactable = false;

        _text = StageHudText.Create(factory, "Message", root, StageHudTheme.HintFontSize, StageHudTheme.TextColor, TextAlignmentOptions.Center);
        RuntimeUIFactory.Stretch(_text.rectTransform);
    }

    private void OnEnable()
    {
        VaultHackFlow.LocalHackStarted += HandleStarted;
        VaultHackFlow.LocalHackFinished += HandleFinished;
        VaultHackFlow.LocalOpenConfirmed += HandleConfirmed;
        VaultHackFlow.LocalOpenRejected += HandleRejected;
    }

    private void OnDisable()
    {
        VaultHackFlow.LocalHackStarted -= HandleStarted;
        VaultHackFlow.LocalHackFinished -= HandleFinished;
        VaultHackFlow.LocalOpenConfirmed -= HandleConfirmed;
        VaultHackFlow.LocalOpenRejected -= HandleRejected;
    }

    private void HandleStarted(VaultHackFlow flow)
    {
        _message.Clear();
    }

    private void HandleFinished(VaultHackFlow flow, bool success)
    {
        if (success)
        {
            Show("해킹 성공 — 금고를 여는 중…", StageHudTheme.SuccessColor, PendingHoldSeconds);
            return;
        }

        // 사망으로 팝업이 닫힌 경우는 실패 안내를 하지 않는다(쿨타임도 없다).
        if (HackingManager.IsCancellingForPlayerDeath)
        {
            return;
        }

        Show("해킹 실패 — 잠시 후 다시 시도할 수 있습니다", StageHudTheme.DangerColor, StageHudTheme.ToastHoldSeconds);
    }

    private void HandleConfirmed(VaultHackFlow flow)
    {
        Show("금고가 열렸습니다", StageHudTheme.SuccessColor, StageHudTheme.ToastHoldSeconds);
    }

    private void HandleRejected(VaultHackFlow flow)
    {
        Show("금고를 열지 못했습니다 — 금고 가까이에서 다시 시도하세요", StageHudTheme.DangerColor, StageHudTheme.ToastHoldSeconds + 1f);
    }

    private void Show(string text, Color color, float holdSeconds)
    {
        _message.Show(text, Time.unscaledTime, holdSeconds);

        if (text != _shownText)
        {
            _shownText = text;
            _text.text = text;
        }

        _text.color = color;
    }

    private void Update()
    {
        _group.alpha = _message.Alpha(Time.unscaledTime, StageHudTheme.HintFadeSeconds);
    }
}
