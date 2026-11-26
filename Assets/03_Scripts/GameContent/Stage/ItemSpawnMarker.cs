using System.Collections.Generic;
using Pawntom.Stage;
using UnityEngine;

/// <summary>
/// 아이템 스폰 지점 마커(편집용, 런타임 로직 없음). 스테이지 시작 시 확률을 굴려 허용 목록에서 1개가 나온다(StageRules.RollItems).
/// 열쇠 후보 지점과 0.5m 안에 겹치면 아이템이 나오지 않는다.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Pawntom/Level/Item Spawn Marker")]
public sealed class ItemSpawnMarker : MonoBehaviour, ILevelLayoutSource
{
    [Tooltip("아이템 지점 Id. 비우면 GameObject 이름")]
    [SerializeField] private string _id;

    [Tooltip("이 지점에 나올 수 있는 ItemData.itemID")]
    [SerializeField] private List<int> _allowedItemIds = new List<int>();

    [Tooltip("스폰 확률 0~1")]
    [SerializeField, Range(0f, 1f)] private float _spawnChance = 1f;

    public string Id
    {
        get { return LevelLayoutIds.Resolve(_id, this); }
    }

    public void AppendTo(StageLayout layout)
    {
        layout.ItemSpots.Add(new ItemSpot(Id, LevelLayoutData.ToStage(transform.position), new List<int>(_allowedItemIds), _spawnChance));
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = _allowedItemIds.Count == 0 ? new Color(1f, 0.3f, 0.3f, 0.9f) : new Color(0.3f, 0.8f, 1f, 0.9f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.25f, 0.25f);
    }
}
