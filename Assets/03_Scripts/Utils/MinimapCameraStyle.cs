using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 미니맵 계열 카메라마다 "플레이어 미니맵 스타일"을 켤지 정한다.
///
/// 배경:
/// - 플레이어 미니맵(MinimapCamera)과 본부 CCTV 지도(MainbaseMapCamera)는 같은 MinimapFloor 복제본을 그린다.
///   하지만 두 화면의 디자인(색)은 달라야 한다. (플레이어: 초록 바닥 + 회색 벽, CCTV: 흰 바탕 + 초록 벽)
///
/// 동작:
/// - 이 컴포넌트가 붙은 카메라가 렌더링을 시작할 때 전역 셰이더 값 _PawntomMinimapPlayerStyle 을 켜고
///   (usePlayerStyle), 끝나면 0 으로 되돌린다. Pawntom/Minimap Flat 의 _UsePlayerStyle 머티리얼만 이 값을 따른다.
///
/// 사용 위치:
/// - MinimapCamera(플레이어 미니맵)에 usePlayerStyle 을 켜고 붙인다. CCTV 카메라에는 붙이지 않아도 된다.
/// </summary>
[RequireComponent(typeof(Camera))]
[DisallowMultipleComponent]
[AddComponentMenu("Pawntom/Minimap/Minimap Camera Style")]
public class MinimapCameraStyle : MonoBehaviour
{
    private static readonly int PlayerStyleId = Shader.PropertyToID("_PawntomMinimapPlayerStyle");

    [SerializeField] private bool usePlayerStyle = true;

    private Camera _camera;

    private void Awake()
    {
        _camera = GetComponent<Camera>();
    }

    private void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
        Shader.SetGlobalFloat(PlayerStyleId, 0f);
    }

    private void OnBeginCameraRendering(ScriptableRenderContext context, Camera cam)
    {
        if (cam == _camera)
        {
            Shader.SetGlobalFloat(PlayerStyleId, usePlayerStyle ? 1f : 0f);
        }
    }

    private void OnEndCameraRendering(ScriptableRenderContext context, Camera cam)
    {
        if (cam == _camera)
        {
            Shader.SetGlobalFloat(PlayerStyleId, 0f);
        }
    }
}
