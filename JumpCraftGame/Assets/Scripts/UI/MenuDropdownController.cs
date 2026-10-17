using UnityEngine;
using UnityEngine.UI;

public class MenuDropdownController : MonoBehaviour
{
    [Header("Panels")]
    public RectTransform multiplayerPanel;
    public RectTransform preferencesPanel;
    public RectTransform playerStatsPanel;

    [Header("Back Buttons")]
    public Button multiplayerBackButton;
    public Button preferencesBackButton;
    public Button playerStatsBackButton;

    [Header("Slide Settings")]
    public float slideSpeed = 12f;

    private Vector2 hiddenOffset = new Vector2(0, 300f);
    private Vector2 multiplayerShownPos, preferencesShownPos, playerStatsShownPos;
    private Vector2 multiplayerTargetPos, preferencesTargetPos, playerStatsTargetPos;

    private bool isMultiplayerOpen = false;
    private bool isPreferencesOpen = false;
    private bool isPlayerStatsOpen = false;

    void Start()
    {
        InitializePanel(multiplayerPanel, out multiplayerShownPos, out multiplayerTargetPos);
        InitializePanel(preferencesPanel, out preferencesShownPos, out preferencesTargetPos);
        InitializePanel(playerStatsPanel, out playerStatsShownPos, out playerStatsTargetPos);

        // Automatically hook up back button clicks via script
        if (multiplayerBackButton != null) multiplayerBackButton.onClick.AddListener(CloseAllMenus);
        if (preferencesBackButton != null) preferencesBackButton.onClick.AddListener(CloseAllMenus);
        if (playerStatsBackButton != null) playerStatsBackButton.onClick.AddListener(CloseAllMenus);
    }

    void InitializePanel(RectTransform panel, out Vector2 shownPos, out Vector2 targetPos)
    {
        if (panel != null)
        {
            shownPos = panel.anchoredPosition;
            targetPos = shownPos + hiddenOffset;
            panel.anchoredPosition = targetPos;
            panel.gameObject.SetActive(false);
        }
        else
        {
            shownPos = Vector2.zero;
            targetPos = Vector2.zero;
        }
    }

    void Update()
    {
        SmoothSlide(multiplayerPanel, multiplayerShownPos, multiplayerTargetPos, isMultiplayerOpen);
        SmoothSlide(preferencesPanel, preferencesShownPos, preferencesTargetPos, isPreferencesOpen);
        SmoothSlide(playerStatsPanel, playerStatsShownPos, playerStatsTargetPos, isPlayerStatsOpen);
    }

    void SmoothSlide(RectTransform panel, Vector2 shownPos, Vector2 targetPos, bool isOpen)
    {
        if (panel == null) return;

        if (panel.gameObject.activeSelf && panel.anchoredPosition != targetPos)
        {
            panel.anchoredPosition = Vector2.Lerp(
                panel.anchoredPosition,
                targetPos,
                slideSpeed * Time.unscaledDeltaTime
            );

            if (!isOpen && Vector2.Distance(panel.anchoredPosition, shownPos + hiddenOffset) < 1f)
            {
                panel.gameObject.SetActive(false);
            }
        }
    }

    public void ToggleMultiplayer()
    {
        isMultiplayerOpen = !isMultiplayerOpen;
        isPreferencesOpen = false;
        isPlayerStatsOpen = false;
        UpdateTargets();
    }

    public void TogglePreferences()
    {
        isPreferencesOpen = !isPreferencesOpen;
        isMultiplayerOpen = false;
        isPlayerStatsOpen = false;
        UpdateTargets();
    }

    public void TogglePlayerStats()
    {
        isPlayerStatsOpen = !isPlayerStatsOpen;
        isMultiplayerOpen = false;
        isPreferencesOpen = false;
        UpdateTargets();
    }

    // Closes all dropdown menus and slides them back up
    public void CloseAllMenus()
    {
        isMultiplayerOpen = false;
        isPreferencesOpen = false;
        isPlayerStatsOpen = false;
        UpdateTargets();
    }

    void UpdateTargets()
    {
        if (multiplayerPanel != null)
        {
            if (isMultiplayerOpen) multiplayerPanel.gameObject.SetActive(true);
            multiplayerTargetPos = isMultiplayerOpen ? multiplayerShownPos : multiplayerShownPos + hiddenOffset;
        }
        if (preferencesPanel != null)
        {
            if (isPreferencesOpen) preferencesPanel.gameObject.SetActive(true);
            preferencesTargetPos = isPreferencesOpen ? preferencesShownPos : preferencesShownPos + hiddenOffset;
        }
        if (playerStatsPanel != null)
        {
            if (isPlayerStatsOpen) playerStatsPanel.gameObject.SetActive(true);
            playerStatsTargetPos = isPlayerStatsOpen ? playerStatsShownPos : playerStatsShownPos + hiddenOffset;
        }
    }
}