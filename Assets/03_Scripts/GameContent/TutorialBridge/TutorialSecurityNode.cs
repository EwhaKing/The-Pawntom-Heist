using Pawntom.Tutorial.Core;
using UnityEngine;
using UnityEngine.Events;

namespace Pawntom.TutorialBridge
{
    /// <summary>
    /// 튜토리얼 분전반(보안 노드). 가까이에서 상호작용 키를 <b>길게</b> 누르면 캐스팅이 진행되고, 캐스팅 동안은 움직일 수 없다.
    /// GAME_DESIGN "링크 트리거: 침투조가 분전반 검색 후 F, 3초 캐스팅, 이동 불가"를 튜토리얼 범위로만 구현한 것이다.
    /// <para>
    /// 정식 보안 노드·구역 해금 시스템이 생기면 이 컴포넌트를 그것으로 바꾸고, 튜토리얼 신호만 같은 시점에 보내면 된다.
    /// 구역 해금 연출(평면도 마스킹 해제 등)과 3-2 "K-9 접근 연출"은 이벤트(UnityEvent)에 씬에서 연결한다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Pawntom/Tutorial/Tutorial Security Node")]
    public sealed class TutorialSecurityNode : MonoBehaviour
    {
        [Header("상호작용")]
        [Tooltip("분전반 앞에서 \"E | 이름\" 으로 보일 이름(길게 눌러 접속)")]
        [SerializeField] private string interactName = "분전반 접속 (길게)";

        [Tooltip("GAME_DESIGN 분전반 캐스팅 3초")]
        [SerializeField, Min(0.1f)] private float castSeconds = 3f;

        [Tooltip("캐스팅 진행도가 이 값을 넘으면 3-2 'K-9 접근 연출'을 시작한다")]
        [SerializeField, Range(0f, 1f)] private float threatCueAt = 0.45f;

        [SerializeField] private string castLabel = "분전반 접속 중";

        [Header("이벤트 (씬에서 연결)")]
        [SerializeField] private UnityEvent onCastStarted = new UnityEvent();
        [Tooltip("K-9 를 이쪽으로 유도하는 연출을 연결한다")]
        [SerializeField] private UnityEvent onThreatCue = new UnityEvent();
        [Tooltip("구역 해금 등 접속 완료 처리를 연결한다")]
        [SerializeField] private UnityEvent onConnected = new UnityEvent();
        [SerializeField] private UnityEvent onCastCanceled = new UnityEvent();

        private InputSystemActions _actions;
        private const string ConnectedMessage = "본부에 연결되었습니다.";
        private TutorialBlinkHighlight _blink;
        private bool _casting;
        private bool _cueFired;
        private float _progress;

        public bool IsConnected { get; private set; }

        /// <summary>안내 상자·판정에 쓰는 범위 상자. 모든 E 상호작용과 같은 표준 거리(분전반 경계 + 1.2m)다.</summary>
        public Bounds RangeBounds()
        {
            return InteractRange.RangeBounds(this, InteractRange.StandardMargin);
        }

        private static readonly System.Collections.Generic.List<TutorialSecurityNode> ActiveNodes =
            new System.Collections.Generic.List<TutorialSecurityNode>(2);

