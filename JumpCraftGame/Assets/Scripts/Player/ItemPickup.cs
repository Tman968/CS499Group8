using UnityEngine;

public class ItemPickup : MonoBehaviour
{
    [Header("UI Inventory Settings")]
    public string itemID = "SkeletonItem";
    public int quantity = 1;

    private void OnCollisionEnter2D(Collision2D collision)
    {
        int playerID = 0;

        // Check which player touched the item
        if (collision.gameObject.CompareTag("Player1") || collision.gameObject.CompareTag("Player"))
        {
            playerID = 1;
        }
        else if (collision.gameObject.CompareTag("Player2"))
        {
            playerID = 2;
        }

        // If a valid player picked it up, add it to their specific inventory
        if (playerID > 0)
        {
            HUDManager hud = FindFirstObjectByType<HUDManager>();
            if (hud != null)
            {
                hud.CollectItem(playerID, itemID, quantity);
            }
            Destroy(gameObject);
        }
    }
}