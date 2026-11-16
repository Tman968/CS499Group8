using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Turns a midair collision of two elements into a reaction:
///
///   Fire + Ice        steam explosion that leaves a cloud no one can see through
///   Fire + Wind       a spinning vortex of fire that grows as it is fed
///   Fire + Lightning  bursts of electrified (plasma) flame
///   Ice + Wind        a drifting blizzard that frosts the ground and freezes what is caught in it
///   Ice + Lightning   crystallised, charged ice: a crackling plate of ice in the air, and the ground below electrified
///   Wind + Lightning  a tornado full of lightning that travels along the ground
///
/// A collision near a running reaction of the same pair feeds it instead of starting a new one.
/// </summary>
public static class ElementReactions
{
    const int MaxReactions = 20;
    const int MaxOfOneKind = 3;

    public static void Trigger(Element a, Element b, Vector2 at, Vector2 velocityA, Vector2 velocityB, float amount)
    {
        if (a == b) return;
        if (a > b)
        {
            (a, b) = (b, a);
            (velocityA, velocityB) = (velocityB, velocityA);
        }
        var flow = velocityA + velocityB;
        // Wind sets the direction of anything it is part of.
        if (b == Element.Wind) flow = velocityB;

        ElementReaction nearest = null;
        var nearestD = float.MaxValue;
        var sameKind = 0;
        foreach (var r in ElementReaction.Active)
        {
            if (r.First != a || r.Second != b) continue;
            sameKind++;
            var d = (r.Center - at).sqrMagnitude;
            if (d < nearestD)
            {
                nearestD = d;
                nearest = r;
            }
        }
        // Feed the nearest one in reach. With too many of a kind already going, feed it anyway.
        if (nearest != null && (nearestD <= nearest.FeedRadius * nearest.FeedRadius || sameKind >= MaxOfOneKind))
        {
            nearest.Feed(at, flow, amount);
            return;
        }
        if (ElementReaction.Active.Count >= MaxReactions) return;

        var go = new GameObject($"Reaction_{a}_{b}");
        ElementReaction reaction = (a, b) switch
        {
            (Element.Fire, Element.Ice) => go.AddComponent<SteamExplosion>(),
            (Element.Fire, Element.Lightning) => go.AddComponent<PlasmaBurst>(),
            (Element.Fire, Element.Wind) => go.AddComponent<FireVortex>(),
            (Element.Ice, Element.Lightning) => go.AddComponent<ChargedFrost>(),
            (Element.Ice, Element.Wind) => go.AddComponent<Blizzard>(),
            _ => go.AddComponent<StormFunnel>(),
        };
        reaction.Begin(a, b, at, flow, amount);
    }
}

/// <summary>Area effects shared by the reactions.</summary>
public static class ReactionArea
{
    static readonly Collider2D[] overlaps = new Collider2D[48];
    static readonly HashSet<IElementReactive> touched = new();
    static readonly HashSet<Rigidbody2D> pushed = new();

    /// <summary>Calls act for every living character within radius, with how close they are (1 at the center).</summary>
    public static void Characters(Vector2 center, float radius, System.Action<ElementStatus, float> act)
    {
        for (var i = ElementStatus.All.Count - 1; i >= 0; i--)
        {
            var s = ElementStatus.All[i];
            if (s == null || s.IsDead) continue;
            var d = Vector2.Distance(s.Center, center);
            if (d > radius) continue;
            act(s, 1f - d / radius);
        }
    }

    /// <summary>Throws characters and loose objects away from center.</summary>
    public static void Blast(Vector2 center, float radius, float speed)
    {
        Characters(center, radius, (s, k) =>
        {
            var dir = (s.Center - center).normalized;
            if (dir.sqrMagnitude < 0.01f) dir = Vector2.up;
            dir = (dir + Vector2.up * 0.4f).normalized;
            // The wizard is usually right beside his own reactions: rock him, don't hurl him off a ledge.
            s.AddPush(dir * speed * (0.4f + 0.6f * k) * (s.IsPlayer ? 0.35f : 0.6f));
        });

        pushed.Clear();
        var n = Physics2D.OverlapCircle(center, radius, new ContactFilter2D { useTriggers = true }, overlaps);
        for (var i = 0; i < n; i++)
        {
            var body = overlaps[i].attachedRigidbody;
            if (body == null || body.bodyType != RigidbodyType2D.Dynamic || !pushed.Add(body)) continue;
            if (body.GetComponent<ElementStatus>() != null) continue;
            var dir = ((Vector2)body.position - center).normalized + Vector2.up * 0.4f;
            body.AddForce(dir.normalized * speed * body.mass, ForceMode2D.Impulse);
        }
    }

    /// <summary>Lets every reactive prop within radius feel the element.</summary>
    public static void Props(Vector2 center, float radius, Element element, float power)
    {
        touched.Clear();
        var n = Physics2D.OverlapCircle(center, radius, new ContactFilter2D { useTriggers = true }, overlaps);
        for (var i = 0; i < n; i++)
        {
            var reactive = overlaps[i].GetComponentInParent<IElementReactive>();
            if (reactive == null || !touched.Add(reactive)) continue;
            var p = overlaps[i].ClosestPoint(center);
            reactive.OnElement(element, power, p, (p - center).normalized);
        }
    }

    /// <summary>The nearest living skeleton within range of a point, or null.</summary>
    public static ElementStatus NearestEnemy(Vector2 from, float range, ElementStatus except = null)
    {
        ElementStatus best = null;
        var bestD = range;
        foreach (var s in ElementStatus.All)
        {
            if (s == null || s.IsDead || s.IsPlayer || s == except) continue;
            var d = Vector2.Distance(s.Center, from);
            if (d >= bestD) continue;
            bestD = d;
            best = s;
        }
        return best;
    }

    /// <summary>Plasma: fire that has taken on the color of lightning.</summary>
    public static readonly Color PlasmaWhite = Elements.Rgb(252, 238, 255);
    public static readonly Color PlasmaPink = Elements.Rgb(255, 122, 226);
    public static readonly Color PlasmaViolet = Elements.Rgb(168, 92, 255);
    public static readonly Color PlasmaDeep = Elements.Rgb(76, 28, 150);
}
