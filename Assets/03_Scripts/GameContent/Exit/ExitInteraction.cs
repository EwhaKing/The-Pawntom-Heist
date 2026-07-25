using Fusion;
using UnityEngine;

/// <summary>
/// ExitInteraction
/// 
/// 담당:
/// - 로컬 플레이어의 탈출구 상호작용 입력 처리
/// - ExitObject 감지
/// - Catleaf 보유 여부 확인 후 탈출 조건 해금 요청
/// - 플레이어 탈출 요청
/// 
/// 참고:
/// - Player 오브젝트에 붙는 NetWorkBehaviour
/// - InventoryManager.Instance 사용x
/// - itemId로 Catleaf 보유 여부 확인
/// 
/// TODO:
/// - 탈출 성공 UI 연결
/// - 탈출한 플레이어 이동 / 입력 제한
/// - 모든 생존자 탈출시 게임 클리어 처리
/// - Catleaf 드랍 처리 
/// </summary>
public class ExitInteraction : NetworkBehaviour
{
    [Header("Camera")]
    [SerializeField] private Camera playerCamera;
    
    [Header("Interaction")]
    [SerializeField] private float interactDistance = 50f;
    [SerializeField] private float interactRadius = 0.5f;

    [Header("Required Item")]
    [SerializeField] private int requiredItemId = 1; // Catleaf의 itemId

    private InventoryManager inventoryManager;

    private NetworkButtons previousButtons;

    /// <summary>
    /// Fusion에서 Player NetworkObject가 Spawn된 후 호출
    /// 플레이어의 InventoryManager와 Camera 초기화
    /// </summary>
    public override void Spawned()
    {
        inventoryManager = GetComponent<InventoryManager>();

        if (inventoryManager == null)
            Debug.LogError("[ExitInteraction] Player에 InventoryManager가 없습니다.");
    
        if (Object.HasInputAuthority)
            playerCamera = Camera.main;
    }

    /// <summary>
    /// Fusion 네트워크 Tick마다 호출
    /// Interact 입력이 새로 눌렸을 때만 탈출 상호작용 시도
    /// </summary>
    public override void FixedUpdateNetwork()
    {
        if (!GetInput(out NetworkInputData input))
            return;
        
        if (input.Buttons.WasPressed(previousButtons, (int)InputButton.Interact))
            TryInteractExit();
        
        previousButtons = input.Buttons;
    }

    /// <summary>
    /// 탈출구 조준하고 있는지 확인
    /// ExitObject를 찾으면 서버 / 호스트에 탈출 상호작용 요청
    /// </summary>
    private void TryInteractExit()
    {
        if (!Object.HasInputAuthority)
            return;
        
        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }
    }
    private void Start()
    {
        if (playerCamera == null)
        {
            playerCamera = Camera.main;
            Debug.LogError("[ExitInteraction] playerCamera가 없습니다.");
            return;
        }

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);

        Debug.DrawRay(ray.origin, ray.direction * interactDistance, Color.green, 2f);

        if (!Physics.SphereCast(ray, interactRadius, out RaycastHit hit, interactDistance))
        {
            Debug.Log("[ExitInteraction] 아무것도 맞추지 못했습니다.");
            return;
        }

        Debug.Log($"[ExitInteraction] {hit.collider.name}을(를) 조준했습니다.");

        ExitObject exit = hit.collider.GetComponent<ExitObject>();

        if (exit == null)
        {
            exit = hit.collider.GetComponentInParent<ExitObject>();
            Debug.Log($"[ExitInteraction] {hit.collider.name} 또는 부모에 ExitObject가 없습니다.");
            return;
        }

        RPC_RequestExitInteraction();
    }

    /// <summary>
    /// 탈출 상호작용 요청
    /// InputAuthority가 호출하고 StateAuthority에서 실제 판정 처리
    /// </summary>
    
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestExitInteraction()
    {
        if (inventoryManager == null)
        {
            inventoryManager = GetComponent<InventoryManager>();
            Debug.LogError("[ExitInteraction] inventoryManager가 없습니다.");
            return;
        }

        if (GameManager.Instance == null)
        {
            Debug.LogError("[ExitInteraction] GameManager.Instance가 없습니다.");
            return;
        }

        // 조건 충족 x
        if (!GameManager.Instance.IsEscapeUnlocked)
        {
            TryUnlockEscape();
            return;
        }

        TryCompleteEscape();
    }

    /// <summary>
    /// 탈출구 해금 시도
    /// Catleaf 가진 플레이어만 탈출구 해금 가능
    /// </summary>
    private void TryUnlockEscape()
    {
        if (!inventoryManager.HasItem(requiredItemId))
        {
            ItemData data = ItemDatabase.Instance != null
                ? ItemDatabase.Instance.GetItemData(requiredItemId) : null;
            
            string itemName = data != null ? data.itemName : requiredItemId.ToString();

            Debug.Log($"[ExitInteraction] {itemName}를 가진 플레이어가 먼저 탈출구를 열어야 합니다.");
            return;
        }

        GameManager.Instance.UnlockEscape();

        Debug.Log($"[ExitInteraction] Catleaf를 사용해 탈출구를 열었습니다.");
        Debug.Log("[ExitInteraction] 이제 모든 플레이어가 탈출할 수 있습니다.");
    }

    /// <summary>
    /// 플레이어 탈출 처리
    /// 탈출구가 열린 뒤에는 Catleaf가 없어도 탈출 가능
    /// </summary>
    private void TryCompleteEscape()
    {
        Debug.Log($"[ExitInteraction] player {Object.InputAuthority} 탈출 성공!");

        GameManager.Instance.PlayerEscaped(Object.InputAuthority);
    }
}
