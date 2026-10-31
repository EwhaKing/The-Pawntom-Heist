using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Pawntom.TutorialBridge
{
    /// <summary>흑백 월드 위의 회색 베일, 주황색 실패 문구와 Space 재시도 안내.</summary>
    public sealed class TutorialFailureView : MonoBehaviour
    {
        private Image _veil;
        private CanvasGroup _message;
        public const string RetryPrompt = "space를 눌러 다시 시도합시다.";
        private CanvasGroup _prompt;
        private Action _retry;
        private bool _ready;
        private bool _confirmReleased;
        private float _openedAt;

        public void Build(TutorialUIFactory factory, Action retry)
        {
            _retry = retry;
            _veil = factory.CreateImage("Death Gray Veil", transform, new Color(0.45f, 0.45f, 0.45f, 0.22f));
            TutorialUIFactory.Stretch(_veil.rectTransform);
            _veil.raycastTarget = true;
            TextMeshProUGUI title = factory.CreateText("Mission Failed", transform, 100f,
                new Color(1f, 0.26f, 0.10f), TextAlignmentOptions.Center);
            title.text = "Mission Failed";
            title.fontStyle = FontStyles.Bold;
            title.enableAutoSizing = true;
            title.fontSizeMin = 45f;
            title.fontSizeMax = 100f;
            TutorialUIFactory.Place(title.rectTransform, Vector2.one * 0.5f, Vector2.one * 0.5f,
                Vector2.zero, new Vector2(1300f, 160f));
            _message = title.gameObject.AddComponent<CanvasGroup>();

            TextMeshProUGUI prompt = factory.CreateText("Retry Prompt", transform, 32f,
                new Color(0.95f, 0.95f, 0.95f), TextAlignmentOptions.Center);
            prompt.text = RetryPrompt;
            TutorialUIFactory.Place(prompt.rectTransform, Vector2.one * 0.5f, Vector2.one * 0.5f,
                new Vector2(0f, -110f), new Vector2(1100f, 64f));
            _prompt = prompt.gameObject.AddComponent<CanvasGroup>();
            gameObject.AddComponent<GraphicRaycaster>();
            Hide();
        }

        public void ShowDeath()
        {
            gameObject.SetActive(true);
            _ready = false;
            _message.alpha = _prompt.alpha = 0f;
            _veil.color = new Color(0.45f, 0.45f, 0.45f, 0.22f);
        }

        public void ShowFailure()
        {
            _openedAt = Time.unscaledTime;
            _ready = true;
            _confirmReleased = false;
        }

        public void Hide()
        {
            _ready = false;
            gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!_ready) return;
            float time = Time.unscaledTime - _openedAt;
            _message.alpha = Mathf.Clamp01(time / 0.65f);
            _prompt.alpha = Mathf.Clamp01((time - 0.45f) / 0.3f);

            Keyboard keyboard = Keyboard.current;
            bool held = keyboard != null && (keyboard.enterKey.isPressed || keyboard.spaceKey.isPressed);
            if (!held) _confirmReleased = true;
            if (time >= 0.75f && _confirmReleased && keyboard != null &&
                (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame)) RequestRetry();
        }

        private void RequestRetry()
        {
            if (!_ready || Time.unscaledTime - _openedAt < 0.75f) return;
            _ready = false;
            _retry?.Invoke();
        }

        public void AllowRetry() => _ready = true;
    }
}
