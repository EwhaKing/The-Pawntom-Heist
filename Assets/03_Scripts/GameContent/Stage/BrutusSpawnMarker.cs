using Pawntom.Stage;
using UnityEngine;

/// <summary>
/// 브루투스 스폰 지점 마커(편집용, 런타임 로직 없음). 캣닢 경보 때 여기서 나온다.
/// 광역 순찰 반경도 레벨 데이터 항목이다(GAME_DESIGN §3.8).
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Pawntom/Level/Brutus Spawn Marker")]
public sealed class BrutusSpawnMarker : MonoBehaviour, ILevelLayoutSource
{
    [Tooltip("브루투스 스폰 Id. 비우면 GameObject 이름")]
    [SerializeField] private string _id;

    [Tooltip("광역 순찰 반경(m)")]
    [SerializeField, Min(0f)] private float _patrolRadius = 20f;

    public string Id
    {
        get { return LevelLayoutIds.Resolve(_id, this); }
    }

    public void AppendTo(StageLayout layout)
    {
        layout.BrutusSpawns.Add(new BrutusSpawn(Id, LevelLayoutData.ToStage(transform.position), LevelLayoutIds.Yaw(transform), _patrolRadius));
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.9f, 0.2f, 0.9f, 0.9f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up, 1f);
        Gizmos.color = new Color(0.9f, 0.2f, 0.9f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, _patrolRadius);
    }
}
