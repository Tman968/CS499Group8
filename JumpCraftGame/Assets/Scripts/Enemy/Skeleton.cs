using UnityEngine;

/// <summary>
/// A skeleton that marches toward the player without stepping off its platform, and hurls bones
/// that lead the player's movement. Wand orbs wound it and knock it back; when its health runs
/// out it collapses and drops its items.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class Skeleton : MonoBehaviour
{
    // Where the bone leaves the throwing hand, in frame pixels from the top-left. Matches
    // THROW_RELEASE_HAND in Tools/generate_combat_sprites.py.
    static readonly Vector2 ThrowReleasePixel = new(32.4f, 21.9f);

    [Header("Item Drop 1 Settings")]
    public GameObject itemDropPrefab1;
    [Range(0f, 100f)]
    public float dropChance1 = 75f;
    public int minAmount1 = 1;
    public int maxAmount1 = 1;

    [Header("Item Drop 2 Settings")]
    public GameObject itemDropPrefab2;
    [Range(0f, 100f)]
    public float dropChance2 = 20f;
    public int minAmount2 = 1;
    public int maxAmount2 = 1;

    [Header("Spawn Settings")]
    [Tooltip("Offset from the skeleton's center where items will spawn.")]
    public Vector3 spawnOffset = new Vector3(0f, -0.5f, 0f);
    [Tooltip("Slight random spread so items don't spawn in the exact same pixel.")]
    public float dropSpread = 0.3f;

    [Header("Health")]
    [Tooltip("The default staff's orbs do 2 (3 hits), the skull wand's do 3 (2 hits).")]
    public int maxHealth = 6;
    [Tooltip("How far a hit knocks the skeleton back, in world units (about a quarter of a platform).")]
    public float knockbackDistance = 1f;
    [Tooltip("How long the knockback slide lasts, in seconds.")]
    public float knockbackTime = 0.16f;

    [Header("Movement")]
    public float walkSpeed = 1.6f;
    [Tooltip("How close (horizontally) the player must be before the skeleton notices them.")]
    public float noticeRange = 14f;
    [Tooltip("The skeleton stops advancing once this close to the player.")]
    public float keepDistance = 3f;
    [Tooltip("How far past its feet the skeleton checks for ground before stepping.")]
    public float ledgeLookAhead = 0.15f;
    [Tooltip("Heavy enough that the player can't shove the skeleton by walking into it.")]
    public float mass = 60f;

    [Header("Bone Throw")]
    public Sprite boneSprite;
    public Texture2D boneChips;
    [Tooltip("Damage per bone. The player has 100 health, so 34 kills in three hits.")]
    public float boneDamage = 34f;
    public float throwRange = 11f;
    public float throwCooldown = 2.4f;
    [Tooltip("Bone speed used to pick the flight time; the arc is then solved to hit exactly.")]
    public float boneSpeed = 11f;
    public float boneGravityScale = 1f;

    public bool IsDead { get; private set; }

    private Rigidbody2D rb;
    private Collider2D bodyCollider;
    private SkeletonAnimator animator;
    private Transform player;
    private Rigidbody2D playerBody;
    private Collider2D playerCollider;
    private Sprite[] chips;
    private int health;
    private float throwTimer;
    private float knockbackTimer;
    private float knockbackSpeed;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();
        animator = GetComponent<SkeletonAnimator>();
        rb.mass = mass;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        health = maxHealth;
        chips = SpriteStrip.SliceCentered(boneChips);
        // Don't open fire the instant the player appears.
        throwTimer = throwCooldown * 0.6f;
    }

    void Start()
    {
        var playerObject = GameObject.FindWithTag("Player");
        if (playerObject != null)
        {
            player = playerObject.transform;
            playerBody = playerObject.GetComponent<Rigidbody2D>();
            playerCollider = playerObject.GetComponent<Collider2D>();
        }
    }

    void FixedUpdate()
    {
        var vy = rb.linearVelocity.y;
        if (IsDead) return;

        // Knocked back: slide away from the hit, but never off the edge of the platform.
        if (knockbackTimer > 0f)
        {
            knockbackTimer -= Time.fixedDeltaTime;
            var dir = Mathf.Sign(knockbackSpeed);
            rb.linearVelocity = new Vector2(CanStep(dir) ? knockbackSpeed : 0f, vy);
            return;
        }

        // Always set our own horizontal speed, so a player walking into us can't push us along.
        var vx = 0f;
        if (player != null && !animator.IsBusy && InNoticeRange())
        {
            var dx = player.position.x - transform.position.x;
            var dir = Mathf.Sign(dx);
            animator.Face(dir > 0f);
            if (Mathf.Abs(dx) > keepDistance && CanStep(dir)) vx = dir * walkSpeed;
        }
        rb.linearVelocity = new Vector2(vx, vy);
    }

    void Update()
    {
        if (IsDead || player == null) return;
        throwTimer -= Time.deltaTime;
        if (throwTimer > 0f || animator.IsBusy || !Grounded()) return;

        var toPlayer = PlayerCenter() - (Vector2)transform.position;
        if (Mathf.Abs(toPlayer.x) <= throwRange && Mathf.Abs(toPlayer.y) <= throwRange * 0.7f)
        {
            animator.Face(toPlayer.x > 0f);
            animator.PlayThrow(ThrowBone);
            throwTimer = throwCooldown * Random.Range(0.85f, 1.15f);
        }
    }

    bool InNoticeRange()
    {
        var d = player.position - transform.position;
        return Mathf.Abs(d.x) <= noticeRange && Mathf.Abs(d.y) <= noticeRange * 0.6f;
    }

    // True if there is ground to stand on just past our feet in this direction, and no wall.
    bool CanStep(float dir)
    {
        var bounds = bodyCollider.bounds;
        var ahead = new Vector2(dir > 0f ? bounds.max.x + ledgeLookAhead : bounds.min.x - ledgeLookAhead,
                                bounds.min.y + 0.1f);
        if (!HitsGround(Physics2D.RaycastAll(ahead, Vector2.down, 0.5f))) return false;
        var chest = new Vector2(bounds.center.x, bounds.center.y);
        return !HitsGround(Physics2D.RaycastAll(chest, new Vector2(dir, 0f), bounds.extents.x + ledgeLookAhead));
    }

    bool Grounded()
    {
        var bounds = bodyCollider.bounds;
        return HitsGround(Physics2D.RaycastAll(new Vector2(bounds.center.x, bounds.min.y + 0.05f), Vector2.down, 0.2f));
    }

    // Ground is any solid collider that isn't a moving body (platforms have no Rigidbody2D).
    bool HitsGround(RaycastHit2D[] hits)
    {
        foreach (var hit in hits)
        {
            if (hit.collider.isTrigger || hit.collider == bodyCollider) continue;
            var body = hit.collider.attachedRigidbody;
            if (body == null || body.bodyType == RigidbodyType2D.Static) return true;
        }
        return false;
    }

    Vector2 PlayerCenter() => playerCollider != null ? (Vector2)playerCollider.bounds.center : (Vector2)player.position;

    void ThrowBone()
    {
        if (IsDead || player == null) return;
        var from = (Vector2)animator.ReleasePoint(ThrowReleasePixel);
        var target = PlayerCenter();
        var flightTime = Mathf.Clamp(Vector2.Distance(from, target) / boneSpeed, 0.35f, 1.1f);

        // Lead the target: aim where the player will be when the bone gets there.
        if (playerBody != null)
        {
            var v = playerBody.linearVelocity;
            target.x += v.x * flightTime;
            if (Mathf.Abs(v.y) > 0.1f)
            {
                var g = Physics2D.gravity.y * playerBody.gravityScale;
                var fall = v.y * flightTime + 0.5f * g * flightTime * flightTime;
                // Don't chase a jump below where the player could land.
                target.y += Mathf.Max(fall, -3f);
            }
        }

        var velocity = BoneProjectile.LaunchVelocity(from, target, flightTime, boneGravityScale);
        BoneProjectile.Throw(boneSprite, chips, from, velocity, boneDamage, boneGravityScale, rb);
    }

    /// <summary>Wounds the skeleton. hitVelocity is the projectile's velocity, used for knockback.</summary>
    public void TakeHit(int damage, Vector2 hitVelocity)
    {
        if (IsDead) return;
        health -= damage;
        var dir = Mathf.Abs(hitVelocity.x) > 0.01f ? Mathf.Sign(hitVelocity.x)
            : player != null ? Mathf.Sign(transform.position.x - player.position.x) : 1f;
        SpawnChips(dir, health <= 0 ? 10 : 5);

        if (health <= 0)
        {
            Die();
            return;
        }

        // Turn to face the attacker and stagger back.
        animator.Face(dir < 0f);
        animator.PlayHurt();
        knockbackSpeed = dir * knockbackDistance / knockbackTime;
        knockbackTimer = knockbackTime;
        throwTimer = Mathf.Max(throwTimer, 0.6f);
    }

    void SpawnChips(float dir, int count)
    {
        if (chips.Length == 0) return;
        var center = bodyCollider.bounds.center;
        for (var i = 0; i < count; i++)
        {
            var v = new Vector2(dir * Random.Range(1f, 4f), Random.Range(1.5f, 5f));
            SpriteFx.Spawn(new[] { chips[i % chips.Length] }, center + (Vector3)(Random.insideUnitCircle * 0.3f),
                           1f, Random.Range(0.45f, 0.8f), 12, Color.white)
                .Moving(v, 18f, Random.Range(-600f, 600f)).FadingFrom(0.65f);
        }
    }

    void Die()
    {
        IsDead = true;
        knockbackTimer = 0f;
        // Freeze the heap of bones in place, and let the player and the dropped items pass through it.
        rb.linearVelocity = Vector2.zero;
        rb.bodyType = RigidbodyType2D.Kinematic;
        bodyCollider.enabled = false;
        animator.PlayDeath(() =>
        {
            DropItems();
            Destroy(gameObject, 1.5f);
        });
    }

    void DropItems()
    {
        Vector3 baseSpawnPos = transform.position + spawnOffset;

        // Roll and drop Item 1 (supports multiple amounts)
        if (itemDropPrefab1 != null && Random.value <= (dropChance1 / 100f))
        {
            int count1 = Random.Range(minAmount1, maxAmount1 + 1);
            for (int i = 0; i < count1; i++)
            {
                Vector3 scatterPos = baseSpawnPos + new Vector3(Random.Range(-dropSpread, dropSpread), Random.Range(-dropSpread, dropSpread), 0f);
                GameObject dropped = Instantiate(itemDropPrefab1, scatterPos, Quaternion.identity);

                // Ensure enemy drops are not flagged as player-dropped
                WorldItem wi = dropped.GetComponent<WorldItem>();
                if (wi != null) wi.droppedByPlayer = false;
            }
        }

        // Roll and drop Item 2 (supports multiple amounts)
        if (itemDropPrefab2 != null && Random.value <= (dropChance2 / 100f))
        {
            int count2 = Random.Range(minAmount2, maxAmount2 + 1);
            for (int i = 0; i < count2; i++)
            {
                Vector3 scatterPos = baseSpawnPos + new Vector3(Random.Range(-dropSpread, dropSpread), Random.Range(-dropSpread, dropSpread), 0f);
                GameObject dropped = Instantiate(itemDropPrefab2, scatterPos, Quaternion.identity);

                // Ensure enemy drops are not flagged as player-dropped
                WorldItem wi = dropped.GetComponent<WorldItem>();
                if (wi != null) wi.droppedByPlayer = false;
            }
        }
    }
}
