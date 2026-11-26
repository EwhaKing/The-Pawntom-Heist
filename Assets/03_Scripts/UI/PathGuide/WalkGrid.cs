using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// 길 안내 화살표용 걷기 격자. 씬의 콜라이더로 바닥을 <see cref="CellSize"/> 칸으로 나눠 빈 칸 / 벽 / 점프로 넘는 칸을 정하고,
/// A* 로 두 지점 사이의 길(꺾이는 점 목록)을 찾는다. NavMesh 는 쓰지 않는다.
/// <para>
/// 지금은 <b>한 층</b>(가장 낮은 넓은 바닥) 기준이다. 여러 층을 지원하려면 층마다 격자를 따로 두고 계단으로 이어야 한다.
/// </para>
/// <para>
/// 지형으로 보지 않는 것: 트리거, 다른 씬, 네트워크 오브젝트(플레이어·적·아이템·버튼·금고), CharacterController·NavMeshAgent 를 가진 것,
/// 격벽(<see cref="ControllableWall"/> — 닫혀 있어도 열 수 있는 문이라 길로 본다), <see cref="PathGuideIgnore"/> 를 붙인 것,
/// 물리로 움직이는 물체. 낮고 작은 물체(<see cref="JumpableHeight"/> 이하, <see cref="JumpableFootprint"/> 미만)는 점프 칸이다.
/// </para>
/// </summary>
public sealed class WalkGrid
{
    public const float CellSize = 0.5f;          // 격자 한 칸(m)

    private const float BodyRadius = 0.45f;      // 벽에서 이만큼은 떨어진 칸만 지나간다(크면 길 가운데로, 너무 크면 좁은 문을 못 지난다)
    private const float BodyBottom = 0.15f;      // 바닥에서 이 높이부터
    private const float BodyTop = 1.5f;          // 이 높이까지 막혀 있는지 본다
    private const float JumpableHeight = 1.1f;   // 이 높이 이하이고
    private const float JumpableFootprint = 4f;  // 가로·세로가 이보다 작으면 점프로 넘는 물체(바닥판·긴 책상은 제외)
    private const float JumpCost = 6f;           // 점프 칸 한 칸의 비용(빈 칸 = 1). 돌아가는 길이 훨씬 길면 넘는다
    private const float SnapRadius = 3f;         // 시작·목적지가 막힌 칸이면 이 반경 안의 가장 가까운 빈 칸으로 옮긴다
    private const int MaxGridCells = 400 * 400;  // 거대한 바닥판이 있어도 이 칸 수로 자른다
    private const int MaxSearchNodes = 120000;

    private const byte CellBlocked = 0;
    private const byte CellFree = 1;
    private const byte CellJump = 2;

    private readonly Scene _scene;
    private readonly Dictionary<Collider, byte> _colliderKinds = new Dictionary<Collider, byte>(256);
    private readonly Collider[] _overlap = new Collider[32];
    private readonly RaycastHit[] _rayHits = new RaycastHit[16];

    private int _width;
    private int _height;
    private float _x0;
    private float _z0;
    private byte[] _cells;
    private float[] _floorY;
    private float[] _jumpTop;

    // A*
    private float[] _g;
    private int[] _parent;
    private int[] _stamp;
    private int _stampValue;
    private int[] _heapItems;
    private float[] _heapKeys;
    private int _heapCount;
    private readonly List<int> _cellPath = new List<int>(256);

    // 같은 칸에서 같은 칸으로면 다시 찾지 않는다.
    private int _lastStart = -1;
    private int _lastGoal = -1;
    private bool _lastFound;

    /// <summary>격자를 만들었는가(바닥을 못 찾으면 false 로 남는다).</summary>
    public bool IsReady { get; private set; }

    /// <summary>마지막으로 길을 못 찾은 이유(진단용). 찾았으면 null.</summary>
    public string LastFailure { get; private set; }

    public WalkGrid(Scene scene)
    {
        _scene = scene;
    }

    // ───────────── 만들기 ─────────────

