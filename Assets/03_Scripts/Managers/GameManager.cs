using UnityEngine;
using System;
using Fusion;

public enum GameState
{
    Lobby, // 로비 UI 상태, 네트워크 접속 이전 상태
    Loading, // Fusion에 연결되기 위한 상태 (로딩 중 UI가 띄워지는 단계)
    Ready, // 방 입장 완료, 플레이어들이 게임 시작을 기다리는 상태
    InGame, // 실제 게임 진행 중
    Result // 게임 결과 화면

    //TODO : 기획에 따라 추가하기
}

/// <summary>
/// 
/// </summary>
public class GameManager : PawntomSingleton<GameManager>
{
    public GameState CurrentState { get; private set; } = GameState.Lobby; //초기 상태 : Lobby 상태

    /// <summary>
    /// 게임 상태가 변경될 때 호출되는 이벤트. 이전 상태와 새로운 상태를 함께 전달함.
    /// </summary>
    public static event Action<GameState, GameState> OnStateChanged;

    private void ChangeState(GameState newState)
    {
        Debug.Log($"GameState -> {newState}");

        if (CurrentState == newState) return;

        GameState previousState = CurrentState;
        CurrentState = newState;
        Debug.Log($"[GameManager] 게임 상태가 변경되었습니다: {previousState} -> {newState}");

        // 상태 바뀌었다는 신호
        OnStateChanged?.Invoke(previousState, CurrentState);
    }

    /// <summary>
    /// 네트워크에 접속하기 전 로비 상태로 진입합니다.
    /// </summary>
    public void EnterLobby()
    {
        ChangeState(GameState.Lobby);
    }

    /// <summary>
    /// 네트워크 접속 또는 씬 로딩 상태로 진입합니다.
    /// </summary>
    public void EnterLoading()
    {
        ChangeState(GameState.Loading);
    }

    /// <summary>
    /// 방 입장이 완료되고 게임 시작을 기다리는 상태로 진입합니다.
    /// </summary>
    public void EnterReady()
    {
        ChangeState(GameState.Ready);
    }

    /// <summary>
    /// 실제 게임 플레이 상태로 진입합니다.
    /// </summary>
    public void EnterInGame()
    {
        ChangeState(GameState.InGame);
    }

    /// <summary>
    /// 게임 결과 상태로 진입합니다.
    /// </summary>
    public void EnterResult()
    {
        ChangeState(GameState.Result);
    }


    [Header("현재 로비에서 선택 중인 플레이어 속성")]
    [SerializeField] private string _currentInputName = string.Empty;
    [SerializeField] private CatType _currentSelectedCat = CatType.BlackCat;
    // 나중에 UI나 저장 시스템에서 이 프로퍼티를 통해 이름을 마음대로 넣고 뺍니다.
    public string CurrentInputName
    {
        get { return _currentInputName; }
        set { _currentInputName = value; }
    }

    public CatType CurrentSelectedCat
    {
        get { return _currentSelectedCat; }
        set { _currentSelectedCat = value; }
    }

    protected override void Awake()
    {
        base.Awake();

        // TODO : 기획과 상의
        // [저장 시스템 연동 뼈대] 게임이 처음 켜질 때, 이전에 저장된 이름이 있다면 자동으로 불러옵니다.
        // 만약 저장된 게 없다면 빈칸이거나 기본값으로 세팅됩니다.
        /*
        _currentInputName = PlayerPrefs.GetString("SavedPlayerName", "UnknownCat");
        _currentSelectedCat = CatType.BlackCat; // 기본 선택 없음
        */
    }


    // TODO : 기획과 상의
    //[저장용 함수] 방에 성공적으로 입장했을 때, 이 이름을 기억해두기 위해 호출할 함수입니다.
    // public void SaveCurrentName()
    // {
    //     PlayerPrefs.SetString("SavedPlayerName", _currentInputName);
    //     PlayerPrefs.Save(); // 하드디스크에 실제로 저장
    //     Debug.Log($"[GameManager] 이름이 성공적으로 저장되었습니다: {_currentInputName}");
    // }

    /// <summary>
    /// 게임 시작 조건을 확인하고 게임 씬 로드를 요청합니다.
    /// </summary>
    public void StartGame()
    {
        if (CurrentState != GameState.Ready)
        {
            Debug.LogWarning($"[GameManager] Ready 상태에서만 게임을 시작할 수 있습니다. " + $"현재 상태: {CurrentState}");
            return;
        }

        if (NetworkManager.Instance == null)
        {
            Debug.LogError("[GameManager] NetworkManager가 존재하지 않습니다.");
            return;
        }

        NetworkRunner runner = NetworkManager.Instance.Runner;

        if (runner == null || !runner.IsRunning)
        {
            Debug.LogError("[GameManager] 실행 중인 NetworkRunner가 없습니다.");
            return;
        }

        if (!runner.IsSceneAuthority)
        {
            Debug.LogWarning("[GameManager] Host만 게임을 시작할 수 있습니다.");
            return;
        }

        // TODO:
        // LobbyManager가 구현되면 모든 플레이어의 Ready 여부를 검사
        //
        // if (!LobbyManager.Instance.IsEveryoneReady())
        // {
        //     Debug.LogWarning("[GameManager] 아직 준비하지 않은 플레이어가 있습니다.");
        //     return;
        // }

        Debug.Log("[GameManager] 게임 시작을 요청합니다.");

        EnterLoading();
        NetworkManager.Instance.LoadGameScene();
    }

}