        /// <summary>씬에 켜져 있는 분전반들. 안내 상자 위치를 정할 때 쓴다.</summary>
        public static System.Collections.Generic.IReadOnlyList<TutorialSecurityNode> All
        {
            get { return ActiveNodes; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            ActiveNodes.Clear();
        }

        private void Awake()
        {
            _actions = new InputSystemActions();
            SyncBindings();
        }

        private void OnEnable()
        {
            _actions?.Player.Enable();
            InputManager.BindingsChanged += SyncBindings;

            if (!ActiveNodes.Contains(this))
            {
                ActiveNodes.Add(this);
            }
        }

        private void OnDisable()
        {
            _actions?.Player.Disable();
            InputManager.BindingsChanged -= SyncBindings;
            ActiveNodes.Remove(this);
            CancelCast();
        }

        // 이 컴포넌트의 입력 인스턴스에 옵션창 키 설정을 맞춘다(키를 바꾼 플레이어도 안내·판정이 같은 키를 쓰게).
        private void SyncBindings()
        {
            InputManager.Instance.CopyBindingOverridesTo(_actions);
        }

        private void OnDestroy()
        {
            _actions?.Dispose();
            _actions = null;
        }

        private void Update()
        {
            TutorialDirector director = TutorialDirector.Active;
            Transform player = director != null ? director.LocalPlayerTransform : null;

            UpdateBlink(director);

            if (player == null)
            {
                return;
            }

            if (IsConnected)
            {
                // 연결된 동안 계속 알린다(상태형). 2단계를 마치기 전에 먼저 열어도, 순서가 3단계에 오면 3-0 이 열린다.
                director.Raise(TutorialSignal.SecurityNodeConnected);
                
                if (!GameWorldPause.IsPaused && !GameplayInputBlocker.IsBlocked && !director.IsControlLockedByDirector &&
                    InteractRange.IsInRange(this, player.position, InteractRange.StandardMargin))
                {
                    InteractPrompt.ShowMessage(ConnectedMessage);
                }
                
                return;
            }

            bool near = InteractRange.IsInRange(this, player.position, InteractRange.StandardMargin);

            if (near)
            {
                director.Raise(TutorialSignal.NearSecurityNode);
            }

            // 대사가 떠 있는 동안은 상호작용 안내도, 캐스팅 진행도 멈춘다. 캐스팅 도중이면 취소하지 않고
            // 진행도를 그대로 둔 채 대사가 끝나면 이어 간다(3-2 처럼 캐스팅 중에 나오는 대사가 있다).
            if (GameWorldPause.IsPaused)
            {
                return;
            }

            if (!director.HasReachedMain(TutorialDirector.SecurityNodeUnlockBeatId))
            {
                CancelCast();

                if (near && !GameplayInputBlocker.IsBlocked && !director.IsControlLockedByDirector)
                {
                    InteractPrompt.Show(TutorialDirector.LockedPrompt);
                }

                return;
            }

            if (near && !_casting && !GameplayInputBlocker.IsBlocked && !director.IsControlLockedByDirector)
            {
                InteractPrompt.Show(interactName);
            }

            bool holding = near &&
                           !GameplayInputBlocker.IsBlocked &&
                           !director.IsControlLockedByDirector &&
                           _actions.Player.Interact.IsPressed();

            if (!holding)
            {
                CancelCast();
                return;
            }

            if (!_casting)
            {
                _casting = true;
                _progress = 0f;
                TutorialInputLock.Acquire(this);
                onCastStarted.Invoke();
                director.Raise(TutorialSignal.SecurityNodeCastStarted);
            }

            _progress += Time.deltaTime / castSeconds;
            director.ShowCastProgress(castLabel, _progress);

            if (!_cueFired && _progress >= threatCueAt)
            {
                _cueFired = true;
                onThreatCue.Invoke();
                director.Raise(TutorialSignal.SecurityNodeThreatCue);
            }

            if (_progress < 1f)
            {
                return;
            }

            IsConnected = true;
            _casting = false;
            TutorialInputLock.Release(this);
            director.HideCastProgress();
            onConnected.Invoke();
            director.Raise(TutorialSignal.SecurityNodeConnected);
        }

        private void UpdateBlink(TutorialDirector director)
        {
            bool on = director != null && !IsConnected && director.HasReachedMain(TutorialDirector.SecurityNodeUnlockBeatId);

            if (_blink == null)
            {
                if (!on) return;
                _blink = GetComponent<TutorialBlinkHighlight>();
                if (_blink == null) _blink = gameObject.AddComponent<TutorialBlinkHighlight>();
            }

            _blink.SetOn(on);
        }

        public void RestoreCheckpoint(bool connected)
        {
            CancelCast();
            IsConnected = connected;
            _cueFired = false;
            _progress = 0f;
        }

        private void CancelCast()
        {
            if (!_casting)
            {
                return;
            }

            _casting = false;
            _progress = 0f;
            TutorialInputLock.Release(this);
            onCastCanceled.Invoke();

            TutorialDirector director = TutorialDirector.Active;

            if (director != null)
            {
                director.HideCastProgress();
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.49f, 1f, 0.7f, 0.8f);
            Bounds range = RangeBounds();
            Gizmos.DrawWireCube(range.center, range.size);
        }
    }
}
