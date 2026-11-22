using Fusion;
using UnityEngine;

/// <summary>
/// 파쿠르 코스의 낙하 구역. 플레이어가 들어오면 지정한 지점으로 되돌린다(철봉에서 떨어졌을 때 다시 시도).
/// 트리거 콜라이더가 붙은 오브젝트에 단다.
/// </summary>
[RequireComponent(typeof(Collider))]
public class ParkourFallZone : MonoBehaviour
{
    [Tooltip("떨어진 플레이어를 돌려보낼 위치·방향")]
    [SerializeField] private Transform respawnPoint;

    private void OnTriggerEnter(Collider other)
    {
        NetworkCharacterController controller = other.GetComponentInParent<NetworkCharacterController>();

        if (respawnPoint == null || controller == null ||
            controller.Object == null || !controller.Object.IsValid || !controller.Object.HasStateAuthority)
        {
            return;
        }

        PlayerParkour parkour = controller.GetComponent<PlayerParkour>();

        if (parkour != null)
        {
            parkour.ResetState();
        }

        controller.Teleport(respawnPoint.position, respawnPoint.rotation);
        controller.Velocity = Vector3.zero;
    }
}
