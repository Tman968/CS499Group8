using UnityEngine;

public class GameInitializer : MonoBehaviour
{
    [Header("Player 2 Objects & UI (Multiplayer)")]
    public GameObject player2Object; // Drag Player 2 GameObject here
    public GameObject player2InventoryUI; // Drag Player 2's inventory panel/icons here
    public GameObject player2HealthBarUI; // Drag Player 2's health bar here
    public GameObject player2CrosshairUI; // Drag Player 2's crosshair here

    [Header("Single-Player Exclusive Items (Solo Mode)")]
    [Tooltip("Drag any objects or UI elements here that should ONLY appear in single-player and disappear in multiplayer.")]
    public GameObject[] singlePlayerObjects;

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

        // Configure Player 2's crosshair
        if (player2CrosshairUI != null)
        {
            player2CrosshairUI.SetActive(isMultiplayer);
        }

        // Configure Single-Player Exclusive Items (Active ONLY when NOT in multiplayer)
        if (singlePlayerObjects != null)
        {
            foreach (var obj in singlePlayerObjects)
            {
                if (obj != null)
                {
                    obj.SetActive(!isMultiplayer);
                }
            }
        }
    }
}