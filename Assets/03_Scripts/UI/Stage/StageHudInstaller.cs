using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 게임 씬(<see cref="SceneNames.Map"/>)의 HUD 피드백 진입점 — 분전반 캐스팅 바·캣닢 경보 화면·환풍구 막힘 안내·금고 해킹 안내.
/// 씬·프리팹 배선 없이 Map 씬이 로드되면 스스로 만들고, 그 씬이 내려가면 지운다(튜토리얼·로비에는 만들지 않는다).
/// 튜토리얼 전용 UI 를 참조하지 않는다 — 게임 쪽 정적 이벤트만 구독한다.
/// <para>
/// 캔버스는 <see cref="DeathOverlayExempt"/> 를 붙이지 않는다: 사망 연출이 화면 캔버스를 흑백 후처리 아래로 옮길 때 같이 흑백이 된다.
/// 옵션/ESC 메뉴(5000)보다 아래라 메뉴가 항상 위에 보인다.
/// </para>
/// </summary>
public sealed class StageHudInstaller : MonoBehaviour
{
    private static StageHudInstaller _instance;

    private RuntimeUIFactory _factory;
    private StageHudSprites _sprites;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        // 도메인 리로드를 끈 에디터에서 구독이 겹치지 않게 먼저 뗀다.
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneUnloaded -= HandleSceneUnloaded;
        _instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneUnloaded -= HandleSceneUnloaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        SceneManager.sceneUnloaded += HandleSceneUnloaded;

        // 에디터에서 Map 씬을 바로 재생한 경우: 첫 씬 로드 이벤트는 이미 지나갔다.
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            if (SceneManager.GetSceneAt(i).name == SceneNames.Map)
            {
                Ensure();
                return;
            }
        }
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == SceneNames.Map)
        {
            Ensure();
        }
    }

    private static void HandleSceneUnloaded(Scene scene)
    {
        if (scene.name == SceneNames.Map && _instance != null)
        {
            Destroy(_instance.gameObject);
            _instance = null;
        }
    }

    private static void Ensure()
    {
        if (_instance != null)
        {
            return;
        }

        GameObject go = new GameObject("StageHud");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<StageHudInstaller>();
    }

    private void Awake()
    {
        _factory = new RuntimeUIFactory(RuntimeUIFactory.Shared.Font);
        _sprites = new StageHudSprites();

        // 한글 글리프를 미리 굽는다(처음 뜰 때 끊김·□ 방지).
        RuntimeUIFactory.Shared.Prewarm("분전반 접속 중완료실패했습니다0123456789%카오스 삼색만 지나갈 수 있는 좁은 환풍구입니다" +
                                        "해킹성공—금고를여는중…잠시후다시시도할수있습니다가열렸습니다지못했가까이에서하세요");

        // 경보 덮개는 HUD 문구보다 아래(피격 번쩍임 850 바로 밑)에 둔다.
        Canvas alarmCanvas = _factory.CreateCanvas("StageAlarmCanvas", StageHudTheme.AlarmSortingOrder, transform);
        alarmCanvas.gameObject.AddComponent<AlarmOverlayView>().Build(_factory, alarmCanvas.transform, _sprites);

        Canvas hudCanvas = _factory.CreateCanvas("StageHudCanvas", StageHudTheme.HudSortingOrder, transform);
        hudCanvas.gameObject.AddComponent<CastBarView>().Build(_factory, hudCanvas.transform, _sprites);
        hudCanvas.gameObject.AddComponent<VentBlockedHintView>().Build(_factory, hudCanvas.transform);
        hudCanvas.gameObject.AddComponent<VaultHackStatusView>().Build(_factory, hudCanvas.transform);
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }

        if (_sprites != null)
        {
            _sprites.Dispose();
        }

        if (_factory != null)
        {
            _factory.DestroyOwnedTextures();
        }
    }
}
