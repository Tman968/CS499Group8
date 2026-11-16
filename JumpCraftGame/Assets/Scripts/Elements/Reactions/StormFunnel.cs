using UnityEngine;

/// <summary>
/// Wind + Lightning: the gust spins the current into a tornado with a storm inside it. It sets off
/// along the ground the way the wind blew, its base hugging whatever surface it crosses, sucking in
/// and hurling loose things, lifting anyone who rides it, and striking every skeleton it passes.
/// It electrifies the puddles and ice it crosses. Fed, it lasts longer and grows taller.
/// </summary>
public class StormFunnel : ElementReaction
{
    // Wide, so while two streams keep crossing they keep feeding the funnel already on its way
    // instead of spawning a new one every second.
    public override float FeedRadius => 6f;
    protected override float BaseLife => 6f;
    protected override float Sustain => 4f;
    protected override float MaxLife => 13f;

    float travel = 1f;
    float height = 3f;
    float strikeTimer;
    float arcTimer;
    float chargeTimer;
    float baseY;

    const float BaseRadius = 0.45f;

    protected override void OnBegin()
    {
        travel = flow.x >= 0f ? 1f : -1f;
        baseY = Center.y;
        Sfx.Play("storm_start", Center);
        if (SurfaceGrid.FindSurface(Center, 8f, out var ground)) baseY = ground.point.y;
        CameraShake.Add(0.3f);
        ElementLights.Burst(Center, new Color(0.7f, 0.65f, 1f), 5f, 2.2f, 0.35f, true);
        LightningBolts.Spawn(Center + Vector2.up * 6f, Center, 0.3f, 2f);
    }

    float TopRadius => 1.5f + intensity * 0.25f;

    protected override string LoopSound => "storm_loop";
    protected override Vector2 LoopPosition => Center + Vector2.up * height * 0.5f;

