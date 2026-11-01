using System.Collections.Generic;
using UnityEngine;

namespace Hacking
{
    /// <summary>
    /// HackingManager
    ///
    /// 담당:
    /// - 해킹 미니게임 팝업 생성
    /// - 보안 등급에 맞는 미니게임 프리팹 랜덤 선택
    /// - 미니게임 성공/실패 결과 처리
    /// - 외부 시스템이 해킹 결과를 받을 수 있도록 콜백 전달
    ///
    /// 사용 위치:
    /// - 씬에 있는 HackingManager 빈 오브젝트에 붙임
    /// </summary>
    public class HackingManager : MonoBehaviour
    {
        [Header("팝업 설정")]
        [SerializeField] private GameObject popupViewPrefab;
        [SerializeField] private Transform canvasParent;

        [Header("미니게임 풀")]
        [SerializeField] private List<HackingGameData> hackingGamePool = new List<HackingGameData>();

        [Header("[TEST ONLY] 테스트 전용 설정")]
        [SerializeField] private SecurityLevel testSecurityLevel = SecurityLevel.Normal;
        [SerializeField] private KeyCode testTriggerKey = KeyCode.H;

        public bool IsHackingActive
        {
            get { return isHackingActive; }
            private set
            {
                if (value && !isHackingActive)
                {
                    ShowCursorForPopup();
                }
                else if (!value && isHackingActive)
                {
                    RestoreCursorAfterPopup();
                }

                isHackingActive = value;
                AnyHackingActive = value;
            }
        }

        private bool isHackingActive;

        // 현장에서 바로 뜨는 팝업(금고 등)은 1인칭 조작 중이라 커서가 잠겨 있다. 팝업 버튼을 누를 수 있게 풀고, 끝나면 되돌린다.
        // 본부 단말기 위에서 뜬 팝업은 이미 커서가 풀려 있으므로 건드리지 않는다.
        private bool cursorUnlockedForPopup;

        private void ShowCursorForPopup()
        {
            cursorUnlockedForPopup = Cursor.lockState != CursorLockMode.None;

            if (cursorUnlockedForPopup)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        private void RestoreCursorAfterPopup()
        {
            // 그사이 단말기 창 등 다른 UI 가 열렸으면 그쪽이 커서를 쓰고 있으므로 잠그지 않는다.
            if (cursorUnlockedForPopup && !GameplayInputBlocker.IsBlocked)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            cursorUnlockedForPopup = false;
        }

        /// <summary>
        /// 어느 HackingManager 든 미니게임이 진행 중이면 true.
        /// 미니게임 조작(방향키·Space·A/D)이 플레이어 이동·점프로 새지 않도록 InputManager 가 확인합니다.
        /// </summary>
        public static bool AnyHackingActive { get; private set; }

        /// <summary>팝업을 같은 캔버스의 다른 UI(본부 단말기 창 등) 위에 그리기 위한 정렬 값.</summary>
        private const int PopupSortingOffset = 100;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            AnyHackingActive = false;
        }

        private void OnDestroy()
        {
            // 씬이 바뀌어 진행 중이던 팝업과 함께 사라지면 입력 차단이 남지 않게 한다.
            if (isHackingActive)
            {
                AnyHackingActive = false;
                HackingGameBase.Paused = false;
            }
        }

        /// <summary>
        /// 미니게임 팝업이 뜬 직후 호출됩니다. (보안 등급, 선택된 미니게임 종류)
        /// 호출한 쪽과 무관하게 해킹 진행을 지켜봐야 하는 시스템(튜토리얼 안내 등)이 구독합니다.
        /// </summary>
        public static event System.Action<SecurityLevel, HackingGameType> HackingStarted;

        /// <summary>미니게임이 끝났을 때 호출됩니다. (보안 등급, 성공 여부)</summary>
        public static event System.Action<SecurityLevel, bool> HackingFinished;

