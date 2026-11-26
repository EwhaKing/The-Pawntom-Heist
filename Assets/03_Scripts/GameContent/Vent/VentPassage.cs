using System.Collections.Generic;
using Pawntom.Stage;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 카오스 삼색 전용 소형 환풍구 통로. GAME_DESIGN §3.2 "카오스 삼색: 소형 환풍구 등 특수 통로 통과".
/// 사용자 결정: 카오스 삼색(CatType.CalicoCat)만 그냥 걸어서 지나가고, 다른 캐릭터는 막힌다.
/// <para>
/// 통로 입구를 막는 BoxCollider(트리거 아님)를 두고, 카오스 삼색 플레이어의 충돌체와만
/// <see cref="Physics.IgnoreCollision(Collider, Collider, bool)"/> 를 건다.
/// 플레이어 이동은 NetworkCharacterController(CharacterController.Move)이고, 호스트(상태 권한 시뮬레이션)와
/// 입력 권한 클라이언트(예측) 양쪽에서 Move 가 돈다. 그래서 이 판정은 <b>모든 피어</b>에서 같은 네트워크 값
/// (<see cref="PlayerData.SelectedCatType"/>, [Networked])으로 똑같이 적용한다.
/// </para>
/// <para>
/// 플레이어가 나중에 스폰되거나 캐릭터가 바뀌어도, 충돌체가 껐다 켜져 무시 설정이 풀려도 주기적으로 다시 적용한다.
/// 적(NavMeshAgent)은 NavMeshObstacle(carve)로 막는다.
/// </para>
/// 네트워크 오브젝트가 아니다 — 판정에 필요한 상태는 플레이어 쪽 [Networked] 값뿐이다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
[AddComponentMenu("Pawntom/Gameplay/Vent Passage")]
public sealed class VentPassage : MonoBehaviour, ILevelLayoutSource
{
    // Player/CatType.cs — 사용자 결정으로 카오스 삼색만 통과한다.
    private const CatType PassableCat = CatType.CalicoCat;

    private const float RefreshSeconds = 0.25f;

    [Header("레벨 데이터")]
    [Tooltip("환풍구 Id. 비우면 GameObject 이름")]
    [SerializeField] private string _id;

    [Header("적 차단")]
    [Tooltip("켜면 시작 시 차단 상자와 같은 크기의 NavMeshObstacle(carve)을 붙여 적이 통로로 경로를 잡지 못하게 한다.\n" +
             "씬에서 직접 NavMeshObstacle 을 붙였으면 그것을 그대로 쓴다.")]
    [SerializeField] private bool _ensureNavMeshObstacle = true;

    [Header("막힘 안내")]
    [Tooltip("다른 캐릭터가 이 거리(m, 수평) 안으로 다가오면 막힘 이벤트를 보낸다")]
    [SerializeField, Min(0f)] private float _blockedHintMargin = 0.6f;

    [SerializeField] private string _blockedMessage = "카오스 삼색만 지나갈 수 있는 좁은 환풍구입니다";

    /// <summary>내 플레이어(카오스 삼색이 아님)가 통로 앞에 막혀 섰다. 안내 문구를 띄우는 데 쓴다.</summary>
    public static event System.Action<VentPassage> LocalPlayerBlocked;

    /// <summary>내 플레이어가 막힌 통로 앞에서 벗어났다(또는 통과 가능해졌다).</summary>
    public static event System.Action<VentPassage> LocalPlayerBlockCleared;

    private static readonly List<VentPassage> ActivePassages = new List<VentPassage>(8);
    private static readonly VentAccessRule Rule = new VentAccessRule((int)PassableCat);

    /// <summary>씬에 켜져 있는 환풍구 통로.</summary>
    public static IReadOnlyList<VentPassage> All
    {
        get { return ActivePassages; }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        LocalPlayerBlocked = null;
        LocalPlayerBlockCleared = null;
        ActivePassages.Clear();
    }

    /// <summary>이 플레이어가 환풍구를 지나갈 수 있는가(모든 피어에서 같은 네트워크 값으로 판정).</summary>
    public static bool CanPlayerPass(PlayerData player)
    {
        bool hasData = player != null && player.Object != null && player.Object.IsValid;
        return Rule.ShouldIgnoreCollision(hasData, hasData ? (int)player.SelectedCatType : -1);
    }

    /// <summary>내 플레이어가 지금 막혀 있는 환풍구. 없으면 null.</summary>
    public static VentPassage LocalBlockedPassage { get; private set; }

    private struct TrackedPlayer
    {
        public PlayerData Player;
        public Collider[] Colliders;
    }

    private readonly List<TrackedPlayer> _tracked = new List<TrackedPlayer>(4);
    private BoxCollider _blocker;
    private float _nextRefresh;
    private bool _localBlocked;

    public string BlockedMessage
    {
        get { return _blockedMessage; }
    }

    /// <summary>내 플레이어가 이 통로 앞에서 막혀 있다.</summary>
    public bool IsLocalPlayerBlocked
    {
        get { return _localBlocked; }
    }

    public string Id
    {
        get { return LevelLayoutIds.Resolve(_id, this); }
    }

    private void Awake()
    {
        _blocker = GetComponent<BoxCollider>();

        if (_blocker.isTrigger)
        {
            Debug.LogWarning("[VentPassage] 차단 BoxCollider 가 트리거라 아무도 막지 못합니다. Is Trigger 를 끄세요.", this);
        }

        EnsureNavMeshObstacle();
    }

