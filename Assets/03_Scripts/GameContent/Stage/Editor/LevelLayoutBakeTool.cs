using System.Collections.Generic;
using System.Text;
using Pawntom.Enemy.Authoring;
using Pawntom.EnemyBridge;
using Pawntom.Stage;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 열린 씬의 배치(마커·ControllableWall·PatrolRoute·EnemySpawnPoint·VaultChest)를 <see cref="LevelLayoutData"/> 에 굽는다.
/// <para>
/// 대상: Project 창에서 선택한 LevelLayoutData, 없으면 씬의 <see cref="StageLayoutProvider"/> 가 꽂은 에셋.
/// 단계·층·열쇠 수는 에셋 값을 그대로 두고 배치 목록만 덮어쓴다. Undo 가능하며, 저장은 사람이 한다(File ▸ Save Project).
/// </para>
/// <para>
/// 계층에서 활성인 오브젝트만 읽는다 — 보관용으로 꺼 둔 옛 층(1stFloor_Old 등)은 굽히지 않는다.
/// 같은 GameObject 에 <see cref="ILevelLayoutSource"/> 가 붙어 있으면 그 기록만 쓰고 직접 읽기는 건너뛴다.
/// </para>
/// </summary>
public static class LevelLayoutBakeTool
{
    private const string BakeMenu = "Pawntom/Level/Bake Layout From Scene";
    private const string ValidateMenu = "Pawntom/Level/Validate Selected Layout";
    private const string LogPrefix = "[LevelLayoutBake] ";

    [MenuItem(BakeMenu)]
    private static void BakeFromScene()
    {
        LevelLayoutData target = ResolveTarget();
        if (target == null)
        {
            Debug.LogError(LogPrefix + "대상 LevelLayoutData 가 없다. Project 창에서 에셋을 선택하거나 씬의 StageLayoutProvider 에 에셋을 꽂는다.");
            return;
        }

        StageLayout layout = target.ToCore();
        layout.ClearEntries();
        int sourceCount = CollectFromOpenScenes(layout);
        layout.SortById();

        Undo.RecordObject(target, "Bake Level Layout");
        target.OverwriteFrom(layout);
        EditorUtility.SetDirty(target);

        Debug.Log(LogPrefix + "'" + target.name + "' 에 구움 — 원본 " + sourceCount + "개. " + Summary(layout), target);
        LogIssues(target, layout);
    }

