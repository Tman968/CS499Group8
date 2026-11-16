using UnityEngine;

/// <summary>
/// The look of each element, built from ArcaneParticles: flames that curl and gutter into soot,
/// ice shards trailing frost, crackling arcs, pale spectral gusts carrying dead leaves. Everything
/// here is visual only.
/// </summary>
public static class ElementFx
{
    const float Px = 1f / 16f;

    static float Busy(FxLayer layer) => 1f - Mathf.Clamp01((ArcaneParticles.Fullness(layer) - 0.6f) / 0.4f);

    // ------------------------------------------------------------------ fire

    /// <summary>One flame mote in flight. t is its age from 0 to 1; scale grows the flame.</summary>
    public static void FireMote(Vector2 pos, Vector2 vel, float t, float scale)
    {
        var busy = Busy(FxLayer.Glow);
        var hot = t < 0.25f;
        // The body of the flame: bright, buoyant pixels that wander as they cool.
        for (var i = 0; i < 2; i++)
        {
            if (Random.value > busy) continue;
            ArcaneParticles.Emit(FxLayer.Glow, new Spark
            {
                pos = pos + Random.insideUnitCircle * (0.06f + 0.22f * t) * scale,
                vel = vel * 0.25f + new Vector2(0f, 0.6f),
                life = Random.Range(0.1f, 0.2f) + t * 0.1f,
                size = Mathf.Lerp(3f, 2f, t) * scale, sizeEnd = 1f,
                colorStart = hot ? Elements.FireWhite : Elements.FireYellow,
                colorMid = Elements.FireOrange.WithAlpha(0.9f),
                colorEnd = Elements.FireRed.WithAlpha(0f),
                gravity = -4f, turbulence = 6f, drag = 2f, flicker = 0.25f,
            });
        }
        if (Random.value < 0.35f * busy)
        {
            ArcaneParticles.Emit(FxLayer.Glow, new Spark
            {
                pos = pos, vel = vel * 0.3f, life = 0.14f, size = Mathf.Lerp(10f, 18f, t) * scale, sizeEnd = 20f * scale,
                shape = FxShape.Halo,
                colorStart = Elements.FireOrange.WithAlpha(0.16f), colorMid = Elements.FireRed.WithAlpha(0.1f), colorEnd = Elements.FireDeep.WithAlpha(0f),
            });
        }
        // Tongues licking off the edge of the stream, curling as they rise.
        if (Random.value < t * 0.6f * busy)
        {
            ArcaneParticles.Emit(FxLayer.Glow, new Spark
            {
                pos = pos + Random.insideUnitCircle * 0.25f * scale, vel = vel * 0.15f + new Vector2(0f, 1.2f),
                life = Random.Range(0.22f, 0.38f), size = 2f, sizeEnd = 1f,
                colorStart = Elements.FireOrange, colorMid = Elements.FireRed.WithAlpha(0.8f), colorEnd = Elements.FireDeep.WithAlpha(0f),
                gravity = -9f, turbulence = 12f, curl = Random.Range(-260f, 260f), flicker = 0.3f, wind = 0.6f,
            });
        }
        if (Random.value < 0.05f) Ember(pos, vel * 0.2f + new Vector2(Random.Range(-1f, 1f), Random.Range(1f, 2.5f)));
        if (t > 0.55f && Random.value < 0.14f * Busy(FxLayer.Back)) Smoke(pos, 1f);
    }

    /// <summary>A single drifting ember, long-lived, carried by the air.</summary>
    public static void Ember(Vector2 pos, Vector2 vel)
    {
        ArcaneParticles.Emit(FxLayer.Glow, new Spark
        {
            pos = pos, vel = vel, life = Random.Range(0.7f, 1.4f), size = 1, sizeEnd = 1,
            colorStart = Elements.FireYellow, colorMid = Elements.FireOrange, colorEnd = Elements.FireRed.WithAlpha(0f),
            gravity = -1.2f, turbulence = 9f, drag = 0.8f, flicker = 0.55f, wind = 1f,
        });
    }

