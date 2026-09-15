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

        public bool IsHackingActive { get; private set; }

        private System.Action<bool> externalResultCallback;

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

            HackingGameData selected = candidates[Random.Range(0, candidates.Count)];

            Debug.Log($"[HackingManager] 선택된 미니게임: {selected.gameType}, Prefab={selected.gamePrefab.name}");

            IsHackingActive = true;
            externalResultCallback = resultCallback;

            // 팝업 생성
            currentPopupObj = Instantiate(popupViewPrefab, canvasParent);
            currentPopupObj.SetActive(true);

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
        }

        /// <summary>
        /// 미니게임이 성공 또는 실패로 끝났을 때 호출됩니다.
        /// </summary>
        private void OnHackingFinished(bool isSuccess)
        {
            Debug.Log($"[HackingManager] 해킹 결과: {(isSuccess ? "성공" : "실패")}");

            IsHackingActive = false;

            externalResultCallback?.Invoke(isSuccess);
            externalResultCallback = null;

            currentPopupObj = null;
            currentGameObj = null;
        }

        /// <summary>
        /// 테스트 중 팝업이 중간에 삭제되거나 상태가 꼬였을 때 강제로 초기화합니다.
        /// </summary>
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