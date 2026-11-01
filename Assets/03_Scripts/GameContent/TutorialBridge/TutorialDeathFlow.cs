using System.Collections.Generic;
using Fusion;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace Pawntom.TutorialBridge
{
    /// <summary>튜토리얼 전용 실패 연출과 현재 단계 재시도. 일반 게임의 관전/사망 규칙과 분리합니다.</summary>
    public sealed class TutorialDeathFlow : MonoBehaviour
    {
        private TutorialDirector _director;
        private TutorialFailureView _view;
        private TutorialStageCheckpoint _checkpoint;
        private PlayerData _player;
        private PlayerDeathPresentation _presentation;
        private Camera _sourceCamera;
        private Camera _failureCamera;
        private bool _sourceEnabled;
        private bool _listenerEnabled;
        private float _diedAt;
        private const float CameraRadius = 0.18f;
        private readonly RaycastHit[] _cameraHits = new RaycastHit[48];
        private readonly Collider[] _cameraOverlaps = new Collider[32];
        private static readonly float[] CameraYawOffsets = { 0f, 45f, -45f, 90f, -90f, 135f, -135f, 180f };
        private static readonly float[] CameraElevations = { 30f, 50f, 70f, 90f };
        private Bounds _corpseBounds;
        private Vector3 _cameraFocus;
        private float _nextCameraCheck;
        private Vector3 _cameraEnd;
        private Quaternion _rotationEnd;
        private Transform _body;
        private Vector3 _bodyPosition;
        private Quaternion _bodyRotation;
        private Animator _animator;
        private bool _animatorEnabled;
        private readonly Dictionary<Renderer, bool> _hands = new Dictionary<Renderer, bool>();
        private readonly Dictionary<PlayerStatusUI, bool> _portraits = new Dictionary<PlayerStatusUI, bool>();
        private bool _inputWasBlocked;

        public bool IsShowing { get; private set; }

        public void Build(TutorialDirector director, TutorialUIFactory factory)
        {
            _director = director;
            factory.Prewarm("Mission Failed" + TutorialFailureView.RetryPrompt);
            Canvas canvas = factory.CreateCanvas("Tutorial Mission Failed", 2500, transform);
            _view = canvas.gameObject.AddComponent<TutorialFailureView>();
            _view.Build(factory, Retry);
            if (EventSystem.current == null)
            {
                GameObject events = new GameObject("Tutorial Event System", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform, false);
            }
        }

        public void CaptureCheckpoint(int stage)
        {
            if (IsShowing || (_checkpoint != null && _checkpoint.Stage == stage)) return;
            Transform local = _director.LocalPlayerTransform;
            PlayerData player = local != null ? local.GetComponent<PlayerData>() : null;
            if (player == null || player.Object == null || !player.Object.IsValid || player.IsDead) return;
            _checkpoint = new TutorialStageCheckpoint(stage, player);
            Debug.Log($"[TutorialDeathFlow] {stage}단계 시작 체크포인트 저장: {_checkpoint.Position}");
        }

        private void Update()
        {
            if (_director == null || _view == null) return;
            Transform local = _director.LocalPlayerTransform;
            PlayerData player = local != null ? local.GetComponent<PlayerData>() : null;
            if (!IsShowing)
            {
                if (player == null || player.Object == null || !player.Object.IsValid) return;
                if (_checkpoint == null && !player.IsDead)
                    CaptureCheckpoint(_director.Sequencer.CurrentBeat?.Stage ?? _director.Sequencer.PendingMain?.Stage ?? 1);
                if (player.IsDead) BeginDeath(player);
                return;
            }
            if (_player == null || _player.Object == null || !_player.Object.IsValid) return;
            if (_failureCamera == null && Time.unscaledTime > _diedAt + 0.1f &&
                (_presentation != null ? _presentation.CanStartSpectating : Time.unscaledTime > _diedAt + 1.2f))
                ShowThirdPerson();
        }

        private void BeginDeath(PlayerData player)
        {
            IsShowing = true;
            _player = player;
            _presentation = player.GetComponent<PlayerDeathPresentation>();
            _sourceCamera = player.GetComponent<PlayerController>().ViewCamera;
            _sourceEnabled = _sourceCamera != null && _sourceCamera.enabled;
            AudioListener listener = _sourceCamera != null ? _sourceCamera.GetComponent<AudioListener>() : null;
            _listenerEnabled = listener != null && listener.enabled;
            _diedAt = Time.unscaledTime;
            _director.SuspendForDeath();
            foreach (PlayerStatusUI status in FindObjectsByType<PlayerStatusUI>(FindObjectsSortMode.None))
            {
                _portraits[status] = status.PortraitVisible;
                status.PortraitVisible = false;
            }
            _inputWasBlocked = GameplayInputBlocker.IsBlocked;
            GameplayInputBlocker.SetBlocked(true);
            _view.ShowDeath();
        }

        private void ShowThirdPerson()
        {
            if (_sourceCamera == null) return;
            foreach (Renderer renderer in _sourceCamera.GetComponentsInChildren<Renderer>(true))
            {
                _hands[renderer] = renderer.forceRenderingOff;
                renderer.forceRenderingOff = true;
            }

            Vector3 focus = PlayerDeathPlacement.GroundPoint(_player.DeathPosition, _player.transform, out _) + Vector3.up * 0.4f;
            _corpseBounds = new Bounds(focus, new Vector3(0.8f, 0.6f, 0.8f));
            PlayerAnimationView animationView = _player.GetComponent<PlayerAnimationView>();
            _body = animationView != null ? animationView.ModelRoot : null;
            if (_body != null)
            {
                _bodyPosition = _body.localPosition;
                _bodyRotation = _body.localRotation;
                _animator = _body.GetComponentInChildren<Animator>();
                if (_animator != null) { _animatorEnabled = _animator.enabled; _animator.enabled = false; }
                _body.rotation *= Quaternion.Euler(0f, 0f, 85f);
                Renderer[] renderers = _body.GetComponentsInChildren<Renderer>();
                Bounds bounds = new Bounds(_body.position, Vector3.zero);
                bool found = false;
                foreach (Renderer renderer in renderers)
                {
                    if (!found) { bounds = renderer.bounds; found = true; }
                    else bounds.Encapsulate(renderer.bounds);
                }
                Vector3 ground = PlayerDeathPlacement.GroundPoint(_player.DeathPosition, _player.transform, out _);
                if (found)
                {
                    // 발을 축으로 눕혔을 때 몸 중심이 벽 안으로 이동하지 않도록 사망 위치에 맞춥니다.
                    Vector3 lift = new Vector3(_player.DeathPosition.x - bounds.center.x,
                        ground.y + 0.03f - bounds.min.y, _player.DeathPosition.z - bounds.center.z);
                    _body.position += lift;
                    focus = bounds.center + lift;
                    _corpseBounds = new Bounds(focus, bounds.size);
                }
            }

            _cameraFocus = focus;
            // 모델을 눕히며 이동한 콜라이더도 이번 프레임의 카메라 검사에 반영합니다.
            Physics.SyncTransforms();
            FindFailureCameraPosition();
            GameObject cameraObject = new GameObject("Tutorial Failure Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.transform.SetParent(transform, false);
            _failureCamera = cameraObject.GetComponent<Camera>();
            _failureCamera.CopyFrom(_sourceCamera);
            _failureCamera.nearClipPlane = Mathf.Min(_sourceCamera.nearClipPlane, 0.1f);
            _failureCamera.enabled = true;
            // 몸 안에 있던 1인칭 위치에서 보간하면 전환 도중 몸을 통과하므로 안전한 외부 시점으로 컷합니다.
            _failureCamera.transform.SetPositionAndRotation(_cameraEnd, _rotationEnd);
            _sourceCamera.enabled = false;
            AudioListener listener = _sourceCamera.GetComponent<AudioListener>();
            if (listener != null) listener.enabled = false;
            _presentation?.SetViewingCamera(_failureCamera);
            _view.ShowFailure();
        }

        private void LateUpdate()
        {
            // 사망 처리의 팝업 정리 등이 입력 차단을 풀어도 실패 화면에서는 게임 조작을 차단합니다.
            // Fusion Render 뒤의 LateUpdate에서 확인해 실행 순서에 영향을 받지 않도록 합니다.
            if (IsShowing && (!GameplayInputBlocker.IsBlocked || Cursor.lockState != CursorLockMode.None || !Cursor.visible))
                GameplayInputBlocker.SetBlocked(true);
            if (_failureCamera == null) return;
            // 적이 실패 화면의 카메라 위치로 걸어 들어오는 경우도 다시 검사합니다.
            if (!IsCameraPositionFree(_cameraEnd) || Time.unscaledTime >= _nextCameraCheck)
            {
                _nextCameraCheck = Time.unscaledTime + 0.2f;
                Vector3 offset = _cameraEnd - _cameraFocus;
                if (!IsCameraPositionFree(_cameraEnd) ||
                    !TryCameraDirection(offset.normalized, offset.magnitude, out Vector3 clearPosition) ||
                    (clearPosition - _cameraEnd).sqrMagnitude > 0.01f)
                    FindFailureCameraPosition();
            }
            _failureCamera.transform.SetPositionAndRotation(_cameraEnd, _rotationEnd);
        }

        private void FindFailureCameraPosition()
        {
            float halfFov = _sourceCamera.fieldOfView * Mathf.Deg2Rad * 0.5f;
            halfFov = Mathf.Min(halfFov, Mathf.Atan(Mathf.Tan(halfFov) * Mathf.Max(0.1f, _sourceCamera.aspect)));
            float framingDistance = Mathf.Max(3.2f, _corpseBounds.extents.magnitude / Mathf.Sin(Mathf.Max(0.1f, halfFov)) * 1.1f);
            float baseYaw = _player.transform.eulerAngles.y + 125f;
            float bestScore = float.NegativeInfinity;
            Vector3 best = Vector3.zero;
            for (int pitch = 0; pitch < CameraElevations.Length; pitch++)
            {
                float elevation = CameraElevations[pitch] * Mathf.Deg2Rad;
                for (int yaw = 0; yaw < CameraYawOffsets.Length; yaw++)
                {
                    float heading = (baseYaw + CameraYawOffsets[yaw]) * Mathf.Deg2Rad;
                    Vector3 direction = new Vector3(Mathf.Sin(heading) * Mathf.Cos(elevation),
                        Mathf.Sin(elevation), Mathf.Cos(heading) * Mathf.Cos(elevation));
                    if (!TryCameraDirection(direction, framingDistance, out Vector3 candidate)) continue;
                    float score = Vector3.Distance(candidate, _cameraFocus) / framingDistance - pitch * 0.025f - yaw * 0.01f;
                    if (score <= bestScore) continue;
                    bestScore = score;
                    best = candidate;
                }
            }
            if (float.IsNegativeInfinity(bestScore))
            {
                // 공간이 극도로 좁아도 카메라를 시체 내부로 밀어 넣지 않습니다.
                // 마지막 외부 시점을 유지하고, 최초 선택이면 모델 경계 위를 사용합니다.
                best = _failureCamera != null && IsCameraPositionFree(_cameraEnd) ? _cameraEnd :
                    _cameraFocus + Vector3.up * Mathf.Max(2.4f, _corpseBounds.extents.y + CameraRadius + 0.25f);
            }
            _cameraEnd = best;
            Vector3 look = _cameraFocus - best;
            Vector3 up = Mathf.Abs(Vector3.Dot(look.normalized, Vector3.up)) > 0.98f ? _player.transform.forward : Vector3.up;
            _rotationEnd = Quaternion.LookRotation(look, up);
        }

        private bool TryCameraDirection(Vector3 direction, float preferredDistance, out Vector3 position)
        {
            position = default;
            if (direction.sqrMagnitude < 0.001f) return false;
            float distance = preferredDistance;
            int count = Physics.SphereCastNonAlloc(_cameraFocus, CameraRadius, direction, _cameraHits,
                preferredDistance, ~0, QueryTriggerInteraction.Ignore);
            if (count == _cameraHits.Length) return false;
            for (int i = 0; i < count; i++)
            {
                Collider collider = _cameraHits[i].collider;
                if (collider.transform.IsChildOf(_player.transform)) continue;
                // 접촉 중인 적의 콜라이더를 벽으로 취급해 시체 중심으로 줌인하지 않습니다.
                // 최종 카메라 위치가 다른 캐릭터 내부인지는 별도로 검사합니다.
                if (collider.GetComponentInParent<PlayerData>() != null ||
                    collider.GetComponentInParent<Pawntom.Enemy.Authoring.EnemyAgent>() != null) continue;
                distance = Mathf.Min(distance, _cameraHits[i].distance - 0.12f);
            }
            if (distance < 0.65f) return false;
            position = _cameraFocus + direction * distance;
            return IsCameraPositionFree(position);
        }

        private bool IsCameraPositionFree(Vector3 position)
        {
            Bounds exclusion = _corpseBounds;
            exclusion.Expand((CameraRadius + 0.12f) * 2f);
            if (exclusion.Contains(position)) return false;
            int count = Physics.OverlapSphereNonAlloc(position, CameraRadius, _cameraOverlaps, ~0, QueryTriggerInteraction.Ignore);
            if (count == _cameraOverlaps.Length) return false;
            for (int i = 0; i < count; i++)
                if (!_cameraOverlaps[i].transform.IsChildOf(_player.transform)) return false;
            return true;
        }

        private void Retry()
        {
            // 기존 튜토리얼의 솔로 Host 경로에서만 월드 상태를 되돌립니다.
            if (_checkpoint == null || _player == null || !_player.Object.HasStateAuthority ||
                System.Linq.Enumerable.Count(_player.Runner.ActivePlayers) > 1)
            {
                Debug.LogWarning("[TutorialDeathFlow] 단계 재시도는 솔로 튜토리얼 Host에서 실행해야 합니다.");
                _view.AllowRetry();
                return;
            }
            if (!_player.TryRestartTutorial(_checkpoint.Position, _checkpoint.Rotation))
            {
                _view.AllowRetry();
                return;
            }
            _checkpoint.RestoreWorld(_player);
            _presentation?.ResetTutorialPresentation();
            RestoreView();
            // 카메라 원위치 복구 뒤 시점을 정면으로 맞춥니다.
            _player.GetComponent<PlayerController>().ResetTutorialLook();
            IsShowing = false;
            _view.Hide();
            GameplayInputBlocker.SetBlocked(false);
            _director.ResumeStage(_checkpoint.Stage);
            Debug.Log($"[TutorialDeathFlow] {_checkpoint.Stage}단계 시작부터 재시도");
        }

        private void RestoreView()
        {
            if (_body != null) { _body.localPosition = _bodyPosition; _body.localRotation = _bodyRotation; }
            if (_animator != null) _animator.enabled = _animatorEnabled;
            foreach (var pair in _hands) if (pair.Key != null) pair.Key.forceRenderingOff = pair.Value;
            _hands.Clear();
            foreach (var pair in _portraits) if (pair.Key != null) pair.Key.PortraitVisible = pair.Value;
            _portraits.Clear();
            if (_failureCamera != null) { _failureCamera.enabled = false; Destroy(_failureCamera.gameObject); }
            _failureCamera = null;
            if (_sourceCamera != null)
            {
                _sourceCamera.enabled = _sourceEnabled;
                AudioListener listener = _sourceCamera.GetComponent<AudioListener>();
                if (listener != null) listener.enabled = _listenerEnabled;
            }
            _body = null;
            _animator = null;
        }

        private void OnDisable()
        {
            if (!IsShowing) return;
            _presentation?.SetViewingCamera(_sourceCamera);
            RestoreView();
            GameplayInputBlocker.SetBlocked(_inputWasBlocked);
            IsShowing = false;
        }
    }
}
