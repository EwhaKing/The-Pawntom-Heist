namespace Pawntom.Stage
{
    /// <summary>한 번 <see cref="CastProgressModel.Tick"/> 했을 때 일어난 일.</summary>
    public enum CastStep
    {
        /// <summary>캐스팅 중이 아니고 시작하지도 않았다.</summary>
        Idle = 0,

        /// <summary>이번에 캐스팅을 시작했다(진행도 0).</summary>
        Started = 1,

        /// <summary>캐스팅이 이어지고 있다.</summary>
        Progressed = 2,

        /// <summary>손을 떼거나·움직이거나·범위를 벗어나 취소됐다.</summary>
        Canceled = 3,

        /// <summary>이번에 끝까지 채웠다. 한 번만 나온다.</summary>
        Completed = 4,
    }

    /// <summary>
    /// 길게 눌러 채우는 캐스팅(엔진 비의존). GAME_DESIGN §3.4 분전반 "F 3초 캐스팅, 이동 불가".
    /// <para>
    /// 누르고 있는 동안만 진행되고, 손을 떼거나 움직이거나 범위를 벗어나면 진행도 0 으로 취소된다.
    /// 움직임·범위 이탈로 취소됐다면 한 번 손을 뗐다가 다시 눌러야 새로 시작한다(눌린 채 자동 재시작 방지).
    /// 끝까지 채우면 <see cref="CastStep.Completed"/> 를 한 번만 돌려주고, <see cref="Reset"/> 전에는 다시 시작하지 않는다.
    /// </para>
    /// 시간은 호출하는 쪽이 넘긴다(테스트·네트워크 틱 주입).
    /// </summary>
    public sealed class CastProgressModel
    {
        private const float MinDuration = 0.05f;

        private bool _needsRelease;

        public CastProgressModel(float durationSeconds)
        {
            Duration = durationSeconds > MinDuration ? durationSeconds : MinDuration;
        }

        public float Duration { get; }

        public float Elapsed { get; private set; }

        public bool IsCasting { get; private set; }

        public bool IsCompleted { get; private set; }

        /// <summary>0~1 진행도.</summary>
        public float Progress
        {
            get
            {
                if (IsCompleted)
                {
                    return 1f;
                }

                float p = Elapsed / Duration;
                return p < 0f ? 0f : (p > 1f ? 1f : p);
            }
        }

        /// <param name="deltaSeconds">지난 호출 이후 흐른 시간. 음수는 0 으로 본다.</param>
        /// <param name="holding">상호작용 키를 누르고 있다.</param>
        /// <param name="moved">이동 입력이 있거나 캐스팅 시작 위치에서 밀려났다.</param>
        /// <param name="inRange">대상 상호작용 범위 안이고 대상이 아직 캐스팅 가능하다.</param>
        public CastStep Tick(float deltaSeconds, bool holding, bool moved, bool inRange)
        {
            if (IsCompleted)
            {
                return CastStep.Idle;
            }

            if (!holding)
            {
                _needsRelease = false;
            }

            if (!IsCasting)
            {
                if (!holding || moved || !inRange || _needsRelease)
                {
                    return CastStep.Idle;
                }

                IsCasting = true;
                Elapsed = 0f;
                return CastStep.Started;
            }

            if (!holding || moved || !inRange)
            {
                // 누른 채로 움직이거나 벗어나 취소됐으면 다시 누를 때까지 재시작하지 않는다.
                _needsRelease = holding;
                CancelInternal();
                return CastStep.Canceled;
            }

            if (deltaSeconds > 0f)
            {
                Elapsed += deltaSeconds;
            }

            if (Elapsed + 1e-5f < Duration)
            {
                return CastStep.Progressed;
            }

            Elapsed = Duration;
            IsCasting = false;
            IsCompleted = true;
            return CastStep.Completed;
        }

        /// <summary>외부 사유(대상이 다른 사람에게 먼저 완료됨, 사망 등)로 취소. 캐스팅 중이었으면 true.</summary>
        public bool Cancel()
        {
            if (!IsCasting)
            {
                return false;
            }

            CancelInternal();
            return true;
        }

        /// <summary>완료 상태까지 지우고 처음으로 되돌린다.</summary>
        public void Reset()
        {
            IsCasting = false;
            IsCompleted = false;
            Elapsed = 0f;
            _needsRelease = false;
        }

        private void CancelInternal()
        {
            IsCasting = false;
            Elapsed = 0f;
        }
    }
}
