using Fusion;
using Pawntom.Enemy.Authoring;
using UnityEngine;

public enum ParkourMode : byte
{
    None = 0,
    WallCling = 1, // 벽면 접촉 체공(발톱으로 빈 벽에 붙음)
    BarHang = 2,   // 철봉에 매달림
    Mantle = 3,    // 벽 끝(턱)을 넘어 위로 올라섬
    Launch = 4,    // 벽 차기·철봉 도약 직후. 잠깐 공중 조작을 막아 도약 속도를 지킨다
}

/// <summary>
/// 고양이 파쿠르. 기획서 1.3 의 Parkour 상태("벽면 접촉 체공")를 구현한다 — <b>전용 키가 없다</b>.
/// 고양이는 발톱이 있어서 지지대 없는 빈 벽도 탄다.
/// <list type="bullet">
/// <item>공중에서 벽 쪽으로 움직이다 닿으면 자동으로 벽에 붙는다. 붙을 때 누르던 이동 키는 한 번 떼야 하고,
///       그 뒤 WASD 로 오르내리고 옆으로 이동, 꼭대기에 닿으면 턱 위로 올라선다.</item>
/// <item>벽에서 Space: 벽을 보고 있으면 위로 짧게 도약(W 를 누르고 있으면 다시 붙는다) — 손 닿는 높이에 턱이 있으면 바로 올라선다.
///       옆이나 반대쪽을 보고 있으면 시선 방향으로 벽을 차고 떨어진다.</item>
/// <item>공중에서 손 근처에 <see cref="ParkourBar"/>(철봉)가 있으면 자동으로 매달린다. 철봉 바로 아래에 가만히 매달리고
///       (대롱대롱 흔들림은 애니메이션 몫 — Animator 의 ParkourMode = 2), A/D 옆 이동, Space 로 시선 방향 도약.</item>
/// </list>
/// <para>
/// 틱은 <see cref="PlayerController"/> 가 돌린다(<see cref="Simulate"/>). 상태는 전부 [Networked] 라 예측·재시뮬레이션에서 같은 결과가 나온다.
/// 붙어 있는 동안 <see cref="PlayerData.IsParkouring"/> 을 켜서 기존 스태미나 소모(ParkourStaminaCost, 초당 -10)를 그대로 쓴다. 벽·철봉 모두 같다.
/// </para>
/// </summary>
[RequireComponent(typeof(NetworkCharacterController))]
[RequireComponent(typeof(PlayerData))]
public class PlayerParkour : NetworkBehaviour
{
    [Header("Wall")]
    [Tooltip("탈 수 있는 벽 레이어")]
    [SerializeField] private LayerMask climbableLayers = ~0;

    [Tooltip("몸 반지름 바깥으로 이 거리(m) 안에 벽이 있으면 잡는다")]
    [SerializeField, Min(0f)] private float wallProbeDistance = 0.2f;

    [Tooltip("벽 법선의 y 가 이 값 이하일 때만 벽으로 본다(바닥·천장 제외)")]
    [SerializeField, Range(0f, 1f)] private float maxWallNormalY = 0.35f;

    [Tooltip("벽을 정면에서 이 각도(도) 안으로 보고 있어야 붙는다")]
    [SerializeField, Range(0f, 90f)] private float maxGrabAngle = 55f;

    [Tooltip("매달렸을 때 몸과 벽 사이 간격(m)")]
    [SerializeField, Min(0f)] private float wallGap = 0.02f;

    [Tooltip("이동 입력이 벽 쪽으로 이만큼(0~1) 향하고 있어야 붙는다. 벽 옆을 스쳐 뛸 때 멋대로 붙지 않게 한다")]
    [SerializeField, Range(0f, 1f)] private float grabInputThreshold = 0.3f;

    [SerializeField, Min(0f)] private float climbSpeed = 0.9f;
    [SerializeField, Min(0f)] private float wallStrafeSpeed = 0.9f;

    [Tooltip("놓거나 도약한 뒤 이 시간(초) 동안은 다시 잡지 않는다")]
    [SerializeField, Min(0f)] private float regrabCooldown = 0.2f;

    [Header("Wall Jump")]
    [Tooltip("벽 차기 높이(m)")]
    [SerializeField, Min(0f)] private float wallJumpHeight = 0.6f;

