using UnityEngine;

/// <summary>
/// Ice + Wind: the gust shreds the shards into a howling blizzard that rolls along with the wind.
/// Everything caught in it is chilled (skeletons freeze solid; the wizard only slows), the ground
/// under it is frosted slick and puddles freeze, swinging hazards slow to a crawl, and the snow is
/// thick enough that skeletons can't aim through it.
/// </summary>
public class Blizzard : ElementReaction
{
    public override float FeedRadius => halfWidth + 1f;
    protected override float BaseLife => 4.5f;
    protected override float Sustain => 3f;
    protected override float MaxLife => 11f;

    float halfWidth = 1.2f;
    float halfHeight = 1f;
    float frostTimer;
    float tickTimer;

    protected override string LoopSound => "blizzard_loop";

    protected override void OnBegin()
    {
        Sfx.Play("blizzard_start", Center);
        ElementLights.Burst(Center, Elements.Glow(Element.Ice), 3f, 1.4f, 0.3f);
        ElementFx.IceShatter(Center, 14, flow.normalized * 3f);
    }

    protected override void Tick(float dt)
    {
        var fade = Fade(1.2f);
        halfWidth = Mathf.MoveTowards(halfWidth, 2.2f + intensity * 0.7f, 2f * dt);
        halfHeight = Mathf.MoveTowards(halfHeight, 1.4f + intensity * 0.3f, 1.5f * dt);

        var dir = flow.sqrMagnitude > 0.01f ? flow.normalized : Vector2.right;
        var drift = new Vector2(dir.x, dir.y * 0.2f);
        Center += drift * (1.4f * dt);

        // The blizzard's own air: driving along the gust and churning.
        AirField.AddCone(Center - drift * halfWidth, drift, halfWidth * 2.4f, 35f, 22f);
        AirField.AddSwirl(Center, halfWidth, 6f * Mathf.Sign(-dir.x), 0f, 2f);
        ElementWorld.AddVeil(Center, halfHeight * 0.9f * fade);

        // Snow driving through: dense pixels streaking along the wind, and a pale murk.
        var busy = 1f - Mathf.Clamp01((ArcaneParticles.Fullness(FxLayer.Front) - 0.6f) / 0.4f);
        for (var i = 0; i < 22 * busy; i++)
        {
            var p = Center + new Vector2(Random.Range(-halfWidth, halfWidth), Random.Range(-halfHeight, halfHeight));
            ArcaneParticles.Emit(FxLayer.Front, new Spark
            {
                pos = p, vel = drift * Random.Range(4f, 8f) + Vector2.down * Random.Range(0.5f, 2f),
                life = Random.Range(0.35f, 0.7f), size = Random.value < 0.25f ? 2 : 1, sizeEnd = 1,
                shape = Random.value < 0.5f ? FxShape.Streak : FxShape.Pixel, stretch = 0.25f,
                colorStart = Elements.IceWhite.WithAlpha(0f), colorMid = Elements.IceWhite.WithAlpha(0.9f * fade), colorEnd = Elements.IcePale.WithAlpha(0f),
                turbulence = 10f, wind = 0.6f, curl = Random.Range(-120f, 120f),
            });
        }
        for (var m = 0; m < 2; m++)
        {
            ArcaneParticles.Emit(FxLayer.Veil, new Spark
            {
                pos = Center + new Vector2(Random.Range(-halfWidth, halfWidth), Random.Range(-halfHeight, halfHeight)) * 0.8f,
                vel = drift * 2f, life = Random.Range(0.8f, 1.3f), size = 16, sizeEnd = 30, shape = FxShape.Halo,
                colorStart = Elements.FrostMist.WithAlpha(0f), colorMid = Elements.FrostMist.WithAlpha(0.3f * fade), colorEnd = Elements.FrostMist.WithAlpha(0f),
                turbulence = 2f, wind = 0.4f,
            });
        }
        if (Random.value < 0.08f) ElementFx.Glint(Center + Random.insideUnitCircle * halfWidth, 0.25f);

        tickTimer -= dt;
        if (tickTimer <= 0f)
        {
            tickTimer = 0.1f;
            var area = Mathf.Max(halfWidth, halfHeight) * 1.1f;
            ReactionArea.Characters(Center, area, (s, k) =>
            {
                s.ApplyChill((s.IsPlayer ? 0.05f : 0.08f) * fade);
                if (!s.IsPlayer) s.Damage(0.02f, Element.Ice, drift, false);
                s.AddPush(drift * 0.25f);
            });
            ReactionArea.Props(Center, area, Element.Ice, 0.5f);
        }

        // Frost the ground beneath it.
        frostTimer -= dt;
        if (frostTimer <= 0f)
        {
            frostTimer = 0.06f;
            var x = Center.x + Random.Range(-halfWidth, halfWidth);
            if (SurfaceGrid.FindSurface(new Vector2(x, Center.y + halfHeight), halfHeight * 2f + 3f, out var surface))
                SurfaceGrid.FrostAt(surface.point, 0.35f);
        }

        glow.enabled = true;
        glow.color = Elements.Glow(Element.Ice);
        glow.pointLightOuterRadius = halfWidth * 2f;
        glow.intensity = 0.5f * fade;
    }
}
