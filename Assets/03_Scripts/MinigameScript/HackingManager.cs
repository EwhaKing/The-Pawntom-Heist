using System.Collections.Generic;
using UnityEngine;

namespace Hacking
{
    /// <summary>
    /// 팝업 호출 및 상태 제어기.
    ///
    /// 지금 단계 목표: "테스트 키를 누르면 팝업이 뜨고, 미니게임이 돌고,
    /// 성공/실패 결과가 콘솔에 찍힌다"까지만 완성합니다.
    ///
    /// 문(Node)과의 상호작용, RPC 동기화, 쿨타임, 플레이어 입력 차단 등은
    /// 전부 [추후 연결 예정] 주석으로 남겨두었습니다. 나중에 주석만 풀면 바로 연결됩니다.
    /// </summary>
    public class HackingManager : MonoBehaviour
    {
        [Header("팝업 설정")]
        [SerializeField] private GameObject popupViewPrefab; // HackingPopupView가 붙은 프리팹
        [SerializeField] private Transform canvasParent;      // 팝업이 자식으로 생성될 Canvas

        [Header("미니게임 풀")]
        [SerializeField] private List<HackingGameData> hackingGamePool = new List<HackingGameData>();

        [Header("[TEST ONLY] 테스트 전용 설정")]
        [Tooltip("나중에 문 상호작용 코드로 교체될 임시 설정입니다.")]
        [SerializeField] private SecurityLevel testSecurityLevel = SecurityLevel.Normal;
        [SerializeField] private KeyCode testTriggerKey = KeyCode.H;

        public bool IsHackingActive { get; private set; }

        // [추후 연결 예정]
        // private int currentNodeId;
        // private Dictionary<int, float> cooldownDict = new Dictionary<int, float>();
        // [SerializeField] private PlayerInputController playerInput;

        private void Update()
        {
            // [TEST ONLY] 실제 구현에서는 문 상호작용(E키 등)이 이 자리를 대체합니다.
            if (Input.GetKeyDown(testTriggerKey) && !IsHackingActive)
            {
                OpenHackingPopup(testSecurityLevel);
            }
        }

        /// <summary>
        /// 지정된 보안 등급에 맞는 미니게임을 풀에서 골라 팝업으로 띄웁니다.
        /// </summary>
        public void OpenHackingPopup(SecurityLevel level)
        {
            if (IsHackingActive)
            {
                Debug.LogWarning("[HackingManager] 이미 해킹이 진행 중입니다.");
                return;
            }

            List<HackingGameData> candidates = hackingGamePool.FindAll(data => data.securityLevel == level);
            if (candidates.Count == 0)
            {
                Debug.LogWarning($"[HackingManager] '{level}' 등급에 등록된 미니게임이 없습니다. hackingGamePool을 확인하세요.");
                return;
            }

            HackingGameData selected = candidates[Random.Range(0, candidates.Count)];
            IsHackingActive = true;

            // [추후 연결 예정]
            // if (playerInput != null) playerInput.enabled = false;
            // Cursor.lockState = CursorLockMode.None;
            // Cursor.visible = true;

            GameObject popupObj = Instantiate(popupViewPrefab, canvasParent);
            HackingPopupView popupView = popupObj.GetComponent<HackingPopupView>();

            GameObject gameObj = Instantiate(selected.gamePrefab);
            HackingGameBase gameInstance = gameObj.GetComponent<HackingGameBase>();
            gameInstance.InitGame(level);

            popupView.InitializePopup(gameInstance, OnHackingFinished);
        }

        /// <summary>
        /// 미니게임 팝업이 닫힐 때 호출되는 콜백. 지금은 로그만 찍습니다.
        /// </summary>
        private void OnHackingFinished(bool isSuccess)
        {
            IsHackingActive = false;
            Debug.Log($"[HackingManager] 해킹 결과: {(isSuccess ? "성공" : "실패")}");

            // [추후 연결 예정]
            // if (isSuccess) SyncNodeStateRPC();
            // else ApplyNodeCooldown();

            // if (playerInput != null) playerInput.enabled = true;
            // Cursor.lockState = CursorLockMode.Locked;
            // Cursor.visible = false;
        }

        // [추후 연결 예정]
        // private void SyncNodeStateRPC() { }
        // private void ApplyNodeCooldown() { }
    }
}