    protected override void Tick(float dt)
    {
        var fade = Fade(1f);
        var time = Time.time;
        height = Mathf.MoveTowards(height, 3.6f + intensity * 0.6f, dt);

        // Travel along the ground, the base following the surface below (or sinking over a gap).
        var x = Center.x + travel * 2.4f * dt;
        if (SurfaceGrid.FindSurface(new Vector2(x, baseY + 1.5f), 4f, out var ground)) baseY = Mathf.MoveTowards(baseY, ground.point.y, 6f * dt);
        else baseY -= 0.8f * dt;
        Center = new Vector2(x, baseY);

        var mid = Center + Vector2.up * height * 0.5f;
        // Near the axis the lift beats gravity, so the funnel can carry someone up; away from it, it only tugs.
        AirField.AddSwirl(mid, height * 0.65f, -travel * 16f, 9f, 19f);
        AirField.AddSwirl(Center + Vector2.up * 0.4f, 1.6f, -travel * 14f, 12f, 17f);

        // The body: a dark column of churning storm cloud, narrow at the base, wide at the top,
        // swaying as it goes.
        for (var i = 0; i < 12; i++)
        {
            var h = i / 11f;
            var r = Mathf.Lerp(BaseRadius, TopRadius, h * h);
            var sway = Mathf.Sin(time * 2f + h * 3f) * 0.15f * h;
            ArcaneParticles.DrawHalo(FxLayer.Back, Center + new Vector2(sway, h * height), r * 2.6f * 16f,
                                     Elements.Rgb(34, 26, 52).WithAlpha(0.42f * fade));
        }
        ArcaneParticles.DrawHalo(FxLayer.Glow, mid, height * 12f, Elements.BoltDeep.WithAlpha(0.22f * fade));

        // Spiralling bands of wind and grit around it, bright on the side turned toward us.
        const int Points = 44;
        for (var s = 0; s < 5; s++)
        {
            for (var i = 0; i < Points; i++)
            {
                var h = i / (float)(Points - 1);
                var r = Mathf.Lerp(BaseRadius, TopRadius, h * h);
                var a = -travel * time * 11f + h * 9f + s * Mathf.PI * 0.4f;
                var front = Mathf.Sin(a) > 0f;
                var p = Center + new Vector2(Mathf.Cos(a) * r + Mathf.Sin(time * 2f + h * 3f) * 0.15f * h, h * height);
                var col = front ? (s % 2 == 0 ? Elements.WispPale : Elements.WispMid) : Elements.WispDark;
                ArcaneParticles.DrawPixel(front ? FxLayer.Front : FxLayer.Back, p, front && s % 2 == 0 ? 2 : 1, col.WithAlpha((front ? 0.9f : 0.7f) * fade));
            }
        }

        // Debris whipping round the funnel.
        for (var i = 0; i < 4; i++)
        {
            var h = Random.value;
            var r = Mathf.Lerp(BaseRadius, TopRadius, h * h) * 1.1f;
            var a = Random.Range(0f, Mathf.PI * 2f);
            var leaf = Random.value < 0.5f;
            ArcaneParticles.Emit(FxLayer.Front, new Spark
            {
                pos = Center + new Vector2(Mathf.Cos(a) * r, h * height), vel = new Vector2(-travel * Mathf.Sin(a) * 6f, 1.5f),
                life = Random.Range(0.3f, 0.6f), size = leaf && Random.value < 0.5f ? 2 : 1, sizeEnd = 1,
                colorStart = leaf ? Elements.Leaf : Elements.WispPale, colorMid = leaf ? Elements.LeafDark : Elements.WispMid,
                colorEnd = Elements.WispDark.WithAlpha(0f), curl = -travel * 420f, drag = 0.6f,
            });
        }

        // Dust and debris kicked up and flung from the base.
        if (Random.value < 0.7f)
        {
            var side = Random.value < 0.5f ? -1f : 1f;
            ArcaneParticles.Emit(FxLayer.Front, new Spark
            {
                pos = Center + new Vector2(side * BaseRadius, 0.05f), vel = new Vector2(side * Random.Range(1.5f, 3.5f), Random.Range(1f, 3f)),
                life = Random.Range(0.5f, 0.9f), size = Random.value < 0.3f ? 2 : 1, sizeEnd = 1,
                colorStart = Random.value < 0.5f ? Elements.Leaf : Elements.WispMid, colorMid = Elements.LeafDark, colorEnd = Elements.WispDark.WithAlpha(0f),
                gravity = 6f, curl = -travel * 300f, wind = 0.8f,
            });
        }

        // Lightning living inside the funnel.
        arcTimer -= dt;
        if (arcTimer <= 0f)
        {
            arcTimer = Random.Range(0.04f, 0.1f);
            var h0 = Random.Range(0.1f, 0.9f);
            var h1 = Mathf.Clamp01(h0 + Random.Range(-0.35f, 0.35f));
            var a0 = Random.Range(0f, Mathf.PI * 2f);
            var p0 = Center + new Vector2(Mathf.Cos(a0) * Mathf.Lerp(BaseRadius, TopRadius, h0 * h0), h0 * height);
            var p1 = Center + new Vector2(Mathf.Cos(a0 + 2.5f) * Mathf.Lerp(BaseRadius, TopRadius, h1 * h1), h1 * height);
            LightningBolts.Spawn(p0, p1, 0.08f, 1f, false);
        }

        // Strike skeletons it passes, and jolt anyone riding it.
        strikeTimer -= dt;
        if (strikeTimer <= 0f)
        {
            strikeTimer = 0.4f;
            var target = ReactionArea.NearestEnemy(mid, 4.5f);
            if (target != null)
            {
                var from = Center + Vector2.up * height * Random.Range(0.5f, 0.9f);
                LightningBolts.Spawn(from, target.Center, 0.2f, 2f);
                ElementFx.BoltImpact(target.Center, (from - target.Center).normalized, 1.4f);
                Sfx.Play("zap_hit", target.Center, 1f, 0.85f);
                Sfx.Play("thunder", target.Center, 0.45f, 1.2f);
                target.Damage(0.6f * target.Vulnerability(Element.Lightning), Element.Lightning, new Vector2(travel, 0.5f));
                target.Shock(0.5f);
                ElementLights.Burst(target.Center, Elements.Glow(Element.Lightning), 3f, 1.6f, 0.2f);
                CameraShake.Add(0.08f);
            }
            ReactionArea.Characters(mid, height * 0.5f, (s, k) =>
            {
                if (s.IsPlayer && Random.value < 0.3f) s.Damage(0.12f, Element.Lightning, Vector2.up, false);
            });
            ReactionArea.Props(mid, height * 0.5f, Element.Lightning, 0.6f);
            ReactionArea.Props(mid, height * 0.5f, Element.Wind, 1f);
        }

        chargeTimer -= dt;
        if (chargeTimer <= 0f)
        {
            chargeTimer = 0.25f;
            SurfaceGrid.Electrify(Center + Vector2.up * 0.05f, null, 2.5f);
        }

        glow.enabled = true;
        glow.color = new Color(0.65f, 0.6f, 1f);
        glow.pointLightOuterRadius = height;
        glow.intensity = (0.5f + Random.value * 0.6f) * fade;
        glow.transform.position = mid;
    }

    protected override void OnEnd()
    {
        Sfx.Play("thunder", Center, 0.8f);
        LightningBolts.Spawn(Center + Vector2.up * height, Center, 0.3f, 2f);
        ElementLights.Burst(Center, Elements.Glow(Element.Lightning), 5f, 2f, 0.3f);
        CameraShake.Add(0.2f);
    }
}
