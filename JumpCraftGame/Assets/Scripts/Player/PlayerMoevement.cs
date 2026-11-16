using UnityEngine;
// Required if you ever use advanced Input System features,
// though using Key.current works directly with Keyboard.current
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 8f;
    public float jumpForce = 12f;

    [Header("Traction")]
    [Tooltip("How fast the wizard reaches walking speed on solid ground (units/s^2). High, so control stays snappy.")]
    public float groundAcceleration = 90f;
    [Tooltip("Steering in the air (units/s^2).")]
    public float airAcceleration = 45f;
    [Tooltip("Traction on frost and ice (units/s^2). Low, so the wizard slides and overshoots.")]
    public float iceAcceleration = 7f;
    [Tooltip("How fast solid ground stops a shove from wind or a blast (fraction per second). Ice doesn't.")]
    public float groundDriftDamping = 7f;
    [Tooltip("Lift from wind and updrafts stops adding upward speed past this (units/s).")]
    public float maxLiftSpeed = 9f;

    private Rigidbody2D rb;
    private PlayerHealth health;
    private ElementStatus status;
    private bool isGrounded = true;
    private float moveInput;
    private bool jumpQueued;
    private float walkVelocity;
    private readonly RaycastHit2D[] groundHits = new RaycastHit2D[4];

    //Read by CameraFollow to track the height of the ground the player is standing on.
    public bool IsGrounded => isGrounded;

    /// <summary>True while standing on frost, an ice sheet or an ice block.</summary>
    public bool OnIce { get; private set; }

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        health = GetComponent<PlayerHealth>();
        status = GetComponent<ElementStatus>();

        // No friction, so running into the side of a platform after a missed jump slides you
        // down it instead of pinning you to the wall. Movement sets velocity directly, so this
        // doesn't make the ground slippery.
        var slippery = new PhysicsMaterial2D("PlayerNoFriction") { friction = 0f, bounciness = 0f };
        foreach (var playerCollider in GetComponents<Collider2D>())
        {
            playerCollider.sharedMaterial = slippery;
        }
    }

    void Update()
    {
        // Make sure a keyboard is connected
        if (Keyboard.current == null) return;

        // No control once dead
        if (health != null && health.IsDead)
        {
            moveInput = 0f;
            return;
        }

        // 1. Left and Right Movement (A and D keys)
        moveInput = 0f;
        if (Keyboard.current.aKey.isPressed) moveInput = -1f; // Move left
        if (Keyboard.current.dKey.isPressed) moveInput = 1f;  // Move right

        // 2. Jumping (W key). Applied in FixedUpdate with the rest of the movement.
        if (Keyboard.current.wKey.wasPressedThisFrame && isGrounded && (status == null || !status.IsStunned))
            jumpQueued = true;

        // 3. Wand attacks are handled by WandCaster.

        // 4. Death check (falling below y = -8): Moves player back to main menu (death screen can be implemented later, or have a revive system with multiplayer?)
        if (transform.position.y <= -8)
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("GameMenu");
        }
    }

    void FixedUpdate()
    {
        var dt = Time.fixedDeltaTime;
        var v = rb.linearVelocity;
        var drift = status != null ? status.Drift : 0f;
        var dead = health != null && health.IsDead;

        // What the wizard is standing on decides how much grip he has.
        var grounded = CheckGround(out var feet);
        var surface = grounded ? SurfaceGrid.Probe(feet) : default;
        OnIce = grounded && surface.slippery;

        // Walking is tracked apart from drift (wind, blasts), so a gust can carry the wizard along
        // without his own steering cancelling it out. Solid ground soon stops a shove; ice doesn't.
        walkVelocity = v.x - drift;
        if (status != null && grounded && !OnIce) status.DampDrift(groundDriftDamping, dt);
        var control = dead ? 0f : status != null ? status.MoveMultiplier : 1f;
        var target = moveInput * moveSpeed * control;
        var accel = !grounded ? airAcceleration : OnIce ? iceAcceleration : groundAcceleration;
        if (control <= 0f && grounded && !OnIce) accel = groundAcceleration * 0.5f;
        walkVelocity = Mathf.MoveTowards(walkVelocity, target, accel * dt);
        v.x = walkVelocity + drift;

        if (dead) v.x = 0f;

        if (jumpQueued)
        {
            jumpQueued = false;
            v.y = jumpForce;
            isGrounded = false; // Prevent mid-air jumping
        }

        // Updrafts, gusts and wand recoil push up and down.
        if (status != null)
        {
            var lift = status.ConsumeLift();
            if (lift > 0f) lift = Mathf.Min(lift, Mathf.Max(0f, maxLiftSpeed - v.y));
            v.y += lift;
        }

        rb.linearVelocity = v;
    }

    // A short cast down from the bottom of the collider for solid ground.
    bool CheckGround(out Vector2 feet)
    {
        var col = GetComponent<Collider2D>();
        var bounds = col.bounds;
        feet = new Vector2(bounds.center.x, bounds.min.y);
        if (rb.linearVelocity.y > 0.5f) return false;
        var filter = new ContactFilter2D { useTriggers = false };
        var n = Physics2D.BoxCast(new Vector2(bounds.center.x, bounds.min.y + 0.1f), new Vector2(bounds.size.x * 0.8f, 0.05f),
                                  0f, Vector2.down, filter, groundHits, 0.15f);
        for (var i = 0; i < n; i++)
        {
            if (groundHits[i].rigidbody == rb || groundHits[i].normal.y < 0.5f) continue;
            return true;
        }
        return false;
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        //Checks the contact between the player and the platform
        //Prevents jumping on side and bottom of platforms
        foreach (ContactPoint2D contact in collision.contacts)
        {
            if (contact.normal.y > 0.5f) //Creates a normal vector based on the y direction of the contact (on top of a platform is positive)
            {
                isGrounded = true;
                break;
            }
        }
    }
}
