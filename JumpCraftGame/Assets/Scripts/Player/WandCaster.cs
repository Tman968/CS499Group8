using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Pours the selected wand's element while the attack is held: left mouse aims at the cursor,
/// Space pours straight ahead. 1-4, Q/E or the mouse wheel swap wands at any time.
///
/// The force of the stream pushes back on the wizard. For most wands that is just a judder; the
/// wind wand's recoil shoves him along (pour it behind you to dash) and, poured at the ground,
/// gives a mild boost upward: a hop from standing, a little extra height on a jump, a softer
/// landing. The lift is rationed per trip off the ground (windLiftBudget), so it can't be flown on.
/// A wand drained to nothing backfires (see Backlash).
///
/// Each stream has its sound: a burst as it starts, a loop while it pours and a tail as it stops.
/// </summary>
[RequireComponent(typeof(WandBelt), typeof(WizardAnimator))]
public class WandCaster : MonoBehaviour
{
    [Tooltip("Push back on the wizard from each stream (units/s^2): fire, ice, lightning, wind.")]
    public float[] recoil = { 2.5f, 1.5f, 1.2f, 16f };
    [Tooltip("How much the wand judders while pouring, in degrees: fire, ice, lightning, wind.")]
    public float[] judder = { 2f, 1f, 4f, 1.5f };
    [Tooltip("Seconds after an ice backfire during which the frozen hand can't cast.")]
    public float frozenHandSeconds = 1.2f;

    [Header("Wind lift")]
    [Tooltip("Upward push from the wind wand's recoil when pouring at the ground (units/s^2).")]
    public float windUpRecoil = 30f;
    [Tooltip("Total upward speed (units/s) the wind wand can add in one trip off the ground. Enough for a hop, never flight.")]
    public float windLiftBudget = 7f;
    [Tooltip("The wind wand adds no lift while the wizard is already rising faster than this (units/s).")]
    public float windLiftCeiling = 4f;

    public bool IsPouring { get; private set; }

    WandBelt belt;
    WizardAnimator wizard;
    ElementStatus status;
    PlayerHealth health;
    PlayerMovement movement;
    Rigidbody2D rb;
    WandStream[] streams;
    bool mouseHeld;
    float handFrozen;
    float windLiftLeft;
    bool wasPouring;
    bool wantedLastFrame;
    Element pouredElement;

    // Sound names per element, built once so nothing is allocated while pouring.
    static readonly string[] StartSound = Names("wand_{0}_start");
    static readonly string[] LoopSound = Names("wand_{0}_loop");
    static readonly string[] EndSound = Names("wand_{0}_end");
    static readonly string[] SwapSound = Names("wand_swap_{0}");
    static readonly string[] BackfireSound = Names("backfire_{0}");

    static string[] Names(string format)
    {
        var names = new string[Elements.Count];
        for (var i = 0; i < names.Length; i++) names[i] = string.Format(format, Sfx.ElementKey[i]);
        return names;
    }

    void Awake()
    {
        belt = GetComponent<WandBelt>();
        wizard = GetComponent<WizardAnimator>();
        rb = GetComponent<Rigidbody2D>();
        health = GetComponent<PlayerHealth>();
        movement = GetComponent<PlayerMovement>();
        status = GetComponent<ElementStatus>();
        if (status == null) status = gameObject.AddComponent<ElementStatus>();
    }

    void Start()
    {
        streams = new WandStream[Elements.Count];
        for (var i = 0; i < Elements.Count; i++) streams[i] = new WandStream((Element)i, rb, transform);
        belt.BurntOut += Backlash;
    }

    void OnDestroy()
    {
        if (belt != null) belt.BurntOut -= Backlash;
        if (streams == null) return;
        foreach (var s in streams) s?.Destroy();
    }

