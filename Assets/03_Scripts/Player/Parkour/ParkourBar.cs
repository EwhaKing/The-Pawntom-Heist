using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 고양이가 매달릴 수 있는 철봉. 철봉 오브젝트에 붙이고 축 방향·길이만 맞추면 된다.
/// 공중에서 손이 닿으면 자동으로 매달린다(PlayerParkour).
/// <para>
/// 물리 레이어를 따로 쓰지 않고 활성 철봉 목록으로 찾는다(씬 오브젝트라 모든 피어에서 같다).
/// 위에 올라서는 판정은 철봉 자신의 콜라이더가 한다 — 이 컴포넌트는 "잡는 선분"만 정의한다.
/// </para>
/// </summary>
[DisallowMultipleComponent]
public class ParkourBar : MonoBehaviour
{
    private static readonly List<ParkourBar> ActiveBars = new List<ParkourBar>();

    /// <summary>현재 씬에서 활성화된 철봉들.</summary>
    public static IReadOnlyList<ParkourBar> Active => ActiveBars;

    [Tooltip("철봉이 뻗은 방향(로컬). 기본은 로컬 X 축")]
    [SerializeField] private Vector3 localAxis = Vector3.right;

    [Tooltip("잡을 수 있는 구간 길이(월드 단위 m). 오브젝트 위치를 가운데로 양쪽 절반씩")]
    [SerializeField, Min(0f)] private float length = 1f;

    public Vector3 Center => transform.position;
    public Vector3 Axis => transform.TransformDirection(localAxis).normalized;
    public float HalfLength => length * 0.5f;

    /// <summary>선분 위에서 <paramref name="point"/> 와 가장 가까운 점.</summary>
    public Vector3 ClosestPoint(Vector3 point)
    {
        Vector3 axis = Axis;
        float t = Mathf.Clamp(Vector3.Dot(point - Center, axis), -HalfLength, HalfLength);
        return Center + axis * t;
    }

    private void OnEnable()
    {
        if (!ActiveBars.Contains(this))
        {
            ActiveBars.Add(this);
        }
    }

    private void OnDisable()
    {
        ActiveBars.Remove(this);
    }

    private void OnDrawGizmos()
    {
        Vector3 half = Axis * HalfLength;
        Gizmos.color = new Color(1f, 0.6f, 0.1f);
        Gizmos.DrawLine(Center - half, Center + half);
        Gizmos.DrawWireSphere(Center - half, 0.03f);
        Gizmos.DrawWireSphere(Center + half, 0.03f);
    }
}
