using UnityEngine;
using System.Collections.Generic;

public class WorldCraftingManager : MonoBehaviour
{
    public static WorldCraftingManager Instance;

    [Header("Master Recipe Database")]
    public List<CraftingRecipe> recipes = new List<CraftingRecipe>();

    [Header("Crafting Scan Settings")]
    [Tooltip("Radius around the dropped item to look for other matching ingredients")]
    public float checkRadius = 1.5f;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void TryCraft(WorldItem triggeringItem)
    {
        if (triggeringItem == null || !triggeringItem.droppedByPlayer) return;

        // Gather all world items within a radius of the triggering item
        Collider2D[] hits = Physics2D.OverlapCircleAll(triggeringItem.transform.position, checkRadius);
        List<WorldItem> nearbyItems = new List<WorldItem>();

        foreach (var hit in hits)
        {
            if (hit != null)
            {
                WorldItem wi = hit.GetComponent<WorldItem>();
                if (wi != null && wi.droppedByPlayer)
                {
                    nearbyItems.Add(wi);
                }
            }
        }

        // Check against all master recipes
        foreach (CraftingRecipe recipe in recipes)
        {
            if (recipe.Matches(nearbyItems))
            {
                // Calculate spawn midpoint of the items used
                Vector3 spawnPosition = Vector3.zero;
                foreach (var item in nearbyItems)
                {
                    if (item != null) spawnPosition += item.transform.position;
                }
                spawnPosition /= nearbyItems.Count;

                // Spawn result
                if (recipe.resultPrefab != null)
                {
                    GameObject craftedObj = Instantiate(recipe.resultPrefab, spawnPosition, Quaternion.identity);
                    WorldItem craftedWorldItem = craftedObj.GetComponent<WorldItem>();
                    if (craftedWorldItem != null)
                    {
                        craftedWorldItem.droppedByPlayer = true;
                    }

                    Debug.Log("Crafting successful! Spawned: " + recipe.resultPrefab.name);
                }

                // Consume/destroy the exact number of ingredients required by the recipe
                ConsumeRecipeIngredients(recipe, nearbyItems);
                return;
            }
        }
    }

    private void ConsumeRecipeIngredients(CraftingRecipe recipe, List<WorldItem> availableItems)
    {
        string req1 = recipe.ingredient1ID.Trim().ToLower();
        int needed1 = recipe.requiredAmount1;

        string req2 = string.IsNullOrEmpty(recipe.ingredient2ID) ? "" : recipe.ingredient2ID.Trim().ToLower();
        int needed2 = recipe.requiredAmount2;

        foreach (var item in availableItems)
        {
            if (item == null) continue;
            string id = item.itemID.Trim().ToLower();

            if (id == req1 && needed1 > 0)
            {
                needed1--;
                Destroy(item.gameObject);
            }
            else if (!string.IsNullOrEmpty(req2) && id == req2 && needed2 > 0)
            {
                needed2--;
                Destroy(item.gameObject);
            }

            if (needed1 <= 0 && needed2 <= 0) break;
        }
    }
}