    [Tooltip("벽 차기 수평 속도(m/s)")]
    [SerializeField, Min(0f)] private float wallJumpSpeed = 2.5f;

    [Tooltip("벽을 보고 Space 를 눌렀을 때 위로 도약하는 높이(m)")]
    [SerializeField, Min(0f)] private float climbHopHeight = 0.45f;

    [Tooltip("도약 직후 이 시간(초) 동안 공중 조작을 막는다(조작이 도약 속도를 바로 덮어쓰지 않게)")]
    [SerializeField, Min(0f)] private float launchLockSeconds = 0.3f;

    [Tooltip("도약 중 낙하 중력 배율. NetworkCharacterController.fallMultiplier 와 맞춘다")]
    [SerializeField, Min(1f)] private float launchFallMultiplier = 2.5f;

    [Header("Ledge")]
    [Tooltip("머리 위 이 높이(m)까지 올라설 턱을 찾는다")]
    [SerializeField, Min(0f)] private float ledgeSearchHeight = 0.6f;

    [Tooltip("올라서는 데 걸리는 시간(초)")]
    [SerializeField, Min(0.05f)] private float mantleSeconds = 0.35f;

    [Tooltip("턱 윗면 법선 y 가 이 값 이상이어야 올라선다")]
    [SerializeField, Range(0f, 1f)] private float minFloorNormalY = 0.7f;

    [Header("Bar")]
    [Tooltip("손(머리 위)과 철봉 사이 거리가 이 값(m) 이하면 잡는다")]
    [SerializeField, Min(0f)] private float barGrabRadius = 0.5f;

    // 너무 작으면 머리가 철봉 콜라이더에 바로 붙어 Space 도약이 철봉에 막혀 그냥 떨어진다.
    [Tooltip("팔을 뻗어 매달린 길이(m): 머리 꼭대기에서 철봉까지. 매달린 몸이 철봉에서 떨어진 거리 = 몸 절반 높이 + 이 값")]
    [SerializeField, Min(0f)] private float barHandReach = 0.35f;

    [SerializeField, Min(0f)] private float barShimmySpeed = 0.7f;

    [Tooltip("잡은 뒤 철봉 바로 아래로 몸이 끌려가는 속도(m/s)")]
    [SerializeField, Min(0f)] private float barSettleSpeed = 4f;

    [Tooltip("철봉에서 Space 도약 높이(m)")]
    [SerializeField, Min(0f)] private float barJumpHeight = 0.3f;

    [Tooltip("철봉에서 Space 도약 시 시선 방향 수평 속도(m/s)")]
    [SerializeField, Min(0f)] private float barJumpForwardSpeed = 2.5f;

    [Networked] public ParkourMode Mode { get; private set; }
    [Networked] private Vector3 WallNormal { get; set; }
    [Networked] private NetworkBool MoveLatched { get; set; } // true = 붙을 때 누르고 있던 이동 입력을 아직 떼지 않았다
    [Networked] private Vector3 BarCenter { get; set; }
    [Networked] private Vector3 BarAxis { get; set; }
    [Networked] private float BarHalfLength { get; set; }
    [Networked] private float BarOffset { get; set; }
    [Networked] private NetworkBool SkipLastBar { get; set; } // true = 방금 도약한 철봉(BarCenter)은 착지 전까지 다시 잡지 않는다
    [Networked] private Vector3 MantleFrom { get; set; }
    [Networked] private Vector3 MantleTo { get; set; }
    [Networked] private float MantleElapsed { get; set; }
    [Networked] private TickTimer LaunchTimer { get; set; }
    [Networked] private TickTimer RegrabTimer { get; set; }

    private const float MantleRiseFraction = 0.6f;
    private const float MoveReleaseThreshold = 0.1f;
    private const float FacingWallDot = -0.5f; // 시선과 벽 법선의 내적이 이보다 작으면 "벽을 보고 있다"

    private NetworkCharacterController _ncc;
    private CharacterController _cc;
    private PlayerData _data;

    private readonly RaycastHit[] _hits = new RaycastHit[16];
    private readonly Collider[] _overlaps = new Collider[16];

    private void Awake()
    {
        _ncc = GetComponent<NetworkCharacterController>();
        _cc = GetComponent<CharacterController>();
        _data = GetComponent<PlayerData>();
    }

