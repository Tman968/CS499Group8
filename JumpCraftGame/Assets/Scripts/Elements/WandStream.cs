using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// A constant stream of one element pouring from a point. The player's wands and the test bench's
/// emitters both pour through this.
///
///   Fire       a short, roaring jet of flame straight out, slowing and rising as it burns out
///   Ice        a spray of shards on true projectile arcs (they fall under gravity like a thrown bone)
///   Lightning  a rapid stream of bolts straight out; they chain between skeletons
///   Wind       a long gust that fans out and weakens with distance
/// </summary>
public class WandStream
{
    // Fire.
    const float FireRate = 70f, FireSpeed = 11f, FireDrag = 2.6f, FireLife = 0.7f, FireSpread = 7f;
    // Ice: gravity matches Physics2D, so shards fly exactly like any other thrown object.
    const float IceRate = 14f, IceSpeed = 13.5f, IceLife = 3f, IceSpread = 2.5f;
    // Lightning.
    const float BoltInterval = 1f / 14f, BoltRange = 8.5f, BoltSpread = 3.5f, BoltDamage = 0.3f;
    const int ChainJumps = 3;
    const float ChainRange = 4.5f;
    // Wind.
    const float WindRate = 46f, WindSpeed = 17f, WindDrag = 0.65f, WindLife = 0.85f, WindSpread = 20f;
    public const float WindRange = 12.5f, WindStrength = 48f;

    static int nextId = 1;
    static readonly RaycastHit2D[] boltHits = new RaycastHit2D[12];
    static readonly List<ElementStatus> chained = new();

    public readonly Element element;
    public readonly int id;
    /// <summary>Power multiplier on everything the stream does.</summary>
    public float power = 1f;

    readonly Rigidbody2D owner;
    readonly int airOwner;
    readonly Light2D light;
    float emit;
    float boltTimer;
    float lightLevel;
    int boltCount;

    /// <param name="owner">The caster's body: never hit by its own stream, never blown by its own wind.</param>
    /// <param name="lightParent">Where to keep the stream's glow light.</param>
    public WandStream(Element element, Rigidbody2D owner, Transform lightParent)
    {
        this.element = element;
        this.owner = owner;
        id = nextId++;
        airOwner = owner != null ? owner.GetHashCode() : -id;
        light = ElementLights.MakeLight(lightParent);
        light.color = Elements.Glow(element);
    }

    /// <summary>Pours the stream for one frame from tip along dir (a unit vector).</summary>
    public void Pour(Vector2 tip, Vector2 dir, float dt)
    {
        var inherited = owner != null ? owner.linearVelocity : Vector2.zero;
        switch (element)
        {
            case Element.Fire:
                Emit(FireRate, dt, frac => new Mote
                {
                    element = Element.Fire, pos = tip + dir * 0.05f,
                    vel = Rotate(dir, Random.Range(-FireSpread, FireSpread)) * FireSpeed * Random.Range(0.9f, 1.1f) + inherited * 0.5f,
                    life = FireLife * Random.Range(0.85f, 1.1f), radius = 0.12f, radiusEnd = 0.42f,
                    drag = FireDrag, gravity = -2.5f, power = power,
                });
                SetLight(tip + dir * 1.4f, 3.2f, 1.5f + 0.4f * Mathf.Sin(Time.time * 37f) * Mathf.Sin(Time.time * 13f), dt);
                break;

            case Element.Ice:
                Emit(IceRate, dt, frac => new Mote
                {
                    element = Element.Ice, pos = tip + dir * 0.1f,
                    vel = Rotate(dir, Random.Range(-IceSpread, IceSpread)) * IceSpeed * Random.Range(0.95f, 1.05f) + inherited * 0.5f,
                    life = IceLife, radius = 0.14f, radiusEnd = 0.14f,
                    drag = 0.04f, gravity = -Physics2D.gravity.y, power = power,
                });
                SetLight(tip, 2f, 0.9f, dt);
                break;

            case Element.Lightning:
                boltTimer -= dt;
                while (boltTimer <= 0f)
                {
                    boltTimer += BoltInterval;
                    FireBolt(tip, dir);
                }
                SetLight(tip + dir * 0.5f, 3f, 0.6f + Random.value * 1.4f, dt);
                break;

            case Element.Wind:
                Emit(WindRate, dt, frac => new Mote
                {
                    element = Element.Wind, pos = tip + dir * 0.1f,
                    vel = Rotate(dir, Random.Range(-WindSpread, WindSpread)) * WindSpeed * Random.Range(0.85f, 1.1f),
                    life = WindLife * Random.Range(0.85f, 1.1f), radius = 0.25f, radiusEnd = 0.65f,
                    drag = WindDrag, gravity = 0f, power = power,
                });
                AirField.AddCone(tip, dir, WindRange, WindSpread + 4f, WindStrength * power, airOwner);
                SetLight(tip, 1.4f, 0.35f, dt);
                break;
        }
        ElementFx.Muzzle(element, tip, dir, 1f);
    }

    /// <summary>Call on frames the stream isn't pouring, to let its light die away.</summary>
    public void Rest(float dt)
    {
        emit = 0f;
        boltTimer = 0f;
        lightLevel = Mathf.MoveTowards(lightLevel, 0f, dt * 8f);
        light.intensity = lightLevel;
        light.enabled = lightLevel > 0.01f;
    }

