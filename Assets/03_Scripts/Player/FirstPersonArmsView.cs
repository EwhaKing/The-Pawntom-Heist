using System.Collections.Generic;
using Fusion;
using Pawntom.Player.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 1인칭 오른팔(내 화면 전용). 화면 오른쪽 아래(핫바와 무전기 사이)에 팔을 두고
/// 대기·걷기·달리기·점프·착지·시점 회전에 맞춰 흔들고, E 상호작용 때 "톡" 뻗는다.
/// 리썰컴퍼니처럼 핫바에서 고른 아이템을 손에 들고, 주울 때는 아이템이 손으로 끌려오고 버릴 때는 손에서 바닥으로 내려놓는다.
/// <para>
/// 렌더링: 팔·손에 든 물건은 전용 레이어에 두고 오버레이 카메라로 그린다 — 벽에 붙어도 팔이 벽을 뚫지 않고,
/// 본 카메라의 near clip(0.3m)에 잘리지 않는다. 동작 계산은 <see cref="FirstPersonArmMotion"/>(순수 C#).
/// </para>
/// <para>버리기는 손이 바닥 쪽으로 다 뻗은 순간에 실제로 보낸다(<see cref="InputManager.RequestDrop"/>) — 손에서 놓는 순간과 월드에 생기는 순간을 맞춘다.</para>
/// </summary>
[DisallowMultipleComponent]
public class FirstPersonArmsView : MonoBehaviour
{
    [Header("팔")]
    [Tooltip("오른팔 모델(어깨가 원점, 팔이 +Z 로 뻗은 메시)")]
    [SerializeField] private GameObject armPrefab;

    [Tooltip("팔 전용 레이어 이름. 프로젝트에 없으면 아래 번호를 쓴다")]
    [SerializeField] private string armLayerName = "FirstPersonArms";

    [SerializeField, Range(8, 31)] private int armLayerFallback = 29;

    [Header("배치 (카메라 기준, m·도)")]
    [Tooltip("어깨 위치. x = 오른쪽, y = 위, z = 앞")]
    [SerializeField] private Vector3 shoulderPosition = new Vector3(0.18f, -0.25f, 0.05f);

    [Tooltip("팔 각도(도). x = 위아래(음수 = 손끝이 위로), y = 좌우(음수 = 화면 가운데 쪽), z = 손목 비틀기(양수 = 손등이 화면 쪽으로)")]
    [SerializeField] private Vector3 armRotation = new Vector3(-22f, -16f, -5f);

    [Tooltip("팔 크기 배율(모델 원본 크기 기준)")]
    [SerializeField, Min(0.1f)] private float armScale = 1.4f;

    [Tooltip("팔을 그리는 오버레이 카메라의 시야각")]
    [SerializeField, Range(30f, 90f)] private float armsFieldOfView = 55f;

    [Header("손에 든 물건")]
    [Tooltip("손바닥 위치(팔 모델 기준, m)")]
    [SerializeField] private Vector3 holdPoint = new Vector3(0f, 0.03f, 0.25f);

    [Tooltip("손에 든 물건 각도(도)")]
    [SerializeField] private Vector3 holdRotation = new Vector3(0f, 0f, 0f);

    [Tooltip("손에 든 물건의 가장 긴 변 길이(m, 팔 크기 배율 적용 전)")]
    [SerializeField, Min(0.02f)] private float holdSize = 0.065f;

    [Header("줍기·버리기 연출")]
    [Tooltip("주울 때 아이템이 손까지 날아오는 시간(초)")]
    [SerializeField, Min(0.02f)] private float pickupFlySeconds = 0.16f;

    [Tooltip("버릴 때 손에서 바닥 자리까지 내려가는 시간(초)")]
    [SerializeField, Min(0.02f)] private float dropFlySeconds = 0.2f;

    [Header("동작")]
    [SerializeField] private FirstPersonArmSettings motion = new FirstPersonArmSettings();

    private NetworkObject _networkObject;
    private NetworkCharacterController _controller;
    private PlayerController _player;
    private PlayerData _playerData;
    private InventoryManager _inventory;

    private bool _built;
    private int _layer;
    private Camera _mainCamera;
    private Camera _overlayCamera;
    private Transform _rig;
    private Transform _pivot;
    private Transform _arm;
    private Transform _hold;
    private FirstPersonArmMotion _motion;

    private float _lastYaw;
    private float _lastPitch;
    private bool _hasLastLook;

