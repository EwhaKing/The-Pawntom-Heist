using System.Collections;
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
/// 사용 위치:
/// - 침투자 / 적 NPC / 테스트용 플레이어 오브젝트에 붙임
/// </summary>
public class SprinklerSlowTarget : MonoBehaviour
{
    [Header("Optional Movement")]
    [SerializeField] private NavMeshAgent navMeshAgent;

    private float originalAgentSpeed;
    private Coroutine slowRoutine;

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

    /// <summary>
    /// 스프링클러 감속 효과 적용
    /// </summary>
    public void ApplySlow(float speedMultiplier, float duration)
    {
        if (slowRoutine != null)
        {
            StopCoroutine(slowRoutine);
        }

        slowRoutine = StartCoroutine(SlowRoutine(speedMultiplier, duration));
    }

    private IEnumerator SlowRoutine(float speedMultiplier, float duration)
    {
        Debug.Log($"[SprinklerSlowTarget] 감속 적용: {gameObject.name}, Duration={duration}");

        if (navMeshAgent != null)
        {
            navMeshAgent.speed = originalAgentSpeed * speedMultiplier;
        }

        yield return new WaitForSeconds(duration);

        if (navMeshAgent != null)
        {
            navMeshAgent.speed = originalAgentSpeed;
        }

        slowRoutine = null;

        Debug.Log($"[SprinklerSlowTarget] 감속 해제: {gameObject.name}");
    }
}