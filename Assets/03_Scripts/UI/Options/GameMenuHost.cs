using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

/// <summary>
/// ESC 메뉴와 옵션창을 소유하고 Esc 키를 처리한다. 첫 씬이 뜬 직후 스스로 생겨 게임 내내 산다(씬·프리팹 작업 없음).
/// <list type="bullet">
/// <item>게임 중(GameState.InGame) Esc → ESC 메뉴. 다시 Esc / 계속하기 → 닫힘.</item>
/// <item>메뉴가 열려 있는 동안 이 클라이언트의 이동·시점·상호작용만 막고 커서를 푼다(GameplayInputBlocker 소유자).
///   Time.timeScale 과 Fusion 시뮬레이션은 건드리지 않는다 — 다른 플레이어의 게임은 멈추지 않는다. 튜토리얼도 같다.</item>
/// <item>타이틀의 OPTIONS 버튼은 <see cref="OpenOptionsFromTitle"/> 로 같은 옵션창을 연다.</item>
/// <item>본부 단말기·마켓처럼 다른 UI 가 입력을 막고 있거나 해킹 미니게임 중이면 Esc 는 그쪽 몫이다(메뉴를 열지 않는다).
///   다른 UI 의 Update 보다 먼저 확인하도록 실행 순서를 앞당겼다 — 같은 프레임에 그쪽이 Esc 로 닫혀도 메뉴가 같이 열리지 않는다.</item>
/// </list>
/// </summary>
[DefaultExecutionOrder(-500)]
public sealed class GameMenuHost : MonoBehaviour
{
    private static GameMenuHost _instance;

    private RuntimeUIFactory _factory;
    private PauseMenuView _pause;
    private OptionsMenuView _options;

    // 옵션창을 ESC 메뉴에서 열었는가(닫으면 ESC 메뉴로 돌아간다) / 타이틀에서 열었는가(닫으면 끝).
    private bool _optionsFromPause;

    /// <summary>ESC 메뉴나 옵션창이 열려 있는가. 키를 직접 읽는 곳(타이틀 메뉴·튜토리얼 대사 넘기기 등)이 확인한다.</summary>
    public static bool IsOpen
    {
        get { return _instance != null && (_instance._pause.IsOpen || _instance._options.IsOpen); }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        Ensure();
    }

    public static GameMenuHost Ensure()
    {
        if (_instance == null)
        {
            GameObject go = new GameObject("GameMenuHost");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<GameMenuHost>();
        }

        return _instance;
    }

    /// <summary>타이틀 화면의 OPTIONS 버튼.</summary>
    public static void OpenOptionsFromTitle()
    {
        GameMenuHost host = Ensure();

        if (host._options.IsOpen)
        {
            return;
        }

        host._optionsFromPause = false;
        host.EnsureEventSystem();
        host._options.Open(host.HandleOptionsClosed);
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        _factory = new RuntimeUIFactory(RuntimeUIFactory.Shared.Font);

        Canvas canvas = _factory.CreateCanvas("GameMenuCanvas", OptionsTheme.CanvasSortingOrder, transform, true);

        // 사망 연출이 화면 캔버스를 죽은 플레이어 카메라(흑백 후처리) 아래로 옮길 때 메뉴는 빼 둔다 —
        // 옮겨지면 메뉴가 흑백이 되고, 튜토리얼 실패 화면의 베일이 메뉴 위를 덮어 버튼을 누를 수 없었다.
        canvas.gameObject.AddComponent<DeathOverlayExempt>();

        RectTransform pauseRoot = _factory.CreateRect("PauseMenu", canvas.transform);
        _pause = pauseRoot.gameObject.AddComponent<PauseMenuView>();
        _pause.Build(_factory, ClosePause, OpenOptionsFromPause);

        RectTransform optionsRoot = _factory.CreateRect("OptionsMenu", canvas.transform);
        _options = optionsRoot.gameObject.AddComponent<OptionsMenuView>();
        _options.Build(_factory);
    }

    private void OnEnable()
    {
        GameManager.OnStateChanged += HandleStateChanged;
    }

    private void OnDisable()
    {
        GameManager.OnStateChanged -= HandleStateChanged;
    }

    private void OnDestroy()
    {
        if (_instance != this)
        {
            return;
        }

        GameplayInputBlocker.Forget(this);
        _factory?.DestroyOwnedTextures();
        _instance = null;
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame)
        {
            return;
        }

        // 키 바꾸기 중의 Esc 는 "취소"다. 취소가 처리된 프레임에도 메뉴는 반응하지 않는다.
        InputManager input = InputManager.Instance;

        if (input.IsRebinding || input.LastRebindEndFrame >= Time.frameCount - 1)
        {
            return;
        }

        if (_options.IsOpen)
        {
            _options.HandleEscape();
            return;
        }

        if (_pause.IsOpen)
        {
            ClosePause();
            return;
        }

        if (CanOpenPause())
        {
            OpenPause();
        }
    }

    private static bool CanOpenPause()
    {
        GameManager game = GameManager.Instance;

        return game != null &&
               game.CurrentState == GameState.InGame &&
               !GameplayInputBlocker.IsBlocked &&
               !Hacking.HackingManager.AnyHackingActive;
    }

    private void OpenPause()
    {
        EnsureEventSystem();
        GameplayInputBlocker.Acquire(this);
        _pause.Show();
    }

    private void ClosePause()
    {
        _options.Close();
        _pause.Hide();
        GameplayInputBlocker.Release(this);
    }

    private void OpenOptionsFromPause()
    {
        _optionsFromPause = true;
        _pause.Hide();
        _options.Open(HandleOptionsClosed);
    }

    private void HandleOptionsClosed()
    {
        if (_optionsFromPause && GameplayInputBlocker.IsHeldBy(this))
        {
            _pause.Show();
        }

        _optionsFromPause = false;
    }

    private void HandleStateChanged(GameState previous, GameState next)
    {
        // 세션이 끝나거나 씬이 바뀌면 메뉴를 닫는다. 커서·입력은 상태를 바꾼 쪽(GameManager)이 정하므로 건드리지 않는다.
        if (next == GameState.InGame)
        {
            return;
        }

        // 먼저 플래그를 내려 옵션창이 닫히면서 ESC 메뉴를 다시 띄우지 않게 한다.
        _optionsFromPause = false;
        _options.Close();
        _pause.Hide();
        GameplayInputBlocker.Forget(this);
    }

    /// <summary>
    /// 버튼을 누르려면 EventSystem 이 필요하다. 지금 씬들(Lobby·Map·Tutorial)에는 있지만, 없는 씬에서 열리면 하나 만든다
    /// (현재 씬에 두어 씬과 함께 사라진다 — 다음 씬의 EventSystem 과 겹치지 않게).
    /// </summary>
    private void EnsureEventSystem()
    {
        if (EventSystem.current != null)
        {
            return;
        }

        GameObject go = new GameObject("EventSystem (GameMenuHost)");
        go.AddComponent<EventSystem>();
        go.AddComponent<InputSystemUIInputModule>();
    }
}
