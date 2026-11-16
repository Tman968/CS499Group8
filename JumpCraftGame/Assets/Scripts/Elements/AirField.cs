using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The moving air in the world: wind cones from wind wands, swirls from vortexes and tornadoes,
/// and rising columns of heat over embers. Everything that can be blown about samples the same
/// field: particles, stream motes, the player, skeletons, crates, thrown bones and dropped items.
///
/// Sources re-add their zones every frame. Zones are double-buffered: those added during one frame
/// are what everything feels during the next, so a source counts once whatever the frame rate,
/// and one that is switched off or destroyed simply stops blowing.
/// </summary>
public static class AirField
{
    enum Kind : byte { Cone, Swirl, Column }

    struct Zone
    {
        public Kind kind;
        public Vector2 origin, dir;
        public float range, cosHalf, strength;
        public float radius, spin, pull, lift;
        public int owner;
    }

    static List<Zone> zones = new();      // last frame's: what is felt now
    static List<Zone> building = new();   // this frame's, being added
    static int buildFrame = -1;

    // Called before every add and query: on the first call of a new frame, last frame's zones go live.
    static void Advance()
    {
        var frame = Time.frameCount;
        if (frame == buildFrame) return;
        // A frame with no adds at all leaves nothing blowing.
        (zones, building) = (building, zones);
        building.Clear();
        if (frame != buildFrame + 1) zones.Clear();
        buildFrame = frame;
    }

    static void Add(Zone z)
    {
        Advance();
        building.Add(z);
    }

    /// <summary>
    /// A fan of wind from origin along dir. strength is the acceleration (units/s^2) right at the
    /// origin; it falls off with distance and fades at the edges of the fan.
    /// </summary>
    public static void AddCone(Vector2 origin, Vector2 dir, float range, float halfAngleDeg, float strength, int owner = 0)
    {
        Add(new Zone
        {
            kind = Kind.Cone, origin = origin, dir = dir.normalized, range = range,
            cosHalf = Mathf.Cos(halfAngleDeg * Mathf.Deg2Rad), strength = strength, owner = owner,
        });
    }

    /// <summary>A vortex: spin is the tangential acceleration (positive is counter-clockwise),
    /// pull draws things toward the axis and lift raises them.</summary>
    public static void AddSwirl(Vector2 center, float radius, float spin, float pull, float lift, int owner = 0)
    {
        Add(new Zone { kind = Kind.Swirl, origin = center, radius = radius, spin = spin, pull = pull, lift = lift, owner = owner });
    }

    /// <summary>A column of rising air, halfWidth either side of baseCenter and height above it.</summary>
    public static void AddColumn(Vector2 baseCenter, float halfWidth, float height, float lift, int owner = 0)
    {
        Add(new Zone { kind = Kind.Column, origin = baseCenter, radius = halfWidth, range = height, lift = lift, owner = owner });
    }

    /// <summary>The air's acceleration at p, leaving out zones made by ignoreOwner.</summary>
    public static Vector2 At(Vector2 p, int ignoreOwner = 0)
    {
        Advance();
        var total = Vector2.zero;
        for (var i = 0; i < zones.Count; i++)
        {
            var z = zones[i];
            if (ignoreOwner != 0 && z.owner == ignoreOwner) continue;
            switch (z.kind)
            {
                case Kind.Cone:
                {
                    var to = p - z.origin;
                    var d = to.magnitude;
                    if (d > z.range || d < 0.001f) break;
                    var cos = Vector2.Dot(to / d, z.dir);
                    if (cos < z.cosHalf) break;
                    // Soft edges across the fan, and a steep fall-off with distance.
                    var edge = Mathf.Clamp01((cos - z.cosHalf) / Mathf.Max(0.0001f, (1f - z.cosHalf) * 0.6f));
                    var falloff = 1f / (1f + (d / (z.range * 0.22f)) * (d / (z.range * 0.22f)));
                    var tail = 1f - Mathf.Clamp01((d - z.range * 0.8f) / (z.range * 0.2f));
                    // Air spreads along the fan, so blow outward from the origin, not just along dir.
                    var along = Vector2.Lerp(z.dir, to / d, 0.5f).normalized;
                    total += along * (z.strength * edge * falloff * tail);
                    break;
                }
                case Kind.Swirl:
                {
                    var to = p - z.origin;
                    var d = to.magnitude;
                    if (d > z.radius || d < 0.001f) break;
                    var k = 1f - d / z.radius;
                    var tangent = new Vector2(-to.y, to.x) / d;
                    total += tangent * (z.spin * k) - to / d * (z.pull * k) + Vector2.up * (z.lift * k);
                    break;
                }
                case Kind.Column:
                {
                    var dx = Mathf.Abs(p.x - z.origin.x);
                    var dy = p.y - z.origin.y;
                    if (dx > z.radius || dy < -0.2f || dy > z.range) break;
                    var k = (1f - dx / z.radius) * (1f - Mathf.Clamp01(dy / z.range) * 0.7f);
                    total += Vector2.up * (z.lift * k);
                    break;
                }
            }
        }
        return total;
    }

    /// <summary>Forgets every zone, for scene changes.</summary>
    public static void Clear()
    {
        zones.Clear();
        building.Clear();
    }
}
