using UnityEngine;

/// <summary>
/// MinimapPlayerArrow
///
/// 담당:
/// - 북쪽 고정 미니맵에서 플레이어의 방향을 UI 화살표로 표시
/// - 실제 플레이어의 Y축 회전을 UI의 Z축 회전으로 변환
///
/// 사용 위치:
/// - HUDCanvas > MinimapUI > PlayerArrow 오브젝트
/// </summary>
public class MinimapPlayerArrow : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform targetPlayer;

    [Header("Rotation")]
    [SerializeField] private float rotationOffset = 180f;

    /// <summary>
    /// 화살표가 바라볼 대상 플레이어를 설정
    /// 로컬 플레이어가 Spawn된 후 호출
    /// </summary>
    public void SetTarget(Transform newTarget)
    {
        targetPlayer = newTarget;
    }

    private void Update()
    {
        if (targetPlayer == null)
            return;
        
        transform.localRotation = Quaternion.Euler(0f, 0f, -targetPlayer.eulerAngles.y + rotationOffset);
    }
}
