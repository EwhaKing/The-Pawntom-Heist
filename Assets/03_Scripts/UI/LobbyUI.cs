using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

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

    [Header("Connection Notice")]
    [SerializeField] private GameObject connectionNoticePanel;
    [SerializeField] private TMP_Text connectionNoticeText;
    [SerializeField, Min(0f)] private float connectionNoticeHoldSeconds = 3f;
    [SerializeField, Min(0f)] private float connectionNoticeFadeSeconds = 0.4f;

    private CanvasGroup _connectionNoticeGroup;
    private Coroutine _connectionNoticeFade;
    private bool _hasStarted;

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
        if (_hasStarted) RefreshUI(GameManager.Instance.CurrentState);
    }

    private void OnDisable()
    {
        GameManager.OnStateChanged -= HandleGameStateChanged;
        StopConnectionNoticeFade();
    }

    private void Start()
    {
        _hasStarted = true;
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
        RefreshConnectionNotice(state);
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

    private void RefreshConnectionNotice(GameState state)
    {
        StopConnectionNoticeFade();
        if (connectionNoticePanel == null) return;

        string message = GameManager.Instance.ConnectionMessage;
        bool show = state == GameState.Lobby
            && !string.IsNullOrEmpty(message);

        if (!show || connectionNoticeText == null)
        {
            connectionNoticePanel.SetActive(false);
            return;
        }

        if (_connectionNoticeGroup == null)
        {
            _connectionNoticeGroup = connectionNoticePanel.GetComponent<CanvasGroup>();
            if (_connectionNoticeGroup == null)
                _connectionNoticeGroup = connectionNoticePanel.AddComponent<CanvasGroup>();
        }

        _connectionNoticeGroup.alpha = 1f;
        _connectionNoticeGroup.interactable = true;
        _connectionNoticeGroup.blocksRaycasts = true;
        connectionNoticeText.text = message;
        connectionNoticePanel.SetActive(true);
        _connectionNoticeFade = StartCoroutine(FadeOutConnectionNotice());
    }

    private IEnumerator FadeOutConnectionNotice()
    {
        // UI 안내는 게임의 일시정지나 Time.timeScale에 영향받지 않습니다.
        yield return new WaitForSecondsRealtime(connectionNoticeHoldSeconds);

        float elapsed = 0f;
        while (elapsed < connectionNoticeFadeSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            _connectionNoticeGroup.alpha = 1f - Mathf.Clamp01(elapsed / connectionNoticeFadeSeconds);
            yield return null;
        }

        _connectionNoticeFade = null;
        OnClickCloseConnectionNotice();
    }

    private void StopConnectionNoticeFade()
    {
        if (_connectionNoticeFade == null) return;
        StopCoroutine(_connectionNoticeFade);
        _connectionNoticeFade = null;
    }

    public void OnClickCloseConnectionNotice()
    {
        StopConnectionNoticeFade();
        GameManager.Instance.DismissConnectionMessage();
        if (connectionNoticePanel != null)
            connectionNoticePanel.SetActive(false);
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