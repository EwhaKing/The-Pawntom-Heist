using Pawntom.Tutorial.Core;
using UnityEngine;

namespace Pawntom.TutorialBridge
{
    /// <summary>
    /// 튜토리얼 효과음 재생. 프로젝트에 무전·타자음 에셋이 없어 <see cref="TutorialSfxSynth"/>로 만든 클립을 기본으로 쓰고,
    /// 인스펙터에 클립을 넣으면 그것으로 대체한다(디렉터를 씬에 직접 배치했을 때).
    /// 모든 소리는 2D(공간감 없음)다 — 무전은 귀에 직접 들리는 소리이기 때문이다.
    /// </summary>
    public sealed class TutorialAudio : MonoBehaviour
    {
        private const int VariantCount = 3;

        [Header("대체 클립 (비우면 코드로 합성)")]
        [SerializeField] private AudioClip commandBlipOverride;
        [SerializeField] private AudioClip catBlipOverride;
        [SerializeField] private AudioClip staticOverride;
        [SerializeField] private AudioClip squelchOverride;
        [SerializeField] private AudioClip heartbeatOverride;

        [Header("볼륨")]
        [SerializeField, Range(0f, 1f)] private float blipVolume = 0.55f;
        [SerializeField, Range(0f, 1f)] private float staticVolume = 0.5f;
        [SerializeField, Range(0f, 1f)] private float ambientStaticVolume = 0.12f;
        [SerializeField, Range(0f, 1f)] private float heartbeatVolume = 0.8f;

        [Header("출력음 간격")]
        [Tooltip("글자가 빠르게 찍혀도 출력음은 이 간격보다 촘촘히 울리지 않는다.")]
        [SerializeField, Min(0f)] private float minBlipInterval = 0.045f;

        private AudioSource _blipSource;
        private AudioSource _sfxSource;
        private AudioSource _ambientSource;

        private readonly AudioClip[] _commandBlips = new AudioClip[VariantCount];
        private readonly AudioClip[] _catBlips = new AudioClip[VariantCount];
        private AudioClip _staticLong;
        private AudioClip _staticShort;
        private AudioClip _staticLoop;
        private AudioClip _squelch;
        private AudioClip _heartbeat;

        private float _nextBlipTime;
        private int _blipCursor;
        private float _ambientTarget;

        private void Awake()
        {
            _blipSource = CreateSource("Blip");
            _sfxSource = CreateSource("Sfx");
            _ambientSource = CreateSource("Ambient");
            _ambientSource.loop = true;
            _ambientSource.volume = 0f;

            BuildClips();
        }

        private void Update()
        {
            if (_ambientSource.clip == null)
            {
                return;
            }

            float volume = Mathf.MoveTowards(_ambientSource.volume, _ambientTarget, Time.unscaledDeltaTime * 0.5f);
            _ambientSource.volume = volume;

            if (volume <= 0f && _ambientTarget <= 0f && _ambientSource.isPlaying)
            {
                _ambientSource.Stop();
            }
        }

        private void OnDestroy()
        {
            DestroyGenerated(_commandBlips);
            DestroyGenerated(_catBlips);
            DestroyGenerated(_staticLong, _staticShort, _staticLoop, _squelch, _heartbeat);
        }

        /// <summary>글자 하나 출력음. 화자별 음색, 매번 음정을 조금씩 흔든다.</summary>
        public void PlayBlip(TutorialSpeaker speaker)
        {
            float now = Time.unscaledTime;

            if (now < _nextBlipTime)
            {
                return;
            }

            _nextBlipTime = now + minBlipInterval;
            _blipCursor = (_blipCursor + 1) % VariantCount;

            AudioClip clip = speaker == TutorialSpeaker.Command ? _commandBlips[_blipCursor] : _catBlips[_blipCursor];

            if (clip == null)
            {
                return;
            }

            _blipSource.pitch = Random.Range(0.94f, 1.07f);
            _blipSource.PlayOneShot(clip, blipVolume);
        }

