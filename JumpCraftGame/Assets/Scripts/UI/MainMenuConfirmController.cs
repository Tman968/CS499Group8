using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class MainMenuConfirmController : MonoBehaviour
{
    [Header("UI References")]
    public RectTransform confirmPanel; // Drag your ConfirmMenuPanel here
    public Button openButton;          // Drag your MainMenuTriggerButton here
    public Button yesButton;           // Drag your YesButton here
    public Button noButton;            // Drag your NoButton here

    [Header("Slide Settings")]
    public float slideSpeed = 12f;
    private bool isOpen = false;

    private Vector2 hiddenPosition;
    private Vector2 shownPosition;
    private Vector2 targetPosition;

    void Start()
    {
        if (confirmPanel == null) return;

        // Calculate positions for sliding from the top
        float panelHeight = confirmPanel.rect.height;

        shownPosition = confirmPanel.anchoredPosition; // The spot on screen where it's visible
        hiddenPosition = shownPosition + new Vector2(0, panelHeight + 150f); // Pushed up above the screen

        // Start hidden at the top
        confirmPanel.anchoredPosition = hiddenPosition;
        targetPosition = hiddenPosition;

        // Hook up all button clicks automatically
        if (openButton != null) openButton.onClick.AddListener(OpenMenu);
        if (yesButton != null) yesButton.onClick.AddListener(ConfirmYes);
        if (noButton != null) confirmPanel.gameObject.SetActive(false); // Clean start
        if (noButton != null) noButton.onClick.AddListener(ConfirmNo);
    }

    void Update()
    {
        // Smoothly slide the panel towards the target position
        if (confirmPanel.gameObject.activeSelf && confirmPanel.anchoredPosition != targetPosition)
        {
            confirmPanel.anchoredPosition = Vector2.Lerp(
                confirmPanel.anchoredPosition,
                targetPosition,
                slideSpeed * Time.unscaledDeltaTime
            );

            // Turn off game object when fully retracted to save performance
            if (!isOpen && Vector2.Distance(confirmPanel.anchoredPosition, hiddenPosition) < 1f)
            {
                confirmPanel.gameObject.SetActive(false);
            }
        }
    }

    public void OpenMenu()
    {
        confirmPanel.gameObject.SetActive(true);
        isOpen = true;
        targetPosition = shownPosition;
        Time.timeScale = 0f; // Pause gameplay while prompt is open
    }

    public void ConfirmYes()
    {
        Time.timeScale = 1f; // Reset time before switching scenes
        SceneManager.LoadScene("GameMenu"); // Matches your start menu scene name
    }

    public void ConfirmNo()
    {
        isOpen = false;
        targetPosition = hiddenPosition;
        Time.timeScale = 1f; // Resume gameplay
    }
}