    /// <summary>씬 콜라이더로 격자를 (다시) 만든다. 지형이 바뀌었으면 다시 부르면 된다.</summary>
    public void Build()
    {
        IsReady = false;
        _colliderKinds.Clear();
        _lastStart = -1;
        _lastGoal = -1;

        Bounds area;
        float floorBase;

        if (!TryComputeLevelArea(out area, out floorBase))
        {
            LastFailure = "씬에서 바닥을 찾지 못함";
            return;
        }

        _width = Mathf.Max(1, Mathf.CeilToInt(area.size.x / CellSize));
        _height = Mathf.Max(1, Mathf.CeilToInt(area.size.z / CellSize));

        if ((long)_width * _height > MaxGridCells)
        {
            float shrink = Mathf.Sqrt(MaxGridCells / (float)((long)_width * _height));
            _width = Mathf.Max(1, Mathf.FloorToInt(_width * shrink));
            _height = Mathf.Max(1, Mathf.FloorToInt(_height * shrink));
            Debug.LogWarning("[PathGuide] 맵이 커서 길 안내 격자를 잘랐습니다.");
        }

        _x0 = area.center.x - _width * CellSize * 0.5f;
        _z0 = area.center.z - _height * CellSize * 0.5f;

        int count = _width * _height;
        _cells = new byte[count];
        _floorY = new float[count];
        _jumpTop = new float[count];
        _g = new float[count];
        _parent = new int[count];
        _stamp = new int[count];
        _heapItems = new int[count];
        _heapKeys = new float[count];

        for (int z = 0; z < _height; z++)
        {
            for (int x = 0; x < _width; x++)
            {
                int index = z * _width + x;
                _cells[index] = ProbeCell(CellCenter(x, z, floorBase), floorBase, out _floorY[index], out _jumpTop[index]);
            }
        }

        IsReady = true;
        LastFailure = null;
    }

    // 콜라이더 종류: 무시(CellFree) / 벽(CellBlocked) / 점프로 넘는 물체(CellJump). 한 번 판정하면 기억한다.
    private byte ClassifyCollider(Collider candidate)
    {
        if (candidate == null)
        {
            return CellFree;
        }

        byte kind;

        if (_colliderKinds.TryGetValue(candidate, out kind))
        {
            return kind;
        }

        kind = ComputeKind(candidate);
        _colliderKinds[candidate] = kind;
        return kind;
    }

    private byte ComputeKind(Collider candidate)
    {
        if (!candidate.enabled || candidate.isTrigger || candidate.gameObject.scene != _scene)
        {
            return CellFree;
        }

        // 플레이어 몸 모델의 MeshCollider 는 CharacterController 와 형제라 부모 검사만으로는 안 걸린다 → NetworkObject 로 거른다.
        if (candidate.GetComponentInParent<Fusion.NetworkObject>() != null ||
            candidate.GetComponentInParent<CharacterController>() != null ||
            candidate.GetComponentInParent<NavMeshAgent>() != null ||
            candidate.GetComponentInParent<ControllableWall>() != null ||
            candidate.GetComponentInParent<PathGuideIgnore>() != null)
        {
            return CellFree;
        }

        Rigidbody body = candidate.attachedRigidbody;

        if (body != null && !body.isKinematic)
        {
            return CellFree;
        }

        Vector3 size = candidate.bounds.size;
        bool jumpable = size.y <= JumpableHeight && size.x < JumpableFootprint && size.z < JumpableFootprint;
        return jumpable ? CellJump : CellBlocked;
    }

    // 한 칸 검사: 바닥이 있는가, 몸 높이 범위에 벽이 있는가, 점프로 넘는 물체만 있는가.
    private byte ProbeCell(Vector3 center, float floorBase, out float floorY, out float jumpTop)
    {
        floorY = floorBase;
        jumpTop = 0f;

        // 바닥: 위에서 아래로 쏴서 가장 낮은 지형 윗면(점프 물체 윗면이 아니라 진짜 바닥).
        Vector3 origin = new Vector3(center.x, floorBase + BodyTop, center.z);
        int hitCount = Physics.RaycastNonAlloc(origin, Vector3.down, _rayHits, BodyTop + 1.5f, ~0, QueryTriggerInteraction.Ignore);
        bool hasFloor = false;
        float lowest = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = _rayHits[i];

            if (hit.normal.y < 0.7f || ClassifyCollider(hit.collider) != CellBlocked)
            {
                continue;
            }

            if (hit.point.y < lowest)
            {
                lowest = hit.point.y;
                hasFloor = true;
            }
        }

        if (!hasFloor)
        {
            return CellBlocked;
        }

        floorY = lowest;