    /// <summary>A curl of soot rising behind the flames.</summary>
    public static void Smoke(Vector2 pos, float scale)
    {
        ArcaneParticles.Emit(FxLayer.Back, new Spark
        {
            pos = pos + Random.insideUnitCircle * 0.15f, vel = new Vector2(Random.Range(-0.3f, 0.3f), Random.Range(0.4f, 1f)),
            life = Random.Range(1f, 1.7f), size = 3f * scale, sizeEnd = 8f * scale,
            colorStart = Elements.Soot.WithAlpha(0.5f), colorMid = Elements.Soot.WithAlpha(0.32f), colorEnd = Elements.Soot.WithAlpha(0f),
            gravity = -1.5f, turbulence = 3f, drag = 0.8f, wind = 0.8f,
        });
    }

    /// <summary>Flame splashing across a surface it hits.</summary>
    public static void FireSplash(Vector2 point, Vector2 normal)
    {
        var tangent = new Vector2(-normal.y, normal.x);
        for (var i = 0; i < 3; i++)
        {
            var side = Random.value < 0.5f ? -1f : 1f;
            ArcaneParticles.Emit(FxLayer.Glow, new Spark
            {
                pos = point + normal * 0.06f, vel = tangent * side * Random.Range(1f, 2.6f) + normal * Random.Range(0.3f, 1f),
                life = Random.Range(0.18f, 0.3f), size = 2, sizeEnd = 1,
                colorStart = Elements.FireYellow, colorMid = Elements.FireOrange, colorEnd = Elements.FireRed.WithAlpha(0f),
                gravity = -6f, turbulence = 8f, drag = 3f, flicker = 0.3f,
            });
        }
        if (Random.value < 0.2f) Smoke(point + normal * 0.2f, 0.8f);
    }

    /// <summary>A flame licking up from a patch of embers.</summary>
    public static void EmberLick(Vector2 pos, float strength)
    {
        ArcaneParticles.Emit(FxLayer.Glow, new Spark
        {
            pos = pos, vel = new Vector2(Random.Range(-0.2f, 0.2f), Random.Range(0.4f, 1.2f)),
            life = Random.Range(0.25f, 0.45f) * (0.6f + 0.4f * strength), size = Random.value < 0.3f ? 2 : 1, sizeEnd = 1,
            colorStart = Elements.FireYellow, colorMid = Elements.FireOrange.WithAlpha(0.85f), colorEnd = Elements.FireRed.WithAlpha(0f),
            gravity = -7f, turbulence = 10f, flicker = 0.35f, wind = 0.8f,
        });
        if (Random.value < 0.15f) Smoke(pos + Vector2.up * 0.3f, 0.7f);
        if (Random.value < 0.06f) Ember(pos, new Vector2(Random.Range(-0.5f, 0.5f), Random.Range(1f, 2f)));
    }

    /// <summary>A character on fire.</summary>
    public static void BodyFlames(Bounds b, float intensity)
    {
        var count = Random.value < 0.5f ? 2 : 3;
        for (var i = 0; i < count; i++)
        {
            var p = new Vector2(Random.Range(b.min.x, b.max.x), Random.Range(b.min.y, b.min.y + b.size.y * 0.75f));
            ArcaneParticles.Emit(FxLayer.Glow, new Spark
            {
                pos = p, vel = new Vector2(Random.Range(-0.3f, 0.3f), Random.Range(1f, 2.4f)), life = Random.Range(0.25f, 0.45f),
                size = Random.value < 0.4f ? 2 : 1, sizeEnd = 1,
                colorStart = Elements.FireYellow, colorMid = Elements.FireOrange, colorEnd = Elements.FireRed.WithAlpha(0f),
                gravity = -6f, turbulence = 9f, flicker = 0.3f, wind = 0.6f,
            });
        }
        if (Random.value < 0.08f) Smoke(new Vector2(b.center.x, b.max.y), 1f);
        if (Random.value < 0.05f) Ember(new Vector2(b.center.x, b.max.y), new Vector2(Random.Range(-1f, 1f), 2f));
    }

