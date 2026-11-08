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

        // 같은 오브젝트의 열쇠 금고. 있으면 열쇠로 잠금을 푼 뒤에만 해킹할 수 있고, 해킹에 성공해야 금고가 열린다.
        private VaultChest _chest;

        private bool HasKeyLock
        {
            get { return _chest != null && _chest.OpenByMinigame; }
        }

        private bool IsKeyLockActive
        {
            get { return HasKeyLock && _chest.Object != null && _chest.Object.IsValid; }
        }

        /// <summary>열쇠로 사용한 횟수. 체크포인트가 저장해 두었다가 사망 재시도 때 되돌린다.</summary>
        public int UsedKeyCount
        {
            get { return IsKeyLockActive ? _chest.UsedKeyCount : 0; }
        }

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

            // 열쇠 사용 횟수는 그대로 두고 개봉 상태만 닫는다(열쇠 복원은 단계 체크포인트가 맡는다).
            if (IsKeyLockActive)
            {
                _chest.RestoreCheckpointState(_chest.UsedKeyCount, false);
            }
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
            _chest = GetComponent<VaultChest>();
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

            // 열쇠 금고가 같이 있으면 그쪽 범위 기준을 따른다. 안내가 뜨는 거리와 E(열쇠 사용)가 먹는 거리가 같아야 한다.
            bool inRange;

            if (_chest != null)
            {
                inRange = _chest.IsPlayerInRange(player.position);
            }
            else
            {
                Vector3 delta = player.position - transform.position;
                delta.y = 0f;
                inRange = delta.sqrMagnitude <= interactRadius * interactRadius;
            }

            if (!inRange)
            {
                return;
            }

            director.Raise(TutorialSignal.VaultFront);

            // 열쇠로 잠금을 풀기 전에는 해킹할 수 없다. 대신 왜 안 되는지, 무엇을 해야 하는지 안내한다.
            // (씬에 VaultPromptUI 글자가 없어도 보이도록 여기서 직접 띄운다.)
            if (HasKeyLock && !_chest.IsUnlocked)
            {
                if (!GameplayInputBlocker.IsBlocked &&
                    !GameWorldPause.IsPaused &&
                    !director.IsControlLockedByDirector &&
                    !Hacking.HackingManager.AnyHackingActive)
                {
                    InteractPrompt.Show(GetLockedPrompt(director));
                }

                return;
            }

            // 대사가 떠 있는 동안(GameWorldPause)에는 안내도 상호작용도 없다.
            bool blocked = GameplayInputBlocker.IsBlocked ||
                           GameWorldPause.IsPaused ||
                           director.IsControlLockedByDirector ||
                           Hacking.HackingManager.AnyHackingActive;

            // 열쇠를 쓰면 잠금만 풀린다. 금고를 열려면(해킹 미니게임) 다시 E 를 눌러야 한다.
            if (!blocked)
            {
                InteractPrompt.Show(interactName);
            }

            if (GameplayInputBlocker.IsBlocked ||
                GameWorldPause.IsPaused ||
                director.IsControlLockedByDirector ||
                !_actions.Player.Interact.WasPressedThisFrame())
            {
                return;
            }

            StartHack();
        }

        /// <summary>
        /// 잠긴 금고 앞 안내 문구. 열쇠를 들고 있으면 사용하라고, 아니면 열쇠가 필요하다고 알린다.
        /// 열쇠 소비 자체는 PlayerInteraction(금고를 조준하고 E)이 VaultChest 에 요청한다.
        /// </summary>
        private string GetLockedPrompt(TutorialDirector director)
        {
            InventoryManager inventory = director.LocalInventory;
            VaultKeyInventoryBridge bridge = inventory != null ? inventory.GetComponent<VaultKeyInventoryBridge>() : null;

            if (bridge != null && bridge.HasSelectedKey)
            {
                return "열쇠 사용 (금고를 보고 누르세요)";
            }

            return "잠겨 있습니다. 열쇠가 필요합니다";
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
                // 사망으로 취소된 경우는 실패 처리(경보 등)를 하지 않는다.
                if (!Hacking.HackingManager.IsCancellingForPlayerDeath)
                {
                    onFailed.Invoke();
                }

                return;
            }

            IsOpened = true;

            // 열쇠 금고가 있으면 금고가 열리며 캣닢이 바닥에 생성된다(직접 주워야 한다). 열 수 없었던 경우에만 가방에 바로 넣는다.
            if (!OpenChest())
            {
                GiveReward();
            }

            onOpened.Invoke();
        }

        // 해킹에 성공하면 열쇠 금고를 "열림"으로 바꾸고 캣닢을 바닥에 생성한다. 요청하지 못했으면 false.
        private bool OpenChest()
        {
            if (!IsKeyLockActive)
            {
                return false;
            }

            TutorialDirector director = TutorialDirector.Active;
            InventoryManager inventory = director != null ? director.LocalInventory : null;

            if (inventory == null || inventory.Object == null)
            {
                Debug.LogWarning("[TutorialVault] 로컬 플레이어를 찾지 못해 금고를 열림 상태로 바꾸지 못했습니다.", this);
                return false;
            }

            _chest.RequestOpenAfterMinigame(inventory.Object, true);
            return true;
        }

        public void RestoreCheckpoint(bool opened, int usedKeyCount)
        {
            IsOpened = opened;
            IsVaultHackPending = false;

            if (IsKeyLockActive)
            {
                _chest.RestoreCheckpointState(usedKeyCount, opened);
            }
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
