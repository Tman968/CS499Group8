using UnityEngine;
using UnityEngine.EventSystems;

public class ItemHoverTemplate : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Item Info Template")]
    public string itemTitle;
    [TextArea(3, 10)]
    public string itemDetails;

   public void OnPointerEnter(PointerEventData eventData)
    {
        Debug.Log("Mouse entered: " + gameObject.name);

        if (TooltipManager.Instance != null)
        {
            TooltipManager.Instance.ShowTooltip(itemTitle, itemDetails);
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