    // ------------------------------------------------------------------ ice

    /// <summary>One ice shard in flight: a pointed sliver along its velocity, trailing frost.</summary>
    public static void IceShard(Vector2 pos, Vector2 vel)
    {
        var dir = vel.sqrMagnitude > 0.0001f ? vel.normalized : Vector2.right;
        ArcaneParticles.DrawStreak(FxLayer.Front, pos - dir * Px, dir, 7f, 2f, Elements.IceCyan.WithAlpha(0.95f));
        ArcaneParticles.DrawStreak(FxLayer.Front, pos, dir, 5f, 1f, Elements.IceWhite);
        ArcaneParticles.DrawPixel(FxLayer.Glow, pos + dir * (3f * Px), 1, Elements.IceWhite.WithAlpha(0.8f));
        ArcaneParticles.DrawHalo(FxLayer.Glow, pos, 9f, Elements.IceCyan.WithAlpha(0.13f));

        var busy = Busy(FxLayer.Front);
        if (Random.value < 0.7f * busy)
        {
            ArcaneParticles.Emit(FxLayer.Front, new Spark
            {
                pos = pos - dir * 0.1f + Random.insideUnitCircle * 0.04f, vel = -vel * 0.04f + Random.insideUnitCircle * 0.25f,
                life = Random.Range(0.35f, 0.65f), size = Random.value < 0.3f ? 2 : 1, sizeEnd = 1,
                colorStart = Elements.IcePale.WithAlpha(0.6f), colorMid = Elements.FrostMist.WithAlpha(0.3f), colorEnd = Elements.FrostMist.WithAlpha(0f),
                drag = 2f, turbulence = 2f, wind = 0.6f,
            });
        }
        if (Random.value < 0.12f) Glint(pos - dir * 0.15f, 0.22f);
        if (Random.value < 0.08f)
        {
            // Snow shaken loose, drifting down.
            ArcaneParticles.Emit(FxLayer.Front, new Spark
            {
                pos = pos, vel = vel * 0.1f, life = Random.Range(0.8f, 1.3f), size = 1, sizeEnd = 1,
                colorStart = Elements.IceWhite, colorMid = Elements.IcePale.WithAlpha(0.7f), colorEnd = Elements.IcePale.WithAlpha(0f),
                gravity = 1.5f, turbulence = 4f, drag = 1.5f, wind = 1f,
            });
        }
    }

    /// <summary>A four-pointed twinkle of light.</summary>
    public static void Glint(Vector2 pos, float life)
    {
        var c = Elements.IceWhite;
        ArcaneParticles.Emit(FxLayer.Glow, new Spark { pos = pos, life = life, size = 1, sizeEnd = 1, colorStart = c, colorMid = c, colorEnd = c.WithAlpha(0f) });
        var arm = c.WithAlpha(0.7f);
        for (var i = 0; i < 4; i++)
        {
            var d = i switch { 0 => Vector2.right, 1 => Vector2.left, 2 => Vector2.up, _ => Vector2.down };
            ArcaneParticles.Emit(FxLayer.Glow, new Spark
            {
                pos = pos + d * Px, life = life * 0.75f, size = 1, sizeEnd = 1,
                colorStart = arm, colorMid = arm, colorEnd = Elements.IcePale.WithAlpha(0f),
            });
        }
    }

