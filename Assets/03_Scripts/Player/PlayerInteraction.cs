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

    // 격벽·아이템 거리도 모든 E 상호작용과 같은 기준으로 통일했다(InteractRange 의 상수). 가까이 다가가야 선택된다.
    private static float wallInteractDistance
    {
        get { return InteractRange.WallDistance; }
    }

    // 카메라 기준 거리라, 벽에 걸린 큰 그림도 조준한 만큼 닿는다.
    private static float itemInteractDistance
    {
        get { return InteractRange.ItemDistance; }
    }

    // 아이템 줍기 범위 판정. 안내 표시와 E 입력이 같은 기준을 쓴다.
    private bool IsItemInRange(NetworkItem item)
    {
        Vector3 origin = playerCamera != null ? playerCamera.transform.position : transform.position;
        return InteractRange.DistanceTo(item, origin) <= itemInteractDistance;
    }

    [Header("Interaction Layer")]
    [Tooltip("상호작용 탐색에 포함할 레이어입니다.")]
    [SerializeField] private LayerMask interactLayerMask = ~0;

    private PlayerData _playerData;
    private InventoryManager inventoryManager;

    /// <summary>
    /// 내 플레이어가 E 로 무언가(기계·출발 버튼·격벽·아이템)를 건드렸다. 인자는 맞은 지점.
    /// 1인칭 팔이 "톡" 뻗는 연출에 쓴다. 아이템을 실제로 주웠을 때는 <see cref="LocalItemPickedUp"/> 이 대신 온다.
    /// </summary>
    public static event System.Action<Vector3> LocalInteracted;

    /// <summary>
    /// 내 플레이어가 아이템을 주웠다(월드 아이템이 사라지기 직전). 인자는 그 아이템의 Transform 과 itemId.
    /// 1인칭 팔이 손을 뻗어 아이템을 손으로 끌어오는 연출에 쓴다.
    /// </summary>
    public static event System.Action<Transform, int> LocalItemPickedUp;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        LocalInteracted = null;
        LocalItemPickedUp = null;
    }

    /// <summary>
    /// Fusion에서 로컬 플레이어가 생성되었을 때
    /// 인벤토리와 카메라를 초기화
    /// </summary>
    public override void Spawned()
    {
        _playerData = GetComponent<PlayerData>();
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

        // 씬의 CCTV/다른 MainCamera가 아니라 이 플레이어의 카메라를 사용합니다.
        if (playerCamera == null)
            playerCamera = GetComponent<PlayerController>()?.ViewCamera;
        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>(true);

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

        UpdatePrompt();

        if (Input.GetKeyDown(KeyCode.E))
        {
            Debug.Log("[PlayerInteraction] E키 입력 감지");

            TryInteract();
        }
    }

    // ==========================================
    // 상호작용 안내 ("E | 이름")
    // ==========================================

    private const float PromptRefreshSeconds = 0.1f;
    private float _nextPromptRefresh;
    private string _promptName;

    /// <summary>
    /// 지금 겨누고 있는 상호작용 대상의 이름을 화면에 띄운다. 대상 판정은 E 를 눌렀을 때와 같은 규칙이다.
    /// 판정(Raycast·SphereCast)은 0.1초마다 한 번만 하고, 결과는 매 프레임 안내 쪽에 다시 알려 준다.
    /// </summary>
    private void UpdatePrompt()
    {
        if (Time.unscaledTime >= _nextPromptRefresh)
        {
            _nextPromptRefresh = Time.unscaledTime + PromptRefreshSeconds;
            _promptName = ResolvePromptName();
        }

        InteractPrompt.Show(_promptName);
    }

    private string ResolvePromptName()
    {
        // 죽은 동안에는 안내를 띄우지 않는다(E 도 막혀 있다).
        if (_playerData != null && _playerData.IsDead)
        {
            return null;
        }

        // 씬의 다른 MainCamera 가 아니라 이 플레이어의 카메라를 쓴다(TryInteract 와 같은 복구 순서).
        if (playerCamera == null)
        {
            playerCamera = GetComponent<PlayerController>()?.ViewCamera;

            if (playerCamera == null)
            {
                playerCamera = GetComponentInChildren<Camera>(true);
            }
        }

        if (playerCamera == null)
        {
            return null;
        }

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);

        if (!TryFindInteractionHit(ray, out RaycastHit hit, false))
        {
            return null;
        }

        ControlMachine controlMachine = hit.collider.GetComponentInParent<ControlMachine>();

        if (controlMachine != null)
        {
            return controlMachine.IsPlayerInRange(transform.position) ? controlMachine.InteractName : null;
        }

        EscapeButtonInteraction escapeButton = hit.collider.GetComponentInParent<EscapeButtonInteraction>();

        if (escapeButton != null)
        {
            return escapeButton.IsPlayerInRange(transform.position) ? escapeButton.InteractName : null;
        }

        ControllableWall wall = hit.collider.GetComponentInParent<ControllableWall>();

        if (wall != null)
        {
            return hit.distance <= wallInteractDistance ? wall.FieldInteractName : null;
        }

        if (hit.collider.GetComponentInParent<NetworkItem>() == null)
        {
            return null;
        }

        NetworkItem item = FindAimedItem(ray);

        if (item == null || item.Object == null || !item.Object.IsValid || item.IsPickedUp)
        {
            return null;
        }

        // 가까이 가야만 안내가 뜬다(E 입력과 같은 기준).
        if (!IsItemInRange(item))
        {
            return null;
        }

        ItemData data = ItemDatabase.Instance != null ? ItemDatabase.Instance.GetItemData(item.ItemId) : null;
        if (data == null || string.IsNullOrEmpty(data.itemName))
        {
            return "아이템";
        }

        // 팔 수 있는 물건이면 가치(판매가)를 이름 옆에 보여 준다. 열쇠처럼 값이 없는 물건은 이름만.
        return data.sellPrice > 0
            ? $"{data.itemName}  <color=#FFD54A>{data.sellPrice} G</color>"
            : data.itemName;
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
        if (Object == null || !Object.IsValid || !Object.HasInputAuthority ||
            (_playerData != null && _playerData.IsDead))
        {
            return;
        }

        // 카메라 참조 복구
        if (playerCamera == null)
        {
            playerCamera = GetComponent<PlayerController>()?.ViewCamera;
            if (playerCamera == null) playerCamera = GetComponentInChildren<Camera>(true);
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
            if (!vaultChest.IsPlayerInRange(transform.position))
            {
                Debug.Log("[PlayerInteraction] 금고에서 너무 멀어 상호작용할 수 없습니다.");
                return;
            }

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
            if (!controlMachine.IsPlayerInRange(transform.position))
            {
                Debug.Log(
                    "[PlayerInteraction] 단말기 앞 상자 밖이라 상호작용할 수 없습니다."
                );

                return;
            }

            Debug.Log(
                "[PlayerInteraction] ControlMachine 감지 성공"
            );

            LocalInteracted?.Invoke(hit.point);
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
            if (!escapeButton.IsPlayerInRange(transform.position))
            {
                Debug.Log("[PlayerInteraction] 출발 버튼에서 너무 멀어 상호작용할 수 없습니다.");
                return;
            }

            Debug.Log(
                "[PlayerInteraction] 출발 버튼 발견"
            );

            LocalInteracted?.Invoke(hit.point);
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

            LocalInteracted?.Invoke(hit.point);
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

        if (!IsItemInRange(item))
        {
            Debug.Log("[PlayerInteraction] 아이템에서 너무 멀어 주울 수 없습니다.");
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

        // 1인칭 팔 연출. 획득 판정은 호스트가 하지만, 월드 아이템이 사라지기 전(지금)에 손으로 끌어온다.
        // 호스트가 거절하면 손에 든 모습은 인벤토리 슬롯 상태를 따라 다시 맞춰진다.
        if (inventoryManager.HasEmptySlot())
        {
            LocalItemPickedUp?.Invoke(item.transform, item.ItemId);
        }
        else
        {
            // 가방이 꽉 차도 손은 톡 건드린다
            LocalInteracted?.Invoke(item.transform.position);
        }

        if (Object.HasStateAuthority)
            TryPickupItem(item.Object);
        else
        {
            Debug.Log($"[PlayerInteraction] 호스트에 획득 요청: {item.Object.name}, ItemId={item.ItemId}");
            RPC_RequestPickupItem(item.Object);
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
        out RaycastHit selectedHit,
        bool verbose = true)
    {
        selectedHit = default;

        // 1차: 카메라 정면의 정확한 Raycast
        RaycastHit[] rayHits = Physics.RaycastAll(
            ray,
            interactDistance,
            interactLayerMask,
            QueryTriggerInteraction.Collide
        );

        bool hasDirectHit = TrySelectClosestHit(rayHits, out RaycastHit directHit);
        if (hasDirectHit && IsInteractionCollider(directHit.collider))
        {
            selectedHit = directHit;

            if (verbose)
            {
                Debug.Log(
                    "[PlayerInteraction] Raycast로 대상 감지"
                );
            }

            return true;
        }

        // 바닥/벽을 맞혔어도 그 앞의 작은 아이템을 보조 탐색합니다.
        // 단, 벽 너머까지 탐색 거리를 늘리지는 않습니다.
        float searchDistance = hasDirectHit ? Mathf.Min(interactDistance, directHit.distance) : interactDistance;
        RaycastHit[] sphereHits = Physics.SphereCastAll(
            ray,
            interactRadius,
            searchDistance,
            interactLayerMask,
            QueryTriggerInteraction.Collide
        );

        System.Array.Sort(sphereHits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit candidate in sphereHits)
        {
            if (candidate.collider == null || IsMyCollider(candidate.collider) ||
                !IsInteractionCollider(candidate.collider) || !HasClearInteractionPath(ray.origin, candidate.collider))
                continue;

            selectedHit = candidate;

            if (verbose)
            {
                Debug.Log(
                    "[PlayerInteraction] SphereCast로 대상 감지"
                );
            }

            return true;
        }

        // 진단용으로 원래 맞힌 바닥/벽은 유지합니다.
        if (hasDirectHit)
        {
            selectedHit = directHit;
            return true;
        }
        return false;
    }

    private static bool IsInteractionCollider(Collider collider)
    {
        return collider.GetComponentInParent<NetworkItem>() != null ||
            collider.GetComponentInParent<ControlMachine>() != null ||
            collider.GetComponentInParent<EscapeButtonInteraction>() != null ||
            collider.GetComponentInParent<ControllableWall>() != null;
    }

    private bool HasClearInteractionPath(Vector3 origin, Collider target)
    {
        Vector3 delta = target.ClosestPoint(origin) - origin;
        if (delta.sqrMagnitude < 0.0001f) return true;
        NetworkItem item = target.GetComponentInParent<NetworkItem>();
        RaycastHit[] hits = Physics.RaycastAll(origin, delta.normalized, delta.magnitude,
            interactLayerMask, QueryTriggerInteraction.Ignore);
        foreach (RaycastHit hit in hits)
        {
            if (IsMyCollider(hit.collider) || hit.collider == target) continue;
            // 같은 아이템의 다른 콜라이더는 장애물로 취급하지 않습니다.
            if (item != null && hit.collider.GetComponentInParent<NetworkItem>() == item) continue;
            return false;
        }
        return true;
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
    private void RPC_RequestPickupItem(NetworkObject itemObject)
    {
        TryPickupItem(itemObject);
    }

    private void TryPickupItem(NetworkObject itemObject)
    {
        if (!Object.HasStateAuthority) return;
        if (_playerData == null || inventoryManager == null)
        {
            RPC_PickupFeedback("획득 거절: 플레이어의 PlayerData 또는 InventoryManager가 없습니다.");
            return;
        }
        if (_playerData.IsDead)
        {
            RPC_PickupFeedback("획득 거절: 호스트에서 이 플레이어를 사망 상태로 판정했습니다.");
            return;
        }
        if (itemObject == null || !itemObject.IsValid || itemObject.Runner != Runner)
        {
            RPC_PickupFeedback("획득 거절: 호스트에서 요청한 네트워크 아이템을 찾을 수 없습니다.");
            return;
        }

        NetworkItem item = itemObject.GetComponent<NetworkItem>();
        if (item == null || item.IsPickedUp || !itemObject.HasStateAuthority)
        {
            RPC_PickupFeedback($"획득 거절: {itemObject.name}, NetworkItem={item != null}, 호스트 권한={itemObject.HasStateAuthority}, 선점됨={item != null && item.IsPickedUp}");
            return;
        }

        // 클라이언트가 보낸 좌표/아이템 ID를 믿지 않고 호스트의 월드 상태로 검사합니다.
        // 검사 기준은 클라이언트의 안내·E 입력과 같다(IsItemInRange / FindAimedItem). 기준이 다르면
        // "이름은 떴는데 안 줍히는" 일이 생긴다 — 특히 콜라이더가 보이는 모양과 다르고 벽에 붙은 그림에서.
        Vector3 origin = PickupOrigin();
        float distance = InteractRange.DistanceTo(item, origin);
        float allowed = itemInteractDistance + PickupHostTolerance;
        if (distance > allowed)
        {
            RPC_PickupFeedback($"획득 거절: 거리 초과 ({distance:F2}m / 허용 {allowed:F2}m)");
            return;
        }
        if (!HasClearPickupPath(origin, item))
        {
            RPC_PickupFeedback($"획득 거절: {itemObject.name} 앞에 장애물이 있습니다.");
            return;
        }

        // 사망/빈 슬롯/다른 사람의 선점 여부를 호스트에서 한 번에 확인합니다.
        if (!inventoryManager.TryAddItem(item.ItemId))
        {
            RPC_PickupFeedback("획득 거절: 인벤토리에 추가하지 못했습니다. 빈 슬롯과 아이템 등록을 확인하세요.");
            return;
        }
        RPC_PickupFeedback($"획득 성공: {itemObject.name}, ItemId={item.ItemId}");
        item.IsPickedUp = true;
        Runner.Despawn(itemObject);
    }

    // 호스트는 클라이언트보다 조금 늦은 위치를 본다. 클라이언트가 가까이 갔다고 본 줍기가 지연 때문에 거절되지 않게 여유를 둔다.
    private const float PickupHostTolerance = 1f;

    // 시선 경로의 끝에서 이만큼은 막힘으로 세지 않는다. 벽에 딱 붙은 그림은 벽 표면이 도착점과 같은 자리에 있다.
    private const float PickupPathEndMargin = 0.15f;

    /// <summary>
    /// 줍기 판정의 눈 위치. 호스트에서 다른 플레이어를 검사할 때는 그 플레이어의 카메라가 아직 없을 수 있어 그때 찾아 둔다.
    /// 카메라가 없으면 발 위치를 쓴다.
    /// </summary>
    private Vector3 PickupOrigin()
    {
        if (playerCamera == null)
        {
            playerCamera = GetComponent<PlayerController>()?.ViewCamera;

            if (playerCamera == null)
            {
                playerCamera = GetComponentInChildren<Camera>(true);
            }
        }

        return playerCamera != null ? playerCamera.transform.position : transform.position;
    }

    /// <summary>
    /// 눈에서 아이템의 보이는 모양까지 벽·선반 같은 단단한 물체가 가로막는가.
    /// 클라이언트의 조준 판정(CollectItems)과 같은 규칙이다: 다른 아이템의 콜라이더는 막힘이 아니다
    /// (보이는 모양보다 큰 콜라이더가 많고, 옆 아이템 위에 놓인 물건도 줍혀야 한다).
    /// 도착점 근처(PickupPathEndMargin)는 보지 않는다.
    /// </summary>
    private bool HasClearPickupPath(Vector3 origin, NetworkItem item)
    {
        Vector3 target = VisualBounds(item).ClosestPoint(origin);
        Vector3 delta = target - origin;
        float length = delta.magnitude - PickupPathEndMargin;

        if (length <= 0f)
        {
            return true;
        }

        RaycastHit[] hits = Physics.RaycastAll(origin, delta.normalized, length, interactLayerMask, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hits.Length; i++)
        {
            Collider col = hits[i].collider;

            if (col == null || IsMyCollider(col) || col.GetComponentInParent<NetworkItem>() != null)
            {
                continue;
            }

            return false;
        }

        return true;
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    private void RPC_PickupFeedback(string message)
    {
        Debug.Log($"[PlayerInteraction] {message}");
    }
}
