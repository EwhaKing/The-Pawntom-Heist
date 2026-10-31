using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// <summary>사망한 로컬 클라이언트에서 생존 팀원의 카메라를 켭니다. 입력/네트워크 권한은 바꾸지 않습니다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerData), typeof(PlayerController))]
public sealed class PlayerSpectator : MonoBehaviour
{
    private PlayerData _player;
    private Camera _camera;
    private Camera _ownCamera;
    private PlayerDeathPresentation _presentation;
    private PlayerData _target;
    private Camera _targetCamera;
    private Canvas _canvas;
    private TMP_Text _label;
    private bool _started;
    private MinimapCameraFollow _minimap;
    private MinimapPlayerArrow _arrow;
    private Renderer[] _hiddenRenderers;
    private bool[] _previousRendererStates;
    private bool _ownCameraEnabled;
    private bool _ownListenerEnabled;
    private bool _targetCameraEnabled;
    private bool _targetListenerEnabled;
    private bool _targetSelected;

    public PlayerData Target => _target;

    private void Awake()
    {
        _player = GetComponent<PlayerData>();
        _ownCamera = GetComponent<PlayerController>().ViewCamera;
        _presentation = GetComponent<PlayerDeathPresentation>();
        _camera = _ownCamera;
    }

    private void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += BeginCameraRendering;
        RenderPipelineManager.endCameraRendering += EndCameraRendering;
    }

    private bool CanSpectate => _player != null && _player.Object != null &&
        _player.Object.IsValid && _player.Object.HasInputAuthority &&
        _player.IsDead;

    private void LateUpdate()
    {
        // 튜토리얼은 일반 관전 대신 단계 재시도 화면이 카메라를 관리합니다.
        if (Pawntom.TutorialBridge.TutorialDirector.Active != null) return;
        if (!CanSpectate)
        {
            if (_started) StopSpectating();
            return;
        }

        if (!_started)
        {
            // 본인의 쓰러짐과 짧은 정지 연출을 끝낸 뒤 관전합니다. 드롭 성공 여부와는 독립적입니다.
            if (_presentation != null && _presentation.isActiveAndEnabled && !_presentation.CanStartSpectating)
                return;
            if (_ownCamera == null) _ownCamera = GetComponent<PlayerController>().ViewCamera;
            if (_ownCamera == null) _ownCamera = GetComponentInChildren<Camera>(true);
            if (_ownCamera == null) return;
            _camera = _ownCamera;
            _started = true;
            _ownCameraEnabled = _ownCamera.enabled;
            AudioListener ownListener = _ownCamera.GetComponent<AudioListener>();
            _ownListenerEnabled = ownListener != null && ownListener.enabled;
            _minimap = FindFirstObjectByType<MinimapCameraFollow>();
            _arrow = FindFirstObjectByType<MinimapPlayerArrow>();
            CreateLabel();
        }

        if (!IsEligible(_target)) SelectTarget(1);
        Keyboard keyboard = Keyboard.current;
        if (!GameplayInputBlocker.IsBlocked && keyboard != null)
        {
            if (keyboard.rightArrowKey.wasPressedThisFrame) SelectTarget(1);
            else if (keyboard.leftArrowKey.wasPressedThisFrame) SelectTarget(-1);
        }

        if (_targetCamera != null)
        {
            _label.text = $"관전 중 · {_target.SelectedCatType} ({_target.Object.InputAuthority})\n← 이전 팀원    다음 팀원 →";
        }
        else
        {
            _label.text = "사망했습니다 · 관전할 생존 팀원이 없습니다";
        }
    }

    private bool IsEligible(PlayerData candidate)
    {
        if (candidate == null || candidate == _player || !candidate.isActiveAndEnabled ||
            candidate.Object == null || !candidate.Object.IsValid ||
            candidate.Runner != _player.Runner || candidate.IsDead ||
            candidate.Object.InputAuthority == PlayerRef.None) return false;
        PlayerController controller = candidate.GetComponent<PlayerController>();
        return controller != null && controller.ViewCamera != null;
    }

    public void SelectTarget(int direction)
    {
        int count = PlayerData.ActivePlayers.Count;
        int start = _target != null ? PlayerData.ActivePlayers.IndexOf(_target) : (direction > 0 ? -1 : 0);
        PlayerData next = null;
        for (int step = 1; step <= count; step++)
        {
            int index = ((start + (direction < 0 ? -step : step)) % count + count) % count;
            if (!IsEligible(PlayerData.ActivePlayers[index])) continue;
            next = PlayerData.ActivePlayers[index];
            break;
        }
        SetTarget(next);
    }

    private void SetTarget(PlayerData next)
    {
        if (_targetSelected && ReferenceEquals(next, _target) && (next == null || _targetCamera != null)) return;
        _targetSelected = true;
        RestoreRenderers();
        RestoreTargetCamera();
        _target = next;
        _targetCamera = next != null ? next.GetComponent<PlayerController>().ViewCamera : null;
        if (_targetCamera != null)
        {
            _targetCameraEnabled = _targetCamera.enabled;
            AudioListener listener = _targetCamera.GetComponent<AudioListener>();
            _targetListenerEnabled = listener != null && listener.enabled;
            _ownCamera.enabled = false;
            AudioListener ownListener = _ownCamera.GetComponent<AudioListener>();
            if (ownListener != null) ownListener.enabled = false;
            _camera = _targetCamera;
            _targetCamera.enabled = true;
            if (listener != null) listener.enabled = true;
        }
        else
        {
            _camera = _ownCamera;
            _ownCamera.enabled = true;
            AudioListener ownListener = _ownCamera.GetComponent<AudioListener>();
            if (ownListener != null) ownListener.enabled = _ownListenerEnabled;
        }
        if (_presentation != null) _presentation.SetViewingCamera(_camera);
        Debug.Log(next != null
            ? $"[PlayerSpectator] 관전 카메라 전환: {next.Object.InputAuthority}"
            : "[PlayerSpectator] 관전할 생존 팀원이 없습니다.");
        // 자기 카메라는 주인의 몸을 렌더링하지 않습니다. 관전자에게도 같은 1인칭 시야를 제공합니다.
        if (next != null)
        {
            _hiddenRenderers = next.GetComponentsInChildren<Renderer>();
            _previousRendererStates = new bool[_hiddenRenderers.Length];
            for (int i = 0; i < _hiddenRenderers.Length; i++)
            {
                _previousRendererStates[i] = _hiddenRenderers[i].forceRenderingOff;
            }
        }
        Transform mapTarget = next != null ? next.transform : null;
        if (_minimap != null) _minimap.SetTarget(mapTarget);
        if (_arrow != null) _arrow.SetTarget(mapTarget);
    }

    private void RestoreTargetCamera()
    {
        if (_targetCamera == null) return;
        _targetCamera.enabled = _targetCameraEnabled;
        AudioListener listener = _targetCamera.GetComponent<AudioListener>();
        if (listener != null) listener.enabled = _targetListenerEnabled;
    }

    private void BeginCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (camera != _camera || _hiddenRenderers == null) return;
        foreach (Renderer renderer in _hiddenRenderers)
            if (renderer != null) renderer.forceRenderingOff = true;
    }

    private void EndCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (camera != _camera) return;
        RestoreRendererVisibility();
    }

    private void CreateLabel()
    {
        GameObject root = new GameObject("Spectator Status", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        _canvas = root.GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 1200;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1800f, 950f);
        GameObject panel = new GameObject("Status", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(root.transform, false);
        RectTransform rect = (RectTransform)panel.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -30f);
        rect.sizeDelta = new Vector2(740f, 84f);
        Image background = panel.GetComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.75f);
        background.raycastTarget = false;
        GameObject text = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        text.transform.SetParent(panel.transform, false);
        RectTransform textRect = (RectTransform)text.transform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(12f, 6f);
        textRect.offsetMax = new Vector2(-12f, -6f);
        _label = text.GetComponent<TextMeshProUGUI>();
        _label.font = TMP_Settings.defaultFontAsset;
        _label.fontSize = 26f;
        _label.color = Color.white;
        _label.alignment = TextAlignmentOptions.Center;
        _label.raycastTarget = false;
    }

    private void RestoreRenderers()
    {
        RestoreRendererVisibility();
        _hiddenRenderers = null;
    }

    private void RestoreRendererVisibility()
    {
        if (_hiddenRenderers == null) return;
        for (int i = 0; i < _hiddenRenderers.Length; i++)
            if (_hiddenRenderers[i] != null) _hiddenRenderers[i].forceRenderingOff = _previousRendererStates[i];
    }

    private void StopSpectating()
    {
        RestoreRenderers();
        RestoreTargetCamera();
        _target = null;
        _targetCamera = null;
        _camera = _ownCamera;
        if (_ownCamera != null)
        {
            _ownCamera.enabled = _ownCameraEnabled;
            AudioListener listener = _ownCamera.GetComponent<AudioListener>();
            if (listener != null) listener.enabled = _ownListenerEnabled;
            if (_presentation != null) _presentation.SetViewingCamera(_ownCamera);
        }
        if (_minimap != null) _minimap.SetTarget(transform);
        if (_arrow != null) _arrow.SetTarget(transform);
        if (_canvas != null) Destroy(_canvas.gameObject);
        _targetSelected = false;
        _started = false;
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= BeginCameraRendering;
        RenderPipelineManager.endCameraRendering -= EndCameraRendering;
        if (_started) StopSpectating();
    }
}