    /// <summary>Ice breaking apart: shards tumbling under gravity, and a puff of powder.</summary>
    public static void IceShatter(Vector2 pos, int count, Vector2 baseVel)
    {
        for (var i = 0; i < count; i++)
        {
            var v = baseVel + Random.insideUnitCircle * Random.Range(1.5f, 4.5f) + Vector2.up * 1.5f;
            var pick = Random.value;
            var col = pick < 0.3f ? Elements.IceWhite : pick < 0.7f ? Elements.IcePale : Elements.IceCyan;
            ArcaneParticles.Emit(FxLayer.Front, new Spark
            {
                pos = pos + Random.insideUnitCircle * 0.2f, vel = v, life = Random.Range(0.45f, 0.85f),
                size = Random.value < 0.35f ? 2 : 1, sizeEnd = 1, shape = Random.value < 0.4f ? FxShape.Streak : FxShape.Pixel, stretch = 0.6f,
                colorStart = col, colorMid = col, colorEnd = col.WithAlpha(0f), gravity = 18f, drag = 0.6f,
            });
        }
        for (var i = 0; i < 3; i++)
        {
            ArcaneParticles.Emit(FxLayer.Front, new Spark
            {
                pos = pos + Random.insideUnitCircle * 0.2f, vel = Random.insideUnitCircle * 0.6f, life = 0.45f,
                size = 6, sizeEnd = 14, shape = FxShape.Halo,
                colorStart = Elements.IcePale.WithAlpha(0.35f), colorMid = Elements.FrostMist.WithAlpha(0.2f), colorEnd = Elements.FrostMist.WithAlpha(0f),
                drag = 2f, wind = 0.6f,
            });
        }
        Glint(pos, 0.3f);
        ElementLights.Burst(pos, Elements.Glow(Element.Ice), 1.8f, 0.8f, 0.25f);
    }

    /// <summary>A shard striking something: a small spray of ice and powder.</summary>
    public static void IceImpact(Vector2 point, Vector2 normal)
    {
        for (var i = 0; i < 6; i++)
        {
            var v = (normal * Random.Range(1f, 3f) + Random.insideUnitCircle * 2f);
            ArcaneParticles.Emit(FxLayer.Front, new Spark
            {
                pos = point + normal * 0.05f, vel = v, life = Random.Range(0.3f, 0.55f), size = 1, sizeEnd = 1,
                colorStart = Random.value < 0.5f ? Elements.IceWhite : Elements.IcePale, colorMid = Elements.IcePale, colorEnd = Elements.IceCyan.WithAlpha(0f),
                gravity = 16f, drag = 0.8f,
            });
        }
        ArcaneParticles.Emit(FxLayer.Front, new Spark
        {
            pos = point + normal * 0.1f, vel = normal * 0.3f, life = 0.35f, size = 5, sizeEnd = 10, shape = FxShape.Halo,
            colorStart = Elements.IcePale.WithAlpha(0.3f), colorMid = Elements.FrostMist.WithAlpha(0.15f), colorEnd = Elements.FrostMist.WithAlpha(0f),
        });
        if (Random.value < 0.4f) Glint(point + normal * 0.08f, 0.2f);
    }

    /// <summary>A new ice block crystallising.</summary>
    public static void IceForm(Vector2 center)
    {
        Glint(center + new Vector2(Random.Range(-2, 3), Random.Range(-2, 3)) * Px, 0.3f);
        ArcaneParticles.Emit(FxLayer.Glow, new Spark
        {
            pos = center, life = 0.3f, size = 8, sizeEnd = 4, shape = FxShape.Halo,
            colorStart = Elements.IcePale.WithAlpha(0.4f), colorMid = Elements.IceCyan.WithAlpha(0.2f), colorEnd = Elements.IceCyan.WithAlpha(0f),
        });
    }

