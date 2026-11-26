using Pawntom.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 분전반 접속 캐스팅 바(GAME_DESIGN §3.4·§3.6): 시계방향으로 차오르는 원형 게이지 + 퍼센트 + 문구.
/// <see cref="SecurityNode"/> 의 로컬 정적 이벤트만 구독한다(내 캐스팅만 그린다).
/// 흐름: 시작 → 진행 → (취소: 바로 사라짐) / (완료: 100% 로 머물며 상태 권한 확정 대기 → 접속 완료 또는 실패 문구).
/// </summary>
[DisallowMultipleComponent]
public sealed class CastBarView : MonoBehaviour
{
    private CanvasGroup _group;
    private Image _fill;
    private TextMeshProUGUI _percent;
    private TextMeshProUGUI _label;

    private SecurityNode _node;      // 지금 그리는 분전반
    private bool _casting;
    private bool _awaiting;          // 완료 요청을 보냈고 확정 대기
    private float _resultUntil;      // 결과 문구를 보여 주는 마감(unscaled)
    private float _targetAlpha;
    private int _lastPercent = -1;
    private string _lastLabel;

    public void Build(RuntimeUIFactory factory, Transform parent, StageHudSprites sprites)
    {
        RectTransform root = factory.CreateRect("CastBar", parent);
        RuntimeUIFactory.Place(root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        _group = root.gameObject.AddComponent<CanvasGroup>();
        _group.alpha = 0f;
        _group.blocksRaycasts = false;
        _group.interactable = false;

        Vector2 gauge = new Vector2(StageHudTheme.GaugeSize, StageHudTheme.GaugeSize);

        Image track = factory.CreateImage("Track", root, StageHudTheme.GaugeTrackColor);
        track.sprite = sprites.Ring;
        RuntimeUIFactory.Place(track.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), StageHudTheme.GaugePosition, gauge);

        _fill = factory.CreateImage("Fill", root, StageHudTheme.GaugeFillColor);
        _fill.sprite = sprites.Ring;
        _fill.type = Image.Type.Filled;
        _fill.fillMethod = Image.FillMethod.Radial360;
        _fill.fillOrigin = (int)Image.Origin360.Top;
        _fill.fillClockwise = true;
        _fill.fillAmount = 0f;
        RuntimeUIFactory.Place(_fill.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), StageHudTheme.GaugePosition, gauge);

        _percent = StageHudText.Create(factory, "Percent", root, 30f, StageHudTheme.TextColor, TextAlignmentOptions.Center);
        RuntimeUIFactory.Place(_percent.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), StageHudTheme.GaugePosition, gauge);

        _label = StageHudText.Create(factory, "Label", root, 30f, StageHudTheme.TextColor, TextAlignmentOptions.Center);
        RuntimeUIFactory.Place(_label.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, StageHudTheme.GaugeLabelOffsetY), new Vector2(700f, 44f));

        // 만들어진 시점에 이미 캐스팅 중이면 이어서 그린다.
        SecurityNode current = SecurityNode.LocalCastingNode;

        if (current != null)
        {
            HandleStarted(current);
            HandleProgress(current, current.LocalCastProgressValue);
        }
    }

    private void OnEnable()
    {
        SecurityNode.LocalCastStarted += HandleStarted;
        SecurityNode.LocalCastProgress += HandleProgress;
        SecurityNode.LocalCastCanceled += HandleCanceled;
        SecurityNode.LocalCastCompleted += HandleCompleted;
        SecurityNode.LocalConnectRejected += HandleRejected;
        SecurityNode.Connected += HandleConnected;
    }

    private void OnDisable()
    {
        SecurityNode.LocalCastStarted -= HandleStarted;
        SecurityNode.LocalCastProgress -= HandleProgress;
        SecurityNode.LocalCastCanceled -= HandleCanceled;
        SecurityNode.LocalCastCompleted -= HandleCompleted;
        SecurityNode.LocalConnectRejected -= HandleRejected;
        SecurityNode.Connected -= HandleConnected;
    }

    private void HandleStarted(SecurityNode node)
    {
        _node = node;
        _casting = true;
        _awaiting = false;
        _resultUntil = 0f;
        SetLabel(node.CastLabel, StageHudTheme.TextColor);
        SetProgress(0f, StageHudTheme.GaugeFillColor);
        _targetAlpha = 1f;
    }

    private void HandleProgress(SecurityNode node, float progress)
    {
        if (node != _node)
        {
            return;
        }

        SetProgress(progress, StageHudTheme.GaugeFillColor);
    }

    private void HandleCanceled(SecurityNode node)
    {
        if (node != _node)
        {
            return;
        }

        _casting = false;
        _awaiting = false;
        _targetAlpha = 0f;
    }

    private void HandleCompleted(SecurityNode node)
    {
        if (node != _node)
        {
            return;
        }

        _casting = false;
        _awaiting = true;
        SetProgress(1f, StageHudTheme.GaugeFillColor);
    }

    private void HandleRejected(SecurityNode node)
    {
        if (node != _node)
        {
            return;
        }

        ShowResult("접속에 실패했습니다", StageHudTheme.DangerColor);
    }

    private void HandleConnected(SecurityNode node)
    {
        // 확정은 모든 피어에 온다. 내가 기다리던 분전반일 때만 결과를 보여 준다.
        if (node != _node || (!_awaiting && !_casting))
        {
            return;
        }

        ShowResult("분전반 접속 완료", StageHudTheme.SuccessColor);
    }

    private void ShowResult(string text, Color color)
    {
        _casting = false;
        _awaiting = false;
        _resultUntil = Time.unscaledTime + StageHudTheme.CastResultHoldSeconds;
        SetLabel(text, color);
        SetProgress(color == StageHudTheme.DangerColor ? _fill.fillAmount : 1f, color);
        _targetAlpha = 1f;
    }

    private void SetProgress(float progress, Color color)
    {
        _fill.fillAmount = StageHudMath.ArcDegrees(progress) / 360f;
        _fill.color = color;
        int percent = StageHudMath.Percent(progress);

        if (percent != _lastPercent)
        {
            _lastPercent = percent;
            _percent.SetText("{0}%", percent);
        }
    }

    private void SetLabel(string text, Color color)
    {
        if (text != _lastLabel)
        {
            _lastLabel = text;
            _label.text = text;
        }

        _label.color = color;
    }

    private void Update()
    {
        if (_targetAlpha > 0f && !_casting && !_awaiting && Time.unscaledTime >= _resultUntil)
        {
            _targetAlpha = 0f;
        }

        _group.alpha = Mathf.MoveTowards(_group.alpha, _targetAlpha, StageHudTheme.CastFadeSpeed * Time.unscaledDeltaTime);
    }
}
