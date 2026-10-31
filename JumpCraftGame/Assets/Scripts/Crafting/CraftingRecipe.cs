using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewCraftingRecipe", menuName = "Crafting/Recipe")]
public class CraftingRecipe : ScriptableObject
{
    [Header("Ingredient 1")]
    public string ingredient1ID;
    public int requiredAmount1 = 1;

    [Header("Ingredient 2 (Can Leave Blank)")]
    public string ingredient2ID;
    public int requiredAmount2 = 0;

    [Header("Result")]
    public GameObject resultPrefab; // e.g., SkullWand or SuperSkullStaff

    // Validates if a collection of colliding world items matches this recipe requirements
    public bool Matches(List<WorldItem> droppedItems)
    {
        if (droppedItems == null || droppedItems.Count == 0) return false;

        // Count up how many of each clean ID we have in the collision zone
        Dictionary<string, int> availableCounts = new Dictionary<string, int>();
        foreach (var item in droppedItems)
        {
            if (item == null) continue;
            string id = item.itemID.Trim().ToLower();
            if (availableCounts.ContainsKey(id))
                availableCounts[id]++;
            else
                availableCounts[id] = 1;
        }

        // Check Ingredient 1 requirements
        string req1 = ingredient1ID.Trim().ToLower();
        if (string.IsNullOrEmpty(req1) || !availableCounts.ContainsKey(req1) || availableCounts[req1] < requiredAmount1)
        {
            return false;
        }

        // Check Ingredient 2 requirements (if used)
        string req2 = string.IsNullOrEmpty(ingredient2ID) ? "" : ingredient2ID.Trim().ToLower();
        if (!string.IsNullOrEmpty(req2))
        {
            if (!availableCounts.ContainsKey(req2) || availableCounts[req2] < requiredAmount2)
            {
                return false;
            }
        }
        else
        {
            // If there's no second ingredient, ensure no extra unwanted unique items are crashing the party beyond ingredient 1's required amount
            int totalRequired = requiredAmount1;
            int totalFound = availableCounts[req1];
            if (totalFound > totalRequired) return false;
        }

        return true;
    }
}