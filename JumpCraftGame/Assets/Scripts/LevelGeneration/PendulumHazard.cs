using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// A rusted crescent blade swinging on a chain over a platform: a moving hazard that cuts the
/// wizard and skeletons alike. Ice slows its swing (enough ice freezes it in place for a while),
/// fire thaws it, wind heaves its swing wider, and lightning charges the iron so it arcs into any
/// skeleton nearby.
/// </summary>
public class PendulumHazard : MonoBehaviour, IElementReactive
{
    public float chainLength = 3.4f;
    public float amplitude = 58f;
    public float period = 2.8f;
    [Tooltip("Player damage (out of 100) per hit.")]
    public float playerDamage = 25f;
    [Tooltip("Skeleton damage per hit (a skeleton has 6).")]
    public float skeletonDamage = 2.5f;

    public bool BlocksMotes => true;

    Vector2 anchor;
    Rigidbody2D blade;
    float phase;
    float speed = 1f;
    float frozen;
    float frost;
    float swingBoost;
    float charge;
    float arcTimer;
    Light2D glow;

    public static PendulumHazard Create(Texture2D bladeArt, Vector2 anchor, float chainLength, Transform parent)
    {
        var go = new GameObject("Pendulum");
        go.transform.SetParent(parent, true);
        go.transform.position = anchor;
        var hazard = go.AddComponent<PendulumHazard>();
        hazard.anchor = anchor;
        hazard.chainLength = chainLength;
        hazard.phase = Random.Range(0f, Mathf.PI * 2f);
        hazard.period = Random.Range(2.4f, 3.2f);

        var bladeGo = new GameObject("Blade");
        bladeGo.transform.SetParent(go.transform, false);
        // Pivot at the ring the chain holds.
        var sprite = Sprite.Create(bladeArt, new Rect(0, 0, bladeArt.width, bladeArt.height),
                                   new Vector2(11f / bladeArt.width, 1f - 1.5f / bladeArt.height), 16f, 0, SpriteMeshType.FullRect);
        var renderer = bladeGo.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = 6;
        var body = bladeGo.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        var edge = bladeGo.AddComponent<PolygonCollider2D>();
        // The crescent, roughly: a fan below the ring.
        edge.points = new[]
        {
            new Vector2(-0.62f, -0.45f), new Vector2(-0.4f, -0.85f), new Vector2(0f, -1f),
            new Vector2(0.4f, -0.85f), new Vector2(0.62f, -0.45f), new Vector2(0f, -0.55f),
        };
        bladeGo.AddComponent<PendulumBlade>().hazard = hazard;
        hazard.blade = body;
        hazard.glow = ElementLights.MakeLight(bladeGo.transform);
        hazard.glow.color = Elements.Glow(Element.Lightning);
        return hazard;
    }

    public void OnElement(Element element, float power, Vector2 point, Vector2 direction)
    {
        switch (element)
        {
            case Element.Ice:
                speed = Mathf.Max(0.12f, speed - 0.06f * power);
                frost = Mathf.Min(1f, frost + 0.08f * power);
                if (frost >= 1f && frozen <= 0f)
                {
                    frozen = 3.5f;
                    ElementFx.IceShatter(blade.position, 10, Vector2.zero);
                }
                break;
            case Element.Fire:
                frozen = 0f;
                frost = Mathf.Max(0f, frost - 0.05f * power);
                speed = Mathf.Min(1f, speed + 0.05f * power);
                break;
            case Element.Wind:
                swingBoost = Mathf.Min(1f, swingBoost + 0.03f * power);
                break;
            case Element.Lightning:
                charge = Mathf.Max(charge, 2.5f);
                break;
        }
    }