        /// <summary>
        /// 설정되어 있고 후보에 해당 종류가 있으면 그 미니게임을 고릅니다. 없으면 기존처럼 무작위입니다.
        /// 튜토리얼이 단계별 안내(방향키 / Space 타이밍)와 미니게임을 맞추기 위해 사용합니다.
        /// </summary>
        public static HackingGameType? PreferredGameType { get; set; }

        private System.Action<bool> externalResultCallback;
        private SecurityLevel currentLevel;

        private GameObject currentPopupObj;
        private GameObject currentGameObj;

        private void Update()
        {
            if (!Input.GetKeyDown(testTriggerKey))
            {
                return;
            }

            Debug.Log($"[HackingManager] 테스트 키 입력됨: {testTriggerKey}");

            if (IsHackingActive)
            {
                Debug.LogWarning("[HackingManager] 이미 해킹 진행 중으로 판단되어 새 팝업을 열지 않습니다.");

                if (currentPopupObj == null)
                {
                    Debug.LogWarning("[HackingManager] 그런데 currentPopupObj가 없습니다. 상태를 강제로 초기화합니다.");
                    ForceResetHackingState();
                }

                return;
            }

            OpenHackingPopup(testSecurityLevel);
        }

        /// <summary>
        /// 테스트용 또는 결과 콜백이 필요 없는 경우 사용합니다.
        /// </summary>
        public void OpenHackingPopup(SecurityLevel level)
        {
            OpenHackingPopup(level, null);
        }

        /// <summary>
        /// 지정된 보안 등급에 맞는 미니게임을 풀에서 골라 팝업으로 띄웁니다.
        /// </summary>
        public void OpenHackingPopup(SecurityLevel level, System.Action<bool> resultCallback)
        {
            if (PlayerData.IsLocalPlayerDead) return;
            Debug.Log($"[HackingManager] OpenHackingPopup 호출됨. Level={level}");

            if (IsHackingActive)
            {
                Debug.LogWarning("[HackingManager] 이미 해킹이 진행 중입니다.");
                return;
            }

            if (popupViewPrefab == null)
            {
                Debug.LogError("[HackingManager] Popup View Prefab이 비어 있습니다.");
                return;
            }

            if (canvasParent == null)
            {
                Debug.LogError("[HackingManager] Canvas Parent가 비어 있습니다.");
                return;
            }

            if (hackingGamePool == null || hackingGamePool.Count == 0)
            {
                Debug.LogError("[HackingManager] Hacking Game Pool이 비어 있습니다.");
                return;
            }

            Debug.Log($"[HackingManager] Hacking Game Pool Count = {hackingGamePool.Count}");

            List<HackingGameData> candidates = hackingGamePool.FindAll(data =>
                data != null &&
                data.securityLevel == level &&
                data.gamePrefab != null
            );

            Debug.Log($"[HackingManager] 후보 미니게임 수 = {candidates.Count}");

            if (candidates.Count == 0)
            {
                Debug.LogWarning($"[HackingManager] '{level}' 등급에 등록된 미니게임이 없습니다. Hacking Game Pool을 확인하세요.");
                return;
            }

            if (PreferredGameType.HasValue)
            {
                HackingGameType preferred = PreferredGameType.Value;
                List<HackingGameData> preferredCandidates = candidates.FindAll(data => data.gameType == preferred);

                if (preferredCandidates.Count > 0)
                {
                    candidates = preferredCandidates;
                }
            }

            HackingGameData selected = candidates[Random.Range(0, candidates.Count)];

            Debug.Log($"[HackingManager] 선택된 미니게임: {selected.gameType}, Prefab={selected.gamePrefab.name}");

            IsHackingActive = true;
            externalResultCallback = resultCallback;

            // 팝업 생성
            currentPopupObj = Instantiate(popupViewPrefab, canvasParent);
            currentPopupObj.SetActive(true);
            BringPopupToFront(currentPopupObj);

            // UI 프리팹이 부모 안에서 이상하게 배치되는 것을 방지
            RectTransform popupRect = currentPopupObj.GetComponent<RectTransform>();
            if (popupRect != null)
            {
                popupRect.anchorMin = Vector2.zero;
                popupRect.anchorMax = Vector2.one;
                popupRect.offsetMin = Vector2.zero;
                popupRect.offsetMax = Vector2.zero;
                popupRect.localScale = Vector3.one;
                popupRect.localRotation = Quaternion.identity;
            }

            HackingPopupView popupView = currentPopupObj.GetComponentInChildren<HackingPopupView>(true);

            if (popupView == null)
            {
                Debug.LogError("[HackingManager] Popup View Prefab 안에서 HackingPopupView 컴포넌트를 찾지 못했습니다.");

                Destroy(currentPopupObj);
                currentPopupObj = null;

                IsHackingActive = false;
                externalResultCallback = null;
                return;
            }

            // 미니게임 생성
            currentGameObj = Instantiate(selected.gamePrefab);
            currentGameObj.SetActive(true);

            HackingGameBase gameInstance = currentGameObj.GetComponentInChildren<HackingGameBase>(true);

            if (gameInstance == null)
            {
                Debug.LogError($"[HackingManager] {selected.gamePrefab.name} 프리팹 안에서 HackingGameBase를 상속한 미니게임 스크립트를 찾지 못했습니다.");

                Destroy(currentPopupObj);
                Destroy(currentGameObj);

                currentPopupObj = null;
                currentGameObj = null;

                IsHackingActive = false;
                externalResultCallback = null;
                return;
            }

            Debug.Log($"[HackingManager] 미니게임 인스턴스 생성 완료: {gameInstance.GetType().Name}");

            gameInstance.InitGame(level);
            popupView.InitializePopup(gameInstance, OnHackingFinished);

            currentLevel = level;
            HackingStarted?.Invoke(level, selected.gameType);
        }

