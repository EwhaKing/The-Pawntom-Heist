using UnityEngine;

public class ExitInteraction : MonoBehaviour
{
    public Camera playerCamera;
    public float interactDistance = 50f;
    public float interactRadius = 0.5f;

    public string requiredItemName = "Catleaf";

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
            TryExit();
        }
    }

    private void TryExit()
    {
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        Debug.DrawRay(ray.origin, ray.direction * interactDistance, Color.green, 2f);

        if (Physics.SphereCast(ray, interactRadius, out RaycastHit hit, interactDistance))
        {
            ExitObject exit = hit.collider.GetComponentInParent<ExitObject>();
            if (exit != null)
            {
                if (Inventory.Instance == null)
                {
                    Debug.LogError("Inventory.Instance가 없음!");
                    return;
                }

                if (Inventory.Instance.HasItem(requiredItemName))
                {
                    Debug.Log("탈출에 성공했다!");
                }

                else
                {
                    Debug.Log(requiredItemName + "를 훔쳐야 탈출할 수 있습니다!");
                }
            }
        }
    }
}