    /// <summary>
    /// 한 틱 진행. true 면 이번 틱 이동을 파쿠르가 처리했으니 PlayerController 의 일반 이동·점프를 건너뛴다.
    /// </summary>
    public bool Simulate(NetworkInputData input, NetworkButtons pressed)
    {
        bool jumpPressed = pressed.IsSet(InputButton.Jump);
        Vector2 move = Vector2.ClampMagnitude(input.Move, 1f);

        if (Mode == ParkourMode.None || Mode == ParkourMode.Launch)
        {
            if (!TryGrab(move))
            {
                if (Mode == ParkourMode.Launch)
                {
                    TickLaunch();
                    return true;
                }

                return false;
            }

            jumpPressed = false; // 붙은 틱에 바로 튀어 나가지 않게
        }

        switch (Mode)
        {
            case ParkourMode.WallCling: TickWall(jumpPressed, move); break;
            case ParkourMode.BarHang: TickBar(jumpPressed, move); break;
            case ParkourMode.Mantle: TickMantle(); break;
        }

        return true;
    }

    /// <summary>사망·튜토리얼 리셋 등에서 파쿠르 상태를 즉시 끈다.</summary>
    public void ResetState()
    {
        if (Mode == ParkourMode.None)
        {
            return;
        }

        Mode = ParkourMode.None;
        LaunchTimer = TickTimer.None;
        _data.StopParkour();
    }

    // ───────────────────────── 잡기 ─────────────────────────

    private bool TryGrab(Vector2 move)
    {
        // 전용 키 없음: 공중에서 벽(또는 철봉)에 닿으면 붙는다.
        if (_ncc.Grounded)
        {
            SkipLastBar = false;
            return false;
        }

        if (!RegrabTimer.ExpiredOrNotRunning(Runner) || !CanHold())
        {
            return false;
        }

        return TryGrabBarAtHands() || TryGrabWall(move);
    }

    private bool CanHold()
    {
        return !_data.IsDead && !_data.IsExhausted && _data.CurrentStamina > 0f;
    }

    private bool TryGrabWall(Vector2 move)
    {
        Vector3 forward = Flat(transform.forward);

        if (!ProbeWall(BodyCenter, forward, BodyRadius + wallProbeDistance, out RaycastHit hit))
        {
            return false;
        }

        Vector3 intoWall = -Flat(hit.normal);
        Vector3 moveDirection = transform.forward * move.y + transform.right * move.x;

        // 벽을 마주 보고, 벽 쪽으로 가려는 중일 때만 붙는다.
        if (Vector3.Angle(forward, intoWall) > maxGrabAngle ||
            Vector3.Dot(moveDirection, intoWall) < grabInputThreshold)
        {
            return false;
        }

        WallNormal = -intoWall;
        // 붙으려고 누른 W 가 그대로 "위로 오르기" 가 되지 않게, 한 번 뗐다가 다시 눌러야 벽에서 움직인다.
        MoveLatched = move.sqrMagnitude > MoveReleaseThreshold * MoveReleaseThreshold;
        Enter(ParkourMode.WallCling);
        _ncc.Velocity = Vector3.zero;
        return true;
    }

    private bool TryGrabBarAtHands()
    {
        Vector3 hands = BodyCenter + Vector3.up * HangLength;
        return TryFindBar(hands, out ParkourBar bar, out Vector3 point) && EnterBar(bar, point);
    }

    private bool TryFindBar(Vector3 from, out ParkourBar best, out Vector3 bestPoint)
    {
        best = null;
        bestPoint = default;
        float bestSqr = barGrabRadius * barGrabRadius;

        foreach (ParkourBar bar in ParkourBar.Active)
        {
            if (SkipLastBar && (bar.Center - BarCenter).sqrMagnitude < 1e-4f)
            {
                continue; // 방금 도약한 철봉에 도로 잡히지 않게
            }

            Vector3 point = bar.ClosestPoint(from);
            float sqr = (point - from).sqrMagnitude;

            if (sqr <= bestSqr)
            {
                best = bar;
                bestPoint = point;
                bestSqr = sqr;
            }
        }

        return best != null;
    }