    // 손에 든 물건
    private int _heldItemId;
    private GameObject _heldVisual;
    private int _pendingHeldId = -1;
    private float _suppressHeldUntil;
    private int _expectPickupId;
    private readonly Dictionary<int, VisualTemplate> _templates = new Dictionary<int, VisualTemplate>();

    // 날아가는 물건(줍기·버리기)
    private Flight _flight;

    // 셰이더 미리 준비: 처음 손에 들 때 재질이 늦게 입혀지는 것(에디터의 비동기 셰이더 컴파일 → 임시 하늘색 셰이더,
    // 빌드에서는 그 순간 끊김)을 막는다. 팔 카메라는 그림자 없는 별도 조합이라 월드에서 이미 그린 아이템도 처음엔 새로 컴파일된다.
    private bool _prewarmed;
    private int _prewarmFramesLeft;
    private readonly List<GameObject> _prewarmObjects = new List<GameObject>();

    private sealed class VisualTemplate
    {
        /// <summary>보이는 모양만 복제한 틀. 원점 = 원본 프리팹 루트, 루트 배율은 뺀 크기.</summary>
        public GameObject Prefab;

        /// <summary>틀 기준 경계(루트 배율 뺀 크기).</summary>
        public Bounds LocalBounds;

        /// <summary>원본 프리팹 루트 배율. 월드에 놓일 때 크기.</summary>
        public Vector3 RootScale;
    }

    private sealed class Flight
    {
        public GameObject Visual;
        public Vector3 FromPosition;
        public Quaternion FromRotation;
        public Vector3 FromScale;
        public bool ToHand;
        public Vector3 ToPosition;
        public Quaternion ToRotation;
        public Vector3 ToScale;
        public float Time;
        public float Duration;
        public bool RequestDropOnLand;
        public float LingerAfterLand;
    }

    private void Awake()
    {
        _networkObject = GetComponent<NetworkObject>();
        _controller = GetComponent<NetworkCharacterController>();
        _player = GetComponent<PlayerController>();
        _playerData = GetComponent<PlayerData>();
        _inventory = GetComponent<InventoryManager>();
        _motion = new FirstPersonArmMotion(motion);
    }

    private void OnDestroy()
    {
        PlayerInteraction.LocalInteracted -= HandleInteracted;
        PlayerInteraction.LocalItemPickedUp -= HandleItemPickedUp;

        if (InputManager.Instance != null)
        {
            InputManager.Instance.DropPressed -= HandleDropPressed;
            InputManager.Instance.DeferDropToArms = false;
        }

        if (_mainCamera != null && _overlayCamera != null)
        {
            UniversalAdditionalCameraData data = _mainCamera.GetUniversalAdditionalCameraData();

            if (data != null)
            {
                data.cameraStack.Remove(_overlayCamera);
            }
        }

        if (_flight != null && _flight.Visual != null)
        {
            Destroy(_flight.Visual);
        }
    }

    private void LateUpdate()
    {
        if (!_built)
        {
            TryBuild();

            if (!_built)
            {
                return;
            }
        }

        bool dead = _playerData != null && _playerData.Object != null && _playerData.Object.IsValid && _playerData.IsDead;

        if (_rig.gameObject.activeSelf == dead)
        {
            _rig.gameObject.SetActive(!dead);
        }

        if (dead)
        {
            return;
        }

        float dt = Time.deltaTime;
        _motion.Tick(BuildInput(dt));
        ApplyPose();
        UpdateHeldItem();
        UpdateFlight(dt);
        UpdatePrewarm();
    }

    /// <summary>
    /// 아이템 데이터가 준비되면 모든 아이템을 아주 작게(1/1000) 두 카메라 앞에 몇 프레임 그려 셰이더 조합을 미리 만든다.
    /// 화면에는 점보다 작아 보이지 않는다.
    /// </summary>
    private void UpdatePrewarm()
    {
        if (!_prewarmed)
        {
            if (ItemDatabase.Instance == null || ItemDatabase.Instance.items == null)
            {
                return;
            }

            _prewarmed = true;

            foreach (ItemData data in ItemDatabase.Instance.items)
            {
                VisualTemplate template = data != null ? GetTemplate(data.itemId) : null;

                if (template == null)
                {
                    continue;
                }

                // 팔 카메라(손에 든 모습)
                GameObject held = Instantiate(template.Prefab, _hold);
                held.transform.localPosition = Vector3.zero;
                held.transform.localScale = Vector3.one * 0.001f;
                held.SetActive(true);
                PrepareRenderers(held);
                _prewarmObjects.Add(held);

                // 본 카메라(줍기·버리기 때 날아가는 모습)
                GameObject world = Instantiate(template.Prefab, _mainCamera.transform.position + _mainCamera.transform.forward * 1.5f, Quaternion.identity);
                world.transform.localScale = Vector3.one * 0.001f;
                world.SetActive(true);
                _prewarmObjects.Add(world);
            }

            _prewarmFramesLeft = 10;
            return;
        }

        if (_prewarmFramesLeft <= 0)
        {
            return;
        }

        if (--_prewarmFramesLeft == 0)
        {
            for (int i = 0; i < _prewarmObjects.Count; i++)
            {
                if (_prewarmObjects[i] != null)
                {
                    Destroy(_prewarmObjects[i]);
                }
            }

            _prewarmObjects.Clear();
        }
    }

