using UnityEngine;

/// <summary>
/// ControllableWall
///
/// 담당:
/// - 실제 맵에 있는 격벽의 해금 상태 관리
/// - 실제 격벽 열림/닫힘 처리
/// - 열린 상태에서는 실제 벽 Renderer/Collider를 끔
/// - 닫힌 상태에서는 실제 벽 Renderer/Collider를 켬
/// - CCTV/미니맵용 격벽 표시만 빨강/파랑으로 변경
///
/// 사용 위치:
/// - 실제 맵의 Wall_01, Wall_02 같은 격벽 오브젝트에 붙임
/// </summary>
public class ControllableWall : MonoBehaviour
{
    [Header("State")]
    [SerializeField] private bool isUnlocked;
    [SerializeField] private bool isOpen;

    [Header("Actual Wall")]
    [Tooltip("실제 맵에서 보이는 벽 Renderer")]
    [SerializeField] private Renderer[] actualWallRenderers;

    [Tooltip("실제 플레이어를 막는 Collider")]
    [SerializeField] private Collider[] actualWallColliders;

    [Header("Minimap / CCTV Visual")]
    [Tooltip("CCTV/미니맵에 보이는 격벽 표시용 Renderer")]
    [SerializeField] private Renderer minimapWallRenderer;

    [Header("Minimap Colors")]
    [SerializeField] private Color minimapClosedColor = new Color(1f, 0.15f, 0.1f, 1f);
    [SerializeField] private Color minimapOpenColor = new Color(0.1f, 0.55f, 1f, 1f);

    public bool IsUnlocked => isUnlocked;
    public bool IsOpen => isOpen;

    private void Awake()
    {
        // Inspector에 직접 연결하지 않았을 때를 위한 자동 탐색
        if (actualWallRenderers == null || actualWallRenderers.Length == 0)
        {
            actualWallRenderers = GetComponentsInChildren<Renderer>();
        }

        if (actualWallColliders == null || actualWallColliders.Length == 0)
        {
            actualWallColliders = GetComponentsInChildren<Collider>();
        }

        ApplyWallState();
    }

    /// <summary>
    /// 격벽을 해금 상태로 변경
    /// </summary>
    public void Unlock()
    {
        isUnlocked = true;

        Debug.Log($"[ControllableWall] {gameObject.name} 해금 완료");

        ApplyWallState();
    }

    /// <summary>
    /// 격벽을 엽니다.
    /// 실제 벽은 사라지고, 미니맵/CCTV 표시는 파란색
    /// </summary>
    public void OpenWall()
    {
        isOpen = true;

        ApplyWallState();

        Debug.Log($"[ControllableWall] {gameObject.name} 열림");
    }

    /// <summary>
    /// 격벽을 닫습니다.
    /// 실제 벽은 다시 보이고, 미니맵/CCTV 표시는 빨간색
    /// </summary>
    public void CloseWall()
    {
        isOpen = false;

        ApplyWallState();

        Debug.Log($"[ControllableWall] {gameObject.name} 닫힘");
    }

    /// <summary>
    /// 해금된 격벽의 열림/닫힘을 전환
    /// </summary>
    public void ToggleWall()
    {
        if (!isUnlocked)
        {
            Debug.Log($"[ControllableWall] {gameObject.name} 아직 해금되지 않음");
            return;
        }

        if (isOpen)
        {
            CloseWall();
        }
        else
        {
            OpenWall();
        }
    }

    /// <summary>
    /// 현재 상태를 실제 벽과 미니맵/CCTV 표시용 벽에 반영
    /// </summary>
    private void ApplyWallState()
    {
        // 실제 벽은 색을 바꾸지 않고, 보이기/숨기기만 처리
        bool shouldShowActualWall = !isOpen;

        if (actualWallRenderers != null)
        {
            for (int i = 0; i < actualWallRenderers.Length; i++)
            {
                if (actualWallRenderers[i] == null)
                {
                    continue;
                }

                actualWallRenderers[i].enabled = shouldShowActualWall;
            }
        }

        // 실제 벽 Collider도 열리면 OFF, 닫히면 ON
        if (actualWallColliders != null)
        {
            for (int i = 0; i < actualWallColliders.Length; i++)
            {
                if (actualWallColliders[i] == null)
                {
                    continue;
                }

                actualWallColliders[i].enabled = !isOpen;
            }
        }

        // 미니맵/CCTV 표시용 벽만 색상 변경
        if (minimapWallRenderer != null)
        {
            minimapWallRenderer.material.color = isOpen ? minimapOpenColor : minimapClosedColor;
        }
    }
}