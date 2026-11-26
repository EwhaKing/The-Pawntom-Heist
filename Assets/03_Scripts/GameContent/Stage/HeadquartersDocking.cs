using System.Collections.Generic;
using Pawntom.Stage;
using UnityEngine;

/// <summary>
/// 본부 도킹을 씬에 적용한다(GAME_DESIGN §3.8: 1층 후보 3곳 중 1곳, 나머지 2곳은 막다른 벽).
/// <para>
/// 네트워크 코드는 없다. <see cref="StageNetworkState"/> 가 모든 피어에서 같은 시드의 계획으로 부른다 —
/// 본부 아래 오브젝트(EscapeButton 등)는 NetworkTransform 이 없으므로 각 피어가 같은 위치로 옮기는 것이 곧 동기화다.
/// </para>
/// <para>
/// 씬 규칙: 본부 루트는 기준 도킹에 붙은 채로 저장된다. 각 <see cref="DockingPointMarker"/> 아래 <c>Blocker</c> 자식은
/// 본부가 없는 도킹을 막는 벽이다(본부가 붙은 도킹만 꺼져 있다).
/// 기준 도킹 = 지정값 → 저장 상태에서 Blocker 가 꺼져 있는 유일한 도킹 → 본부와 가장 가까운 도킹.
/// </para>
/// </summary>
public sealed class HeadquartersDocking
{
    /// <summary>도킹 마커 아래 막다른 벽 자식 이름(씬 규칙).</summary>
    public const string BlockerChildName = "Blocker";

    private readonly Transform _mainbase;
    private readonly DockingPointMarker _referenceOverride;

    private bool _captured;
    private Vector3 _originalPosition;
    private Quaternion _originalRotation;
    private StagePose _referencePose;
    private DockingPointMarker[] _markers = new DockingPointMarker[0];

    public HeadquartersDocking(Transform mainbase, DockingPointMarker referenceOverride)
    {
        _mainbase = mainbase;
        _referenceOverride = referenceOverride;
    }

    /// <summary>적용된 도킹 Id. 아직 적용 전이면 null.</summary>
    public string AppliedDockId { get; private set; }

    /// <summary>
    /// <paramref name="layout"/> 의 <paramref name="dockingIndex"/> 번 도킹으로 본부를 옮기고 막다른 벽을 켠다/끈다.
    /// 항상 처음 저장된 포즈에서 계산하므로 여러 번 불러도 결과가 같다.
    /// </summary>
    public bool Apply(StageLayout layout, int dockingIndex)
    {
        if (_mainbase == null)
        {
            Debug.LogError("[HeadquartersDocking] 본부 루트를 찾지 못해 도킹을 적용하지 않습니다.");
            return false;
        }

        if (layout == null || dockingIndex < 0 || dockingIndex >= layout.DockingCandidates.Count || layout.DockingCandidates[dockingIndex] == null)
        {
            Debug.LogError($"[HeadquartersDocking] 도킹 인덱스가 레이아웃 범위 밖입니다. Index={dockingIndex}");
            return false;
        }

        Capture();

        DockingCandidate candidate = layout.DockingCandidates[dockingIndex];
        DockingPointMarker targetMarker = FindMarker(candidate.Id);

        // 막다른 벽·프레임은 씬 마커에 붙어 있으므로 포즈도 씬 마커를 따른다. 마커가 없으면 레벨 데이터 값.
        StagePose target = targetMarker != null
            ? PoseOf(targetMarker.transform)
            : new StagePose(candidate.Position, candidate.Yaw);

        if (targetMarker == null)
        {
            Debug.LogWarning($"[HeadquartersDocking] 씬에 도킹 마커 '{candidate.Id}' 가 없어 레벨 데이터 좌표로 옮깁니다. 막다른 벽은 바꾸지 않습니다.");
        }

        var body = new StagePose(LevelLayoutData.ToStage(_originalPosition), _originalRotation.eulerAngles.y);
        StagePose moved = DockingPose.Relocate(_referencePose, target, body);
        float delta = DockingPose.DeltaYaw(_referencePose.Yaw, target.Yaw);

        _mainbase.SetPositionAndRotation(
            LevelLayoutData.ToUnity(moved.Position),
            Quaternion.Euler(0f, delta, 0f) * _originalRotation);

        for (int i = 0; i < _markers.Length; i++)
        {
            DockingPointMarker marker = _markers[i];
            Transform blocker = marker != null ? marker.transform.Find(BlockerChildName) : null;

            if (blocker != null)
            {
                blocker.gameObject.SetActive(targetMarker == null ? blocker.gameObject.activeSelf : marker != targetMarker);
            }
        }

        // 같은 프레임에 스폰되는 플레이어 컨트롤러가 옛 위치의 벽과 부딪히지 않게 물리 좌표를 바로 맞춘다(1회).
        Physics.SyncTransforms();

        AppliedDockId = candidate.Id;
        Debug.Log($"[HeadquartersDocking] 본부 도킹 = {candidate.Id} (#{dockingIndex}), 위치 {_mainbase.position}");
        return true;
    }

