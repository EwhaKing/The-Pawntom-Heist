using UnityEngine;

public class ItemPickup : MonoBehaviour
{
    public Camera playerCamera;
    public float pickupDistance = 50f;
    public float pickupRadius = 0.5f;

    private bool pickupPressed;

    private void Start()
    {
        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }
    }
    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            TryPickup();
        }
    }

    private void TryPickup()
    {
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);

        Debug.DrawRay(ray.origin, ray.direction * pickupDistance, Color.red, 2f);

        if (Physics.SphereCast(ray, pickupRadius, out RaycastHit hit, pickupDistance))
        {
            GameObject hitObject = hit.collider.gameObject;
            Debug.Log($"🎯 맞힌 오브젝트: {hitObject.name}");

            // 1순위: 맞힌 오브젝트 자체
            ItemObject item = hitObject.GetComponent<ItemObject>();
            
            // 2순위: 부모들을 위로 탐색
            if (item == null)
            {
                Transform parent = hitObject.transform.parent;
                int depth = 0;
                while (parent != null && depth < 10)
                {
                    item = parent.GetComponent<ItemObject>();
                    if (item != null)
                        break;
                    parent = parent.parent;
                    depth++;
                }
            }
            
            // 3순위: 자식들을 재귀 탐색
            if (item == null)
                item = FindItemObjectInChildren(hitObject.transform);
            
            // 4순위: collider의 rigidbody 확인
            if (item == null && hit.collider.attachedRigidbody != null)
            {
                item = hit.collider.attachedRigidbody.GetComponent<ItemObject>();
            }

            if (item != null)
            {
                Inventory.Instance.AddItem(item);
                Debug.Log($"✅ {item.itemName}을 획득했습니다!");
                Destroy(item.gameObject);
            }
            else
            {
                // 상세한 디버그 정보
                Debug.LogWarning($"❌ {hitObject.name}에서 ItemObject를 찾지 못함!");
                LogObjectStructure(hitObject.transform);
            }
        }
        else
        {
            Debug.Log("진짜 아이템을 맞추지 못함");
        }
    }

    private ItemObject FindItemObjectInChildren(Transform parent)
    {
        ItemObject item = parent.GetComponent<ItemObject>();
        if (item != null)
            return item;

        foreach (Transform child in parent)
        {
            item = FindItemObjectInChildren(child);
            if (item != null)
                return item;
        }
        return null;
    }

    private void LogObjectStructure(Transform obj, int depth = 0)
    {
        string indent = new string(' ', depth * 2);
        Debug.Log($"{indent}📦 {obj.name} (Components: {obj.GetComponents<Component>().Length})");
        
        if (depth < 5)
        {
            foreach (Transform child in obj)
            {
                LogObjectStructure(child, depth + 1);
            }
        }
    }
}