        float half = (BodyTop - BodyBottom) * 0.5f;
        Vector3 boxCenter = new Vector3(center.x, floorY + BodyBottom + half, center.z);
        Vector3 extents = new Vector3(BodyRadius, half, BodyRadius);
        int overlapCount = Physics.OverlapBoxNonAlloc(boxCenter, extents, _overlap, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
        byte result = CellFree;

        for (int i = 0; i < overlapCount; i++)
        {
            byte kind = ClassifyCollider(_overlap[i]);

            if (kind == CellBlocked)
            {
                return CellBlocked;
            }

            if (kind == CellJump)
            {
                result = CellJump;
                jumpTop = Mathf.Max(jumpTop, _overlap[i].bounds.max.y);
            }
        }

        return result;
    }

    // 지형 콜라이더가 차지하는 영역과 바닥 높이(넓고 얇은 바닥판 중 가장 낮은 윗면).
    private bool TryComputeLevelArea(out Bounds area, out float floorBase)
    {
        area = default;
        floorBase = 0f;
        bool hasArea = false;
        bool hasFloor = false;
        Collider[] colliders = Object.FindObjectsByType<Collider>(FindObjectsSortMode.None);

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider candidate = colliders[i];

            if (ClassifyCollider(candidate) != CellBlocked)
            {
                continue;
            }

            Bounds bounds = candidate.bounds;

            if (!hasArea)
            {
                area = bounds;
                hasArea = true;
            }
            else
            {
                area.Encapsulate(bounds);
            }

            bool floorLike = bounds.size.x >= JumpableFootprint && bounds.size.z >= JumpableFootprint && bounds.size.y < 0.5f;

            if (floorLike && (!hasFloor || bounds.max.y < floorBase))
            {
                floorBase = bounds.max.y;
                hasFloor = true;
            }
        }

