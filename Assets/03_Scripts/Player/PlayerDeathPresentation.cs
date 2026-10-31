using Fusion;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 사망 상태의 시각적 표현만 담당합니다. 체력/판정은 PlayerData가 관리합니다.
/// 애니메이션 연결은 모든 클라이언트에서, 흑백 효과는 본인 카메라에서만 실행합니다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerData))]
public sealed class PlayerDeathPresentation : NetworkBehaviour
{
    [SerializeField] private Camera _playerCamera;
    [SerializeField, Min(0f)] private float _grayscaleDuration = 0.35f;

    [Header("Local Death Camera")]
    [SerializeField, Min(0f)] private float _cameraFallDuration = 0.65f;
    [SerializeField, Min(0f)] private float _cameraFallHold = 0.35f;
    [SerializeField, Min(0.05f)] private float _cameraGroundHeight = 0.25f;
    [SerializeField, Range(-90f, 90f)] private float _cameraFallRoll = 78f;
    [Tooltip("바닥에 닿은 뒤 반동이 가라앉는 시간(초)")]
    [SerializeField, Min(0f)] private float _cameraImpactDuration = 0.24f;
    [SerializeField, Min(0f)] private float _cameraImpactBounce = 0.07f;
    [SerializeField, Range(0f, 20f)] private float _cameraImpactAngle = 7f;
    private Camera _fallCamera;
    private Vector3 _cameraLocalPosition;
    private Quaternion _cameraLocalRotation;
    private Vector3 _fallStartPosition;
    private Vector3 _fallEndPosition;
    private Quaternion _fallStartRotation;
    private Quaternion _fallEndRotation;
    private float _fallStartedAt;
    private bool _cameraPoseCaptured;
    private bool _cameraFallPlaying;

    public bool CanStartSpectating => _presented && !_cameraFallPlaying;

    [Header("Death Animation (Optional)")]
    [Tooltip("나중에 Animator.SetTrigger 등 쓰러짐 연출을 연결합니다. 모든 클라이언트에서 한 번 호출됩니다.")]
    [SerializeField] private UnityEvent _onDeathPresentation = new UnityEvent();
    [Header("Death Trace")]
    [SerializeField] private Material _deathTraceMaterial;
    [Tooltip("선택 사항. 비워도 기본 발자국과 피격 흔적을 생성합니다.")]
    [SerializeField] private GameObject _deathEffectPrefab;
    private GameObject _deathTrace;
    private Renderer[] _traceRenderers;
    private bool[] _traceRendererVisibility;
    private bool _traceHidden;
    private GameObject _deathEffect;
    private bool _traceCreated;

    private PlayerData _playerData;
    private bool _spawned;
    private bool _presented;
    private bool _animationPlayed;
    private Volume _volume;
    private VolumeProfile _profile;
    private UniversalAdditionalCameraData _cameraData;
    private bool _previousPostProcessing;
    private LayerMask _previousVolumeMask;
    private VolumeFrameworkUpdateMode _previousUpdateMode;
    private int _previousCullingMask;
    private readonly Dictionary<Canvas, CanvasState> _screenCanvases = new Dictionary<Canvas, CanvasState>();

    private struct CanvasState
    {
        public RenderMode RenderMode;
        public Camera Camera;
        public float PlaneDistance;
        public int SortingLayerId;
        public int SortingOrder;
    }

    private void Awake()
    {
        _playerData = GetComponent<PlayerData>();
        if (_playerCamera == null)
        {
            _playerCamera = GetComponentInChildren<Camera>(true);
        }
    }

    private void OnEnable()
    {
        _playerData.Died += PresentDeath;
        RenderPipelineManager.beginCameraRendering += BeginCameraRendering;
        RenderPipelineManager.endCameraRendering += EndCameraRendering;
    }

    public override void Spawned()
    {
        RestoreDeathCamera();
        PlayerController controller = GetComponent<PlayerController>();
        if (controller != null && controller.ViewCamera != null)
            _playerCamera = controller.ViewCamera;
        _spawned = true;
        _presented = false;
        _animationPlayed = false;
        _traceCreated = false;
        // 초기 상태는 모든 컴포넌트의 Spawned가 끝난 뒤 Render에서 확인합니다.
    }

