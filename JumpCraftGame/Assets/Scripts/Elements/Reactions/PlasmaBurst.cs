using UnityEngine;

/// <summary>
/// Fire + Lightning: current pours into the flame and it detonates as electrified, violet-white
/// plasma. A ring of plasma fire tears outward, arcs leap to every skeleton in reach, and the
/// blast burns, shocks and throws. While both streams keep feeding it, a crackling plasma ball
/// hangs at the crossing and bursts again and again.
/// </summary>
public class PlasmaBurst : ElementReaction
{
    public override float FeedRadius => 2.4f;
    protected override float BaseLife => 0.9f;
    protected override float Sustain => 0.8f;
    protected override float MaxLife => 10f;

    float ringAge = 0f;
    float ringRadius;
    float ringMax;
    float pulseTimer;

    protected override void OnBegin() => Burst(1f);

    void Burst(float scale)
    {
        var c = Center;
        ringAge = 0f;
        ringMax = 3.2f * scale;
        pulseTimer = 0.5f;

        ReactionArea.Blast(c, 3.4f * scale, 8f * scale);
        ReactionArea.Characters(c, 3.6f * scale, (s, k) =>
        {
            if (s.IsPlayer) return;
            s.Damage(1.5f * (0.5f + 0.5f * k) * scale * s.Vulnerability(Element.Lightning), Element.Lightning, (s.Center - c).normalized);
            s.Ignite(3f, 0.7f);
            s.Shock(0.9f);
        });
        // Arcs to everything in reach, even outside the blast.
        foreach (var s in ElementStatus.All)
        {
            if (s == null || s.IsDead || s.IsPlayer) continue;
            if (Vector2.Distance(s.Center, c) > 6.5f * scale) continue;
            LightningBolts.Spawn(c, s.Center, 0.25f, 2f);
            ElementFx.BoltImpact(s.Center, (c - s.Center).normalized, 1.5f);
            s.Shock(0.5f);
        }
        ReactionArea.Props(c, 3.5f * scale, Element.Fire, 1.5f);
        ReactionArea.Props(c, 3.5f * scale, Element.Lightning, 1.5f);
        if (SurfaceGrid.FindSurface(c, 5f, out var below))
        {
            SurfaceGrid.Electrify(below.point, below.collider, 4f);
            LightningBolts.Spawn(c, below.point, 0.25f, 2f);
            SurfaceGrid.ApplyFire(below, 1f);
        }

        CameraShake.Add(0.5f * scale);
        ElementLights.Burst(c, new Color(0.85f, 0.65f, 1f), 7f * scale, 3.4f, 0.45f, true);

        ArcaneParticles.Emit(FxLayer.Glow, new Spark
        {
            pos = c, life = 0.22f, size = 30 * scale, sizeEnd = 60 * scale, shape = FxShape.Halo,
            colorStart = ReactionArea.PlasmaWhite.WithAlpha(0.8f), colorMid = ReactionArea.PlasmaPink.WithAlpha(0.35f), colorEnd = ReactionArea.PlasmaDeep.WithAlpha(0f),
        });
        // Plasma shrapnel: streaks of violet fire that arc and fall.
        for (var i = 0; i < 34 * scale; i++)
        {
            var v = Random.insideUnitCircle.normalized * Random.Range(4f, 11f);
            ArcaneParticles.Emit(FxLayer.Glow, new Spark
            {
                pos = c, vel = v, life = Random.Range(0.35f, 0.8f), size = Random.value < 0.3f ? 2 : 1, sizeEnd = 1,
                shape = FxShape.Streak, stretch = 0.45f,
                colorStart = ReactionArea.PlasmaWhite, colorMid = ReactionArea.PlasmaPink, colorEnd = ReactionArea.PlasmaViolet.WithAlpha(0f),
                gravity = 9f, drag = 1.2f, flicker = 0.2f,
            });
        }
        for (var i = 0; i < 6; i++)
        {
            var a = Random.Range(0f, Mathf.PI * 2f);
            var to = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(1.5f, 3f) * scale;
            LightningBolts.Spawn(c, to, Random.Range(0.12f, 0.22f), 1f);
        }
        for (var i = 0; i < 6; i++) ElementFx.Smoke(c + Random.insideUnitCircle * 0.8f, 1.4f);
    }

    protected override void Tick(float dt)
    {
        var c = Center;
        var time = Time.time;

        // The expanding ring of plasma flame.
        if (ringAge < 0.4f)
        {
            ringAge += dt;
            var t = Mathf.Clamp01(ringAge / 0.35f);
            ringRadius = Mathf.Lerp(0.3f, ringMax, 1f - (1f - t) * (1f - t));
            var points = 56;
            for (var i = 0; i < points; i++)
            {
                var a = i * Mathf.PI * 2f / points;
                var wobble = 1f + 0.08f * Mathf.Sin(a * 5f + time * 30f);
                var p = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * ringRadius * wobble;
                ArcaneParticles.DrawPixel(FxLayer.Glow, p, t < 0.5f ? 2 : 1, Color.Lerp(ReactionArea.PlasmaWhite, ReactionArea.PlasmaPink, t).WithAlpha(1f - t * 0.7f));
                if (Random.value < 0.15f)
                {
                    ArcaneParticles.Emit(FxLayer.Glow, new Spark
                    {
                        pos = p, vel = Vector2.up * 1.5f, life = 0.25f, size = 2, sizeEnd = 1,
                        colorStart = ReactionArea.PlasmaPink, colorMid = ReactionArea.PlasmaViolet, colorEnd = ReactionArea.PlasmaDeep.WithAlpha(0f),
                        gravity = -6f, turbulence = 8f,
                    });
                }
            }
        }

        // While fed: a plasma ball, writhing and spitting arcs, that keeps detonating.
        if (RecentlyFed || age < 0.3f)
        {
            var r = 0.35f + 0.08f * intensity;
            for (var i = 0; i < 14; i++)
            {
                var a = time * 9f + i * 0.9f;
                var rr = r * (0.5f + 0.5f * Mathf.Sin(time * 17f + i * 2.3f));
                ArcaneParticles.DrawPixel(FxLayer.Glow, c + new Vector2(Mathf.Cos(a), Mathf.Sin(a * 1.3f)) * rr, 1,
                                          (i % 3 == 0 ? ReactionArea.PlasmaWhite : ReactionArea.PlasmaPink).WithAlpha(0.9f));
            }
            ArcaneParticles.DrawHalo(FxLayer.Glow, c, 22f + Mathf.Sin(time * 40f) * 4f, ReactionArea.PlasmaViolet.WithAlpha(0.35f));
            if (Random.value < 0.3f)
                LightningBolts.Spawn(c, c + Random.insideUnitCircle.normalized * Random.Range(0.5f, 1.4f), 0.07f, 1f, false);

            pulseTimer -= dt;
            if (pulseTimer <= 0f) Burst(0.6f + 0.1f * intensity);
        }

        glow.enabled = true;
        glow.color = new Color(0.8f, 0.55f, 1f);
        glow.pointLightOuterRadius = 3.5f;
        glow.intensity = (RecentlyFed ? 1.6f : 0.6f) * Fade(0.5f) * (0.7f + 0.3f * Random.value);
    }
}
