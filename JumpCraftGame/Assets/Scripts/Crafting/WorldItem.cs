using UnityEngine;

[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class WorldItem : MonoBehaviour
{
    [Header("Item Identity")]
    public string itemID;

    [Header("Origin Tracking")]
    public bool droppedByPlayer = false;

    private void Start()
    {
        // Give the item a tiny moment to settle on the ground, then check for recipes
        if (droppedByPlayer)
        {
            Invoke("CheckForCrafting", 0.2f);
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // If another item bumps into this one, check recipes immediately
        if (droppedByPlayer && WorldCraftingManager.Instance != null)
        {
            WorldCraftingManager.Instance.TryCraft(this);
        }
    }

    public void CheckForCrafting()
    {
        if (WorldCraftingManager.Instance != null)
        {
            WorldCraftingManager.Instance.TryCraft(this);
        }
    }
}