using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class HUDManager : MonoBehaviour
{
    public static HUDManager Instance;

    [Header("Player 1 UI (Slots 1 & 3)")]
    public GameObject[] player1BoneIcons;
    public GameObject[] player1SkullIcons;
    public GameObject[] player1KeyIcons; // Player 1 Key icons
    public TextMeshProUGUI player1BoneCountText;
    public TextMeshProUGUI player1SkullCountText;
    public TextMeshProUGUI player1KeyCountText;

    [Header("Player 2 UI (Slot 2)")]
    public GameObject[] player2BoneIcons;
    public GameObject[] player2SkullIcons;
    public GameObject[] player2KeyIcons; // Player 2 Key icons
    public TextMeshProUGUI player2BoneCountText;
    public TextMeshProUGUI player2SkullCountText;
    public TextMeshProUGUI player2KeyCountText;

    public bool recipeUnlocked = false;

    private Dictionary<int, Dictionary<string, int>> playerInventories = new Dictionary<int, Dictionary<string, int>>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
        UpdateUI();
    }

    public void CollectItem(int playerID, string id, int amount)
    {
        if (string.IsNullOrEmpty(id)) return;
        string cleanID = id.Trim().ToLower();

        if (cleanID == "skeletonitem" || cleanID == "skeletonitembone") cleanID = "bone";
        if (cleanID == "raregem" || cleanID == "skeletonitemskull") cleanID = "skull";
        if (cleanID == "key" || cleanID == "skull_key" || cleanID == "skullkey") cleanID = "skullkey";

        int current = GetItemCount(playerID, cleanID);
        SetItemCountDirect(playerID, cleanID, current + amount);

        // Notifications & specific item triggers
        if (cleanID == "bone" && playerID == 1 && recipeUnlocked == false)
        {
            if (ItemNotificationPopup.Instance != null)
                ItemNotificationPopup.Instance.TriggerPopup("Recipe Book Updated!");
            recipeUnlocked = true;
        }
        else if (cleanID == "skullkey")
        {
            if (ItemNotificationPopup.Instance != null)
            {
                ItemNotificationPopup.Instance.TriggerPopup($"Player {playerID} Acquired Exit Key!");
            }
        }
        else if (cleanID == "skullwand" || cleanID == "item3")
        {
            if (ItemNotificationPopup.Instance != null)
                ItemNotificationPopup.Instance.TriggerPopup("Weapon Updated!");
            if (SpriteSwitcher.Instance != null && playerID == 1)
                SpriteSwitcher.Instance.UnlockAndActivate();
        }

        UpdateUI();
    }

    public void CollectItem(string id, int amount) { CollectItem(1, id, amount); }
    public void CollectItem(string id) { CollectItem(1, id, 1); }

    public int GetItemCount(int playerID, string id)
    {
        if (string.IsNullOrEmpty(id)) return 0;
        string cleanID = id.Trim().ToLower();

        if (!playerInventories.ContainsKey(playerID))
            playerInventories[playerID] = new Dictionary<string, int>();

        return playerInventories[playerID].ContainsKey(cleanID) ? playerInventories[playerID][cleanID] : 0;
    }

    public void SetItemCountDirect(int playerID, string id, int value)
    {
        string cleanID = id.Trim().ToLower();
        if (!playerInventories.ContainsKey(playerID))
            playerInventories[playerID] = new Dictionary<string, int>();

        playerInventories[playerID][cleanID] = Mathf.Max(0, value);
    }

    public void UpdateUI()
    {
        // --- Player 1 UI Update ---
        int p1Bones = GetItemCount(1, "bone");
        if (player1BoneIcons != null) foreach (var icon in player1BoneIcons) if (icon != null) icon.SetActive(p1Bones > 0);
        if (player1BoneCountText != null) { player1BoneCountText.text = "x" + p1Bones; player1BoneCountText.gameObject.SetActive(p1Bones > 0); }

        int p1Skulls = GetItemCount(1, "skull");
        if (player1SkullIcons != null) foreach (var icon in player1SkullIcons) if (icon != null) icon.SetActive(p1Skulls > 0);
        if (player1SkullCountText != null) { player1SkullCountText.text = "x" + p1Skulls; player1SkullCountText.gameObject.SetActive(p1Skulls > 0); }

        int p1Keys = GetItemCount(1, "skullkey");
        if (player1KeyIcons != null) foreach (var icon in player1KeyIcons) if (icon != null) icon.SetActive(p1Keys > 0);
        if (player1KeyCountText != null) { player1KeyCountText.text = "x" + p1Keys; player1KeyCountText.gameObject.SetActive(p1Keys > 0); }

        // --- Player 2 UI Update ---
        int p2Bones = GetItemCount(2, "bone");
        if (player2BoneIcons != null) foreach (var icon in player2BoneIcons) if (icon != null) icon.SetActive(p2Bones > 0);
        if (player2BoneCountText != null) { player2BoneCountText.text = "x" + p2Bones; player2BoneCountText.gameObject.SetActive(p2Bones > 0); }

        int p2Skulls = GetItemCount(2, "skull");
        if (player2SkullIcons != null) foreach (var icon in player2SkullIcons) if (icon != null) icon.SetActive(p2Skulls > 0);
        if (player2SkullCountText != null) { player2SkullCountText.text = "x" + p2Skulls; player2SkullCountText.gameObject.SetActive(p2Skulls > 0); }

        int p2Keys = GetItemCount(2, "skullkey");
        if (player2KeyIcons != null) foreach (var icon in player2KeyIcons) if (icon != null) icon.SetActive(p2Keys > 0);
        if (player2KeyCountText != null) { player2KeyCountText.text = "x" + p2Keys; player2KeyCountText.gameObject.SetActive(p2Keys > 0); }
    }
}