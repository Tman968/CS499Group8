using UnityEngine;

public class Skeleton : MonoBehaviour
{
    [Header("Item Drop 1 Settings")]
    public GameObject itemDropPrefab1;
    [Range(0f, 100f)]
    public float dropChance1 = 75f;
    public int minAmount1 = 1;
    public int maxAmount1 = 1;

    [Header("Item Drop 2 Settings")]
    public GameObject itemDropPrefab2;
    [Range(0f, 100f)]
    public float dropChance2 = 20f;
    public int minAmount2 = 1;
    public int maxAmount2 = 1;

    [Header("Spawn Settings")]
    [Tooltip("Offset from the skeleton's center where items will spawn.")]
    public Vector3 spawnOffset = new Vector3(0f, -0.5f, 0f);
    [Tooltip("Slight random spread so items don't spawn in the exact same pixel.")]
    public float dropSpread = 0.3f;

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            DieAndDropItems();
        }
    }

    void DieAndDropItems()
    {
        Vector3 baseSpawnPos = transform.position + spawnOffset;

        // Roll and drop Item 1 (supports multiple amounts)
        if (itemDropPrefab1 != null && Random.value <= (dropChance1 / 100f))
        {
            int count1 = Random.Range(minAmount1, maxAmount1 + 1);
            for (int i = 0; i < count1; i++)
            {
                Vector3 scatterPos = baseSpawnPos + new Vector3(Random.Range(-dropSpread, dropSpread), Random.Range(-dropSpread, dropSpread), 0f);
                GameObject dropped = Instantiate(itemDropPrefab1, scatterPos, Quaternion.identity);

                // Ensure enemy drops are not flagged as player-dropped
                WorldItem wi = dropped.GetComponent<WorldItem>();
                if (wi != null) wi.droppedByPlayer = false;
            }
        }

        // Roll and drop Item 2 (supports multiple amounts)
        if (itemDropPrefab2 != null && Random.value <= (dropChance2 / 100f))
        {
            int count2 = Random.Range(minAmount2, maxAmount2 + 1);
            for (int i = 0; i < count2; i++)
            {
                Vector3 scatterPos = baseSpawnPos + new Vector3(Random.Range(-dropSpread, dropSpread), Random.Range(-dropSpread, dropSpread), 0f);
                GameObject dropped = Instantiate(itemDropPrefab2, scatterPos, Quaternion.identity);

                // Ensure enemy drops are not flagged as player-dropped
                WorldItem wi = dropped.GetComponent<WorldItem>();
                if (wi != null) wi.droppedByPlayer = false;
            }
        }

        // Destroy the skeleton
        Destroy(gameObject);
    }
}