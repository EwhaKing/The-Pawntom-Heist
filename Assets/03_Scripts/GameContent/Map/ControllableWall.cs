using UnityEngine;

/// <summary>
/// ControllableWall
///
/// 담당:
/// - 실제 맵에 있는 격벽의 해금 상태 관리
/// - 격벽 열림/닫힘 상태 관리
/// - 실제 Collider ON/OFF
/// - 실제 벽 색상 변경
/// - CCTV/미니맵용 벽 표시 색상 변경
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
    [SerializeField] private Collider wallCollider;
    [SerializeField] private Renderer wallRenderer;

    [Header("Minimap / CCTV Visual")]
    [SerializeField] private Renderer minimapWallRenderer;

    [Header("Colors")]
    [SerializeField] private Color closedColor = new Color(1f, 0.15f, 0.1f, 1f);
    [SerializeField] private Color openColor = new Color(0.1f, 0.55f, 1f, 1f);

    public bool IsUnlocked => isUnlocked;
    public bool IsOpen => isOpen;

    private void Awake()
    {
        if (wallCollider == null)
        {
            wallCollider = GetComponent<Collider>();
        }

        if (wallRenderer == null)
        {
            wallRenderer = GetComponentInChildren<Renderer>();
        }

        ApplyWallState();
    }

    /// <summary>
    /// 격벽을 해금 상태로 변경합니다.
    /// </summary>
    public void Unlock()
    {
        isUnlocked = true;
        Debug.Log($"[ControllableWall] {gameObject.name} 해금 완료");

        ApplyWallState();
    }

    /// <summary>
    /// 격벽을 엽니다.
    /// </summary>
    public void OpenWall()
    {
        isOpen = true;
        ApplyWallState();

        Debug.Log($"[ControllableWall] {gameObject.name} 열림");
    }

    /// <summary>
    /// 격벽을 닫습니다.
    /// </summary>
    public void CloseWall()
    {
        isOpen = false;
        ApplyWallState();

        Debug.Log($"[ControllableWall] {gameObject.name} 닫힘");
    }

    /// <summary>
    /// 해금된 격벽의 열림/닫힘을 전환합니다.
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
    /// 현재 상태를 실제 맵과 미니맵/CCTV 표시 모두에 반영합니다.
    /// </summary>
    private void ApplyWallState()
    {
        Color currentColor = isOpen ? openColor : closedColor;

        // 실제 격벽 충돌 처리
        if (wallCollider != null)
        {
            wallCollider.enabled = !isOpen;
        }

        // 실제 맵 격벽 색상
        if (wallRenderer != null)
        {
            wallRenderer.material.color = currentColor;
        }

        // 미니맵 / CCTV용 격벽 색상
        if (minimapWallRenderer != null)
        {
            minimapWallRenderer.material.color = currentColor;
        }
    }
}