    [MenuItem(BakeMenu, true)]
    private static bool BakeFromSceneEnabled()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode;
    }

    [MenuItem(ValidateMenu)]
    private static void ValidateSelected()
    {
        LevelLayoutData target = ResolveTarget();
        if (target == null)
        {
            Debug.LogError(LogPrefix + "검증할 LevelLayoutData 가 없다.");
            return;
        }

        StageLayout layout = target.ToCore();
        Debug.Log(LogPrefix + "'" + target.name + "' 검증 — " + Summary(layout), target);
        LogIssues(target, layout);
    }

    /// <summary>선택한 에셋 → 씬의 제공자가 꽂은 에셋 순으로 찾는다.</summary>
    private static LevelLayoutData ResolveTarget()
    {
        if (Selection.activeObject is LevelLayoutData selected)
        {
            return selected;
        }

        StageLayoutProvider[] providers = Object.FindObjectsByType<StageLayoutProvider>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < providers.Length; i++)
        {
            if (providers[i].Data != null)
            {
                return providers[i].Data;
            }
        }

        return null;
    }

    /// <returns>읽은 원본 컴포넌트 수.</returns>
    private static int CollectFromOpenScenes(StageLayout layout)
    {
        int count = 0;
        var ownedBySource = new HashSet<GameObject>();

        MonoBehaviour[] behaviours = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is ILevelLayoutSource source && behaviours[i].isActiveAndEnabled)
            {
                source.AppendTo(layout);
                ownedBySource.Add(behaviours[i].gameObject);
                count++;
            }
        }

        foreach (ControllableWall wall in Object.FindObjectsByType<ControllableWall>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (ownedBySource.Contains(wall.gameObject))
            {
                continue;
            }

            Transform t = wall.transform;
            layout.Doors.Add(new DoorEntry(wall.gameObject.name, LevelLayoutData.ToStage(t.position), LevelLayoutIds.Yaw(t),
                DoorSecurity.Normal, DoorInitialState.Locked));
            count++;
        }

        foreach (PatrolRoute route in Object.FindObjectsByType<PatrolRoute>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (ownedBySource.Contains(route.gameObject))
            {
                continue;
            }

            var entry = new PatrolRouteEntry(route.gameObject.name, route.Loop);
            IReadOnlyList<Vector3> points = route.GetWaypointPositions();
            for (int p = 0; p < points.Count; p++)
            {
                entry.Waypoints.Add(LevelLayoutData.ToStage(points[p]));
            }

            layout.PatrolRoutes.Add(entry);
            count++;
        }

        foreach (EnemySpawnPoint spawn in Object.FindObjectsByType<EnemySpawnPoint>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (ownedBySource.Contains(spawn.gameObject))
            {
                continue;
            }

            Transform t = spawn.transform;
            string routeId = spawn.PatrolRoute != null ? spawn.PatrolRoute.gameObject.name : string.Empty;
            layout.K9Spawns.Add(new K9Spawn(spawn.gameObject.name, LevelLayoutData.ToStage(t.position), LevelLayoutIds.Yaw(t), routeId));
            count++;
        }

        foreach (VaultChest vault in Object.FindObjectsByType<VaultChest>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (ownedBySource.Contains(vault.gameObject))
            {
                continue;
            }

            Transform t = vault.transform;
            layout.Vaults.Add(new VaultEntry(vault.gameObject.name, LevelLayoutData.ToStage(t.position), LevelLayoutIds.Yaw(t)));
            count++;
        }

        return count;
    }

    private static void LogIssues(LevelLayoutData target, StageLayout layout)
    {
        List<StageLayoutIssue> issues = StageRules.Validate(layout);
        if (issues.Count == 0)
        {
            Debug.Log(LogPrefix + "'" + target.name + "' 검증 통과 — 오류 0.", target);
            return;
        }

        int errorCount = 0;
        for (int i = 0; i < issues.Count; i++)
        {
            if (issues[i].Severity == StageLayoutIssueSeverity.Error)
            {
                errorCount++;
            }
        }

        var builder = new StringBuilder();
        builder.Append(LogPrefix).Append('\'').Append(target.name).Append("' 검증 — 오류 ").Append(errorCount)
            .Append("개, 경고 ").Append(issues.Count - errorCount).Append("개:");
        for (int i = 0; i < issues.Count; i++)
        {
            builder.Append("\n - ").Append(issues[i].ToString());
        }

        // 굽기 자체는 성공했으므로 에러가 아니라 경고로 남긴다(배치 작업 중간 상태일 수 있다).
        Debug.LogWarning(builder.ToString(), target);
    }

    private static string Summary(StageLayout layout)
    {
        return layout.StageNumber + "단계 " + layout.FloorNumber + "층, 열쇠 " + layout.RequiredKeyCount + "개, K-9 " + layout.K9Count
            + "마리 | 도킹 " + layout.DockingCandidates.Count
            + " · 아이템 " + layout.ItemSpots.Count + " · 열쇠 후보 " + layout.KeySpots.Count + " · 순찰 " + layout.PatrolRoutes.Count
            + " · K-9 " + layout.K9Spawns.Count + " · 브루투스 " + layout.BrutusSpawns.Count + " · 문 " + layout.Doors.Count
            + " · 환풍구 " + layout.Vents.Count + " · 분전반 " + layout.PowerPanels.Count + " · 구역 " + layout.Zones.Count
            + " · 금고 " + layout.Vaults.Count;
    }
}
