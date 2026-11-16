using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Something that burns: a rotten crate (loose; wind shoves it, you can stand on it) or a bramble
/// thicket (rooted; a thorny wall that hurts to touch). Enough fire, or a lightning strike, sets it
/// alight; it burns for a while, spreading to anything flammable beside it and scorching whoever
/// stands too close, then crumbles to ash and embers. Ice or a puddle douses it; wind fans it.
/// </summary>
public class FlammableProp : MonoBehaviour, IElementReactive
{
    public enum Kind { Crate, Bramble }

    public Kind kind;
    [Tooltip("Seconds it burns before it is gone.")]
    public float burnSeconds = 6f;
    [Tooltip("Player damage (out of 100) from the bramble's thorns.")]
    public float thornDamage = 6f;

    public bool IsBurning => burning;
    public bool BlocksMotes => true;

    SpriteRenderer spriteRenderer;
    Collider2D body;
    Rigidbody2D rb;
    Light2D glow;
    float heat;
    bool burning;
    float burnLeft;
    float spreadTimer;

    /// <summary>Builds a crate or a bramble with its art, standing on surfaceY at x.</summary>
    public static FlammableProp Create(Kind kind, Texture2D art, float x, float surfaceY, Transform parent)
    {
        var go = new GameObject(kind.ToString());
        go.transform.SetParent(parent, true);
        var sprite = Sprite.Create(art, new Rect(0, 0, art.width, art.height), new Vector2(0.5f, 0f), 16f, 0, SpriteMeshType.FullRect);
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = 3;
        go.transform.position = new Vector3(x, surfaceY, 0f);

        var box = go.AddComponent<BoxCollider2D>();
        if (kind == Kind.Crate)
        {
            box.size = new Vector2(art.width / 16f - 0.06f, art.height / 16f - 0.06f);
            box.offset = new Vector2(0f, art.height / 32f);
            var rb = go.AddComponent<Rigidbody2D>();
            rb.mass = 1.5f;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            box.sharedMaterial = new PhysicsMaterial2D("Crate") { friction = 0.6f, bounciness = 0f };
        }
        else
        {
            // The thicket's solid core: lower and narrower than its straggling art.
            box.size = new Vector2(art.width / 16f * 0.8f, art.height / 16f * 0.75f);
            box.offset = new Vector2(0f, box.size.y * 0.5f);
        }

        var prop = go.AddComponent<FlammableProp>();
        prop.kind = kind;
        prop.burnSeconds = kind == Kind.Crate ? 7f : 5f;
        return prop;
    }

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        body = GetComponent<Collider2D>();
        rb = GetComponent<Rigidbody2D>();
    }

    public void OnElement(Element element, float power, Vector2 point, Vector2 direction)
    {
        switch (element)
        {
            case Element.Fire:
                heat += power * 0.08f;
                if (burning) burnLeft -= power * 0.02f;
                break;
            case Element.Lightning:
                heat += power * 0.35f;
                break;
            case Element.Ice:
                heat = Mathf.Max(0f, heat - power * 0.3f);
                if (burning && Random.value < 0.15f * power) Douse();
                break;
            case Element.Wind:
                if (burning) burnLeft -= power * 0.05f;
                break;
        }
        if (!burning && heat >= 1f) Ignite();
    }

    public void Ignite()
    {
        if (burning) return;
        burning = true;
        burnLeft = burnSeconds;
        glow = ElementLights.MakeLight(transform);
        glow.color = Elements.Glow(Element.Fire);
        glow.enabled = true;
        ElementLights.Burst(body.bounds.center, Elements.Glow(Element.Fire), 3f, 1.6f, 0.3f);
    }

    void Douse()
    {
        burning = false;
        heat = 0f;
        if (glow != null) Destroy(glow.gameObject);
        ElementFx.SteamPuff(body.bounds.center, 6, 1f);
    }

    void FixedUpdate()
    {
        if (rb == null) return;
        // A crate is loose: gusts shove it, vortexes and updrafts lift it.
        var air = AirField.At(rb.worldCenterOfMass);
        if (air.sqrMagnitude > 0.01f) rb.AddForce(air * (rb.mass * 0.6f));
    }

    void Update()
    {
        if (transform.position.y < -30f)
        {
            Destroy(gameObject);
            return;
        }
        heat = Mathf.Max(0f, heat - 0.1f * Time.deltaTime);
        if (!burning) return;

        var dt = Time.deltaTime;
        var b = body.bounds;
        var air = AirField.At(b.center);
        // Wind fans the fire: it burns hotter and faster.
        var fanned = 1f + Mathf.Clamp01(air.magnitude / 20f);
        burnLeft -= dt * fanned;

        var t = 1f - burnLeft / burnSeconds;
        spriteRenderer.color = Color.Lerp(new Color(1f, 0.8f, 0.7f), new Color(0.2f, 0.12f, 0.1f), t);

        // Flames all over it, more as it gets going.
        var count = Mathf.RoundToInt(Mathf.Lerp(2f, 5f, Mathf.Min(1f, t * 2f)) * fanned);
        for (var i = 0; i < count; i++)
        {
            var p = new Vector2(Random.Range(b.min.x, b.max.x), Random.Range(b.min.y, b.max.y));
            ArcaneParticles.Emit(FxLayer.Glow, new Spark
            {
                pos = p, vel = new Vector2(Random.Range(-0.3f, 0.3f), Random.Range(1f, 2.6f)), life = Random.Range(0.3f, 0.55f),
                size = Random.value < 0.5f ? 2 : 1, sizeEnd = 1,
                colorStart = Elements.FireYellow, colorMid = Elements.FireOrange, colorEnd = Elements.FireRed.WithAlpha(0f),
                gravity = -6f, turbulence = 9f, flicker = 0.3f, wind = 0.8f,
            });
        }
        if (Random.value < 0.25f) ElementFx.Smoke(new Vector2(b.center.x, b.max.y), 1.3f);
        if (Random.value < 0.08f) ElementFx.Ember(new Vector2(b.center.x, b.max.y), new Vector2(Random.Range(-1f, 1f), 2f));
        glow.transform.position = b.center;
        glow.pointLightOuterRadius = 3f + b.extents.x;
        glow.intensity = 1.2f + 0.4f * Mathf.Sin(Time.time * 29f) * Mathf.Sin(Time.time * 9f);

        spreadTimer -= dt;
        if (spreadTimer <= 0f)
        {
            spreadTimer = 0.25f;
            // Scorch whoever stands too close, and catch neighbours.
            ReactionArea.Characters(b.center, b.extents.magnitude + 0.4f, (s, k) => s.Ignite(1.5f, s.IsPlayer ? 0.2f : 0.8f));
            foreach (var other in FindObjectsByType<FlammableProp>(FindObjectsSortMode.None))
            {
                if (other == this || other.burning) continue;
                if (Vector2.Distance(other.body.bounds.center, b.center) < b.extents.x + other.body.bounds.extents.x + 0.6f)
                    other.OnElement(Element.Fire, 2.5f * fanned, b.center, Vector2.zero);
            }
            // Standing in a puddle puts it out.
            var under = SurfaceGrid.Probe(new Vector2(b.center.x, b.min.y));
            if (under.found && under.coat == SurfaceCoat.Water)
            {
                Douse();
                return;
            }
        }

        if (burnLeft <= 0f) BurnAway();
    }

    void BurnAway()
    {
        var b = body.bounds;
        for (var i = 0; i < 30; i++)
        {
            var p = new Vector2(Random.Range(b.min.x, b.max.x), Random.Range(b.min.y, b.max.y));
            ArcaneParticles.Emit(FxLayer.Front, new Spark
            {
                pos = p, vel = new Vector2(Random.Range(-1f, 1f), Random.Range(0f, 2f)), life = Random.Range(0.5f, 1f),
                size = 1, sizeEnd = 1, gravity = 6f, drag = 1f, wind = 1f,
                colorStart = Elements.Rgb(60, 52, 56), colorMid = Elements.Soot, colorEnd = Elements.Soot.WithAlpha(0f),
            });
        }
        for (var i = 0; i < 5; i++) ElementFx.Smoke(b.center, 1.5f);
        for (var i = 0; i < 8; i++) ElementFx.Ember(b.center, Random.insideUnitCircle * 3f + Vector2.up * 2f);
        // Leave it smouldering on the ground.
        for (var x = b.min.x; x <= b.max.x; x += SurfaceGrid.Cell)
            if (SurfaceGrid.FindSurface(new Vector2(x, b.min.y + 0.3f), 1f, out var s)) SurfaceGrid.ApplyFire(s, 1f);
        Destroy(gameObject);
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        if (kind != Kind.Bramble) return;
        var health = collision.collider.GetComponentInParent<PlayerHealth>();
        if (health != null) health.TakeDamage(thornDamage);
    }
}
