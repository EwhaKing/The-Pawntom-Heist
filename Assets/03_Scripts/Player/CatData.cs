using UnityEngine;

/// <summary>
/// 고양이 한 종류의 정적 데이터를 저장합니다.
/// 네트워크로 직접 전송하지 않고, CatType으로 찾아서 사용합니다.
/// </summary>
[CreateAssetMenu(
    fileName = "CatData_",
    menuName = "Game/Cat Data"
)]
public class CatData : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private CatType catType;
    [SerializeField] private string displayName;

    [Header("Lobby")]
    [SerializeField] private Sprite lobbySprite;

    [Header("Character Select Popup")]
    [SerializeField] private Sprite characterSelectIllustration;

    [Header("In Game")]
    [SerializeField] private GameObject playerPrefab;

    [Header("Health & Stamina")]
    [SerializeField, Min(1f)] private float maxHealth = 100f;
    [SerializeField, Min(1f)] private float maxStamina = 100f;
    [SerializeField, Min(0f)] private float staminaRegenRate = 20f;
    [SerializeField, Min(0f)] private float sprintStaminaCost = 10f;
    [SerializeField, Min(0f)] private float jumpStaminaCost = 15f;
    [SerializeField, Min(0f)] private float parkourStaminaCost = 35f;
    [SerializeField, Min(0f)] private float exhaustedDuration = 3f;

    [Header("Description")]
    [TextArea]
    [SerializeField] private string description;

    public CatType CatType => catType;
    public string DisplayName => displayName;
    public Sprite LobbySprite => lobbySprite;
    public Sprite CharacterSelectIllustration => characterSelectIllustration;
    public GameObject PlayerPrefab => playerPrefab;
    public float MaxHealth => maxHealth;
    public float MaxStamina => maxStamina;
    public float StaminaRegenRate => staminaRegenRate;
    public float SprintStaminaCost => sprintStaminaCost;
    public float JumpStaminaCost => jumpStaminaCost;
    public float ParkourStaminaCost => parkourStaminaCost;
    public float ExhaustedDuration => exhaustedDuration;
    public string Description => description;
}
