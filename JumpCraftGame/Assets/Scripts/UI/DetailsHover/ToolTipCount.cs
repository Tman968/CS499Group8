using UnityEngine;
using UnityEngine.EventSystems;

public class TooltipSlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Player & Item Configuration")]
    [Tooltip("Which player's inventory does this slot represent? (1 or 2)")]
    public int playerID = 1;

    [Tooltip("The unique ID of this item (e.g., 'bone', 'skull', 'skullwand')")]
    public string itemID;

    [Tooltip("Display name of the item shown in the title")]
    public string itemName;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (string.IsNullOrEmpty(itemID)) return;

        // Fetch current count dynamically from HUDManager using the specific playerID
        int currentCount = 0;
        if (HUDManager.Instance != null)
        {
            currentCount = HUDManager.Instance.GetItemCount(playerID, itemID);
        }

        // Format details to show your description + the dynamic inventory count
        string details = $"Amount: {currentCount}";

        // Call your TooltipManager with separate title and details
        if (TooltipManager.Instance != null)
        {
            TooltipManager.Instance.ShowTooltip(itemName, details);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (TooltipManager.Instance != null)
        {
            TooltipManager.Instance.HideTooltip();
        }
    }
}