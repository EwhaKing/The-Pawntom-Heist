using Pawntom.Stage;
using UnityEngine;

/// <summary>
/// 본부 도킹 후보 지점 마커(편집용, 런타임 로직 없음). GAME_DESIGN §3.8: 1층에 3곳.
/// 위치·회전만 레벨 데이터에 구워진다. 도킹 프리팹 참조는 두지 않는다 — 활성화는 스폰 쪽이 레벨 데이터로 한다.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Pawntom/Level/Docking Point Marker")]
public sealed class DockingPointMarker : MonoBehaviour, ILevelLayoutSource
{
    [Tooltip("도킹 후보 Id. 비우면 GameObject 이름")]
    [SerializeField] private string _id;

    public string Id
    {
        get { return LevelLayoutIds.Resolve(_id, this); }
    }

    public void AppendTo(StageLayout layout)
    {
        layout.DockingCandidates.Add(new DockingCandidate(Id, LevelLayoutData.ToStage(transform.position), LevelLayoutIds.Yaw(transform)));
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.9f);
        Gizmos.DrawWireCube(transform.position + Vector3.up, new Vector3(3f, 2f, 3f));
        Gizmos.DrawLine(transform.position + Vector3.up, transform.position + Vector3.up + transform.forward * 3f);
    }
}
