using Pawntom.Stage;
using UnityEngine;

/// <summary>
/// 열쇠 배치 후보 지점 마커(편집용, 런타임 로직 없음). GAME_DESIGN §3.8: 후보 풀에서 단계 수만큼 중복 없이 무작위.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Pawntom/Level/Key Spawn Marker")]
public sealed class KeySpawnMarker : MonoBehaviour, ILevelLayoutSource
{
    [Tooltip("열쇠 후보 Id. 비우면 GameObject 이름")]
    [SerializeField] private string _id;

    public string Id
    {
        get { return LevelLayoutIds.Resolve(_id, this); }
    }

    public void AppendTo(StageLayout layout)
    {
        layout.KeySpots.Add(new KeySpot(Id, LevelLayoutData.ToStage(transform.position)));
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.75f, 0.1f, 0.95f);
        Gizmos.DrawWireCube(transform.position + Vector3.up * 0.25f, new Vector3(0.4f, 0.5f, 0.4f));
    }
}
