using UnityEngine;

/// <summary>
/// A bone thrown by a skeleton. It arcs under gravity, tumbling end over end, hurts the player on
/// contact and shatters on the player or the ground. Wind carries it; blown back hard enough
/// against its throw it is turned, and hurts skeletons instead of the player.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class BoneProjectile : MonoBehaviour
{
    private const int SortingOrder = 14;

    private float damage;
    private Rigidbody2D owner;
    private Sprite[] chips;
    private bool spent;
    private bool deflected;
    private Vector2 thrownDirection;
    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;

    /// <summary>
    /// Works out the launch velocity that lands a projectile on target after flightTime seconds,
    /// under this gravity scale.
    /// </summary>
    public static Vector2 LaunchVelocity(Vector2 from, Vector2 target, float flightTime, float gravityScale)
    {
        var g = Physics2D.gravity * gravityScale;
        return (target - from) / flightTime - 0.5f * g * flightTime;
    }

    public static BoneProjectile Throw(Sprite sprite, Sprite[] chips, Vector3 position, Vector2 velocity,
                                       float damage, float gravityScale, Rigidbody2D owner)
    {
        var go = new GameObject("ThrownBone");
        go.transform.position = position;

        var body = go.AddComponent<Rigidbody2D>();
        body.gravityScale = gravityScale;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.linearVelocity = velocity;
        // Spin end over end, the way it was flung.
        body.angularVelocity = velocity.x >= 0f ? -720f : 720f;

        var box = go.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = new Vector2(0.5f, 0.25f);

        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = SortingOrder;

        var bone = go.AddComponent<BoneProjectile>();
        bone.damage = damage;
        bone.owner = owner;
        bone.chips = chips;
        bone.rb = body;
        bone.spriteRenderer = renderer;
        bone.thrownDirection = velocity.normalized;
        Destroy(go, 6f);
        return bone;
    }

    void FixedUpdate()
    {
        if (spent) return;
        var air = AirField.At(rb.position);
        if (air.sqrMagnitude < 0.01f) return;
        rb.linearVelocity += air * (1.2f * Time.fixedDeltaTime);
        // Blown back against its throw: it now flies for the wizard.
        if (!deflected && Vector2.Dot(rb.linearVelocity, thrownDirection) < 0f)
        {
            deflected = true;
            owner = null;
            spriteRenderer.color = new Color(0.8f, 1f, 0.92f);
            ElementFx.WindPuff(rb.position, Vector2.up, rb.linearVelocity);
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (spent || other.isTrigger) return;
        if (owner != null && other.attachedRigidbody == owner) return;

        if (deflected)
        {
            var skeleton = other.GetComponentInParent<Skeleton>();
            if (skeleton != null)
            {
                if (skeleton.IsDead) return;
                skeleton.TakeElementalDamage(damage / 20f, rb.linearVelocity.normalized, true);
                Shatter();
                return;
            }
            if (other.GetComponentInParent<PlayerHealth>() != null) return;
            var hitBody = other.attachedRigidbody;
            if (hitBody == null || hitBody.bodyType == RigidbodyType2D.Static) Shatter();
            return;
        }

        if (other.GetComponentInParent<Skeleton>() != null) return;

        var health = other.GetComponentInParent<PlayerHealth>();
        if (health != null)
        {
            health.TakeDamage(damage);
            Shatter();
            return;
        }

        var body = other.attachedRigidbody;
        if (body == null || body.bodyType == RigidbodyType2D.Static) Shatter();
    }

    void Shatter()
    {
        spent = true;
        for (var i = 0; i < 5 && chips.Length > 0; i++)
        {
            var v = new Vector2(Random.Range(-2.5f, 2.5f), Random.Range(1.5f, 4.5f));
            SpriteFx.Spawn(new[] { chips[i % chips.Length] }, transform.position, 1f, Random.Range(0.4f, 0.7f),
                           SortingOrder, Color.white)
                .Moving(v, 18f, Random.Range(-540f, 540f)).FadingFrom(0.6f);
        }
        Destroy(gameObject);
    }
}