    private void OnEnable()
    {
        if (!ActivePassages.Contains(this))
        {
            ActivePassages.Add(this);
        }

        // 충돌체를 껐다 켜면 IgnoreCollision 이 풀린다. 켜지자마자 다시 적용한다.
        _nextRefresh = 0f;
    }

    private void OnDisable()
    {
        ActivePassages.Remove(this);
        _tracked.Clear();
        SetLocalBlocked(false);
    }

    private void Update()
    {
        if (Time.unscaledTime >= _nextRefresh)
        {
            _nextRefresh = Time.unscaledTime + RefreshSeconds;
            RefreshCollisionIgnores();
        }

        UpdateLocalHint();
    }

    /// <summary>
    /// 스폰된 플레이어마다 통과 여부를 다시 계산해 충돌 무시를 맞춘다. 같은 값으로 다시 거는 것은 비용이 거의 없고,
    /// 충돌체가 비활성화됐다 돌아와 풀린 설정까지 되살린다. 새 플레이어를 처음 볼 때만 충돌체 배열을 만든다.
    /// </summary>
    private void RefreshCollisionIgnores()
    {
        if (_blocker == null || !_blocker.enabled)
        {
            return;
        }

        // 사라진 플레이어 정리
        for (int i = _tracked.Count - 1; i >= 0; i--)
        {
            PlayerData tracked = _tracked[i].Player;

            if (tracked == null || !PlayerData.ActivePlayers.Contains(tracked))
            {
                _tracked.RemoveAt(i);
            }
        }

        List<PlayerData> players = PlayerData.ActivePlayers;

        for (int i = 0; i < players.Count; i++)
        {
            PlayerData player = players[i];

            if (player == null || player.Object == null || !player.Object.IsValid)
            {
                continue;
            }

            int index = FindTracked(player);

            if (index < 0)
            {
                _tracked.Add(new TrackedPlayer
                {
                    Player = player,
                    Colliders = player.GetComponentsInChildren<Collider>(true),
                });
                index = _tracked.Count - 1;
            }

            Apply(_tracked[index].Colliders, CanPlayerPass(player));
        }
    }

    private int FindTracked(PlayerData player)
    {
        for (int i = 0; i < _tracked.Count; i++)
        {
            if (_tracked[i].Player == player)
            {
                return i;
            }
        }

        return -1;
    }

    private void Apply(Collider[] colliders, bool ignore)
    {
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider col = colliders[i];

            if (col != null && col != _blocker)
            {
                Physics.IgnoreCollision(col, _blocker, ignore);
            }
        }
    }

    // 내 플레이어가 카오스 삼색이 아닌데 통로 바로 앞(차단 상자 둘레)에 서 있으면 막힘으로 알린다.
    private void UpdateLocalHint()
    {
        PlayerData local = FindLocalPlayer();
        bool blocked = false;

        if (local != null && !local.IsDead && !CanPlayerPass(local) && _blocker != null && _blocker.enabled)
        {
            Bounds box = _blocker.bounds;
            box.Expand(new Vector3(_blockedHintMargin * 2f, 1f, _blockedHintMargin * 2f));
            blocked = box.Contains(local.transform.position);
        }

        SetLocalBlocked(blocked);
    }

    private static PlayerData FindLocalPlayer()
    {
        List<PlayerData> players = PlayerData.ActivePlayers;

        for (int i = 0; i < players.Count; i++)
        {
            PlayerData player = players[i];

            if (player != null && player.Object != null && player.Object.IsValid && player.Object.HasInputAuthority)
            {
                return player;
            }
        }

        return null;
    }

    private void SetLocalBlocked(bool blocked)
    {
        if (blocked == _localBlocked)
        {
            return;
        }

        _localBlocked = blocked;

        if (blocked)
        {
            LocalBlockedPassage = this;
            LocalPlayerBlocked?.Invoke(this);
            return;
        }

        if (LocalBlockedPassage == this)
        {
            LocalBlockedPassage = null;
        }

        LocalPlayerBlockCleared?.Invoke(this);
    }

    private void EnsureNavMeshObstacle()
    {
        if (!_ensureNavMeshObstacle || GetComponent<NavMeshObstacle>() != null)
        {
            return;
        }

        NavMeshObstacle obstacle = gameObject.AddComponent<NavMeshObstacle>();
        obstacle.shape = NavMeshObstacleShape.Box;
        obstacle.center = _blocker.center;
        obstacle.size = _blocker.size;
        obstacle.carving = true;
        obstacle.carveOnlyStationary = true;
    }

    public void AppendTo(StageLayout layout)
    {
        layout.Vents.Add(new VentEntry(Id, LevelLayoutData.ToStage(transform.position)));
    }

    private void OnDrawGizmos()
    {
        BoxCollider box = _blocker != null ? _blocker : GetComponent<BoxCollider>();

        if (box == null)
        {
            return;
        }

        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(0.95f, 0.6f, 0.2f, 0.35f);
        Gizmos.DrawCube(box.center, box.size);
        Gizmos.color = new Color(0.95f, 0.6f, 0.2f, 0.9f);
        Gizmos.DrawWireCube(box.center, box.size);
    }
}
