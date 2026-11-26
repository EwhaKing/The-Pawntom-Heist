using System.Collections.Generic;
using Fusion;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// SprinklerSlowTarget
///
/// 담당:
/// - 스프링클러 감속 효과를 받을 수 있는 대상 표시
/// - NavMeshAgent가 있으면 속도를 직접 낮춤
/// - NavMeshAgent가 없으면 일단 로그만 출력
///
/// 네트워크:
/// - 감속 판정은 스프링클러의 상태 권한(호스트)에서만 내려진다(<see cref="SprinklerZone"/>).
///   적 NPC 의 NavMeshAgent 는 호스트에서만 움직이므로 호스트에서 속도를 바꾸면 된다.
/// - 지속 시간은 네트워크 틱(TickTimer)으로 잰다 — Time.deltaTime·WaitForSeconds 를 쓰지 않는다.
/// - 플레이어 이동 감속은 아직 없다(플레이어 이동 속도는 PlayerController 의 네트워크 상태 — 후속 작업).
///
/// 사용 위치:
/// - 침투자 / 적 NPC / 테스트용 플레이어 오브젝트에 붙임
/// </summary>
public class SprinklerSlowTarget : MonoBehaviour
{
    private static readonly List<SprinklerSlowTarget> ActiveTargets = new List<SprinklerSlowTarget>(16);

    /// <summary>켜져 있는 감속 대상 전체. 스프링클러가 물리 트리거 대신 이 목록을 위치로 검사한다.</summary>
    public static IReadOnlyList<SprinklerSlowTarget> All => ActiveTargets;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        ActiveTargets.Clear();
    }

    [Header("Optional Movement")]
    [SerializeField] private NavMeshAgent navMeshAgent;

    private float originalAgentSpeed;
    private NetworkRunner _runner;
    private TickTimer _slowTimer;
    private bool _isSlowed;

    public bool IsSlowed => _isSlowed;

    private void Awake()
    {
        if (navMeshAgent == null)
        {
            navMeshAgent = GetComponentInChildren<NavMeshAgent>();
        }

        if (navMeshAgent != null)
        {
            originalAgentSpeed = navMeshAgent.speed;
        }
    }

    private void OnEnable()
    {
        if (!ActiveTargets.Contains(this))
        {
            ActiveTargets.Add(this);
        }
    }

    private void OnDisable()
    {
        ActiveTargets.Remove(this);
        EndSlow();
    }

    /// <summary>
    /// 스프링클러 감속 효과 적용(상태 권한 쪽에서만 호출). 이미 감속 중이면 남은 시간을 duration 으로 갱신한다.
    /// </summary>
    public void ApplySlow(NetworkRunner runner, float speedMultiplier, float duration)
    {
        if (runner == null)
        {
            return;
        }

        _runner = runner;
        _slowTimer = TickTimer.CreateFromSeconds(runner, Mathf.Max(0f, duration));

        if (_isSlowed)
        {
            return;
        }

        _isSlowed = true;

        if (navMeshAgent != null)
        {
            navMeshAgent.speed = originalAgentSpeed * speedMultiplier;
        }

        Debug.Log($"[SprinklerSlowTarget] 감속 적용: {gameObject.name}, Duration={duration}");
    }

    private void Update()
    {
        if (!_isSlowed)
        {
            return;
        }

        if (_runner == null || !_runner.IsRunning || _slowTimer.ExpiredOrNotRunning(_runner))
        {
            EndSlow();
        }
    }

    private void EndSlow()
    {
        if (!_isSlowed)
        {
            return;
        }

        _isSlowed = false;
        _slowTimer = TickTimer.None;
        _runner = null;

        if (navMeshAgent != null)
        {
            navMeshAgent.speed = originalAgentSpeed;
        }

        Debug.Log($"[SprinklerSlowTarget] 감속 해제: {gameObject.name}");
    }
}