    // ───────────── 준비 ─────────────

    private void TryBuild()
    {
        if (armPrefab == null || _networkObject == null || !_networkObject.IsValid || !_networkObject.HasInputAuthority)
        {
            return;
        }

        _mainCamera = _player != null ? GetComponentInChildren<Camera>(true) : Camera.main;

        if (_mainCamera == null)
        {
            return;
        }

        int named = LayerMask.NameToLayer(armLayerName);
        _layer = named >= 0 ? named : armLayerFallback;

        // 카메라는 플레이어 스케일(0.05)을 물려받는다. 그 아래에서 미터 단위로 다루도록 크기를 되돌린다.
        _rig = new GameObject("FirstPersonArms").transform;
        _rig.SetParent(_mainCamera.transform, false);
        Vector3 lossy = _mainCamera.transform.lossyScale;
        _rig.localScale = new Vector3(1f / Mathf.Max(0.0001f, lossy.x), 1f / Mathf.Max(0.0001f, lossy.y), 1f / Mathf.Max(0.0001f, lossy.z));

        _pivot = new GameObject("ShoulderPivot").transform;
        _pivot.SetParent(_rig, false);

        // 팔 크기는 그릇이 맡는다. 모델 루트의 자체 변환(FBX 단위 변환: 배율 100·회전)은 그대로 둬야 한다.
        _arm = new GameObject("Arm").transform;
        _arm.SetParent(_pivot, false);
        _arm.localScale = Vector3.one * armScale;
        Instantiate(armPrefab, _arm);

        _hold = new GameObject("HoldPoint").transform;
        _hold.SetParent(_arm, false);
        _hold.localPosition = holdPoint;
        _hold.localRotation = Quaternion.Euler(holdRotation);

        PrepareRenderers(_rig.gameObject);
        BuildOverlayCamera();
        HideLegacyHands();

        PlayerInteraction.LocalInteracted += HandleInteracted;
        PlayerInteraction.LocalItemPickedUp += HandleItemPickedUp;

        if (InputManager.Instance != null)
        {
            InputManager.Instance.DeferDropToArms = true;
            InputManager.Instance.DropPressed += HandleDropPressed;
        }

        _built = true;
        ApplyPose();
    }

    private void BuildOverlayCamera()
    {
        GameObject cameraObject = new GameObject("FirstPersonArmsCamera");
        cameraObject.transform.SetParent(_mainCamera.transform, false);

        _overlayCamera = cameraObject.AddComponent<Camera>();
        _overlayCamera.cullingMask = 1 << _layer;
        _overlayCamera.fieldOfView = armsFieldOfView;
        _overlayCamera.nearClipPlane = 0.01f;
        _overlayCamera.farClipPlane = 5f;

        UniversalAdditionalCameraData overlayData = _overlayCamera.GetUniversalAdditionalCameraData();
        overlayData.renderType = CameraRenderType.Overlay;
        overlayData.renderShadows = false;

        UniversalAdditionalCameraData mainData = _mainCamera.GetUniversalAdditionalCameraData();

        if (mainData != null && !mainData.cameraStack.Contains(_overlayCamera))
        {
            mainData.cameraStack.Add(_overlayCamera);
        }

        // 본 카메라는 팔 레이어를 그리지 않는다(두 번 그려지거나 벽 뒤에서 비치지 않게).
        _mainCamera.cullingMask &= ~(1 << _layer);
    }

    /// <summary>예전 임시 1인칭 손(캡슐 두 개, HandBob)을 내 화면에서 숨긴다.</summary>
    private void HideLegacyHands()
    {
        HandBob legacy = _mainCamera.GetComponentInChildren<HandBob>(true);

        if (legacy != null)
        {
            legacy.gameObject.SetActive(false);
        }
    }

