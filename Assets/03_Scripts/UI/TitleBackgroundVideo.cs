using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// 타이틀 배경 영상. 영상 전체를 반복 재생한다. <see cref="replayDelaySeconds"/> 가 0 보다 크면
/// 한 번 끝날 때마다 마지막 장면에서 그만큼 멈춰 있다가 처음부터 다시 재생한다.
/// <para>
/// 화질: 영상과 같은 해상도(1920×1080)의 sRGB 렌더 텍스처에 그려 UI(RawImage)로 보여 준다.
/// 카메라 후처리를 거치지 않고 재인코딩도 하지 않아(임포트 시 트랜스코딩 끔) 원본 그대로 나온다.
/// 리니어 색 공간에서 색이 바래지 않도록 렌더 텍스처를 sRGB 로 만든다.
/// </para>
/// <para>
/// 첫 프레임이 준비되기 전에는 검은 화면이고, 준비되면 서서히 밝아진다(로딩 중 빈 화면·깜빡임 방지).
/// 화면 비율이 16:9 가 아니어도 <see cref="AspectRatioFitter"/>(EnvelopeParent)로 늘어나지 않고 꽉 채운다.
/// 영상을 재생할 수 없으면 정지 이미지(<see cref="endBackground"/>)로 대신한다.
/// </para>
/// </summary>
[RequireComponent(typeof(RawImage))]
public class TitleBackgroundVideo : MonoBehaviour
{
    [SerializeField] private VideoClip clip;

    [Tooltip("영상을 재생할 수 없을 때 대신 보여 줄 정지 이미지(영상 마지막 장면과 같은 원본)")]
    [SerializeField] private Texture endBackground;

    [Tooltip("첫 프레임이 뜰 때 검은 화면에서 밝아지는 시간(초)")]
    [SerializeField, Min(0f)] private float fadeInSeconds = 0.35f;

    [Tooltip("한 번 끝난 뒤 마지막 장면에서 멈춰 있다가 다시 재생하기까지의 시간(초). 0 이면 바로 이어서 반복")]
    [SerializeField, Min(0f)] private float replayDelaySeconds = 0f;

    private RawImage _image;
    private VideoPlayer _player;
    private RenderTexture _target;
    private float _fade;
    private bool _showingVideo;
    private bool _failed;
    private bool _holding;
    private float _replayAt;

    private void Awake()
    {
        _image = GetComponent<RawImage>();
        _image.color = Color.black;
        _image.raycastTarget = false;

        if (clip == null)
        {
            ShowFallback();
            return;
        }

        int width = (int)clip.width;
        int height = (int)clip.height;
        _target = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
        {
            name = "TitleVideo",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            useMipMap = false,
            antiAliasing = 1
        };
        _target.Create();

        _player = gameObject.AddComponent<VideoPlayer>();
        _player.playOnAwake = false;
        // 멈춤 없이 반복할 때는 플레이어 자체 반복(끊김 없음)을 쓰고, 멈춤이 있으면 끝에서 직접 다시 튼다.
        _player.isLooping = replayDelaySeconds <= 0f;
        _player.source = VideoSource.VideoClip;
        _player.clip = clip;
        _player.renderMode = VideoRenderMode.RenderTexture;
        _player.targetTexture = _target;
        _player.aspectRatio = VideoAspectRatio.Stretch; // 렌더 텍스처가 영상과 같은 크기라 늘어나지 않는다
        _player.audioOutputMode = VideoAudioOutputMode.None;
        _player.skipOnDrop = true;
        _player.waitForFirstFrame = true;
        _player.sendFrameReadyEvents = true;
        _player.prepareCompleted += HandlePrepared;
        _player.frameReady += HandleFirstFrame;
        _player.loopPointReached += HandleLoopPoint;
        _player.errorReceived += HandleError;

        _image.texture = _target;
        FitAspect(width, height);
        _player.Prepare();
    }

    private void OnDestroy()
    {
        if (_player != null)
        {
            _player.prepareCompleted -= HandlePrepared;
            _player.frameReady -= HandleFirstFrame;
            _player.loopPointReached -= HandleLoopPoint;
            _player.errorReceived -= HandleError;
        }

        if (_target != null)
        {
            _target.Release();
            Destroy(_target);
        }
    }

    private void Update()
    {
        if (_holding && Time.unscaledTime >= _replayAt)
        {
            _holding = false;
            _player.frame = 0;
            _player.Play();
        }

        if (!_showingVideo || _fade >= 1f)
        {
            return;
        }

        _fade = fadeInSeconds > 0f ? Mathf.Min(1f, _fade + Time.unscaledDeltaTime / fadeInSeconds) : 1f;
        _image.color = Color.Lerp(Color.black, Color.white, _fade);
    }

    private void HandlePrepared(VideoPlayer source)
    {
        source.Play();
    }

    // 첫 프레임이 렌더 텍스처에 들어온 뒤에 보이기 시작한다(그 전엔 텍스처가 비어 있다).
    private void HandleFirstFrame(VideoPlayer source, long frameIndex)
    {
        source.sendFrameReadyEvents = false;
        source.frameReady -= HandleFirstFrame;
        _showingVideo = true;
    }

    // 자체 반복이면 알림만 오고 계속 재생된다. 멈춤이 있으면 마지막 장면(렌더 텍스처에 남아 있다)에서 기다린다.
    private void HandleLoopPoint(VideoPlayer source)
    {
        if (source.isLooping || _failed)
        {
            return;
        }

        source.Pause();
        _holding = true;
        _replayAt = Time.unscaledTime + replayDelaySeconds;
    }

    private void HandleError(VideoPlayer source, string message)
    {
        Debug.LogWarning("[TitleBackgroundVideo] 영상 재생 실패 — 정지 이미지로 대신합니다: " + message);
        ShowFallback();
    }

    /// <summary>영상을 쓸 수 없을 때 정지 이미지로 바꾼다(영상 디코더는 멈춘다).</summary>
    private void ShowFallback()
    {
        if (_failed)
        {
            return;
        }

        _failed = true;
        _holding = false;

        if (endBackground != null)
        {
            _image.texture = endBackground;
            FitAspect(endBackground.width, endBackground.height);
        }

        _image.color = Color.white;
        _fade = 1f;
        _showingVideo = true;

        if (_player != null)
        {
            _player.Stop();
        }
    }

    private void FitAspect(int width, int height)
    {
        AspectRatioFitter fitter = GetComponent<AspectRatioFitter>();

        if (fitter != null && height > 0)
        {
            fitter.aspectRatio = (float)width / height;
        }
    }
}
