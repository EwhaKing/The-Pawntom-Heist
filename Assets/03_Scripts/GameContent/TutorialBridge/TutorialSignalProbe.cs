using System;
using Fusion;
using Hacking;
using Pawntom.Enemy.Authoring;
using Pawntom.Enemy.Core;
using Pawntom.EnemyBridge;
using Pawntom.Tutorial.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Pawntom.TutorialBridge
{
    /// <summary>
    /// 게임 상태를 읽어 튜토리얼 신호로 바꾼다. 게임 코드는 튜토리얼을 모르고, 튜토리얼만 게임을 관찰한다.
    /// <para>
    /// 대부분은 주기적으로 상태를 확인해 "조건이 참인 동안" 신호를 보낸다(상태형). 해킹·단말기·가방 가득 참처럼
    /// 순간에만 의미가 있는 것은 게임 쪽 정적 이벤트(HackingManager / MainbaseControlUI / InventoryManager)를 구독한다.
    /// </para>
    /// <para>
    /// 씬에 <see cref="TutorialZone"/>이 없는 지점은 기존 오브젝트로 대신 감지한다:
    /// 복도 A 끝 격벽 = 잠긴 ControllableWall 근처, 푸른 격벽 = 해금된 ControllableWall 근처, 단말기 = ControlMachine 근처,
    /// 본부 = MainbaseZone 박스. 나머지(상자·파밍 방·금고 복도 등)는 영역을 배치해야 신호가 난다.
    /// </para>
    /// </summary>
    public sealed class TutorialSignalProbe : MonoBehaviour
    {
        private const float PlayerSearchInterval = 0.5f;
        private const float SceneCacheInterval = 3f;
        private const int OverlapCapacity = 24;

        [Header("주기")]
        [SerializeField, Min(0.02f)] private float pollInterval = 0.1f;

        [Header("근접 판정 (월드 단위)")]
        [SerializeField, Min(0.1f)] private float terminalRadius = 3.5f;
        [SerializeField, Min(0.1f)] private float doorRadius = 3.5f;
        [SerializeField, Min(0.1f)] private float lootRadius = 2.5f;

        [Header("2단계 조작 확인")]
        [Tooltip("2-1 'WASD로 이동하고 마우스로 둘러봅시다' 완료로 볼 이동 거리")]
        [SerializeField, Min(0f)] private float moveDistanceForHint = 3f;
        [Tooltip("2-1 완료로 볼 누적 회전 각도")]
        [SerializeField, Min(0f)] private float lookAngleForHint = 45f;
        [Tooltip("2-3 '뒤를 돌아본 후'로 볼 각도(기준 방향 대비)")]
        [SerializeField, Range(90f, 180f)] private float lookBackAngle = 135f;

        [Header("4단계 CCTV")]
        [Tooltip("CCTV(위치) 탭을 이만큼 보고 있으면 4-5 '경비견 위치 확인'으로 본다")]
        [SerializeField, Min(0f)] private float cctvCheckSeconds = 2.5f;

        [Header("아이템")]
        [Tooltip("전설의 캣닢 아이템 ID. RuleManager/MainbaseManager 기본값(1)과 같다")]
        [SerializeField] private int catnipItemId = 1;

        [Header("상황별 반복 대사 (튜토리얼 기획서 '상황별 반복 대사')")]
        [Tooltip("이동 입력 없이 이만큼 지나면 '입력 없음(이동)'")]
        [SerializeField, Min(1f)] private float idleMoveSeconds = 10f;
        [Tooltip("단말기를 연 채 조작 없이 이만큼 지나면 '입력 없음(격벽)'")]
        [SerializeField, Min(1f)] private float idleTerminalSeconds = 15f;
        [Tooltip("금고 앞에서 해킹 없이 이만큼 지나면 '입력 없음(금고)'")]
        [SerializeField, Min(1f)] private float idleVaultSeconds = 10f;
        [Tooltip("체력 비율이 이 값 이하로 내려가면 '체력 낮음'")]
        [SerializeField, Range(0.05f, 0.9f)] private float lowHealthRatio = 0.3f;

        // ── 로컬 플레이어 ──
        private PlayerData _player;
        private Transform _playerTransform;
        private InventoryManager _inventory;
        private float _nextPlayerSearch;

        // ── 씬 캐시 ──
        private ControlMachine[] _terminals = new ControlMachine[0];
        private Bounds[] _terminalBounds = new Bounds[0];
        private ControllableWall[] _walls = new ControllableWall[0];
        private Bounds[] _wallBounds = new Bounds[0];
        private bool[] _wallWasOpen = new bool[0];
        private MainbaseZone[] _bases = new MainbaseZone[0];
        private BoxCollider[] _baseBoxes = new BoxCollider[0];
        private EnemyAgent[] _enemies = new EnemyAgent[0];
        private NetworkEnemyAgent[] _networkEnemies = new NetworkEnemyAgent[0];
        private float _nextSceneCache;

        private readonly Collider[] _overlap = new Collider[OverlapCapacity];

        // ── 진행 상태 ──
        private float _nextPoll;
        private bool _controlEnabled;
        private Vector3 _lastPosition;
        private float _lastYaw;
        private float _movedDistance;
        private float _turnedDegrees;
        private Vector3 _lookReference = Vector3.forward;

        private bool _terminalOpen;
        private bool _cctvTabVisible;
        private float _cctvSeconds;
        private bool _cctvChecked;

        private bool _hackIsVault;
        private bool _hackActive;
        private int _lastInventoryCount = -1;

        // ── 상황 감지 ──
        private bool _dialogueBusy;
        private float _idleMove;
        private float _idleTerminal;
        private float _idleVault;
        private bool _lowHealthFired;
        private bool _isDead;
        private bool _awaitingRespawn;
        private readonly System.Collections.Generic.Dictionary<EnemyAgent, EnemyState> _enemyStates =
            new System.Collections.Generic.Dictionary<EnemyAgent, EnemyState>();

        private InputSystemActions _actions;

        /// <summary>신호가 났다. 디렉터가 시퀀서로 넘긴다.</summary>
        public event Action<TutorialSignal> SignalRaised;

        public bool HasLocalPlayer
        {
            get { return _player != null && _playerTransform != null; }
        }

        public Transform LocalPlayerTransform
        {
            get { return _playerTransform; }
        }

        public PlayerData LocalPlayer
        {
            get { return _player; }
        }

        public InventoryManager LocalInventory
        {
            get { return _inventory; }
        }

        public bool IsPlayerInMainbase { get; private set; }

        public bool IsAnyEnemyChasing { get; private set; }

        private void Awake()
        {
            _actions = new InputSystemActions();
        }

        private void OnEnable()
        {
            _actions?.Player.Enable();

            HackingManager.HackingStarted += HandleHackingStarted;
            HackingManager.HackingFinished += HandleHackingFinished;
            MainbaseControlUI.Opened += HandleTerminalOpened;
            MainbaseControlUI.Closed += HandleTerminalClosed;
            MainbaseControlUI.TabShown += HandleTerminalTab;
            InventoryManager.AddRejectedFull += HandleInventoryFull;
        }

        private void OnDisable()
        {
            _actions?.Player.Disable();

            HackingManager.HackingStarted -= HandleHackingStarted;
            HackingManager.HackingFinished -= HandleHackingFinished;
            MainbaseControlUI.Opened -= HandleTerminalOpened;
            MainbaseControlUI.Closed -= HandleTerminalClosed;
            MainbaseControlUI.TabShown -= HandleTerminalTab;
            InventoryManager.AddRejectedFull -= HandleInventoryFull;
        }

        private void OnDestroy()
        {
            _actions?.Dispose();
            _actions = null;
        }

        /// <summary>1-7 조작 가능 전환. 2-1 의 이동·회전 누적을 여기서부터 잰다.</summary>
        public void MarkControlEnabled()
        {
            _controlEnabled = true;
            _movedDistance = 0f;
            _turnedDegrees = 0f;

            if (_playerTransform != null)
            {
                _lastPosition = _playerTransform.position;
                _lastYaw = _playerTransform.eulerAngles.y;
            }
        }

        /// <summary>
        /// 대사가 떠 있거나 기다리는 중이면 true. 그동안은 "입력 없음" 시간을 재지 않는다(대사를 듣느라 멈춘 것은 막힌 게 아니다).
        /// </summary>
        public void SetDialogueBusy(bool busy)
        {
            _dialogueBusy = busy;
        }

        /// <summary>메인 비트가 시작될 때마다 "뒤를 돌아봄" 기준 방향을 지금 바라보는 방향으로 다시 잡는다.</summary>
        public void ResetLookReference()
        {
            if (_playerTransform != null)
            {
                _lookReference = Flat(_playerTransform.forward);
            }
        }

        private void Update()
        {
            float now = Time.unscaledTime;

            if (!IsPlayerValid())
            {
                if (now >= _nextPlayerSearch)
                {
                    _nextPlayerSearch = now + PlayerSearchInterval;
                    FindLocalPlayer();
                }

                return;
            }

            if (now >= _nextSceneCache)
            {
                _nextSceneCache = now + SceneCacheInterval;
                RefreshSceneCache();
            }

            TrackLife();

            if (_isDead)
            {
                // 죽어 있는 동안은 진행 신호를 내지 않는다(사망 처리 방식 미확정 — 재시작 지점에서 이어간다).
                ResetIdleTimers();
                return;
            }

            TrackMovement();
            TrackCctv(Time.unscaledDeltaTime);
            DetectJump();
            TrackIdle(Time.unscaledDeltaTime);

            if (now < _nextPoll)
            {
                return;
            }

            _nextPoll = now + pollInterval;
            Poll();
        }

        private void Poll()
        {
            Vector3 position = _playerTransform.position;

            PollBasicControls();
            PollZones(position);
            PollEnemies();
            PollMainbase(position);
            PollTerminal(position);
            PollDoors(position);
            PollInventory();
            PollLoot(position);
        }

        // ───────────── 2단계 ─────────────

        private void TrackMovement()
        {
            if (!_controlEnabled)
            {
                return;
            }

            Vector3 position = _playerTransform.position;
            Vector3 delta = position - _lastPosition;
            delta.y = 0f;
            _movedDistance += delta.magnitude;
            _lastPosition = position;

            float yaw = _playerTransform.eulerAngles.y;
            _turnedDegrees += Mathf.Abs(Mathf.DeltaAngle(_lastYaw, yaw));
            _lastYaw = yaw;
        }

        private void DetectJump()
        {
            if (!_controlEnabled || _actions == null || GameplayInputBlocker.IsBlocked || TutorialInputLock.IsLocked)
            {
                return;
            }

            if (_actions.Player.Jump.WasPressedThisFrame())
            {
                Emit(TutorialSignal.Jumped);
            }
        }

        private void PollBasicControls()
        {
            if (_controlEnabled && _movedDistance >= moveDistanceForHint && _turnedDegrees >= lookAngleForHint)
            {
                Emit(TutorialSignal.MovedAndLooked);
            }

            PlayerRef local = _player.Object.InputAuthority;
            var traces = FurBallTrace.Active;

            for (int i = 0; i < traces.Count; i++)
            {
                FurBallTrace trace = traces[i];

                if (trace != null && trace.Object != null && trace.Object.IsValid && trace.WasCreatedBy(local))
                {
                    Emit(TutorialSignal.FirstFootprint);
                    break;
                }
            }

            if (Vector3.Angle(Flat(_playerTransform.forward), _lookReference) >= lookBackAngle)
            {
                Emit(TutorialSignal.LookedBack);
            }

            if (_player.IsSprinting)
            {
                Emit(TutorialSignal.SprintStarted);
            }

            float stamina = _player.StaminaRatio;

            if (stamina <= 0.5f)
            {
                Emit(TutorialSignal.StaminaBelowHalf);
            }

            if (stamina <= 0.3f)
            {
                Emit(TutorialSignal.StaminaBelow30);
            }
        }

        // ───────────── 상황별 반복 대사 ─────────────

        /// <summary>체력 30% 이하 / 사망 / 재시작을 경계에서 한 번씩 알린다.</summary>
        private void TrackLife()
        {
            bool dead = _player.IsDead;

            if (dead && !_isDead)
            {
                _isDead = true;
                _awaitingRespawn = true;
                Emit(TutorialSignal.PlayerDied);
                return;
            }

            if (!dead && _awaitingRespawn)
            {
                // 같은 플레이어가 되살아났거나, 죽은 플레이어가 사라지고 새로 스폰됐다.
                _isDead = false;
                _awaitingRespawn = false;
                _lowHealthFired = false;
                _lastPosition = _playerTransform.position;
                _lastYaw = _playerTransform.eulerAngles.y;
                Emit(TutorialSignal.PlayerRespawned);
            }

            if (dead || _player.MaxHealth <= 0f)
            {
                return; // MaxHealth 가 0이면 아직 능력치 초기화 전이다(비율 0 으로 오판하지 않도록).
            }

            float ratio = _player.HealthRatio;

            if (ratio <= lowHealthRatio && !_lowHealthFired)
            {
                _lowHealthFired = true;
                Emit(TutorialSignal.HealthLow);
            }
            else if (ratio > lowHealthRatio)
            {
                _lowHealthFired = false; // 회복하면 다시 경고할 수 있다
            }
        }

        private void TrackIdle(float deltaTime)
        {
            bool nearVault = IsNearClosedVault(_playerTransform.position);
            bool calm = !_dialogueBusy && !_hackActive;

            // 이동: 조작 가능 · UI/잠금 없음 · 금고 앞 아님(금고 대사가 맡는다)
            bool moveCounting = calm &&
                                _controlEnabled &&
                                !nearVault &&
                                !TutorialInputLock.IsLocked &&
                                !GameplayInputBlocker.IsBlocked &&
                                _actions != null &&
                                _actions.Player.Move.ReadValue<Vector2>().sqrMagnitude < 0.01f;

            _idleMove = Accumulate(_idleMove, moveCounting, deltaTime, idleMoveSeconds, TutorialSignal.IdleNoMove);

            // 격벽(단말기): 연 채로 키·클릭·휠 입력 없음
            bool terminalCounting = calm && _terminalOpen && !AnyOperatorInputThisFrame();
            _idleTerminal = Accumulate(_idleTerminal, terminalCounting, deltaTime, idleTerminalSeconds, TutorialSignal.IdleAtTerminal);

            // 금고: 앞에 서서 해킹을 시작하지 않음
            bool vaultCounting = calm && nearVault && !GameplayInputBlocker.IsBlocked;
            _idleVault = Accumulate(_idleVault, vaultCounting, deltaTime, idleVaultSeconds, TutorialSignal.IdleAtVault);
        }

        /// <summary>조건이 이어지는 동안 시간을 쌓고, 기준을 넘으면 신호를 한 번 내고 다시 센다.</summary>
        private float Accumulate(float elapsed, bool counting, float deltaTime, float threshold, TutorialSignal signal)
        {
            if (!counting)
            {
                return 0f;
            }

            elapsed += deltaTime;

            if (elapsed < threshold)
            {
                return elapsed;
            }

            Emit(signal);
            return 0f;
        }

        private void ResetIdleTimers()
        {
            _idleMove = 0f;
            _idleTerminal = 0f;
            _idleVault = 0f;
        }

        private static bool AnyOperatorInputThisFrame()
        {
            Keyboard keyboard = Keyboard.current;

            if (keyboard != null && keyboard.anyKey.wasPressedThisFrame)
            {
                return true;
            }

            Mouse mouse = Mouse.current;

            return mouse != null &&
                   (mouse.leftButton.wasPressedThisFrame ||
                    mouse.rightButton.wasPressedThisFrame ||
                    mouse.scroll.ReadValue().sqrMagnitude > 0f);
        }

        private static bool IsNearClosedVault(Vector3 position)
        {
            var vaults = TutorialVault.All;

            for (int i = 0; i < vaults.Count; i++)
            {
                TutorialVault vault = vaults[i];

                if (vault == null || vault.IsOpened)
                {
                    continue;
                }

                Vector3 delta = position - vault.transform.position;
                delta.y = 0f;

                if (delta.sqrMagnitude <= vault.InteractRadius * vault.InteractRadius)
                {
                    return true;
                }
            }

            return false;
        }

        // ───────────── 안내 상자 위치 ─────────────

        /// <summary>이 위치 신호의 목적지를 씬에서 찾을 수 있는가(안내 상자 대상 선정용).</summary>
        public bool CanResolveGuide(TutorialSignal signal)
        {
            Bounds unused;
            return TryGetGuideBounds(signal, out unused);
        }

        /// <summary>
        /// 위치 신호의 목적지 범위. 배치된 <see cref="TutorialZone"/>이 우선이고, 없으면 기존 오브젝트에서 찾는다.
        /// 어느 것을 가리키는지 모호하면(잠긴 격벽이 여럿 등) 찾지 못한 것으로 친다 — 틀린 곳을 가리키지 않기 위해서다.
        /// </summary>
        public bool TryGetGuideBounds(TutorialSignal signal, out Bounds bounds)
        {
            TutorialSignal zoneSignal = signal == TutorialSignal.SafeInMainbase ? TutorialSignal.PlayerInMainbase : signal;

            if (TryGetZoneBounds(zoneSignal, out bounds))
            {
                return true;
            }

            switch (signal)
            {
                case TutorialSignal.NearTerminal:
                    for (int i = 0; i < _terminals.Length; i++)
                    {
                        if (_terminals[i] != null)
                        {
                            bounds = _terminalBounds[i];
                            bounds.Expand(new Vector3(2.4f, 0f, 2.4f));
                            return true;
                        }
                    }

                    return false;

                case TutorialSignal.PlayerInMainbase:
                case TutorialSignal.SafeInMainbase:
                    for (int i = 0; i < _baseBoxes.Length; i++)
                    {
                        if (_baseBoxes[i] != null && _baseBoxes[i].enabled)
                        {
                            bounds = _baseBoxes[i].bounds;
                            return true;
                        }
                    }

                    return false;

                case TutorialSignal.BlueDoorAhead:
                    return TryGetSingleWallBounds(true, out bounds);

                case TutorialSignal.NearSecurityNode:
                    var nodes = TutorialSecurityNode.All;

                    for (int i = 0; i < nodes.Count; i++)
                    {
                        if (nodes[i] != null && !nodes[i].IsConnected)
                        {
                            bounds = RadiusBounds(nodes[i].transform.position, nodes[i].InteractRadius);
                            return true;
                        }
                    }

                    return false;

                case TutorialSignal.VaultFront:
                    var vaults = TutorialVault.All;

                    for (int i = 0; i < vaults.Count; i++)
                    {
                        if (vaults[i] != null && !vaults[i].IsOpened)
                        {
                            bounds = RadiusBounds(vaults[i].transform.position, vaults[i].InteractRadius);
                            return true;
                        }
                    }

                    return false;

                default:
                    // 상자 앞·복도 A 끝·파밍 방·금고 복도는 TutorialZone 이 있어야 한다.
                    return false;
            }
        }

        private static bool TryGetZoneBounds(TutorialSignal signal, out Bounds bounds)
        {
            bounds = default;
            bool found = false;
            var zones = TutorialZone.All;

            for (int i = 0; i < zones.Count; i++)
            {
                TutorialZone zone = zones[i];

                Bounds zoneBounds;

                if (zone == null || zone.Signal != signal || !zone.TryGetBounds(out zoneBounds))
                {
                    continue;
                }

                if (!found)
                {
                    bounds = zoneBounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(zoneBounds);
                }
            }

            return found;
        }

        /// <summary>조건에 맞는 격벽이 정확히 하나일 때만 그 경계를 준다.</summary>
        private bool TryGetSingleWallBounds(bool unlocked, out Bounds bounds)
        {
            bounds = default;
            int matches = 0;

            for (int i = 0; i < _walls.Length; i++)
            {
                ControllableWall wall = _walls[i];

                if (wall == null || wall.Object == null || !wall.Object.IsValid || (bool)wall.IsUnlocked != unlocked)
                {
                    continue;
                }

                matches++;
                bounds = _wallBounds[i];
                bounds.Expand(new Vector3(1.5f, 0f, 1.5f));
            }

            return matches == 1;
        }

        private static Bounds RadiusBounds(Vector3 center, float radius)
        {
            return new Bounds(center + Vector3.up, new Vector3(radius * 2f, 2f, radius * 2f));
        }

        // ───────────── 영역 ─────────────

        private void PollZones(Vector3 position)
        {
            var zones = TutorialZone.All;

            for (int i = 0; i < zones.Count; i++)
            {
                TutorialZone zone = zones[i];

                if (zone != null && zone.Contains(position))
                {
                    Emit(zone.Signal);
                }
            }
        }

        // ───────────── 적 ─────────────

        private void PollEnemies()
        {
            bool chasing = false;

            for (int i = 0; i < _enemies.Length; i++)
            {
                EnemyAgent enemy = _enemies[i];

                if (enemy == null || !enemy.isActiveAndEnabled)
                {
                    continue;
                }

                // 네트워크 적은 복제된 상태를 읽어야 클라이언트에서도 맞다.
                NetworkEnemyAgent networked = _networkEnemies[i];
                EnemyState state;

                if (networked != null)
                {
                    if (networked.Object == null || !networked.Object.IsValid)
                    {
                        continue;
                    }

                    state = networked.State;
                }
                else
                {
                    state = enemy.State;
                }

                chasing |= state == EnemyState.Chase;

                // 추격 종료: Chase → Investigate. 본부 안에서 끊긴 추격은 3-6 대사가 맡으므로 제외한다.
                EnemyState previous;

                if (_enemyStates.TryGetValue(enemy, out previous) &&
                    previous == EnemyState.Chase &&
                    state == EnemyState.Investigate &&
                    !IsPlayerInMainbase)
                {
                    Emit(TutorialSignal.ChaseEnded);
                }

                _enemyStates[enemy] = state;
            }

            IsAnyEnemyChasing = chasing;

            if (chasing)
            {
                Emit(TutorialSignal.EnemyChasing);
            }
        }

        // ───────────── 본부 ─────────────

        private void PollMainbase(Vector3 position)
        {
            bool inside;

            if (TutorialZone.HasZoneFor(TutorialSignal.PlayerInMainbase))
            {
                inside = IsInsideZoneFor(TutorialSignal.PlayerInMainbase, position);
            }
            else
            {
                inside = false;

                for (int i = 0; i < _baseBoxes.Length && !inside; i++)
                {
                    inside = IsInsideBox(_baseBoxes[i], position);
                }
            }

            IsPlayerInMainbase = inside;

            if (!inside)
            {
                return;
            }

            Emit(TutorialSignal.PlayerInMainbase);

            if (!IsAnyEnemyChasing)
            {
                Emit(TutorialSignal.SafeInMainbase);
            }
        }

        // ───────────── 4단계 단말기 ─────────────

        private void PollTerminal(Vector3 position)
        {
            if (!TutorialZone.HasZoneFor(TutorialSignal.NearTerminal))
            {
                for (int i = 0; i < _terminals.Length; i++)
                {
                    if (_terminals[i] != null && _terminalBounds[i].SqrDistance(position) <= terminalRadius * terminalRadius)
                    {
                        Emit(TutorialSignal.NearTerminal);
                        break;
                    }
                }
            }

            if (_terminalOpen)
            {
                Emit(TutorialSignal.TerminalOpened);
            }

            if (_cctvChecked)
            {
                Emit(TutorialSignal.CctvChecked);
            }
        }

        private void TrackCctv(float deltaTime)
        {
            if (!_terminalOpen || !_cctvTabVisible || _cctvChecked)
            {
                return;
            }

            _cctvSeconds += deltaTime;

            if (_cctvSeconds >= cctvCheckSeconds)
            {
                _cctvChecked = true;
            }
        }

        private void HandleTerminalOpened()
        {
            _terminalOpen = true;
            Emit(TutorialSignal.TerminalOpened);
        }

        private void HandleTerminalClosed()
        {
            _terminalOpen = false;
            _cctvTabVisible = false;
            _cctvSeconds = 0f;
            Emit(TutorialSignal.TerminalClosed);
        }

        private void HandleTerminalTab(MainbaseControlUI.Tab tab)
        {
            _cctvTabVisible = tab == MainbaseControlUI.Tab.Position;
        }

        private void HandleHackingStarted(SecurityLevel level, HackingGameType type)
        {
            _hackIsVault = level == SecurityLevel.VaultFinal || TutorialVault.IsVaultHackPending;
            _hackActive = true;
            Emit(_hackIsVault ? TutorialSignal.VaultHackStarted : TutorialSignal.DoorHackStarted);
        }

        private void HandleHackingFinished(SecurityLevel level, bool success)
        {
            _hackActive = false;

            if (_hackIsVault)
            {
                Emit(success ? TutorialSignal.VaultHackSucceeded : TutorialSignal.VaultHackFailed);
            }
            else
            {
                Emit(success ? TutorialSignal.DoorHackSucceeded : TutorialSignal.DoorHackFailed);
            }
        }

        // ───────────── 격벽 ─────────────

        private void PollDoors(Vector3 position)
        {
            bool zoneForLocked = TutorialZone.HasZoneFor(TutorialSignal.CorridorAEndReached);
            bool zoneForBlue = TutorialZone.HasZoneFor(TutorialSignal.BlueDoorAhead);
            float radiusSq = doorRadius * doorRadius;

            for (int i = 0; i < _walls.Length; i++)
            {
                ControllableWall wall = _walls[i];

                if (wall == null || wall.Object == null || !wall.Object.IsValid)
                {
                    continue;
                }

                bool open = wall.IsOpen;

                if (open && !_wallWasOpen[i])
                {
                    Emit(TutorialSignal.DoorOpened);
                }

                _wallWasOpen[i] = open;

                if (_wallBounds[i].SqrDistance(position) > radiusSq)
                {
                    continue;
                }

                if (!wall.IsUnlocked && !zoneForLocked)
                {
                    Emit(TutorialSignal.CorridorAEndReached);
                }
                else if (wall.IsUnlocked && !zoneForBlue)
                {
                    Emit(TutorialSignal.BlueDoorAhead);
                }
            }
        }

        // ───────────── 가방 ─────────────

        private void PollInventory()
        {
            if (_inventory == null || _inventory.Object == null || !_inventory.Object.IsValid)
            {
                return;
            }

            int filled = 0;
            int loot = 0;
            bool catnip = false;

            for (int i = 0; i < InventoryManager.MaxSlotCount; i++)
            {
                int itemId = _inventory.Slots[i];

                if (itemId == InventoryManager.EmptySlot)
                {
                    continue;
                }

                filled++;

                if (itemId == catnipItemId)
                {
                    catnip = true;
                }
                else
                {
                    loot++;
                }
            }

            if (_lastInventoryCount >= 0 && filled < _lastInventoryCount)
            {
                Emit(TutorialSignal.ItemDropped);
            }

            _lastInventoryCount = filled;

            if (loot > 0)
            {
                Emit(TutorialSignal.ItemLooted);
            }

            if (filled >= 3)
            {
                Emit(TutorialSignal.InventoryThreeFilled);
            }

            if (catnip)
            {
                Emit(TutorialSignal.CatnipAcquired);
            }
        }

        private void HandleInventoryFull(InventoryManager inventory)
        {
            if (inventory != null && inventory == _inventory)
            {
                Emit(TutorialSignal.InventoryFullRejected);
            }
        }

        private void PollLoot(Vector3 position)
        {
            int count = Physics.OverlapSphereNonAlloc(position, lootRadius, _overlap, ~0, QueryTriggerInteraction.Collide);

            for (int i = 0; i < count; i++)
            {
                Collider hit = _overlap[i];

                if (hit == null)
                {
                    continue;
                }

                NetworkItem item = hit.GetComponentInParent<NetworkItem>();

                if (item != null && item.Object != null && item.Object.IsValid && !item.IsPickedUp)
                {
                    Emit(TutorialSignal.NearLoot);
                    return;
                }
            }
        }

        // ───────────── 캐시 ─────────────

        private bool IsPlayerValid()
        {
            if (_player == null || _playerTransform == null)
            {
                return false;
            }

            if (_player.Object == null || !_player.Object.IsValid)
            {
                _player = null;
                _playerTransform = null;
                _inventory = null;
                _isDead = false; // 재시작 대기(_awaitingRespawn)는 유지 — 새로 스폰된 플레이어에서 "재시도"를 알린다
                return false;
            }

            return true;
        }

        private void FindLocalPlayer()
        {
            PlayerData[] players = FindObjectsByType<PlayerData>(FindObjectsSortMode.None);

            for (int i = 0; i < players.Length; i++)
            {
                PlayerData candidate = players[i];

                if (candidate.Object == null || !candidate.Object.IsValid || !candidate.Object.HasInputAuthority)
                {
                    continue;
                }

                _player = candidate;
                _playerTransform = candidate.transform;
                _inventory = candidate.GetComponent<InventoryManager>();
                _lastPosition = _playerTransform.position;
                _lastYaw = _playerTransform.eulerAngles.y;
                _lookReference = Flat(_playerTransform.forward);
                _lastInventoryCount = -1;
                _nextSceneCache = 0f;
                return;
            }
        }

        /// <summary>
        /// 씬 오브젝트 목록을 다시 모은다. 몇 초에 한 번만 돈다(Find 계열은 배열을 할당한다).
        /// 격벽·단말기는 움직이지 않으므로 경계 상자를 여기서 한 번 계산해 둔다.
        /// </summary>
        private void RefreshSceneCache()
        {
            _terminals = FindObjectsByType<ControlMachine>(FindObjectsSortMode.None);
            _terminalBounds = new Bounds[_terminals.Length];

            for (int i = 0; i < _terminals.Length; i++)
            {
                _terminalBounds[i] = ComputeBounds(_terminals[i].gameObject);
            }

            ControllableWall[] walls = FindObjectsByType<ControllableWall>(FindObjectsSortMode.None);

            if (!SameSet(walls, _walls))
            {
                _walls = walls;
                _wallBounds = new Bounds[walls.Length];
                _wallWasOpen = new bool[walls.Length];

                for (int i = 0; i < walls.Length; i++)
                {
                    _wallBounds[i] = ComputeBounds(walls[i].gameObject);
                    _wallWasOpen[i] = walls[i].Object != null && walls[i].Object.IsValid && walls[i].IsOpen;
                }
            }

            _bases = FindObjectsByType<MainbaseZone>(FindObjectsSortMode.None);
            _baseBoxes = new BoxCollider[_bases.Length];

            for (int i = 0; i < _bases.Length; i++)
            {
                _baseBoxes[i] = _bases[i].GetComponent<BoxCollider>();
            }

            _enemies = FindObjectsByType<EnemyAgent>(FindObjectsSortMode.None);
            _networkEnemies = new NetworkEnemyAgent[_enemies.Length];

            for (int i = 0; i < _enemies.Length; i++)
            {
                _networkEnemies[i] = _enemies[i].GetComponent<NetworkEnemyAgent>();
            }
        }

        private static bool SameSet(ControllableWall[] a, ControllableWall[] b)
        {
            if (a.Length != b.Length)
            {
                return false;
            }

            for (int i = 0; i < a.Length; i++)
            {
                if (Array.IndexOf(b, a[i]) < 0)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>콜라이더(꺼져 있으면 렌더러) 경계를 합친다. 열린 격벽은 콜라이더가 꺼져 있다.</summary>
        private static Bounds ComputeBounds(GameObject target)
        {
            bool hasBounds = false;
            Bounds bounds = new Bounds(target.transform.position, Vector3.one);

            Collider[] colliders = target.GetComponentsInChildren<Collider>(true);

            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i].isTrigger || !colliders[i].enabled)
                {
                    continue;
                }

                Encapsulate(ref bounds, ref hasBounds, colliders[i].bounds);
            }

            if (!hasBounds)
            {
                Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);

                for (int i = 0; i < renderers.Length; i++)
                {
                    Encapsulate(ref bounds, ref hasBounds, renderers[i].bounds);
                }
            }

            return bounds;
        }

        private static void Encapsulate(ref Bounds bounds, ref bool hasBounds, Bounds next)
        {
            if (next.size == Vector3.zero)
            {
                return;
            }

            if (!hasBounds)
            {
                bounds = next;
                hasBounds = true;
                return;
            }

            bounds.Encapsulate(next);
        }

        private static bool IsInsideBox(BoxCollider box, Vector3 worldPoint)
        {
            if (box == null)
            {
                return false;
            }

            Vector3 local = box.transform.InverseTransformPoint(worldPoint) - box.center;
            Vector3 half = box.size * 0.5f;
            return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z;
        }

        private static bool IsInsideZoneFor(TutorialSignal signal, Vector3 position)
        {
            var zones = TutorialZone.All;

            for (int i = 0; i < zones.Count; i++)
            {
                if (zones[i] != null && zones[i].Signal == signal && zones[i].Contains(position))
                {
                    return true;
                }
            }

            return false;
        }

        private static Vector3 Flat(Vector3 direction)
        {
            direction.y = 0f;
            return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
        }

        private void Emit(TutorialSignal signal)
        {
            SignalRaised?.Invoke(signal);
        }
    }
}
