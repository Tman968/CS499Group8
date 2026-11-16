using System.Collections.Generic;
using UnityEngine;

/// <summary>A packet of an element in flight: a lick of flame, an ice shard, a gust.</summary>
public struct Mote
{
    public Element element;
    public Vector2 pos, vel;
    public float age, life;
    public float radius, radiusEnd;
    /// <summary>Fraction of velocity lost per second.</summary>
    public float drag;
    /// <summary>Downward acceleration; negative rises.</summary>
    public float gravity;
    public float power;
    /// <summary>The WandStream that made it.</summary>
    public int stream;
    /// <summary>The caster's body, which the mote never hits.</summary>
    public Rigidbody2D ignore;
    public float seed;
    public bool dead;

    public float Radius => Mathf.Lerp(radius, radiusEnd, age / life);
}

/// <summary>
/// Simulates every mote in flight: moves them (drag, gravity, the air), sends them into skeletons,
/// props and the world, and finds where streams of different elements meet in midair, which is
/// where reactions happen. Lightning has no motes; its bolts ask ProbeSegment whether they cross
/// another stream.
/// </summary>
[DefaultExecutionOrder(500)]
public class ElementWorld : MonoBehaviour
{
    const int MaxMotes = 1400;
    const float HashCell = 0.5f;

    static ElementWorld instance;

    readonly Mote[] motes = new Mote[MaxMotes];
    int count;

    readonly Dictionary<long, int> head = new();
    readonly int[] next = new int[MaxMotes];
    readonly RaycastHit2D[] hits = new RaycastHit2D[12];
    ContactFilter2D filter;

    struct Veil
    {
        public Vector2 center;
        public float radius, expires;
    }

    static readonly List<Veil> veils = new();

    public static ElementWorld Instance
    {
        get
        {
            if (instance == null && Application.isPlaying) new GameObject("ElementWorld").AddComponent<ElementWorld>();
            return instance;
        }
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        filter = new ContactFilter2D { useTriggers = false };
    }

