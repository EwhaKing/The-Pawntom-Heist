using Hacking;
using Pawntom.Tutorial.Core;
using UnityEngine;
using UnityEngine.Events;

namespace Pawntom.TutorialBridge
{
    /// <summary>
    /// 튜토리얼 금고. 앞에서 상호작용 키를 누르면 해킹 미니게임(기본: Space 타이밍 = DigitalSync)을 띄우고,
    /// 성공하면 전설의 캣닢을 로컬 플레이어 가방에 넣는다.
    /// <para>
    /// 정식 금고 시스템(열쇠·부분 개방)이 생기면 그것으로 바꾸고 튜토리얼 신호만 같은 시점에 보내면 된다.
    /// 7-3 "경보" 연출(경비견 소집 등)은 <c>onFailed</c> 이벤트에 씬에서 연결한다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Pawntom/Tutorial/Tutorial Vault")]
    public sealed class TutorialVault : MonoBehaviour
    {
        [Header("상호작용")]
        [SerializeField, Min(0.1f)] private float interactRadius = 2.5f;

        [Tooltip("금고 앞에서 \"E | 이름\" 으로 보일 이름")]
        [SerializeField] private string interactName = "금고 해킹";

        [Header("해킹")]
        [Tooltip("HackingManager 풀에 이 등급이 없으면 Normal 로 대신 띄운다(현재 Tutorial_Scene 풀은 Normal 만 있다)")]
        [SerializeField] private SecurityLevel securityLevel = SecurityLevel.VaultFinal;
        [Tooltip("7-2 안내가 Space 타이밍이므로 DigitalSync 를 우선한다")]
        [SerializeField] private HackingGameType preferredGame = HackingGameType.DigitalSync;

        [Header("보상")]
        [Tooltip("전설의 캣닢 아이템 ID. RuleManager/MainbaseManager 의 탈출 아이템 기본값(1)과 같아야 한다")]
        [SerializeField] private int rewardItemId = 1;

        [Header("이벤트 (씬에서 연결)")]
        [SerializeField] private UnityEvent onOpened = new UnityEvent();
        [Tooltip("경보 연출을 연결한다")]
        [SerializeField] private UnityEvent onFailed = new UnityEvent();

        private InputSystemActions _actions;
        private HackingManager _hackingManager;

        /// <summary>
        /// 금고 해킹을 띄우는 중이다. 수집기가 HackingStarted 를 금고 해킹으로 분류하는 데 쓴다
        /// (풀에 VaultFinal 이 없어 Normal 로 띄운 경우에도 금고로 보기 위해).
        /// </summary>
        public static bool IsVaultHackPending { get; private set; }

        public bool IsOpened { get; private set; }

        /// <summary>사망 후 같은 미션을 다시 하게 할 때 금고를 닫힌 상태로 되돌린다(보상은 가방 초기화로 이미 사라졌다).</summary>
        public void ResetForRetry()
        {
            IsOpened = false;
            IsVaultHackPending = false;
        }

        public float InteractRadius
        {
            get { return interactRadius; }
        }

        private static readonly System.Collections.Generic.List<TutorialVault> ActiveVaults =
            new System.Collections.Generic.List<TutorialVault>(2);

