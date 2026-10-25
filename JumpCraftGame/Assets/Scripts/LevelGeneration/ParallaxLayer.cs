using UnityEngine;

// Moves a background sprite by a fraction of the camera's movement so it looks far away, and
// repeats it left and right so the backdrop never runs out on an infinite level.
// Runs after CameraFollow so it always uses this frame's camera position.
[DefaultExecutionOrder(100)]
[RequireComponent(typeof(SpriteRenderer))]
public class ParallaxLayer : MonoBehaviour
{
    public Camera targetCamera;

    //How much the layer follows the camera on each axis: 0 = fixed in the world like the platforms, 1 = stuck to the camera (infinitely far away).
    public Vector2 parallax = new Vector2(0.5f, 0.5f);

    //Tile the sprite sideways forever. Turn off for single objects like the moon.
    public bool repeat = true;

    private Vector3 startPosition;
    private Vector3 cameraStart;
    private float tileWidth;

    void Start()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        startPosition = transform.position;
        cameraStart = targetCamera.transform.position;

        SpriteRenderer sprite = GetComponent<SpriteRenderer>();
        tileWidth = sprite.bounds.size.x;

        //One copy on each side. Each frame the layer jumps by whole tiles to stay centered on the camera, so three copies always cover the screen.
        if (repeat)
        {
            AddTile(sprite, -tileWidth);
            AddTile(sprite, tileWidth);
        }
    }

    void LateUpdate()
    {
        Vector3 cameraPosition = targetCamera.transform.position;
        Vector3 cameraMoved = cameraPosition - cameraStart;

        float x = startPosition.x + cameraMoved.x * parallax.x;
        float y = startPosition.y + cameraMoved.y * parallax.y;

        if (repeat)
            x += Mathf.Round((cameraPosition.x - x) / tileWidth) * tileWidth;

        transform.position = new Vector3(x, y, startPosition.z);
    }

    void AddTile(SpriteRenderer original, float offsetX)
    {
        GameObject tile = new GameObject(name + " Tile");
        tile.transform.SetParent(transform, false);
        tile.transform.localPosition = new Vector3(offsetX / transform.lossyScale.x, 0f, 0f);

        SpriteRenderer copy = tile.AddComponent<SpriteRenderer>();
        copy.sprite = original.sprite;
        copy.color = original.color;
        copy.sortingLayerID = original.sortingLayerID;
        copy.sortingOrder = original.sortingOrder;
    }
}
