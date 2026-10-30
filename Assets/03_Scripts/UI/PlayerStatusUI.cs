using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 로컬 플레이어의 체력과 스태미나를 HUD에 표시합니다.
/// 체력이 줄면 바가 미끄러져 내려가고 깎인 부분이 잠깐 밝게 남으며(잔상), 화면·카메라·소리 연출(<see cref="PlayerHitFeedback"/>)이 나갑니다.
/// </summary>
public class PlayerStatusUI : MonoBehaviour
{
    [Header("Character")]
    [SerializeField] private Image catImage;
    [SerializeField] private Sprite blackCatSprite;
    [SerializeField] private Sprite tabbyCatSprite;
    [SerializeField] private Sprite orangeCatSprite;
    [SerializeField] private Sprite calicoCatSprite;

    [Header("Health")]
    [SerializeField] private Slider healthSlider;
    [SerializeField] private TMP_Text healthText;

    [Header("Stamina")]
    [SerializeField] private Slider staminaSlider;
    [SerializeField] private TMP_Text staminaText;

    [Header("Player Search")]
    [SerializeField, Min(0.05f)] private float searchInterval = 0.25f;

    [Header("Hit Feedback")]
    [Tooltip("체력 바가 새 값까지 따라 내려가는 빠르기(1/초). 클수록 빨리 줄어든다")]
    [SerializeField, Min(0.1f)] private float healthFollowSpeed = 14f;

    [Tooltip("깎인 만큼 밝게 남는 잔상 바가 머무는 시간(초)")]
    [SerializeField, Min(0f)] private float chipHoldSeconds = 0.45f;

    [Tooltip("잔상 바가 줄어드는 속도(최대 체력 비율/초)")]
    [SerializeField, Min(0.05f)] private float chipDrainPerSecond = 0.8f;

    [Tooltip("잔상 바 색(깎인 부분)")]
    [SerializeField] private Color chipColor = new Color(1f, 0.85f, 0.75f, 0.9f);

    [Tooltip("피격 시 HP 글자가 잠깐 바뀌는 색")]
    [SerializeField] private Color hitTextColor = new Color(1f, 0.3f, 0.25f, 1f);

    private PlayerData _playerData;
    private float _nextSearchTime;

    private PlayerHitFeedback _hitFeedback;
    private RectTransform _chip;
    private float _healthTarget = 1f;  // 비율 0~1
    private float _healthShown = 1f;
    private float _chipShown = 1f;
    private float _chipHoldRemaining;
    private float _shakeRemaining;
    private Vector2 _barHome;
    private RectTransform _barRect;
    private Color _healthTextColor = Color.white;
    private bool _healthInitialized;

    private void Awake()
    {
        _hitFeedback = gameObject.AddComponent<PlayerHitFeedback>();

        if (healthSlider != null)
        {
            _barRect = healthSlider.GetComponent<RectTransform>();
            _barHome = _barRect.anchoredPosition;
        }

        if (healthText != null)
        {
            _healthTextColor = healthText.color;
        }

        BuildChip();
    }

    private void OnEnable()
    {
        TryBindLocalPlayer();
    }

    private void Update()
    {
        AnimateHealth(Time.unscaledDeltaTime);

        if (_playerData != null || Time.unscaledTime < _nextSearchTime)
        {
            return;
        }

        _nextSearchTime = Time.unscaledTime + searchInterval;
        TryBindLocalPlayer();
    }

    /// <summary>
    /// 깎인 부분을 밝게 남겨 두는 잔상 바. 슬라이더 채움(Fill) 바로 뒤에 같은 모양으로 깔아 둔다.
    /// 프리팹을 고치지 않도록 실행 중에 만든다.
    /// </summary>
    private void BuildChip()
    {
        if (healthSlider == null || healthSlider.fillRect == null)
        {
            return;
        }

        RectTransform fill = healthSlider.fillRect;
        GameObject chipObject = new GameObject("HealthChip", typeof(RectTransform));
        _chip = (RectTransform)chipObject.transform;
        _chip.SetParent(fill.parent, false);
        _chip.SetSiblingIndex(fill.GetSiblingIndex()); // 채움 바로 뒤(먼저 그려짐)
        _chip.anchorMin = fill.anchorMin;
        _chip.anchorMax = fill.anchorMax;
        _chip.pivot = fill.pivot;
        _chip.offsetMin = fill.offsetMin;
        _chip.offsetMax = fill.offsetMax;

        Image image = chipObject.AddComponent<Image>();
        Image fillImage = fill.GetComponent<Image>();

        if (fillImage != null)
        {
            image.sprite = fillImage.sprite;
            image.type = fillImage.type;
        }

        image.color = chipColor;
        image.raycastTarget = false;
    }

