using Fusion;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// ControllableWall
///
/// 담당:
/// - 실제 맵에 있는 격벽의 해금 상태 관리
/// - 실제 격벽 열림/닫힘 처리
/// - 열린 상태에서는 실제 벽 Renderer/Collider를 끔
/// - 닫힌 상태에서는 실제 벽 Renderer/Collider를 켬
/// - CCTV/미니맵용 격벽 표시만 빨강/파랑으로 변경
///
/// 특징:
/// - 상태(IsUnlocked/IsOpen)는 [Networked] 값으로 StateAuthority가 권위를 가진다
/// - 비권위 피어의 호출은 RPC로 StateAuthority에 요청되고, 실제 반영은 StateAuthority에서만 일어난다
///
/// 사용 위치:
/// - 실제 맵의 Wall_01, Wall_02 같은 격벽 오브젝트에 붙임
/// </summary>
public class ControllableWall : NetworkBehaviour
{
    [Header("Actual Wall")]
    [Tooltip("실제 맵에서 보이는 벽 Renderer")]
    [SerializeField] private Renderer[] actualWallRenderers;

    [Tooltip("실제 플레이어를 막는 Collider")]
    [SerializeField] private Collider[] actualWallColliders;

    [Header("Navigation")]
    [Tooltip("닫힘 상태에서만 켜지는 NavMeshObstacle(Carve). 비워 두면 자기/자식에서 자동 탐색한다. " +
             "이 격벽은 NavMesh 굽기에서 제외(NavMeshModifier의 Ignore From Build)해야 열렸을 때 적이 지나간다")]
    [SerializeField] private NavMeshObstacle navMeshObstacle;

    [Header("Minimap / CCTV Visual")]
    [Tooltip("CCTV/미니맵에 보이는 격벽 표시용 Renderer")]
    [SerializeField] private Renderer minimapWallRenderer;

    // 세 상태를 색(빨강·파랑·초록)과 모양(꽉 찬 막대 / 얇은 막대 / 자물쇠)으로 이중 구분한다.
    // 해금 안 된 격벽은 열 수 없으므로 실제 상태는 이 셋뿐이다(UI_SPEC 3.2 참고).
    [Header("Minimap Colors")]
    [Tooltip("잠김(해금 안 됨, 닫힘) = 빨강 #E5342B")]
    [SerializeField] private Color minimapClosedColor = new Color(0.898f, 0.204f, 0.169f, 1f);

    [Tooltip("열림 = 초록 #2ED47A. 막대가 얇아져 통로가 뚫린 모양으로 보인다")]
    [SerializeField] private Color minimapOpenColor = new Color(0.180f, 0.831f, 0.478f, 1f);

    [Tooltip("해금됐지만 닫혀 있을 때 = 파랑 #2E7DE0 (대본의 \"푸른색이 됐습니다\")")]
    [SerializeField] private Color minimapUnlockedColor = new Color(0.180f, 0.490f, 0.878f, 1f);

    [Header("Minimap Shape")]
    [Tooltip("열림일 때 표시용 막대의 두께 비율. 작을수록 가늘다. 1이면 두께 변화 없음")]
    [SerializeField, Range(0.1f, 1f)] private float openThicknessRatio = 0.3f;

    [Tooltip("잠김일 때 격벽 위에 띄울 자물쇠 아이콘(위에서 본 모양). 비우면 표시하지 않는다")]
    [SerializeField] private Sprite lockIcon;

    [Tooltip("자물쇠 아이콘의 월드 크기(m)")]
    [SerializeField, Min(0.2f)] private float lockIconSize = 1.4f;

    // 월드(3D)에서도 육안으로 상태를 구분할 수 있게 한다. 실제 벽은 열리면 사라지므로 표시는 벽과 따로 둔다.
    [Header("World Indicator (현장 육안 구분)")]
    [Tooltip("바닥 문턱 띠에 쓰는 단색 재질(URP Unlit). 비우면 띠는 만들지 않고 벽 틴트만 적용한다")]
    [SerializeField] private Material indicatorMaterial;

    [Tooltip("닫힌 실제 벽에 상태색을 섞는 비율. 0 이면 벽 색은 그대로")]
    [SerializeField, Range(0f, 0.8f)] private float worldTintStrength = 0.35f;