    /// <summary>A character frozen solid inside a block of ice.</summary>
    public static void IceBlock(Bounds b, float timeLeft)
    {
        var min = new Vector2(Mathf.Floor(b.min.x * 16f) - 2f, Mathf.Floor(b.min.y * 16f)) * Px;
        var max = new Vector2(Mathf.Ceil(b.max.x * 16f) + 2f, Mathf.Ceil(b.max.y * 16f) + 2f) * Px;
        var cracking = timeLeft < 0.8f && (int)(timeLeft * 20f) % 2 == 0;
        ArcaneParticles.DrawRect(FxLayer.Front, min, max, Elements.IcePale.WithAlpha(cracking ? 0.22f : 0.3f));
        ArcaneParticles.DrawRect(FxLayer.Front, min, new Vector2(max.x, min.y + Px), Elements.IceDeep.WithAlpha(0.8f));
        ArcaneParticles.DrawRect(FxLayer.Front, new Vector2(max.x - Px, min.y), max, Elements.IceDeep.WithAlpha(0.7f));
        ArcaneParticles.DrawRect(FxLayer.Front, new Vector2(min.x, max.y - Px), max, Elements.IceWhite.WithAlpha(0.85f));
        ArcaneParticles.DrawRect(FxLayer.Front, min, new Vector2(min.x + Px, max.y), Elements.IceCyan.WithAlpha(0.8f));
        // A sheen sliding diagonally across the face of the ice.
        var phase = Mathf.Repeat(Time.time * 0.8f, 2.2f);
        if (phase < 1f)
        {
            var w = max.x - min.x;
            var x = Mathf.Lerp(min.x, max.x + w * 0.4f, phase);
            ArcaneParticles.DrawLine(FxLayer.Front, new Vector2(x, min.y + 2 * Px), new Vector2(x - w * 0.4f, max.y - 2 * Px), 1, Elements.IceWhite.WithAlpha(0.45f));
        }
        if (cracking)
        {
            var c = (min + max) * 0.5f;
            ArcaneParticles.DrawLine(FxLayer.Front, c + new Vector2(-0.2f, 0.4f), c + new Vector2(0.1f, -0.1f), 1, Elements.IceWhite);
            ArcaneParticles.DrawLine(FxLayer.Front, c + new Vector2(0.1f, -0.1f), c + new Vector2(-0.05f, -0.5f), 1, Elements.IceWhite);
        }
        if (Random.value < 0.02f) Glint(new Vector2(Random.Range(min.x, max.x), Random.Range(min.y, max.y)), 0.3f);
    }

    // ------------------------------------------------------------------ steam

    /// <summary>A puff of white steam that rises and hides what is behind it.</summary>
    public static void SteamPuff(Vector2 at, int count, float scale)
    {
        for (var i = 0; i < count; i++)
        {
            ArcaneParticles.Emit(FxLayer.Veil, new Spark
            {
                pos = at + Random.insideUnitCircle * 0.2f * scale,
                vel = new Vector2(Random.Range(-0.5f, 0.5f), Random.Range(0.7f, 1.6f)) * scale,
                life = Random.Range(0.6f, 1.1f), size = 5f * scale, sizeEnd = 14f * scale, shape = FxShape.Halo,
                colorStart = Elements.Steam.WithAlpha(0.5f), colorMid = Elements.Steam.WithAlpha(0.28f), colorEnd = Elements.Steam.WithAlpha(0f),
                drag = 1.5f, turbulence = 2f, wind = 0.6f, gravity = -0.6f,
            });
        }
    }

    // ------------------------------------------------------------------ lightning

    /// <summary>Sparks spraying from where a bolt strikes.</summary>
    public static void BoltImpact(Vector2 point, Vector2 normal, float strength)
    {
        var count = Mathf.RoundToInt(5 * strength);
        for (var i = 0; i < count; i++)
        {
            var v = (normal + Random.insideUnitCircle * 1.1f).normalized * Random.Range(2.5f, 7f);
            ArcaneParticles.Emit(FxLayer.Glow, new Spark
            {
                pos = point, vel = v, life = Random.Range(0.15f, 0.4f), size = 1, sizeEnd = 1,
                shape = FxShape.Streak, stretch = 0.5f,
                colorStart = Elements.BoltCore, colorMid = Elements.BoltViolet, colorEnd = Elements.BoltBlue.WithAlpha(0f),
                gravity = 14f, drag = 1.5f,
            });
        }
        ArcaneParticles.Emit(FxLayer.Glow, new Spark
        {
            pos = point, life = 0.12f, size = 16 * strength, sizeEnd = 6, shape = FxShape.Halo,
            colorStart = Elements.BoltCore.WithAlpha(0.5f), colorMid = Elements.BoltViolet.WithAlpha(0.3f), colorEnd = Elements.BoltDeep.WithAlpha(0f),
        });
        // A scorched, glowing pock where it struck.
        if (Random.value < 0.3f)
        {
            ArcaneParticles.Emit(FxLayer.Back, new Spark
            {
                pos = point, life = 0.8f, size = 2, sizeEnd = 2,
                colorStart = Elements.BoltViolet, colorMid = Elements.BoltDeep.WithAlpha(0.6f), colorEnd = Elements.Soot.WithAlpha(0f),
            });
        }
    }