        return hasArea && hasFloor;
    }

    // ───────────── 칸 조회(그리는 쪽이 쓴다) ─────────────

    /// <summary>그 위치 칸의 바닥 높이. 격자 밖이면 <paramref name="fallback"/>.</summary>
    public float FloorAt(Vector3 position, float fallback)
    {
        int index = CellIndexAt(position);
        return index >= 0 ? _floorY[index] : fallback;
    }

    /// <summary>그 위치가 점프로 넘는 칸이면 넘어야 할 높이(바닥에서 장애물 윗면까지), 아니면 0.</summary>
    public float JumpHeightAt(Vector3 position)
    {
        int index = CellIndexAt(position);
        return index >= 0 && _cells[index] == CellJump ? Mathf.Max(0f, _jumpTop[index] - _floorY[index]) : 0f;
    }

    private Vector3 CellCenter(int x, int z, float y)
    {
        return new Vector3(_x0 + (x + 0.5f) * CellSize, y, _z0 + (z + 0.5f) * CellSize);
    }

    private Vector3 CellCenter(int index)
    {
        return CellCenter(index % _width, index / _width, _floorY[index]);
    }

    private int CellIndexAt(Vector3 position)
    {
        if (!IsReady)
        {
            return -1;
        }

        int x = Mathf.FloorToInt((position.x - _x0) / CellSize);
        int z = Mathf.FloorToInt((position.z - _z0) / CellSize);

        if (x < 0 || z < 0 || x >= _width || z >= _height)
        {
            return -1;
        }

        return z * _width + x;
    }

    // 막힌 칸이면 반경 안의 가장 가까운 지나갈 수 있는 칸으로 옮긴다(빈 칸 우선).
    private int SnapToPassable(Vector3 position)
    {
        int start = CellIndexAt(position);

        if (start >= 0 && _cells[start] != CellBlocked)
        {
            return start;
        }

        int cx = Mathf.FloorToInt((position.x - _x0) / CellSize);
        int cz = Mathf.FloorToInt((position.z - _z0) / CellSize);
        int radius = Mathf.CeilToInt(SnapRadius / CellSize);
        int best = -1;
        float bestScore = float.MaxValue;

        for (int dz = -radius; dz <= radius; dz++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                int x = cx + dx;
                int z = cz + dz;

                if (x < 0 || z < 0 || x >= _width || z >= _height)
                {
                    continue;
                }

                int index = z * _width + x;

                if (_cells[index] == CellBlocked)
                {
                    continue;
                }

                float score = dx * dx + dz * dz + (_cells[index] == CellJump ? 4f : 0f);

                if (score < bestScore)
                {
                    bestScore = score;
                    best = index;
                }
            }
        }

        return best;
    }

    // ───────────── 길 찾기 ─────────────

    /// <summary>
    /// <paramref name="from"/>(발 위치)에서 <paramref name="goal"/> 까지의 길을 꺾이는 점 목록으로 채운다(첫 점은 발밑).
    /// 못 찾으면 false 이고 이유는 <see cref="LastFailure"/>.
    /// </summary>
    public bool FindRoute(Vector3 from, Vector3 goal, List<Vector3> route)
    {
        route.Clear();

        if (!IsReady)
        {
            LastFailure = LastFailure ?? "길 격자가 없음";
            return false;
        }

        int startCell = SnapToPassable(from);

        if (startCell < 0)
        {
            LastFailure = "플레이어 주변이 모두 막힘 " + from + " / 막고 있는 것: " + DescribeBlockers(from);
            return false;
        }

        int goalCell = SnapToPassable(goal);

        if (goalCell < 0)
        {
            LastFailure = "목적지 주변이 모두 막힘 " + goal + " / 막고 있는 것: " + DescribeBlockers(goal);
            return false;
        }

        bool found;

        if (startCell == _lastStart && goalCell == _lastGoal)
        {
            found = _lastFound;
        }
        else
        {
            found = Search(startCell, goalCell);
            _lastStart = startCell;
            _lastGoal = goalCell;
            _lastFound = found;
        }

        if (!found)
        {
            LastFailure = "목적지까지 이어지는 길이 없음 " + CellCenter(startCell) + " → " + CellCenter(goalCell);
            return false;
        }

        LastFailure = null;

        // 발밑부터 시작해서, 곧게 갈 수 있는 칸은 건너뛰며(줄 당기기) 꺾이는 곳만 남긴다.
        route.Add(new Vector3(from.x, _floorY[startCell], from.z));
        int anchor = 0;

        while (anchor < _cellPath.Count - 1)
        {
            int next = anchor + 1;

            for (int k = _cellPath.Count - 1; k > anchor + 1; k--)
            {
                if (CanWalkStraight(_cellPath[anchor], _cellPath[k]))
                {
                    next = k;
                    break;
                }
            }

            route.Add(CellCenter(_cellPath[next]));
            anchor = next;
        }

        return route.Count >= 2;
    }

    // 두 칸 사이를 곧게 가도 막힌 칸을 지나지 않는가. 점프 칸 여부가 바뀌는 곳에서는 꺾어 둔다(넘는 구간을 정확히 남기려고).
    private bool CanWalkStraight(int fromCell, int toCell)
    {
        Vector3 a = CellCenter(fromCell);
        Vector3 b = CellCenter(toCell);
        float length = Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
        int steps = Mathf.Max(1, Mathf.CeilToInt(length / (CellSize * 0.25f)));
        bool startJump = _cells[fromCell] == CellJump;

        for (int i = 1; i <= steps; i++)
        {
            int index = CellIndexAt(Vector3.Lerp(a, b, i / (float)steps));

            if (index < 0 || _cells[index] == CellBlocked)
            {
                return false;
            }

            if ((_cells[index] == CellJump) != startJump && index != toCell)
            {
                return false;
            }
        }

        return true;
    }

    private bool Search(int start, int goal)
    {
        _cellPath.Clear();
        _stampValue++;

        if (_stampValue == int.MaxValue)
        {
            System.Array.Clear(_stamp, 0, _stamp.Length);
            _stampValue = 1;
        }

        int gx = goal % _width;
        int gz = goal / _width;

        _heapCount = 0;
        _g[start] = 0f;
        _parent[start] = -1;
        _stamp[start] = _stampValue;
        HeapPush(start, Heuristic(start % _width, start / _width, gx, gz));

        int expanded = 0;

        while (_heapCount > 0)
        {
            int current = HeapPop();

            if (current == goal)
            {
                for (int c = goal; c >= 0; c = _parent[c])
                {
                    _cellPath.Add(c);
                }

                _cellPath.Reverse();
                return true;
            }

            if (++expanded > MaxSearchNodes)
            {
                return false;
            }

            int cx = current % _width;
            int cz = current / _width;

            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dz == 0)
                    {
                        continue;
                    }

                    int nx = cx + dx;
                    int nz = cz + dz;

                    if (nx < 0 || nz < 0 || nx >= _width || nz >= _height)
                    {
                        continue;
                    }

                    int neighbor = nz * _width + nx;

                    if (_cells[neighbor] == CellBlocked)
                    {
                        continue;
                    }

                    // 대각선은 양옆 칸이 모두 열려 있을 때만(벽 모서리를 깎지 않게).
                    if (dx != 0 && dz != 0 &&
                        (_cells[cz * _width + nx] == CellBlocked || _cells[nz * _width + cx] == CellBlocked))
                    {
                        continue;
                    }

                    float step = (dx != 0 && dz != 0) ? 1.41421356f : 1f;

                    if (_cells[neighbor] == CellJump)
                    {
                        step *= JumpCost;
                    }

                    float cost = _g[current] + step;

                    if (_stamp[neighbor] == _stampValue && cost >= _g[neighbor])
                    {
                        continue;
                    }

                    _stamp[neighbor] = _stampValue;
                    _g[neighbor] = cost;
                    _parent[neighbor] = current;
                    HeapPush(neighbor, cost + Heuristic(nx, nz, gx, gz));
                }
            }
        }

        return false;
    }

    // 8방향 격자 거리(옥타일).
    private static float Heuristic(int x0, int z0, int x1, int z1)
    {
        int dx = Mathf.Abs(x1 - x0);
        int dz = Mathf.Abs(z1 - z0);
        return (dx + dz) + (1.41421356f - 2f) * Mathf.Min(dx, dz);
    }

    // 최소 힙. 같은 칸이 여러 번 들어갈 수 있다(더 싼 길을 찾으면 다시 넣는다).
    private void HeapPush(int item, float key)
    {
        if (_heapCount >= _heapItems.Length)
        {
            System.Array.Resize(ref _heapItems, _heapItems.Length * 2);
            System.Array.Resize(ref _heapKeys, _heapKeys.Length * 2);
        }

        int i = _heapCount++;
        _heapItems[i] = item;
        _heapKeys[i] = key;

        while (i > 0)
        {
            int parent = (i - 1) / 2;

            if (_heapKeys[parent] <= _heapKeys[i])
            {
                break;
            }

            Swap(i, parent);
            i = parent;
        }
    }

    private int HeapPop()
    {
        int top = _heapItems[0];
        _heapCount--;

        if (_heapCount > 0)
        {
            _heapItems[0] = _heapItems[_heapCount];
            _heapKeys[0] = _heapKeys[_heapCount];
            int i = 0;

            while (true)
            {
                int left = i * 2 + 1;
                int right = left + 1;
                int smallest = i;

                if (left < _heapCount && _heapKeys[left] < _heapKeys[smallest])
                {
                    smallest = left;
                }

                if (right < _heapCount && _heapKeys[right] < _heapKeys[smallest])
                {
                    smallest = right;
                }

                if (smallest == i)
                {
                    break;
                }

                Swap(i, smallest);
                i = smallest;
            }
        }

        return top;
    }

    private void Swap(int a, int b)
    {
        int item = _heapItems[a];
        _heapItems[a] = _heapItems[b];
        _heapItems[b] = item;

        float key = _heapKeys[a];
        _heapKeys[a] = _heapKeys[b];
        _heapKeys[b] = key;
    }

    // 그 자리를 막고 있는 지형 콜라이더 이름(진단용).
    private string DescribeBlockers(Vector3 position)
    {
        Vector3 center = new Vector3(position.x, position.y + (BodyBottom + BodyTop) * 0.5f, position.z);
        Vector3 extents = new Vector3(BodyRadius, (BodyTop - BodyBottom) * 0.5f, BodyRadius);
        int count = Physics.OverlapBoxNonAlloc(center, extents, _overlap, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
        System.Text.StringBuilder names = new System.Text.StringBuilder();

        for (int i = 0; i < count; i++)
        {
            if (ClassifyCollider(_overlap[i]) != CellBlocked)
            {
                continue;
            }

            if (names.Length > 0)
            {
                names.Append(", ");
            }

            names.Append(GetPath(_overlap[i].transform));
        }

        return names.Length > 0 ? names.ToString() : "(몸 높이에는 없음 — 바닥을 못 찾았을 수 있음)";
    }

    private static string GetPath(Transform t)
    {
        string result = t.name;

        for (Transform p = t.parent; p != null; p = p.parent)
        {
            result = p.name + "/" + result;
        }

        return result;
    }
}