    [Header("Field Open (현장 상호작용)")]
    [Tooltip("끄면 현장에서 상호작용 키로 격벽을 열 수 없다. 본부(OpenWall/CloseWall)에서만 열고 닫으며, 한 번 열린 격벽은 본부에서 닫기 전까지 열려 있다")]
    [SerializeField] private bool allowFieldOpen = true;


    [Tooltip("현장에서 상호작용 키로 연 뒤 자동으로 닫힐 때까지의 시간. GAME_DESIGN 격벽: Open → 5초 후 Unlocked 회귀")]
    [SerializeField, Min(0.5f)] private float fieldOpenSeconds = 5f;

    [Tooltip("닫힐 차례에 문 자리에 플레이어가 있으면 이만큼 미룬다(끼임 방지)")]
    [SerializeField, Min(0.1f)] private float closeRetrySeconds = 0.5f;

    [Networked, OnChangedRender(nameof(OnWallStateChanged))]
    public NetworkBool IsUnlocked { get; private set; }

    [Networked, OnChangedRender(nameof(OnWallStateChanged))]
    public NetworkBool IsOpen { get; private set; }

    /// <summary>현장에서 연 경우 자동으로 닫힐 시각. 본부에서 연 경우(수동)는 None.</summary>
    [Networked] private TickTimer AutoCloseTimer { get; set; }

    /// <summary>상태가 바뀔 때마다(원격 포함) 호출된다. UI가 구독해 새로고침한다.</summary>
    public event System.Action OnStateChanged;

    // 닫혀 있을 때의 문 자리(콜라이더 경계). 열리면 콜라이더가 꺼지므로 미리 잡아 둔다.
    private Bounds _doorwayBounds;
    private bool _hasDoorwayBounds;

    private void Awake()
    {
        // Inspector에 직접 연결하지 않았을 때를 위한 자동 탐색
        if (actualWallRenderers == null || actualWallRenderers.Length == 0)
        {
            actualWallRenderers = GetComponentsInChildren<Renderer>();
        }

        if (actualWallColliders == null || actualWallColliders.Length == 0)
        {
            actualWallColliders = GetComponentsInChildren<Collider>();
        }

        if (navMeshObstacle == null)
        {
            navMeshObstacle = GetComponentInChildren<NavMeshObstacle>();
        }

        CacheDoorwayBounds();
    }

    private void CacheDoorwayBounds()
    {
        if (actualWallColliders == null)
        {
            return;
        }

        for (int i = 0; i < actualWallColliders.Length; i++)
        {
            Collider wallCollider = actualWallColliders[i];

            if (wallCollider == null || !wallCollider.enabled || wallCollider.isTrigger)
            {
                continue;
            }

            if (!_hasDoorwayBounds)
            {
                _doorwayBounds = wallCollider.bounds;
                _hasDoorwayBounds = true;
            }
            else
            {
                _doorwayBounds.Encapsulate(wallCollider.bounds);
            }
        }

        // 문 앞뒤로 서 있는 플레이어도 "문 자리"로 본다.
        _doorwayBounds.Expand(0.6f);
    }

    /// <summary>
    /// 현장에서 E 를 눌렀을 때 열 수 있는 상태면 안내에 띄울 이름, 아니면 null.
    /// 현장 개방이 꺼져 있거나, 잠겼거나, 이미 열려 있으면 안내를 띄우지 않는다.
    /// </summary>
    public string FieldInteractName
    {
        get
        {
            if (!allowFieldOpen || Object == null || !Object.IsValid || !IsUnlocked || IsOpen)
            {
                return null;
            }

            return "격벽 열기";
        }
    }

    /// <summary>
    /// 현장(침투조) 상호작용. 해금된(푸른) 격벽만 열리고, <see cref="fieldOpenSeconds"/> 뒤 자동으로 닫힌다.
    /// 잠긴(붉은) 격벽이면 false — 본부 해킹으로 먼저 해금해야 한다.
    /// </summary>
    public bool TryFieldOpen()
    {
        if (!allowFieldOpen)
        {
            return false;
        }

        if (!IsUnlocked)
        {
            Debug.Log($"[ControllableWall] {gameObject.name} 잠김 — 접근 거부. 본부에서 해킹으로 해금해야 합니다.");
            return false;
        }

        if (IsOpen)
        {
            return false;
        }

        if (Object.HasStateAuthority)
        {
            DoFieldOpen();
        }
        else
        {
            RPC_RequestFieldOpen();
        }

        return true;
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestFieldOpen()
    {
        DoFieldOpen();
    }

    private void DoFieldOpen()
    {
        if (!IsUnlocked || IsOpen)
        {
            return;
        }

        IsOpen = true;
        AutoCloseTimer = TickTimer.CreateFromSeconds(Runner, fieldOpenSeconds);
        Debug.Log($"[ControllableWall] {gameObject.name} 현장 개방 — {fieldOpenSeconds}초 뒤 자동으로 닫힘");
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority || !IsOpen || !AutoCloseTimer.Expired(Runner))
        {
            return;
        }

        if (IsDoorwayOccupied())
        {
            AutoCloseTimer = TickTimer.CreateFromSeconds(Runner, closeRetrySeconds);
            return;
        }

        AutoCloseTimer = TickTimer.None;
        IsOpen = false;
        Debug.Log($"[ControllableWall] {gameObject.name} 자동으로 닫힘(해금 유지)");
    }

