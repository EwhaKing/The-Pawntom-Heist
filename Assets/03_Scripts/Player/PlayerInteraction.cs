using Fusion;
using UnityEngine;

/// <summary>
/// PlayerInteraction
///
/// 담당:
/// - 로컬 플레이어의 E키 상호작용 처리
/// - 카메라 정면으로 Raycast / SphereCast 발사
/// - 자기 캐릭터 Collider 무시
/// - MainbaseMachine 상호작용
/// - 출발 버튼 상호작용
/// - 실제 격벽 현장 조작
/// - NetworkItem 획득 및 인벤토리 추가
/// - 획득한 월드 아이템 제거 요청
///
/// 사용 위치:
/// - Player 프리팹 루트 오브젝트
///
/// 필요 조건:
/// - Player에 NetworkObject
/// - Player에 InventoryManager
/// - 상호작용 대상에 Collider
/// - 본부 기계에 ControlMachine
/// - 아이템에 NetworkItem
/// </summary>
public class PlayerInteraction : NetworkBehaviour
{
    [Header("Camera")]
    [SerializeField] private Camera playerCamera;

    [Header("Interact")]
    [Tooltip("일반 상호작용 최대 거리")]
    [SerializeField] private float interactDistance = 4f;

    [Tooltip("Raycast가 대상을 맞추지 못했을 때 사용하는 보조 SphereCast 반경")]
    [SerializeField] private float interactRadius = 0.2f;

    [Tooltip("현장에서 격벽을 조작할 수 있는 최대 거리")]
    [SerializeField, Min(0.5f)]
    private float wallInteractDistance = 3f;

    [Header("Interaction Layer")]
    [Tooltip("상호작용 탐색에 포함할 레이어입니다.")]
    [SerializeField] private LayerMask interactLayerMask = ~0;

    private InventoryManager inventoryManager;

    /// <summary>
    /// Fusion에서 로컬 플레이어가 생성되었을 때
    /// 인벤토리와 카메라를 초기화
    /// </summary>
    public override void Spawned()
    {
        inventoryManager = GetComponent<InventoryManager>();

        if (inventoryManager == null)
        {
            Debug.LogError(
                "[PlayerInteraction] Player에 InventoryManager가 없습니다."
            );
        }

        if (!Object.HasInputAuthority)
        {
            return;
        }

        playerCamera = Camera.main;

        if (playerCamera == null)
        {
            Debug.LogWarning(
                "[PlayerInteraction] MainCamera를 찾지 못했습니다."
            );
        }

        Debug.Log(
            "[PlayerInteraction] 내 플레이어 상호작용 준비 완료"
        );
    }

