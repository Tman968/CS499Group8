using UnityEngine;

public class GameInitializer : MonoBehaviour
{
    [Header("Player Objects")]
    public GameObject player2Object; // Drag Player 2 GameObject here

    [Header("UI Groups")]
    public GameObject player2InventoryUI; // Drag Player 2's inventory panel/icons here
    public GameObject player2HealthBarUI; // Drag Player 2's health bar here

    private void Awake()
    {
        bool isMultiplayer = GameModeManager.IsMultiplayer;

        // Configure Player 2 object
        if (player2Object != null)
        {
            player2Object.SetActive(isMultiplayer);
        }

        // Configure Player 2's UI inventory slots
        if (player2InventoryUI != null)
        {
            player2InventoryUI.SetActive(isMultiplayer);
        }

        // Configure Player 2's health bar
        if (player2HealthBarUI != null)
        {
            player2HealthBarUI.SetActive(isMultiplayer);
        }
    }
}