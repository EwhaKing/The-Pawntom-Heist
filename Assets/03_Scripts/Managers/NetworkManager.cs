using UnityEngine;
using UnityEngine.SceneManagement;
using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// NetworkManager : Fusion 네트워크의 생성, 시작, 종료 및 콜백 수신을 담당합니다.
///
/// 담당:
/// - Fusion NetworkRunner 관리
/// - Host / Client 시작
/// - Network Shutdown
/// - Fusion Callback 수신
/// - Callback을 각 Manager에게 전달
///
/// 담당하지 않음:
/// - Player 생성/삭제      -> SpawnManager
/// - Input 생성            -> InputManager
/// - Lobby 처리            -> LobbyManager
/// - Game State 관리       -> GameManager
/// </summary>
public class NetworkManager : PawntomSingleton<NetworkManager>, INetworkRunnerCallbacks
{
    private NetworkRunner _runner;
    private NetworkSceneManagerDefault _sceneManager;
    private bool _isStarting;
    public NetworkRunner Runner => _runner;

    /// <summary>
    /// Fusion 네트워크 시작
    /// </summary>
    public async Task<bool> StartNetworkGame(GameMode mode, string sessionName = "TestRoom")
    {
        if (_isStarting)
        {
            Debug.LogWarning("[NetworkManager] 이미 네트워크 연결을 시도 중입니다.");
            return false;
        }

        if (_runner != null && _runner.IsRunning)
        {
            Debug.LogWarning("[NetworkManager] 이미 Fusion 세션에 접속되어 있습니다.");
            return false;
        }

        _isStarting = true;
        GameManager.Instance.EnterLoading();

        try
        {
            CreateRunner();

            SceneRef lobbyScene = SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex);

            StartGameResult result = await _runner.StartGame(
                new StartGameArgs
                {
                    GameMode = mode,
                    SessionName = sessionName,
                    Scene = lobbyScene,
                    SceneManager = _sceneManager
                }
            );

            if (!result.Ok)
            {
                Debug.LogError(
                    $"[NetworkManager] 세션 접속 실패: " +
                    $"{result.ShutdownReason}, {result.ErrorMessage}"
                );

                GameManager.Instance.EnterLobby();
                return false;
            }

            Debug.Log($"[NetworkManager] 세션 접속 성공: " + $"{sessionName}, Mode: {mode}");

            GameManager.Instance.EnterReady();
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);

            GameManager.Instance.EnterLobby();
            return false;
        }
        finally
        {
            _isStarting = false;
        }
    }

    private void CreateRunner()
    {
        if (_runner != null)
        {
            return;
        }

        _runner = gameObject.AddComponent<NetworkRunner>();
        _runner.ProvideInput = true;
        _runner.AddCallbacks(this);

        _sceneManager =
            gameObject.AddComponent<NetworkSceneManagerDefault>();

        Debug.Log("[NetworkManager] NetworkRunner를 생성했습니다.");
    }

    /// <summary>
    /// Fusion을 통해 모든 플레이어를 게임 씬(map)으로 이동시킵니다.
    /// 실제 게임 시작 조건은 GameManager가 판단합니다.
    /// </summary>
    public void LoadGameScene()
    {
        if (_runner == null || !_runner.IsRunning)
        {
            Debug.LogError("[NetworkManager] 실행 중인 NetworkRunner가 없습니다.");
            return;
        }

        if (!_runner.IsSceneAuthority)
        {
            Debug.LogWarning("[NetworkManager] Host만 네트워크 씬을 변경할 수 있습니다.");
            return;
        }

        int mapSceneBuildIndex = SceneUtilityHelper.GetBuildIndex(SceneNames.Map);

        if (mapSceneBuildIndex < 0)
        {
            Debug.LogError($"[NetworkManager] {SceneNames.Map} 씬이 Build Settings에 없습니다.");
            return;
        }

        SceneRef gameScene = SceneRef.FromIndex(mapSceneBuildIndex);

        Debug.Log($"[NetworkManager] 게임 씬 로드를 요청합니다. " + $"BuildIndex: {mapSceneBuildIndex}");
        _runner.LoadScene(gameScene, LoadSceneMode.Single);
    }

    #region Fusion Callback
    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log($"[Fusion] Player Joined: {player}");

        if (!SceneUtilityHelper.IsActiveScene(SceneNames.Map)) //플레이어는 맵 씬에서만 spawn됩니다.
        {
            return;
        }

        if (runner.IsServer) //호스트만 플레이어를 spawn합니다.
        {
            SpawnManager.Instance.SpawnPlayer(runner, player);
        }
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        if (!runner.IsServer)
        {
            return;
        }

        Debug.Log($"[Fusion] Player Left: {player}");

        SpawnManager.Instance.DespawnPlayer(runner, player);
    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        if (InputManager.Instance == null)
        {
            Debug.LogError("[NetworkManager] InputManager가 연결되지 않았습니다.");
            return;
        }

        NetworkInputData data = InputManager.Instance.GetNetworkInput();
        input.Set(data);
    }

    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) //Runner가 완전히 종료됐다.
    {
        Debug.Log($"[Fusion] Shutdown : {shutdownReason}");
    }

    public void OnConnectedToServer(NetworkRunner runner)
    {
        Debug.Log("[NetworkManager] Photon 서버에 연결되었습니다.");
    }

    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) //네트워크 연결이 끊어졌다.
    {
        Debug.Log($"[Fusion] Disconnect : {reason}");
    }

    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }

    /// <summary>
    /// Fusion 네트워크 씬 로드가 완료되었을 때 호출됩니다.
    /// </summary>
    public void OnSceneLoadDone(NetworkRunner runner)
    {
        Scene activeScene = SceneManager.GetActiveScene();

        Debug.Log($"[NetworkManager] 네트워크 씬 로드가 완료되었습니다. " + $"Scene: {activeScene.name}, BuildIndex: {activeScene.buildIndex}");

        switch (activeScene.name)
        {
            // case SceneNames.Lobby:
            //     OnLobbySceneLoaded(runner);
            //     break;

            case SceneNames.Map:
                OnMapSceneLoaded(runner);
                break;

            // case SceneNames.Result:
            //     OnResultSceneLoaded(runner);
            //     break;

            default:
                Debug.LogWarning(
                    $"[NetworkManager] 별도 로드 처리가 없는 씬입니다. " +
                    $"Scene: {activeScene.name}"
                );
                break;
        }
    }

    public void OnSceneLoadStart(NetworkRunner runner)
    {
        //GameManager.Instance.EnterLoading();
    }

    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }

    #endregion

    private void OnMapSceneLoaded(NetworkRunner runner)
    {
        if (runner.IsServer)
        {
            SpawnManager.Instance.SpawnAllPlayers(runner);
        }

        GameManager.Instance.EnterInGame();
    }
}