using UnityEngine;

/// <summary>
/// 배경음(BGM)·효과음(SFX) 음량 배율. 옵션창 값을 GameSettingsManager 가 넣는다.
/// <para>
/// 프로젝트에 AudioMixer 가 없고 모든 AudioSource 를 코드가 만든다. 그래서 믹서 그룹 대신
/// 재생하는 곳에서 이 배율을 곱한다(PlayOneShot 의 volumeScale, 루프 소스의 volume).
/// 전체 음량은 AudioListener.volume 으로 따로 건다.
/// </para>
/// 새 소리를 추가할 때: 음악·환경음 루프는 <see cref="Bgm"/>, 그 밖의 효과음은 <see cref="Sfx"/> 를 곱한다.
/// </summary>
public static class GameAudioVolume
{
    public static float Bgm { get; private set; } = 1f;
    public static float Sfx { get; private set; } = 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Bgm = 1f;
        Sfx = 1f;
    }

    public static void Set(float bgm, float sfx)
    {
        Bgm = Mathf.Clamp01(bgm);
        Sfx = Mathf.Clamp01(sfx);
    }
}
