using System;

namespace Pawntom.Settings
{
    /// <summary>
    /// 옵션창에서 고르는 값 묶음. 엔진에 의존하지 않는 순수 데이터라 EditMode 테스트로 검증한다.
    /// 적용(오디오·화면·입력에 반영)은 GameSettingsManager 가 맡는다.
    /// <para>
    /// "현재 값 유지"가 필요한 항목은 음수(-1)나 0 을 "지정 안 함"으로 쓴다 — 처음 실행하면 플레이어의 모니터·
    /// 프로젝트 품질 설정을 그대로 두고, 옵션창에서 고른 뒤에만 덮어쓴다.
    /// </para>
    /// </summary>
    public sealed class GameSettingsData
    {
        public const float MinVolume = 0f;
        public const float MaxVolume = 1f;

        public const float MinLookSensitivity = 0.1f;
        public const float MaxLookSensitivity = 3f;
        public const float DefaultLookSensitivity = 1f;

        /// <summary>화면 모드 값. UnityEngine.FullScreenMode 와 같은 숫자를 쓴다(0 독점 전체 화면, 1 전체 화면 창, 3 창 모드).</summary>
        public const int ScreenModeUnset = -1;
        public const int ScreenModeExclusive = 0;
        public const int ScreenModeBorderless = 1;
        public const int ScreenModeWindowed = 3;

        public const int QualityUnset = -1;

        // ── 사운드 ──
        public float MasterVolume = 1f;
        public float BgmVolume = 0.8f;
        public float SfxVolume = 1f;

        // ── 조작 ──
        /// <summary>마우스 감도 배율. 1 = 기존 손맛(PlayerController.lookSensitivity 그대로).</summary>
        public float LookSensitivity = DefaultLookSensitivity;
        public bool InvertLookY;

        /// <summary>입력 바인딩 오버라이드(InputManager 가 만들고 읽는 JSON). 비어 있으면 기본 키.</summary>
        public string BindingOverridesJson = string.Empty;

        // ── 화면 ──
        /// <summary>0 이면 지정 안 함(현재 해상도 유지).</summary>
        public int ResolutionWidth;
        public int ResolutionHeight;
        public int ScreenMode = ScreenModeUnset;
        public int QualityLevel = QualityUnset;
        public bool VSync = true;

        public bool HasResolution
        {
            get { return ResolutionWidth > 0 && ResolutionHeight > 0; }
        }

        /// <summary>
        /// 저장소에서 읽었거나 사람이 손댄 값을 쓸 수 있는 범위로 맞춘다.
        /// </summary>
        /// <param name="qualityLevelCount">프로젝트 품질 단계 수. 0 이하이면 품질 값 검사를 건너뛴다.</param>
        public void Sanitize(int qualityLevelCount)
        {
            MasterVolume = ClampVolume(MasterVolume);
            BgmVolume = ClampVolume(BgmVolume);
            SfxVolume = ClampVolume(SfxVolume);

            LookSensitivity = IsFinite(LookSensitivity)
                ? Math.Max(MinLookSensitivity, Math.Min(MaxLookSensitivity, LookSensitivity))
                : DefaultLookSensitivity;

            if (BindingOverridesJson == null)
            {
                BindingOverridesJson = string.Empty;
            }

            if (ResolutionWidth <= 0 || ResolutionHeight <= 0)
            {
                ResolutionWidth = 0;
                ResolutionHeight = 0;
            }

            if (ScreenMode != ScreenModeExclusive && ScreenMode != ScreenModeBorderless && ScreenMode != ScreenModeWindowed)
            {
                ScreenMode = ScreenModeUnset;
            }

            if (qualityLevelCount > 0 && (QualityLevel < 0 || QualityLevel >= qualityLevelCount))
            {
                QualityLevel = QualityUnset;
            }
        }

        /// <summary>시선 입력에 곱할 배율. Y 반전이면 세로 배율이 음수다.</summary>
        public float LookScaleX
        {
            get { return LookSensitivity; }
        }

        public float LookScaleY
        {
            get { return InvertLookY ? -LookSensitivity : LookSensitivity; }
        }

        public GameSettingsData Clone()
        {
            return (GameSettingsData)MemberwiseClone();
        }

        /// <summary>조작 탭의 "기본값으로"용: 감도·Y 반전·키 바인딩만 기본값으로 되돌린다.</summary>
        public void ResetControls()
        {
            LookSensitivity = DefaultLookSensitivity;
            InvertLookY = false;
            BindingOverridesJson = string.Empty;
        }

        private static float ClampVolume(float value)
        {
            if (!IsFinite(value))
            {
                return MaxVolume;
            }

            return Math.Max(MinVolume, Math.Min(MaxVolume, value));
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
