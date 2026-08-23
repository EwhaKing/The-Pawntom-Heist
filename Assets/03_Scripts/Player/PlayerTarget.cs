using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// PlayerTarget
///
/// 담당:
/// - CCTV 위치 탭에서 추적할 플레이어로 등록
/// - 현재 씬에 존재하는 모든 플레이어 위치를 CCTV UI가 읽을 수 있게 함
///
/// 사용 위치:
/// - Player 프리팹 루트 오브젝트에 붙임
/// </summary>
/// 
public class PlayerTarget : MonoBehaviour
{
    /// <summary>
    /// 현재 씬에 존재하는 모든 CCTV 추적 대상 플레이어 목록
    /// </summary>
    public static readonly List<PlayerTarget> AllTargets = new List<PlayerTarget>();

    [Header("Marker Target")]
    [Tooltip("마커 위치 기준점 비워두면 이 오브젝트의 Transform을 사용")]
    [SerializeField] private Transform markerTarget;

    /// <summary>
    /// CCTV 마커가 따라갈 실제 위치
    /// </summary>
    public Transform MarkerTarget => markerTarget != null ? markerTarget : transform;

    private void OnEnable()
    {
        if (!AllTargets.Contains(this))
        {
            AllTargets.Add(this);
        }
    }

    private void OnDisable()
    {
        if (AllTargets.Contains(this))
        {
            AllTargets.Remove(this);
        }
    }

    private void OnDestroy()
    {
        if (AllTargets.Contains(this))
        {
            AllTargets.Remove(this);
        }
    }
}
