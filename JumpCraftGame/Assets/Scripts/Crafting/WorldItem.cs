using UnityEngine;

[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class WorldItem : MonoBehaviour
{
    [Header("Item Identity")]
    public string itemID;

    [Header("Origin Tracking")]
    public bool droppedByPlayer = false;

    // Set when a recipe uses this item up, so it can't be crafted with again before it's destroyed
    [HideInInspector] public bool consumed = false;

    private Rigidbody2D body;

    private void Start()
    {
        body = GetComponent<Rigidbody2D>();
        // Give the item a tiny moment to settle on the ground, then check for recipes
        if (droppedByPlayer)
        {
            Invoke("CheckForCrafting", 0.2f);
        }
    }

    // Loose items are carried by wind, vortexes and updrafts.
    private void FixedUpdate()
    {
        if (body == null || body.bodyType != RigidbodyType2D.Dynamic) return;
        var air = AirField.At(body.position);
        if (air.sqrMagnitude > 0.01f) body.AddForce(air * (body.mass * 0.8f));
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