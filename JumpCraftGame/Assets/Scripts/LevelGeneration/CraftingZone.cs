
using UnityEngine;

public class CraftingZone : MonoBehaviour
{
    public bool IsOnCraftingIsland { get; private set; }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("CraftingIsland"))
        {
            IsOnCraftingIsland = true;
            Debug.Log(gameObject.name + " entered a crafting island.");
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("CraftingIsland"))
        {
            IsOnCraftingIsland = false;
            Debug.Log(gameObject.name + " left a crafting island.");
        }
    }
}
