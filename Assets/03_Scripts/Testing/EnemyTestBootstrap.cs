using System;
using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using UnityEngine;

/// <summary>
/// Enemy_TestScene 전용 부트스트랩.
/// 자체 NetworkRunner를 Single 모드로 띄우고 플레이어 프리팹을 스폰한다.
/// 프로덕션 NetworkManager/SpawnManager 경로를 타지 않는다.
///
/// 주의:
/// - Enemy_TestScene은 Build Settings에 없어 buildIndex가 -1이다.
///   따라서 SceneRef.FromIndex(...)를 쓰지 않고 현재 로드된 씬을 그대로 사용한다.
/// - 플레이어를 스폰한 뒤 InputManager.EnableGameplayInput()을 호출하지 않으면
///   입력이 전부 0이라 이동도 털공 생성도 일어나지 않는다.
/// </summary>
public class EnemyTestBootstrap : MonoBehaviour, INetworkRunnerCallbacks
{
    [SerializeField] private NetworkPrefabRef playerPrefab;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private bool disableOtherMainCameras = true;

    private const string SessionName = "EnemyTest";
    private const string MainCameraTag = "MainCamera";

    private NetworkRunner _runner;
    private NetworkObject _spawnedPlayer;

    /// <summary>스폰된 테스트 플레이어. 아직 스폰 전이면 null.</summary>
    public NetworkObject SpawnedPlayer => _spawnedPlayer;

    private async void Start()
    {
        if (disableOtherMainCameras)
        {
            // 러너를 띄우기 전에 처리한다.
            // 이 시점에는 플레이어가 아직 스폰되지 않았으므로
            // 태그가 걸린 카메라는 전부 씬에 원래 있던 것들이다.
            DisableSceneMainCameras();
        }

        _runner = gameObject.AddComponent<NetworkRunner>();
        _runner.ProvideInput = true;
        _runner.AddCallbacks(this);

        StartGameResult result = await _runner.StartGame(
            new StartGameArgs
            {
                GameMode = GameMode.Single,
                SessionName = SessionName,
                Scene = default,
                SceneManager = null
            }
        );

        if (!result.Ok)
        {
            Debug.LogError(
                $"[EnemyTestBootstrap] 세션 시작 실패: " +
                $"{result.ShutdownReason}, {result.ErrorMessage}"
            );
            return;
        }

        SpawnTestPlayer();
    }

    /// <summary>
    /// 테스트 플레이어를 스폰하고 게임 플레이 입력을 켠다.
    /// </summary>
    private void SpawnTestPlayer()
    {
        Transform origin = spawnPoint != null ? spawnPoint : transform;

        _spawnedPlayer = _runner.Spawn(
            playerPrefab,
            origin.position,
            origin.rotation,
            inputAuthority: _runner.LocalPlayer
        );

        if (_spawnedPlayer == null)
        {
            Debug.LogError("[EnemyTestBootstrap] 플레이어 스폰에 실패했습니다. playerPrefab을 확인하세요.");
            return;
        }

        if (InputManager.Instance == null)
        {
            Debug.LogError("[EnemyTestBootstrap] InputManager를 찾을 수 없어 입력을 활성화하지 못했습니다.");
            return;
        }

        // 이 호출이 빠지면 GetNetworkInput()이 빈 입력만 반환한다.
        InputManager.Instance.EnableGameplayInput();

        Debug.Log($"[EnemyTestBootstrap] 테스트 플레이어 스폰 완료: {origin.position}");
    }

    /// <summary>
    /// 씬에 남아 있는 MainCamera 태그 카메라를 끈다.
    /// 플레이어 프리팹의 자식 카메라도 MainCamera 태그라 Camera.main이 흔들리기 때문이다.
    /// </summary>
    private void DisableSceneMainCameras()
    {
        GameObject[] taggedCameras = GameObject.FindGameObjectsWithTag(MainCameraTag);

        for (int i = 0; i < taggedCameras.Length; i++)
        {
            GameObject taggedCamera = taggedCameras[i];

            if (taggedCamera == null || taggedCamera == gameObject)
            {
                continue;
            }

            taggedCamera.SetActive(false);

            Debug.Log($"[EnemyTestBootstrap] 씬 카메라를 비활성화했습니다: {taggedCamera.name}");
        }
    }

    #region Fusion Callback

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        if (InputManager.Instance == null)
        {
            Debug.LogError("[EnemyTestBootstrap] InputManager가 연결되지 않았습니다.");
            return;
        }

        NetworkInputData data = InputManager.Instance.GetNetworkInput();
        input.Set(data);
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }

    #endregion
}
