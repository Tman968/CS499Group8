using UnityEngine;

// Blends the backdrop between the mountains and the spooky forest as the camera travels.
// Across each forest edge the two sets cross-fade: walking toward a forest, the mountains drift
// closer (near layers more than far ones, like walking toward their trees) and fade back as the
// forest's sky and trees fade in through a light haze. Halfway across, both biomes are visible at
// once, faint behind the haze. Walking back out plays the same thing in reverse.
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

    //A light haze over the backdrop (not the platforms) through the middle of each transition.
    public SpriteRenderer fog;

    [Header("Biome Layout (world units)")]
    //Where the first forest starts. After that, forest and mountains alternate forever.
    public float firstForestX = 100f;
    public float forestLength = 150f;
    public float mountainLength = 200f;

    //Distance the cross-fade plays out over, centered on each forest edge.
    public float transitionLength = 60f;

    [Header("Blend")]
    //How opaque the haze gets at the very middle of a transition. Keep it well below 1 so both biomes show through.
    [Range(0f, 1f)] public float hazeStrength = 0.4f;
    //Scales every layer's zoom, so the mountains drift closer without leaving the view.
    public float zoomScale = 0.35f;

    void LateUpdate()
    {
        Vector3 cameraPosition = targetCamera.transform.position;
        float forest = ForestAmount(cameraPosition.x);

        //The first layer in each set is its sky.
        for (int i = 0; i < mountainLayers.Length; i++)
        {
            Layer m = mountainLayers[i];
            //The forest's sky sits in front of the mountains and fades in over them, so the
            //mountain ranges only need to fade out once the forest has mostly taken over.
            //The mountain sky stays solid underneath everything.
            float alpha = m.layer.repeat
                ? (i == 0 ? 1f : 1f - Smooth(0.5f, 1f, forest))
                : 1f - Smooth(0.3f, 0.5f, forest); //moon: swaps quickly so two moons never share the sky
            m.layer.SetAlpha(alpha);
            m.layer.zoom = 1f + m.zoom * zoomScale * forest * forest;
        }
        for (int i = 0; i < forestLayers.Length; i++)
        {
            Layer f = forestLayers[i];
            float alpha = f.layer.repeat
                ? (i == 0 ? Smooth(0f, 1f, forest) : Smooth(0.15f, 0.85f, forest))
                : Smooth(0.5f, 0.7f, forest);
            f.layer.SetAlpha(alpha);
            float away = 1f - forest;
            f.layer.zoom = 1f - f.zoom * zoomScale * away * away;
        }

        //The haze peaks in the middle of the transition and clears on either side.
        Color color = fog.color;
        color.a = hazeStrength * Mathf.Sin(Mathf.PI * forest);
        fog.color = color;
        fog.enabled = color.a > 0.001f;

        //Stretch the fog over the whole view. Its sprite is pivoted at the bottom center.
        float height = targetCamera.orthographicSize * 2f + 2f;
        float width = height * targetCamera.aspect + 2f;
        Vector3 spriteSize = fog.sprite.bounds.size;
        fog.transform.position = new Vector3(cameraPosition.x, cameraPosition.y - height / 2f, 0f);
        fog.transform.localScale = new Vector3(width / spriteSize.x, height / spriteSize.y, 1f);
    }

    static float Smooth(float from, float to, float t) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(from, to, t));

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