    // ------------------------------------------------------------------ wind

    /// <summary>One gust mote. strength fades it out with distance.</summary>
    public static void WindMote(Vector2 pos, Vector2 vel, float strength, float seed)
    {
        var speed = vel.magnitude;
        if (speed < 0.01f) return;
        var dir = vel / speed;
        // Wind lines: pale streaks that bend with the air.
        ArcaneParticles.DrawStreak(FxLayer.Front, pos, dir, 3f + speed * 0.9f, 1f, Elements.WispPale.WithAlpha(0.3f * strength));
        var busy = Busy(FxLayer.Front);
        if (Random.value < 0.25f * busy)
        {
            // Spectral wisps that spiral off the gust.
            ArcaneParticles.Emit(FxLayer.Front, new Spark
            {
                pos = pos, vel = vel * 0.55f, life = Random.Range(0.35f, 0.6f), size = 1, sizeEnd = 1,
                colorStart = Elements.WispPale.WithAlpha(0.6f * strength), colorMid = Elements.WispMid.WithAlpha(0.35f * strength), colorEnd = Elements.WispDark.WithAlpha(0f),
                curl = (seed > 0.5f ? 1f : -1f) * Random.Range(320f, 560f), drag = 1.4f, wind = 0.3f,
            });
        }
        if (Random.value < 0.035f * busy)
        {
            // A dead leaf or grave dust caught up in it.
            var leaf = Random.value < 0.6f;
            ArcaneParticles.Emit(FxLayer.Front, new Spark
            {
                pos = pos, vel = vel * 0.7f, life = Random.Range(1f, 1.8f), size = leaf && Random.value < 0.5f ? 2 : 1, sizeEnd = 1,
                colorStart = leaf ? Elements.Leaf : Elements.WispMid, colorMid = leaf ? Elements.LeafDark : Elements.WispDark,
                colorEnd = Elements.LeafDark.WithAlpha(0f),
                gravity = 2f, turbulence = 7f, curl = Random.Range(-200f, 200f), drag = 0.8f, wind = 1f,
            });
        }
        if (Random.value < 0.05f * strength)
        {
            ArcaneParticles.Emit(FxLayer.Back, new Spark
            {
                pos = pos, vel = vel * 0.4f, life = 0.5f, size = 14, sizeEnd = 26, shape = FxShape.Halo,
                colorStart = Elements.WispMid.WithAlpha(0.07f), colorMid = Elements.WispMid.WithAlpha(0.05f), colorEnd = Elements.WispDark.WithAlpha(0f),
            });
        }
    }

    /// <summary>A gust breaking against a surface.</summary>
    public static void WindPuff(Vector2 point, Vector2 normal, Vector2 vel)
    {
        var tangent = new Vector2(-normal.y, normal.x);
        var along = Vector2.Dot(vel, tangent) >= 0f ? tangent : -tangent;
        ArcaneParticles.Emit(FxLayer.Front, new Spark
        {
            pos = point + normal * 0.1f, vel = along * Random.Range(2f, 4f) + normal, life = 0.4f, size = 1, sizeEnd = 1,
            colorStart = Elements.WispPale.WithAlpha(0.5f), colorMid = Elements.WispMid.WithAlpha(0.3f), colorEnd = Elements.WispDark.WithAlpha(0f),
            curl = Random.Range(-400f, 400f), drag = 2f,
        });
    }

    // ------------------------------------------------------------------ wand tips