    void Update()
    {
        var dt = Time.deltaTime;
        handFrozen -= dt;
        var held = belt.Current;
        ReadSwap();
        if (belt.Current != held) Sfx.PlayFlat(SwapSound[(int)belt.Current]);

        // Back on the ground, the wind wand's lift is ready again.
        if (movement == null || (movement.OnGround && rb.linearVelocity.y < 0.5f)) windLiftLeft = windLiftBudget;

        var dead = health != null && health.IsDead;
        var wantsToPour = WantsToPour(out var target) && !dead;
        var element = belt.Current;
        var canPour = wantsToPour && !belt.IsBurntOut(element) && !status.IsStunned && handFrozen <= 0f;
        // Pulling the trigger on a dead or frozen wand: a dry click and a sputter.
        if (wantsToPour && !wantedLastFrame && (belt.IsBurntOut(element) || handFrozen > 0f))
            Sfx.Play("wand_empty", wizard.WeaponTip());
        wantedLastFrame = wantsToPour;

        IsPouring = false;
        if (canPour)
        {
            wizard.Channel(target, judder[(int)element]);
            var tip = (Vector2)wizard.WeaponTip();
            var dir = wizard.WeaponDirection();
            if (belt.Drain(element, dt))
            {
                IsPouring = true;
                streams[(int)element].Pour(tip, dir, dt);
                status.AddPush(Recoil(element, dir, dt));
            }
        }
        else
        {
            wizard.EndChannel();
        }
        PlayStreamSounds(element);

        for (var i = 0; i < streams.Length; i++)
            if (!IsPouring || i != (int)element) streams[i].Rest(dt);

        // The wand in hand always shows a little of its nature; a burnt-out one gutters.
        if (!IsPouring)
        {
            var tipNow = (Vector2)wizard.WeaponTip();
            if (belt.IsBurntOut(element)) ElementFx.Sputter(element, tipNow);
            else ElementFx.TipIdle(element, tipNow, belt.Charge(element));
        }
    }

    Vector2 Recoil(Element element, Vector2 dir, float dt)
    {
        var push = -dir * (recoil[(int)element] * dt);
        if (element != Element.Wind || dir.y >= 0f) return push;
        // Pouring wind at the ground lifts the wizard, but only by so much per trip off the ground,
        // and not at all while he is already rising: a boost, not a jetpack.
        var up = -dir.y * windUpRecoil * dt;
        if (rb.linearVelocity.y > windLiftCeiling) up = 0f;
        up = Mathf.Min(up, windLiftLeft);
        windLiftLeft -= up;
        push.y = up;
        return push;
    }

    void PlayStreamSounds(Element element)
    {
        var tip = (Vector2)wizard.WeaponTip();
        if (IsPouring)
        {
            if (!wasPouring || pouredElement != element)
            {
                if (wasPouring) Sfx.Play(EndSound[(int)pouredElement], tip);
                Sfx.Play(StartSound[(int)element], tip);
            }
            Sfx.Loop(this, LoopSound[(int)element], tip, 1f);
            pouredElement = element;
        }
        else if (wasPouring) Sfx.Play(EndSound[(int)pouredElement], tip);
        wasPouring = IsPouring;
    }

    bool WantsToPour(out Vector2 target)
    {
        target = default;
        var cam = Camera.main;
        var mouse = Mouse.current;
        var keyboard = Keyboard.current;

        // A click that starts on the HUD isn't an attack.
        if (mouse != null)
        {
            if (mouse.leftButton.wasPressedThisFrame)
                mouseHeld = EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject();
            if (!mouse.leftButton.isPressed) mouseHeld = false;
        }

        if (mouseHeld && cam != null)
        {
            Vector3 screen = mouse.position.ReadValue();
            screen.z = -cam.transform.position.z;
            target = cam.ScreenToWorldPoint(screen);
            return true;
        }
        if (keyboard != null && keyboard.spaceKey.isPressed)
        {
            var facing = wizard.FacingRight ? 1f : -1f;
            target = (Vector2)wizard.WeaponTip() + new Vector2(facing * 10f, 0f);
            return true;
        }
        return false;
    }