        /// <summary>무전 잡음 한 덩어리. long=대사 개시("칙..."), short=글리치 순간.</summary>
        public void PlayStatic(bool longBurst)
        {
            AudioClip clip = longBurst ? _staticLong : _staticShort;

            if (clip != null)
            {
                _sfxSource.PlayOneShot(clip, staticVolume * (longBurst ? 1f : 0.6f));
            }
        }

        public void PlaySquelch()
        {
            if (_squelch != null)
            {
                _sfxSource.PlayOneShot(_squelch, staticVolume);
            }
        }

        public void PlayHeartbeat(float volumeScale)
        {
            if (_heartbeat != null)
            {
                _sfxSource.PlayOneShot(_heartbeat, heartbeatVolume * volumeScale);
            }
        }

        /// <summary>잔잔한 무전 배경 잡음. 0이면 서서히 끈다.</summary>
        public void SetAmbientStatic(float level)
        {
            _ambientTarget = Mathf.Clamp01(level) * ambientStaticVolume;

            if (_ambientTarget > 0f && !_ambientSource.isPlaying && _ambientSource.clip != null)
            {
                _ambientSource.Play();
            }
        }

        private AudioSource CreateSource(string sourceName)
        {
            GameObject go = new GameObject(sourceName);
            go.transform.SetParent(transform, false);

            AudioSource source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.ignoreListenerPause = true;
            return source;
        }

        private void BuildClips()
        {
            int rate = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 44100;

            // 지휘부: 낮고 거친 사각파 + 잡음(무전 수신). 깜냥이: 높고 부드러운 톤.
            float[] commandFreqs = { 470f, 520f, 575f };
            float[] catFreqs = { 700f, 760f, 830f };

            for (int i = 0; i < VariantCount; i++)
            {
                _commandBlips[i] = commandBlipOverride != null
                    ? commandBlipOverride
                    : MakeClip("TutorialBlip_Command_" + i, TutorialSfxSynth.Blip(rate, commandFreqs[i], 0.045d, 0.65d, 0.18d, 11 + i), rate);

                _catBlips[i] = catBlipOverride != null
                    ? catBlipOverride
                    : MakeClip("TutorialBlip_Cat_" + i, TutorialSfxSynth.Blip(rate, catFreqs[i], 0.038d, 0.25d, 0.02d, 31 + i), rate);
            }

            _staticLong = staticOverride != null ? staticOverride : MakeClip("TutorialStatic_Long", TutorialSfxSynth.Static(rate, 0.42d, 7, 55d), rate);
            _staticShort = staticOverride != null ? staticOverride : MakeClip("TutorialStatic_Short", TutorialSfxSynth.Static(rate, 0.09d, 8, 120d), rate);
            _staticLoop = MakeClip("TutorialStatic_Loop", TutorialSfxSynth.Static(rate, 2.0d, 9, 18d), rate);
            _squelch = squelchOverride != null ? squelchOverride : MakeClip("TutorialSquelch", TutorialSfxSynth.Squelch(rate, 5), rate);
            _heartbeat = heartbeatOverride != null ? heartbeatOverride : MakeClip("TutorialHeartbeat", TutorialSfxSynth.Heartbeat(rate), rate);

            _ambientSource.clip = _staticLoop;
        }

        private static AudioClip MakeClip(string clipName, float[] samples, int rate)
        {
            AudioClip clip = AudioClip.Create(clipName, samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            clip.hideFlags = HideFlags.DontSave;
            return clip;
        }

        private void DestroyGenerated(params AudioClip[] clips)
        {
            for (int i = 0; i < clips.Length; i++)
            {
                AudioClip clip = clips[i];

                if (clip == null || IsOverride(clip))
                {
                    continue;
                }

                Destroy(clip);
            }
        }

        private bool IsOverride(AudioClip clip)
        {
            return clip == commandBlipOverride || clip == catBlipOverride || clip == staticOverride ||
                   clip == squelchOverride || clip == heartbeatOverride;
        }
    }
}