    private void AnimateHealth(float deltaTime)
    {
        if (healthSlider == null || !_healthInitialized)
        {
            return;
        }

        // 바: 새 값까지 빠르게 미끄러져 내려간다(회복은 같은 속도로 올라간다).
        float follow = 1f - Mathf.Exp(-healthFollowSpeed * deltaTime);
        _healthShown += (_healthTarget - _healthShown) * follow;

        if (Mathf.Abs(_healthTarget - _healthShown) < 0.0005f)
        {
            _healthShown = _healthTarget;
        }

        healthSlider.value = _healthShown * healthSlider.maxValue;

        // 잔상: 잠깐 머문 뒤 따라 줄어든다. 회복 중이면 바로 맞춘다.
        if (_chipHoldRemaining > 0f)
        {
            _chipHoldRemaining -= deltaTime;
        }
        else if (_chipShown > _healthShown)
        {
            _chipShown = Mathf.Max(_healthShown, _chipShown - chipDrainPerSecond * deltaTime);
        }

        if (_chipShown < _healthShown)
        {
            _chipShown = _healthShown;
        }

        if (_chip != null)
        {
            Vector2 anchorMax = _chip.anchorMax;
            anchorMax.x = _chipShown;
            _chip.anchorMax = anchorMax;
        }

        // 바 흔들림 + HP 글자 붉게 → 원래 색
        if (_shakeRemaining > 0f)
        {
            _shakeRemaining -= deltaTime;
            float strength = Mathf.Max(0f, _shakeRemaining) / 0.3f;

            if (_barRect != null)
            {
                _barRect.anchoredPosition = _barHome + new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)) * (6f * strength);
            }

            if (healthText != null)
            {
                healthText.color = Color.Lerp(_healthTextColor, hitTextColor, strength);
            }

            if (_shakeRemaining <= 0f)
            {
                if (_barRect != null)
                {
                    _barRect.anchoredPosition = _barHome;
                }

                if (healthText != null)
                {
                    healthText.color = _healthTextColor;
                }
            }
        }
    }

    private void OnDisable()
    {
        UnbindPlayer();
    }

    private void TryBindLocalPlayer()
    {
        PlayerData[] players = FindObjectsByType<PlayerData>(
            FindObjectsSortMode.None
        );

        foreach (PlayerData playerData in players)
        {
            if (playerData.Object == null ||
                !playerData.Object.HasInputAuthority)
            {
                continue;
            }

            BindPlayer(playerData);
            return;
        }
    }

    private void BindPlayer(PlayerData playerData)
    {
        if (_playerData == playerData)
        {
            return;
        }

        UnbindPlayer();

        _playerData = playerData;
        _playerData.HealthChanged += RefreshHealth;
        _playerData.StaminaChanged += RefreshStamina;

        // 붙는 순간의 값은 피격이 아니다 — 연출 없이 바로 맞춘다.
        _healthInitialized = false;

        if (_hitFeedback != null)
        {
            _hitFeedback.SetCamera(_playerData.GetComponentInChildren<Camera>(true));
        }

        RefreshCatImage(_playerData.SelectedCatType);
        RefreshHealth(_playerData.CurrentHealth, _playerData.MaxHealth);
        RefreshStamina(_playerData.CurrentStamina, _playerData.MaxStamina);
    }

    private void UnbindPlayer()
    {
        if (_playerData == null)
        {
            return;
        }

        _playerData.HealthChanged -= RefreshHealth;
        _playerData.StaminaChanged -= RefreshStamina;
        _playerData = null;
    }

    private void RefreshCatImage(CatType catType)
    {
        if (catImage == null)
        {
            return;
        }

        catImage.sprite = catType switch
        {
            CatType.BlackCat => blackCatSprite,
            CatType.TabbyCat => tabbyCatSprite,
            CatType.OrangeCat => orangeCatSprite,
            CatType.CalicoCat => calicoCatSprite,
            _ => null
        };

        catImage.enabled = catImage.sprite != null;
    }

    private void RefreshHealth(float current, float maximum)
    {
        float ratio = maximum > 0f ? Mathf.Clamp01(current / maximum) : 0f;

        if (healthSlider != null)
        {
            healthSlider.minValue = 0f;
            healthSlider.maxValue = Mathf.Max(1f, maximum);
        }

        if (!_healthInitialized)
        {
            // 처음 붙을 때는 연출 없이 바로 맞춘다.
            _healthInitialized = true;
            _healthTarget = ratio;
            _healthShown = ratio;
            _chipShown = ratio;
            SetSliderValue(healthSlider, current, maximum);
        }
        else
        {
            if (ratio < _healthTarget - 0.0001f)
            {
                // 피격: 잔상은 깎이기 전 자리에 잠깐 남고, 바·글자가 흔들리며 화면 연출이 나간다.
                _chipShown = Mathf.Max(_chipShown, _healthShown);
                _chipHoldRemaining = chipHoldSeconds;
                _shakeRemaining = 0.3f;

                if (_hitFeedback != null)
                {
                    _hitFeedback.Play(_healthTarget - ratio);
                }
            }

            _healthTarget = ratio;
        }

        if (_hitFeedback != null)
        {
            _hitFeedback.SetHealthRatio(ratio);
        }

        if (healthText != null)
        {
            healthText.text = $"HP {current:0}/{maximum:0}";
        }
    }

    private void RefreshStamina(float current, float maximum)
    {
        SetSliderValue(staminaSlider, current, maximum);

        if (staminaText != null)
        {
            staminaText.text = $"ST {current:0}/{maximum:0}";
        }
    }

    private static void SetSliderValue(
        Slider slider,
        float current,
        float maximum)
    {
        if (slider == null)
        {
            return;
        }

        slider.minValue = 0f;
        slider.maxValue = Mathf.Max(1f, maximum);
        slider.value = Mathf.Clamp(current, 0f, slider.maxValue);
    }
}
