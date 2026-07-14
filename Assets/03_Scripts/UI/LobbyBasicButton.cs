using UnityEngine;
using Fusion;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 게임에 접속하자마자 보이는 로비 씬에서
/// 방 생성 버튼과 방 참가 버튼을 담당합니다.
/// </summary>
public class LobbyBasicButton : MonoBehaviour
{
    public async void OnClickCreateRoom()
    {
        await NetworkManager.Instance.StartNetworkGame(GameMode.Host);
    }

    public async void OnClickJoinRoom()
    {
        await NetworkManager.Instance.StartNetworkGame(GameMode.Client);
    }

    public void OnClickQuit()
    {
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
