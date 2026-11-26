using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// <see cref="PathGuide"/> 의 실제 몸체(씬마다 하나). 부탁을 모으고, 0.2초마다 가장 우선인 부탁의 길을 다시 찾아 <see cref="PathGuideView"/> 에 넘긴다.
/// 직접 쓰지 말고 <see cref="PathGuide"/> 를 쓴다.
/// </summary>
public sealed class PathGuideHost : MonoBehaviour
{
    private const float RouteRefreshSeconds = 0.2f;
    private const float ArriveDistance = 1.1f;   // 목적지 영역과 이만큼 가까우면 도착으로 보고 숨긴다

    private sealed class Request
    {
        public object Owner;
        public Transform Player;
        public Bounds Target;
        public int Priority;
        public int Order;
    }

    public static PathGuideHost Current { get; private set; }

    private readonly List<Request> _requests = new List<Request>(4);
    private readonly List<Vector3> _route = new List<Vector3>(128);
    private WalkGrid _grid;
    private PathGuideView _view;
    private int _order;
    private float _nextRefresh;
    private bool _dirty;
    private string _lastReport;

    /// <summary>
    /// 없으면 <paramref name="preferredScene"/>(보통 플레이어가 있는 씬)에 만든다. 걷기 격자는 그 씬의 콜라이더로 만들어지고,
    /// 씬이 내려가면 함께 사라진다. 그 씬이 DontDestroyOnLoad 처럼 쓸 수 없는 씬이면 활성 씬을 쓴다.
    /// </summary>
    public static PathGuideHost GetOrCreate(Scene preferredScene)
    {
        if (Current != null)
        {
            return Current;
        }

        Scene scene = preferredScene.IsValid() && preferredScene.isLoaded && preferredScene.buildIndex >= 0
            ? preferredScene
            : SceneManager.GetActiveScene();

        GameObject host = new GameObject("PathGuide");
        SceneManager.MoveGameObjectToScene(host, scene);
        return host.AddComponent<PathGuideHost>();
    }

    private void Awake()
    {
        Current = this;
        _view = gameObject.AddComponent<PathGuideView>();
        _grid = new WalkGrid(gameObject.scene);
        _grid.Build();

        if (!_grid.IsReady)
        {
            Report(_grid.LastFailure);
        }
    }

    private void OnDestroy()
    {
        if (Current == this)
        {
            Current = null;
        }
    }

    public void SetRequest(object owner, Transform player, Bounds target, int priority)
    {
        Request request = Find(owner);

        if (request == null)
        {
            request = new Request { Owner = owner };
            _requests.Add(request);
        }

        // 같은 부탁이면 순서·갱신 표시를 바꾸지 않는다(매 프레임 불러도 길을 매번 다시 찾지 않게).
        bool changed = request.Player != player || request.Priority != priority || request.Target != target;
        request.Player = player;
        request.Target = target;
        request.Priority = priority;

        if (changed)
        {
            request.Order = ++_order;
            _dirty = true;
        }
    }

    public void RemoveRequest(object owner)
    {
        Request request = Find(owner);

        if (request != null)
        {
            _requests.Remove(request);
            _dirty = true;
        }
    }

    public void RebuildGrid()
    {
        _grid.Build();
        _dirty = true;
    }

    private Request Find(object owner)
    {
        for (int i = 0; i < _requests.Count; i++)
        {
            if (ReferenceEquals(_requests[i].Owner, owner))
            {
                return _requests[i];
            }
        }

        return null;
    }

    private void Update()
    {
        if (!_dirty && Time.unscaledTime < _nextRefresh)
        {
            return;
        }

        _dirty = false;
        _nextRefresh = Time.unscaledTime + RouteRefreshSeconds;

        Request active = PickActive();

        if (active == null)
        {
            _view.Hide();
            return;
        }

        Vector3 from = active.Player.position;
        Vector3 goal;

        if (IsArrived(from, active.Target, out goal))
        {
            _view.Hide();
            return;
        }

        if (!_grid.FindRoute(from, goal, _route))
        {
            Report(_grid.LastFailure);
            _view.Hide();
            return;
        }

        Report(null);
        _view.SetRoute(_route, _grid);
    }

    // 우선순위가 가장 높은(같으면 나중) 살아 있는 부탁. 파괴된 요청자·플레이어의 부탁은 치운다.
    private Request PickActive()
    {
        Request best = null;

        for (int i = _requests.Count - 1; i >= 0; i--)
        {
            Request request = _requests[i];
            Object unityOwner = request.Owner as Object;

            if (request.Player == null || (unityOwner is Object && unityOwner == null))
            {
                _requests.RemoveAt(i);
                continue;
            }

            if (best == null || request.Priority > best.Priority ||
                (request.Priority == best.Priority && request.Order > best.Order))
            {
                best = request;
            }
        }

        return best;
    }

    // 목적지 영역 안(수평)에 들어섰거나 가장 가까운 지점이 충분히 가까우면 도착. goal 은 영역에서 플레이어와 가장 가까운 지점.
    private static bool IsArrived(Vector3 from, Bounds target, out Vector3 goal)
    {
        goal = target.ClosestPoint(new Vector3(from.x, target.center.y, from.z));
        goal.y = target.min.y;

        if (from.x >= target.min.x && from.x <= target.max.x && from.z >= target.min.z && from.z <= target.max.z)
        {
            return true;
        }

        Vector2 flat = new Vector2(goal.x - from.x, goal.z - from.z);
        return flat.sqrMagnitude <= ArriveDistance * ArriveDistance;
    }

    // 길을 못 그린 이유를 바뀔 때만 콘솔에 남긴다(에디터·개발 빌드). null 이면 정상으로 돌아온 것.
    [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    private void Report(string reason)
    {
        if (reason == _lastReport)
        {
            return;
        }

        _lastReport = reason;

        if (reason != null)
        {
            Debug.LogWarning("[PathGuide] 화살표를 숨김: " + reason);
        }
    }
}
