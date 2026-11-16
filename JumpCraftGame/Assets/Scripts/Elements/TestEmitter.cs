using UnityEngine;

/// <summary>
/// A test-bench rift: a floating sigil that pours an endless stream of one element straight up or
/// straight down, so wand-against-wand reactions can be tried alone. It pours exactly like a
/// wizard's wand would, from a separate source.
/// </summary>
public class TestEmitter : MonoBehaviour
{
    public Element Element { get; private set; }
    public Vector2 Direction { get; private set; }

    WandStream stream;
    float age;

    public static TestEmitter Create(Element element, Vector2 origin, Vector2 direction)
    {
        var go = new GameObject($"TestEmitter_{element}");
        go.transform.position = origin;
        var emitter = go.AddComponent<TestEmitter>();
        emitter.Element = element;
        emitter.Direction = direction.normalized;
        emitter.stream = new WandStream(element, null, go.transform);
        ElementLights.Burst(origin, Elements.Glow(element), 3f, 2f, 0.3f);
        return emitter;
    }

    void Update()
    {
        var dt = Time.deltaTime;
        age += dt;
        var origin = (Vector2)transform.position;
        // Open over a moment before pouring.
        var open = Mathf.Clamp01(age / 0.35f);
        if (open >= 1f) stream.Pour(origin + Direction * 0.15f, Direction, dt);
        else stream.Rest(dt);
        DrawSigil(origin, open);
    }

    void OnDestroy() => stream?.Destroy();

    // A rift in the air: a turning ring of runes in the element's color around a dark eye.
    void DrawSigil(Vector2 c, float open)
    {
        var color = Elements.Glow(Element);
        var time = Time.time;
        var r = 0.4f * open;
        const int Points = 20;
        for (var i = 0; i < Points; i++)
        {
            var a = time * 1.6f + i * Mathf.PI * 2f / Points;
            var p = c + new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r * 0.45f);
            var rune = i % 5 == 0;
            ArcaneParticles.DrawPixel(FxLayer.Glow, p, rune ? 2 : 1, (rune ? Color.white : color).WithAlpha(0.9f * open));
        }
        for (var i = 0; i < 6; i++)
        {
            var a = -time * 3f + i * Mathf.PI / 3f;
            ArcaneParticles.DrawPixel(FxLayer.Glow, c + new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.45f) * (r * 0.55f), 1, color.WithAlpha(0.6f * open));
        }
        ArcaneParticles.DrawPixel(FxLayer.Back, c, 3, Elements.Soot.WithAlpha(open));
        ArcaneParticles.DrawHalo(FxLayer.Glow, c, 22f * open, color.WithAlpha(0.25f));
    }
}
