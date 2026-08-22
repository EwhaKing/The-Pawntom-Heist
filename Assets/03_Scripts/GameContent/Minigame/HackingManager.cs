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
    ///
    /// 필요 연결:
    /// - Popup View Prefab: HackingPopup.prefab
    /// - Canvas Parent: HUDCanvas 안의 PopupRoot 또는 HUDCanvas
    /// - Hacking Game Pool: CommandOverrideGamePrefab, SignalDivideGamePrefab 등
    /// </summary>
    public class HackingManager : MonoBehaviour
    {
        [Header("팝업 설정")]
        [SerializeField] private GameObject popupViewPrefab;
        [SerializeField] private Transform canvasParent;

        [Header("미니게임 풀")]
        [SerializeField] private List<HackingGameData> hackingGamePool = new List<HackingGameData>();

        [Header("[TEST ONLY] 테스트 전용 설정")]
        [Tooltip("나중에 문 상호작용 코드로 교체될 임시 설정입니다.")]
        [SerializeField] private SecurityLevel testSecurityLevel = SecurityLevel.Normal;
        [SerializeField] private KeyCode testTriggerKey = KeyCode.H;

        public bool IsHackingActive { get; private set; }

        /// <summary>
        /// 외부에서 해킹 결과를 받고 싶을 때 저장되는 콜백
        /// 예: MainbaseControlUI가 해킹 성공 여부를 받아야 할 때 사용
        /// </summary>
        private System.Action<bool> externalResultCallback;

        private void Update()
        {
            // [TEST ONLY]
            // H 키를 누르면 테스트용으로 해킹 미니게임을 실행
            // 이 경우 외부 콜백은 사용하지 않습니다.
            if (Input.GetKeyDown(testTriggerKey) && !IsHackingActive)
            {
                OpenHackingPopup(testSecurityLevel);
            }
        }

        /// <summary>
        /// 지정된 보안 등급에 맞는 미니게임을 풀에서 골라 팝업으로 띄움
        ///
        /// 테스트용 또는 결과를 따로 받을 필요가 없는 경우 사용
        /// </summary>
        public void OpenHackingPopup(SecurityLevel level)
        {
            OpenHackingPopup(level, null);
        }

        /// <summary>
        /// 지정된 보안 등급에 맞는 미니게임을 풀에서 골라 팝업으로 띄웁니다.
        ///
        /// 외부에서 해킹 결과를 받아야 할 때 사용하는 함수
        /// 예:
        /// - MainbaseControlUI에서 조작 버튼 클릭
        /// - 해킹 성공 시 격벽 열기
        /// - 해킹 실패 시 아무 일도 안 함
        /// </summary>
        public void OpenHackingPopup(SecurityLevel level, System.Action<bool> resultCallback)
        {
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

            List<HackingGameData> candidates = hackingGamePool.FindAll(data =>
                data != null &&
                data.securityLevel == level &&
                data.gamePrefab != null
            );

            if (candidates.Count == 0)
            {
                Debug.LogWarning($"[HackingManager] '{level}' 등급에 등록된 미니게임이 없습니다. Hacking Game Pool을 확인하세요.");
                return;
            }

            HackingGameData selected = candidates[Random.Range(0, candidates.Count)];

            Debug.Log($"[HackingManager] 선택된 미니게임: {selected.gameType}");

            IsHackingActive = true;
            externalResultCallback = resultCallback;

            // [추후 연결 예정]
            // 플레이어 이동 입력 막기, 커서 보이기 등은 나중에 여기서 처리
            // Cursor.lockState = CursorLockMode.None;
            // Cursor.visible = true;

            GameObject popupObj = Instantiate(popupViewPrefab, canvasParent);

            // HackingPopupView가 루트가 아니라 자식에 붙어 있어도 찾을 수 있게 처리
            HackingPopupView popupView = popupObj.GetComponentInChildren<HackingPopupView>(true);

            if (popupView == null)
            {
                Debug.LogError("[HackingManager] Popup View Prefab 안에서 HackingPopupView 컴포넌트를 찾지 못했습니다.");

                Destroy(popupObj);
                IsHackingActive = false;
                externalResultCallback = null;
                return;
            }

            GameObject gameObj = Instantiate(selected.gamePrefab);

            // 미니게임 스크립트가 루트가 아니라 자식에 붙어 있어도 찾을 수 있게 처리
            HackingGameBase gameInstance = gameObj.GetComponentInChildren<HackingGameBase>(true);

            if (gameInstance == null)
            {
                Debug.LogError($"[HackingManager] {selected.gamePrefab.name} 프리팹 안에서 HackingGameBase를 상속한 미니게임 스크립트를 찾지 못했습니다.");

                Destroy(popupObj);
                Destroy(gameObj);

                IsHackingActive = false;
                externalResultCallback = null;
                return;
            }

            gameInstance.InitGame(level);
            popupView.InitializePopup(gameInstance, OnHackingFinished);
        }

        /// <summary>
        /// 미니게임이 성공 또는 실패로 끝났을 때 호출
        /// </summary>
        private void OnHackingFinished(bool isSuccess)
        {
            IsHackingActive = false;

            Debug.Log($"[HackingManager] 해킹 결과: {(isSuccess ? "성공" : "실패")}");

            // MainbaseControlUI 같은 외부 시스템에 결과 전달
            externalResultCallback?.Invoke(isSuccess);
            externalResultCallback = null;

            // [추후 연결 예정]
            // 플레이어 입력 복구, 커서 잠금 등은 나중에 여기서 처리
            // Cursor.lockState = CursorLockMode.Locked;
            // Cursor.visible = false;
        }
    }
}