    public void Destroy()
    {
        if (light != null) Object.Destroy(light.gameObject);
    }

    void SetLight(Vector2 at, float radius, float intensity, float dt)
    {
        lightLevel = Mathf.MoveTowards(lightLevel, intensity, dt * 12f);
        light.enabled = true;
        light.transform.position = at;
        light.pointLightOuterRadius = radius;
        light.intensity = lightLevel;
    }

    // Emits rate motes per second, spacing the ones released in the same frame along their path.
    void Emit(float rate, float dt, System.Func<float, Mote> make)
    {
        emit += rate * dt;
        var n = Mathf.FloorToInt(emit);
        emit -= n;
        for (var i = 0; i < n; i++)
        {
            var m = make((i + 0.5f) / n);
            m.stream = id;
            m.ignore = owner;
            m.pos += m.vel * (dt * (i + 0.5f) / n);
            ElementWorld.Spawn(m);
        }
    }

    public static Vector2 Rotate(Vector2 v, float degrees)
    {
        var a = degrees * Mathf.Deg2Rad;
        var cos = Mathf.Cos(a);
        var sin = Mathf.Sin(a);
        return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
    }

    // ------------------------------------------------------------------ lightning

    void FireBolt(Vector2 tip, Vector2 dir)
    {
        boltCount++;
        var aim = Rotate(dir, Random.Range(-BoltSpread, BoltSpread));
        var end = tip + aim * BoltRange;
        ElementStatus target = null;
        IElementReactive prop = null;
        RaycastHit2D worldHit = default;
        var struck = false;

        var filter = new ContactFilter2D { useTriggers = false };
        var n = Physics2D.CircleCast(tip, 0.16f, aim, filter, boltHits, BoltRange);
        var best = float.MaxValue;
        for (var i = 0; i < n; i++)
        {
            var hit = boltHits[i];
            if (hit.distance >= best) continue;
            var body = hit.rigidbody;
            if (body != null && body == owner) continue;
            var status = body != null ? body.GetComponent<ElementStatus>() : null;
            if (status != null)
            {
                if (status.IsPlayer || status.IsDead) continue;
                best = hit.distance;
                target = status;
                prop = null;
                worldHit = hit;
                struck = true;
                continue;
            }
            var reactive = hit.collider.GetComponentInParent<IElementReactive>();
            if (reactive != null || SurfaceGrid.IsStatic(hit.collider))
            {
                best = hit.distance;
                target = null;
                prop = reactive;
                worldHit = hit;
                struck = true;
            }
        }
        if (struck) end = target != null ? target.Center : worldHit.point;

        // Crossing another stream in midair: the bolt ends there and the two react.
        if (ElementWorld.ProbeSegment(tip, end, Element.Lightning, out var crossing, out var other, out var otherVelocity))
        {
            LightningBolts.Spawn(tip, crossing, 0.09f, 1f, Random.value < 0.4f);
            ElementReactions.Trigger(Element.Lightning, other, crossing, aim * 20f, otherVelocity, power);
            return;
        }

        LightningBolts.Spawn(tip, end, 0.09f, 1f, Random.value < 0.5f);
        if (!struck)
        {
            // Spent in the air: a little crackle at the end of its reach.
            if (Random.value < 0.3f) ElementFx.BoltImpact(end, -aim, 0.4f);
            return;
        }

        if (target != null)
        {
            Strike(target, aim, BoltDamage * power);
            Chain(target);
        }
        else
        {
            if (prop != null) prop.OnElement(Element.Lightning, power, worldHit.point, aim);
            else SurfaceGrid.Electrify(worldHit.point, worldHit.collider);
            ElementFx.BoltImpact(worldHit.point, worldHit.normal, 1f);
        }
        if (boltCount % 3 == 0) ElementLights.Burst(end, Elements.Glow(Element.Lightning), 2.2f, 1.4f, 0.12f);
    }

    static void Strike(ElementStatus target, Vector2 dir, float damage)
    {
        var k = target.Vulnerability(Element.Lightning);
        if (target.IsFrozen)
        {
            // Lightning shatters a frozen skeleton out of its ice, painfully.
            target.Shatter();
            k += 2.5f;
            CameraShake.Add(0.15f);
        }
        target.Damage(damage * k, Element.Lightning, dir);
        target.Shock(0.18f);
        ElementFx.BoltImpact(target.Center, -dir, 1f);
    }

    // The bolt leaps on from its target to the nearest skeletons it hasn't hit yet.
    void Chain(ElementStatus first)
    {
        chained.Clear();
        chained.Add(first);
        var from = first;
        var damage = BoltDamage * power;
        for (var j = 0; j < ChainJumps; j++)
        {
            ElementStatus next = null;
            var bestD = ChainRange;
            foreach (var s in ElementStatus.All)
            {
                if (s == null || s.IsDead || s.IsPlayer || chained.Contains(s)) continue;
                var d = Vector2.Distance(s.Center, from.Center);
                if (d >= bestD) continue;
                bestD = d;
                next = s;
            }
            if (next == null) return;
            damage *= 0.75f;
            LightningBolts.Spawn(from.Center, next.Center, 0.1f, 1f, false);
            Strike(next, (next.Center - from.Center).normalized, damage);
            chained.Add(next);
            from = next;
        }
    }
}
