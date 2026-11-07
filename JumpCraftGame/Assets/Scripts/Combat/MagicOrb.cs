using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// A glowing orb fired from the wizard's weapon. It flies straight, sheds sparks, lights up its
/// surroundings, and bursts on the first skeleton or piece of ground it touches.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class MagicOrb : MonoBehaviour
{
    public struct Style
    {
        public Sprite[] frames;
        public Sprite[] burst;
        public Sprite[] spark;
        public Color glow;
    }

    private const int SortingOrder = 15;

    private Style style;
    private int damage;
    private Rigidbody2D owner;
    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private Light2D glowLight;
    private float age;
    private float lifetime;
    private float sparkTimer;
    private bool spent;

    public static MagicOrb Fire(Style style, Vector3 position, Vector2 velocity, int damage, float lifetime, Rigidbody2D owner)
    {
        var go = new GameObject("MagicOrb");
        go.transform.position = position;

        var body = go.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.linearVelocity = velocity;

        var circle = go.AddComponent<CircleCollider2D>();
        circle.isTrigger = true;
        circle.radius = 0.2f;

        var orb = go.AddComponent<MagicOrb>();
        orb.style = style;
        orb.damage = damage;
        orb.owner = owner;
        orb.lifetime = lifetime;
        orb.rb = body;
        orb.spriteRenderer = go.AddComponent<SpriteRenderer>();
        orb.spriteRenderer.sortingOrder = SortingOrder;
        if (style.frames.Length > 0) orb.spriteRenderer.sprite = style.frames[0];

        orb.glowLight = go.AddComponent<Light2D>();
        orb.glowLight.lightType = Light2D.LightType.Point;
        orb.glowLight.color = style.glow;
        orb.glowLight.intensity = 1.6f;
        orb.glowLight.pointLightInnerRadius = 0.1f;
        orb.glowLight.pointLightOuterRadius = 1.8f;

        // A flash at the weapon as the orb leaves it.
        if (style.burst.Length > 0)
            SpriteFx.Spawn(new[] { style.burst[0] }, position, 1f, 0.08f, SortingOrder + 1, Color.white).FadingFrom(0f);
        return orb;
    }

    void Update()
    {
        age += Time.deltaTime;
        if (age >= lifetime)
        {
            Burst(false);
            return;
        }

        if (style.frames.Length > 0)
            spriteRenderer.sprite = style.frames[(int)(age * 14f) % style.frames.Length];
        // The glow flickers like a live flame.
        glowLight.intensity = 1.6f + 0.35f * Mathf.Sin(age * 37f) * Mathf.Sin(age * 13f);

        // Shed sparks that drift off behind the orb.
        sparkTimer -= Time.deltaTime;
        if (sparkTimer <= 0f && style.spark.Length > 0)
        {
            sparkTimer = 0.025f;
            var drift = -rb.linearVelocity * 0.08f + Random.insideUnitCircle * 0.6f;
            SpriteFx.Spawn(style.spark, transform.position + (Vector3)(Random.insideUnitCircle * 0.12f),
                           1f, Random.Range(0.18f, 0.32f), SortingOrder - 1, style.glow)
                .Moving(drift).FadingFrom(0.2f);
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (spent || other.isTrigger) return;
        if (owner != null && other.attachedRigidbody == owner) return;

        var skeleton = other.GetComponentInParent<Skeleton>();
        if (skeleton != null)
        {
            if (skeleton.IsDead) return;
            skeleton.TakeHit(damage, rb.linearVelocity);
            Burst(true);
            return;
        }

        // Fly through dropped items and other loose physics objects; burst on solid ground.
        var body = other.attachedRigidbody;
        if (body == null || body.bodyType == RigidbodyType2D.Static) Burst(true);
    }

    void Burst(bool hit)
    {
        if (spent) return;
        spent = true;
        if (style.burst.Length > 0)
            SpriteFx.Spawn(style.burst, transform.position, 22f, hit ? 0.24f : 0.16f, SortingOrder, Color.white).FadingFrom(0.6f);
        if (hit)
        {
            for (var i = 0; i < 6; i++)
            {
                var v = Random.insideUnitCircle.normalized * Random.Range(2f, 4.5f);
                SpriteFx.Spawn(style.spark, transform.position, 1f, Random.Range(0.25f, 0.45f), SortingOrder, style.glow)
                    .Moving(v, 6f).FadingFrom(0.3f);
            }
        }
        Destroy(gameObject);
    }
}