    void FixedUpdate()
    {
        var dt = Time.fixedDeltaTime;
        frozen -= dt;
        if (frozen <= 0f)
        {
            // Ice wears off and the swing recovers its pace.
            speed = Mathf.MoveTowards(speed, 1f, 0.12f * dt);
            phase += dt * Mathf.PI * 2f / period * speed;
        }
        frost = Mathf.Max(0f, frost - 0.06f * dt);
        swingBoost = Mathf.Max(0f, swingBoost - 0.15f * dt);

        var angle = Mathf.Sin(phase) * (amplitude + swingBoost * 30f);
        var rot = Quaternion.Euler(0f, 0f, angle);
        var pos = anchor + (Vector2)(rot * Vector3.down) * chainLength;
        blade.MovePosition(pos);
        blade.MoveRotation(angle);
    }

    void Update()
    {
        var dt = Time.deltaTime;
        var bladePos = blade.position;

        // The chain, link by link.
        var dir = (bladePos - anchor);
        var links = Mathf.CeilToInt(dir.magnitude / (3f / 16f));
        for (var i = 0; i <= links; i++)
        {
            var p = anchor + dir * (i / (float)links);
            var c = i % 2 == 0 ? Elements.Rgb(122, 116, 130) : Elements.Rgb(78, 72, 86);
            if (charge > 0f && Random.value < 0.3f) c = Elements.BoltViolet;
            ArcaneParticles.DrawPixel(FxLayer.Back, p, i % 2 == 0 ? 2 : 1, c);
        }
        ArcaneParticles.DrawPixel(FxLayer.Back, anchor, 3, Elements.Rgb(132, 124, 140));

        var renderer = blade.GetComponent<SpriteRenderer>();
        renderer.color = Color.Lerp(Color.white, new Color(0.65f, 0.88f, 1f), Mathf.Max(frost, frozen > 0f ? 1f : 0f));
        if (frozen > 0f) ElementFx.IceBlock(renderer.bounds, frozen);
        else if (frost > 0.2f && Random.value < frost * 0.2f)
        {
            var b = renderer.bounds;
            ArcaneParticles.Emit(FxLayer.Front, new Spark
            {
                pos = new Vector2(Random.Range(b.min.x, b.max.x), b.min.y), life = 0.8f, size = 1, sizeEnd = 1, gravity = 3f,
                colorStart = Elements.IceWhite, colorMid = Elements.IcePale, colorEnd = Elements.IcePale.WithAlpha(0f), turbulence = 2f,
            });
        }

        charge -= dt;
        glow.enabled = charge > 0f;
        if (charge > 0f)
        {
            glow.intensity = 0.6f + Random.value * 0.8f;
            glow.pointLightOuterRadius = 2.5f;
            if (Random.value < 0.2f)
                LightningBolts.Spawn(bladePos, bladePos + Random.insideUnitCircle.normalized * Random.Range(0.4f, 0.9f), 0.06f, 1f, false);
            arcTimer -= dt;
            if (arcTimer <= 0f)
            {
                arcTimer = 0.3f;
                var target = ReactionArea.NearestEnemy(bladePos, 3.5f);
                if (target != null)
                {
                    LightningBolts.Spawn(bladePos, target.Center, 0.15f, 1.5f);
                    target.Damage(0.35f * target.Vulnerability(Element.Lightning), Element.Lightning, (target.Center - bladePos).normalized);
                    target.Shock(0.35f);
                }
            }
        }
    }

    /// <summary>Called by the blade when it strikes something.</summary>
    public void Struck(Collider2D other)
    {
        if (frozen > 0f) return;
        var bladeVelocity = blade.linearVelocity;
        var status = other.GetComponentInParent<ElementStatus>();
        if (status != null)
        {
            if (status.IsPlayer)
            {
                var health = status.GetComponent<PlayerHealth>();
                if (health != null) health.TakeDamage(playerDamage * Mathf.Max(0.3f, speed));
            }
            else status.Damage(skeletonDamage * Mathf.Max(0.3f, speed), Element.Wind, bladeVelocity.normalized);
            status.AddPush(new Vector2(Mathf.Sign(bladeVelocity.x) * 7f * speed, 3f));
            ElementFx.BoltImpact(other.ClosestPoint(blade.position), Vector2.up, 0.6f);
        }
    }
}
