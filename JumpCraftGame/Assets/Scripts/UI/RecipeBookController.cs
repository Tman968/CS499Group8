using UnityEngine;
using UnityEngine.UI;

public class RecipeBookController : MonoBehaviour
{
    [Header("UI References")]
    public RectTransform menuPanel; // Drag your SlidingMenu here
    public Button toggleButton;   // Drag your MenuButton here

    [Header("Slide Settings")]
    public float slideSpeed = 10f; // How fast it slides
    private bool isOpen = false;

    private Vector2 hiddenPosition;
    private Vector2 targetPosition;
    private Vector2 shownPosition;

    void Start()
    {
        if (menuPanel == null) return;

        // Calculate positions based on panel width
        float panelWidth = menuPanel.rect.width;

        // Assuming it slides in from the right. (If from left, switch negative/positive signs)
        shownPosition = menuPanel.anchoredPosition;
        hiddenPosition = shownPosition - new Vector2(panelWidth, 0);

        // Start closed
        menuPanel.anchoredPosition = hiddenPosition;
        targetPosition = hiddenPosition;

        // Hook up button click event automatically
        if (toggleButton != null)
        {
            toggleButton.onClick.AddListener(ToggleMenu);
        }
    }

    void Update()
    {
        // Smoothly slide the panel towards the target position every frame
        if (menuPanel.anchoredPosition != targetPosition)
        {
            menuPanel.anchoredPosition = Vector2.Lerp(
                menuPanel.anchoredPosition,
                targetPosition,
                slideSpeed * Time.unscaledDeltaTime
            );
        }
    }

    public void ToggleMenu()
    {
        isOpen = !isOpen;
        targetPosition = isOpen ? shownPosition : hiddenPosition;
    }
}