    public override void Render()
    {
        // 늦은 참가와 재활성화 시에도 현재 사망 상태를 반영합니다.
        if (!_spawned || !isActiveAndEnabled || !_playerData.IsDead)
        {
            return;
        }

        PresentDeath();
        if (_playerData.DeathSequenceCompleted && !_traceCreated)
        {
            _traceCreated = true;
            Vector3 position = PlayerDeathPlacement.GroundPoint(_playerData.DeathPosition, transform, out Vector3 normal);
            if (_deathTraceMaterial != null)
            {
                _deathTrace = PlayerDeathTrace.Create(position, normal, _deathTraceMaterial);
                _traceRenderers = _deathTrace.GetComponentsInChildren<Renderer>(true);
                _traceRendererVisibility = new bool[_traceRenderers.Length];
            }
            if (_deathEffectPrefab != null)
                _deathEffect = Instantiate(_deathEffectPrefab, position, Quaternion.FromToRotation(Vector3.up, normal));
        }
        if (_volume != null)
        {
            _volume.weight = _grayscaleDuration <= 0f
                ? 1f
                : Mathf.MoveTowards(_volume.weight, 1f, Time.unscaledDeltaTime / _grayscaleDuration);
        }
    }

    private void PresentDeath()
    {
        if (!_spawned || _presented || !isActiveAndEnabled)
        {
            return;
        }

        _presented = true;
        // 죽은 캐릭터를 적이 계속 추격하지 않도록 각 피어의 감지 대상에서 제외합니다.
        foreach (Pawntom.Enemy.Adapters.EnemyTargetRegistrar registrar in GetComponentsInChildren<Pawntom.Enemy.Adapters.EnemyTargetRegistrar>())
            registrar.enabled = false;
        if (Object.HasInputAuthority && _playerCamera != null)
        {
            foreach (Hacking.HackingManager hacking in FindObjectsByType<Hacking.HackingManager>(FindObjectsSortMode.None))
                hacking.CancelForPlayerDeath();
            foreach (MainbaseControlUI terminal in FindObjectsByType<MainbaseControlUI>(FindObjectsSortMode.None))
                if (terminal.IsOpen) terminal.Close();
            foreach (MarketUI market in FindObjectsByType<MarketUI>(FindObjectsSortMode.None))
                market.Close();
            StartDeathCamera();
            CreateGrayscaleEffect();
        }

        if (!_animationPlayed)
        {
            _animationPlayed = true;
            _onDeathPresentation.Invoke();
        }
    }

    private void StartDeathCamera()
    {
        _fallCamera = _playerCamera;
        Transform cameraTransform = _fallCamera.transform;
        _cameraLocalPosition = cameraTransform.localPosition;
        _cameraLocalRotation = cameraTransform.localRotation;
        _cameraPoseCaptured = true;
        _fallStartPosition = cameraTransform.position;
        _fallStartRotation = cameraTransform.rotation;

        // 월드 좌표로 계산해 플레이어 프리팹의 축소 스케일에 영향을 받지 않게 합니다.
        Vector3 ground = PlayerDeathPlacement.GroundPoint(_fallStartPosition, transform, out _);
        float clearance = Mathf.Max(_cameraGroundHeight, _fallCamera.nearClipPlane * 2f);
        _fallEndPosition = _fallStartPosition;
        _fallEndPosition.y = Mathf.Min(_fallStartPosition.y, ground.y + clearance);
        _fallEndRotation = Quaternion.Euler(10f, cameraTransform.eulerAngles.y, _cameraFallRoll);
        _fallStartedAt = Time.unscaledTime;
        _cameraFallPlaying = true;
    }

