using UnityEngine;
using TMPro;
using System.Collections;

public class ItemNotificationPopup : MonoBehaviour
{
    public static ItemNotificationPopup Instance;

    [Header("UI References")]
    [Tooltip("The RectTransform of the notification panel that will slide in.")]
    public RectTransform notificationPanel;
    [Tooltip("Text component inside the sliding panel.")]
    public TextMeshProUGUI messageText;

    [Header("Slide Settings")]
    public float slideSpeed = 12f;
    [Tooltip("How many seconds the popup stays on screen before sliding back out.")]
    public float displayDuration = 2.5f;

    private Vector2 hiddenPosition;
    private Vector2 shownPosition;
    private Vector2 targetPosition;
    private Coroutine activeRoutine;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        if (notificationPanel != null)
        {
            shownPosition = notificationPanel.anchoredPosition;
            hiddenPosition = shownPosition - new Vector2(notificationPanel.rect.width + 100f, 0f);

            notificationPanel.anchoredPosition = hiddenPosition;
            targetPosition = hiddenPosition;
        }
    }

    private void Update()
    {
        if (notificationPanel != null && notificationPanel.anchoredPosition != targetPosition)
        {
            notificationPanel.anchoredPosition = Vector2.Lerp(
                notificationPanel.anchoredPosition,
                targetPosition,
                slideSpeed * Time.unscaledDeltaTime
            );
        }
    }

    // Pass any custom text message you want into this function!
    public void TriggerPopup(string customMessage)
    {
        if (notificationPanel == null) return;

        if (messageText != null)
        {
            messageText.text = customMessage;
        }

        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
        }

        activeRoutine = StartCoroutine(ShowPopupRoutine());
    }

    private IEnumerator ShowPopupRoutine()
    {
        targetPosition = shownPosition;
        yield return new WaitForSecondsRealtime(displayDuration);
        targetPosition = hiddenPosition;
    }
}