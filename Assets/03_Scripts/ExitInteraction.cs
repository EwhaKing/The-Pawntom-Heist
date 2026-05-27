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
        if (Input.GetMouseButtonDown(1))
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
            Debug.Log($"[탈출 레이저] {hit.collider.name}을(를) 조준했습니다.");
            ExitObject exit = hit.collider.GetComponent<ExitObject>();
            
            if (exit == null)
                exit = hit.collider.GetComponentInParent<ExitObject>();
            
            if (exit != null)
            {
                if (Inventory.Instance == null)
                {
                    Debug.LogError("Inventory.Instance가 없음!");
                    return;
                }

                if (Inventory.Instance.HasItem(requiredItemName))
                {
                    Debug.Log("🎉 탈출에 성공했다!");
                    CompleteLevel();
                }
                else
                {
                    Debug.Log(requiredItemName + "를 훔쳐야 탈출할 수 있습니다!");
                }
            }else
            {
                // 🔍 [추가] 물체는 맞췄는데 ExitObject 명찰을 못 찾았을 때 치는 대사
                Debug.Log($"[탈출 레이저] {hit.collider.name} 또는 그 부모 오브젝트에 'ExitObject' 스크립트가 붙어있지 않습니다!");
            }
        }
        else
        {
            // 🔍 [추가] 레이저가 허공을 날아갔을 때 치는 대사
            Debug.Log("[탈출 레이저] 아무것도 맞추지 못했습니다. 큐브를 정확히 조준해 주세요.");
        }
    }

    private void CompleteLevel()
    {
        Time.timeScale = 0f;
        Debug.Log("✅ 레벨 완료! (시간 정지)");
    }
}
