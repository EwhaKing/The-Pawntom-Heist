using UnityEngine;

/// <summary>
/// MinimapCameraFollow
///
/// 담당:
/// - 미니맵 카메라가 로컬 플레이어의 위치를 따라가게 함
/// - 카메라는 항상 위에서 아래를 내려다봄
/// - 북쪽 고정 방식이므로 카메라 회전은 고정
///
/// 사용 위치:
/// - Map_Scene의 MinimapCamera 오브젝트
/// </summary>
public class MinimapCameraFollow : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;

    [Header("Camera Settings")]
    [SerializeField] private float height = 30f;

    /// <summary>
    /// 미니맵 카메라가 따라갈 대상을 설정
    /// 로컬 플레이어가 Spawn된 후 호출
    /// </summary>
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        transform.position = new Vector3(
            target.position.x,
            target.position.y + height,
            target.position.z
        );

        transform.rotation = Quaternion.Euler(90f, 0f, 0f);
    }
}
