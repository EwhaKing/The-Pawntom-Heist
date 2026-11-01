using UnityEngine;

/// <summary>
/// 월드에 놓인 줍기 아이템의 "보여 주는 방식". 모든 피어에서 같은 규칙으로 로컬에서만 계산한다(네트워크로 보내지 않는다).
///
/// <list type="bullet">
/// <item><b>눕히기</b>: 판때기처럼 얇은 아이템(열쇠·그림)은 가장 얇은 축이 위를 향하게 눕힌다. 병·보석 같은 입체 아이템은 세워 둔다.</item>
/// <item><b>천천히 돌기</b>: 아이템의 보이는 모양 중심을 고정한 채 수직축으로 돈다. 피벗이 한쪽으로 치우친 아이템(그림)도 제자리에서 돈다.</item>
/// <item><b>바닥에 맞추기</b>: 스폰 지점 아래의 바닥 면을 찾아 그 위로 살짝(hoverHeight) 띄운다. 아이템마다 피벗이 달라 바닥에 박히거나 공중에 뜨던 것을 막는다.</item>
/// </list>
///
/// 방향은 스폰 지점의 <b>좌우 회전(yaw)만</b> 따른다. 지점이 기울어 놓여 있어도 아이템이 같이 기울지 않는다.
/// 스폰 위치·회전 자체는 <see cref="NetworkItem"/>이 모든 피어에 복제한다.
///
/// 사용 위치: 월드 아이템 프리팹 루트(NetworkItem 과 같은 오브젝트). 사망 드롭은 눕히기·바닥 맞추기를 하지 않고 돌기만 한다.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Pawntom/Item/Pickup Display")]
public sealed class PickupDisplay : MonoBehaviour
{
    [Header("회전")]
    [Tooltip("초당 회전 각도. 작을수록 천천히 돈다")]
    [SerializeField] private float spinDegreesPerSecond = 30f;

    [Header("눕히기")]
    [Tooltip("얇은 아이템(가장 얇은 변이 가장 긴 변의 thinRatio 미만)을 눕힌다")]
    [SerializeField] private bool layFlatIfThin = true;

    [SerializeField, Range(0.1f, 0.6f)] private float thinRatio = 0.4f;

    [Header("바닥에 맞추기")]
    [Tooltip("스폰 위치 아래의 바닥 면을 찾아 그 위로 띄운다. 손으로 높이를 잡아 둔 아이템(공중에 떠 있는 열쇠)은 끈다")]
    [SerializeField] private bool snapToGround = true;

    [Tooltip("바닥 면에서 아이템 가장 아래까지 띄우는 높이(m)")]
    [SerializeField, Min(0f)] private float hoverHeight = 0.35f;

    [Tooltip("스폰 중심에서 위로 이만큼 올라가 아래로 쏜다(천장을 맞히지 않도록 작게)")]
    [SerializeField, Min(0.05f)] private float groundSearchUp = 0.6f;

    [Tooltip("스폰 중심에서 아래로 이만큼까지 바닥을 찾는다")]
    [SerializeField, Min(0.1f)] private float groundSearchDown = 3f;

    // 미니맵용 복제 지오메트리(레이어 6~8)는 바닥으로 보지 않는다.
    private const int IgnoredGroundLayers = (1 << 6) | (1 << 7) | (1 << 8);

    private NetworkItem _item;
    private bool _ready;
    private bool _unusable;

    private Vector3 _localCenter;   // 보이는 모양 경계 중심(루트 로컬, 스케일 적용 전)
    private Vector3 _scale;         // 루트 월드 스케일
    private Quaternion _restRotation;
    private Vector3 _anchorCenter;  // 돌아도 고정되는 월드 중심
    private float _angle;

    private static readonly RaycastHit[] GroundHits = new RaycastHit[16];

    private void Awake()
    {
        _item = GetComponent<NetworkItem>();
        _angle = Random.value * 360f; // 여럿이 같은 박자로 돌지 않게
    }

    private void Update()
    {
        if (_unusable)
        {
            return;
        }

        if (!_ready)
        {
            // 네트워크 스폰이 끝나 위치·회전이 복제된 뒤에 시작한다. 네트워크 객체가 아닌 쪽(옛 단일 맵)은 아무것도 하지 않는다.
            if (_item != null && (_item.Object == null || !_item.Object.IsValid))
            {
                return;
            }

            if (!ApplyRestPose())
            {
                _unusable = true;
                return;
            }
        }

        Spin(Time.deltaTime);
    }

