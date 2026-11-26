using Pawntom.Stage;
using UnityEngine;

/// <summary>
/// 격벽 배치 보정 마커(편집용, 런타임 로직 없음). ControllableWall 과 같은 GameObject 에 붙인다.
/// <para>
/// 굽기 도구는 ControllableWall 을 직접 읽어 "일반 등급 · 잠김" 으로 기록한다.
/// 금고방 문처럼 등급·초기 상태·Id 를 바꿔야 할 때만 이 마커를 붙인다 — 붙어 있으면 이 값이 쓰인다.
/// </para>
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Pawntom/Level/Door Layout Marker")]
public sealed class DoorLayoutMarker : MonoBehaviour, ILevelLayoutSource
{
    [Tooltip("격벽 Id. 비우면 GameObject 이름")]
    [SerializeField] private string _id;

    [SerializeField] private DoorSecurity _security = DoorSecurity.Normal;

    [SerializeField] private DoorInitialState _initialState = DoorInitialState.Locked;

    public string Id
    {
        get { return LevelLayoutIds.Resolve(_id, this); }
    }

    public void AppendTo(StageLayout layout)
    {
        layout.Doors.Add(new DoorEntry(Id, LevelLayoutData.ToStage(transform.position), LevelLayoutIds.Yaw(transform), _security, _initialState));
    }
}