    private bool EnterBar(ParkourBar bar, Vector3 point)
    {
        BarCenter = bar.Center;
        BarAxis = bar.Axis;
        BarHalfLength = bar.HalfLength;
        BarOffset = Vector3.Dot(point - bar.Center, bar.Axis);
        SkipLastBar = false;
        Enter(ParkourMode.BarHang);
        return true;
    }

    private void Enter(ParkourMode mode)
    {
        Mode = mode;
        LaunchTimer = TickTimer.None;
        _ncc.Grounded = false;

        if (Object.HasStateAuthority)
        {
            _data.TryStartParkour();
        }
    }

    // ───────────────────────── 벽 ─────────────────────────

    private void TickWall(bool jumpPressed, Vector2 move)
    {
        if (!CanHold())
        {
            Drop();
            return;
        }

        if (MoveLatched)
        {
            if (move.sqrMagnitude < MoveReleaseThreshold * MoveReleaseThreshold)
            {
                MoveLatched = false;
            }

            move = Vector2.zero;
        }

        Vector3 before = transform.position;

        // 벽을 보고 Space: 손 닿는 높이에 턱이 있으면 위로 도약하는 대신 바로 올라선다.
        if (jumpPressed && Vector3.Dot(Flat(transform.forward), WallNormal) < FacingWallDot && TryStartMantle(WallNormal))
        {
            return;
        }

        if (jumpPressed && TryPayJump())
        {
            WallJump();
            return;
        }

        Vector3 normal = WallNormal;
        Vector3 right = Vector3.Cross(normal, Vector3.up).normalized; // 벽을 마주 봤을 때 오른쪽
        float side = Vector3.Dot(transform.right, right) >= 0f ? 1f : -1f; // 벽을 등지고 있어도 A/D 가 화면 기준이 되게
        Vector3 desired = Vector3.up * (move.y * climbSpeed) + right * (move.x * side * wallStrafeSpeed);

        // 머리 높이에 벽이 없으면 꼭대기 → 위로 오르는 중이면 턱 넘기.
        if (move.y > 0f && !ProbeWall(HeadPoint, -normal, BodyRadius + wallProbeDistance, out _) && TryStartMantle(normal))
        {
            return;
        }

        _cc.Move(desired * Runner.DeltaTime);

        if (!ProbeWall(BodyCenter, -normal, BodyRadius + wallProbeDistance, out RaycastHit hit))
        {
            // 벽이 끝났다(옆 모서리·꼭대기를 지나침).
            if (!(move.y > 0f && TryStartMantle(normal)))
            {
                Drop();
            }

            return;
        }

        // 벽에 붙여 둔다(곡면·각진 벽도 따라가게 법선 갱신).
        normal = Flat(hit.normal);
        WallNormal = normal;
        _cc.Move(-normal * (hit.distance - (BodyRadius + wallGap)));

        _ncc.Velocity = (transform.position - before) * Runner.TickRate;
        _ncc.Grounded = false;

        // 아래로 내려가다 바닥에 닿으면 놓는다.
        if (_cc.isGrounded && move.y < 0f)
        {
            Drop();
            _ncc.Grounded = true;
        }
    }

    private void WallJump()
    {
        Vector3 normal = WallNormal;
        Vector3 look = Flat(transform.forward);
        Vector3 velocity;

        if (Vector3.Dot(look, normal) < FacingWallDot)
        {
            // 벽을 보고 있다 → 벽을 짚고 위로 도약. W 를 누르고 있으면 다시 붙는다.
            velocity = normal * (wallJumpSpeed * 0.1f) + Vector3.up * JumpSpeed(climbHopHeight);
        }
        else
        {
            // 옆이나 반대쪽을 보고 있다 → 벽을 차고 시선 방향으로. 벽과 나란히 보고 있어도 벽에서 떨어지게 법선 성분을 섞는다.
            velocity = Flat(look + normal * 0.5f) * wallJumpSpeed + Vector3.up * JumpSpeed(wallJumpHeight);
        }

        Launch(velocity, false);
    }

