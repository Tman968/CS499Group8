using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class HUDManager : MonoBehaviour
{
    public static HUDManager Instance;

    [Header("HUD Icon References")]
    public GameObject item1IconUI;
    public GameObject item2IconUI;
    public GameObject craftedItemIconUI;

    [Header("UI Text Count References")]
    public TextMeshProUGUI item1CountText;
    public TextMeshProUGUI item2CountText;

    [Header("World Prefabs for Dropping")]
    public GameObject item1WorldPrefab;
    public GameObject item2WorldPrefab;
    public bool BoneCollected => item1Count > 0;

    // SCALABLE DICTIONARY: Tracks count for ANY item ID automatically!
    private Dictionary<string, int> itemCounts = new Dictionary<string, int>();

    // Backward-compatible properties for your existing drop/craft methods
    public int item1Count { get => GetItemCount("bone"); set => SetItemCount("bone", value); }
    public int item2Count { get => GetItemCount("skull"); set => SetItemCount("skull", value); }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
        UpdateUI();
    }

    public void CollectItem(string id, int amount)
    {
        if (string.IsNullOrEmpty(id)) return;
        string cleanID = id.Trim().ToLower();

        // Standardize common variations to main IDs
        if (cleanID == "skeletonitem" || cleanID == "skeletonitembone") cleanID = "bone";
        if (cleanID == "raregem" || cleanID == "skeletonitemskull") cleanID = "skull";

        int current = GetItemCount(cleanID);
        SetItemCount(cleanID, current + amount);

        // Trigger notifications with custom text based on what was picked up
        if (cleanID == "bone")
        {
            if (ItemNotificationPopup.Instance != null)
                ItemNotificationPopup.Instance.TriggerPopup("Recipe Book Updated!");
        }
        else if (cleanID == "skullkey")
        {
            if (ItemNotificationPopup.Instance != null)
                ItemNotificationPopup.Instance.TriggerPopup("Exit Key Acquired!");

            if (ItemPickupDisplay.Instance != null)
            {
                ItemPickupDisplay.Instance.ShowImage();
            }
        }
        else if (cleanID == "skullwand" || cleanID == "item3")
        {
            if (ItemNotificationPopup.Instance != null)
                ItemNotificationPopup.Instance.TriggerPopup("Weapon Updated!");
        }

        UpdateUI();
    }

    public void CollectItem(string id) { CollectItem(id, 1); }

    // SCALABLE GETTER: Query count for any item ID
    public int GetItemCount(string id)
    {
        if (string.IsNullOrEmpty(id)) return 0;
        string cleanID = id.Trim().ToLower();
        return itemCounts.ContainsKey(cleanID) ? itemCounts[cleanID] : 0;
    }

    private void SetItemCount(string id, int value)
    {
        string cleanID = id.Trim().ToLower();
        itemCounts[cleanID] = Mathf.Max(0, value);
    }

    public void DropItem1()
    {
        if (GetItemCount("bone") > 0)
        {
            SetItemCount("bone", GetItemCount("bone") - 1);
            SpawnItemInWorld(item1WorldPrefab);
            UpdateUI();
        }
    }

    public void DropItem2()
    {
        if (GetItemCount("skull") > 0)
        {
            SetItemCount("skull", GetItemCount("skull") - 1);
            SpawnItemInWorld(item2WorldPrefab);
            UpdateUI();
        }
    }

    public void UpdateUI()
    {
        int bCount = GetItemCount("bone");
        if (item1IconUI != null)
        {
            item1IconUI.SetActive(bCount > 0);
            if (item1CountText != null)
            {
                item1CountText.text = "x" + bCount;
                item1CountText.gameObject.SetActive(bCount > 0);
            }
        }

        int sCount = GetItemCount("skull");
        if (item2IconUI != null)
        {
            item2IconUI.SetActive(sCount > 0);
            if (item2CountText != null)
            {
                item2CountText.text = "x" + sCount;
                item2CountText.gameObject.SetActive(sCount > 0);
            }
        }
    }

    private void SpawnItemInWorld(GameObject prefabToSpawn)
    {
        if (prefabToSpawn != null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            Vector3 spawnPosition = player != null ? player.transform.position + new Vector3(1.5f, 2f, 0f) : Vector3.zero;
            GameObject spawnedItem = Instantiate(prefabToSpawn, spawnPosition, Quaternion.identity);

            WorldItem worldItem = spawnedItem.GetComponent<WorldItem>();
            if (worldItem != null) worldItem.droppedByPlayer = true;
        }
    }

    public bool HasIngredients(int req1, int req2)
    {
        return GetItemCount("bone") >= req1 && GetItemCount("skull") >= req2;
    }

    public void ConsumeIngredients(int req1, int req2)
    {
        SetItemCount("bone", GetItemCount("bone") - req1);
        SetItemCount("skull", GetItemCount("skull") - req2);
        UpdateUI();
    }
}