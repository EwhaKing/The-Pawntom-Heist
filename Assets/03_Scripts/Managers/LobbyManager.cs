// using UnityEngine;
// using Fusion;

// public class LobbyManager : PawntomSingleton<LobbyManager>
// {
//     public async Task Host()
//     {
//         await NetworkManager.Instance.StartGame(GameMode.Host);

//         GameManager.Instance.ChangeState(GameState.Lobby);
//     }

//     public async Task Join()
//     {
//         await NetworkManager.Instance.StartGame(GameMode.Client);

//         GameManager.Instance.ChangeState(GameState.Lobby);
//     }
// }