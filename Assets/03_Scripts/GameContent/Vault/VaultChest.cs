using Fusion;
using UnityEngine;

/// <summary>
/// VaultChest
///
/// 담당:
/// - 실제 인벤토리의 열쇠 검사 및 소비 요청
/// - 사용된 열쇠 개수 네트워크 동기화
/// - 잠금 해제 후 E키로 금고 열기
/// - 황금 캣닢 네트워크 스폰
/// - 금고 개봉 후 모델 및 Collider 숨기기
///
/// 사용 위치:
/// - TutorialVault_Main
///
/// 필요 컴포넌트:
/// - NetworkObject
/// - BoxCollider
///
/// 주의:
/// - E키 상호작용 요청은 StateAuthority에서 판정
/// - Fusion Host/Server Mode 기준
/// </summary>
public class VaultChest : NetworkBehaviour
{
    [Header("Vault Settings")]
    [SerializeField, Min(1)]
    private int requiredKeyCount = 1;

    [SerializeField, Min(0.5f)]
    private float maxUseDistance = 3.5f;

    [Header("Chest Visual")]
    [SerializeField] private GameObject chestVisual;
    [SerializeField] private Collider chestCollider;

    [Header("Reward")]
    [SerializeField] private NetworkObject catnipPrefab;
    [SerializeField] private Transform catnipSpawnPoint;

    [Networked]
    public int UsedKeyCount { get; set; }

    [Networked]
    public NetworkBool IsOpened { get; set; }

    public float MaxUseDistance => maxUseDistance;

    /// <summary>
    /// 모든 열쇠를 사용하여 잠금이 해제되었는지 확인
    /// </summary>
    public bool IsUnlocked =>
        Object != null &&
        Object.IsValid &&
        UsedKeyCount >= Mathf.Max(1, requiredKeyCount);

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            UsedKeyCount = 0;
            IsOpened = false;
        }

        ApplyVisualState();
    }

    public override void Render()
    {
        ApplyVisualState();
    }

    /// <summary>
    /// 열린 상태라면 금고 모델과 충돌체를 숨김
    /// NetworkObject가 붙어 있는 루트는 유지
    /// </summary>
    private void ApplyVisualState()
    {
        if (Object == null || !Object.IsValid)
            return;

        if (chestVisual != null)
            chestVisual.SetActive(!IsOpened);

        if (chestCollider != null)
            chestCollider.enabled = !IsOpened;
    }

    /// <summary>
    /// E키로 금고 상호작용을 요청
    /// 열쇠 사용과 금고 열기를 하나의 함수로 처리
    /// </summary>
    public void RequestInteract(NetworkObject playerObject)
    {
        if (Object == null || !Object.IsValid)
        {
            Debug.LogWarning(
                "[VaultChest] 금고가 Fusion Spawn 상태가 아닙니다."
            );
            return;
        }

        if (playerObject == null || !playerObject.IsValid)
            return;

        RPC_RequestInteract(playerObject);
    }

    /// <summary>
    /// 서버에서 금고 상호작용을 판정
    ///
    /// 1. 이미 열렸다면 종료
    /// 2. 모든 열쇠가 사용되었다면 금고 개봉
    /// 3. 잠겨 있다면 현재 선택한 열쇠를 소비
    /// </summary>
    [Rpc(
        RpcSources.All,
        RpcTargets.StateAuthority,
        HostMode = RpcHostMode.SourceIsHostPlayer)]
    private void RPC_RequestInteract(
        NetworkObject playerObject,
        RpcInfo info = default)
    {
        // 요청한 플레이어의 소유권과 거리 검사
        if (!IsValidPlayer(playerObject, info.Source))
        {
            Debug.LogWarning(
                "[VaultChest] 플레이어 검증 또는 거리 검사 실패"
            );
            return;
        }

        if (IsOpened)
        {
            Debug.Log("[VaultChest] 이미 열린 금고입니다.");
            return;
        }

        // ==========================================
        // 1. 모든 열쇠 사용 완료 → 금고 열기
        // ==========================================
        if (IsUnlocked)
        {
            OpenVaultOnAuthority();
            return;
        }

        // ==========================================
        // 2. 아직 잠김 → 열쇠 사용 시도
        // ==========================================

        VaultKeyInventoryBridge keyBridge =
            playerObject.GetComponent<VaultKeyInventoryBridge>();

        if (keyBridge == null)
        {
            Debug.LogWarning(
                "[VaultChest] 플레이어에 VaultKeyInventoryBridge가 없습니다."
            );
            return;
        }

        // 여기서 실제 인벤토리의 선택된 슬롯을 검사하고 소비
        bool consumed = keyBridge.TryConsumeKeyOnAuthority();

        if (!consumed)
        {
            Debug.Log(
                "[VaultChest] 열쇠를 선택하지 않았거나 소비에 실패했습니다."
            );
            return;
        }

        // 열쇠가 실제로 소비된 경우에만 진행도 증가
        UsedKeyCount++;

        Debug.Log(
            $"[VaultChest] 열쇠 사용 성공: " +
            $"{UsedKeyCount}/{Mathf.Max(1, requiredKeyCount)}"
        );

        if (IsUnlocked)
        {
            Debug.Log(
                "[VaultChest] 금고 잠금 해제! 다시 E키로 열 수 있습니다."
            );
        }
    }

    /// <summary>
    /// StateAuthority에서 금고를 열고 캣닢을 생성
    /// </summary>
    private void OpenVaultOnAuthority()
    {
        if (!Object.HasStateAuthority)
            return;

        if (IsOpened || !IsUnlocked)
            return;

        if (catnipPrefab == null || catnipSpawnPoint == null)
        {
            Debug.LogError(
                "[VaultChest] Catnip Prefab 또는 Spawn Point가 없습니다."
            );
            return;
        }

        NetworkObject spawnedCatnip = Runner.Spawn(
            catnipPrefab,
            catnipSpawnPoint.position,
            catnipSpawnPoint.rotation
        );

        if (spawnedCatnip == null)
        {
            Debug.LogError("[VaultChest] 황금 캣닢 생성 실패");
            return;
        }

        IsOpened = true;
        ApplyVisualState();

        Debug.Log(
            "[VaultChest] 금고 개봉 완료! 황금 캣닢 생성"
        );
    }

    /// <summary>
    /// RPC를 보낸 플레이어의 소유권과 금고와의 거리를 검사
    /// </summary>
    private bool IsValidPlayer(
        NetworkObject playerObject,
        PlayerRef source)
    {
        if (playerObject == null || !playerObject.IsValid)
            return false;

        if (source == PlayerRef.None ||
            playerObject.InputAuthority != source)
            return false;

        float distance = Vector3.Distance(
            playerObject.transform.position,
            transform.position
        );

        return distance <= maxUseDistance;
    }

    /// <summary>
    /// 현재 금고 상태에 맞는 안내 문구를 반환
    /// </summary>
    public string GetPrompt(bool hasSelectedKey)
    {
        if (Object == null || !Object.IsValid || IsOpened)
            return "";

        // 열쇠 사용이 모두 완료된 상태
        if (IsUnlocked)
            return "금고가 열렸습니다. (E키로 OPEN)";

        // 아직 잠겼지만 열쇠를 선택한 상태
        if (hasSelectedKey)
            return "E키로 열쇠를 사용하세요";

        // 맨손이거나 다른 아이템을 선택한 상태
        return
            $"열쇠가 필요합니다 ({UsedKeyCount}/{Mathf.Max(1, requiredKeyCount)})";
    }
}