    /// <summary>The wand tip while the stream pours out: a sigil and a flare in the element's style.</summary>
    public static void Muzzle(Element element, Vector2 tip, Vector2 dir, float power)
    {
        var time = Time.time;
        var perp = new Vector2(-dir.y, dir.x);
        switch (element)
        {
            case Element.Fire:
                ArcaneParticles.DrawHalo(FxLayer.Glow, tip, 16f + Mathf.Sin(time * 40f) * 3f, Elements.FireYellow.WithAlpha(0.4f * power));
                ArcaneParticles.DrawPixel(FxLayer.Glow, tip, 2, Elements.FireWhite.WithAlpha(power));
                if (Random.value < 0.3f) Smoke(tip + Vector2.up * 0.2f, 0.8f);
                // Heat roiling back along the wand.
                if (Random.value < 0.4f)
                {
                    ArcaneParticles.Emit(FxLayer.Glow, new Spark
                    {
                        pos = tip - dir * 0.15f + perp * Random.Range(-0.1f, 0.1f), vel = Vector2.up * 1.2f - dir * 0.6f,
                        life = 0.25f, size = 1, sizeEnd = 1,
                        colorStart = Elements.FireOrange, colorMid = Elements.FireRed, colorEnd = Elements.FireDeep.WithAlpha(0f),
                        gravity = -6f, turbulence = 8f,
                    });
                }
                break;

            case Element.Ice:
            {
                // A frost sigil: six points turning around the tip.
                for (var i = 0; i < 6; i++)
                {
                    var a = time * 3f + i * Mathf.PI / 3f;
                    var r = 0.28f + 0.04f * Mathf.Sin(time * 9f + i);
                    var p = tip + (Vector2)(Quaternion.FromToRotation(Vector3.right, dir) * new Vector3(Mathf.Cos(a) * r * 0.45f, Mathf.Sin(a) * r, 0f));
                    ArcaneParticles.DrawPixel(FxLayer.Glow, p, 1, (i % 2 == 0 ? Elements.IceWhite : Elements.IceCyan).WithAlpha(0.9f * power));
                }
                ArcaneParticles.DrawHalo(FxLayer.Glow, tip, 12f, Elements.IceCyan.WithAlpha(0.25f * power));
                if (Random.value < 0.35f)
                {
                    ArcaneParticles.Emit(FxLayer.Front, new Spark
                    {
                        pos = tip, vel = dir * 0.8f + Random.insideUnitCircle * 0.5f, life = 0.5f, size = 5, sizeEnd = 12, shape = FxShape.Halo,
                        colorStart = Elements.IcePale.WithAlpha(0.22f), colorMid = Elements.FrostMist.WithAlpha(0.12f), colorEnd = Elements.FrostMist.WithAlpha(0f),
                        drag = 2f, wind = 0.5f,
                    });
                }
                if (Random.value < 0.15f) Glint(tip + Random.insideUnitCircle * 0.2f, 0.2f);
                break;
            }

            case Element.Lightning:
                ArcaneParticles.DrawHalo(FxLayer.Glow, tip, 12f + Random.Range(-3f, 4f), Elements.BoltViolet.WithAlpha(0.35f * power));
                ArcaneParticles.DrawPixel(FxLayer.Glow, tip, 2, Elements.BoltCore.WithAlpha(power));
                if (Random.value < 0.45f)
                    LightningBolts.Spawn(tip, tip + Random.insideUnitCircle.normalized * Random.Range(0.2f, 0.45f), 0.05f, 1f, false);
                break;

            case Element.Wind:
            {
                // A vortex sigil: pale points spiralling into the tip as air is drawn in from behind.
                for (var i = 0; i < 3; i++)
                {
                    var a = -time * 14f + i * 2f * Mathf.PI / 3f;
                    var p = tip + (Vector2)(Quaternion.FromToRotation(Vector3.right, dir) * new Vector3(Mathf.Cos(a) * 0.12f, Mathf.Sin(a) * 0.3f, 0f));
                    ArcaneParticles.DrawPixel(FxLayer.Front, p, 1, Elements.WispPale.WithAlpha(0.8f * power));
                }
                if (Random.value < 0.6f)
                {
                    var from = tip - dir * Random.Range(0.6f, 1.2f) + perp * Random.Range(-0.6f, 0.6f);
                    ArcaneParticles.Emit(FxLayer.Front, new Spark
                    {
                        pos = from, vel = (tip - from) / 0.2f, life = 0.2f, size = 1, sizeEnd = 1, shape = FxShape.Streak, stretch = 0.25f,
                        colorStart = Elements.WispPale.WithAlpha(0f), colorMid = Elements.WispPale.WithAlpha(0.5f), colorEnd = Elements.WispMid.WithAlpha(0f),
                    });
                }
                break;
            }
        }
    }

