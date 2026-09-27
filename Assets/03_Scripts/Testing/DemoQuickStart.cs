#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Threading.Tasks;
using Fusion;
using UnityEngine;

/// <summary>
/// 데모 테스트용 빠른 진입.
///
/// Lobby_Scene에서 P를 누르면 혼자 Host로 접속하고, Ready 대기 없이 곧바로 게임 씬
/// (SceneNames.Map)으로 진입한다. 로비의 "방 만들기 → 시작" 버튼과 같은 경로
/// (NetworkManager.StartNetworkGame → GameManager.StartGame)를 코드로 대신 누르는 것이라
/// 접속·씬 로드·플레이어/적 스폰은 기존 흐름 그대로 동작한다.
///
/// 사용:
/// - Bootstrap_Scene에서 Play를 시작한다. Bootstrap이 GameManager/NetworkManager/InputManager를 만든다.
/// - 씬에 오브젝트를 둘 필요가 없다. 첫 씬이 로드된 직후 스스로 하나 만들어져 씬을 넘어 유지된다.
///
/// 주의:
/// - 에디터와 Development Build에서만 컴파일된다. 일반 빌드에서는 이 파일이 비어 있다.
/// - feature/DemoMap 전용. main에 머지하지 않는다.
/// </summary>
public class DemoQuickStart : MonoBehaviour
{
    private const KeyCode TriggerKey = KeyCode.P;

    // 내 LobbyPlayerData(고양이 선택 정보)가 생기길 기다리는 한도. 넘기면 기본 품종으로 진행한다.
    private const float LocalPlayerDataWaitSeconds = 5f;

    // 다른 사람의 방과 이름이 겹치지 않도록 매번 새 이름을 쓴다(같은 Photon 앱 ID를 공유한다).
    private const string SessionNamePrefix = "DemoSolo_";

    private bool _isRunning;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        GameObject host = new GameObject(nameof(DemoQuickStart));
        DontDestroyOnLoad(host);
        host.AddComponent<DemoQuickStart>();
    }

    private void Update()
    {
        if (_isRunning || !Input.GetKeyDown(TriggerKey))
        {
            return;
        }

        // 씬 이름 비교는 문자열을 새로 만든다. 키가 눌린 프레임에만 해서 Update의 GC 할당을 피한다.
        if (!SceneUtilityHelper.IsActiveScene(SceneNames.Lobby))
        {
            return;
        }

        _ = QuickStartAsync();
    }

    private async Task QuickStartAsync()
    {
        _isRunning = true;

        try
        {
            // 접속부터 하고 나서 씬 로드에서 막히면 세션만 남는다. 먼저 확인한다.
            if (SceneUtilityHelper.GetBuildIndex(SceneNames.Map) < 0)
            {
                Debug.LogError(
                    $"[DemoQuickStart] Build Settings에 {SceneNames.Map} 씬이 없어 진입할 수 없습니다. " +
                    "File > Build Profiles에서 씬을 추가한 뒤 다시 누르세요."
                );
                return;
            }

            GameManager gameManager = GameManager.Instance;

            if (gameManager.CurrentState == GameState.Lobby)
            {
                if (!gameManager.CanConnect)
                {
                    Debug.LogWarning("[DemoQuickStart] 지금은 접속할 수 없는 상태입니다.");
                    return;
                }

                string sessionName = SessionNamePrefix + Guid.NewGuid().ToString("N").Substring(0, 8);

                Debug.Log($"[DemoQuickStart] {TriggerKey} 입력: 혼자 Host로 접속합니다. Session={sessionName}");

                bool connected = await NetworkManager.Instance.StartNetworkGame(GameMode.Host, sessionName);

                if (!connected)
                {
                    Debug.LogError("[DemoQuickStart] 접속에 실패했습니다. 위의 NetworkManager 로그를 확인하세요.");
                    return;
                }
            }
            else if (gameManager.CurrentState != GameState.Ready)
            {
                Debug.LogWarning(
                    $"[DemoQuickStart] 이미 진행 중인 상태라 무시합니다. State={gameManager.CurrentState}"
                );
                return;
            }

            // 방 만들기 버튼으로 이미 접속한 경우(Ready)도 여기서부터 같은 경로를 탄다.
            await WaitForLocalLobbyPlayerData();

            Debug.Log("[DemoQuickStart] 게임 시작을 요청합니다.");
            gameManager.StartGame();
        }
        catch (Exception exception)
        {
            // 기다리지 않는 Task의 예외는 조용히 사라진다. 여기서 반드시 남긴다.
            Debug.LogException(exception);
        }
        finally
        {
            _isRunning = false;
        }
    }

    /// <summary>
    /// 접속 직후에는 내 LobbyPlayerData가 아직 없다. 이게 없으면 선택한 고양이 정보를
    /// 못 찾아 기본 품종으로 스폰되므로, 생길 때까지 잠깐 기다린다.
    /// </summary>
    private static async Task WaitForLocalLobbyPlayerData()
    {
        float deadline = Time.unscaledTime + LocalPlayerDataWaitSeconds;

        while (Time.unscaledTime < deadline)
        {
            LobbyManager lobbyManager = LobbyManager.Instance;

            if (lobbyManager != null && lobbyManager.GetLocalPlayerData() != null)
            {
                return;
            }

            await Task.Yield();
        }

        Debug.LogWarning(
            "[DemoQuickStart] 내 LobbyPlayerData가 생기지 않아 기본 고양이 품종으로 진행합니다."
        );
    }
}
#endif
