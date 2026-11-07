using System;
using UnityEngine;

/// <summary>
/// Frame-based animation for the wizard: idle, run, rise, glide and a wand cast, plus a held
/// weapon that can be swapped at runtime.
///
/// Body frames are horizontal strips made by Tools/generate_wizard_frames.py. The weapon and the
/// hand gripping it are separate sprites on a "Grip" child, so they can be swapped and swung.
/// Everything sits under the body sprite's object, which is a child of this one, so the physics
/// body and its collider are never moved. Facing is done by mirroring that object.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class WizardAnimator : MonoBehaviour
{
    // Where the hand grips the weapon in each cast frame, in frame pixels from the top-left, and
    // the weapon's angle (degrees, counter-clockwise, for art facing right). These match
    // CAST_POSES in Tools/generate_wizard_frames.py, which draws the sleeve reaching to the hand.
    static readonly Vector2[] CastHands =
    {
        new(32.5f, 31.5f), new(34.5f, 27.5f), new(36.5f, 24.5f), new(37.5f, 23.5f),
        new(37.5f, 23.5f), new(37.5f, 23.5f), new(35.5f, 27.5f), new(34.5f, 29.5f),
    };
    static readonly float[] CastAngles = { 12f, -18f, -44f, -52f, -50f, -48f, -22f, -8f };

    // Pixels the body rises in each run frame: lowest mid-stride, highest between steps.
    static readonly int[] RunBob = { 2, 1, 0, 1, 2, 1, 0, 1 };

    [Tooltip("The body sprite, on a child of this object.")]
    public SpriteRenderer body;

    [Tooltip("Tick if the source art faces right. Untick if it faces left.")]
    public bool artFacesRight = true;

    [Tooltip("Round offsets and sizes to whole art pixels so the sprite stays crisp.")]
    public bool pixelSnap = true;

    [Header("Frames")]
    public Texture2D idleFrames;
    public Texture2D runFrames;
    public Texture2D riseFrames;
    public Texture2D glideFrames;
    public Texture2D castFrames;
    [Tooltip("Width of one frame in the strips, in pixels.")]
    public int frameWidth = 44;
    public float pixelsPerUnit = 16f;

    [Header("Weapon")]
    [Tooltip("The weapon in the wizard's hand. Its pivot must be where the hand grips it.")]
    public Sprite weapon;
    [Tooltip("The hand drawn over the weapon. Its pivot must be the grip point too.")]
    public Sprite hand;
    [Tooltip("Where the hand rests, in frame pixels from the top-left.")]
    public Vector2 gripPixel = new(33.5f, 30.5f);

    [Header("Idle")]
    [Tooltip("Seconds per breath.")]
    public float idlePeriod = 2.2f;
    [Tooltip("How many art pixels taller the wizard gets at the top of a breath.")]
    public float idleBreathPixels = 1f;

    [Header("Run")]
    [Tooltip("Horizontal speed (units/sec) above which the wizard counts as running.")]
    public float runSpeedThreshold = 0.1f;
    [Tooltip("Full strides (two steps) per world unit travelled, so faster movement steps faster.")]
    public float stridesPerUnit = 0.25f;
    [Tooltip("How far the weapon rocks with each step, in degrees.")]
    public float runWeaponSwing = 4f;

    [Header("Air")]
    [Tooltip("Vertical speed (units/sec) above which the wizard counts as airborne.")]
    public float airborneSpeedThreshold = 0.2f;
    [Tooltip("Upward speed above which the rising frames play instead of the glide.")]
    public float riseSpeedThreshold = 1f;
    public float riseFps = 12f;
    [Tooltip("Frame rate of the billowing robe while falling. Slower looks grander.")]
    public float glideFps = 9f;
    [Tooltip("How much the wizard stretches while airborne (0.03 = 3%).")]
    public float airStretch = 0.03f;
    [Tooltip("How far the weapon drifts back and forth while gliding, in degrees.")]
    public float glideWeaponSway = 5f;

    [Header("Cast")]
    [Tooltip("Seconds each cast frame is shown: brace, raise, thrust, three holds, recover, settle.")]
    public float[] castFrameSeconds = { 0.09f, 0.06f, 0.06f, 0.16f, 0.14f, 0.14f, 0.09f, 0.07f };
    [Tooltip("Index of the cast frame where the spell leaves the weapon (the thrust).")]
    public int castReleaseFrame = 2;
    [Tooltip("How far up the weapon from the grip the spell leaves it, in art pixels.")]
    public float weaponTipPixels = 20f;
    [Tooltip("How far the weapon can point, in degrees from straight up (negative is forward).")]
    public Vector2 aimAngleRange = new(-140f, 20f);

    [Header("Hurt")]
    public Color hurtColor = new(1f, 0.35f, 0.35f, 1f);
    public float hurtFlashSeconds = 0.5f;

    public bool IsCasting => castTime >= 0f;
    public bool FacingRight => facingRight;

    private Rigidbody2D rb;
    private Sprite[] idle, run, rise, glide, cast;
    private Transform grip;
    private SpriteRenderer weaponRenderer;
    private SpriteRenderer handRenderer;
    private bool facingRight;
    private float idlePhase;
    private float runPhase;
    private float airTime;
    private float castTime = -1f;
    private float airWeight;
    private Vector2 castTarget;
    private Action<Vector3, Vector2> onCastRelease;
    private bool castReleased;
    private float hurtTimer;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (body == null) body = GetComponentInChildren<SpriteRenderer>();
        if (body == null || body.transform == transform)
        {
            Debug.LogWarning($"{name}: WizardAnimator needs its SpriteRenderer on a child object.", this);
            enabled = false;
            return;
        }

        idle = Slice(idleFrames);
        run = Slice(runFrames);
        rise = Slice(riseFrames);
        glide = Slice(glideFrames);
        cast = Slice(castFrames);
        if (idle.Length > 0) body.sprite = idle[0];
        body.flipX = false;

        grip = new GameObject("Grip").transform;
        grip.SetParent(body.transform, false);
        weaponRenderer = AddLayer("Weapon", 1);
        handRenderer = AddLayer("Hand", 2);
        weaponRenderer.sprite = weapon;
        handRenderer.sprite = hand;

        facingRight = artFacesRight;
        idlePhase = UnityEngine.Random.value;
    }

    /// <summary>Puts a different weapon in the wizard's hand.</summary>
    public void SetWeapon(Sprite newWeapon)
    {
        weapon = newWeapon;
        if (weaponRenderer != null) weaponRenderer.sprite = newWeapon;
    }

    /// <summary>
    /// Turns toward target (world space) and thrusts the weapon at it. onRelease runs on the thrust
    /// frame with the weapon's tip and the target. Returns false while the previous cast is still
    /// winding up, so casts can't be spammed faster than the animation.
    /// </summary>
    public bool Cast(Vector2 target, Action<Vector3, Vector2> onRelease)
    {
        if (!enabled || cast.Length == 0) return false;
        if (IsCasting && CurrentCastFrame() < castReleaseFrame + 2) return false;
        castTime = 0f;
        castTarget = target;
        onCastRelease = onRelease;
        castReleased = false;
        facingRight = target.x > transform.position.x;
        return true;
    }

    /// <summary>Flickers the wizard red for a moment, after he takes a hit.</summary>
    public void Flash() => hurtTimer = hurtFlashSeconds;

    void LateUpdate()
    {
        var dt = Time.deltaTime;
        var velocity = rb.linearVelocity;
        var speed = Mathf.Abs(velocity.x);
        var airborne = Mathf.Abs(velocity.y) > airborneSpeedThreshold;
        var running = !airborne && speed > runSpeedThreshold;

        // Keep facing the last direction moved in when stopping. While casting, face the target.
        if (speed > runSpeedThreshold && !IsCasting) facingRight = velocity.x > 0f;

        airWeight = Mathf.MoveTowards(airWeight, airborne ? 1f : 0f, 8f * dt);
        airTime = airborne ? airTime + dt : 0f;
        idlePhase = Mathf.Repeat(idlePhase + dt / idlePeriod, 1f);
        runPhase = running ? Mathf.Repeat(runPhase + speed * stridesPerUnit * dt, 1f) : 0f;

        var frame = idle.Length > 0 ? idle[0] : body.sprite;
        var handPx = gripPixel;
        var angle = 0f;
        var bobPx = 0f;
        var breath = 0f;

        var castFrame = AdvanceCast(dt);
        if (castFrame >= 0)
        {
            frame = cast[castFrame];
            handPx = CastHands[Mathf.Min(castFrame, CastHands.Length - 1)];
            angle = CastAngles[Mathf.Min(castFrame, CastAngles.Length - 1)];
            // Point the weapon at the target from the thrust until the recovery.
            var aim = AimAngle();
            if (castFrame == castReleaseFrame - 1 || castFrame == castReleaseFrame + 4) angle = Mathf.Lerp(angle, aim, 0.5f);
            else if (castFrame >= castReleaseFrame && castFrame < castReleaseFrame + 4) angle = aim;
        }
        else if (airborne && velocity.y > riseSpeedThreshold && rise.Length > 0)
        {
            frame = Loop(rise, airTime * riseFps);
            angle = 6f; // the staff trails a little as he rises
        }
        else if (airborne && glide.Length > 0)
        {
            frame = Loop(glide, airTime * glideFps);
            angle = Mathf.Sin(airTime * glideFps / glide.Length * 2f * Mathf.PI) * glideWeaponSway;
        }
        else if (running && run.Length > 0)
        {
            var i = (int)(runPhase * run.Length) % run.Length;
            frame = run[i];
            bobPx = RunBob[i % RunBob.Length];
            angle = -Mathf.Sin(runPhase * 4f * Mathf.PI) * runWeaponSwing;
        }
        else
        {
            breath = 0.5f - 0.5f * Mathf.Cos(idlePhase * 2f * Mathf.PI);
        }

        body.sprite = frame;
        var heightPx = frame.rect.height;
        var widthPx = frame.rect.width;

        var scaleY = 1f + breath * idleBreathPixels / heightPx;
        scaleY = Mathf.Lerp(scaleY, 1f + airStretch, airWeight);
        // Narrow as it stretches, so it keeps roughly the same volume.
        var scaleX = 1f + (1f - scaleY) * 0.5f;
        if (pixelSnap)
        {
            scaleY = Mathf.Round(scaleY * heightPx) / heightPx;
            scaleX = Mathf.Round(scaleX * widthPx) / widthPx;
        }

        var mirror = facingRight == artFacesRight ? 1f : -1f;
        var visual = body.transform;
        visual.localPosition = new Vector3(0f, bobPx / pixelsPerUnit, 0f);
        visual.localScale = new Vector3(scaleX * mirror, scaleY, 1f);
        visual.localRotation = Quaternion.identity;

        if (pixelSnap) angle = Mathf.Round(angle);
        grip.localPosition = FramePixelToLocal(handPx, frame);
        grip.localRotation = Quaternion.Euler(0f, 0f, angle);
        weaponRenderer.sortingLayerID = handRenderer.sortingLayerID = body.sortingLayerID;

        if (castFrame >= castReleaseFrame && !castReleased)
        {
            castReleased = true;
            onCastRelease?.Invoke(WeaponTip(), castTarget);
        }

        hurtTimer -= dt;
        // Flicker between the hurt tint and normal while the flash lasts.
        var tint = hurtTimer > 0f && (int)(hurtTimer * 16f) % 2 == 0 ? hurtColor : Color.white;
        body.color = weaponRenderer.color = handRenderer.color = tint;
    }

    /// <summary>Where spells leave the weapon, in world space.</summary>
    public Vector3 WeaponTip() => grip.TransformPoint(new Vector3(0f, weaponTipPixels / pixelsPerUnit, 0f));

    // The weapon angle that points it from the grip at the cast target, for art facing right.
    float AimAngle()
    {
        var mirror = facingRight == artFacesRight ? 1f : -1f;
        var to = castTarget - (Vector2)grip.position;
        var forward = to.x * mirror * (artFacesRight ? 1f : -1f);
        var angle = Mathf.Atan2(-forward, to.y) * Mathf.Rad2Deg;
        return Mathf.Clamp(angle, aimAngleRange.x, aimAngleRange.y);
    }

    int CurrentCastFrame()
    {
        var t = castTime;
        for (var i = 0; i < cast.Length; i++)
        {
            var seconds = i < castFrameSeconds.Length ? castFrameSeconds[i] : 0.08f;
            if (t < seconds) return i;
            t -= seconds;
        }
        return cast.Length;
    }

    // Steps the cast timer and returns the cast frame to show, or -1 when not casting.
    int AdvanceCast(float dt)
    {
        if (!IsCasting) return -1;
        castTime += dt;
        var t = castTime;
        for (var i = 0; i < cast.Length; i++)
        {
            var seconds = i < castFrameSeconds.Length ? castFrameSeconds[i] : 0.08f;
            if (t < seconds) return i;
            t -= seconds;
        }
        castTime = -1f;
        return -1;
    }

    static Sprite Loop(Sprite[] frames, float frameTime) => frames[(int)frameTime % frames.Length];

    // Frame pixels are measured from the top-left; the frame's pivot is its bottom-center.
    Vector3 FramePixelToLocal(Vector2 px, Sprite frame)
    {
        return new Vector3((px.x - frame.rect.width * 0.5f) / pixelsPerUnit,
                           (frame.rect.height - px.y) / pixelsPerUnit, 0f);
    }

    SpriteRenderer AddLayer(string layerName, int orderAboveBody)
    {
        var go = new GameObject(layerName);
        go.transform.SetParent(grip, false);
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sortingLayerID = body.sortingLayerID;
        renderer.sortingOrder = body.sortingOrder + orderAboveBody;
        renderer.sharedMaterial = body.sharedMaterial;
        return renderer;
    }

    Sprite[] Slice(Texture2D sheet)
    {
        if (sheet == null) return new Sprite[0];
        var count = Mathf.Max(1, sheet.width / frameWidth);
        var frames = new Sprite[count];
        for (var i = 0; i < count; i++)
        {
            var rect = new Rect(i * frameWidth, 0, frameWidth, sheet.height);
            frames[i] = Sprite.Create(sheet, rect, new Vector2(0.5f, 0f), pixelsPerUnit, 0, SpriteMeshType.FullRect);
            frames[i].name = $"{sheet.name}_{i}";
        }
        return frames;
    }
}