        /// <summary>
        /// 팝업이 같은 캔버스의 형제 UI(본부 단말기 창 등)보다 뒤에 있으면 가려진다.
        /// 계층 순서와 무관하게 항상 위에 그려지도록 팝업에 정렬 우선 캔버스를 붙인다(버튼 클릭을 위해 Raycaster 도 함께).
        /// </summary>
        private static void BringPopupToFront(GameObject popup)
        {
            Canvas parentCanvas = popup.transform.parent != null
                ? popup.transform.parent.GetComponentInParent<Canvas>()
                : null;

            Canvas popupCanvas = popup.GetComponent<Canvas>();

            if (popupCanvas == null)
            {
                popupCanvas = popup.AddComponent<Canvas>();
            }

            popupCanvas.overrideSorting = true;
            popupCanvas.sortingOrder = (parentCanvas != null ? parentCanvas.rootCanvas.sortingOrder : 0) + PopupSortingOffset;

            if (popup.GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
            {
                popup.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            }
        }

        /// <summary>
        /// 미니게임이 성공 또는 실패로 끝났을 때 호출됩니다.
        /// </summary>
        private void OnHackingFinished(bool isSuccess)
        {
            if (!IsHackingActive) return;
            Debug.Log($"[HackingManager] 해킹 결과: {(isSuccess ? "성공" : "실패")}");

            IsHackingActive = false;

            externalResultCallback?.Invoke(isSuccess);
            externalResultCallback = null;

            currentPopupObj = null;
            currentGameObj = null;

            HackingFinished?.Invoke(currentLevel, isSuccess);
        }

        /// <summary>
        /// 테스트 중 팝업이 중간에 삭제되거나 상태가 꼬였을 때 강제로 초기화합니다.
        /// </summary>
        public void CancelForPlayerDeath()
        {
            if (!IsHackingActive) return;
            GameObject popup = currentPopupObj;
            GameObject game = currentGameObj;
            // 먼저 결과를 실패로 확정해 파괴 콜백의 중복 성공 처리를 막습니다.
            OnHackingFinished(false);
            if (popup != null) Destroy(popup);
            if (game != null) Destroy(game);
        }

        private void ForceResetHackingState()
        {
            IsHackingActive = false;
            externalResultCallback = null;

            currentPopupObj = null;
            currentGameObj = null;

            Debug.Log("[HackingManager] 해킹 상태 강제 초기화 완료");
        }
    }
}
