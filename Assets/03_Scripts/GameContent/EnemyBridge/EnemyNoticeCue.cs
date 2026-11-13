using System.Collections.Generic;
using UnityEngine;

namespace Pawntom.EnemyBridge
{
    /// <summary>
    /// 경비견이 발자국을 알아챈 순간을 눈에 띄게 보여 주는 연출: 머리 위에 "!" 가 톡 튀어 오르고, 몸이 제자리에서 깡충 뛴다.
    /// <para>
    /// 판단에는 관여하지 않는다. <see cref="NetworkEnemyAgent.TraceNoticeCount"/> 가 늘어났는지만 보고 모든 피어에서 같은 순간에 띄운다.
    /// 씬·프리팹을 고치지 않도록 <see cref="NetworkEnemyAgent"/> 가 깨어날 때 스스로 붙인다.
    /// 깡충은 개 루트(내비 에이전트)가 아니라 렌더러가 든 자식 오브젝트만 위로 올려서 이동·충돌에 영향이 없다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyNoticeCue : MonoBehaviour
    {
        private const float CueSeconds = 1.3f;
        private const float HopSeconds = 0.38f;
        private const float HopHeight = 0.28f;      // m (월드)
        private const float MarkHeight = 0.55f;     // 머리 위 높이(m)
        private const float MarkWorldSize = 0.9f;   // 느낌표 글자 높이(m, 대략)
        private static readonly Color MarkColor = new Color(1f, 0.82f, 0.18f, 1f);   // 발자국 발견: 노랑
        private static readonly Color SpotColor = new Color(1f, 0.15f, 0.12f, 1f);   // 플레이어 발견: 빨강

        private NetworkEnemyAgent _agent;
        private SpriteRenderer _mark;
        private static Sprite _markSprite;
        private static Camera _viewCamera;
        private static float _nextCameraSearch;
        private Transform _markTransform;

        private readonly List<Transform> _hopTargets = new List<Transform>(4);
        private readonly List<Vector3> _hopBase = new List<Vector3>(4);

        private int _lastCount = -1;
        private int _lastSpot;
        private Color _currentColor = new Color(1f, 0.82f, 0.18f, 1f);
        private float _time = -1f;
        private bool _hopApplied;

        private void Awake()
        {
            _agent = GetComponent<NetworkEnemyAgent>();
        }

        private void OnDisable()
        {
            RestoreHop();

            if (_markTransform != null)
            {
                _markTransform.gameObject.SetActive(false);
            }

            _time = -1f;
        }

        private void OnDestroy()
        {
            if (_markTransform != null)
            {
                Destroy(_markTransform.gameObject);
            }
        }

        private void Update()
        {
            if (_agent == null || _agent.Object == null || !_agent.Object.IsValid)
            {
                return;
            }

            int count = _agent.TraceNoticeCount;
            int spot = _agent.SpotNoticeCount;

            if (_lastCount < 0 || count < _lastCount || spot < _lastSpot)
            {
                // 처음 붙었거나 두뇌가 다시 만들어져 숫자가 줄었다 — 연출 없이 맞춘다.
                _lastCount = count;
                _lastSpot = spot;
            }
            else
            {
                // 플레이어를 직접 발견한 것이 발자국보다 중요하다: 같은 프레임이면 빨간 느낌표가 이긴다.
                if (spot > _lastSpot)
                {
                    _lastSpot = spot;
                    _lastCount = count;
                    Play(SpotColor);
                }
                else if (count > _lastCount)
                {
                    _lastCount = count;
                    Play(MarkColor);
                }
            }

            if (_time >= 0f)
            {
                Advance(Time.deltaTime);
            }
        }

        private void Play(Color color)
        {
            _currentColor = color;
            EnsureMark();
            CacheHopTargets();
            RestoreHop();
            _time = 0f;
            if (_markTransform != null)
            {
                _markTransform.gameObject.SetActive(true);
            }
            else
            {
                _time = -1f;
            }
        }

        private void Advance(float deltaTime)
        {
            // 느낌표가 장면 정리 등으로 사라졌으면 연출을 접는다(매 프레임 예외를 내지 않는다).
            if (_markTransform == null || _mark == null)
            {
                _markTransform = null;
                _mark = null;
                _time = -1f;
                RestoreHop();
                return;
            }

            _time += deltaTime;

            if (_time >= CueSeconds)
            {
                _time = -1f;
                RestoreHop();
                _markTransform.gameObject.SetActive(false);
                return;
            }

            // 느낌표: 0.18초 동안 0→1.25→1 로 톡 튀어 오르고, 머무는 동안 살짝 통통 뛰며, 끝 0.3초에 사라진다.
            float pop = Mathf.Clamp01(_time / 0.18f);
            float popScale = pop < 1f ? Mathf.Sin(pop * Mathf.PI * 0.5f) * 1.25f : 1f + 0.25f * Mathf.Max(0f, 1f - (_time - 0.18f) / 0.15f);
            float bob = Mathf.Sin(_time * 14f) * 0.04f * Mathf.Clamp01(1f - _time / CueSeconds);
            float fade = Mathf.Clamp01((CueSeconds - _time) / 0.3f);

            // 느낌표는 개의 자식이 아니라 씬 루트라 월드 크기 그대로 쓴다.
            _markTransform.localScale = Vector3.one * popScale;
            _markTransform.position = HeadPosition() + Vector3.up * bob;

            // 느낌표는 플레이어 화면(렌더 텍스처가 아닌 카메라)을 바라보게 한다. 멀리서도 보이도록 거리에 따라 조금 키운다.
            Camera camera = FindViewCamera();

            if (camera != null)
            {
                _markTransform.rotation = camera.transform.rotation;
                float distance = Vector3.Distance(camera.transform.position, _markTransform.position);
                _markTransform.localScale = Vector3.one * (popScale * Mathf.Clamp(distance / 7f, 1f, 3.5f));
            }

            Color color = _currentColor;
            color.a = fade;
            _mark.color = color;

            ApplyHop();
        }

        // 깡충: 처음 0.38초 동안 위로 올랐다 내려온다(포물선).
        private void ApplyHop()
        {
            if (_time > HopSeconds)
            {
                RestoreHop();
                return;
            }

            float u = _time / HopSeconds;
            float lift = 4f * u * (1f - u) * HopHeight;
            float parentScale = Mathf.Max(0.0001f, transform.lossyScale.y);

            for (int i = 0; i < _hopTargets.Count; i++)
            {
                if (_hopTargets[i] != null)
                {
                    _hopTargets[i].localPosition = _hopBase[i] + Vector3.up * (lift / parentScale);
                }
            }

            _hopApplied = true;
        }

        private void RestoreHop()
        {
            if (!_hopApplied)
            {
                return;
            }

            for (int i = 0; i < _hopTargets.Count; i++)
            {
                if (_hopTargets[i] != null)
                {
                    _hopTargets[i].localPosition = _hopBase[i];
                }
            }

            _hopApplied = false;
        }

        // 렌더러를 가진 직계 자식을 올린다(자식이 없으면 깡충은 생략하고 느낌표만 띄운다).
        private void CacheHopTargets()
        {
            if (_hopTargets.Count > 0)
            {
                return;
            }

            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);

                if (child == _markTransform || child.GetComponentInChildren<Renderer>(true) == null)
                {
                    continue;
                }

                _hopTargets.Add(child);
                _hopBase.Add(child.localPosition);
            }
        }

        // 보이는 모양의 가장 윗부분 바로 위.
        private Vector3 HeadPosition()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            float top = transform.position.y;
            bool found = false;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];

                if (renderer == null || !renderer.enabled || (_markTransform != null && renderer.transform.IsChildOf(_markTransform)))
                {
                    continue;
                }

                float max = renderer.bounds.max.y;

                if (!found || max > top)
                {
                    top = max;
                    found = true;
                }
            }

            // 깡충 중에는 몸이 올라가 있으므로 머리 위치도 따라 올라간다(렌더러 경계가 이미 반영한다).
            return new Vector3(transform.position.x, top + MarkHeight, transform.position.z);
        }

        private void EnsureMark()
        {
            if (_markTransform != null && _mark != null)
            {
                return;
            }

            GameObject mark = new GameObject("TraceNoticeMark");

            // 개와 같은 장면에 둔다. 활성 장면이 다르면 그 장면이 닫힐 때 느낌표만 먼저 사라져 예외가 난다.
            if (gameObject.scene.IsValid() && gameObject.scene.isLoaded)
            {
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(mark, gameObject.scene);
            }

            _markTransform = mark.transform; // 개의 자식으로 두지 않는다(스케일·깡충·렌더러 계산에 섞이지 않게)

            // 글꼴에 기대지 않고 그림으로 만든 느낌표 스프라이트. 스프라이트는 양면이 보여서 뒤에서 봐도 사라지지 않는다.
            _mark = mark.AddComponent<SpriteRenderer>();
            _mark.sprite = GetMarkSprite();
            _mark.color = MarkColor;
            _mark.sortingOrder = 100;
            _mark.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _mark.receiveShadows = false;

            mark.SetActive(false);
        }

        // 지금 플레이어 화면을 그리는 카메라(렌더 텍스처 대상이 아닌, 켜진 것 중 depth 가 가장 높은 것). 1초에 한 번만 다시 찾는다.
        private static Camera FindViewCamera()
        {
            if (_viewCamera != null && _viewCamera.isActiveAndEnabled && Time.unscaledTime < _nextCameraSearch)
            {
                return _viewCamera;
            }

            _nextCameraSearch = Time.unscaledTime + 1f;
            Camera best = null;
            Camera[] cameras = Camera.allCameras;

            for (int i = 0; i < cameras.Length; i++)
            {
                Camera candidate = cameras[i];

                if (candidate == null || candidate.targetTexture != null || candidate.cameraType != CameraType.Game)
                {
                    continue;
                }

                if (best == null || candidate.depth > best.depth)
                {
                    best = candidate;
                }
            }

            _viewCamera = best != null ? best : Camera.main;
            return _viewCamera;
        }

        // 두꺼운 느낌표(위쪽이 넓은 막대 + 점). 흰 속 + 어두운 테두리라 색(SpriteRenderer.color)을 곱해 노랑·빨강으로 쓴다.
        private static Sprite GetMarkSprite()
        {
            if (_markSprite != null)
            {
                return _markSprite;
            }

            const int width = 64;
            const int height = 128;
            const float outline = 5f;
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, true)
            {
                name = "NoticeMark",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };

            Color32[] pixels = new Color32[width * height];
            Color32 edge = new Color32(40, 12, 0, 255);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float px = x + 0.5f;
                    float py = y + 0.5f;

                    // 막대: 아래 y=48(반지름 8) → 위 y=108(반지름 15)로 굵어지는 원들의 합집합.
                    float best = float.MaxValue;

                    for (int s = 0; s <= 24; s++)
                    {
                        float u = s / 24f;
                        float cy = Mathf.Lerp(48f, 108f, u);
                        float r = Mathf.Lerp(8f, 15f, u);
                        float d = Mathf.Sqrt((px - 32f) * (px - 32f) + (py - cy) * (py - cy)) - r;
                        best = Mathf.Min(best, d);
                    }

                    // 점
                    float dot = Mathf.Sqrt((px - 32f) * (px - 32f) + (py - 20f) * (py - 20f)) - 11f;
                    best = Mathf.Min(best, dot);

                    float inside = Mathf.Clamp01(0.5f - best);
                    float outer = Mathf.Clamp01(outline + 0.5f - best);

                    if (outer <= 0f)
                    {
                        pixels[y * width + x] = new Color32(0, 0, 0, 0);
                        continue;
                    }

                    Color32 c = Color32.Lerp(edge, new Color32(255, 255, 255, 255), inside);
                    c.a = (byte)(outer * 255f);
                    pixels[y * width + x] = c;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(true, true);

            // 높이가 MarkWorldSize(m)가 되도록 픽셀 밀도를 잡는다.
            _markSprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0f), height / MarkWorldSize);
            _markSprite.hideFlags = HideFlags.HideAndDontSave;
            return _markSprite;
        }
    }
}