    private bool IsDoorwayOccupied()
    {
        if (!_hasDoorwayBounds)
        {
            return false;
        }

        for (int i = 0; i < PlayerTarget.AllTargets.Count; i++)
        {
            PlayerTarget target = PlayerTarget.AllTargets[i];

            if (target != null && _doorwayBounds.Contains(target.transform.position))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Fusion에서 NetworkObject가 Spawn된 뒤 호출.
    /// [Networked] 값은 이 시점에야 유효하므로 여기서 초기 상태를 반영한다.
    /// </summary>
    public override void Spawned()
    {
        ApplyWallState();
    }

    /// <summary>
    /// 격벽을 해금 상태로 변경
    /// </summary>
    public void Unlock()
    {
        if (Object.HasStateAuthority)
        {
            DoUnlock();
        }
        else
        {
            RPC_RequestUnlock();
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestUnlock()
    {
        DoUnlock();
    }

    private void DoUnlock()
    {
        IsUnlocked = true;
        Debug.Log($"[ControllableWall] {gameObject.name} 해금 완료");
    }

    /// <summary>
    /// 격벽을 엽니다.
    /// 실제 벽은 사라지고, 미니맵/CCTV 표시는 파란색
    /// </summary>
    public void OpenWall()
    {
        RequestSetOpen(true);
    }

    /// <summary>
    /// 격벽을 닫습니다.
    /// 실제 벽은 다시 보이고, 미니맵/CCTV 표시는 빨간색
    /// </summary>
    public void CloseWall()
    {
        RequestSetOpen(false);
    }

    private void RequestSetOpen(bool open)
    {
        if (Object.HasStateAuthority)
        {
            DoSetOpen(open);
        }
        else
        {
            RPC_RequestSetOpen(open);
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestSetOpen(NetworkBool open)
    {
        DoSetOpen(open);
    }

    private void DoSetOpen(bool open)
    {
        // 본부에서 직접 열고 닫는 것은 수동 조작이다 — 현장 개방의 자동 닫힘 예약을 지운다.
        AutoCloseTimer = TickTimer.None;
        IsOpen = open;
        Debug.Log($"[ControllableWall] {gameObject.name} {(open ? "열림" : "닫힘")}");
    }

    /// <summary>
    /// 해금된 격벽의 열림/닫힘을 전환
    /// </summary>
    public void ToggleWall()
    {
        if (!IsUnlocked)
        {
            Debug.Log($"[ControllableWall] {gameObject.name} 아직 해금되지 않음");
            return;
        }

        RequestSetOpen(!IsOpen);
    }

    /// <summary>
    /// [Networked] 상태가 바뀔 때마다(로컬·원격 무관) 호출된다.
    /// </summary>
    private void OnWallStateChanged()
    {
        ApplyWallState();
        OnStateChanged?.Invoke();
    }

    /// <summary>
    /// 현재 상태를 실제 벽과 미니맵/CCTV 표시용 벽에 반영
    /// </summary>
    public void RestoreTutorialCheckpoint(bool unlocked, bool open)
    {
        if (Pawntom.TutorialBridge.TutorialDirector.Active == null || !Object.HasStateAuthority) return;
        IsUnlocked = unlocked;
        IsOpen = open;
        AutoCloseTimer = TickTimer.None;
        ApplyWallState();
        OnStateChanged?.Invoke();
    }

    private void ApplyWallState()
    {
        // 실제 벽은 색을 바꾸지 않고, 보이기/숨기기만 처리
        bool shouldShowActualWall = !IsOpen;

        if (actualWallRenderers != null)
        {
            for (int i = 0; i < actualWallRenderers.Length; i++)
            {
                if (actualWallRenderers[i] == null)
                {
                    continue;
                }

                actualWallRenderers[i].enabled = shouldShowActualWall;
            }
        }

        // 실제 벽 Collider도 열리면 OFF, 닫히면 ON
        if (actualWallColliders != null)
        {
            for (int i = 0; i < actualWallColliders.Length; i++)
            {
                if (actualWallColliders[i] == null)
                {
                    continue;
                }

                actualWallColliders[i].enabled = !IsOpen;
            }
        }

        // 열림/닫힘은 렌더러와 콜라이더만 바꾸고 굽힌 NavMesh는 그대로다. 닫힌 채로 구우면
        // 열어도 적의 경로가 막혀 있으므로, 굽기에서는 빼고 닫힘 동안만 장애물로 판 자리를 막는다.
        if (navMeshObstacle != null)
        {
            navMeshObstacle.enabled = !IsOpen;
        }

        // 미니맵/CCTV 표시용 벽만 색상·모양 변경
        if (minimapWallRenderer != null)
        {
            // 잠김 = 빨강, 해금(닫힘) = 파랑, 열림 = 초록 (UI_SPEC 색 의미 규칙)
            minimapWallRenderer.material.color = !IsUnlocked
                ? minimapClosedColor
                : (IsOpen ? minimapOpenColor : minimapUnlockedColor);

            ApplyMinimapShape();
        }

        ApplyWorldIndicator(IsUnlocked, IsOpen);
    }

    private Color StateColor(bool unlocked, bool open)
    {
        return !unlocked ? minimapClosedColor : (open ? minimapOpenColor : minimapUnlockedColor);
    }

    private MaterialPropertyBlock _worldBlock;
    private Transform _indicatorRoot;
    private Renderer[] _indicatorRenderers;

    /// <summary>
    /// 월드에서 상태를 구분한다: 닫힌 벽에 상태색 틴트 + 바닥 문턱 띠(열려서 벽이 사라져도 남는다).
    /// 색은 미니맵·CCTV 버튼과 같다(잠김 빨강 / 해금 파랑 / 열림 초록).
    /// </summary>
    private void ApplyWorldIndicator(bool unlocked, bool open)
    {
        Color state = StateColor(unlocked, open);

        if (_worldBlock == null)
        {
            _worldBlock = new MaterialPropertyBlock();
        }

        // 1) 닫힌 실제 벽에 상태색을 섞는다. 원래 색(재질)은 건드리지 않고 인스턴스별 값만 바꾼다.
        if (actualWallRenderers != null && worldTintStrength > 0f)
        {
            Color tint = Color.Lerp(Color.white, state, worldTintStrength);

            for (int i = 0; i < actualWallRenderers.Length; i++)
            {
                Renderer wallRenderer = actualWallRenderers[i];

                if (wallRenderer == null || wallRenderer.sharedMaterial == null || !wallRenderer.sharedMaterial.HasProperty("_BaseColor"))
                {
                    continue;
                }

                wallRenderer.GetPropertyBlock(_worldBlock);
                _worldBlock.SetColor("_BaseColor", tint);
                wallRenderer.SetPropertyBlock(_worldBlock);
            }
        }

        // 2) 문 위 램프와 바닥 문턱 띠.
        EnsureIndicators();

        if (_indicatorRenderers == null)
        {
            return;
        }

        for (int i = 0; i < _indicatorRenderers.Length; i++)
        {
            _worldBlock.Clear();
            _worldBlock.SetColor("_BaseColor", state);
            _indicatorRenderers[i].SetPropertyBlock(_worldBlock);
        }
    }

    // 벽의 자식으로 두지 않는다 — Awake 의 자식 Renderer 자동 탐색에 섞이면 벽이 열릴 때 같이 사라진다.
    private void EnsureIndicators()
    {
        if (_indicatorRoot != null || indicatorMaterial == null)
        {
            return;
        }

        Vector3 lossy = transform.lossyScale;
        bool alongZ = lossy.z >= lossy.x;
        float length = alongZ ? lossy.z : lossy.x;
        float thickness = alongZ ? lossy.x : lossy.z;

        GameObject root = new GameObject("GateIndicators_" + gameObject.name);
        root.transform.SetPositionAndRotation(transform.position, transform.rotation);
        _indicatorRoot = root.transform;

        // 바닥 문턱 띠: 문이 열려 벽이 사라져도 어디가 격벽인지, 어떤 상태인지 남는다.
        // (문 위 램프도 시도했지만 문틀 윗부분에 가려서 보이지 않아 뺐다.) 벽보다 조금만 넓게 해서 양면에서 보이게 한다.
        Vector3 stripSize = alongZ
            ? new Vector3(thickness + 0.15f, 0.04f, length * 0.96f)
            : new Vector3(length * 0.96f, 0.04f, thickness + 0.15f);
        Renderer strip = CreateIndicatorBar("Threshold", new Vector3(0f, -lossy.y * 0.5f + 0.03f, 0f), stripSize);

        _indicatorRenderers = new[] { strip };
    }

    private Renderer CreateIndicatorBar(string barName, Vector3 localPosition, Vector3 size)
    {
        GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bar.name = barName;

        Collider barCollider = bar.GetComponent<Collider>();

        if (barCollider != null)
        {
            // 표시용이라 충돌·길찾기에 관여하지 않는다. (에디터 모드 검증에서도 호출되므로 Play 여부로 가른다)
            if (Application.isPlaying)
            {
                Destroy(barCollider);
            }
            else
            {
                DestroyImmediate(barCollider);
            }
        }

        bar.transform.SetParent(_indicatorRoot, false);
        bar.transform.localPosition = localPosition;
        bar.transform.localScale = size;

        Renderer barRenderer = bar.GetComponent<Renderer>();
        barRenderer.sharedMaterial = indicatorMaterial;
        barRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        barRenderer.receiveShadows = false;

        return barRenderer;
    }

    private Vector3 _minimapBaseScale;
    private bool _minimapScaleCached;
    private Transform _lockIconTransform;

    /// <summary>
    /// 색만으로 구분하지 않도록 모양도 바꾼다. 열림 = 얇은 막대(통로가 뚫린 느낌), 잠김 = 자물쇠 아이콘.
    /// </summary>
    private void ApplyMinimapShape()
    {
        Transform bar = minimapWallRenderer.transform;

        if (!_minimapScaleCached)
        {
            _minimapBaseScale = bar.localScale;
            _minimapScaleCached = true;
        }

        // 두께 축 = 수평 두 축 중 작은 쪽. 길이(문 폭)는 그대로 둔다.
        Vector3 scale = _minimapBaseScale;

        if (IsUnlocked && IsOpen)
        {
            if (scale.x <= scale.z)
            {
                scale.x *= openThicknessRatio;
            }
            else
            {
                scale.z *= openThicknessRatio;
            }
        }

        bar.localScale = scale;

        UpdateLockIcon(!IsUnlocked);
    }

    private void UpdateLockIcon(bool show)
    {
        if (lockIcon == null)
        {
            return;
        }

        if (_lockIconTransform == null)
        {
            if (!show)
            {
                return;
            }

            // 표시용 막대의 부모는 비균일 스케일이라 아이콘이 찌그러진다. 씬 루트에 따로 둔다.
            // 격벽의 자식으로도 두지 않는다(Awake 의 자식 Renderer 자동 탐색에 섞이지 않게).
            GameObject icon = new GameObject("GateLockIcon_" + gameObject.name);
            icon.layer = minimapWallRenderer.gameObject.layer;

            SpriteRenderer spriteRenderer = icon.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = lockIcon;
            spriteRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            spriteRenderer.receiveShadows = false;
            spriteRenderer.sortingOrder = 5;

            float spriteWidth = Mathf.Max(0.01f, lockIcon.bounds.size.x);
            icon.transform.localScale = Vector3.one * (lockIconSize / spriteWidth);

            _lockIconTransform = icon.transform;
        }

        if (show)
        {
            Bounds bounds = minimapWallRenderer.bounds;
            _lockIconTransform.position = new Vector3(bounds.center.x, bounds.max.y + 0.05f, bounds.center.z);
            _lockIconTransform.rotation = Quaternion.Euler(90f, 0f, 0f); // 위에서 내려다보는 미니맵 카메라를 향한다
        }

        _lockIconTransform.gameObject.SetActive(show);
    }

    private void OnDestroy()
    {
        if (_lockIconTransform != null)
        {
            Destroy(_lockIconTransform.gameObject);
        }

        if (_indicatorRoot != null)
        {
            Destroy(_indicatorRoot.gameObject);
        }
    }
}
