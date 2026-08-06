using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// GameState에 따라 로비 씬의 UI 패널을 전환합니다.
///
/// 담당:
/// - Lobby UI 표시
/// - Loading UI 표시
/// - Ready UI 표시
/// 
/// </summary>
public class LobbyUI : MonoBehaviour
{
    [Header("Lobby UI Panels")]
    [SerializeField] private GameObject loadingPanel;
    [SerializeField] private GameObject readyPanel;

    [Header("Ready UI")]
    [SerializeField] Button startGameButton;
    [SerializeField] GameObject readyButton;

    public static LobbyUI Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }


    private void OnEnable()
    {
        GameManager.OnStateChanged += HandleGameStateChanged;
    }

    private void OnDisable()
    {
        GameManager.OnStateChanged -= HandleGameStateChanged;
    }

    private void Start()
    {
        RefreshUI(GameManager.Instance.CurrentState);
        RefreshButtons();
    }

    private void HandleGameStateChanged(
        GameState previousState,
        GameState newState)
    {
        RefreshUI(newState);
    }

    private void RefreshUI(GameState state)
    {
        loadingPanel.SetActive(state == GameState.Loading);
        readyPanel.SetActive(state == GameState.Ready);

        RefreshButtons();
    }

    public void RefreshButtons()
    {
        bool isReadyState =
            GameManager.Instance != null &&
            GameManager.Instance.CurrentState == GameState.Ready;

        LobbyManager lobbyManager = LobbyManager.Instance;

        if (!isReadyState || lobbyManager == null)
        {
            startGameButton.gameObject.SetActive(false);
            readyButton.SetActive(false);
            return;
        }

        LobbyPlayerData localPlayerData =
            lobbyManager.GetLocalPlayerData();

        // 로컬 플레이어 데이터 등록 전에는 두 버튼 모두 숨김
        if (localPlayerData == null)
        {
            startGameButton.gameObject.SetActive(false);
            readyButton.SetActive(false);
            return;
        }

        bool isHost = lobbyManager.IsLocalPlayerHost();

        // 호스트: Showtime
        startGameButton.gameObject.SetActive(isHost);

        // 클라이언트: Ready
        readyButton.SetActive(!isHost);

        if (isHost)
        {
            startGameButton.interactable =
                lobbyManager.CanStartGame();
        }

        Debug.Log(
            $"[LobbyUI] 버튼 갱신 | " +
            $"LocalPlayer={localPlayerData.PlayerRef}, " +
            $"IsHost={isHost}"
        );
    }

    public void OnClickStartGame()
    {
        GameManager.Instance.StartGame();
    }

    public void OnClickReady()
    {
        LobbyPlayerData player = LobbyManager.Instance.GetLocalPlayerData();

        if (player == null)
            return;

        player.RequestToggleReady();
    }
}