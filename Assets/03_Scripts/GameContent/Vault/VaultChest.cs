using Fusion;
using UnityEngine;

/// <summary>
/// VaultChest
///
/// 담당:
/// - 실제 인벤토리의 열쇠 검사 및 소비 요청
/// - 사용된 열쇠 개수 네트워크 동기화
/// - 잠금 해제 후 금고 열기 (Open By Minigame 켬: 해킹 미니게임 성공 후 / 끔: E키로 바로)
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

    [Header("Open Rule")]
    [Tooltip("켜면 열쇠로 잠금을 푼 뒤 E로 바로 열리지 않고, 해킹 미니게임에 성공해야 열립니다.\n" +
             "미니게임 쪽(예: TutorialVault)이 성공 시 RequestOpenAfterMinigame을 호출합니다.")]
    [SerializeField] private bool openByMinigame = true;

    [Header("Chest Visual")]
    [SerializeField] private GameObject chestVisual;
    [SerializeField] private Collider chestCollider;

    [Header("Reward")]
    [SerializeField] private NetworkObject catnipPrefab;
    [SerializeField] private Transform catnipSpawnPoint;

    [Tooltip("바닥에 생성되는 보상의 아이템 ID. 전설의 캣닢(ItemData_Catleaf)과 같다")]
    [SerializeField] private int rewardItemId = 1;

    [Networked]
    public int UsedKeyCount { get; set; }

    [Networked]
    public NetworkBool IsOpened { get; set; }

    public float MaxUseDistance => maxUseDistance;

    [Tooltip("금고 경계에서 이 거리(m) 안으로 다가가야 안내가 뜨고 E 가 먹는다. 서버 검증(Max Use Distance)과는 별개다")]
    [SerializeField, Min(0f)] private float interactMargin = 1.5f;

    /// <summary>플레이어가 금고 앞 상호작용 범위 안에 있는가. 안내 표시와 E 입력이 같은 기준을 쓴다.</summary>
    public bool IsPlayerInRange(Vector3 playerPosition)
    {
        return InteractRange.IsInRange(this, playerPosition, interactMargin);
    }

    /// <summary>잠금 해제 뒤 해킹 미니게임에 성공해야 열리는 금고인지</summary>
    public bool OpenByMinigame => openByMinigame;

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
    /// 사망 재시도 등으로 월드를 되돌릴 때 열쇠 사용 횟수와 개봉 상태를 복원
    /// StateAuthority에서만 적용
    /// </summary>
    public void RestoreCheckpointState(int usedKeyCount, bool opened)
    {
        if (Object == null || !Object.IsValid || !Object.HasStateAuthority)
            return;

        UsedKeyCount = Mathf.Max(0, usedKeyCount);
        IsOpened = opened;
        ApplyVisualState();
    }

    /// <summary>
    /// 해킹 미니게임에 성공한 뒤 금고 개봉을 요청
    /// 잠금이 풀린 금고에서만 열리며, 보상 생성 여부는 호출한 쪽이 정함
    /// (보상을 따로 지급하는 쪽은 spawnReward를 false로 넘김)
    /// </summary>
    public void RequestOpenAfterMinigame(NetworkObject playerObject, bool spawnReward = true)
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

        RPC_OpenAfterMinigame(playerObject, spawnReward);
    }

    [Rpc(
        RpcSources.All,
        RpcTargets.StateAuthority,
        HostMode = RpcHostMode.SourceIsHostPlayer)]
    private void RPC_OpenAfterMinigame(
        NetworkObject playerObject,
        bool spawnReward,
        RpcInfo info = default)
    {
        if (!IsValidPlayer(playerObject, info.Source))
        {
            Debug.LogWarning(
                "[VaultChest] 플레이어 검증 또는 거리 검사 실패"
            );
            return;
        }

        if (IsOpened)
            return;

        // 열쇠로 잠금을 풀지 않았다면 미니게임에 성공해도 열리지 않음
        if (!IsUnlocked)
        {
            Debug.LogWarning(
                "[VaultChest] 열쇠로 잠금을 풀지 않아 열 수 없습니다."
            );
            return;
        }

        OpenVaultOnAuthority(spawnReward);
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
        // 1. 모든 열쇠 사용 완료
        //    - 미니게임 방식: 여기서는 열지 않음 (미니게임 성공 후 RequestOpenAfterMinigame)
        //    - 즉시 개봉 방식: 바로 금고 열기
        // ==========================================
        if (IsUnlocked)
        {
            if (!openByMinigame)
                OpenVaultOnAuthority(true);

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
    private void OpenVaultOnAuthority(bool spawnReward)
    {
        if (!Object.HasStateAuthority)
            return;

        if (IsOpened || !IsUnlocked)
            return;

        if (spawnReward)
        {
            if (catnipPrefab == null)
            {
                Debug.LogError("[VaultChest] Catnip Prefab이 없습니다.");
                return;
            }

            // 생성 위치가 지정돼 있지 않아도 금고가 영영 안 열리는 일이 없게, 금고 위쪽에 놓는다.
            Vector3 rewardPosition = catnipSpawnPoint != null
                ? catnipSpawnPoint.position
                : transform.position + Vector3.up * 0.6f;
            Quaternion rewardRotation = catnipSpawnPoint != null
                ? catnipSpawnPoint.rotation
                : transform.rotation;

            // 캣닢 프리팹은 이전 맵 스케일(루트 12배, 12m×24m 단단한 충돌체)이라 그대로 생성하면 플레이어 몸속에
            // 거대한 충돌체가 생겨 CharacterController 가 튕겨 나간다. 사망 드롭과 같은 경로로 만들면 NetworkItem 이
            // 현재 캐릭터 크기(0.45m)에 맞추고 바닥 위에 얹어 준다. 금고(NetworkObject)는 바닥 탐색에서 빠진다.
            Vector3 groundPosition = PlayerDeathPlacement.GroundPoint(rewardPosition, transform, out _);
            int itemId = rewardItemId;

            NetworkObject spawnedCatnip = Runner.Spawn(
                catnipPrefab,
                groundPosition,
                rewardRotation,
                onBeforeSpawned: (runner, spawned) =>
                {
                    NetworkItem rewardItem = spawned.GetComponent<NetworkItem>();

                    if (rewardItem != null)
                    {
                        rewardItem.InitializeDeathDrop(itemId, groundPosition, rewardRotation);
                    }
                }
            );

            if (spawnedCatnip == null)
            {
                Debug.LogError("[VaultChest] 황금 캣닢 생성 실패");
                return;
            }
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
        // 미니게임 방식이면 해킹 안내는 미니게임 쪽(예: TutorialVault)이 띄우므로 여기서는 비움
        if (IsUnlocked)
            return openByMinigame ? "" : "금고가 열렸습니다. (E키로 OPEN)";

        // 아직 잠겼지만 열쇠를 선택한 상태
        if (hasSelectedKey)
            return "E키로 열쇠를 사용하세요";

        // 맨손이거나 다른 아이템을 선택한 상태
        return
            $"열쇠가 필요합니다 ({UsedKeyCount}/{Mathf.Max(1, requiredKeyCount)})";
    }
}