    /// <summary>
    /// 로컬 플레이어의 E키 입력을 확인
    ///
    /// 기존 코드에서는 FixedUpdateNetwork와 Update에서
    /// E키를 중복 처리할 가능성이 있었으므로
    /// 여기서만 상호작용을 실행합니다.
    ///
    /// 현재 프로젝트에서 정상 작동하던
    /// Input.GetKeyDown(KeyCode.E) 방식을 유지
    /// </summary>
    private void Update()
    {
        if (Object == null || !Object.HasInputAuthority)
        {
            return;
        }

        // 다른 UI가 열려 있거나 해킹 진행 중이면 상호작용 차단
        if (GameplayInputBlocker.IsBlocked ||
            Hacking.HackingManager.AnyHackingActive)
        {
            return;
        }

        // 연출 중 잠금(튜토리얼 대사·오프닝 등)으로 게임 입력이 꺼져 있으면 E 도 막는다.
        // E 는 입력 액션이 아니라 키를 직접 읽어서, 액션을 꺼도 따로 확인해야 한다.
        if (InputManager.Instance != null && !InputManager.Instance.IsGameplayInputEnabled)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.E))
        {
            Debug.Log("[PlayerInteraction] E키 입력 감지");

            TryInteract();
        }
    }

    /// <summary>
    /// 카메라 정면으로 상호작용을 시도.
    ///
    /// 탐색 순서:
    /// 1. 정확한 Raycast
    /// 2. Raycast가 아무것도 맞추지 못하면 SphereCast
    ///
    /// 자기 플레이어의 Collider는 제외
    /// </summary>
    private void TryInteract()
    {
        if (!Object.HasInputAuthority)
        {
            return;
        }

        // 카메라 참조 복구
        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }

        if (playerCamera == null)
        {
            Debug.LogError(
                "[PlayerInteraction] playerCamera가 없습니다."
            );

            return;
        }

        // 카메라 정면으로 Ray 생성
        Ray ray = new Ray(
            playerCamera.transform.position,
            playerCamera.transform.forward
        );

        // Scene View에서 탐색 방향 확인
        Debug.DrawRay(
            ray.origin,
            ray.direction * interactDistance,
            Color.yellow,
            2f
        );

        Debug.Log("[PlayerInteraction] 상호작용 Ray 발사");

        // Raycast 우선 + SphereCast 보조 탐색
        if (!TryFindInteractionHit(ray, out RaycastHit hit))
        {
            Debug.Log(
                "[PlayerInteraction] 상호작용 대상과 충돌하지 않았습니다."
            );

            return;
        }

        // 어떤 오브젝트가 맞았는지 확인
        Debug.Log(
            $"[PlayerInteraction] 맞춘 오브젝트: {hit.collider.name} / " +
            $"Root: {hit.collider.transform.root.name} / " +
            $"Distance: {hit.distance:F2} / " +
            $"내 캐릭터: {IsMyCollider(hit.collider)}",
            hit.collider.gameObject
        );

        // ==========================================
        // 0. 금고 상호작용
        // ==========================================

        VaultChest vaultChest =
            hit.collider.GetComponentInParent<VaultChest>();

        // 디버깅: 현재 충돌한 오브젝트의 종류 확인
        Debug.Log(
            $"[PlayerInteraction] 충돌 대상 검사 / " +
            $"Vault={vaultChest != null}, " +
            $"Wall={hit.collider.GetComponentInParent<ControllableWall>() != null}, " +
            $"Machine={hit.collider.GetComponentInParent<ControlMachine>() != null}"
        );

        if (vaultChest != null)
        {
            Debug.Log("[PlayerInteraction] VaultChest 감지 성공!");

            // 현재 인벤토리 상태 확인
            InventoryManager inventory =
                GetComponent<InventoryManager>();

            if (inventory != null)
            {
                Debug.Log(
                    $"[PlayerInteraction] 선택 슬롯={inventory.SelectedSlotIndex}, " +
                    $"손에 든 ItemId={inventory.GetHeldItemId()}"
                );
            }

            // 열쇠 사용과 금고 개봉은 VaultChest가 판정
            vaultChest.RequestInteract(Object);
            return;
        }

        // ==========================================
        // 1. 본부 조작 기계
        // ==========================================

        ControlMachine controlMachine =
            hit.collider.GetComponentInParent<ControlMachine>();

        if (controlMachine != null)
        {
            Debug.Log(
                "[PlayerInteraction] ControlMachine 감지 성공"
            );

            controlMachine.TryInteract();
            return;
        }

        // ==========================================
        // 2. 출발 버튼
        // ==========================================

        EscapeButtonInteraction escapeButton =
            hit.collider.GetComponentInParent<EscapeButtonInteraction>();

        if (escapeButton != null)
        {
            Debug.Log(
                "[PlayerInteraction] 출발 버튼 발견"
            );

            escapeButton.TryInteract();
            return;
        }

        // ==========================================
        // 3. 현장 격벽 조작
        // ==========================================

        ControllableWall wall =
            hit.collider.GetComponentInParent<ControllableWall>();

        if (wall != null)
        {
            if (hit.distance > wallInteractDistance)
            {
                Debug.Log(
                    "[PlayerInteraction] 격벽이 너무 멀어 열 수 없습니다."
                );

                return;
            }

            Debug.Log(
                "[PlayerInteraction] 현장 격벽 상호작용"
            );

            wall.TryFieldOpen();
            return;
        }

        // ==========================================
        // 4. 월드 아이템 획득
        // ==========================================

        if (hit.collider.GetComponentInParent<NetworkItem>() == null)
        {
            Debug.Log(
                "[PlayerInteraction] 맞춘 오브젝트에 NetworkItem이 없습니다."
            );

            return;
        }

        // 가장 가까운 콜라이더가 곧 겨눈 아이템은 아니다 — 콜라이더가 보이는 모양보다 큰 아이템(그림 등)이
        // 옆 아이템 앞을 덮고 있으면 그쪽이 먼저 맞는다. 광선에 걸린 아이템들 중 보이는 모양을 겨눈 것을 다시 고른다.
        NetworkItem item = FindAimedItem(ray);

        if (item == null)
        {
            Debug.Log(
                "[PlayerInteraction] 조준선이 아이템의 보이는 모양을 겨누고 있지 않습니다."
            );

            return;
        }

        // 네트워크에 아직 정상 등록되지 않은
        // 아이템의 Networked Property 접근 방지
        if (item.Object == null || !item.Object.IsValid)
        {
            Debug.LogWarning(
                $"[PlayerInteraction] 아직 네트워크 Spawn 상태가 아닌 아이템: {item.name}"
            );

            return;
        }

        // 이미 획득된 아이템인지 확인
        if (item.IsPickedUp)
        {
            Debug.Log(
                "[PlayerInteraction] 이미 획득된 아이템입니다."
            );

            return;
        }

        if (inventoryManager == null)
        {
            Debug.LogError(
                "[PlayerInteraction] inventoryManager가 없습니다."
            );

            return;
        }

        Debug.Log(
            $"[PlayerInteraction] NetworkItem 발견 ItemId={item.ItemId}"
        );

        // 인벤토리에 아이템 추가
        bool added = inventoryManager.TryAddItem(item.ItemId);

        if (!added)
        {
            Debug.Log(
                "[PlayerInteraction] 인벤토리에 아이템 추가 실패"
            );

            return;
        }

        Debug.Log(
            $"[PlayerInteraction] 아이템 획득 성공 ItemId={item.ItemId}"
        );

        // 기존 아이템 상태 변경 로직 유지
        item.IsPickedUp = true;

        // 월드 아이템 제거
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
    /// 광선(과 보조 SphereCast)에 걸린 아이템 중 조준선이 실제로 겨눈 것을 고른다.
    /// 판정은 콜라이더가 아니라 렌더러 경계로 한다(<see cref="Pawntom.Player.Core.InteractAimSelector"/>).
    /// 아이템이 아닌 단단한 물체(벽 등)보다 뒤에 있는 아이템은 고르지 않는다. E 를 누를 때만 불린다.
    /// </summary>
    private NetworkItem FindAimedItem(Ray ray)
    {
        System.Collections.Generic.List<NetworkItem> items = new System.Collections.Generic.List<NetworkItem>(4);
        float blockDistance = float.MaxValue;

        CollectItems(Physics.RaycastAll(ray, interactDistance, interactLayerMask, QueryTriggerInteraction.Collide), items, ref blockDistance);
        CollectItems(Physics.SphereCastAll(ray, interactRadius, interactDistance, interactLayerMask, QueryTriggerInteraction.Collide), items, ref blockDistance);

        System.Collections.Generic.List<Bounds> visuals = new System.Collections.Generic.List<Bounds>(items.Count);

        for (int i = 0; i < items.Count; i++)
        {
            visuals.Add(VisualBounds(items[i]));
        }

        int chosen = Pawntom.Player.Core.InteractAimSelector.Choose(ray, interactDistance, visuals, interactRadius, blockDistance);
        return chosen >= 0 ? items[chosen] : null;
    }

    private void CollectItems(RaycastHit[] hits, System.Collections.Generic.List<NetworkItem> items, ref float blockDistance)
    {
        for (int i = 0; i < hits.Length; i++)
        {
            Collider col = hits[i].collider;

            if (col == null || IsMyCollider(col))
            {
                continue;
            }

            NetworkItem item = col.GetComponentInParent<NetworkItem>();

            if (item != null)
            {
                if (!items.Contains(item))
                {
                    items.Add(item);
                }

                continue;
            }

            // 아이템이 아닌 단단한 물체(벽·선반)는 그 뒤를 가린다. SphereCast 는 옆을 스치는 것까지 맞으므로 광선 위의 점만 가림으로 친다.
            if (!col.isTrigger && hits[i].distance > 0f && hits[i].distance < blockDistance && IsOnRay(hits[i]))
            {
                blockDistance = hits[i].distance;
            }
        }
    }

    // SphereCast 결과는 광선 옆을 스친 것일 수 있다. 맞은 점이 광선에서 반경보다 멀면 가림으로 치지 않는다.
    private bool IsOnRay(RaycastHit hit)
    {
        if (playerCamera == null)
        {
            return true;
        }

        Vector3 origin = playerCamera.transform.position;
        Vector3 direction = playerCamera.transform.forward;
        Vector3 toPoint = hit.point - origin;
        Vector3 perpendicular = toPoint - direction * Vector3.Dot(toPoint, direction);
        return perpendicular.sqrMagnitude <= 0.0025f; // 5cm
    }

    /// <summary>아이템의 보이는 경계. 크기가 있는 렌더러만 합친다(빈 루트 렌더러 제외). 없으면 콜라이더 경계.</summary>
    private static Bounds VisualBounds(NetworkItem item)
    {
        bool has = false;
        Bounds bounds = new Bounds(item.transform.position, Vector3.zero);
        Renderer[] renderers = item.GetComponentsInChildren<Renderer>();

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];

            if (!renderer.enabled || renderer.bounds.size.sqrMagnitude < 0.0001f)
            {
                continue;
            }

            if (!has)
            {
                bounds = renderer.bounds;
                has = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        if (has)
        {
            return bounds;
        }

        Collider[] colliders = item.GetComponentsInChildren<Collider>();

        for (int i = 0; i < colliders.Length; i++)
        {
            if (i == 0)
            {
                bounds = colliders[i].bounds;
            }
            else
            {
                bounds.Encapsulate(colliders[i].bounds);
            }
        }

        return bounds;
    }

    /// <summary>
    /// Raycast를 먼저 시도
    ///
    /// 정확한 Raycast가 대상을 맞췄으면 그 대상을 사용
    /// 아무것도 맞추지 못했을 때만
    /// 작은 SphereCast로 보조 탐색합니다.
    /// </summary>
    private bool TryFindInteractionHit(
        Ray ray,
        out RaycastHit selectedHit)
    {
        selectedHit = default;

        // 1차: 카메라 정면의 정확한 Raycast
        RaycastHit[] rayHits = Physics.RaycastAll(
            ray,
            interactDistance,
            interactLayerMask,
            QueryTriggerInteraction.Collide
        );

        if (TrySelectClosestHit(rayHits, out selectedHit))
        {
            Debug.Log(
                "[PlayerInteraction] Raycast로 대상 감지"
            );

            return true;
        }

        // 2차: 작은 SphereCast로 보조 탐색
        RaycastHit[] sphereHits = Physics.SphereCastAll(
            ray,
            interactRadius,
            interactDistance,
            interactLayerMask,
            QueryTriggerInteraction.Collide
        );

        if (TrySelectClosestHit(sphereHits, out selectedHit))
        {
            Debug.Log(
                "[PlayerInteraction] SphereCast로 대상 감지"
            );

            return true;
        }

        return false;
    }

    /// <summary>
    /// 여러 충돌 결과 중 가장 가까운 유효한 대상 탐색
    ///
    /// 제외 대상:
    /// - 자기 캐릭터의 Collider
    /// - 상호작용 컴포넌트가 없는 Trigger
    ///
    /// 일반 벽은 통과하지 않음
    /// 벽 뒤에 있는 기계를 잘못 조작하지 않도록 하기 위함
    /// </summary>
    private bool TrySelectClosestHit(
        RaycastHit[] hits,
        out RaycastHit selectedHit)
    {
        selectedHit = default;

        if (hits == null || hits.Length == 0)
        {
            return false;
        }

        // 카메라에서 가까운 순서로 정렬
        System.Array.Sort(
            hits,
            (a, b) => a.distance.CompareTo(b.distance)
        );

        foreach (RaycastHit hit in hits)
        {
            Collider col = hit.collider;

            if (col == null)
            {
                continue;
            }

            // 자기 캐릭터 Collider는 무시
            if (IsMyCollider(col))
            {
                continue;
            }

            // Trigger가 상호작용 대상인지 확인
            if (col.isTrigger)
            {
                bool isInteractableTrigger =
                    col.GetComponentInParent<ControlMachine>() != null ||
                    col.GetComponentInParent<EscapeButtonInteraction>() != null ||
                    col.GetComponentInParent<ControllableWall>() != null ||
                    col.GetComponentInParent<NetworkItem>() != null ||
                    col.GetComponentInParent<VaultChest>() != null;

                // 발견 구역, MainbaseZone 같은
                // 상호작용과 무관한 Trigger는 무시
                if (!isInteractableTrigger)
                {
                    continue;
                }
            }

            selectedHit = hit;

            return true;
        }

        return false;
    }

    /// <summary>
    /// 충돌한 Collider가 내 캐릭터에 속하는지 확인
    ///
    /// Player 하위에 있는 Capsule, 몸통 Collider,
    /// 자식 Collider를 모두 자기 자신으로 판정
    /// </summary>
    private bool IsMyCollider(Collider col)
    {
        if (col == null)
        {
            return false;
        }

        // 내 Player 오브젝트 또는 자식인지 확인
        if (col.transform.IsChildOf(transform))
        {
            return true;
        }

        // Collider가 PlayerInteraction을 가진
        // 내 플레이어의 하위에 있는지도 확인
        PlayerInteraction hitPlayer =
            col.GetComponentInParent<PlayerInteraction>();

        if (hitPlayer == this)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// 클라이언트에서 아이템 제거를 요청
    /// 실제 제거는 StateAuthority가 수행
    /// </summary>
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestDespawnItem(NetworkObject itemObject)
    {
        if (itemObject == null)
        {
            return;
        }

        Runner.Despawn(itemObject);
    }
}