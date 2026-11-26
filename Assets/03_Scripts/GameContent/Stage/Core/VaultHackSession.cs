namespace Pawntom.Stage
{
    /// <summary>지금 금고 해킹을 시작할 수 있는가.</summary>
    public enum VaultHackGate
    {
        Ready = 0,
        AlreadyOpened = 1,

        /// <summary>열쇠 잠금이 남아 있다(§3.8 부분 개방).</summary>
        Locked = 2,

        /// <summary>이 금고 또는 다른 대상의 미니게임 팝업이 떠 있다.</summary>
        HackInProgress = 3,

        /// <summary>미니게임은 성공했고 상태 권한의 개봉 결과를 기다린다.</summary>
        AwaitingOpen = 4,

        /// <summary>실패 쿨타임(§3.5 3초).</summary>
        CoolingDown = 5,
    }

    /// <summary>개봉 결과 대기 중 관찰 결과.</summary>
    public enum VaultHackPoll
    {
        None = 0,

        /// <summary>상태 권한이 금고를 열었다(네트워크 상태로 확인). 한 번만 나온다.</summary>
        Opened = 1,

        /// <summary>제한 시간 안에 열리지 않았다 — 권한 쪽이 요청을 거부했다고 본다.</summary>
        Rejected = 2,
    }

    /// <summary>
    /// 금고 최종 해킹 흐름의 로컬 진행 상태(엔진 비의존). GAME_DESIGN §3.8 "모든 잠금 해제 → 미니게임 → 성공 시 최종 개방",
    /// §3.5 "실패 시 3초 쿨타임".
    /// <para>
    /// 미니게임 성공은 개봉 <b>요청</b>일 뿐이다. 금고가 열렸는지는 상태 권한이 쓴 네트워크 상태(<c>IsOpened</c>)로만 판단한다 —
    /// 요청이 거부되면 열림으로 처리하지 않는다(fusion-network.md 의 과거 결함).
    /// </para>
    /// 시간은 호출하는 쪽이 넘긴다.
    /// </summary>
    public sealed class VaultHackSession
    {
        private readonly float _failCooldownSeconds;
        private readonly float _openTimeoutSeconds;

        private bool _inHack;
        private bool _awaitingOpen;
        private float _awaitUntil;
        private float _cooldownUntil = float.NegativeInfinity;

        public VaultHackSession(float failCooldownSeconds, float openTimeoutSeconds)
        {
            _failCooldownSeconds = failCooldownSeconds > 0f ? failCooldownSeconds : 0f;
            _openTimeoutSeconds = openTimeoutSeconds > 0f ? openTimeoutSeconds : 0f;
        }

        public bool IsAwaitingOpen
        {
            get { return _awaitingOpen; }
        }

        public VaultHackGate Evaluate(float now, bool isOpened, bool isUnlocked, bool anyHackActive)
        {
            if (isOpened)
            {
                return VaultHackGate.AlreadyOpened;
            }

            if (!isUnlocked)
            {
                return VaultHackGate.Locked;
            }

            if (_inHack || anyHackActive)
            {
                return VaultHackGate.HackInProgress;
            }

            if (_awaitingOpen)
            {
                return VaultHackGate.AwaitingOpen;
            }

            return now < _cooldownUntil ? VaultHackGate.CoolingDown : VaultHackGate.Ready;
        }

        /// <summary>시작할 수 있으면 해킹 중으로 바꾸고 true.</summary>
        public bool TryBegin(float now, bool isOpened, bool isUnlocked, bool anyHackActive)
        {
            if (Evaluate(now, isOpened, isUnlocked, anyHackActive) != VaultHackGate.Ready)
            {
                return false;
            }

            _inHack = true;
            return true;
        }

        /// <summary>팝업을 띄우지 못했다. 시작 전 상태로 되돌린다(쿨타임 없음).</summary>
        public void CancelBegin()
        {
            _inHack = false;
        }

        /// <param name="success">미니게임 성공.</param>
        /// <param name="canceled">사망 등으로 강제 종료됐다(실패 쿨타임을 걸지 않는다).</param>
        public void Finish(bool success, bool canceled, float now)
        {
            _inHack = false;

            if (success)
            {
                _awaitingOpen = true;
                _awaitUntil = now + _openTimeoutSeconds;
                return;
            }

            if (!canceled)
            {
                _cooldownUntil = now + _failCooldownSeconds;
            }
        }

        /// <summary>개봉 결과 대기 중일 때 네트워크 상태를 관찰한다.</summary>
        public VaultHackPoll Poll(float now, bool isOpened)
        {
            if (!_awaitingOpen)
            {
                return VaultHackPoll.None;
            }

            if (isOpened)
            {
                _awaitingOpen = false;
                return VaultHackPoll.Opened;
            }

            if (now < _awaitUntil)
            {
                return VaultHackPoll.None;
            }

            _awaitingOpen = false;
            return VaultHackPoll.Rejected;
        }

        public float CooldownRemaining(float now)
        {
            float remaining = _cooldownUntil - now;
            return remaining > 0f ? remaining : 0f;
        }
    }
}
