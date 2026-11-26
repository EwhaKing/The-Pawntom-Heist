namespace Pawntom.Network
{
    /// <summary>
    /// [Networked] 불 값이 참이 된 것을 이 피어가 처음 볼 때 한 번만 true 를 돌려준다.
    /// Render 에서 매 프레임 현재 값을 넣는다. 늦은 참가자는 첫 관찰이 참이어도 1회 울린다(RPC 로만 알리면 받지 못하는 경우를 막는다).
    /// 값이 거짓으로 돌아가면 다시 무장한다(새 라운드).
    /// </summary>
    public sealed class OneShotLatch
    {
        private bool _fired;

        public bool HasFired
        {
            get { return _fired; }
        }

        public bool Observe(bool active)
        {
            if (!active)
            {
                _fired = false;
                return false;
            }

            if (_fired)
            {
                return false;
            }

            _fired = true;
            return true;
        }

        public void Reset()
        {
            _fired = false;
        }
    }
}