    void ReadSwap()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.digit1Key.wasPressedThisFrame) belt.Select(Element.Fire);
            if (keyboard.digit2Key.wasPressedThisFrame) belt.Select(Element.Ice);
            if (keyboard.digit3Key.wasPressedThisFrame) belt.Select(Element.Lightning);
            if (keyboard.digit4Key.wasPressedThisFrame) belt.Select(Element.Wind);
            if (keyboard.qKey.wasPressedThisFrame) belt.Cycle(-1);
            if (keyboard.eKey.wasPressedThisFrame) belt.Cycle(1);
        }
        var mouse = Mouse.current;
        if (mouse != null)
        {
            var scroll = mouse.scroll.ReadValue().y;
            if (scroll > 0.5f) belt.Cycle(-1);
            else if (scroll < -0.5f) belt.Cycle(1);
        }
    }

    /// <summary>
    /// A wand drained to nothing backfires on the wizard:
    ///   Fire       the wand flares up and sets him alight
    ///   Ice        frost bursts over his hand and arm: chilled and slowed, and he can't cast for a moment
    ///   Lightning  the current earths through him: stunned and hurt
    ///   Wind       the gust rips back the other way and throws him
    /// </summary>
    void Backlash(Element element)
    {
        var tip = (Vector2)wizard.WeaponTip();
        var dir = wizard.WeaponDirection();
        var center = status.Center;
        switch (element)
        {
            case Element.Fire:
                status.Ignite(1.6f, 0.35f);
                status.Damage(0.5f, Element.Fire, -dir);
                for (var i = 0; i < 26; i++)
                {
                    ArcaneParticles.Emit(FxLayer.Glow, new Spark
                    {
                        pos = tip, vel = Random.insideUnitCircle * 4f + Vector2.up * 2f, life = Random.Range(0.3f, 0.6f),
                        size = Random.value < 0.4f ? 2 : 1, sizeEnd = 1,
                        colorStart = Elements.FireWhite, colorMid = Elements.FireOrange, colorEnd = Elements.FireRed.WithAlpha(0f),
                        gravity = -5f, turbulence = 10f, drag = 2f,
                    });
                }
                for (var i = 0; i < 6; i++) ElementFx.Smoke(tip, 1.4f);
                ElementLights.Burst(tip, Elements.Glow(Element.Fire), 4f, 2.4f, 0.4f, true);
                break;

            case Element.Ice:
                status.ApplyChill(0.75f);
                handFrozen = frozenHandSeconds;
                ElementFx.IceShatter(tip, 24, Vector2.zero);
                ElementFx.IceShatter(center, 12, Vector2.up);
                break;

            case Element.Lightning:
                status.Shock(1f);
                status.Damage(0.45f, Element.Lightning, Vector2.up);
                for (var i = 0; i < 5; i++)
                    LightningBolts.Spawn(tip, center + Random.insideUnitCircle * 0.8f, 0.25f, 1f);
                LightningBolts.Spawn(center, center + Vector2.down * 1.3f, 0.3f, 2f);
                ElementLights.Burst(center, Elements.Glow(Element.Lightning), 4f, 2.6f, 0.35f, true);
                break;

            case Element.Wind:
                status.AddPush(-dir * 13f + Vector2.up * 4f);
                for (var i = 0; i < 20; i++)
                {
                    var v = WandStream.Rotate(-dir, Random.Range(-60f, 60f)) * Random.Range(3f, 8f);
                    ArcaneParticles.Emit(FxLayer.Front, new Spark
                    {
                        pos = tip, vel = v, life = Random.Range(0.4f, 0.8f), size = 1, sizeEnd = 1,
                        colorStart = Elements.WispPale, colorMid = Elements.WispMid, colorEnd = Elements.WispDark.WithAlpha(0f),
                        curl = Random.Range(-500f, 500f), drag = 2f,
                    });
                }
                break;
        }
        CameraShake.Add(0.35f);
        Sfx.Play(BackfireSound[(int)element], tip);
        if (ItemNotificationPopup.Instance != null)
            ItemNotificationPopup.Instance.TriggerPopup($"{Elements.Name(element)} wand burnt out!", false);
    }
}