    /// <summary>
    /// 눕힘·바닥 맞춤을 계산해 처음 자세를 만든다. 보이는 모양이 없으면 false.
    /// </summary>
    private bool ApplyRestPose()
    {
        if (!MeasureLocalBounds(out Vector3 localCenter, out Vector3 localSize))
        {
            return false;
        }

        _localCenter = localCenter;
        _scale = transform.lossyScale;

        Vector3 worldSize = new Vector3(
            localSize.x * Mathf.Abs(_scale.x),
            localSize.y * Mathf.Abs(_scale.y),
            localSize.z * Mathf.Abs(_scale.z));

        // 드롭은 NetworkItem 이 이미 바닥에 맞춰 놓았다. 네트워크 상태는 스폰된 뒤에만 읽을 수 있다.
        bool dropped = _item != null && _item.Object != null && _item.Object.IsValid && _item.IsDeathDrop;
        float yaw = transform.eulerAngles.y;

        // 눕히기: 가장 얇은 축을 위로.
        Quaternion flat = Quaternion.identity;
        float halfUp = worldSize.y * 0.5f;

        if (layFlatIfThin && !dropped)
        {
            int thin = worldSize.x <= worldSize.y && worldSize.x <= worldSize.z ? 0 : (worldSize.y <= worldSize.z ? 1 : 2);
            float longest = Mathf.Max(worldSize.x, Mathf.Max(worldSize.y, worldSize.z));

            if (worldSize[thin] < longest * thinRatio)
            {
                Vector3 axis = thin == 0 ? Vector3.right : (thin == 1 ? Vector3.up : Vector3.forward);
                flat = Quaternion.FromToRotation(axis, Vector3.up);
                halfUp = worldSize[thin] * 0.5f;
            }
        }

        _restRotation = Quaternion.AngleAxis(yaw, Vector3.up) * flat;

        // 지금 보이는 모양의 중심이 기준점이다(스폰 자세 그대로).
        Vector3 center = transform.TransformPoint(_localCenter);

        if (snapToGround && !dropped)
        {
            // 스폰 지점(= 피벗의 수평 위치) 위에 보이는 모양의 가운데를 둔다. 피벗이 한쪽으로 치우친 아이템(그림)이
            // 받침대 옆으로 빗나가 바닥에 내려앉던 것을 막는다.
            center.x = transform.position.x;
            center.z = transform.position.z;

            if (TryFindGround(center, out float surfaceY))
            {
                center.y = surfaceY + hoverHeight + halfUp;
            }
        }

        _anchorCenter = center;
        _ready = true;
        ApplyPose(_restRotation);
        return true;
    }

    private void Spin(float deltaTime)
    {
        _angle += spinDegreesPerSecond * deltaTime;

        if (_angle > 360f)
        {
            _angle -= 360f;
        }

        ApplyPose(Quaternion.AngleAxis(_angle, Vector3.up) * _restRotation);
    }

    // 보이는 모양의 중심(_anchorCenter)이 제자리에 있도록 피벗 위치를 정한다.
    private void ApplyPose(Quaternion rotation)
    {
        Vector3 offset = rotation * Vector3.Scale(_localCenter, _scale);
        transform.SetPositionAndRotation(_anchorCenter - offset, rotation);
    }

    private bool TryFindGround(Vector3 center, out float surfaceY)
    {
        Vector3 origin = center + Vector3.up * groundSearchUp;
        int count = Physics.RaycastNonAlloc(
            origin, Vector3.down, GroundHits, groundSearchUp + groundSearchDown,
            ~IgnoredGroundLayers, QueryTriggerInteraction.Ignore);

        float best = float.MaxValue;
        surfaceY = 0f;
        bool found = false;

        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = GroundHits[i];

            // 자기 자신과 다른 아이템은 바닥이 아니다.
            if (hit.collider == null || hit.collider.transform.IsChildOf(transform) ||
                hit.collider.GetComponentInParent<NetworkItem>() != null)
            {
                continue;
            }

            if (hit.distance < best)
            {
                best = hit.distance;
                surfaceY = hit.point.y;
                found = true;
            }
        }

        return found;
    }

    /// <summary>
    /// 보이는 모양(메시)의 경계를 루트 로컬 공간에서 구한다. 회전과 무관하게 같은 값이 나온다.
    /// </summary>
    private bool MeasureLocalBounds(out Vector3 center, out Vector3 size)
    {
        Matrix4x4 toRoot = transform.worldToLocalMatrix;
        Bounds bounds = new Bounds();
        bool has = false;
        MeshFilter[] filters = GetComponentsInChildren<MeshFilter>(true);

        for (int i = 0; i < filters.Length; i++)
        {
            MeshFilter filter = filters[i];
            MeshRenderer meshRenderer = filter.GetComponent<MeshRenderer>();

            if (filter.sharedMesh == null || meshRenderer == null || !meshRenderer.enabled)
            {
                continue;
            }

            Bounds meshBounds = filter.sharedMesh.bounds;
            Matrix4x4 matrix = toRoot * filter.transform.localToWorldMatrix;

            for (int c = 0; c < 8; c++)
            {
                Vector3 sign = new Vector3((c & 1) == 0 ? -1f : 1f, (c & 2) == 0 ? -1f : 1f, (c & 4) == 0 ? -1f : 1f);
                Vector3 point = matrix.MultiplyPoint3x4(meshBounds.center + Vector3.Scale(meshBounds.extents, sign));

                if (!has)
                {
                    bounds = new Bounds(point, Vector3.zero);
                    has = true;
                }
                else
                {
                    bounds.Encapsulate(point);
                }
            }
        }

        center = bounds.center;
        size = bounds.size;
        return has && size.sqrMagnitude > 0.000001f;
    }
}
