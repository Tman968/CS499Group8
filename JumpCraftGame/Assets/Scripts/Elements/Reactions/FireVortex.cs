using UnityEngine;

/// <summary>
/// Fire + Wind: the gust whips the flame into a spinning vortex of fire that keeps expanding while
/// it is fed. It draws in and lifts anything nearby, burns what it catches, flings embers, and
/// scorches the ground below, leaving burning patches (and their updrafts) behind. It drifts the
/// way the wind blew.
/// </summary>
public class FireVortex : ElementReaction
{
    public override float FeedRadius => radius + 1.2f;
    protected override float BaseLife => 3f;
    protected override float Sustain => 2f;
    protected override float MaxLife => 12f;

    protected override string LoopSound => "vortex_loop";

    float radius = 0.5f;
    float spinSign = 1f;
    float scorchTimer;
    float hurtTimer;

    protected override void OnBegin()
    {
        // Spin the way the gust curls past the flame.
        spinSign = flow.x >= 0f ? -1f : 1f;
        Sfx.Play("vortex_start", Center);
        CameraShake.Add(0.15f);
        ElementLights.Burst(Center, Elements.Glow(Element.Fire), 4f, 2f, 0.3f);
        for (var i = 0; i < 16; i++)
            ElementFx.Ember(Center, Random.insideUnitCircle * 4f);
    }

    protected override void Tick(float dt)
    {
        var fade = Fade(1f);
        var targetRadius = 0.9f + intensity * 0.6f;
        radius = Mathf.MoveTowards(radius, targetRadius * (0.3f + 0.7f * fade), 1.2f * dt);
        var height = radius * 2.4f;
        var spin = spinSign * (7f + 3f / Mathf.Max(0.4f, radius));
        var time = Time.time;

        // Drift on with the wind.
        var drift = flow.sqrMagnitude > 0.01f ? flow.normalized : Vector2.zero;
        Center += new Vector2(drift.x * 0.9f, drift.y * 0.25f) * dt;

        // Strong enough to lift and draw in a skeleton or a crate near the axis, not to hurl the wizard across the level.
        AirField.AddSwirl(Center, radius * 1.8f, spin * 2.5f, 9f, 17f);

        // The funnel: a helix of flame, brighter on the side turned toward us.
        const int Strands = 3;
        const int Points = 26;
        for (var s = 0; s < Strands; s++)
        {
            for (var i = 0; i < Points; i++)
            {
                var h = i / (float)(Points - 1);
                var r = radius * (0.35f + 0.75f * h);
                var a = time * spin + h * 7f + s * Mathf.PI * 2f / Strands;
                var depth = Mathf.Sin(a);
                var p = Center + new Vector2(Mathf.Cos(a) * r, -radius * 0.6f + h * height);
                var front = depth > 0f;
                var col = front ? (h < 0.35f ? Elements.FireWhite : h < 0.7f ? Elements.FireYellow : Elements.FireOrange)
                                : (h < 0.5f ? Elements.FireRed : Elements.FireDeep);
                ArcaneParticles.DrawPixel(front ? FxLayer.Glow : FxLayer.Back, p, front && h < 0.6f ? 2 : 1, col.WithAlpha(fade * (front ? 1f : 0.8f)));
            }
        }
        ArcaneParticles.DrawHalo(FxLayer.Glow, Center + Vector2.up * radius * 0.4f, radius * 40f, Elements.FireOrange.WithAlpha(0.14f * fade));

        // Flames peeling off the funnel, and embers flung out of it.
        for (var i = 0; i < 4; i++)
        {
            var h = Random.value;
            var a = time * spin + h * 7f + Random.Range(0, 3) * Mathf.PI * 2f / 3f;
            var r = radius * (0.35f + 0.75f * h);
            var p = Center + new Vector2(Mathf.Cos(a) * r, -radius * 0.6f + h * height);
            var tangent = new Vector2(-Mathf.Sin(a), 0f) * spin * r;
            ArcaneParticles.Emit(FxLayer.Glow, new Spark
            {
                pos = p, vel = tangent * 0.5f + Vector2.up * 1.5f, life = Random.Range(0.2f, 0.35f), size = 2, sizeEnd = 1,
                colorStart = Elements.FireYellow, colorMid = Elements.FireOrange, colorEnd = Elements.FireRed.WithAlpha(0f),
                gravity = -6f, turbulence = 8f, flicker = 0.3f,
            });
            if (Random.value < 0.25f) ElementFx.Ember(p, tangent * 1.2f + Vector2.up * 2f);
        }
        if (Random.value < 0.35f) ElementFx.Smoke(Center + Vector2.up * (height - radius * 0.6f), 1.4f);

        // Burn what it holds.
        hurtTimer -= dt;
        if (hurtTimer <= 0f)
        {
            hurtTimer = 0.2f;
            ReactionArea.Characters(Center, radius * 1.4f, (s, k) =>
            {
                if (s.IsPlayer) s.Ignite(0.6f, 0.15f);
                else
                {
                    s.Ignite(2.5f, 1f);
                    s.Damage(0.15f, Element.Fire, Vector2.up, false);
                }
            });
            ReactionArea.Props(Center, radius * 1.4f, Element.Fire, 0.6f);
        }

        // Scorch the ground beneath it.
        scorchTimer -= dt;
        if (scorchTimer <= 0f)
        {
            scorchTimer = 0.12f;
            var x = Center.x + Random.Range(-radius, radius) * 1.2f;
            if (SurfaceGrid.FindSurface(new Vector2(x, Center.y + radius), 4f + radius, out var surface))
                SurfaceGrid.ApplyFire(surface, 1f);
        }

        glow.enabled = true;
        glow.color = Elements.Glow(Element.Fire);
        glow.pointLightOuterRadius = 2.5f + radius * 2f;
        glow.intensity = (1.4f + 0.3f * Mathf.Sin(time * 23f) * Mathf.Sin(time * 7f)) * fade;
    }

    protected override void OnEnd()
    {
        for (var i = 0; i < 20; i++)
            ElementFx.Ember(Center + Random.insideUnitCircle * radius, Random.insideUnitCircle * 4f + Vector2.up * 2f);
        for (var i = 0; i < 4; i++) ElementFx.Smoke(Center + Random.insideUnitCircle * radius, 1.6f);
    }
}
