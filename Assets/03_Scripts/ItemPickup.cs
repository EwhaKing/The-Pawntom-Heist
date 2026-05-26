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
            ItemObject item = hit.collider.GetComponentInParent<ItemObject>();

            if (item != null)
            {
                Inventory.Instance.AddItem(item);
                Destroy(item.gameObject);
            }
            else
            {
                Debug.Log("맞췄지만 ItemObject가 없음");
            }
        }
        else
        {
            Debug.Log("진짜 아이템을 맞추지 못함");
        }
    }
}