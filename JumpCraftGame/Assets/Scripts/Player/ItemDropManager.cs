using UnityEngine;

public class ItemDropManager : MonoBehaviour
{
    public static ItemDropManager Instance;

    [Header("World Prefabs for Dropping")]
    public GameObject item1WorldPrefab; // Bone prefab
    public GameObject item2WorldPrefab; // Skull prefab

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    // Overloads for Player 1 (defaults to 1 if no ID is passed)
    public void DropItem1() { DropItem1(1); }
    public void DropItem2() { DropItem2(1); }

    // Parameterized methods supporting Player 1 or Player 2
    public void DropItem1(int playerID)
    {
        if (HUDManager.Instance != null && HUDManager.Instance.GetItemCount(playerID, "bone") > 0)
        {
            int current = HUDManager.Instance.GetItemCount(playerID, "bone");
            HUDManager.Instance.SetItemCountDirect(playerID, "bone", current - 1);

            SpawnItemInFrontOfPlayer(playerID, item1WorldPrefab);
            HUDManager.Instance.UpdateUI();
        }
    }

    public void DropItem2(int playerID)
    {
        if (HUDManager.Instance != null && HUDManager.Instance.GetItemCount(playerID, "skull") > 0)
        {
            int current = HUDManager.Instance.GetItemCount(playerID, "skull");
            HUDManager.Instance.SetItemCountDirect(playerID, "skull", current - 1);

            SpawnItemInFrontOfPlayer(playerID, item2WorldPrefab);
            HUDManager.Instance.UpdateUI();
        }
    }

    private void SpawnItemInFrontOfPlayer(int playerID, GameObject prefabToSpawn)
    {
        if (prefabToSpawn != null)
        {
            string targetTag = (playerID == 2) ? "Player2" : "Player1";
            GameObject player = GameObject.FindWithTag(targetTag);
            if (player == null) player = GameObject.FindWithTag("Player");

            Vector3 spawnPosition = player != null ? player.transform.position + new Vector3(1.5f, 2f, 0f) : Vector3.zero;
            GameObject spawnedItem = Instantiate(prefabToSpawn, spawnPosition, Quaternion.identity);

            WorldItem worldItem = spawnedItem.GetComponent<WorldItem>();
            if (worldItem != null) worldItem.droppedByPlayer = true;
        }
    }
}