using System;
using TMPro;
using UnityEngine;

/// <summary>
/// The four elemental wands the wizard carries from the start, and their charge.
///
/// Each wand has its own charge. Pouring a stream drains it; let go and, after a short pause, it
/// refills (the cooldown, then the reset). Drain a wand completely and it burns out: it backfires
/// on the wizard and stays dead until it has refilled all the way, more slowly than usual. The
/// other wands can still be used meanwhile, so swapping is part of fighting.
/// </summary>
public class WandBelt : MonoBehaviour
{
    // Where the hand grips the wand art, in pixels from the top-left (between the shaft's two
    // columns), and how far above the grip each wand's gem is, where its stream pours out: fire,
    // ice, lightning, wind. Mirrors WAND_GRIP and WAND_TIPS in Tools/generate_element_sprites.py.
    public static readonly Vector2 GripPixel = new(6f, 21.5f);
    public static readonly float[] TipPixels = { 18f, 20.5f, 18.5f, 17.5f };

    [Header("Wand art (Art/Sprites/Wands)")]
    public Texture2D fireWand;
    public Texture2D iceWand;
    public Texture2D lightningWand;
    public Texture2D windWand;

    [Header("HUD")]
    public TMP_FontAsset hudFont;

    [Header("Charge")]
    [Tooltip("Seconds of continuous pouring that drain each wand: fire, ice, lightning, wind.")]
    public float[] drainSeconds = { 3.2f, 3.8f, 2.6f, 4.2f };
    [Tooltip("Pause after letting go before a wand starts refilling.")]
    public float resetDelay = 0.45f;
    [Tooltip("Seconds to refill an empty wand normally.")]
    public float refillSeconds = 1.8f;
    [Tooltip("Seconds to refill a burnt-out wand. It can't be used until it is full.")]
    public float burnoutRefillSeconds = 5f;

    [Tooltip("Testing: wands never drain (the test bench toggles this).")]
    public bool unlimitedCharge;

    public Element Current { get; private set; } = Element.Fire;

    /// <summary>Raised when a wand is drained completely.</summary>
    public event Action<Element> BurntOut;
    /// <summary>Raised when the wizard swaps wands.</summary>
    public event Action<Element> Switched;

    readonly float[] charge = { 1f, 1f, 1f, 1f };
    readonly bool[] burnt = new bool[Elements.Count];
    readonly float[] idle = new float[Elements.Count];
    readonly bool[] drainedThisFrame = new bool[Elements.Count];
    Sprite[] held;
    Sprite[] icons;

    void Awake()
    {
        var textures = new[] { fireWand, iceWand, lightningWand, windWand };
        held = new Sprite[Elements.Count];
        icons = new Sprite[Elements.Count];
        for (var i = 0; i < Elements.Count; i++)
        {
            var tex = textures[i];
            if (tex == null) continue;
            tex.filterMode = FilterMode.Point;
            var pivot = new Vector2(GripPixel.x / tex.width, 1f - GripPixel.y / tex.height);
            held[i] = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), pivot, SpriteStrip.PixelsPerUnit, 0, SpriteMeshType.FullRect);
            held[i].name = $"HeldWand{(Element)i}";
            icons[i] = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), SpriteStrip.PixelsPerUnit, 0, SpriteMeshType.FullRect);
        }
    }

    void Start()
    {
        WandHud.Create(this);
        Select(Element.Fire, true);
    }

    public Sprite HeldSprite(Element e) => held[(int)e];
    public Sprite Icon(Element e) => icons[(int)e];
    public float Charge(Element e) => charge[(int)e];
    public bool IsBurntOut(Element e) => burnt[(int)e];
    public float DrainSeconds(Element e) => drainSeconds[Mathf.Clamp((int)e, 0, drainSeconds.Length - 1)];

    /// <summary>Takes a wand in hand. Returns false if it already was.</summary>
    public bool Select(Element e, bool force = false)
    {
        if (e == Current && !force) return false;
        Current = e;
        var wizard = GetComponent<WizardAnimator>();
        if (wizard != null && held[(int)e] != null) wizard.SetWeapon(held[(int)e], TipPixels[(int)e]);
        Switched?.Invoke(e);
        return true;
    }

    public void Cycle(int step) => Select((Element)(((int)Current + step + Elements.Count) % Elements.Count));

    /// <summary>
    /// Drains a wand for one frame of pouring. Returns false if it has nothing left to pour,
    /// which burns it out the moment it empties.
    /// </summary>
    public bool Drain(Element e, float dt)
    {
        var i = (int)e;
        if (burnt[i]) return false;
        drainedThisFrame[i] = true;
        idle[i] = 0f;
        if (unlimitedCharge) return true;
        charge[i] -= dt / DrainSeconds(e);
        if (charge[i] > 0f) return true;
        charge[i] = 0f;
        burnt[i] = true;
        BurntOut?.Invoke(e);
        return false;
    }

    void LateUpdate()
    {
        var dt = Time.deltaTime;
        for (var i = 0; i < Elements.Count; i++)
        {
            if (drainedThisFrame[i])
            {
                drainedThisFrame[i] = false;
                continue;
            }
            idle[i] += dt;
            if (!burnt[i] && idle[i] < resetDelay) continue;
            charge[i] = Mathf.Min(1f, charge[i] + dt / (burnt[i] ? burnoutRefillSeconds : refillSeconds));
            if (burnt[i] && charge[i] >= 1f) burnt[i] = false;
        }
    }
}
