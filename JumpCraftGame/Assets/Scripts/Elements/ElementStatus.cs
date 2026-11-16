using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Elemental effects on a character (a skeleton or the player): burning, chilled and frozen
/// solid, shocked, soaked, and blown about by the air. It also reacts to what the character is
/// standing on: embers burn, puddles soak, charged surfaces shock.
///
/// Damage is in skeleton health units (a skeleton has 6); the player's PlayerHealth is on a
/// 100 scale, so damage to the player is multiplied by playerDamageScale.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class ElementStatus : MonoBehaviour
{
    public static readonly List<ElementStatus> All = new();

    [Tooltip("How strongly wind and vortexes move this body. Heavy skeletons resist more.")]
    public float airResponse = 1f;
    [Tooltip("Player only: multiplies elemental damage onto PlayerHealth's 100 point scale.")]
    public float playerDamageScale = 12f;
    [Tooltip("Players can be chilled and slowed but never frozen solid.")]
    public bool canFreeze = true;
    public float freezeSeconds = 3f;

    public bool IsPlayer { get; private set; }
    public bool IsFrozen => frozenTimer > 0f;
    public bool IsStunned => stunTimer > 0f || IsFrozen;
    public bool IsBurning => burnTimer > 0f;
    public bool IsWet => wetTimer > 0f;
    public float Chill => chill;
    /// <summary>Sideways drift from wind and blasts, which dies away. Controllers add it to their walking speed.</summary>
    public float Drift => push.x;
    public Rigidbody2D Body => rb;
    public int OwnerId => rb != null ? rb.GetHashCode() : 0;
    public bool IsDead => (skeleton != null && skeleton.IsDead) || (playerHealth != null && playerHealth.IsDead);

    /// <summary>Multiplier on walking speed: chill slows, frozen and stunned stop.</summary>
    public float MoveMultiplier => IsStunned ? 0f : 1f - 0.6f * chill;

    public Vector2 Center => bodyCollider != null ? (Vector2)bodyCollider.bounds.center : (Vector2)transform.position;
    public Bounds Bounds => bodyCollider != null ? bodyCollider.bounds : new Bounds(transform.position, Vector3.one);
    public Vector2 Feet
    {
        get
        {
            var b = Bounds;
            return new Vector2(b.center.x, b.min.y);
        }
    }

    Rigidbody2D rb;
    Collider2D bodyCollider;
    Skeleton skeleton;
    SkeletonAnimator skeletonAnimator;
    PlayerHealth playerHealth;
    WizardAnimator wizard;
    SpriteRenderer bodyRenderer;

    float burnTimer, burnDps;
    float chill;
    float frozenTimer;
    float stunTimer;
    float wetTimer;
    float surfaceTimer;
    float arcTimer;
    Vector2 push;
    float lift;
    readonly Vector2[] arc = new Vector2[8];

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();
        skeleton = GetComponent<Skeleton>();
        skeletonAnimator = GetComponent<SkeletonAnimator>();
        playerHealth = GetComponent<PlayerHealth>();
        wizard = GetComponent<WizardAnimator>();
        IsPlayer = playerHealth != null;
        if (IsPlayer) canFreeze = false;
        if (skeletonAnimator != null) bodyRenderer = skeletonAnimator.body;
        if (bodyRenderer == null && wizard == null) bodyRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    // ------------------------------------------------------------------ effects

    /// <summary>Hurts the character. dir is the direction the hit came in, for knockback.</summary>
    public void Damage(float amount, Element element, Vector2 dir, bool flinch = true)
    {
        if (amount <= 0f || IsDead) return;
        if (skeleton != null) skeleton.TakeElementalDamage(amount, dir, flinch);
        else if (playerHealth != null) playerHealth.TakeDamage(amount * playerDamageScale);
    }

    /// <summary>Sets the character alight for seconds, taking dps damage per second.</summary>
    public void Ignite(float seconds, float dps)
    {
        if (IsDead) return;
        // Fire thaws ice before it can burn.
        if (IsFrozen)
        {
            Thaw();
            return;
        }
        if (IsWet)
        {
            wetTimer = Mathf.Max(0f, wetTimer - seconds);
            SteamPuff(Center, 3);
            return;
        }
        chill = Mathf.Max(0f, chill - 0.3f);
        burnTimer = Mathf.Max(burnTimer, seconds);
        burnDps = Mathf.Max(burnDps, dps);
    }

    /// <summary>Adds chill (1 is enough to freeze solid).</summary>
    public void ApplyChill(float amount)
    {
        if (IsDead || IsFrozen) return;
        if (IsBurning)
        {
            burnTimer = Mathf.Max(0f, burnTimer - amount * 4f);
            return;
        }
        chill = Mathf.Min(canFreeze ? 1f : 0.75f, chill + amount * (IsWet ? 1.6f : 1f));
        if (canFreeze && chill >= 1f) Freeze(freezeSeconds);
    }

    public void Freeze(float seconds)
    {
        if (!canFreeze || IsDead) return;
        frozenTimer = Mathf.Max(frozenTimer, seconds);
        burnTimer = 0f;
        chill = 0f;
        if (skeletonAnimator != null) skeletonAnimator.enabled = false;
        ElementLights.Burst(Center, Elements.Glow(Element.Ice), 2.5f, 1.2f, 0.4f);
        for (var i = 0; i < 14; i++)
        {
            var b = Bounds;
            var p = new Vector2(Random.Range(b.min.x, b.max.x), Random.Range(b.min.y, b.max.y));
            ArcaneParticles.Emit(FxLayer.Glow, new Spark
            {
                pos = p, vel = Random.insideUnitCircle * 1.5f, life = Random.Range(0.3f, 0.6f), size = 1, sizeEnd = 1,
                colorStart = Elements.IceWhite, colorMid = Elements.IcePale, colorEnd = Elements.IceCyan.WithAlpha(0f),
                drag = 3f, flicker = 0.4f,
            });
        }
    }

    /// <summary>Stuns the character for seconds.</summary>
    public void Shock(float seconds)
    {
        if (IsDead) return;
        stunTimer = Mathf.Max(stunTimer, seconds);
    }

    public void Soak(float seconds)
    {
        wetTimer = Mathf.Max(wetTimer, seconds);
        if (IsBurning)
        {
            burnTimer = 0f;
            SteamPuff(Center, 5);
        }
    }

    /// <summary>
    /// Adds an instant change of velocity (a blast, a gust, a wand's recoil). The sideways part
    /// becomes Drift, which dies away; the vertical part waits in ConsumeLift for the controller.
    /// </summary>
    public void AddPush(Vector2 dv)
    {
        push.x += dv.x;
        lift += dv.y;
    }

    /// <summary>Bleeds off drift, as ground friction does. rate is the fraction lost per second.</summary>
    public void DampDrift(float rate, float dt) => push.x *= Mathf.Max(0f, 1f - rate * dt);

    /// <summary>The vertical velocity change gathered since the last call, from the air and from pushes.</summary>
    public float ConsumeLift()
    {
        var l = lift;
        lift = 0f;
        return l;
    }

    /// <summary>Breaks the character out of the ice with a burst of shards.</summary>
    public void Shatter()
    {
        if (!IsFrozen) return;
        frozenTimer = 0f;
        if (skeletonAnimator != null) skeletonAnimator.enabled = true;
        ElementFx.IceShatter(Center, 22, Vector2.up * 2f);
    }

    void Thaw()
    {
        frozenTimer = 0f;
        if (skeletonAnimator != null) skeletonAnimator.enabled = true;
        SteamPuff(Center, 8);
        Soak(3f);
    }

    /// <summary>How much more (or less) this element hurts given the character's state.</summary>
    public float Vulnerability(Element element)
    {
        if (element == Element.Lightning)
        {
            var k = 1f;
            if (IsWet) k *= 1.75f;
            if (IsFrozen) k *= 1.5f;
            return k;
        }
        return 1f;
    }

    // ------------------------------------------------------------------ update

    void FixedUpdate()
    {
        var dt = Time.fixedDeltaTime;
        var air = AirField.At(Center, OwnerId) * airResponse;
        push.x += air.x * dt;
        lift += air.y * dt;
        // Frozen characters are blocks of ice: they don't drift on the wind.
        if (IsFrozen) push.x = 0f;
        push.x *= Mathf.Max(0f, 1f - 2.6f * dt);
        if (Mathf.Abs(push.x) < 0.02f) push.x = 0f;
    }

    void Update()
    {
        var dt = Time.deltaTime;
        if (IsDead)
        {
            burnTimer = 0f;
            frozenTimer = 0f;
            stunTimer = 0f;
            return;
        }

        if (frozenTimer > 0f)
        {
            frozenTimer -= dt;
            if (frozenTimer <= 0f)
            {
                frozenTimer = 0f;
                if (skeletonAnimator != null) skeletonAnimator.enabled = true;
                ElementFx.IceShatter(Center, 16, Vector2.up);
            }
        }
        stunTimer = Mathf.Max(0f, stunTimer - dt);
        wetTimer = Mathf.Max(0f, wetTimer - dt);
        if (!IsFrozen) chill = Mathf.Max(0f, chill - 0.22f * dt);

        if (burnTimer > 0f)
        {
            burnTimer -= dt;
            Damage(burnDps * dt, Element.Fire, Vector2.zero, false);
        }

        surfaceTimer -= dt;
        if (surfaceTimer <= 0f)
        {
            surfaceTimer = 0.15f;
            ReactToSurface();
        }
    }

    void ReactToSurface()
    {
        var info = SurfaceGrid.Probe(Feet);
        if (!info.found) return;
        switch (info.coat)
        {
            case SurfaceCoat.Embers:
                Ignite(1.2f, IsPlayer ? 0.2f : 0.8f);
                break;
            case SurfaceCoat.Water:
                Soak(2.5f);
                break;
        }
        if (info.charged)
        {
            Shock(IsPlayer ? 0.25f : 0.6f);
            Damage((IsPlayer ? 0.25f : 0.5f) * Vulnerability(Element.Lightning), Element.Lightning, Vector2.up, !IsPlayer);
            arcTimer = 0f;
        }
    }

    void LateUpdate()
    {
        var b = Bounds;
        var center = (Vector2)b.center;

        // Tint the body to show its state.
        var tint = Color.white;
        if (chill > 0f) tint = Color.Lerp(tint, new Color(0.62f, 0.85f, 1f), chill);
        if (IsFrozen) tint = new Color(0.66f, 0.9f, 1f);
        if (IsBurning && (Time.time * 14f) % 2f < 1f) tint *= new Color(1f, 0.72f, 0.55f);
        if (stunTimer > 0f && (Time.time * 30f) % 2f < 1f) tint = new Color(0.85f, 0.8f, 1.4f);
        if (wizard != null) wizard.statusTint = tint;
        else if (bodyRenderer != null && !IsDead) bodyRenderer.color = tint;

        if (IsDead) return;

        if (IsBurning) ElementFx.BodyFlames(b, burnDps);
        if (IsFrozen) ElementFx.IceBlock(b, frozenTimer);
        if (chill > 0.15f && !IsFrozen && Random.value < chill * 0.4f)
        {
            ArcaneParticles.Emit(FxLayer.Front, new Spark
            {
                pos = new Vector2(Random.Range(b.min.x, b.max.x), Random.Range(b.min.y, b.max.y)),
                vel = new Vector2(0f, -0.4f), life = 0.6f, size = 1, sizeEnd = 1,
                colorStart = Elements.IceWhite, colorMid = Elements.IcePale, colorEnd = Elements.IcePale.WithAlpha(0f),
                turbulence = 2f,
            });
        }
        if (IsWet && Random.value < 0.12f)
        {
            ArcaneParticles.Emit(FxLayer.Front, new Spark
            {
                pos = new Vector2(Random.Range(b.min.x, b.max.x), Random.Range(center.y, b.max.y)),
                vel = Vector2.zero, life = 0.5f, size = 1, sizeEnd = 1, gravity = 12f,
                colorStart = Elements.WaterShine, colorMid = Elements.WaterMid, colorEnd = Elements.WaterMid.WithAlpha(0f),
            });
        }
        if (stunTimer > 0f)
        {
            arcTimer -= Time.deltaTime;
            if (arcTimer <= 0f)
            {
                arcTimer = 0.05f;
                // A fresh crackle of current crawling over the body.
                var from = new Vector2(Random.Range(b.min.x, b.max.x), Random.Range(b.min.y, b.max.y));
                var to = from + Random.insideUnitCircle.normalized * Random.Range(0.4f, 0.9f);
                LightningBolts.Spawn(from, to, 0.08f, 0.6f, false);
            }
        }
    }

    static void SteamPuff(Vector2 at, int count)
    {
        for (var i = 0; i < count; i++)
        {
            ArcaneParticles.Emit(FxLayer.Veil, new Spark
            {
                pos = at + Random.insideUnitCircle * 0.3f, vel = new Vector2(Random.Range(-0.6f, 0.6f), Random.Range(0.8f, 1.8f)),
                life = Random.Range(0.6f, 1.1f), size = 6, sizeEnd = 16, shape = FxShape.Halo,
                colorStart = Elements.Steam.WithAlpha(0.55f), colorMid = Elements.Steam.WithAlpha(0.3f), colorEnd = Elements.Steam.WithAlpha(0f),
                drag = 1.5f, turbulence = 2f, wind = 0.6f,
            });
        }
    }
}
