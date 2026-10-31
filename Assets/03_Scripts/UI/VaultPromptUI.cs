using Fusion;
using UnityEngine;
using TMPro;

/// <summary>
/// VaultPromptUI
///
/// 담당:
/// - 로컬 플레이어와 금고 사이의 거리 검사
/// - 현재 선택한 인벤토리 아이템 검사
/// - 금고 상태에 따른 안내 문구 실시간 표시
///
/// 사용 위치:
/// - Canvas_HUD > VaultPromptUI
///
/// 주의:
/// - VaultPromptUI 오브젝트는 Active ON 유지
/// - 금고가 열리면 문구를 자동으로 숨깁니다.
/// </summary>
public class VaultPromptUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI promptText;

    [Header("Vault")]
    [Tooltip("TutorialVault_Main의 VaultChest를 연결합니다.")]
    [SerializeField] private VaultChest targetVault;

    private PlayerInteraction localPlayer;
    private VaultKeyInventoryBridge keyBridge;

    private void Awake()
    {
        if (promptText != null)
            promptText.text = "";
    }

    private void Update()
    {
        if (promptText == null)
            return;

        // 씬에서 금고 찾기
        if (targetVault == null)
        {
            targetVault = FindFirstObjectByType<VaultChest>();
        }

        // 로컬 플레이어 찾기
        if (localPlayer == null)
        {
            FindLocalPlayer();
        }

        if (targetVault == null || localPlayer == null)
        {
            HidePrompt();
            return;
        }

        // UI 또는 해킹 팝업이 열려 있다면 안내 숨김
        if (GameplayInputBlocker.IsBlocked ||
            Hacking.HackingManager.AnyHackingActive)
        {
            HidePrompt();
            return;
        }

        // 금고가 아직 Fusion에서 Spawn되지 않았다면 숨김
        if (targetVault.Object == null ||
            !targetVault.Object.IsValid)
        {
            HidePrompt();
            return;
        }

        // 이미 금고를 열었다면 안내 숨김
        if (targetVault.IsOpened)
        {
            HidePrompt();
            return;
        }

        // 플레이어와 금고 사이의 거리
        float distance = Vector3.Distance(
            localPlayer.transform.position,
            targetVault.transform.position
        );

        // 금고에서 너무 멀다면 안내 숨김
        if (distance > targetVault.MaxUseDistance)
        {
            HidePrompt();
            return;
        }

        // 현재 선택된 인벤토리 슬롯에 열쇠가 있는지 확인
        bool hasSelectedKey =
            keyBridge != null && keyBridge.HasSelectedKey;

        // 금고 상태에 맞는 안내 문구 출력
        promptText.text =
            targetVault.GetPrompt(hasSelectedKey);
    }

    /// <summary>
    /// 현재 로컬 플레이어와
    /// 해당 플레이어의 금고 열쇠 관리 스크립트를 찾습니다.
    /// </summary>
    private void FindLocalPlayer()
    {
        PlayerInteraction[] players =
            FindObjectsByType<PlayerInteraction>(
                FindObjectsSortMode.None
            );

        foreach (PlayerInteraction player in players)
        {
            if (player.Object == null ||
                !player.Object.IsValid)
                continue;

            if (!player.Object.HasInputAuthority)
                continue;

            localPlayer = player;

            keyBridge =
                player.GetComponent<VaultKeyInventoryBridge>();

            if (keyBridge == null)
            {
                Debug.LogWarning(
                    "[VaultPromptUI] VaultKeyInventoryBridge가 없습니다."
                );
            }

            Debug.Log(
                "[VaultPromptUI] 로컬 플레이어 연결 성공"
            );

            return;
        }
    }

    /// <summary>
    /// 금고 안내 문구 숨김.
    /// </summary>
    private void HidePrompt()
    {
        if (promptText != null)
            promptText.text = "";
    }
}
