using UnityEngine;

/// <summary>
/// ControlMachine
///
/// 담당:
/// - Mainbase 안의 조작 기계 상호작용
/// - 플레이어가 E키로 상호작용하면 MainbaseControlUI를 엶
///
/// 사용 위치:
/// - MainbaseControlMachine 오브젝트에 붙임
/// </summary>
public class ControlMachine : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private MainbaseControlUI controlUI;

    [Header("상호작용 안내")]
    [Tooltip("가까이 가서 겨눴을 때 \"E | 이름\" 으로 보일 이름")]
    [SerializeField] private string interactName = "본부 단말기";

    public string InteractName
    {
        get { return interactName; }
    }

    [Tooltip("단말기 둘레로 이만큼(m) 넓힌 상자 안에서만 상호작용할 수 있다. 튜토리얼의 연초록 안내 상자와 같은 크기(기본 1.2 = 안내 상자의 넓힘 2.4 의 절반)")]
    [SerializeField, Min(0f)] private float interactMargin = 1.2f;

    /// <summary>
    /// 플레이어(수평 위치)가 단말기 앞 상호작용 상자 안에 있는가. 상자 = 단말기 경계를 사방(수평)으로 <c>interactMargin</c> 만큼 넓힌 것.
    /// 멀리서 단말기를 겨누기만 해도 E 가 먹던 것을 막는다.
    /// </summary>
    public bool IsPlayerInRange(Vector3 playerPosition)
    {
        Bounds box = ComputeBounds();
        box.Expand(new Vector3(interactMargin * 2f, 0f, interactMargin * 2f));

        return playerPosition.x >= box.min.x && playerPosition.x <= box.max.x &&
               playerPosition.z >= box.min.z && playerPosition.z <= box.max.z;
    }

    // 단말기 경계: 충돌체(트리거 제외) 우선, 없으면 렌더러. 튜토리얼 안내 상자가 쓰는 계산과 같다.
    private Bounds ComputeBounds()
    {
        bool has = false;
        Bounds bounds = new Bounds(transform.position, Vector3.one);

        foreach (Collider col in GetComponentsInChildren<Collider>(true))
        {
            if (col.isTrigger || !col.enabled || col.bounds.size == Vector3.zero)
            {
                continue;
            }

            if (!has)
            {
                bounds = col.bounds;
                has = true;
            }
            else
            {
                bounds.Encapsulate(col.bounds);
            }
        }

        if (has)
        {
            return bounds;
        }

        foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
        {
            if (renderer.bounds.size == Vector3.zero)
            {
                continue;
            }

            if (!has)
            {
                bounds = renderer.bounds;
                has = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return bounds;
    }

    private void Awake()
    {
        if (controlUI == null)
        {
            controlUI = FindFirstObjectByType<MainbaseControlUI>();
        }
    }

    public void TryInteract()
    {
        Debug.Log("[ControlMachine] 조작 기계 상호작용");

        if (controlUI == null)
        {
            controlUI = FindFirstObjectByType<MainbaseControlUI>();
        }

        if (controlUI == null)
        {
            Debug.LogWarning("[ControlMachine] MainbaseControlUI를 찾지 못했습니다.");
            return;
        }

        controlUI.Open();
    }
}