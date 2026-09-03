using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SprinklerZone
///
/// 담당:
/// - 실제 맵의 스프링클러 효과 구역 관리
/// - 해킹 성공 시 스프링클러를 일정 시간 작동
/// - 작동 중 구역 안의 대상에게 감속 효과 적용
/// - 구역을 벗어난 대상에게도 일정 시간 감속 유지
/// - 한 번 사용한 스프링클러는 재사용 불가
///
/// 주의:
/// - 구역 발견 여부는 이 스크립트가 관리하지 않음
/// - 발견 여부는 DiscoverableArea가 담당함
///
/// 사용 위치:
/// - Sprinkler_01 > SprinklerTrigger 오브젝트에 붙임
///
/// 필요 컴포넌트:
/// - BoxCollider
/// - Is Trigger ON
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class SprinklerZone : MonoBehaviour
{
    [Header("State")]
    [SerializeField] private bool isUsed;
    [SerializeField] private bool isRunning;

    [Header("Sprinkler Option")]
    [SerializeField] private float activeDuration = 5f;
    [SerializeField] private float slowMultiplier = 0.5f;
    [SerializeField] private float afterExitSlowDuration = 3f;

    private readonly HashSet<SprinklerSlowTarget> targetsInside =
        new HashSet<SprinklerSlowTarget>();

    private Coroutine runningRoutine;

    public bool IsUsed => isUsed;
    public bool IsRunning => isRunning;

    private void Awake()
    {
        BoxCollider boxCollider = GetComponent<BoxCollider>();
        boxCollider.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        SprinklerSlowTarget target = other.GetComponentInParent<SprinklerSlowTarget>();

        if (target == null)
        {
            return;
        }

        targetsInside.Add(target);

        // 스프링클러가 작동 중이면 들어온 대상에게 즉시 감속 적용
        if (isRunning)
        {
            target.ApplySlow(slowMultiplier, afterExitSlowDuration);
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (!isRunning)
        {
            return;
        }

        SprinklerSlowTarget target = other.GetComponentInParent<SprinklerSlowTarget>();

        if (target == null)
        {
            return;
        }

        // 구역 안에 있는 동안 감속 시간을 계속 갱신
        target.ApplySlow(slowMultiplier, afterExitSlowDuration);
    }

    private void OnTriggerExit(Collider other)
    {
        SprinklerSlowTarget target = other.GetComponentInParent<SprinklerSlowTarget>();

        if (target == null)
        {
            return;
        }

        targetsInside.Remove(target);

        // 작동 중에 벗어나면 3초간 감속 유지
        if (isRunning)
        {
            target.ApplySlow(slowMultiplier, afterExitSlowDuration);
        }
    }

    /// <summary>
    /// 해킹 성공 후 스프링클러를 작동
    /// 한 번 작동한 스프링클러는 다시 작동할 수 없음
    /// </summary>
    public void ActivateSprinkler()
    {
        if (isUsed)
        {
            Debug.Log($"[SprinklerZone] 이미 사용한 스프링클러입니다: {gameObject.name}");
            return;
        }

        isUsed = true;

        if (runningRoutine != null)
        {
            StopCoroutine(runningRoutine);
        }

        runningRoutine = StartCoroutine(RunSprinklerRoutine());
    }

    private IEnumerator RunSprinklerRoutine()
    {
        isRunning = true;

        Debug.Log($"[SprinklerZone] 스프링클러 작동 시작: {gameObject.name}");

        foreach (SprinklerSlowTarget target in targetsInside)
        {
            if (target == null)
            {
                continue;
            }

            target.ApplySlow(slowMultiplier, afterExitSlowDuration);
        }

        yield return new WaitForSeconds(activeDuration);

        isRunning = false;
        runningRoutine = null;

        Debug.Log($"[SprinklerZone] 스프링클러 작동 종료: {gameObject.name}");
    }

    [ContextMenu("TEST Activate")]
    private void TestActivate()
    {
        ActivateSprinkler();
    }
}
