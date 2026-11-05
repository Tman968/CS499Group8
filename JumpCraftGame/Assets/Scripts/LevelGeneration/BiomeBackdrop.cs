using UnityEngine;

// Switches the backdrop between the mountains and the spooky forest as the camera travels.
// Walking toward a forest, the mountain backdrop zooms in (near layers much more than far ones, like
// walking into its trees) until a fog bank hides the swap, then the forest backdrop finishes the
// push in and settles. Walking back out plays the same thing in reverse.
// Runs after CameraFollow and before ParallaxLayer, which applies the zoom.
[DefaultExecutionOrder(90)]
public class BiomeBackdrop : MonoBehaviour
{
    [System.Serializable]
    public struct Layer
    {
        public ParallaxLayer layer;

        //Mountains: how much bigger the layer gets by the forest edge. Forest: how much smaller it starts at the edge.
        public float zoom;
    }

    public Camera targetCamera;
    public Layer[] mountainLayers;
    public Layer[] forestLayers;

    //Covers the backdrop (not the platforms) at the forest edge while the two sets swap.
    public SpriteRenderer fog;

    [Header("Biome Layout (world units)")]
    //Where the first forest starts. After that, forest and mountains alternate forever.
    public float firstForestX = 100f;
    public float forestLength = 150f;
    public float mountainLength = 200f;

    //Distance the zoom and fog play out over, centered on each forest edge.
    public float transitionLength = 30f;

    void LateUpdate()
    {
        Vector3 cameraPosition = targetCamera.transform.position;
        float forest = ForestAmount(cameraPosition.x);
        bool inForest = forest >= 0.5f;

        //0 well away from a forest edge, 1 right at it, on either side.
        float approach = inForest ? (1f - forest) * 2f : forest * 2f;
        float push = approach * approach;

        foreach (Layer m in mountainLayers)
        {
            m.layer.SetVisible(!inForest);
            m.layer.zoom = 1f + m.zoom * push;
        }
        foreach (Layer f in forestLayers)
        {
            f.layer.SetVisible(inForest);
            f.layer.zoom = 1f - f.zoom * push;
        }

        //Fog thickens over the last stretch before the edge and clears over the first stretch after it.
        Color color = fog.color;
        color.a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.7f, 1f, approach));
        fog.color = color;
        fog.enabled = color.a > 0f;

        //Stretch the fog over the whole view. Its sprite is pivoted at the bottom center.
        float height = targetCamera.orthographicSize * 2f + 2f;
        float width = height * targetCamera.aspect + 2f;
        Vector3 spriteSize = fog.sprite.bounds.size;
        fog.transform.position = new Vector3(cameraPosition.x, cameraPosition.y - height / 2f, 0f);
        fog.transform.localScale = new Vector3(width / spriteSize.x, height / spriteSize.y, 1f);
    }

    //0 = mountains, 1 = forest, 0.5 exactly on a forest edge.
    float ForestAmount(float x)
    {
        float period = forestLength + mountainLength;
        float local = Mathf.Repeat(x - firstForestX, period); //0 at a forest's left edge

        //Distance to the nearest forest edge: positive inside the forest, negative outside.
        float inside = local < forestLength
            ? Mathf.Min(local, forestLength - local)
            : -Mathf.Min(local - forestLength, period - local);

        return Mathf.Clamp01(0.5f + inside / transitionLength);
    }
}
