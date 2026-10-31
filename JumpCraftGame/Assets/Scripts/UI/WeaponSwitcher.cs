using UnityEngine;
using UnityEngine.UI;

public class SpriteSwitcher : MonoBehaviour
{
    public static SpriteSwitcher Instance; // Allows HUDManager to find this easily

    [Header("Sprite Options")]
    [Tooltip("The default / placeholder image before or when turned off")]
    public Sprite defaultSprite;

    [Tooltip("The crafted / active option sprite")]
    public Sprite craftedSprite;

    [Header("State Tracking")]
    [SerializeField] private bool _isUnlocked = false;    // False until picked up
    [SerializeField] private bool _isToggledOn = false;    // True when showing the crafted state

    // Public readable properties for other code and game mechanics
    public bool SkullStaffUnlocked => _isUnlocked;
    public bool IsToggledOn => _isToggledOn;
    public bool IsDefaultImage => !_isToggledOn;

    private Image uiImage;

    private void Awake()
    {
        // Set up Singleton instance
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        uiImage = GetComponent<Image>();

        if (uiImage != null && defaultSprite != null)
        {
            uiImage.sprite = defaultSprite;
        }
    }

    // Called automatically when the SkullWand is picked up
    public void UnlockAndActivate()
    {
        if (_isUnlocked) return; // Prevent running twice

        _isUnlocked = true;
        _isToggledOn = true; // Automatically turn it on upon picking up the wand

        if (uiImage != null && craftedSprite != null)
        {
            uiImage.sprite = craftedSprite;
            Debug.Log("SkullWand weapon sprite unlocked and activated!");
        }
    }

    // Attach this method to the UI Button's OnClick event for toggling it afterward
    public void OnClickButton()
    {
        if (!_isUnlocked)
        {
            return;
        }

        _isToggledOn = !_isToggledOn;

        if (uiImage != null)
        {
            uiImage.sprite = _isToggledOn ? craftedSprite : defaultSprite;
        }
    }
}