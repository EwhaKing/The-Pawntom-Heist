using Fusion;
using Pawntom.PlayerAnimation;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 플레이어 모델의 Animator 를 이동 상태에 맞춰 굴린다(Idle·Walk·Run 블렌드 + Jump).
/// <para>
/// 애니메이션 상태를 따로 동기화하지 않는다. NetworkCharacterController 의 Velocity·Grounded 는 이미 네트워크 값이라
/// 모든 피어(프록시 포함)가 같은 값으로 각자 계산한다. 계산 규칙은 <see cref="PlayerLocomotionModel"/>.
/// </para>
/// <para>내 캐릭터는 내 1인칭 카메라에서만 그림자로 그린다(시야를 가리지 않게). 씬 뷰·다른 플레이어 화면에서는 그대로 보인다.</para>
/// </summary>
[DisallowMultipleComponent]
public class PlayerAnimationView : MonoBehaviour
{
    private static readonly int LocomotionHash = Animator.StringToHash("Locomotion");
    private static readonly int AirborneHash = Animator.StringToHash("Airborne");

    [SerializeField] private Animator animator;

    [Tooltip("모델 루트. 내 캐릭터일 때 이 아래 렌더러를 그림자 전용으로 바꾼다")]
    [SerializeField] private Transform modelRoot;

    [Tooltip("속도 추종 빠르기(1/초). 클수록 걷기↔달리기 전환이 빠르다")]
    [SerializeField, Min(0f)] private float speedSmoothing = 12f;

    [Tooltip("발이 떨어진 뒤 이 시간(초)이 지나야 점프 동작을 낸다. 계단·턱에서 점프 동작이 튀는 것 방지")]
    [SerializeField, Min(0f)] private float airborneGraceSeconds = 0.12f;

    [Tooltip("이 속도(월드 단위/초)보다 빠르게 올라가면 유예 없이 바로 점프 동작을 낸다")]
    [SerializeField] private float jumpVelocityThreshold = 0.5f;

    private NetworkObject _networkObject;
    private NetworkCharacterController _controller;
    private PlayerLocomotionModel _model;
    private bool _ownerVisualApplied;
    private Camera _ownCamera;
    private Renderer[] _ownRenderers;
    private bool _subscribed;

    private void Awake()
    {
        _networkObject = GetComponent<NetworkObject>();
        _controller = GetComponent<NetworkCharacterController>();

        PlayerController player = GetComponent<PlayerController>();
        float walk = player != null ? player.WalkSpeed : 1f;
        float sprint = player != null ? player.SprintSpeed : 2f;
        _model = new PlayerLocomotionModel(walk, sprint, speedSmoothing, airborneGraceSeconds, jumpVelocityThreshold);

        if (animator != null)
        {
            // 이동은 NetworkCharacterController 가 한다. 클립의 루트 이동이 몸을 끌고 가면 안 된다.
            animator.applyRootMotion = false;
        }

        SinkModelBySkinWidth();
    }

    /// <summary>
    /// CharacterController 는 바닥과 skinWidth 만큼 떨어져 선다(실측 8cm). skinWidth 는 플레이어 스케일(0.05)을 따르지 않는
    /// 월드 값이라 캡슐 바닥에 맞춘 모델 발이 그만큼 떠 보인다. 판정은 그대로 두고 보이는 모델만 그만큼 내린다.
    /// </summary>
    private void SinkModelBySkinWidth()
    {
        CharacterController controller = GetComponent<CharacterController>();

        if (controller == null || modelRoot == null || modelRoot.parent == null)
        {
            return;
        }

        float parentScale = Mathf.Max(0.0001f, modelRoot.parent.lossyScale.y);
        modelRoot.localPosition -= Vector3.up * (controller.skinWidth / parentScale);
    }

    private void Update()
    {
        if (animator == null || _controller == null || _networkObject == null || !_networkObject.IsValid)
        {
            return;
        }

        if (!_ownerVisualApplied)
        {
            ApplyOwnerVisual(_networkObject.HasInputAuthority);
            _ownerVisualApplied = true;
        }

        Vector3 velocity = _controller.Velocity;
        float horizontal = Mathf.Sqrt(velocity.x * velocity.x + velocity.z * velocity.z);
        _model.Tick(horizontal, velocity.y, _controller.Grounded, Time.deltaTime);

        animator.SetFloat(LocomotionHash, _model.Locomotion);
        animator.SetBool(AirborneHash, _model.Airborne);
    }

    /// <summary>
    /// 내 캐릭터는 <b>내 1인칭 카메라가 그릴 때만</b> 그림자 전용으로 바꾸고, 다 그리면 되돌린다.
    /// 카메라가 고양이 몸 안(가슴 높이)에 있어서 그대로 그리면 눈알·목걸이·멜빵 안쪽이 화면을 가린다.
    /// 씬 뷰·다른 카메라에서는 평소대로 보인다(항상 그림자 전용이면 씬 뷰에서도 모델이 사라져 보인다).
    /// </summary>
    private void ApplyOwnerVisual(bool isLocalPlayer)
    {
        if (!isLocalPlayer || modelRoot == null)
        {
            return;
        }

        _ownCamera = GetComponentInChildren<Camera>(true);
        _ownRenderers = modelRoot.GetComponentsInChildren<Renderer>(true);

        if (_ownCamera == null || _ownRenderers.Length == 0)
        {
            return;
        }

        RenderPipelineManager.beginCameraRendering += HandleBeginCameraRendering;
        RenderPipelineManager.endCameraRendering += HandleEndCameraRendering;
        _subscribed = true;
    }

    private void HandleBeginCameraRendering(ScriptableRenderContext context, Camera rendering)
    {
        if (rendering == _ownCamera)
        {
            SetOwnShadowCasting(ShadowCastingMode.ShadowsOnly);
        }
    }

    private void HandleEndCameraRendering(ScriptableRenderContext context, Camera rendering)
    {
        if (rendering == _ownCamera)
        {
            SetOwnShadowCasting(ShadowCastingMode.On);
        }
    }

    private void SetOwnShadowCasting(ShadowCastingMode mode)
    {
        for (int i = 0; i < _ownRenderers.Length; i++)
        {
            if (_ownRenderers[i] != null)
            {
                _ownRenderers[i].shadowCastingMode = mode;
            }
        }
    }

    private void OnDestroy()
    {
        if (!_subscribed)
        {
            return;
        }

        RenderPipelineManager.beginCameraRendering -= HandleBeginCameraRendering;
        RenderPipelineManager.endCameraRendering -= HandleEndCameraRendering;
        _subscribed = false;
    }
}
