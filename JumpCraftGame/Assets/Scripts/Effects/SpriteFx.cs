using UnityEngine;

/// <summary>
/// A short-lived sprite effect: plays its frames, drifts, falls, spins and fades, then destroys
/// itself. Used for orb trails and bursts, and for bone chips knocked off the skeleton.
/// </summary>
public class SpriteFx : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private Sprite[] frames;
    private float fps;
    private float lifetime;
    private float age;
    private Vector2 velocity;
    private float gravity;
    private float spin;
    private float fadeFrom;
    private bool loop;
    private Color color;

    /// <summary>Spawns an effect. Looping effects fade over the second half of their life, others not at all, until FadingFrom says otherwise.</summary>
    public static SpriteFx Spawn(Sprite[] frames, Vector3 position, float fps, float lifetime,
                                 int sortingOrder, Color color, bool loop = false)
    {
        var go = new GameObject("Fx");
        go.transform.position = position;
        var fx = go.AddComponent<SpriteFx>();
        fx.spriteRenderer = go.AddComponent<SpriteRenderer>();
        fx.spriteRenderer.sortingOrder = sortingOrder;
        fx.frames = frames;
        fx.fps = fps;
        fx.lifetime = lifetime;
        fx.loop = loop;
        fx.color = color;
        fx.fadeFrom = loop ? 0.5f : 1f;
        fx.spriteRenderer.color = color;
        fx.spriteRenderer.sprite = frames.Length > 0 ? frames[0] : null;
        return fx;
    }

    public SpriteFx Moving(Vector2 newVelocity, float newGravity = 0f, float newSpin = 0f)
    {
        velocity = newVelocity;
        gravity = newGravity;
        spin = newSpin;
        return this;
    }

    public SpriteFx FadingFrom(float fraction)
    {
        fadeFrom = Mathf.Clamp01(fraction);
        return this;
    }

    void Update()
    {
        var dt = Time.deltaTime;
        age += dt;
        if (age >= lifetime)
        {
            Destroy(gameObject);
            return;
        }

        velocity.y -= gravity * dt;
        transform.position += (Vector3)(velocity * dt);
        if (spin != 0f) transform.Rotate(0f, 0f, spin * dt);

        if (frames.Length > 0)
        {
            var i = (int)(age * fps);
            spriteRenderer.sprite = frames[loop ? i % frames.Length : Mathf.Min(i, frames.Length - 1)];
        }

        var t = age / lifetime;
        var alpha = t <= fadeFrom ? 1f : 1f - (t - fadeFrom) / Mathf.Max(0.0001f, 1f - fadeFrom);
        spriteRenderer.color = new Color(color.r, color.g, color.b, color.a * alpha);
    }
}