    private bool TryStartMantle(Vector3 normal)
    {
        Vector3 forward = -normal;
        float radius = BodyRadius;
        Vector3 top = BodyCenter + Vector3.up * BodyHalfHeight;
        Vector3 origin = top + Vector3.up * ledgeSearchHeight + forward * (radius * 2f + wallProbeDistance);

        if (!Physics.Raycast(origin, Vector3.down, out RaycastHit floor, ledgeSearchHeight + BodyHalfHeight * 2f,
                climbableLayers, QueryTriggerInteraction.Ignore) ||
            floor.normal.y < minFloorNormalY ||
            !IsValidSurface(floor.collider, floor.rigidbody))
        {
            return false;
        }

        Vector3 pivotFromCenter = transform.position - BodyCenter;
        Vector3 targetCenter = floor.point + Vector3.up * (BodyHalfHeight + 0.01f);

        if (IsCapsuleBlocked(targetCenter, false))
        {
            return false;
        }

        MantleFrom = transform.position;
        MantleTo = targetCenter + pivotFromCenter;
        MantleElapsed = 0f;
        Mode = ParkourMode.Mantle;
        return true;
    }

    private void TickMantle()
    {
        MantleElapsed += Runner.DeltaTime;
        float t = Mathf.Clamp01(MantleElapsed / mantleSeconds);

        // 먼저 몸을 끌어 올리고, 그다음 앞으로 넘어간다.
        float rise = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / MantleRiseFraction));
        float over = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - MantleRiseFraction) / (1f - MantleRiseFraction)));

        Vector3 from = MantleFrom;
        Vector3 to = MantleTo;
        Vector3 target = new Vector3(
            Mathf.Lerp(from.x, to.x, over),
            Mathf.Lerp(from.y, to.y, rise),
            Mathf.Lerp(from.z, to.z, over));

        Vector3 before = transform.position;
        SetPosition(target);
        _ncc.Velocity = (target - before) * Runner.TickRate;

        if (t >= 1f)
        {
            ResetState();
            _ncc.Velocity = Vector3.zero;
            _ncc.Grounded = true;
        }
    }

    // ───────────────────────── 철봉 ─────────────────────────

    private void TickBar(bool jumpPressed, Vector2 move)
    {
        if (!CanHold())
        {
            Drop();
            return;
        }

        if (jumpPressed && TryPayJump())
        {
            SkipLastBar = true;
            // 공중 조작(제동)이 끼어들면 다음 철봉에 못 닿는다. 착지·잡기 전까지 포물선을 유지한다.
            Launch(Vector3.up * JumpSpeed(barJumpHeight) + Flat(transform.forward) * barJumpForwardSpeed, true);
            return;
        }

        Vector3 axis = BarAxis;
        float dt = Runner.DeltaTime;

        // A/D: 철봉을 따라 옆으로.
        float side = Vector3.Dot(transform.right, axis) >= 0f ? 1f : -1f;
        BarOffset = Mathf.Clamp(BarOffset + move.x * side * barShimmySpeed * dt, -BarHalfLength, BarHalfLength);

        // 철봉 바로 아래에 매달린다. 잡은 직후에는 순간이동하지 않게 일정 속도로 끌려간다.
        Vector3 pivotFromCenter = transform.position - BodyCenter;
        Vector3 hangCenter = BarCenter + axis * BarOffset + Vector3.down * HangLength;
        Vector3 targetCenter = Vector3.MoveTowards(BodyCenter, hangCenter, barSettleSpeed * dt);

        if (IsCapsuleBlocked(targetCenter, true))
        {
            // 매달릴 자리가 막혀 있다(철봉 아래가 벽 등).
            Drop();
            return;
        }

        Vector3 before = transform.position;
        Vector3 target = targetCenter + pivotFromCenter;
        SetPosition(target);
        _ncc.Velocity = (target - before) * Runner.TickRate;
        _ncc.Grounded = false;
    }

    // ───────────────────────── 도약 / 놓기 ─────────────────────────

    /// <param name="untilLanding">true 면 착지하거나 다른 것을 잡을 때까지 포물선 그대로 날아간다(철봉 도약).
    /// false 면 launchLockSeconds 뒤 공중 조작이 돌아온다(벽 차기).</param>
    private void Launch(Vector3 velocity, bool untilLanding)
    {
        ResetState();
        Mode = ParkourMode.Launch;
        LaunchTimer = untilLanding ? TickTimer.None : TickTimer.CreateFromSeconds(Runner, launchLockSeconds);
        RegrabTimer = TickTimer.CreateFromSeconds(Runner, regrabCooldown);
        _ncc.Velocity = velocity;
        _ncc.Grounded = false;
    }

    private void Drop()
    {
        ResetState();
        RegrabTimer = TickTimer.CreateFromSeconds(Runner, regrabCooldown);
        _ncc.Velocity = Vector3.zero;
    }

    private void TickLaunch()
    {
        float dt = Runner.DeltaTime;
        Vector3 velocity = _ncc.Velocity;
        float gravity = Mathf.Abs(_ncc.gravity);
        velocity.y -= gravity * (velocity.y > 0f ? 1f : launchFallMultiplier) * dt;

        Vector3 before = transform.position;
        _cc.Move(velocity * dt);
        _ncc.Velocity = (transform.position - before) * Runner.TickRate;
        _ncc.Grounded = _cc.isGrounded;

        if (_ncc.Grounded || LaunchTimer.Expired(Runner))
        {
            Mode = ParkourMode.None;
            LaunchTimer = TickTimer.None;
        }
    }

    /// <summary>점프와 같은 스태미나 규칙. 예측 쪽은 검사만, 상태 권한 쪽이 실제로 깎는다.</summary>
    private bool TryPayJump()
    {
        if (_data.IsExhausted || !_data.HasStamina(_data.JumpStaminaCost))
        {
            return false;
        }

        // 비용이 0 이면(점프 무소모) 깎지 않는다. TryConsumeStamina 는 0 을 실패로 돌려준다.
        return _data.JumpStaminaCost <= 0f || !Object.HasStateAuthority || _data.TryConsumeStamina(_data.JumpStaminaCost);
    }

    private float JumpSpeed(float height)
    {
        return Mathf.Sqrt(2f * Mathf.Abs(_ncc.gravity) * height);
    }

    // ───────────────────────── 판정 도우미 ─────────────────────────

    private float Scale => transform.lossyScale.y;
    private float BodyRadius => _cc.radius * transform.lossyScale.x;
    private float BodyHalfHeight => _cc.height * Scale * 0.5f;
    private Vector3 BodyCenter => transform.TransformPoint(_cc.center);
    private Vector3 HeadPoint => BodyCenter + Vector3.up * (BodyHalfHeight - BodyRadius * 0.5f);
    private float HangLength => BodyHalfHeight + barHandReach;

    private static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward;
    }

    private bool ProbeWall(Vector3 origin, Vector3 direction, float distance, out RaycastHit best)
    {
        best = default;
        int count = Physics.RaycastNonAlloc(origin, direction, _hits, distance, climbableLayers, QueryTriggerInteraction.Ignore);
        float bestDistance = float.MaxValue;
        bool found = false;

        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = _hits[i];

            if (hit.distance >= bestDistance ||
                Mathf.Abs(hit.normal.y) > maxWallNormalY ||
                !IsValidSurface(hit.collider, hit.rigidbody))
            {
                continue;
            }

            best = hit;
            bestDistance = hit.distance;
            found = true;
        }

        return found;
    }

    /// <summary>나·다른 플레이어·적·굴러다니는 물체·철봉은 붙는 벽이 아니다.</summary>
    private bool IsValidSurface(Collider collider, Rigidbody body)
    {
        if (collider.transform.IsChildOf(transform) ||
            collider.GetComponentInParent<PlayerController>() != null ||
            collider.GetComponentInParent<EnemyAgent>() != null ||
            collider.GetComponentInParent<ParkourBar>() != null)
        {
            return false;
        }

        return body == null || body.isKinematic;
    }

    private bool IsCapsuleBlocked(Vector3 center, bool ignoreBars)
    {
        float radius = BodyRadius * 0.9f;
        Vector3 offset = Vector3.up * Mathf.Max(0f, BodyHalfHeight - BodyRadius);
        int count = Physics.OverlapCapsuleNonAlloc(center + offset, center - offset, radius, _overlaps,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            Collider other = _overlaps[i];

            if (other.transform.IsChildOf(transform) ||
                other.GetComponentInParent<PlayerController>() != null ||
                (ignoreBars && other.GetComponentInParent<ParkourBar>() != null))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private void SetPosition(Vector3 position)
    {
        // CharacterController 가 켜져 있으면 transform 직접 대입이 덮어써진다.
        _cc.enabled = false;
        transform.position = position;
        _cc.enabled = true;
    }
}
