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
    /// 탈출구가 열렸는지 여부
    /// 
    /// **NetworkBehaviour가 아니기에 [Networked]로 동기화되지는 않음
    /// </summary>
    public bool IsEscapeUnlocked { get; private set; } = false;

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

    protected override void Awake()
    {
        base.Awake();
    }

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
        // LobbyManager가 구현되면 모든 플레이어의 Ready 여부를 검사 UI 처리

        if (!LobbyManager.Instance.CanStartGame())
        {
            Debug.LogWarning("아직 준비하지 않은 플레이어가 있습니다.");
            return;
        }

        Debug.Log("[GameManager] 게임 시작을 요청합니다.");

        EnterLoading();
        NetworkManager.Instance.LoadGameScene();
    }


    /// <summary>
    /// 탈출 성공 처리
    ///
    /// 조건:
    /// - 본부 안에 살아있는 플레이어가 최소 1명 이상 있음
    /// - 팀원 중 한 명 이상이 전설의 캣닢을 소지 중
    /// - 출발 버튼 상호작용 성공
    /// </summary>
    public void EscapeSuccess()
    {
        Debug.Log("[GameManager] 레벨 클리어! 탈출 성공");

        // TODO:
        // - 인벤토리 내 생존 전리품 정산
        // - 보너스 계산
        // - 다음 일차 데이터 해금
        // - 상점 씬으로 이동
        // - 결과 UI 출력

        EnterResult();
    }
}