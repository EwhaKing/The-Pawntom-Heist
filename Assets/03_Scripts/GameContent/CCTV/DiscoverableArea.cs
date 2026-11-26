using Fusion;
using UnityEngine;

/// <summary>
/// DiscoverableArea
///
/// 담당:
/// - CCTV 지도에서 특정 구역이 발견되었는지 상태를 관리
/// - 위치탭 / 스프링클러탭 / 전등탭에서 공통으로 사용
///
/// 예:
/// - 플레이어가 방에 들어가면 발견됨(DiscoveryZone)
/// - 분전반 접속을 마치면 발견됨(SecurityNode)
/// - 발견되면 ? 가림막이 사라짐
/// - 발견된 구역의 스프링클러/전등 조작이 가능해짐
///
/// 네트워크:
/// - 발견 여부는 [Networked] <see cref="Discovered"/> — 상태 권한(호스트)만 쓴다. 늦게 들어온 참가자도 값을 받는다.
/// - <see cref="Discover"/> 는 상태 권한에서 부를 때만 상태를 바꾼다. 클라이언트에서 부르면 아무 일도 하지 않는다
///   (발견 판정은 DiscoveryZone·SecurityNode 가 상태 권한 쪽에서 한다 — 클라이언트 요청 경로는 두지 않는다).
/// - NetworkObject 가 아직 붙지 않은 씬(전환기)에서는 예전처럼 로컬 값으로 동작하고 경고를 한 번 남긴다.
///
/// 사용 위치:
/// - Area_01, Area_02 같은 빈 오브젝트에 붙임 (같은 오브젝트에 NetworkObject 필요)
/// </summary>
public class DiscoverableArea : NetworkBehaviour
{
    [Header("Area Info")]
    [SerializeField] private string areaId = "Area_01";

    [Header("State")]
    [Tooltip("시작 시 발견 상태(예: 도킹 구역). 세션 중 실제 값은 상태 권한이 동기화한다")]
    [SerializeField] private bool isDiscovered;

    [Networked, OnChangedRender(nameof(OnDiscoveredChanged))]
    private NetworkBool Discovered { get; set; }

    /// <summary>발견 상태가 이 피어에 반영될 때(원격 포함). UI 가 구독해 새로고침할 수 있다.</summary>
    public event System.Action<DiscoverableArea> StateChanged;

    private bool _warnedOffline;

    public string AreaId => areaId;

    /// <summary>네트워크 준비 전·NetworkObject 가 없는 씬에서는 인스펙터 값(로컬)을 돌려준다.</summary>
    public bool IsDiscovered => IsNetworkReady ? (bool)Discovered : isDiscovered;

    private bool IsNetworkReady => Object != null && Object.IsValid;

    /// <summary>
    /// 이 피어가 발견 판정을 내려도 되는가. 상태 권한이거나, NetworkObject 가 없는 로컬 전용 구역이면 true.
    /// 네트워크 오브젝트가 아직 스폰 전이면 false(클라이언트가 먼저 판정해 갈라지지 않게).
    /// </summary>
    public bool CanDecideDiscovery
    {
        get
        {
            if (IsNetworkReady)
            {
                return Object.HasStateAuthority;
            }

            return !_hasNetworkObject;
        }
    }

    private bool _hasNetworkObject;

    private void Awake()
    {
        _hasNetworkObject = GetComponentInParent<NetworkObject>(true) != null;
    }

    public override void Spawned()
    {
        if (Object.HasStateAuthority && isDiscovered)
        {
            Discovered = true;
        }

        StateChanged?.Invoke(this);
    }

    /// <summary>
    /// 이 구역을 발견 상태로 변경. 상태 권한에서만 반영된다.
    /// </summary>
    public void Discover()
    {
        if (!IsNetworkReady)
        {
            DiscoverOffline();
            return;
        }

        if (!Object.HasStateAuthority || Discovered)
        {
            return;
        }

        Discovered = true;

        Debug.Log($"[DiscoverableArea] 구역 발견됨: {areaId}");
    }

    private void DiscoverOffline()
    {
        if (isDiscovered)
        {
            return;
        }

        if (!_warnedOffline && NetworkRunner.Instances.Count > 0)
        {
            _warnedOffline = true;
            Debug.LogWarning($"[DiscoverableArea] {gameObject.name} 에 NetworkObject 가 없어 발견 상태가 동기화되지 않습니다(로컬만 반영).", this);
        }

        isDiscovered = true;
        StateChanged?.Invoke(this);

        Debug.Log($"[DiscoverableArea] 구역 발견됨(로컬): {areaId}");
    }

    private void OnDiscoveredChanged()
    {
        StateChanged?.Invoke(this);
    }

    /// <summary>
    /// 테스트용으로 발견 상태를 초기화(상태 권한 또는 오프라인에서만)
    /// </summary>
    [ContextMenu("Reset Discovery")]
    private void ResetDiscovery()
    {
        if (IsNetworkReady)
        {
            if (!Object.HasStateAuthority)
            {
                return;
            }

            Discovered = false;
        }
        else
        {
            isDiscovered = false;
        }

        Debug.Log($"[DiscoverableArea] 발견 상태 초기화: {areaId}");
    }

    /// <summary>
    /// 테스트용으로 즉시 발견 처리
    /// </summary>
    [ContextMenu("Test Discover")]
    private void TestDiscover()
    {
        Discover();
    }
}
