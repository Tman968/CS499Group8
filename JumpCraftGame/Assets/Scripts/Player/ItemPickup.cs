using UnityEngine;

public class ItemPickup : MonoBehaviour
{
    [Header("UI Inventory Settings")]
    public string itemID = "SkeletonItem";
    public int quantity = 1;

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            HUDManager hud = FindFirstObjectByType<HUDManager>();
            if (hud != null)
            {
                hud.CollectItem(itemID, quantity);
            }
            Destroy(gameObject);
        }
    }
}