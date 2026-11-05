using UnityEngine;
using UnityEngine.UI;

public class ItemPickupDisplay : MonoBehaviour
{
    public static ItemPickupDisplay Instance;

    [Header("UI Image Reference")]
    [Tooltip("Drag the UI Image GameObject here that should appear upon pickup.")]
    public GameObject pickupImageObject;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // Ensure the image starts hidden when the game begins
        if (pickupImageObject != null)
        {
            pickupImageObject.SetActive(false);
        }
    }

    // Call this method when the item is picked up
    public void ShowImage()
    {
        if (pickupImageObject != null)
        {
            pickupImageObject.SetActive(true);
            Debug.Log("Pickup image displayed on screen!");
        }
        else
        {
            Debug.LogWarning("Pickup Image Object is not assigned in the Inspector!");
        }
    }
}