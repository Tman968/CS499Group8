using UnityEngine;

/// <summary>
/// Fires magic orbs from the wizard's weapon toward a point. The orb leaves the weapon's tip on
/// the thrust frame of WizardAnimator's cast. The crafted skull wand fires stronger orbs than the
/// default staff.
/// </summary>
[RequireComponent(typeof(WizardAnimator))]
public class WandShooter : MonoBehaviour
{
    [Header("Default Staff")]
    public Texture2D staffOrbFrames;
    public Texture2D staffBurstFrames;
    public Sprite staffSpark;
    public Color staffGlow = new(0.45f, 1f, 0.4f, 1f);
    [Tooltip("Skeletons have 6 health, so three hits from the staff kill one.")]
    public int staffDamage = 2;

    [Header("Skull Wand")]
    public Texture2D skullOrbFrames;
    public Texture2D skullBurstFrames;
    public Sprite skullSpark;
    public Color skullGlow = new(0.75f, 0.45f, 1f, 1f);
    [Tooltip("Skeletons have 6 health, so two hits from the skull wand kill one.")]
    public int skullDamage = 3;

    [Header("Flight")]
    public float orbSpeed = 15f;
    public float orbLifetime = 1.4f;

    private WizardAnimator wizard;
    private Rigidbody2D rb;
    private MagicOrb.Style staffStyle;
    private MagicOrb.Style skullStyle;

    void Awake()
    {
        wizard = GetComponent<WizardAnimator>();
        rb = GetComponent<Rigidbody2D>();
        staffStyle = MakeStyle(staffOrbFrames, staffBurstFrames, staffSpark, staffGlow);
        skullStyle = MakeStyle(skullOrbFrames, skullBurstFrames, skullSpark, skullGlow);
    }

    static MagicOrb.Style MakeStyle(Texture2D orb, Texture2D burst, Sprite spark, Color glow)
    {
        return new MagicOrb.Style
        {
            frames = SpriteStrip.SliceCentered(orb),
            burst = SpriteStrip.SliceCentered(burst),
            spark = spark != null ? new[] { spark } : new Sprite[0],
            glow = glow,
        };
    }

    /// <summary>Casts at a point in world space. Returns false if the wizard is still mid-cast.</summary>
    public bool FireAt(Vector2 target) => wizard.Cast(target, Release);

    void Release(Vector3 tip, Vector2 target)
    {
        var skull = SpriteSwitcher.Instance != null && SpriteSwitcher.Instance.IsToggledOn;
        var direction = target - (Vector2)tip;
        // A click right on top of the weapon would give no direction; shoot the way he faces.
        if (direction.sqrMagnitude < 0.04f) direction = new Vector2(target.x >= transform.position.x ? 1f : -1f, 0f);
        MagicOrb.Fire(skull ? skullStyle : staffStyle, tip, direction.normalized * orbSpeed,
                      skull ? skullDamage : staffDamage, orbLifetime, rb);
    }
}
