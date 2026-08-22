using UnityEngine;

/// <summary>
/// ControllableWall
///
/// 담당:
/// - 실제 맵에 있는 격벽의 해금 상태 관리
/// - 격벽 열림/닫힘 상태 관리
/// - 열림 상태면 Collider를 끄고, 닫힘 상태면 Collider를 켬
///
/// 사용 위치:
/// - 실제 맵의 Gate_01, Gate_02 같은 격벽 오브젝트에 붙임
/// </summary>
public class ControllableWall : MonoBehaviour
{
    [Header("State")]
    [SerializeField] private bool isUnlocked;
    [SerializeField] private bool isOpen;

    [Header("Components")]
    [SerializeField] private Collider wallCollider;
    [SerializeField] private Renderer wallRenderer;

    [Header("World Colors")]
    [SerializeField] private Color closedColor = Color.red;
    [SerializeField] private Color openColor = Color.blue;

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
    /// 격벽을 해금 상태로 변경
    /// </summary>
    public void Unlock()
    {
        isUnlocked = true;
        Debug.Log($"[ControllableWall] {gameObject.name} 해금 완료");
    }

    /// <summary>
    /// 격벽 열기
    /// </summary>
    public void OpenWall()
    {
        isOpen = true;
        ApplyWallState();

        Debug.Log($"[ControllableWall] {gameObject.name} 열림");
    }

    /// <summary>
    /// 격벽 닫기
    /// </summary>
    public void CloseWall()
    {
        isOpen = false;
        ApplyWallState();

        Debug.Log($"[ControllableWall] {gameObject.name} 닫힘");
    }

    /// <summary>
    /// 현재 열림/닫힘 상태 변경
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
    /// Collider와 색상에 현재 상태를 반영
    /// </summary>
    private void ApplyWallState()
    {
        if (wallCollider != null)
        {
            wallCollider.enabled = !isOpen;
        }

        if (wallRenderer != null)
        {
            wallRenderer.material.color = isOpen ? openColor : closedColor;
        }
    }
}