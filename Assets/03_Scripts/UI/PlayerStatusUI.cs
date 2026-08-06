using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 로컬 플레이어의 체력과 스태미나를 HUD에 표시합니다.
/// </summary>
public class PlayerStatusUI : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private Slider healthSlider;
    [SerializeField] private TMP_Text healthText;

    [Header("Stamina")]
    [SerializeField] private Slider staminaSlider;
    [SerializeField] private TMP_Text staminaText;

    [Header("Player Search")]
    [SerializeField, Min(0.05f)] private float searchInterval = 0.25f;

    private PlayerData _playerData;
    private float _nextSearchTime;

    private void OnEnable()
    {
        TryBindLocalPlayer();
    }

    private void Update()
    {
        if (_playerData != null || Time.unscaledTime < _nextSearchTime)
        {
            return;
        }

        _nextSearchTime = Time.unscaledTime + searchInterval;
        TryBindLocalPlayer();
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

    private void RefreshHealth(float current, float maximum)
    {
        SetSliderValue(healthSlider, current, maximum);

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