    void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
            veils.Clear();
        }
    }

    public static void Spawn(Mote m)
    {
        var self = Instance;
        if (self == null || self.count >= MaxMotes) return;
        m.seed = Random.value;
        m.dead = false;
        self.motes[self.count++] = m;
    }

    // ------------------------------------------------------------------ veils (things that block sight)

    /// <summary>Marks a circle the skeletons can't see through this frame (steam, blizzards).</summary>
    public static void AddVeil(Vector2 center, float radius)
    {
        veils.Add(new Veil { center = center, radius = radius, expires = Time.time + 0.1f });
    }

    /// <summary>True if a cloud lies between a and b.</summary>
    public static bool SightBlocked(Vector2 a, Vector2 b)
    {
        veils.RemoveAll(v => v.expires < Time.time);
        foreach (var v in veils)
        {
            if (DistanceToSegment(v.center, a, b) < v.radius) return true;
        }
        return false;
    }

    public static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        var t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
        return Vector2.Distance(p, a + ab * t);
    }

    // ------------------------------------------------------------------ lightning crossings

    /// <summary>
    /// Finds the first mote of another element that a bolt from a to b passes through. The mote is
    /// used up. Returns false if the bolt crosses nothing.
    /// </summary>
    public static bool ProbeSegment(Vector2 a, Vector2 b, Element bolt, out Vector2 point, out Element other, out Vector2 otherVelocity)
    {
        point = default;
        other = bolt;
        otherVelocity = default;
        var self = instance;
        if (self == null) return false;
        var best = -1;
        var bestT = float.MaxValue;
        var ab = b - a;
        var len2 = Mathf.Max(1e-6f, ab.sqrMagnitude);
        for (var i = 0; i < self.count; i++)
        {
            ref var m = ref self.motes[i];
            if (m.dead || m.element == bolt) continue;
            var t = Mathf.Clamp01(Vector2.Dot(m.pos - a, ab) / len2);
            var closest = a + ab * t;
            var r = m.Radius + 0.2f;
            if ((m.pos - closest).sqrMagnitude > r * r || t >= bestT) continue;
            bestT = t;
            best = i;
        }
        if (best < 0) return false;
        ref var hit = ref self.motes[best];
        point = hit.pos;
        other = hit.element;
        otherVelocity = hit.vel;
        hit.dead = true;
        return true;
    }

    // ------------------------------------------------------------------ simulation

    void Update()
    {
        var dt = Mathf.Min(Time.deltaTime, 0.04f);
        if (dt <= 0f) return;

        for (var i = 0; i < count; i++)
        {
            ref var m = ref motes[i];
            if (m.dead) continue;
            m.age += dt;
            if (m.age >= m.life || m.pos.y < -20f)
            {
                Expire(ref m);
                continue;
            }
            Move(ref m, dt);
        }

        FindReactions();

        // Draw what is left, and compact the list.
        var w = 0;
        for (var i = 0; i < count; i++)
        {
            ref var m = ref motes[i];
            if (m.dead) continue;
            Draw(ref m);
            if (w != i) motes[w] = m;
            w++;
        }
        count = w;
    }

    void Move(ref Mote m, float dt)
    {
        var v = m.vel;
        v.y -= m.gravity * dt;
        if (m.element != Element.Wind)
        {
            var airFactor = m.element == Element.Fire ? 0.55f : 0.3f;
            v += AirField.At(m.pos) * (airFactor * dt);
        }
        if (m.drag > 0f) v *= Mathf.Max(0f, 1f - m.drag * dt);
        m.vel = v;
        var to = m.pos + v * dt;

        var n = Physics2D.Linecast(m.pos, to, filter, hits);
        if (n > 1) System.Array.Sort(hits, 0, n, HitDistance.Instance);
        for (var h = 0; h < n; h++)
        {
            var hit = hits[h];
            var body = hit.rigidbody;
            if (body != null && body == m.ignore) continue;

            var status = body != null ? body.GetComponent<ElementStatus>() : null;
            if (status != null)
            {
                // Streams never hurt the wizards; they pass through, and so do corpses.
                if (status.IsPlayer || status.IsDead) continue;
                if (m.element == Element.Wind) continue;
                HitCharacter(ref m, status, hit);
                return;
            }

            var reactive = hit.collider.GetComponentInParent<IElementReactive>();
            if (reactive != null)
            {
                reactive.OnElement(m.element, m.power, hit.point, v.normalized);
                if (reactive.BlocksMotes)
                {
                    HitWorld(ref m, hit, false);
                    return;
                }
                continue;
            }

            if (SurfaceGrid.IsStatic(hit.collider))
            {
                HitWorld(ref m, hit, true);
                return;
            }
            // Loose objects (dropped items, thrown bones) don't stop a stream.
        }
        m.pos = to;
    }

    static void HitCharacter(ref Mote m, ElementStatus status, RaycastHit2D hit)
    {
        var dir = m.vel.normalized;
        switch (m.element)
        {
            case Element.Fire:
                status.Damage(0.07f * m.power, Element.Fire, dir, false);
                status.Ignite(2.5f, 0.55f);
                ElementFx.FireSplash(hit.point, hit.normal);
                Sfx.Play("fire_hit", hit.point);
                break;
            case Element.Ice:
                status.Damage(0.3f * m.power, Element.Ice, dir);
                status.ApplyChill(0.22f * m.power);
                ElementFx.IceImpact(hit.point, hit.normal);
                Sfx.Play("ice_hit", hit.point, 1.2f, 0.85f);
                break;
        }
        m.dead = true;
    }

    static void HitWorld(ref Mote m, RaycastHit2D hit, bool surface)
    {
        m.dead = true;
        switch (m.element)
        {
            case Element.Fire:
                if (surface) SurfaceGrid.ApplyFire(hit, m.power * 0.35f);
                ElementFx.FireSplash(hit.point, hit.normal);
                Sfx.Play("fire_hit", hit.point, 0.7f);
                break;
            case Element.Ice:
                if (surface) SurfaceGrid.ApplyIce(hit, 0.36f * m.power);
                ElementFx.IceImpact(hit.point, hit.normal);
                Sfx.Play("ice_hit", hit.point);
                if (surface) Sfx.Play("frost_form", hit.point);
                break;
            case Element.Wind:
                if (Random.value < 0.3f) ElementFx.WindPuff(hit.point, hit.normal, m.vel);
                break;
        }
    }

    static void Expire(ref Mote m)
    {
        m.dead = true;
        if (m.element == Element.Fire)
        {
            ElementFx.EmberLick(m.pos, 1f);
            if (Random.value < 0.4f) ElementFx.Smoke(m.pos, 1f);
        }
    }

    static void Draw(ref Mote m)
    {
        var t = m.age / m.life;
        switch (m.element)
        {
            case Element.Fire:
                ElementFx.FireMote(m.pos, m.vel, t, m.power);
                break;
            case Element.Ice:
                ElementFx.IceShard(m.pos, m.vel);
                break;
            case Element.Wind:
                ElementFx.WindMote(m.pos, m.vel, m.power * (1f - t * t), m.seed);
                break;
        }
    }

    // Streams of different elements that touch in midair react, using up both motes.
    void FindReactions()
    {
        head.Clear();
        for (var i = 0; i < count; i++)
        {
            if (motes[i].dead) continue;
            var key = CellKey(motes[i].pos);
            next[i] = head.TryGetValue(key, out var first) ? first : -1;
            head[key] = i;
        }

        for (var i = 0; i < count; i++)
        {
            ref var a = ref motes[i];
            if (a.dead) continue;
            var cx = Mathf.FloorToInt(a.pos.x / HashCell);
            var cy = Mathf.FloorToInt(a.pos.y / HashCell);
            var ra = a.Radius;
            for (var dx = -1; dx <= 1 && !a.dead; dx++)
            {
                for (var dy = -1; dy <= 1 && !a.dead; dy++)
                {
                    if (!head.TryGetValue(Key(cx + dx, cy + dy), out var j)) continue;
                    for (; j >= 0; j = next[j])
                    {
                        if (j <= i) continue;
                        ref var b = ref motes[j];
                        if (b.dead || b.element == a.element) continue;
                        var r = ra + b.Radius;
                        if ((a.pos - b.pos).sqrMagnitude > r * r) continue;
                        var at = (a.pos * a.power + b.pos * b.power) / Mathf.Max(0.01f, a.power + b.power);
                        ElementReactions.Trigger(a.element, b.element, at, a.vel, b.vel, Mathf.Min(a.power, b.power) + 0.3f);
                        a.dead = true;
                        b.dead = true;
                        break;
                    }
                }
            }
        }
    }

    static long Key(int x, int y) => ((long)x << 32) ^ (uint)y;

    static long CellKey(Vector2 p) => Key(Mathf.FloorToInt(p.x / HashCell), Mathf.FloorToInt(p.y / HashCell));

    sealed class HitDistance : IComparer<RaycastHit2D>
    {
        public static readonly HitDistance Instance = new();
        public int Compare(RaycastHit2D a, RaycastHit2D b) => a.fraction.CompareTo(b.fraction);
    }
}
