using Fusion;
using UnityEngine;

/// <summary>
/// PlayerHUDConnector
///
/// 담당:
/// - 로컬 플레이어가 Spawn되었을 때 HUD UI와 플레이어를 연결
/// - InventoryUI, MinimapCameraFollow, MinimapPlayerArrow 등을 연결
///
/// 사용 위치:
/// - Player 프리팹
/// </summary>
public class PlayerMinimapConnector : NetworkBehaviour
{
    private InventoryManager inventoryManager;

    private void Awake()
    {
        inventoryManager = GetComponent<InventoryManager>();
    }

    public override void Spawned()
    {
        if (!Object.HasInputAuthority)
            return;

        ConnectMinimap();
    }

    /// <summary>
    /// 로컬 플레이어를 미니맵 카메라와 플레이어 화살표에 연결
    /// </summary>
    private void ConnectMinimap()
    {
        MinimapCameraFollow minimapCamera = FindFirstObjectByType<MinimapCameraFollow>();

        if (minimapCamera != null)
        {
            minimapCamera.SetTarget(transform);
            Debug.Log("[PlayerMinimapConnector] MinimapCameraFollow 연결 완료");
        }
        else
        {
            Debug.LogWarning("[PlayerMinimapConnector] MinimapCameraFollow를 찾지 못함");
        }

        MinimapPlayerArrow playerArrow = FindFirstObjectByType<MinimapPlayerArrow>();

        if (playerArrow != null)
        {
            playerArrow.SetTarget(transform);
            Debug.Log("[PlayerMinimapConnector] MinimapPlayerArrow 연결 완료");
        }
        else
        {
            Debug.LogWarning("[PlayerMinimapConnector] MinimapPlayerArrow를 찾지 못함");
        }
    }
}
