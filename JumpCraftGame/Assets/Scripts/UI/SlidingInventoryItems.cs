using UnityEngine;

public class SlidingPanel : MonoBehaviour
{
    [Header("Slide Settings")]
    [Tooltip("The position of the panel when it is fully open/visible on screen.")]
    public Vector2 openPosition;
    [Tooltip("The position of the panel when it is closed/hidden off-screen.")]
    public Vector2 closedPosition;
    
    [Tooltip("How fast the panel slides.")]
    public float slideSpeed = 10f;

    private RectTransform rectTransform;
    private bool isOpen = false;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        // Ensure it starts at the closed position
        rectTransform.anchoredPosition = closedPosition;
    }

    private void Update()
    {
        // Target position based on whether the panel is open or closed
        Vector2 targetPos = isOpen ? openPosition : closedPosition;
        
        // Smoothly slide the panel toward the target position using Lerp
        rectTransform.anchoredPosition = Vector2.Lerp(rectTransform.anchoredPosition, targetPos, slideSpeed * Time.deltaTime);
    }

    // Call this method from a button to toggle the panel open/closed
    public void TogglePanel()
    {
        isOpen = !isOpen;
    }

    // Explicitly open the panel
    public void OpenPanel()
    {
        isOpen = true;
    }

    // Explicitly close the panel
    public void ClosePanel()
    {
        isOpen = false;
    }
}