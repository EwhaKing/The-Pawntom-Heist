using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Bootstrap 씬 전용 씬 컨트롤러
/// </summary>

public class BootstrapSceneController : MonoBehaviour
{
    private void Start()
    {
        SceneManager.LoadScene("Lobby_Scene");
    }
}