using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Something that happens where two elements meet in midair. It begins when motes of the two
/// first collide; while the streams keep colliding nearby it is fed, which keeps it alive and
/// builds its intensity, so holding two streams against each other grows a bigger and bigger
/// effect. When the feeding stops it plays out and ends.
/// </summary>
public abstract class ElementReaction : MonoBehaviour
{
    public static readonly List<ElementReaction> Active = new();

    public Element First { get; private set; }
    public Element Second { get; private set; }
    public Vector2 Center { get; protected set; }

    /// <summary>Collisions within this distance feed this reaction instead of starting another.</summary>
    public virtual float FeedRadius => 2f;
    /// <summary>Seconds the reaction lasts after it was last fed.</summary>
    protected virtual float Sustain => 1.5f;
    /// <summary>Seconds it lasts if it is never fed after it starts.</summary>
    protected virtual float BaseLife => 2f;
    protected virtual float MaxLife => 12f;
    protected virtual float MaxIntensity => 3f;

    protected float age;
    protected float life;
    protected float intensity;
    /// <summary>The average flow of the streams feeding it, for drifting and spinning.</summary>
    protected Vector2 flow;
    protected Light2D glow;
    float sinceFeed;

    public void Begin(Element first, Element second, Vector2 at, Vector2 flowVelocity, float amount)
    {
        First = first;
        Second = second;
        Center = at;
        flow = flowVelocity;
        intensity = Mathf.Min(MaxIntensity, amount);
        life = BaseLife;
        glow = ElementLights.MakeLight(transform);
        transform.position = at;
        OnBegin();
    }

    /// <summary>Another collision nearby keeps the reaction going.</summary>
    public void Feed(Vector2 at, Vector2 flowVelocity, float amount)
    {
        life = Mathf.Min(MaxLife, Mathf.Max(life, age + Sustain));
        intensity = Mathf.Min(MaxIntensity, intensity + amount * 0.12f);
        flow = Vector2.Lerp(flow, flowVelocity, 0.1f);
        sinceFeed = 0f;
        OnFeed(at);
    }

    protected bool RecentlyFed => sinceFeed < 0.25f;
    protected float Remaining => life - age;
    /// <summary>1 while running, falling to 0 over the last fadeSeconds.</summary>
    protected float Fade(float fadeSeconds = 0.8f) => Mathf.Clamp01(Remaining / fadeSeconds);

    protected virtual void OnBegin() { }
    protected virtual void OnFeed(Vector2 at) { }
    protected abstract void Tick(float dt);
    protected virtual void OnEnd() { }

    void OnEnable() => Active.Add(this);
    void OnDisable() => Active.Remove(this);

    void Update()
    {
        var dt = Time.deltaTime;
        age += dt;
        sinceFeed += dt;
        if (age >= life)
        {
            OnEnd();
            Destroy(gameObject);
            return;
        }
        Tick(dt);
        transform.position = Center;
    }
}
