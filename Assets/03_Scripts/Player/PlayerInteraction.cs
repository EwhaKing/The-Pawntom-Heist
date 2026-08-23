using Fusion;
using UnityEngine;

/// <summary>
/// PlayerInteraction
///
/// 담당:
/// - 플레이어의 Interact 입력 처리
/// - 화면 중앙 방향으로 Ray/SphereCast 발사
/// - NetworkItem을 감지하면 인벤토리에 아이템 추가
/// - 아이템 획득 후 월드 아이템 제거 요청
///
/// 사용 위치:
/// - Player 프리팹에 붙임
///
/// 필요 조건:
/// - Player 프리팹에 NetworkObject가 있어야 함
/// - Player 프리팹에 InventoryManager가 있어야 함
/// - 월드 아이템 프리팹에 Collider가 있어야 함
/// - 월드 아이템 프리팹에 NetworkItem이 있어야 함
/// </summary>
public class PlayerInteraction : NetworkBehaviour
{
    [Header("Camera")]
    [SerializeField] private Camera playerCamera;

    [Header("Interact")]
    [SerializeField] private float interactDistance = 100f;
    [SerializeField] private float interactRadius = 2f;

    private InventoryManager inventoryManager;
    private NetworkButtons previousButtons;

    /// <summary>
    /// Fusion에서 Player 오브젝트가 Spawn된 후 호출
    /// 인벤토리와 카메라를 초기화
    /// </summary>
    public override void Spawned()
    {
        inventoryManager = GetComponent<InventoryManager>();

        if (inventoryManager == null)
        {
            Debug.LogError("[PlayerInteraction] Player에 InventoryManager가 없습니다.");
        }

        if (Object.HasInputAuthority)
        {
            playerCamera = Camera.main;
            Debug.Log("[PlayerInteraction] 내 플레이어 상호작용 스크립트 준비 완료");
        }
    }

    /// <summary>
    /// Fusion Tick마다 입력 확인
    /// Interact 버튼이 새로 눌렸을 때 아이템 상호작용 시도
    /// </summary>
    public override void FixedUpdateNetwork()
    {
        // Fusion 입력 가져오기
        if (!GetInput(out NetworkInputData input))
        {
            return;
        }

        // Interact 버튼이 이번 Tick에 새로 눌렸는지 확인
        if (input.Buttons.WasPressed(previousButtons, (int)InputButton.Interact))
        {
            Debug.Log("[PlayerInteraction] Interact 입력 받음");
            TryInteract();
        }

        // 다음 Tick 비교를 위해 현재 버튼 상태 저장
        previousButtons = input.Buttons;
    }

    // UI가 열려있는 동안 플레이어의 동작을 막음
    private void Update()
    {
        if (GameplayInputBlocker.IsBlocked)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.E))
        {
            TryInteract();
        }
    }

    /// <summary>
    /// 카메라 정면으로 상호작용 가능한 아이템을 탐색합니다.
    /// </summary>
    private void TryInteract()
    {
        if (!Object.HasInputAuthority)
        {
            return;
        }

        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }

        if (playerCamera == null)
        {
            Debug.LogError("[PlayerInteraction] playerCamera가 없습니다.");
            return;
        }

        // 카메라 정면으로 Ray 생성
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);

        // Scene 뷰에서 Ray 확인 가능
        Debug.DrawRay(ray.origin, ray.direction * interactDistance, Color.yellow, 2f);

        Debug.Log("[PlayerInteraction] 상호작용 Ray 발사");

        // SphereCast로 조금 넓게 감지
        if (!Physics.SphereCast(ray, interactRadius, out RaycastHit hit, interactDistance))
        {
            Debug.Log("[PlayerInteraction] 아무것도 맞추지 못했습니다.");
            return;
        }

        Debug.Log($"[PlayerInteraction] 맞춘 오브젝트: {hit.collider.name}");

        // 본부 기계인지 확인
        ControlMachine controlMachine = hit.collider.GetComponentInParent<ControlMachine>();

        if (controlMachine != null)
        {
            controlMachine.TryInteract();
            return;
        }

        // 출발 버튼인지 확인
        EscapeButtonInteraction escapeButton = hit.collider.GetComponentInParent<EscapeButtonInteraction>();

        if (escapeButton != null)
        {
            Debug.Log("[PlayerInteraction] 출발 버튼 발견");
            escapeButton.TryInteract();
            return;
        }
        
        // 맞은 오브젝트 또는 부모에서 NetworkItem 찾기
        NetworkItem item = hit.collider.GetComponentInParent<NetworkItem>();

        if (item == null)
        {
            Debug.Log("[PlayerInteraction] 맞춘 오브젝트에 NetworkItem이 없습니다.");
            return;
        }

        // 이미 주운 아이템이면 처리x
        if (item.IsPickedUp)
        {
            Debug.Log("[PlayerInteraction] 이미 획득된 아이템입니다.");
            return;
        }

        if (inventoryManager == null)
        {
            Debug.LogError("[PlayerInteraction] inventoryManager가 없습니다.");
            return;
        }

        Debug.Log($"[PlayerInteraction] NetworkItem 발견 ItemId={item.ItemId}");

        // 인벤토리에 아이템 추가
        bool added = inventoryManager.TryAddItem(item.ItemId);

        if (!added)
        {
            Debug.Log("[PlayerInteraction] 인벤토리에 아이템 추가 실패");
            return;
        }

        Debug.Log($"[PlayerInteraction] 아이템 획득 성공 ItemId={item.ItemId}");

        item.IsPickedUp = true;

        if (Object.HasStateAuthority)
        {
            Runner.Despawn(item.Object);
        }
        else
        {
            RPC_RequestDespawnItem(item.Object);
        }
    }

    /// <summary>
    /// 아이템 제거 요청
    /// </summary>
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestDespawnItem(NetworkObject itemObject)
    {
        if (itemObject == null)
            return;

        Runner.Despawn(itemObject);
    }

    
}