    /// <summary>The wand tip at rest: a little life in each wand even when it isn't being used.</summary>
    public static void TipIdle(Element element, Vector2 tip, float readiness)
    {
        var time = Time.time;
        switch (element)
        {
            case Element.Fire:
                if (Random.value < 0.5f * readiness)
                {
                    ArcaneParticles.Emit(FxLayer.Glow, new Spark
                    {
                        pos = tip, vel = new Vector2(0f, 0.6f), life = Random.Range(0.18f, 0.3f), size = 1, sizeEnd = 1,
                        colorStart = Elements.FireYellow, colorMid = Elements.FireOrange, colorEnd = Elements.FireRed.WithAlpha(0f),
                        gravity = -4f, turbulence = 7f, flicker = 0.3f,
                    });
                }
                if (Random.value < 0.01f) Ember(tip, new Vector2(Random.Range(-0.4f, 0.4f), 1.2f));
                break;
            case Element.Ice:
                if (Random.value < 0.12f * readiness)
                {
                    ArcaneParticles.Emit(FxLayer.Front, new Spark
                    {
                        pos = tip + Random.insideUnitCircle * 0.08f, vel = new Vector2(0f, -0.3f), life = 0.9f, size = 1, sizeEnd = 1,
                        colorStart = Elements.IceWhite, colorMid = Elements.IcePale.WithAlpha(0.7f), colorEnd = Elements.IcePale.WithAlpha(0f),
                        gravity = 0.4f, turbulence = 2f,
                    });
                }
                if (Random.value < 0.015f) Glint(tip, 0.25f);
                break;
            case Element.Lightning:
                if (Random.value < 0.3f) ArcaneParticles.DrawPixel(FxLayer.Glow, tip + Random.insideUnitCircle * 0.08f, 1, Elements.BoltViolet.WithAlpha(readiness));
                if (Random.value < 0.04f * readiness)
                    LightningBolts.Spawn(tip, tip + Random.insideUnitCircle.normalized * Random.Range(0.15f, 0.3f), 0.05f, 1f, false);
                break;
            case Element.Wind:
                for (var i = 0; i < 2; i++)
                {
                    var a = time * 5f + i * Mathf.PI;
                    ArcaneParticles.DrawPixel(FxLayer.Front, tip + new Vector2(Mathf.Cos(a) * 0.18f, Mathf.Sin(a) * 0.08f), 1,
                                              Elements.WispPale.WithAlpha((0.35f + 0.25f * Mathf.Sin(a)) * readiness));
                }
                break;
        }
    }

    /// <summary>A drained wand guttering: smoke, sparks and sputters while it recovers.</summary>
    public static void Sputter(Element element, Vector2 tip)
    {
        if (Random.value < 0.25f) Smoke(tip, 0.6f);
        if (Random.value > 0.12f) return;
        switch (element)
        {
            case Element.Fire:
                Ember(tip, new Vector2(Random.Range(-1f, 1f), Random.Range(0.5f, 1.5f)));
                break;
            case Element.Ice:
                IceImpact(tip, Vector2.up);
                break;
            case Element.Lightning:
                LightningBolts.Spawn(tip, tip + Random.insideUnitCircle.normalized * Random.Range(0.2f, 0.5f), 0.06f, 1f, false);
                break;
            default:
                WindPuff(tip, Vector2.up, Random.insideUnitCircle);
                break;
        }
    }
}
