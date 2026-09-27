#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Threading.Tasks;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 데모 테스트용 빠른 진입.
///
/// Lobby_Scene에서 P를 누르면 혼자 Host로 접속하고, SceneNames.DemoTutorialMap
/// ("Tutorial_Scene")으로 곧바로 진입한다.
///
/// 중요: <b>SceneNames.Map("Map_Scene")은 건드리지 않는다.</b> 그래서 로비의
/// "방 만들기 → 시작" 정상 흐름은 이 파일과 무관하게 계속 Map_Scene으로 간다.
/// 이 파일은 NetworkManager.LoadGameScene()과 GameManager.StartGame()을 호출하지
/// <b>않는다</b> — 그 두 메서드는 SceneNames.Map을 하드코딩해서 쓰기 때문에, 그대로 불렀다가는
/// "Map을 통째로 Tutorial_Scene으로 바꾸는" 예전 실수를 코드로 반복하게 된다.
/// 대신 이 파일이 <c>Runner.LoadScene</c>을 직접 불러 데모 씬으로 가고, 씬 로드가 끝나면
/// <see cref="NetworkManager.OnMapSceneLoaded"/>가 하는 일(플레이어·적 스폰, 입력 활성화)을
/// <see cref="HandleSceneLoadCompleted"/>에서 직접 재현한다.
///
/// 사용:
/// - Bootstrap_Scene에서 Play를 시작한다. Bootstrap이 GameManager/NetworkManager/InputManager를 만든다.
/// - 씬에 오브젝트를 둘 필요가 없다. 첫 씬이 로드된 직후 스스로 하나 만들어져 씬을 넘어 유지된다.
///
/// 알려진 한계:
/// - NetworkManager.CacheSelectedCatTypes()가 private이라 이 경로에서는 호출되지 않는다.
///   그래서 로비에서 고른 고양이 품종이 반영되지 않고 기본 품종(BlackCat)으로 스폰된다.
///   혼자 진행하는 데모라 문제 삼지 않았다.
///
/// 주의:
/// - 에디터와 Development Build에서만 컴파일된다. 일반 빌드에서는 이 파일이 비어 있다.
/// - feature/DemoMap 전용. main에 머지하지 않는다.
/// </summary>
public class DemoQuickStart : MonoBehaviour
{
    private const KeyCode TriggerKey = KeyCode.P;

    // 다른 사람의 방과 이름이 겹치지 않도록 매번 새 이름을 쓴다(같은 Photon 앱 ID를 공유한다).
    private const string SessionNamePrefix = "DemoSolo_";

    private bool _isRunning;

    // Runner.LoadScene은 결과를 돌려주지 않는다. NetworkManager.SceneLoadCompleted로 완료를
    // 받는데, 이 이벤트는 세션 전역이라 다른 씬 로드(정상 흐름 포함)에도 울린다. 그래서
    // "지금 내가 요청한 로드를 기다리는 중"인지를 이 플래그로 가려낸다.
    private bool _waitingForDemoSceneLoad;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        GameObject host = new GameObject(nameof(DemoQuickStart));
        DontDestroyOnLoad(host);
        host.AddComponent<DemoQuickStart>();
    }

    private void OnEnable()
    {
        NetworkManager.SceneLoadCompleted += HandleSceneLoadCompleted;
    }

    private void OnDisable()
    {
        NetworkManager.SceneLoadCompleted -= HandleSceneLoadCompleted;
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
            int buildIndex = SceneUtilityHelper.GetBuildIndex(SceneNames.DemoTutorialMap);

            if (buildIndex < 0)
            {
                Debug.LogError(
                    $"[DemoQuickStart] Build Settings에 {SceneNames.DemoTutorialMap} 씬이 없어 진입할 수 없습니다. " +
                    "File > Build Profiles에서 씬을 추가한 뒤 다시 누르세요."
                );
                return;
            }

            NetworkManager networkManager = NetworkManager.Instance;
            NetworkRunner runner = networkManager.Runner;

            if (runner == null || !runner.IsRunning)
            {
                // 아직 접속 전이면 여기서 접속한다. 이미 "방 만들기"로 접속해 있으면(Ready) 건너뛴다.
                if (!GameManager.Instance.CanConnect)
                {
                    Debug.LogWarning("[DemoQuickStart] 지금은 접속할 수 없는 상태입니다.");
                    return;
                }

                string sessionName = SessionNamePrefix + Guid.NewGuid().ToString("N").Substring(0, 8);

                Debug.Log($"[DemoQuickStart] {TriggerKey} 입력: 혼자 Host로 접속합니다. Session={sessionName}");

                bool connected = await networkManager.StartNetworkGame(GameMode.Host, sessionName);

                if (!connected)
                {
                    Debug.LogError("[DemoQuickStart] 접속에 실패했습니다. 위의 NetworkManager 로그를 확인하세요.");
                    return;
                }

                runner = networkManager.Runner;
            }

            if (runner == null || !runner.IsSceneAuthority)
            {
                Debug.LogWarning("[DemoQuickStart] Host(SceneAuthority)만 데모 씬으로 전환할 수 있습니다.");
                return;
            }

            Debug.Log($"[DemoQuickStart] {SceneNames.DemoTutorialMap} 씬 로드를 요청합니다.");

            _waitingForDemoSceneLoad = true;
            runner.LoadScene(SceneRef.FromIndex(buildIndex), LoadSceneMode.Single);
        }
        catch (Exception exception)
        {
            // 기다리지 않는 Task의 예외는 조용히 사라진다. 여기서 반드시 남긴다.
            Debug.LogException(exception);
            _waitingForDemoSceneLoad = false;
        }
        finally
        {
            _isRunning = false;
        }
    }

    /// <summary>
    /// NetworkManager.OnSceneLoadDone은 이 데모 씬 이름을 모르므로 default 분기로 빠져
    /// 경고 로그 한 줄만 남기고 아무것도 하지 않는다(정상 동작). 플레이어·적 스폰과 입력 활성화는
    /// 이 메서드가 NetworkManager.OnMapSceneLoaded와 같은 내용으로 직접 수행한다.
    /// </summary>
    private void HandleSceneLoadCompleted(string sceneName)
    {
        if (!_waitingForDemoSceneLoad || sceneName != SceneNames.DemoTutorialMap)
        {
            return;
        }

        _waitingForDemoSceneLoad = false;

        NetworkRunner runner = NetworkManager.Instance.Runner;

        if (runner == null)
        {
            Debug.LogError("[DemoQuickStart] 씬 로드는 끝났는데 Runner가 없습니다.");
            return;
        }

        InputManager.Instance.EnableGameplayInput();

        if (runner.IsServer)
        {
            SpawnManager.Instance.SpawnAllPlayers(runner);
            SpawnManager.Instance.SpawnAllEnemies(runner);
        }

        GameManager.Instance.EnterInGame();

        Debug.Log("[DemoQuickStart] 진입 완료.");
    }
}
#endif
