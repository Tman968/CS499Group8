using UnityEngine;

/// <summary>
/// Idle, walk and in-air animation for single-frame pixel-art characters, driven by the
/// Rigidbody2D's velocity. It bobs, squashes and leans the sprite, and flips it to face the
/// direction of travel.
///
/// The sprite must live on a child object: only that child's transform is animated, so the
/// physics body and its collider on this object are never moved.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class CharacterAnimator : MonoBehaviour
{
    [Tooltip("The sprite to animate, on a child of this object.")]
    public SpriteRenderer sprite;

    [Tooltip("Tick if the source art faces right. Untick if it faces left.")]
    public bool artFacesRight = true;

    [Tooltip("Round offsets and sizes to whole art pixels so the sprite stays crisp.")]
    public bool pixelSnap = true;

    [Header("Idle")]
    [Tooltip("Seconds per breath.")]
    public float idlePeriod = 1.6f;
    [Tooltip("How many art pixels taller the sprite gets at the top of a breath.")]
    public float idleBreathPixels = 1f;
    [Tooltip("How many art pixels the sprite drifts side to side while idle.")]
    public float idleSwayPixels = 0f;

    [Header("Walk")]
    [Tooltip("Horizontal speed (units/sec) above which the character counts as walking.")]
    public float walkSpeedThreshold = 0.1f;
    [Tooltip("Footsteps per world unit travelled, so faster movement steps faster.")]
    public float stepsPerUnit = 0.5f;
    [Tooltip("How many art pixels the sprite rises mid-stride.")]
    public float walkBobPixels = 2f;
    [Tooltip("How much the sprite flattens on each footfall (0.05 = 5%).")]
    public float walkSquash = 0.05f;
    [Tooltip("Forward lean while walking, in degrees.")]
    public float walkLeanDegrees = 4f;

    [Header("Air")]
    [Tooltip("Vertical speed (units/sec) above which the character counts as airborne.")]
    public float airborneSpeedThreshold = 0.2f;
    [Tooltip("How much the sprite stretches while jumping or falling (0.06 = 6%).")]
    public float airStretch = 0.06f;

    [Header("Blending")]
    [Tooltip("How quickly the character blends between idle, walk and air poses.")]
    public float blendSpeed = 8f;

    private Rigidbody2D body;
    private bool facingRight;
    private float idlePhase;
    private float stepPhase;
    private float walkWeight;
    private float airWeight;

    void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        if (sprite == null) sprite = GetComponentInChildren<SpriteRenderer>();
        if (sprite == null || sprite.transform == transform)
        {
            Debug.LogWarning($"{name}: CharacterAnimator needs its SpriteRenderer on a child object.", this);
            enabled = false;
            return;
        }

        facingRight = artFacesRight;
        // Start each character at a different point in its breath so they don't move in sync.
        idlePhase = Random.value;
    }

    void LateUpdate()
    {
        var dt = Time.deltaTime;
        var velocity = body.linearVelocity;
        var speed = Mathf.Abs(velocity.x);
        var airborne = Mathf.Abs(velocity.y) > airborneSpeedThreshold;
        var walking = !airborne && speed > walkSpeedThreshold;

        // Keep facing the last direction moved in when stopping.
        if (speed > walkSpeedThreshold) facingRight = velocity.x > 0f;
        sprite.flipX = facingRight != artFacesRight;

        walkWeight = Mathf.MoveTowards(walkWeight, walking ? 1f : 0f, blendSpeed * dt);
        airWeight = Mathf.MoveTowards(airWeight, airborne ? 1f : 0f, blendSpeed * dt);

        // Idle: slow breathing, plus optional sway.
        idlePhase = Mathf.Repeat(idlePhase + dt / idlePeriod, 1f);
        var breath = 0.5f - 0.5f * Mathf.Cos(idlePhase * 2f * Mathf.PI);
        var idleGrowPx = breath * idleBreathPixels;
        var idleSwayPx = Mathf.Sin(idlePhase * 2f * Mathf.PI) * idleSwayPixels;

        // Walk: one bounce per footstep, flattened at each footfall, leaning into the direction of travel.
        stepPhase = Mathf.Repeat(stepPhase + speed * stepsPerUnit * dt, 1f);
        var stride = Mathf.Abs(Mathf.Sin(stepPhase * Mathf.PI));
        var walkBobPx = stride * walkBobPixels;
        var walkSquashAmount = (1f - stride) * walkSquash;
        var lean = (facingRight ? -1f : 1f) * walkLeanDegrees;

        var heightPx = sprite.sprite.rect.height;
        var widthPx = sprite.sprite.rect.width;

        var groundScaleY = Mathf.Lerp(1f + idleGrowPx / heightPx, 1f - walkSquashAmount, walkWeight);
        var scaleY = Mathf.Lerp(groundScaleY, 1f + airStretch, airWeight);
        // Widen as it flattens and narrow as it stretches, so it keeps roughly the same volume.
        var scaleX = 1f + (1f - scaleY) * 0.5f;

        var offsetXPx = Mathf.Lerp(idleSwayPx, 0f, Mathf.Max(walkWeight, airWeight));
        var offsetYPx = walkBobPx * walkWeight * (1f - airWeight);
        var angle = lean * walkWeight * (1f - airWeight);

        if (pixelSnap)
        {
            offsetXPx = Mathf.Round(offsetXPx);
            offsetYPx = Mathf.Round(offsetYPx);
            scaleY = Mathf.Round(scaleY * heightPx) / heightPx;
            scaleX = Mathf.Round(scaleX * widthPx) / widthPx;
        }

        var ppu = sprite.sprite.pixelsPerUnit;
        var visual = sprite.transform;
        visual.localPosition = new Vector3(offsetXPx / ppu, offsetYPx / ppu, 0f);
        visual.localScale = new Vector3(scaleX, scaleY, 1f);
        visual.localRotation = Quaternion.Euler(0f, 0f, angle);
    }
}
