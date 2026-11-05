using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class TooltipManager : MonoBehaviour
{
    public static TooltipManager Instance;

    [Header("UI References")]
    public GameObject tooltipObject;
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI detailsText;

    [Header("Mouse Follow Settings")]
    [Tooltip("Offset from the mouse cursor so it never overlaps the pointer.")]
    public Vector2 mouseOffset = new Vector2(20f, -20f);

    private RectTransform canvasRectTransform;
    private RectTransform tooltipRectTransform;
    private Canvas parentCanvas;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        HideTooltip();

        parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas != null)
        {
            canvasRectTransform = parentCanvas.GetComponent<RectTransform>();
        }

        if (tooltipObject != null)
        {
            tooltipRectTransform = tooltipObject.GetComponent<RectTransform>();

            // Force the tooltip's pivot to Top-Left (0, 1) so it always expands down and right from the cursor
            tooltipRectTransform.pivot = new Vector2(0f, 1f);
        }
    }

    private void Update()
    {
        if (tooltipObject != null && tooltipObject.activeSelf)
        {
            if (Mouse.current != null)
            {
                Vector2 mouseScreenPosition = Mouse.current.position.ReadValue();

                Vector2 localPoint;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRectTransform,
                    mouseScreenPosition,
                    parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : parentCanvas.worldCamera,
                    out localPoint
                );

                // Apply position with offset
                tooltipRectTransform.localPosition = localPoint + mouseOffset;
            }
        }
    }

    public void ShowTooltip(string title, string details)
    {
        if (titleText != null) titleText.text = title;
        if (detailsText != null) detailsText.text = details;

        if (tooltipObject != null) tooltipObject.SetActive(true);
    }

    public void HideTooltip()
    {
        if (tooltipObject != null) tooltipObject.SetActive(false);
    }
}