using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Ancient machinery sunk in a gap between platforms: a carved slab with a copper coil beneath.
/// Dormant, it rests below the platforms, out of reach. Strike it with lightning and its runes
/// light up and it rises (above the next platform, to reach higher ground); as the charge drains
/// it sinks back. Ice locks it where it is for a few seconds.
/// </summary>
public class RuneLift : MonoBehaviour, IElementReactive
{
    // The runes' pixels, from Tools/generate_element_sprites.py (RUNE_ROW, RUNE_COLUMNS, RUNE_GLYPHS).
    const int RuneRow = 3;
    static readonly int[] RuneColumns = { 7, 14, 20, 26, 33 };
    static readonly string[][] RuneGlyphs =
    {
        new[] { "x.x", ".x.", "x.x" },
        new[] { ".x.", "xxx", ".x." },
        new[] { "xx.", ".x.", ".xx" },
        new[] { "x..", "xxx", "..x" },
        new[] { ".xx", "x..", "xx." },
    };

    public float riseSpeed = 2.4f;
    public float sinkSpeed = 1.1f;
    [Tooltip("Charge lost per second; a full charge holds the lift up for this long, reciprocal.")]
    public float drainPerSecond = 0.09f;

    public bool BlocksMotes => true;

    Rigidbody2D body;
    SpriteRenderer spriteRenderer;
    Light2D glow;
    float lowY, highY;
    float charge;
    float iced;
    bool moving;
    Texture2D art;

    public static RuneLift Create(Texture2D art, float centerX, float lowY, float highY, Transform parent)
    {
        var go = new GameObject("RuneLift");
        go.transform.SetParent(parent, true);
        go.transform.position = new Vector3(centerX, lowY, 0f);
        var renderer = go.AddComponent<SpriteRenderer>();
        // Pivot at the slab's top surface, so the position is where the wizard stands.
        renderer.sprite = Sprite.Create(art, new Rect(0, 0, art.width, art.height), new Vector2(0.5f, 1f), 16f, 0, SpriteMeshType.FullRect);
        renderer.sortingOrder = 2;
        var body = go.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        var box = go.AddComponent<BoxCollider2D>();
        box.size = new Vector2(art.width / 16f, 8f / 16f);
        box.offset = new Vector2(0f, -box.size.y * 0.5f);

        var lift = go.AddComponent<RuneLift>();
        lift.art = art;
        lift.lowY = lowY;
        lift.highY = highY;
        lift.body = body;
        lift.spriteRenderer = renderer;
        lift.glow = ElementLights.MakeLight(go.transform);
        lift.glow.color = Elements.Glow(Element.Lightning);
        return lift;
    }

    public void OnElement(Element element, float power, Vector2 point, Vector2 direction)
    {
        if (element == Element.Lightning)
        {
            var was = charge;
            charge = Mathf.Min(1f, charge + 0.1f * power);
            if (was <= 0f)
            {
                ElementLights.Burst(body.position, Elements.Glow(Element.Lightning), 3f, 2f, 0.3f);
                Sfx.Play("lift_charge", body.position);
            }
        }
        else if (element == Element.Ice)
        {
            iced = Mathf.Min(4f, iced + 0.25f * power);
        }
        else if (element == Element.Fire)
        {
            iced = 0f;
        }
    }

    void FixedUpdate()
    {
        var dt = Time.fixedDeltaTime;
        iced = Mathf.Max(0f, iced - dt);
        charge = Mathf.Max(0f, charge - drainPerSecond * dt);
        if (iced > 0f)
        {
            moving = false;
            return;
        }
        var y = body.position.y;
        var target = charge > 0f ? highY : lowY;
        var speed = charge > 0f ? riseSpeed : sinkSpeed;
        var next = Mathf.MoveTowards(y, target, speed * dt);
        body.MovePosition(new Vector2(body.position.x, next));
        // Stone grinding while it moves, and a thud as it settles at either end.
        var wasMoving = moving;
        moving = Mathf.Abs(next - y) > 0.0001f;
        if (wasMoving && !moving) Sfx.Play("lift_stop", body.position);
    }

    void Update()
    {
        var pos = body.position;
        if (moving) Sfx.Loop(this, "lift_loop", pos, charge > 0f ? 1f : 0.7f, charge > 0f ? 1f : 0.85f);
        var w = art.width / 16f;
        var left = pos.x - w * 0.5f;
        var top = pos.y;
        var k = Mathf.Clamp01(charge * 3f);
        var time = Time.time;

        // The runes kindle in turn as charge flows into the slab.
        for (var r = 0; r < RuneColumns.Length; r++)
        {
            var lit = Mathf.Clamp01(k * RuneColumns.Length - r * 0.6f);
            if (lit <= 0f) continue;
            var flicker = 0.75f + 0.25f * Mathf.Sin(time * 13f + r * 2.1f);
            var glyph = RuneGlyphs[r];
            for (var gy = 0; gy < 3; gy++)
            {
                for (var gx = 0; gx < 3; gx++)
                {
                    if (glyph[gy][gx] != 'x') continue;
                    var px = RuneColumns[r] - 1 + gx;
                    var py = RuneRow - 1 + gy;
                    var p = new Vector2(left + (px + 0.5f) / 16f, top - (py + 0.5f) / 16f);
                    ArcaneParticles.DrawPixel(FxLayer.Glow, p, 1, Color.Lerp(Elements.BoltViolet, Elements.BoltCore, lit * 0.6f).WithAlpha(lit * flicker));
                }
            }
            ArcaneParticles.DrawHalo(FxLayer.Glow, new Vector2(left + RuneColumns[r] / 16f, top - RuneRow / 16f), 8f, Elements.BoltViolet.WithAlpha(0.25f * lit));
        }

        var coil = new Vector2(pos.x, top - 13f / 16f);
        if (charge > 0f)
        {
            if (Random.value < 0.25f)
                LightningBolts.Spawn(coil, coil + new Vector2(Random.Range(-0.6f, 0.6f), Random.Range(-0.6f, -0.1f)), 0.06f, 1f, false);
            // Dust and grit shaken loose while it grinds upward.
            if (Mathf.Abs(pos.y - highY) > 0.05f && Random.value < 0.4f)
            {
                ArcaneParticles.Emit(FxLayer.Front, new Spark
                {
                    pos = new Vector2(Random.Range(left, left + w), top - 0.5f), vel = new Vector2(Random.Range(-0.3f, 0.3f), -0.5f),
                    life = 0.8f, size = 1, sizeEnd = 1, gravity = 6f,
                    colorStart = Elements.Rgb(96, 88, 104), colorMid = Elements.Rgb(66, 60, 76), colorEnd = Elements.Soot.WithAlpha(0f),
                });
            }
        }
        if (iced > 0f)
        {
            spriteRenderer.color = new Color(0.7f, 0.9f, 1f);
            if (Random.value < 0.1f) ElementFx.Glint(new Vector2(Random.Range(left, left + w), top - 0.1f), 0.25f);
        }
        else spriteRenderer.color = Color.white;

        glow.enabled = k > 0f;
        glow.transform.position = new Vector3(pos.x, top - 0.3f, 0f);
        glow.pointLightOuterRadius = 3f;
        glow.intensity = k * (1f + 0.3f * Mathf.Sin(time * 21f));
    }
}
