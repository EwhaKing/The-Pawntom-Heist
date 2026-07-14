using UnityEngine;

/// <summary>
/// GameState에 따라 로비 씬의 UI 패널을 전환합니다.
///
/// 담당:
/// - Lobby UI 표시
/// - Loading UI 표시
/// - Ready UI 표시
///
/// 담당하지 않음:
/// - Fusion 연결
/// - 게임 상태 변경
/// - 방 생성 및 참가
/// </summary>
public class LobbyUI : MonoBehaviour
{
    [Header("Lobby UI Panels")]
    [SerializeField] private GameObject loadingPanel;
    [SerializeField] private GameObject readyPanel;

    [Header("Ready UI")]
    [SerializeField] private GameObject startGameButton;


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

        bool isHost =
            NetworkManager.Instance != null &&
            NetworkManager.Instance.Runner != null &&
            NetworkManager.Instance.Runner.IsSceneAuthority;

        startGameButton.SetActive(state == GameState.Ready && isHost);

        Debug.Log($"[LobbyUI] UI 상태 변경: {state}, Host: {isHost}");
    }

    public void OnClickStartGame()
    {
        GameManager.Instance.StartGame();
    }
}