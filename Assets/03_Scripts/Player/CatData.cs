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

    [Header("Description")]
    [TextArea]
    [SerializeField] private string description;

    public CatType CatType => catType;
    public string DisplayName => displayName;
    public Sprite LobbySprite => lobbySprite;
    public Sprite CharacterSelectIllustration => characterSelectIllustration;
    public GameObject PlayerPrefab => playerPrefab;
    public string Description => description;
}