    // 처음 저장 상태를 한 번만 기록한다. 이후 적용은 모두 이 기록에서 계산한다.
    private void Capture()
    {
        if (_captured)
        {
            return;
        }

        _captured = true;
        _originalPosition = _mainbase.position;
        _originalRotation = _mainbase.rotation;
        _markers = Object.FindObjectsByType<DockingPointMarker>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        DockingPointMarker reference = ResolveReference();

        if (reference != null)
        {
            _referencePose = PoseOf(reference.transform);
            Debug.Log($"[HeadquartersDocking] 기준 도킹 = {reference.Id}");
        }
        else
        {
            // 마커가 하나도 없으면 본부 자신을 기준으로 본다(대상 = 레벨 데이터 좌표).
            _referencePose = new StagePose(LevelLayoutData.ToStage(_originalPosition), _originalRotation.eulerAngles.y);
            Debug.LogWarning("[HeadquartersDocking] 씬에 DockingPointMarker 가 없어 본부 위치를 기준으로 씁니다.");
        }
    }

    private DockingPointMarker ResolveReference()
    {
        if (_referenceOverride != null)
        {
            return _referenceOverride;
        }

        DockingPointMarker open = null;
        int openCount = 0;

        for (int i = 0; i < _markers.Length; i++)
        {
            DockingPointMarker marker = _markers[i];
            Transform blocker = marker != null ? marker.transform.Find(BlockerChildName) : null;

            if (blocker != null && !blocker.gameObject.activeSelf)
            {
                open = marker;
                openCount++;
            }
        }

        if (openCount == 1)
        {
            return open;
        }

        DockingPointMarker nearest = null;
        float best = float.MaxValue;

        for (int i = 0; i < _markers.Length; i++)
        {
            if (_markers[i] == null)
            {
                continue;
            }

            float d = (_markers[i].transform.position - _originalPosition).sqrMagnitude;

            if (d < best)
            {
                best = d;
                nearest = _markers[i];
            }
        }

        if (nearest != null)
        {
            Debug.LogWarning($"[HeadquartersDocking] Blocker 가 꺼진 도킹이 {openCount}곳이라 본부와 가장 가까운 '{nearest.Id}' 를 기준 도킹으로 씁니다.");
        }

        return nearest;
    }

    private DockingPointMarker FindMarker(string id)
    {
        for (int i = 0; i < _markers.Length; i++)
        {
            if (_markers[i] != null && string.Equals(_markers[i].Id, id, System.StringComparison.Ordinal))
            {
                return _markers[i];
            }
        }

        return null;
    }

    private static StagePose PoseOf(Transform t)
    {
        return new StagePose(LevelLayoutData.ToStage(t.position), LevelLayoutIds.Yaw(t));
    }

    /// <summary>본부 아래 플레이어 스폰 지점(자식 순서). 루트가 없으면 빈 목록.</summary>
    public static void CollectSpawnPoints(Transform root, List<Transform> results)
    {
        results.Clear();

        if (root == null)
        {
            return;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            results.Add(root.GetChild(i));
        }
    }
}
