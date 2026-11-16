using UnityEngine;

/// <summary>
/// Fire + Ice: the ice flashes to steam with a bang that throws everything back and scalds the
/// skeletons, then hangs as a thick cloud. Skeletons can't see through it to aim, and anyone in it
/// comes out soaked (which makes lightning hurt more). Fed, it keeps erupting; when it lifts, the
/// steam condenses into puddles.
/// </summary>
public class SteamExplosion : ElementReaction
{
    public override float FeedRadius => 2.6f;
    protected override float BaseLife => 5f;
    protected override float Sustain => 4.5f;
    protected override float MaxLife => 14f;

    float radius = 0.5f;
    float pulseCharge;
    float pulseCooldown;
    float puffTimer;

    protected override void OnBegin()
    {
        Explode(1f);
    }

    protected override void OnFeed(Vector2 at)
    {
        pulseCharge += 0.25f;
        radius = Mathf.Min(3.4f, radius + 0.02f);
    }

    void Explode(float scale)
    {
        var c = Center;
        ReactionArea.Blast(c, 2.6f * scale, 9f * scale);
        ReactionArea.Characters(c, 2.4f * scale, (s, k) =>
        {
            if (!s.IsPlayer) s.Damage(0.9f * k * scale, Element.Fire, (s.Center - c).normalized);
            s.Soak(5f);
        });
        ReactionArea.Props(c, 2f * scale, Element.Ice, 0.5f);

        CameraShake.Add(0.35f * scale);
        ElementLights.Burst(c, new Color(0.92f, 0.95f, 1f), 5f * scale, 2.6f, 0.35f);

        // A white-hot flash, then a ring of steam racing outward.
        ArcaneParticles.Emit(FxLayer.Glow, new Spark
        {
            pos = c, life = 0.16f, size = 26 * scale, sizeEnd = 40 * scale, shape = FxShape.Halo,
            colorStart = Color.white.WithAlpha(0.7f), colorMid = Elements.IcePale.WithAlpha(0.35f), colorEnd = Elements.Steam.WithAlpha(0f),
        });
        var ring = Mathf.RoundToInt(36 * scale);
        for (var i = 0; i < ring; i++)
        {
            var a = i * Mathf.PI * 2f / ring + Random.Range(-0.05f, 0.05f);
            var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            ArcaneParticles.Emit(FxLayer.Veil, new Spark
            {
                pos = c + dir * 0.2f, vel = dir * Random.Range(6f, 9f) * scale, life = Random.Range(0.7f, 1.2f),
                size = 8, sizeEnd = 22, shape = FxShape.Halo,
                colorStart = Elements.Steam.WithAlpha(0.6f), colorMid = Elements.Steam.WithAlpha(0.35f), colorEnd = Elements.Steam.WithAlpha(0f),
                drag = 4.5f, turbulence = 3f, wind = 0.5f, gravity = -0.8f,
            });
        }
        // Scalding droplets and leftover shards flung out.
        for (var i = 0; i < 18 * scale; i++)
        {
            var v = Random.insideUnitCircle.normalized * Random.Range(3f, 8f) + Vector2.up * 2f;
            var droplet = Random.value < 0.6f;
            ArcaneParticles.Emit(FxLayer.Front, new Spark
            {
                pos = c, vel = v, life = Random.Range(0.4f, 0.8f), size = 1, sizeEnd = 1,
                shape = FxShape.Streak, stretch = 0.4f,
                colorStart = droplet ? Elements.WaterShine : Elements.IceWhite, colorMid = droplet ? Elements.WaterMid : Elements.IcePale,
                colorEnd = Elements.WaterMid.WithAlpha(0f), gravity = 16f, drag = 0.8f,
            });
        }
        ElementFx.SteamPuff(c, 8, 1.4f * scale);
        pulseCharge = 0f;
        pulseCooldown = 0.7f;
    }

    protected override void Tick(float dt)
    {
        // Grow quickly to full size, then rise and drift on the wind.
        radius = Mathf.MoveTowards(radius, 1.8f + intensity * 0.5f, 3f * dt);
        Center += (AirField.At(Center) * 0.04f + new Vector2(0f, 0.18f)) * dt;

        pulseCooldown -= dt;
        if (pulseCharge >= 1f && pulseCooldown <= 0f) Explode(0.65f);

        var fade = Fade(1.5f);
        ElementWorld.AddVeil(Center, radius * 0.85f * fade);

        // Keep the cloud thick: billows tumbling inside its radius.
        puffTimer -= dt;
        if (puffTimer <= 0f)
        {
            puffTimer = 0.03f;
            for (var i = 0; i < 2; i++)
            {
                var p = Center + Random.insideUnitCircle * radius;
                ArcaneParticles.Emit(FxLayer.Veil, new Spark
                {
                    pos = p, vel = Random.insideUnitCircle * 0.4f + Vector2.up * 0.2f, life = Random.Range(0.9f, 1.5f),
                    size = Random.Range(12f, 20f), sizeEnd = Random.Range(22f, 34f), shape = FxShape.Halo,
                    colorStart = Elements.Steam.WithAlpha(0f), colorMid = Elements.Steam.WithAlpha(0.42f * fade), colorEnd = Elements.Steam.WithAlpha(0f),
                    turbulence = 1.5f, wind = 0.5f, drag = 1f,
                });
            }
            if (Random.value < 0.2f)
            {
                // Condensation dripping from the underside.
                ArcaneParticles.Emit(FxLayer.Front, new Spark
                {
                    pos = Center + new Vector2(Random.Range(-radius, radius) * 0.7f, -radius * 0.5f), life = 0.7f, size = 1, sizeEnd = 1,
                    gravity = 14f, colorStart = Elements.WaterShine, colorMid = Elements.WaterMid, colorEnd = Elements.WaterMid.WithAlpha(0f),
                });
            }
        }

        ReactionArea.Characters(Center, radius, (s, k) => s.Soak(1.5f));

        glow.enabled = true;
        glow.color = new Color(0.75f, 0.8f, 0.9f);
        glow.pointLightOuterRadius = radius * 1.6f;
        glow.intensity = 0.35f * fade;
    }

    protected override void OnEnd()
    {
        // The steam condenses into puddles on whatever is below.
        for (var x = -radius; x <= radius; x += SurfaceGrid.Cell)
        {
            if (Random.value < 0.4f) continue;
            if (SurfaceGrid.FindSurface(Center + new Vector2(x, 0f), 8f, out var surface))
                SurfaceGrid.WaterAt(surface.point, 0.6f, 14f);
        }
    }
}