        /// <summary>씬에 켜져 있는 금고들. 수집기가 "금고 앞 무입력"을 잴 때 쓴다.</summary>
        public static System.Collections.Generic.IReadOnlyList<TutorialVault> All
        {
            get { return ActiveVaults; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            IsVaultHackPending = false;
            ActiveVaults.Clear();
        }

        private void Awake()
        {
            _actions = new InputSystemActions();
        }

        private void OnEnable()
        {
            _actions?.Player.Enable();

            if (!ActiveVaults.Contains(this))
            {
                ActiveVaults.Add(this);
            }
        }

        private void OnDisable()
        {
            _actions?.Player.Disable();
            ActiveVaults.Remove(this);
        }

        private void OnDestroy()
        {
            _actions?.Dispose();
            _actions = null;
            IsVaultHackPending = false;
        }

        private void Update()
        {
            TutorialDirector director = TutorialDirector.Active;
            Transform player = director != null ? director.LocalPlayerTransform : null;

            if (IsOpened || player == null)
            {
                return;
            }

            Vector3 delta = player.position - transform.position;
            delta.y = 0f;

            if (delta.sqrMagnitude > interactRadius * interactRadius)
            {
                return;
            }

            director.Raise(TutorialSignal.VaultFront);

            bool blocked = GameplayInputBlocker.IsBlocked ||
                           director.IsControlLockedByDirector ||
                           Hacking.HackingManager.AnyHackingActive;

            if (!blocked)
            {
                InteractPrompt.Show(interactName);
            }

            if (GameplayInputBlocker.IsBlocked ||
                director.IsControlLockedByDirector ||
                !_actions.Player.Interact.WasPressedThisFrame())
            {
                return;
            }

            StartHack();
        }

        private void StartHack()
        {
            if (_hackingManager == null)
            {
                _hackingManager = FindFirstObjectByType<HackingManager>();
            }

            if (_hackingManager == null)
            {
                Debug.LogWarning("[TutorialVault] HackingManager 가 씬에 없어 금고 해킹을 띄울 수 없습니다.", this);
                return;
            }

            if (_hackingManager.IsHackingActive)
            {
                return;
            }

            HackingGameType? previous = HackingManager.PreferredGameType;
            HackingManager.PreferredGameType = preferredGame;
            IsVaultHackPending = true;

            _hackingManager.OpenHackingPopup(securityLevel, HandleHackFinished);

            if (!_hackingManager.IsHackingActive && securityLevel != SecurityLevel.Normal)
            {
                Debug.LogWarning($"[TutorialVault] 해킹 풀에 {securityLevel} 등급이 없어 Normal 등급으로 띄웁니다.", this);
                _hackingManager.OpenHackingPopup(SecurityLevel.Normal, HandleHackFinished);
            }

            HackingManager.PreferredGameType = previous;

            if (!_hackingManager.IsHackingActive)
            {
                IsVaultHackPending = false;
            }
        }

        private void HandleHackFinished(bool success)
        {
            IsVaultHackPending = false;

            if (!success)
            {
                onFailed.Invoke();
                return;
            }

            IsOpened = true;
            GiveReward();
            onOpened.Invoke();
        }

        private void GiveReward()
        {
            TutorialDirector director = TutorialDirector.Active;
            InventoryManager inventory = director != null ? director.LocalInventory : null;

            if (inventory == null)
            {
                Debug.LogWarning("[TutorialVault] 로컬 플레이어 인벤토리를 찾지 못해 캣닢을 넣지 못했습니다.", this);
                return;
            }

            // TryAddItem 은 StateAuthority(Host)에서만 성공한다. 튜토리얼은 혼자 Host 로 진행하는 데모 경로다.
            if (inventory.TryAddItem(rewardItemId))
            {
                return;
            }

            if (!inventory.Object.HasStateAuthority)
            {
                Debug.LogWarning("[TutorialVault] Host 가 아니라 캣닢을 가방에 넣지 못했습니다.", this);
                return;
            }

            // 가방이 가득 찼다. 아직 버리기(Q) 입력이 없어 칸을 비울 방법이 없으므로, 캣닢을 못 받으면 튜토리얼이 7-4 에서 멈춘다.
            // 들고 있는 칸(없으면 마지막 칸)의 전리품을 캣닢으로 바꿔 넣는다 — 대본 5-6 "한 칸은 캣닢 자리로 비워 두십시오"의 보정.
            int slot = inventory.SelectedSlotIndex >= 0 && inventory.SelectedSlotIndex < InventoryManager.MaxSlotCount
                ? inventory.SelectedSlotIndex
                : InventoryManager.MaxSlotCount - 1;
            int replaced = inventory.Slots[slot];
            inventory.Slots.Set(slot, rewardItemId);
            Debug.Log($"[TutorialVault] 가방이 가득 차 {slot + 1}번 칸(ItemId={replaced})을 캣닢으로 바꿔 넣었습니다.", this);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.82f, 0.35f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, interactRadius);
        }
    }
}
