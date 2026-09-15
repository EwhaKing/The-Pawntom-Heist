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
    // 네트워크에서 발생한 사실만 알립니다. 게임 상태 전환은 구독자가 결정합니다.
    public static event Action ConnectionStarted;
    public static event Action ConnectionSucceeded;
    public static event Action<string> SessionEnding;
    public static event Action SessionEnded;
    public static event Action SceneLoadStarted;
    public static event Action<string> SceneLoadCompleted;

    public bool CanLoadGameScene =>
        !_isStopping && _runner != null && _runner.IsRunning && _runner.IsSceneAuthority;

    private NetworkRunner _runner;
    private NetworkSceneManagerDefault _sceneManager;
    private bool _isStarting;
    private bool _isStopping;
    private bool _isQuitting;
    private Task _stopTask;
    private GameObject _runnerObject;
    private readonly Dictionary<PlayerRef, CatType> _selectedCatTypes = new();
    public NetworkRunner Runner => _runner;

    /// <summary>
    /// Fusion 네트워크 시작
    /// </summary>
    public async Task<bool> StartNetworkGame(GameMode mode, string sessionName = "TestRoom")
    {
        if (_isQuitting || _isStarting || _isStopping)
        {
            Debug.LogWarning("[NetworkManager] 네트워크 연결 또는 종료를 처리 중입니다.");
            return false;
        }

        if (_runner != null && _runner.IsRunning)
        {
            Debug.LogWarning("[NetworkManager] 이미 Fusion 세션에 접속되어 있습니다.");
            return false;
        }

        _isStarting = true;
        NetworkRunner startingRunner = null;

        try
        {
            ConnectionStarted?.Invoke();
            CreateRunner();
            startingRunner = _runner;

            SceneRef lobbyScene = SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex);

            StartGameResult result = await startingRunner.StartGame(
                new StartGameArgs
                {
                    GameMode = mode,
                    SessionName = sessionName,
                    Scene = lobbyScene,
                    SceneManager = _sceneManager
                }
            );

            // 종료 콜백이 StartGame의 비동기 결과보다 먼저 도착할 수 있습니다.
            if (_isStopping || !ReferenceEquals(startingRunner, _runner)) return false;

            if (!result.Ok)
            {
                Debug.LogError(
                    $"[NetworkManager] 세션 접속 실패: " +
                    $"{result.ShutdownReason}, {result.ErrorMessage}"
                );

                await EndSessionAsync(startingRunner, GetSessionEndMessage(result.ShutdownReason));
                return false;
            }

            Debug.Log($"[NetworkManager] 세션 접속 성공: " + $"{sessionName}, Mode: {mode}");

            ConnectionSucceeded?.Invoke();
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);

            if (!_isQuitting && (ReferenceEquals(startingRunner, null) || ReferenceEquals(startingRunner, _runner)))
                await EndSessionAsync(_runner, "서버 연결 중 오류가 발생했습니다. 다시 시도해 주세요.");
            return false;
        }
        finally
        {
            // 이전 접속 시도의 늦은 완료가 새 접속 시도의 잠금을 풀면 안 됩니다.
            if (ReferenceEquals(startingRunner, _runner)) _isStarting = false;
        }
    }

    private void CreateRunner()
    {
        if (_runner != null)
        {
            return;
        }

        // Fusion이 Runner의 GameObject를 파괴해도 영속 Manager는 유지됩니다.
        _runnerObject = new GameObject("SessionRunner");
        _runnerObject.transform.SetParent(transform);
        _runner = _runnerObject.AddComponent<NetworkRunner>();
        _runner.ProvideInput = true;
        _runner.AddCallbacks(this);

        _sceneManager =
            _runnerObject.AddComponent<NetworkSceneManagerDefault>();

        Debug.Log("[NetworkManager] NetworkRunner를 생성했습니다.");
    }

    /// <summary>
    /// Fusion을 통해 모든 플레이어를 게임 씬(map)으로 이동시킵니다.
    /// 실제 게임 시작 조건은 GameManager가 판단합니다.
    /// </summary>
    public void LoadGameScene()
    {
        if (_isStopping || _runner == null || !_runner.IsRunning)
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

        CacheSelectedCatTypes();

        SceneRef gameScene = SceneRef.FromIndex(mapSceneBuildIndex);

        Debug.Log($"[NetworkManager] 게임 씬 로드를 요청합니다. " + $"BuildIndex: {mapSceneBuildIndex}");
        _runner.LoadScene(gameScene, LoadSceneMode.Single);
    }

    private void CacheSelectedCatTypes()
    {
        _selectedCatTypes.Clear();

        if (LobbyManager.Instance == null)
        {
            Debug.LogError("[NetworkManager] 고양이 선택 정보를 저장할 LobbyManager가 없습니다.");
            return;
        }

        foreach (PlayerRef player in _runner.ActivePlayers)
        {
            if (LobbyManager.Instance.TryGetSelectedCatType(player, out CatType selectedCatType))
            {
                _selectedCatTypes[player] = selectedCatType;
                continue;
            }

            Debug.LogWarning(
                $"[NetworkManager] 플레이어의 고양이 선택 정보를 찾지 못했습니다. " +
                $"Player={player}"
            );
        }
    }

    public bool TryGetSelectedCatType(
        PlayerRef player,
        out CatType selectedCatType)
    {
        return _selectedCatTypes.TryGetValue(player, out selectedCatType);
    }

    /// <summary>정상적인 방 나가기에도 같은 정리 경로를 사용합니다.</summary>
    public Task LeaveSessionAsync()
    {
        if (_runner == null && !_isStopping) return Task.CompletedTask;
        return EndSessionAsync(_runner, "방에서 나왔습니다.");
    }

    private Task EndSessionAsync(NetworkRunner runner, string message)
    {
        if (_isQuitting || !ReferenceEquals(runner, _runner)) return Task.CompletedTask;
        if (_isStopping) return _stopTask ?? Task.CompletedTask;

        _isStopping = true;
        _stopTask = CleanupSessionAsync(runner, message);
        return _stopTask;
    }

    private async Task CleanupSessionAsync(NetworkRunner runner, string message)
    {
        // Fusion 콜백 스택 안에서 Shutdown을 재진입하지 않습니다.
        await Task.Yield();
        if (_isQuitting) return;

        try
        {
            SessionEnding?.Invoke(message);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }

        try
        {
            if (runner != null && !runner.IsShutdown)
                await runner.Shutdown(destroyGameObject: false);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
        finally
        {
            if (runner != null) runner.RemoveCallbacks(this);
            if (_runnerObject != null) Destroy(_runnerObject);
            _runnerObject = null;
            _runner = null;
            _sceneManager = null;
            _selectedCatTypes.Clear();
            _isStarting = false;
            _isStopping = false;
        }

        if (!_isQuitting) SessionEnded?.Invoke();
    }

    private static string GetSessionEndMessage(ShutdownReason reason)
    {
        return reason switch
        {
            ShutdownReason.GameNotFound => "참가할 방이 없습니다. 방이 열려 있는지 확인해 주세요.",
            ShutdownReason.GameIsFull => "방이 가득 찼습니다. 다른 방에 참가해 주세요.",
            ShutdownReason.GameClosed => "방이 종료되었습니다. 다시 방을 만들거나 참가해 주세요.",
            ShutdownReason.GameIdAlreadyExists => "같은 이름의 방이 이미 있습니다. 방 참가를 이용해 주세요.",
            _ => "서버와의 연결이 종료되었습니다. 네트워크 상태를 확인한 뒤 다시 접속해 주세요."
        };
    }

    private bool IsCurrentSession(NetworkRunner runner)
    {
        return !_isQuitting && !_isStopping && !ReferenceEquals(runner, null) && ReferenceEquals(runner, _runner);
    }

    private void OnApplicationQuit()
    {
        _isQuitting = true;
    }

    #region Fusion Callback

    /// <summary>
    /// Fusion이 내부적으로 플레이어의 입장을 감지해주는 콜백
    /// </summary>
    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (!IsCurrentSession(runner)) return;
        Debug.Log($"[Fusion] Player Joined: {player}");

        if (!SceneUtilityHelper.IsActiveScene(SceneNames.Lobby))
        {
            return;
        }

        LobbyManager.Instance?.HandlePlayerJoined(runner, player);
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        if (!IsCurrentSession(runner)) return;
        _selectedCatTypes.Remove(player);
        Debug.Log($"[Fusion] Player Left: {player}");

        if (SceneUtilityHelper.IsActiveScene(SceneNames.Lobby))
        {
            if (LobbyManager.Instance == null)
            {
                Debug.LogError(
                    "[NetworkManager] 로비 씬에 LobbyManager가 없습니다."
                );
                return;
            }

            LobbyManager.Instance.HandlePlayerLeft(runner, player);
            return;
        }

        if (SceneUtilityHelper.IsActiveScene(SceneNames.Map) && runner.IsServer)
        {
            SpawnManager.Instance.DespawnPlayer(runner, player);
        }
    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        if (!IsCurrentSession(runner)) return;
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
        _ = EndSessionAsync(runner, GetSessionEndMessage(shutdownReason));
    }

    public void OnConnectedToServer(NetworkRunner runner)
    {
        Debug.Log("[NetworkManager] Photon 서버에 연결되었습니다.");
    }

    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) //네트워크 연결이 끊어졌다.
    {
        Debug.Log($"[Fusion] Disconnect : {reason}");
        _ = EndSessionAsync(runner, "서버와의 연결이 끊어졌습니다. 네트워크 상태를 확인한 뒤 다시 접속해 주세요.");
    }

    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
    {
        Debug.LogWarning($"[Fusion] Connect failed : {reason}");
        _ = EndSessionAsync(runner, "서버에 연결하지 못했습니다. 네트워크 상태를 확인한 뒤 다시 시도해 주세요.");
    }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken)
    {
        _ = EndSessionAsync(runner, "방장과의 연결이 종료되어 방을 나갑니다.");
    }

    /// <summary>
    /// Fusion 네트워크 씬 로드가 완료되었을 때 호출됩니다.
    /// </summary>
    public void OnSceneLoadDone(NetworkRunner runner)
    {
        if (!IsCurrentSession(runner)) return;
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
        SceneLoadCompleted?.Invoke(activeScene.name);
    }

    public void OnSceneLoadStart(NetworkRunner runner)
    {
        if (!IsCurrentSession(runner)) return;
        SceneLoadStarted?.Invoke();
    }

    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }

    #endregion

    //각 씬 로드 시 행해져야 하는 일들
    private void OnLobbySceneLoaded(NetworkRunner runner)
    {
        InputManager.Instance.DisableGameplayInput();
    }

    private void OnMapSceneLoaded(NetworkRunner runner)
    {
        InputManager.Instance.EnableGameplayInput();

        if (runner.IsServer)
        {
            SpawnManager.Instance.SpawnAllPlayers(runner);

            // 플레이어 다음이어야 한다. 적의 감지 대상은 EnemyTargetRegistrar를 단 플레이어가
            // 스스로 등록하는데, 적이 먼저 태어나 첫 틱을 돌면 대상 목록이 빈 상태로 시작한다.
            SpawnManager.Instance.SpawnAllEnemies(runner);
        }
    }

    private void OnResultSceneLoaded(NetworkRunner runner)
    {
        InputManager.Instance.DisableGameplayInput();
    }
}
