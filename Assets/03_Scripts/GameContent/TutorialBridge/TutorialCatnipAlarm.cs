using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Pawntom.TutorialBridge
{
    /// <summary>
    /// 캣닢을 손에 넣은 순간의 "비상" 연출. 금고가 부서지며 사라지고, 사이렌이 울리고, 화면 전체가 붉게 깜박인다.
    /// <para>
    /// 금고(<see cref="TutorialVault"/>)의 <c>onOpened</c> 이벤트에 <see cref="Trigger"/> 를 연결해 쓴다.
    /// 금고 코드/튜토리얼 진행 신호는 건드리지 않는다.
    /// </para>
    /// <para>
    /// 붉은 화면은 런타임에 만든 전역 Volume(색상 필터 + 비네트 + 블룸)이다. 플레이어 카메라는 평소에
    /// 후처리를 꺼 두므로, 경보가 시작될 때만 켜고 끝나면(<see cref="Stop"/>/파괴) 원래대로 돌려 둔다.
    /// 사이렌 소리는 에셋 없이 코드로 합성한다(<see cref="TutorialSfxSynth"/> 와 같은 방식).
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Pawntom/Tutorial/Tutorial Catnip Alarm")]
    public sealed class TutorialCatnipAlarm : MonoBehaviour
    {
        private const int SirenSampleRate = 22050;

        [Header("부술 금고")]
        [Tooltip("경보와 함께 부서져 사라질 금고 루트(TutorialVault_Main)")]
        [SerializeField] private GameObject vaultRoot;
        [SerializeField, Min(0f)] private float shakeSeconds = 0.45f;
        [SerializeField, Min(0f)] private float shakeAmount = 0.05f;
        [SerializeField, Range(0, 40)] private int debrisCount = 22;

        [Header("붉은 화면")]
        [SerializeField] private Color filterColor = new Color(1f, 0.13f, 0.1f, 1f);
        [SerializeField] private float postExposure = 0.5f;
        [SerializeField] private Color vignetteColor = new Color(0.25f, 0f, 0f, 1f);
        [SerializeField, Range(0f, 1f)] private float vignetteIntensity = 0.36f;
        [SerializeField, Range(0f, 1f)] private float bloomIntensity = 0.6f;
        [Tooltip("화면이 가장 약해지는 때의 세기(1이면 깜박이지 않는다)")]
        [SerializeField, Range(0f, 1f)] private float pulseMinWeight = 0.62f;
        [SerializeField, Min(0.1f)] private float fadeInSeconds = 0.7f;

        [Header("사이렌")]
        [SerializeField, Range(0f, 1f)] private float sirenVolume = 0.32f;
        [Tooltip("사이렌이 한 번 올라갔다 내려오는 길이(초). 화면 깜박임도 이 주기를 따른다")]
        [SerializeField, Min(0.5f)] private float sirenCycleSeconds = 2.4f;

        private bool _triggered;
        private bool _stopped;
        private Volume _volume;
        private VolumeProfile _profile;
        private AudioSource _siren;
        private AudioClip _sirenClip;
        private UniversalAdditionalCameraData _cameraData;
        private bool _cameraPostWas;
        private Renderer[] _vaultRenderers;
        private Collider[] _vaultColliders;
        private Vector3 _vaultHome;

        public bool IsRunning
        {
            get { return _triggered && !_stopped; }
        }

        /// <summary>금고 onOpened 에 연결한다. 한 번만 동작한다.</summary>
        public void Trigger()
        {
            if (_triggered)
            {
                return;
            }

            _triggered = true;
            StartCoroutine(RunAlarm());
            if (vaultRoot != null)
            {
                StartCoroutine(BreakVault());
            }
        }

        /// <summary>경보를 끈다(화면과 소리를 서서히 되돌린다).</summary>
        public void Stop()
        {
            _stopped = true;
        }

        private void OnEnable()
        {
            TutorialDirector.LocalPlayerDied += HandlePlayerDied;
            TutorialDirector.StageResumed += HandleStageResumed;
        }

        private void OnDisable()
        {
            TutorialDirector.LocalPlayerDied -= HandlePlayerDied;
            TutorialDirector.StageResumed -= HandleStageResumed;
        }

        // 사망 재시도로 단계가 다시 시작되면, 이 연출이 부순 금고를 되살리고 경보를 다시 쓸 수 있게 되돌린다.
        // 부서진 금고는 비활성이라 단계 체크포인트(TutorialStageCheckpoint)의 금고 복구 대상에 안 잡힌다.
        private void HandleStageResumed()
        {
            // 경보를 켠 적이 없어도 금고가 숨겨진 채라면(경보 중 죽어 상태가 어긋난 경우) 되살린다.
            if (_triggered || (vaultRoot != null && !vaultRoot.activeSelf))
            {
                ResetForRetry();
                Debug.Log("[TutorialCatnipAlarm] 단계 재시도: 금고를 되살리고 경보를 초기화했습니다.");
            }
        }

        /// <summary>부서진 금고를 되살리고 경보를 처음 상태로 되돌린다. 금고는 닫힌 상태로 돌아온다.</summary>
        public void ResetForRetry()
        {
            _triggered = false;
            _stopped = false;
            StopAllCoroutines();
            Cleanup();

            if (vaultRoot == null)
            {
                return;
            }

            vaultRoot.transform.position = _vaultHome;
            vaultRoot.SetActive(true);

            if (_vaultRenderers != null)
            {
                foreach (Renderer r in _vaultRenderers)
                {
                    if (r != null)
                    {
                        r.enabled = true;
                    }
                }
            }

            if (_vaultColliders != null)
            {
                foreach (Collider c in _vaultColliders)
                {
                    if (c != null)
                    {
                        c.enabled = true;
                    }
                }
            }

            TutorialVault vault = vaultRoot.GetComponent<TutorialVault>();
            if (vault != null)
            {
                vault.ResetForRetry();
            }
        }

        // 죽으면 경보를 끈다. 새로 깨어난 뒤에도 사이렌·붉은 화면이 남지 않게 한다.
        private void HandlePlayerDied()
        {
            if (IsRunning)
            {
                Stop();
            }
        }

        private void OnDestroy()
        {
            Cleanup();
        }

        // ───────── 경보(화면 + 소리) ─────────

        private IEnumerator RunAlarm()
        {
            BuildVolume();
            StartSiren();

            float start = Time.unscaledTime;
            float fade = 0f;

            while (!_stopped)
            {
                float t = Time.unscaledTime - start;
                fade = Mathf.Min(1f, fade + Time.unscaledDeltaTime / fadeInSeconds);

                // 사이렌이 가장 높을 때(주기의 절반) 가장 밝다.
                float wave = 0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * t / sirenCycleSeconds);
                float weight = Mathf.Lerp(pulseMinWeight, 1f, wave);

                if (_volume != null)
                {
                    _volume.weight = weight * Mathf.SmoothStep(0f, 1f, fade);
                }

                if (_siren != null)
                {
                    _siren.volume = sirenVolume * fade;
                }

                yield return null;
            }

            // 서서히 꺼짐
            float outTime = 0.8f;
            for (float e = 0f; e < outTime; e += Time.unscaledDeltaTime)
            {
                float k = 1f - e / outTime;
                if (_volume != null)
                {
                    _volume.weight *= k;
                }

                if (_siren != null)
                {
                    _siren.volume = sirenVolume * k;
                }

                yield return null;
            }

            Cleanup();
        }

        private void BuildVolume()
        {
            _profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _profile.name = "CatnipAlarmProfile";

            ColorAdjustments adjust = _profile.Add<ColorAdjustments>(true);
            adjust.colorFilter.Override(filterColor);
            adjust.postExposure.Override(postExposure);
            adjust.saturation.Override(25f);
            adjust.contrast.Override(15f);

            Vignette vignette = _profile.Add<Vignette>(true);
            vignette.color.Override(vignetteColor);
            vignette.intensity.Override(vignetteIntensity);
            vignette.smoothness.Override(0.65f);

            Bloom bloom = _profile.Add<Bloom>(true);
            bloom.threshold.Override(0.85f);
            bloom.intensity.Override(bloomIntensity);
            bloom.scatter.Override(0.6f);
            bloom.tint.Override(new Color(1f, 0.45f, 0.4f, 1f));

            GameObject go = new GameObject("CatnipAlarmVolume");
            go.transform.SetParent(transform, false);
            _volume = go.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = 200f;
            _volume.weight = 0f;
            _volume.sharedProfile = _profile;

            EnableCameraPostProcessing();
        }

        // 플레이어 카메라는 후처리가 꺼져 있다. 경보 동안만 켠다.
        private void EnableCameraPostProcessing()
        {
            Camera cam = FindPlayerCamera();
            if (cam == null)
            {
                return;
            }

            _cameraData = cam.GetUniversalAdditionalCameraData();
            _cameraPostWas = _cameraData.renderPostProcessing;
            _cameraData.renderPostProcessing = true;
        }

        private static Camera FindPlayerCamera()
        {
            TutorialDirector director = TutorialDirector.Active;
            Transform player = director != null ? director.LocalPlayerTransform : null;
            if (player != null)
            {
                foreach (Camera cam in player.GetComponentsInChildren<Camera>(true))
                {
                    if (cam.targetTexture == null)
                    {
                        return cam;
                    }
                }
            }

            Camera main = Camera.main;
            return main != null && main.targetTexture == null ? main : null;
        }

        private void Cleanup()
        {
            if (_cameraData != null)
            {
                _cameraData.renderPostProcessing = _cameraPostWas;
                _cameraData = null;
            }

            if (_volume != null)
            {
                Destroy(_volume.gameObject);
                _volume = null;
            }

            if (_profile != null)
            {
                Destroy(_profile);
                _profile = null;
            }

            if (_siren != null)
            {
                _siren.Stop();
                Destroy(_siren);
                _siren = null;
            }

            if (_sirenClip != null)
            {
                Destroy(_sirenClip);
                _sirenClip = null;
            }
        }

        // ───────── 사이렌 합성 ─────────

        private void StartSiren()
        {
            _sirenClip = BuildSirenClip(sirenCycleSeconds);
            _siren = gameObject.AddComponent<AudioSource>();
            _siren.clip = _sirenClip;
            _siren.loop = true;
            _siren.playOnAwake = false;
            _siren.spatialBlend = 0f;
            _siren.volume = 0f;
            _siren.Play();
        }

        /// <summary>650~1100Hz 를 사인 곡선으로 오르내리는 경보음. 한 주기가 정확히 정수 파장이라 이음매 소리가 없다.</summary>
        private static AudioClip BuildSirenClip(float cycleSeconds)
        {
            int count = Mathf.RoundToInt(cycleSeconds * SirenSampleRate);
            float[] data = new float[count];
            float[] phase = new float[count];

            double total = 0.0;
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / count;
                float freq = 650f + 450f * (0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * t));
                phase[i] = (float)total;
                total += 2.0 * Mathf.PI * freq / SirenSampleRate;
            }

            double cycles = System.Math.Max(1.0, System.Math.Round(total / (2.0 * Mathf.PI)));
            float scale = (float)(cycles * 2.0 * Mathf.PI / total);

            for (int i = 0; i < count; i++)
            {
                float p = phase[i] * scale;
                float s = Mathf.Sin(p) + 0.38f * Mathf.Sin(2f * p) + 0.18f * Mathf.Sin(3f * p);
                data[i] = (float)System.Math.Tanh(s * 0.9f) * 0.7f;
            }

            AudioClip clip = AudioClip.Create("CatnipSiren", count, 1, SirenSampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // ───────── 금고 파괴 ─────────

        private IEnumerator BreakVault()
        {
            Transform root = vaultRoot.transform;
            Vector3 home = root.position;
            _vaultHome = home;
            Renderer[] renderers = vaultRoot.GetComponentsInChildren<Renderer>();
            _vaultRenderers = renderers;
            _vaultColliders = vaultRoot.GetComponentsInChildren<Collider>();
            Bounds bounds = renderers.Length > 0 ? renderers[0].bounds : new Bounds(home, Vector3.one);
            Material debrisMaterial = renderers.Length > 0 ? renderers[0].sharedMaterial : null;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            // 부서지기 직전 흔들림
            for (float e = 0f; e < shakeSeconds; e += Time.deltaTime)
            {
                float k = 1f - e / shakeSeconds * 0.4f;
                root.position = home + Random.insideUnitSphere * (shakeAmount * k);
                yield return null;
            }

            root.position = home;

            foreach (Collider col in vaultRoot.GetComponentsInChildren<Collider>())
            {
                col.enabled = false;
            }

            foreach (Renderer r in renderers)
            {
                r.enabled = false;
            }

            SpawnFlash(bounds.center);
            SpawnDebris(bounds, debrisMaterial);
            // 부수지만 지우지 않는다 - 죽어서 이 미션으로 돌아오면 되살려야 한다.
            vaultRoot.SetActive(false);
        }

        private static void SpawnFlash(Vector3 position)
        {
            GameObject go = new GameObject("CatnipBreakFlash");
            go.transform.position = position;
            Light light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.75f, 0.5f);
            light.range = 9f;
            light.intensity = 14f;
            light.shadows = LightShadows.None;
            go.AddComponent<FlashFade>();
        }

        private void SpawnDebris(Bounds bounds, Material material)
        {
            for (int i = 0; i < debrisCount; i++)
            {
                GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
                piece.name = "VaultDebris";
                Destroy(piece.GetComponent<Collider>());

                float size = Random.Range(0.08f, 0.28f);
                piece.transform.localScale = new Vector3(size, size * Random.Range(0.4f, 1f), size * Random.Range(0.6f, 1.2f));
                piece.transform.position = bounds.center + new Vector3(
                    Random.Range(-bounds.extents.x, bounds.extents.x),
                    Random.Range(-bounds.extents.y * 0.6f, bounds.extents.y),
                    Random.Range(-bounds.extents.z, bounds.extents.z));
                piece.transform.rotation = Random.rotation;

                MeshRenderer mr = piece.GetComponent<MeshRenderer>();
                if (material != null)
                {
                    mr.sharedMaterial = material;
                }

                mr.shadowCastingMode = ShadowCastingMode.Off;

                Vector3 outward = (piece.transform.position - bounds.center).normalized;
                Vector3 velocity = outward * Random.Range(1.5f, 4.5f) + Vector3.up * Random.Range(1.5f, 4.5f);
                piece.AddComponent<DebrisPiece>().Launch(velocity, Random.onUnitSphere * Random.Range(120f, 420f));
            }
        }

        /// <summary>부서진 파편. 물리 없이 포물선으로 날아가 바닥에서 멈췄다가 서서히 작아져 사라진다.</summary>
        private sealed class DebrisPiece : MonoBehaviour
        {
            private const float FloorY = 0.06f;
            private Vector3 _velocity;
            private Vector3 _spin;
            private float _age;
            private Vector3 _startScale;

            public void Launch(Vector3 velocity, Vector3 spin)
            {
                _velocity = velocity;
                _spin = spin;
                _startScale = transform.localScale;
            }

            private void Update()
            {
                _age += Time.deltaTime;

                if (transform.position.y > FloorY || _velocity.y > 0f)
                {
                    _velocity += Physics.gravity * Time.deltaTime;
                    transform.position += _velocity * Time.deltaTime;
                    transform.Rotate(_spin * Time.deltaTime, Space.World);

                    if (transform.position.y <= FloorY && _velocity.y < 0f)
                    {
                        Vector3 p = transform.position;
                        p.y = FloorY;
                        transform.position = p;
                        _velocity = new Vector3(_velocity.x * 0.35f, 0f, _velocity.z * 0.35f);
                        _spin *= 0.2f;
                    }
                }

                if (_age > 3.2f)
                {
                    float k = Mathf.Clamp01(1f - (_age - 3.2f) / 1.2f);
                    transform.localScale = _startScale * k;
                    if (k <= 0f)
                    {
                        Destroy(gameObject);
                    }
                }
            }
        }

        /// <summary>부서지는 순간의 섬광. 0.5초 동안 꺼진다.</summary>
        private sealed class FlashFade : MonoBehaviour
        {
            private Light _light;
            private float _start;
            private float _initial;

            private void Awake()
            {
                _light = GetComponent<Light>();
                _initial = _light.intensity;
                _start = Time.time;
            }

            private void Update()
            {
                float k = 1f - (Time.time - _start) / 0.5f;
                if (k <= 0f)
                {
                    Destroy(gameObject);
                    return;
                }

                _light.intensity = _initial * k * k;
            }
        }
    }
}
