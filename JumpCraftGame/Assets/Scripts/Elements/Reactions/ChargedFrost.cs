using UnityEngine;

/// <summary>
/// Ice + Lightning: current crystallises the shards into charged ice. A crackling plate of ice
/// freezes in midair at the crossing (a platform, but a live one), and charged crystals rain onto
/// the ground below, glazing it slick and electrified. Skeletons on it are shocked and chilled;
/// the wizard can cross it, but not without a jolt. Feeding it widens the plate.
/// </summary>
public class ChargedFrost : ElementReaction
{
    public override float FeedRadius => 2f;
    protected override float BaseLife => 3.5f;
    protected override float Sustain => 2f;
    protected override float MaxLife => 9f;

    const float PlateLife = 11f;

    int plateHalfWidth;
    float growTimer;
    float zapTimer;
    Vector2 groundPoint;
    bool hasGround;

    protected override void OnBegin()
    {
        plateHalfWidth = 2;
        SurfaceGrid.BuildPlate(Center, plateHalfWidth, PlateLife, 5f);
        hasGround = SurfaceGrid.FindSurface(Center + Vector2.down * 0.4f, 9f, out var ground);
        if (hasGround)
        {
            groundPoint = ground.point;
            GlazeGround(1.6f);
            LightningBolts.Spawn(Center, groundPoint, 0.3f, 2f);
        }
        CameraShake.Add(0.25f);
        ElementLights.Burst(Center, new Color(0.6f, 0.75f, 1f), 4.5f, 2.4f, 0.35f, true);
        Crystals(26);
    }

    protected override void OnFeed(Vector2 at)
    {
        growTimer -= 0.12f;
        if (growTimer > 0f || plateHalfWidth >= 7) return;
        growTimer = 0.35f;
        plateHalfWidth++;
        SurfaceGrid.BuildPlate(Center, plateHalfWidth, PlateLife, 5f);
        if (hasGround) GlazeGround(0.6f + plateHalfWidth * SurfaceGrid.Cell);
        Crystals(8);
    }

    void GlazeGround(float halfWidth)
    {
        for (var x = -halfWidth; x <= halfWidth; x += SurfaceGrid.Cell)
        {
            if (SurfaceGrid.FindSurface(new Vector2(groundPoint.x + x, groundPoint.y + 0.6f), 1.5f, out var s))
                SurfaceGrid.FrostAt(s.point, 0.8f);
        }
        SurfaceGrid.Electrify(groundPoint, null, 5f);
    }

    void Crystals(int count)
    {
        for (var i = 0; i < count; i++)
        {
            var v = new Vector2(Random.Range(-3f, 3f), Random.Range(-1f, 4f));
            var c = Random.value;
            ArcaneParticles.Emit(FxLayer.Glow, new Spark
            {
                pos = Center + Random.insideUnitCircle * 0.3f, vel = v, life = Random.Range(0.5f, 1f), size = Random.value < 0.4f ? 2 : 1, sizeEnd = 1,
                shape = FxShape.Streak, stretch = 0.3f,
                colorStart = c < 0.5f ? Elements.IceWhite : Elements.BoltCore,
                colorMid = c < 0.5f ? Elements.IceCyan : Elements.BoltViolet,
                colorEnd = Elements.BoltDeep.WithAlpha(0f), gravity = 12f, drag = 0.5f, flicker = 0.3f,
            });
        }
    }

    protected override void Tick(float dt)
    {
        var fade = Fade(0.6f);
        var time = Time.time;

        // Crackling between the plate and the ground, and across the plate.
        if (Random.value < 0.25f * fade)
        {
            var x = Random.Range(-plateHalfWidth, plateHalfWidth + 1) * SurfaceGrid.Cell;
            var from = Center + new Vector2(x, 0f);
            if (hasGround && Random.value < 0.4f)
                LightningBolts.Spawn(from, groundPoint + new Vector2(x * 0.6f, 0f), 0.08f, 1f, false);
            else
                LightningBolts.Spawn(from, from + new Vector2(Random.Range(-0.8f, 0.8f), Random.Range(-0.2f, 0.3f)), 0.06f, 1f, false);
        }
        if (Random.value < 0.1f) ElementFx.Glint(Center + new Vector2(Random.Range(-plateHalfWidth, plateHalfWidth) * SurfaceGrid.Cell, 0.15f), 0.25f);

        // The plate zaps and chills skeletons that come near.
        zapTimer -= dt;
        if (zapTimer <= 0f)
        {
            zapTimer = 0.35f;
            var reach = plateHalfWidth * SurfaceGrid.Cell + 1.8f;
            var target = ReactionArea.NearestEnemy(Center, reach);
            if (target != null)
            {
                LightningBolts.Spawn(Center, target.Center, 0.15f, 1.5f);
                ElementFx.BoltImpact(target.Center, (Center - target.Center).normalized, 1f);
                target.Damage(0.4f * target.Vulnerability(Element.Lightning), Element.Lightning, (target.Center - Center).normalized);
                target.Shock(0.4f);
                target.ApplyChill(0.25f);
            }
        }

        glow.enabled = true;
        glow.color = new Color(0.55f, 0.7f, 1f);
        glow.pointLightOuterRadius = 2.5f + plateHalfWidth * 0.3f;
        glow.intensity = (0.8f + 0.4f * Mathf.Sin(time * 31f) * Mathf.Sin(time * 11f)) * fade;
    }
}
