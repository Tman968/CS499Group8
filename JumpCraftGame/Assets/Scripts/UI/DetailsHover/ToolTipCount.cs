using UnityEngine;
using UnityEngine.EventSystems;

public class TooltipSlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Item Configuration")]
    [Tooltip("The unique ID of this item (e.g., 'bone', 'skull', 'skullwand')")]
    public string itemID;

    [Tooltip("Display name of the item shown in the title")]
    public string itemName;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (string.IsNullOrEmpty(itemID)) return;

        // Fetch current count dynamically from HUDManager
        int currentCount = 0;
        if (HUDManager.Instance != null)
        {
            currentCount = HUDManager.Instance.GetItemCount(itemID);
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