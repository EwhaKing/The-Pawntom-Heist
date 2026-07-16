using UnityEngine;
using System.IO;
using UnityEngine.SceneManagement;

public static class SceneUtilityHelper
{
    public static int GetBuildIndex(string sceneName)
    {
        int sceneCount = SceneManager.sceneCountInBuildSettings;

        for (int i = 0; i < sceneCount; i++)
        {
            string scenePath = SceneUtility.GetScenePathByBuildIndex(i);
            string buildSceneName = Path.GetFileNameWithoutExtension(scenePath);

            if (buildSceneName == sceneName)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// 주어진 씬 이름이 현재 활성 씬과 일치하는지 확인합니다.
    /// </summary>
    public static bool IsActiveScene(string sceneName)
    {
        return SceneManager.GetActiveScene().name == sceneName;
    }
}