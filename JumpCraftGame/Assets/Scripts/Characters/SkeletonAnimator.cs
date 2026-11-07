using System;
using UnityEngine;

/// <summary>
/// Frame-based animation for the skeleton: idle, walk, a bone throw, a hurt recoil and a death
/// collapse. Frames are horizontal strips made by Tools/generate_combat_sprites.py.
///
/// The sprite must live on a child object, so the physics body is never moved. Facing is done by
/// mirroring that child; Skeleton decides which way to face.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class SkeletonAnimator : MonoBehaviour
{
    [Tooltip("The sprite to animate, on a child of this object.")]
    public SpriteRenderer body;

    [Tooltip("Tick if the source art faces right. Untick if it faces left.")]
    public bool artFacesRight = true;

    [Header("Frames")]
    public Texture2D idleFrames;
    public Texture2D walkFrames;
    public Texture2D throwFrames;
    public Texture2D hurtFrames;
    public Texture2D deathFrames;
    [Tooltip("Width of one frame in the strips, in pixels.")]
    public int frameWidth = 44;

    [Header("Timing")]
    [Tooltip("Full strides (two steps) per world unit walked.")]
    public float stridesPerUnit = 0.45f;
    [Tooltip("Seconds per breath while idle.")]
    public float idlePeriod = 2.2f;
    [Tooltip("Seconds each throw frame is shown: reach back, wind up, cock, release, follow through, recover.")]
    public float[] throwFrameSeconds = { 0.12f, 0.12f, 0.22f, 0.07f, 0.12f, 0.16f };
    [Tooltip("Index of the throw frame where the bone leaves the hand.")]
    public int throwReleaseFrame = 3;
    public float hurtFps = 14f;
    public float deathFps = 11f;

    public bool IsBusy => current != Clip.None;
    public bool FacingRight { get; private set; } = true;

    private enum Clip { None, Throw, Hurt, Death }

    private Rigidbody2D rb;
    private Sprite[] idle, walk, throwing, hurt, death;
    private Clip current = Clip.None;
    private float clipTime;
    private float stridePhase;
    private float idlePhase;
    private Action onRelease;
    private Action onFinished;
    private bool released;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (body == null) body = GetComponentInChildren<SpriteRenderer>();
        if (body == null || body.transform == transform)
        {
            Debug.LogWarning($"{name}: SkeletonAnimator needs its SpriteRenderer on a child object.", this);
            enabled = false;
            return;
        }

        var pivot = new Vector2(0.5f, 0f);
        idle = SpriteStrip.Slice(idleFrames, frameWidth, pivot);
        walk = SpriteStrip.Slice(walkFrames, frameWidth, pivot);
        throwing = SpriteStrip.Slice(throwFrames, frameWidth, pivot);
        hurt = SpriteStrip.Slice(hurtFrames, frameWidth, pivot);
        death = SpriteStrip.Slice(deathFrames, frameWidth, pivot);
        if (idle.Length > 0) body.sprite = idle[0];
        body.flipX = false;
        idlePhase = UnityEngine.Random.value;
    }

    public void Face(bool right) => FacingRight = right;

    /// <summary>Plays the throw; onRelease runs on the frame the bone leaves the hand.</summary>
    public void PlayThrow(Action release, Action finished = null)
    {
        if (current == Clip.Death || throwing.Length == 0) return;
        Play(Clip.Throw, finished);
        onRelease = release;
        released = false;
    }

    /// <summary>Plays the hurt recoil, interrupting a throw before its release.</summary>
    public void PlayHurt()
    {
        if (current == Clip.Death || hurt.Length == 0) return;
        Play(Clip.Hurt, null);
    }

    public void PlayDeath(Action finished)
    {
        if (death.Length == 0)
        {
            finished?.Invoke();
            return;
        }
        Play(Clip.Death, finished);
    }

    void Play(Clip clip, Action finished)
    {
        current = clip;
        clipTime = 0f;
        onFinished = finished;
        onRelease = null;
    }

    void LateUpdate()
    {
        var dt = Time.deltaTime;
        clipTime += dt;
        var speed = Mathf.Abs(rb.linearVelocity.x);
        idlePhase = Mathf.Repeat(idlePhase + dt / idlePeriod, 1f);

        Sprite frame;
        var breathPx = 0f;
        switch (current)
        {
            case Clip.Throw:
                frame = ThrowFrame();
                break;
            case Clip.Hurt:
                frame = Timed(hurt, hurtFps, false);
                break;
            case Clip.Death:
                frame = Timed(death, deathFps, true);
                break;
            default:
                if (speed > 0.1f && walk.Length > 0)
                {
                    stridePhase = Mathf.Repeat(stridePhase + speed * stridesPerUnit * dt, 1f);
                    frame = walk[(int)(stridePhase * walk.Length) % walk.Length];
                }
                else
                {
                    frame = idle.Length > 0 ? idle[0] : body.sprite;
                    breathPx = Mathf.Round(0.5f - 0.5f * Mathf.Cos(idlePhase * 2f * Mathf.PI));
                }
                break;
        }

        body.sprite = frame;
        var mirror = FacingRight == artFacesRight ? 1f : -1f;
        var height = frame.rect.height;
        var visual = body.transform;
        visual.localPosition = Vector3.zero;
        visual.localScale = new Vector3(mirror, 1f + breathPx / height, 1f);
        visual.localRotation = Quaternion.identity;
    }

    Sprite ThrowFrame()
    {
        var t = clipTime;
        for (var i = 0; i < throwing.Length; i++)
        {
            var seconds = i < throwFrameSeconds.Length ? throwFrameSeconds[i] : 0.1f;
            if (t < seconds)
            {
                if (i >= throwReleaseFrame && !released)
                {
                    released = true;
                    onRelease?.Invoke();
                }
                return throwing[i];
            }
            t -= seconds;
        }
        Finish();
        return idle.Length > 0 ? idle[0] : throwing[throwing.Length - 1];
    }

    // Plays frames at fps. Clips that hold stay on their last frame afterwards.
    Sprite Timed(Sprite[] frames, float fps, bool hold)
    {
        var i = (int)(clipTime * fps);
        if (i < frames.Length) return frames[i];
        if (hold)
        {
            if (onFinished != null)
            {
                var done = onFinished;
                onFinished = null;
                done();
            }
            return frames[frames.Length - 1];
        }
        Finish();
        return idle.Length > 0 ? idle[0] : frames[frames.Length - 1];
    }

    void Finish()
    {
        current = Clip.None;
        var done = onFinished;
        onFinished = null;
        done?.Invoke();
    }

    /// <summary>Where the throwing hand is at the release, in world space.</summary>
    public Vector3 ReleasePoint(Vector2 releasePixel)
    {
        var frame = body.sprite;
        var local = new Vector3((releasePixel.x - frame.rect.width * 0.5f) / SpriteStrip.PixelsPerUnit,
                                (frame.rect.height - releasePixel.y) / SpriteStrip.PixelsPerUnit, 0f);
        return body.transform.TransformPoint(local);
    }
}
