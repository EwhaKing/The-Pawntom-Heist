using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 미니맵(CCTV) 카메라가 그릴 때만 켜지는 조명.
///
/// 배경:
/// - 미니맵(CCTV) 카메라는 레이어 6~8 만 그린다. 바닥/벽(MinimapFloor)은 조명과 무관한 평면 셰이더
///   (Pawntom/Minimap Flat)라 이 조명이 필요 없지만, 레이어 6 에 있는 아이템(Item Color, Lit)은
///   빛을 받아야 아이콘으로 보인다.
/// - 실내 분위기를 위해 씬의 Directional Light 를 끄면 아이템 아이콘이 어두워진다.
///
/// 동작:
/// - 이 조명은 평소에 꺼 두고, 렌더 텍스처로 그리면서 minimapLayers 안의 레이어만 보는 카메라가
///   렌더링을 시작할 때(beginCameraRendering) 켰다가 끝나면(endCameraRendering) 끈다.
/// - 그래서 메인 카메라 화면의 조명/분위기에는 영향을 주지 않는다. (그림자는 쓰지 않는다)
///
/// 사용 위치:
/// - Directional Light 가 붙은 오브젝트(Realtime, Shadows None)에 같이 붙인다. 컴포넌트는 항상 켜져 있어야 한다.
/// </summary>
[RequireComponent(typeof(Light))]
[DisallowMultipleComponent]
[AddComponentMenu("Pawntom/Lighting/Minimap Only Light")]
public class MinimapOnlyLight : MonoBehaviour
{
    [Tooltip("미니맵 레이어(MinimapGround/MinimapWall/MinimapIcon). 이 레이어만 그리는 카메라에서만 켠다")]
    [SerializeField] private LayerMask minimapLayers = (1 << 6) | (1 << 7) | (1 << 8);

    private Light _light;

    private void Awake()
    {
        _light = GetComponent<Light>();
        _light.enabled = false;
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

        if (_light != null)
        {
            _light.enabled = false;
        }
    }

    private void OnBeginCameraRendering(ScriptableRenderContext context, Camera cam)
    {
        _light.enabled = IsMinimapCamera(cam);
    }

    private void OnEndCameraRendering(ScriptableRenderContext context, Camera cam)
    {
        _light.enabled = false;
    }

    // 렌더 텍스처로 그리고, 컬링 마스크가 미니맵 레이어 안에 모두 들어가는 카메라만 해당한다.
    private bool IsMinimapCamera(Camera cam)
    {
        return cam.targetTexture != null && cam.cullingMask != 0 && (cam.cullingMask & ~minimapLayers.value) == 0;
    }
}
