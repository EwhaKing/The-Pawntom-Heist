using UnityEngine;

public class ItemSpawner : MonoBehaviour
{
    [Header("랜덤 아이템 프리팹들")]
    public GameObject[] itemPrefabs;

    [Header("스폰 지점")]
    public Transform[] spawnPoints;
    
    [Header("랜덤 설정")]
    [Range(0f, 1f)]
    public float spawnChance = 0.5f;

    public float yOffset = 25f;
    public bool spawnOnStart = true;

    private void Start()
    {
        if (spawnOnStart)
        {
            SpawnItems();
        }
    }

    public void SpawnItems()
    {
        if (itemPrefabs == null || itemPrefabs.Length == 0)
        {
            Debug.LogWarning("아이템 프리팹이 비어있음!");
            return;
        }

        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogError("Spawn Points가 비어있음!");
            return;
        }

        foreach (Transform spawnPoint in spawnPoints)
        {
            if (spawnPoint == null)
            {
                Debug.LogWarning("Spawn Points 배열 빈칸 있음!");
                continue;
            }

            if (Random.value > spawnChance)
            {
                Debug.Log(spawnPoint.name +" : 아이템 스폰 안됌");
                continue;
            }
            int randomIndex = Random.Range(0, itemPrefabs.Length);
            GameObject selectedItem = itemPrefabs[randomIndex];

            if (selectedItem == null)
            {
                Debug.LogWarning("아이템 프리팹 배열 빈칸 있음!");
                continue;
            }

            Vector3 spawnPosition = spawnPoint.position + Vector3.up * yOffset;

            GameObject spawnedItem = Instantiate(selectedItem, spawnPosition, spawnPoint.rotation);

            Debug.Log(spawnPoint.name + " 위치에 " + spawnedItem.name + " 생성됨");
        }
    }
}