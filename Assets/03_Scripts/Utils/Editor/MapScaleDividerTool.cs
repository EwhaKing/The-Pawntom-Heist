using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MapScaleDividerTool
{
    private const float DivideFactor = 20f;

    [MenuItem("Tools/Pawntom Heist/Divide Selected Local Scale By 20")]
    private static void DivideSelectedScale()
    {
        Transform[] selected = Selection.transforms;
        if (selected.Length == 0)
        {
            Debug.LogWarning("[MapScaleDividerTool] 선택된 오브젝트가 없습니다. 하이러키에서 스케일을 줄일 오브젝트를 먼저 선택하세요.");
            return;
        }

        Undo.RecordObjects(selected, "Divide Local Scale By 20");

        foreach (Transform t in selected)
        {
            t.localScale /= DivideFactor;
            EditorUtility.SetDirty(t);

            if (t.gameObject.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(t.gameObject.scene);
            }
        }

        Debug.Log($"[MapScaleDividerTool] 선택한 오브젝트 {selected.Length}개의 로컬 스케일을 1/20로 줄였습니다. Ctrl+Z로 되돌릴 수 있습니다.");
    }
}
