using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Pawntom.TutorialBridge
{
    /// <summary>
    /// 1인칭 눈꺼풀 오버레이. 위·아래 꺼풀(곡선 가장자리 텍스처)이 화면 중앙에서 만나고 벌어지며,
    /// 흐릿함(haze)은 비네트·어둡기로 표현한다. 플레이어 카메라의 후처리가 꺼져 있어(player.prefab) 블러 대신 이 방식을 쓴다.
    /// 엔딩의 암전·종료 문구도 이 오버레이가 맡는다.
    /// </summary>
    public sealed class TutorialEyelidView : MonoBehaviour
    {
        private const float LidHeightRatio = 0.8f;
        private const float LidCurve = 0.36f;
        private const float LidFeather = 0.06f;

        private RectTransform _root;
        private RectTransform _top;
        private RectTransform _bottom;
        private RawImage _vignette;
        private Image _haze;
        private Image _blackout;
        private CanvasGroup _endCard;
        private TextMeshProUGUI _endTitle;
        private TextMeshProUGUI _endSubtitle;

        public void Build(TutorialUIFactory factory, Transform parent)
        {
            _root = factory.CreateRect("Eyelids", parent);
            TutorialUIFactory.Stretch(_root);

            _haze = factory.CreateImage("Haze", _root, new Color(0f, 0f, 0f, 0f));
            TutorialUIFactory.Stretch(_haze.rectTransform);

            _vignette = factory.CreateRawImage("Vignette", _root, factory.CreateVignetteTexture(256, 0.35f, 1.15f), new Color(1f, 1f, 1f, 0f));
            TutorialUIFactory.Stretch(_vignette.rectTransform);

            Texture2D lid = factory.CreateEyelidTexture(512, 256, LidCurve, LidFeather);

            RawImage top = factory.CreateRawImage("TopLid", _root, lid, Color.white);
            _top = top.rectTransform;
            _top.anchorMin = new Vector2(0f, 0.5f);
            _top.anchorMax = new Vector2(1f, 0.5f);
            _top.pivot = new Vector2(0.5f, 0f);

            RawImage bottom = factory.CreateRawImage("BottomLid", _root, lid, Color.white);
            bottom.uvRect = new Rect(0f, 1f, 1f, -1f); // 같은 텍스처를 위아래로 뒤집어 아래 꺼풀로 쓴다
            _bottom = bottom.rectTransform;
            _bottom.anchorMin = new Vector2(0f, 0.5f);
            _bottom.anchorMax = new Vector2(1f, 0.5f);
            _bottom.pivot = new Vector2(0.5f, 1f);

            _blackout = factory.CreateImage("Blackout", _root, Color.black);
            TutorialUIFactory.Stretch(_blackout.rectTransform);

            RectTransform card = factory.CreateRect("EndCard", _root);
            TutorialUIFactory.Stretch(card);
            _endCard = card.gameObject.AddComponent<CanvasGroup>();
            _endCard.alpha = 0f;
            _endCard.blocksRaycasts = false;

            _endTitle = factory.CreateText("Title", card, 54f, TutorialTheme.CommandColor, TextAlignmentOptions.Center);
            TutorialUIFactory.Place(_endTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 26f), new Vector2(1200f, 80f));

            _endSubtitle = factory.CreateText("Subtitle", card, 24f, TutorialTheme.RadioIdleColor, TextAlignmentOptions.Center);
            _endSubtitle.characterSpacing = 12f;
            TutorialUIFactory.Place(_endSubtitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -36f), new Vector2(1200f, 40f));
        }

        /// <param name="openness">0=감음 ~ 1=완전히 뜸</param>
        /// <param name="haze">0=선명 ~ 1=흐림</param>
        public void SetOpenness(float openness, float haze)
        {
            float height = _root.rect.height;

            if (height <= 0f)
            {
                height = TutorialTheme.ReferenceResolution.y;
            }

            float lidHeight = height * LidHeightRatio;
            float closedEdge = -(LidCurve * lidHeight) - height * 0.03f; // 감았을 때 가운데까지 덮고 조금 겹친다
            float openEdge = height * 0.5f + 4f;                         // 떴을 때 화면 밖
            float edge = Mathf.LerpUnclamped(closedEdge, openEdge, Mathf.Clamp01(openness));

            // 가로로 조금 넘치게 그려 꺼풀 양 끝의 곡선이 화면 모서리에 걸리지 않게 한다.
            Vector2 size = new Vector2(height * 0.35f, lidHeight);
            _top.sizeDelta = size;
            _bottom.sizeDelta = size;
            _top.anchoredPosition = new Vector2(0f, edge);
            _bottom.anchoredPosition = new Vector2(0f, -edge);

            bool lidsVisible = openness < 0.999f;
            _top.gameObject.SetActive(lidsVisible);
            _bottom.gameObject.SetActive(lidsVisible);

            haze = Mathf.Clamp01(haze);
            _vignette.color = new Color(1f, 1f, 1f, haze * 0.95f);
            _haze.color = new Color(0f, 0f, 0f, haze * 0.45f);
        }

        public void SetBlackout(float alpha)
        {
            alpha = Mathf.Clamp01(alpha);
            _blackout.color = new Color(0f, 0f, 0f, alpha);
            _blackout.gameObject.SetActive(alpha > 0f);
        }

        public void SetEndCard(float alpha, string title, string subtitle)
        {
            if (_endTitle.text != title)
            {
                _endTitle.text = title;
            }

            if (_endSubtitle.text != subtitle)
            {
                _endSubtitle.text = subtitle;
            }

            _endCard.alpha = Mathf.Clamp01(alpha);
        }
    }
}