    private void LateUpdate()
    {
        if (!_spawned || !_cameraPoseCaptured || _fallCamera == null) return;

        float elapsed = Time.unscaledTime - _fallStartedAt;
        float fallDuration = Mathf.Max(0f, _cameraFallDuration);
        float impactDuration = Mathf.Max(0f, _cameraImpactDuration);
        float progress = fallDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / fallDuration);
        // 끝에서 감속하지 않고 무게가 실리도록 가속한 뒤 바닥에서 급격히 멈춥니다.
        Vector3 position = Vector3.Lerp(_fallStartPosition, _fallEndPosition, progress * progress);
        Quaternion rotation = Quaternion.Slerp(_fallStartRotation, _fallEndRotation, Mathf.Pow(progress, 1.4f));
        if (elapsed >= fallDuration && impactDuration > 0f)
        {
            float impact = Mathf.Clamp01((elapsed - fallDuration) / impactDuration);
            float remaining = 1f - impact;
            // 위로만 짧게 튀어 바닥을 관통하지 않고, 작은 회전 반동이 빠르게 잦아듭니다.
            float bounce = Mathf.Sin(impact * Mathf.PI) * remaining * _cameraImpactBounce;
            float jolt = Mathf.Sin(impact * Mathf.PI * 3f) * remaining * remaining * _cameraImpactAngle;
            position += Vector3.up * bounce;
            rotation *= Quaternion.Euler(jolt * 0.45f, 0f, jolt);
        }
        // 관전 카메라로 바뀐 뒤에도 다른 플레이어의 Transform은 수정하지 않습니다.
        if (_playerCamera == _fallCamera)
        {
            _fallCamera.transform.SetPositionAndRotation(position, rotation);
        }
        _cameraFallPlaying = elapsed < fallDuration + impactDuration + Mathf.Max(0f, _cameraFallHold);
    }

    private void RestoreDeathCamera()
    {
        if (_cameraPoseCaptured && _fallCamera != null)
        {
            _fallCamera.transform.localPosition = _cameraLocalPosition;
            _fallCamera.transform.localRotation = _cameraLocalRotation;
        }
        _fallCamera = null;
        _cameraPoseCaptured = false;
        _cameraFallPlaying = false;
    }

    private void CreateGrayscaleEffect()
    {
        _cameraData = _playerCamera.GetUniversalAdditionalCameraData();
        _previousPostProcessing = _cameraData.renderPostProcessing;
        _previousVolumeMask = _cameraData.volumeLayerMask;
        _previousUpdateMode = _playerCamera.GetVolumeFrameworkUpdateMode();
        _previousCullingMask = _playerCamera.cullingMask;
        _cameraData.renderPostProcessing = true;
        _playerCamera.SetVolumeFrameworkUpdateMode(VolumeFrameworkUpdateMode.EveryFrame);

        // 기존 카메라 마스크에 포함된 레이어를 사용합니다.
        int mask = _previousVolumeMask.value;
        int layer = 0;
        if (mask == 0)
        {
            _cameraData.volumeLayerMask = 1;
        }
        else
        {
            while ((mask & (1 << layer)) == 0) layer++;
        }

        GameObject volumeObject = new GameObject("Local Death Grayscale");
        volumeObject.SetActive(false);
        volumeObject.transform.SetParent(transform, false);
        volumeObject.layer = layer;
        _volume = volumeObject.AddComponent<Volume>();
        _volume.enabled = false;
        _volume.isGlobal = true;
        _volume.priority = float.MaxValue;
        _volume.weight = _grayscaleDuration <= 0f ? 1f : 0f;

        // 런타임 전용 프로필: 씬/에셋의 공용 VolumeProfile은 수정하지 않습니다.
        _profile = ScriptableObject.CreateInstance<VolumeProfile>();
        _profile.Add<ColorAdjustments>().saturation.Override(-100f);
        _volume.sharedProfile = _profile;
        volumeObject.SetActive(true);

        // Overlay UI는 후처리 뒤에 그려지므로 본인 카메라로 함께 렌더링합니다.
        // 사망 이후 새로 열리는 팝업도 다음 UI 렌더링 전에 연결합니다.
        Canvas.preWillRenderCanvases += RouteScreenCanvases;
        RouteScreenCanvases();
    }

    /// <summary>로컬 관전자가 실제로 보는 카메라에 흑백과 HUD를 다시 연결합니다.</summary>
    public void SetViewingCamera(Camera camera)
    {
        if (camera == null || camera == _playerCamera) return;
        ClearGrayscaleEffect();
        _playerCamera = camera;
        if (_spawned && _presented && isActiveAndEnabled && Object != null &&
            Object.IsValid && Object.HasInputAuthority)
        {
            CreateGrayscaleEffect();
            if (_volume != null) _volume.weight = 1f;
        }
    }

    private void RouteScreenCanvases()
    {
        if (_volume == null || _playerCamera == null || !_spawned ||
            Object == null || !Object.IsValid || !Object.HasInputAuthority)
        {
            return;
        }

        Canvas[] canvases = FindObjectsByType<Canvas>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Canvas canvas in canvases)
        {
            if (!canvas.isRootCanvas || canvas.renderMode == RenderMode.WorldSpace ||
                canvas.targetDisplay != _playerCamera.targetDisplay || _screenCanvases.ContainsKey(canvas))
            {
                continue;
            }

            // 다른 카메라의 전용 UI는 가져오지 않습니다.
            if (canvas.renderMode == RenderMode.ScreenSpaceCamera && canvas.worldCamera != _playerCamera)
            {
                continue;
            }

            CanvasState state = new CanvasState
            {
                RenderMode = canvas.renderMode,
                Camera = canvas.worldCamera,
                PlaneDistance = canvas.planeDistance,
                SortingLayerId = canvas.sortingLayerID,
                SortingOrder = canvas.sortingOrder
            };
            _screenCanvases.Add(canvas, state);
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = _playerCamera;
            // 카메라 바로 앞에 배치해 기존 HUD처럼 월드보다 앞에 표시합니다.
            canvas.planeDistance = Mathf.Lerp(_playerCamera.nearClipPlane, _playerCamera.farClipPlane, 0.0001f);
            canvas.sortingLayerID = state.SortingLayerId;
            canvas.sortingOrder = state.SortingOrder;
            _playerCamera.cullingMask |= 1 << canvas.gameObject.layer;
        }
    }

    private void RestoreScreenCanvases()
    {
        Canvas.preWillRenderCanvases -= RouteScreenCanvases;
        foreach (KeyValuePair<Canvas, CanvasState> entry in _screenCanvases)
        {
            if (entry.Key == null) continue;
            entry.Key.renderMode = entry.Value.RenderMode;
            entry.Key.worldCamera = entry.Value.Camera;
            entry.Key.planeDistance = entry.Value.PlaneDistance;
            entry.Key.sortingLayerID = entry.Value.SortingLayerId;
            entry.Key.sortingOrder = entry.Value.SortingOrder;
        }
        _screenCanvases.Clear();
    }

    private void BeginCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        // 자신의 사망 흔적만 본인이 보는 카메라에서 숨깁니다. 다른 카메라에서는 그대로 표시합니다.
        if (!_traceHidden && _traceRenderers != null && camera == _playerCamera && _spawned &&
            Object != null && Object.IsValid && Object.HasInputAuthority)
        {
            for (int i = 0; i < _traceRenderers.Length; i++)
            {
                Renderer renderer = _traceRenderers[i];
                if (renderer == null) continue;
                _traceRendererVisibility[i] = renderer.forceRenderingOff;
                renderer.forceRenderingOff = true;
            }
            _traceHidden = true;
        }
        if (_volume != null)
        {
            // URP가 해당 카메라의 Volume 스택을 계산하기 직전에만 등록합니다.
            // 다른 플레이어, CCTV, Scene 뷰 카메라에는 적용되지 않습니다.
            _volume.enabled = camera == _playerCamera && _spawned &&
                Object != null && Object.IsValid && Object.HasInputAuthority;
        }
    }

    private void EndCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (camera == _playerCamera) RestoreTraceVisibility();
        if (_volume != null)
        {
            _volume.enabled = false;
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        RestoreTraceVisibility();
        _spawned = false;
        ClearGrayscaleEffect();
        RestoreDeathCamera();
        if (_deathTrace != null) Destroy(_deathTrace);
        if (_deathEffect != null) Destroy(_deathEffect);
    }

    private void OnDisable()
    {
        RestoreTraceVisibility();
        _playerData.Died -= PresentDeath;
        RenderPipelineManager.beginCameraRendering -= BeginCameraRendering;
        RenderPipelineManager.endCameraRendering -= EndCameraRendering;
        ClearGrayscaleEffect();
        RestoreDeathCamera();
        _presented = false;
    }

    private void RestoreTraceVisibility()
    {
        if (!_traceHidden) return;
        for (int i = 0; i < _traceRenderers.Length; i++)
            if (_traceRenderers[i] != null)
                _traceRenderers[i].forceRenderingOff = _traceRendererVisibility[i];
        _traceHidden = false;
    }

    private void ClearGrayscaleEffect()
    {
        RestoreScreenCanvases();
        if (_volume != null)
        {
            _volume.enabled = false;
            Destroy(_volume.gameObject);
            _volume = null;
        }

        if (_profile != null)
        {
            foreach (VolumeComponent component in _profile.components) Destroy(component);
            Destroy(_profile);
            _profile = null;
        }

        if (_cameraData != null)
        {
            _cameraData.renderPostProcessing = _previousPostProcessing;
            _cameraData.volumeLayerMask = _previousVolumeMask;
            if (_playerCamera != null)
            {
                _playerCamera.cullingMask = _previousCullingMask;
                _playerCamera.SetVolumeFrameworkUpdateMode(_previousUpdateMode);
            }
            _cameraData = null;
        }
    }
}