    private void PrepareRenderers(GameObject root)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            t.gameObject.layer = _layer;
        }

        foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
        {
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
        }
    }

    // ───────────── 동작 ─────────────

    private ArmMotionInput BuildInput(float dt)
    {
        ArmMotionInput input = new ArmMotionInput { DeltaTime = dt, Grounded = true };

        if (_controller != null && _controller.Object != null && _controller.Object.IsValid)
        {
            Vector3 velocity = _controller.Velocity;
            float horizontal = new Vector2(velocity.x, velocity.z).magnitude;
            float walk = _player != null ? Mathf.Max(0.01f, _player.WalkSpeed) : 1f;
            float sprint = _player != null ? Mathf.Max(walk + 0.01f, _player.SprintSpeed) : 2f;
            input.Locomotion = horizontal <= walk ? horizontal / walk : Mathf.Min(2f, 1f + (horizontal - walk) / (sprint - walk));
            input.Grounded = _controller.Grounded;
            input.VerticalSpeed = velocity.y;
        }

        Transform cam = _mainCamera.transform;
        float yaw = cam.eulerAngles.y;
        float pitch = -cam.localEulerAngles.x;

        if (_hasLastLook)
        {
            input.LookDelta = new Vector2(Mathf.DeltaAngle(_lastYaw, yaw), Mathf.DeltaAngle(_lastPitch, pitch));
        }

        _lastYaw = yaw;
        _lastPitch = pitch;
        _hasLastLook = true;
        return input;
    }

    private void ApplyPose()
    {
        _pivot.localPosition = shoulderPosition + _motion.PositionOffset;
        _pivot.localRotation = Quaternion.Euler(armRotation) * Quaternion.Euler(_motion.RotationOffset);
    }

    // ───────────── 상호작용 ─────────────

    private void HandleInteracted(Vector3 point)
    {
        _motion.Reach(FirstPersonArmMotion.ReachKind.Touch);
    }

    /// <summary>주웠다: 손을 톡 뻗고, 아이템이 원래 자리에서 손으로 날아온다. 도착할 때까지 손에 든 모습은 감춘다.</summary>
    private void HandleItemPickedUp(Transform item, int itemId)
    {
        _motion.Reach(FirstPersonArmMotion.ReachKind.Touch);
        _expectPickupId = itemId;

        VisualTemplate template = GetTemplate(itemId);

        if (template == null || item == null)
        {
            return;
        }

        ClearFlight();
        GameObject visual = Instantiate(template.Prefab);
        visual.SetActive(true);

        // 날아오는 동안은 월드 레이어로 그린다(벽 앞뒤가 자연스럽게 가려지도록).
        _flight = new Flight
        {
            Visual = visual,
            FromPosition = item.position,
            FromRotation = item.rotation,
            FromScale = item.lossyScale,
            ToHand = true,
            Duration = pickupFlySeconds
        };

        visual.transform.SetPositionAndRotation(item.position, item.rotation);
        visual.transform.localScale = item.lossyScale;
        _suppressHeldUntil = Time.time + pickupFlySeconds;
    }

    /// <summary>Q: 손에 든 물건을 바닥에 내려놓는다. 손을 앞 아래로 톡 뻗고, 물건은 손에서 바닥 자리까지 내려간다.</summary>
    private void HandleDropPressed()
    {
        if (_inventory == null || _heldItemId == InventoryManager.EmptySlot || _flight != null)
        {
            return;
        }

        if (!_inventory.TryPredictDrop(_heldItemId, out Vector3 position, out Quaternion rotation))
        {
            return; // 버릴 수 없는 아이템
        }

        _motion.Reach(FirstPersonArmMotion.ReachKind.Place);

        VisualTemplate template = GetTemplate(_heldItemId);

        if (template == null || _heldVisual == null)
        {
            InputManager.Instance.RequestDrop();
            return;
        }

        // 손에 든 모습을 그대로 떼어 바닥 자리로 보낸다
        GameObject visual = Instantiate(template.Prefab);
        visual.SetActive(true);
        Transform held = _heldVisual.transform;

        _flight = new Flight
        {
            Visual = visual,
            FromPosition = held.position,
            FromRotation = held.rotation,
            FromScale = held.lossyScale,
            ToHand = false,
            ToPosition = position,
            ToRotation = rotation,
            ToScale = template.RootScale,
            Duration = dropFlySeconds,
            RequestDropOnLand = true,
            LingerAfterLand = 0.35f
        };

        visual.transform.SetPositionAndRotation(held.position, held.rotation);
        visual.transform.localScale = held.lossyScale;
        _heldVisual.SetActive(false);
        _suppressHeldUntil = Time.time + dropFlySeconds + 0.5f;
    }

    private void UpdateFlight(float dt)
    {
        if (_flight == null)
        {
            return;
        }

        if (_flight.Visual == null)
        {
            _flight = null;
            return;
        }

        _flight.Time += dt;
        float u = Mathf.Clamp01(_flight.Time / _flight.Duration);
        Transform t = _flight.Visual.transform;

        if (_flight.ToHand)
        {
            // 손이 계속 움직이므로 목표를 매 프레임 다시 잡는다. 빨려 들어오듯 끝에서 빨라진다(ease-in).
            GetHeldPose(_expectPickupId, out Vector3 handPosition, out Quaternion handRotation, out Vector3 handScale);
            float e = u * u;
            t.position = Vector3.Lerp(_flight.FromPosition, handPosition, e);
            t.rotation = Quaternion.Slerp(_flight.FromRotation, handRotation, e);
            t.localScale = Vector3.Lerp(_flight.FromScale, handScale, e);

            if (u >= 1f)
            {
                ClearFlight();
                _suppressHeldUntil = 0f;
            }

            return;
        }

        // 버리기: 손을 떠나 바닥으로 — 처음엔 천천히, 떨어지며 빨라진다(ease-in)
        float fall = u * u;
        t.position = Vector3.Lerp(_flight.FromPosition, _flight.ToPosition, fall);
        t.rotation = Quaternion.Slerp(_flight.FromRotation, _flight.ToRotation, u);
        t.localScale = Vector3.Lerp(_flight.FromScale, _flight.ToScale, u);

        if (u >= 1f && _flight.RequestDropOnLand)
        {
            _flight.RequestDropOnLand = false;

            if (InputManager.Instance != null)
            {
                InputManager.Instance.RequestDrop();
            }
        }

        // 실제 아이템이 네트워크로 생길 때까지 같은 자리에 잠깐 남겨 둔다(빈틈 없이 이어지도록)
        if (u >= 1f && _flight.Time >= _flight.Duration + _flight.LingerAfterLand)
        {
            ClearFlight();
        }
    }

    private void ClearFlight()
    {
        if (_flight != null && _flight.Visual != null)
        {
            Destroy(_flight.Visual);
        }

        _flight = null;
    }

    // ───────────── 손에 든 물건 ─────────────

    private void UpdateHeldItem()
    {
        int current = _inventory != null && _inventory.Object != null && _inventory.Object.IsValid
            ? _inventory.GetHeldItemId()
            : InventoryManager.EmptySlot;

        if (current != _heldItemId && _pendingHeldId < 0)
        {
            if (current != InventoryManager.EmptySlot && current == _expectPickupId)
            {
                // 방금 주운 물건: 슬롯 교체 동작 없이 바로(날아온 물건이 도착하면 보인다)
                _expectPickupId = 0;
                SetHeld(current);
            }
            else if (_flight != null && !_flight.ToHand && current == InventoryManager.EmptySlot)
            {
                // 방금 버린 물건: 손은 이미 비어 보인다
                SetHeld(current);
            }
            else
            {
                // 슬롯을 바꿨다: 팔을 내렸다 올리며 가장 깊이 내려갔을 때 바꿔 든다
                _motion.Switch();
                _pendingHeldId = current;
            }
        }

        // 가장 깊이 내려갔을 때(또는 교체 동작이 끝났는데 아직 못 바꿨으면 그때) 바꿔 든다
        if (_pendingHeldId >= 0 && (_motion.SwitchWeight > 0.9f || !_motion.IsSwitching))
        {
            SetHeld(_pendingHeldId);
            _pendingHeldId = -1;
        }

        if (_heldVisual != null)
        {
            bool visible = Time.time >= _suppressHeldUntil;

            if (_heldVisual.activeSelf != visible)
            {
                _heldVisual.SetActive(visible);
            }
        }
    }

    private void SetHeld(int itemId)
    {
        _heldItemId = itemId;

        if (_heldVisual != null)
        {
            Destroy(_heldVisual);
            _heldVisual = null;
        }

        if (itemId == InventoryManager.EmptySlot)
        {
            return;
        }

        VisualTemplate template = GetTemplate(itemId);

        if (template == null)
        {
            return;
        }

        _heldVisual = Instantiate(template.Prefab, _hold);
        GetHeldLocalPose(template, out Vector3 localPosition, out float scale);
        _heldVisual.transform.localPosition = localPosition;
        _heldVisual.transform.localRotation = Quaternion.identity;
        _heldVisual.transform.localScale = Vector3.one * scale;
        _heldVisual.SetActive(Time.time >= _suppressHeldUntil);
        PrepareRenderers(_heldVisual);
    }

    /// <summary>손에 들 때의 크기·위치: 가장 긴 변을 holdSize 에 맞추고 물건 중심을 손바닥에 둔다.</summary>
    private void GetHeldLocalPose(VisualTemplate template, out Vector3 localPosition, out float scale)
    {
        float longest = Mathf.Max(template.LocalBounds.size.x, Mathf.Max(template.LocalBounds.size.y, template.LocalBounds.size.z));
        scale = longest > 0.0001f ? holdSize / longest : 1f;
        localPosition = -template.LocalBounds.center * scale;
    }

    /// <summary>손에 든 모습의 월드 자세(날아오는 물건의 목표).</summary>
    private void GetHeldPose(int itemId, out Vector3 position, out Quaternion rotation, out Vector3 scale)
    {
        VisualTemplate template = GetTemplate(itemId);
        float local = 1f;
        Vector3 offset = Vector3.zero;

        if (template != null)
        {
            GetHeldLocalPose(template, out offset, out local);
        }

        position = _hold.TransformPoint(offset);
        rotation = _hold.rotation;
        scale = _hold.lossyScale * local;
    }

    /// <summary>
    /// 아이템의 "보이는 모양만" 복제한 틀(네트워크·콜라이더 없음). 아이템 데이터의 월드 프리팹에서 메시·머티리얼만 옮긴다.
    /// 틀의 원점은 프리팹 루트, 크기는 프리팹 루트 기준(루트 배율 제외)이다.
    /// </summary>
    private VisualTemplate GetTemplate(int itemId)
    {
        if (itemId == InventoryManager.EmptySlot)
        {
            return null;
        }

        if (_templates.TryGetValue(itemId, out VisualTemplate cached))
        {
            return cached;
        }

        ItemData data = ItemDatabase.Instance != null ? ItemDatabase.Instance.GetItemData(itemId) : null;

        if (data == null || data.modelPrefab == null)
        {
            _templates[itemId] = null;
            return null;
        }

        Transform source = data.modelPrefab.transform;
        Matrix4x4 rootInverse = Matrix4x4.TRS(source.position, source.rotation, source.lossyScale).inverse;

        GameObject root = new GameObject("ItemVisual_" + data.itemName);
        root.SetActive(false);
        root.transform.SetParent(transform, false);

        bool hasBounds = false;
        Bounds bounds = new Bounds();

        foreach (MeshFilter filter in source.GetComponentsInChildren<MeshFilter>(true))
        {
            MeshRenderer renderer = filter.GetComponent<MeshRenderer>();

            if (renderer == null || filter.sharedMesh == null)
            {
                continue;
            }

            Matrix4x4 relative = rootInverse * filter.transform.localToWorldMatrix;
            GameObject part = new GameObject(filter.name);
            part.transform.SetParent(root.transform, false);
            part.transform.localPosition = relative.GetColumn(3);
            part.transform.localRotation = relative.rotation;
            part.transform.localScale = relative.lossyScale;
            part.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            MeshRenderer copy = part.AddComponent<MeshRenderer>();
            copy.sharedMaterials = renderer.sharedMaterials;
            copy.shadowCastingMode = ShadowCastingMode.Off;

            Bounds meshBounds = filter.sharedMesh.bounds;

            for (int c = 0; c < 8; c++)
            {
                Vector3 corner = meshBounds.center + Vector3.Scale(meshBounds.extents,
                    new Vector3((c & 1) == 0 ? -1f : 1f, (c & 2) == 0 ? -1f : 1f, (c & 4) == 0 ? -1f : 1f));
                Vector3 p = relative.MultiplyPoint3x4(corner);

                if (!hasBounds)
                {
                    bounds = new Bounds(p, Vector3.zero);
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(p);
                }
            }
        }

        if (!hasBounds)
        {
            Destroy(root);
            _templates[itemId] = null;
            return null;
        }

        VisualTemplate template = new VisualTemplate { Prefab = root, LocalBounds = bounds, RootScale = source.lossyScale };
        _templates[itemId] = template;
        return template;
    }
}
