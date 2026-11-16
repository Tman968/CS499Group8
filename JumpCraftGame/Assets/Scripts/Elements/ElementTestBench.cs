using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Playtest tools for the elemental wands. Opens rifts that pour a vertical stream of any element
/// in front of the wizard, so pouring a wand across them shows each wand-against-wand reaction,
/// and spawns skeletons and props to try the elements on.
///
///   F1      show or hide these controls
///   5 6 7 8 open a Fire, Ice, Lightning or Wind rift in front of the wizard
///   9       rifts pour downward from above / upward from the ground
///   0       close every rift
///   U       unlimited wand charge on or off
///   K C B   spawn a skeleton, a crate or a bramble in front of the wizard
///   V       lay a puddle where the wizard stands
///   P L     hang a pendulum blade / raise a rune lift in front of the wizard
/// </summary>
public class ElementTestBench : MonoBehaviour
{
    const float RiftDistance = 3.2f;
    const float RiftHeight = 3f;

    readonly List<TestEmitter> rifts = new();
    WizardAnimator wizard;
    WandBelt belt;
    Collider2D body;
    bool fromAbove = true;
    TextMeshProUGUI help;
    TextMeshProUGUI hint;
    GameObject panel;

    void Start()
    {
        wizard = GetComponent<WizardAnimator>();
        belt = GetComponent<WandBelt>();
        body = GetComponent<Collider2D>();
        BuildUi();
    }

    void Update()
    {
        var k = Keyboard.current;
        if (k == null) return;

        if (k.f1Key.wasPressedThisFrame) panel.SetActive(!panel.activeSelf);
        if (k.digit5Key.wasPressedThisFrame) OpenRift(Element.Fire);
        if (k.digit6Key.wasPressedThisFrame) OpenRift(Element.Ice);
        if (k.digit7Key.wasPressedThisFrame) OpenRift(Element.Lightning);
        if (k.digit8Key.wasPressedThisFrame) OpenRift(Element.Wind);
        if (k.digit9Key.wasPressedThisFrame)
        {
            fromAbove = !fromAbove;
            RefreshHelp();
        }
        if (k.digit0Key.wasPressedThisFrame) CloseRifts();
        if (k.uKey.wasPressedThisFrame && belt != null)
        {
            belt.unlimitedCharge = !belt.unlimitedCharge;
            RefreshHelp();
        }

        var dressing = LevelDressing.Instance;
        if (dressing != null)
        {
            if (k.kKey.wasPressedThisFrame) dressing.SpawnSkeleton(Ahead(2.5f) + Vector2.up * 1.5f);
            if (k.cKey.wasPressedThisFrame && Ground(Ahead(1.6f), out var crate)) dressing.SpawnCrate(crate);
            if (k.bKey.wasPressedThisFrame && Ground(Ahead(2f), out var bramble)) dressing.SpawnBramble(bramble);
            if (k.pKey.wasPressedThisFrame && Ground(Ahead(2.5f), out var hang)) dressing.SpawnPendulum(hang);
            if (k.lKey.wasPressedThisFrame) dressing.SpawnLift(Ahead(2.6f) + Vector2.down * 0.2f);
        }
        if (k.vKey.wasPressedThisFrame && Ground((Vector2)body.bounds.center, out var feet))
            SurfaceGrid.AddPuddle(feet.x - 1.2f, feet.x + 1.2f, feet.y);
    }

    Vector2 Ahead(float distance)
    {
        var facing = wizard != null && !wizard.FacingRight ? -1f : 1f;
        var b = body.bounds;
        return new Vector2(b.center.x + facing * distance, b.min.y);
    }

    static bool Ground(Vector2 near, out Vector2 surface)
    {
        surface = default;
        if (!SurfaceGrid.FindSurface(near + Vector2.up * 1.5f, 6f, out var hit)) return false;
        surface = hit.point;
        return true;
    }

    void OpenRift(Element element)
    {
        var x = Ahead(RiftDistance).x;
        // Side by side with any rift already there.
        foreach (var r in rifts)
            if (r != null && Mathf.Abs(r.transform.position.x - x) < 0.9f) x += wizard != null && !wizard.FacingRight ? -1.1f : 1.1f;

        Vector2 origin;
        Vector2 dir;
        if (fromAbove)
        {
            // Above the wand's height, pouring down across where the wizard's own stream will go.
            origin = new Vector2(x, wizard.WeaponTip().y + RiftHeight);
            dir = Vector2.down;
        }
        else
        {
            origin = Ground(new Vector2(x, body.bounds.min.y), out var g) ? g + Vector2.up * 0.25f : new Vector2(x, body.bounds.min.y + 0.25f);
            dir = Vector2.up;
        }
        rifts.Add(TestEmitter.Create(element, origin, dir));
    }

    void CloseRifts()
    {
        foreach (var r in rifts)
        {
            if (r == null) continue;
            ElementFx.SteamPuff(r.transform.position, 2, 0.6f);
            Destroy(r.gameObject);
        }
        rifts.Clear();
    }

    // ------------------------------------------------------------------ on-screen help

    void BuildUi()
    {
        var go = new GameObject("ElementTestBenchUi", typeof(RectTransform));
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 41;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;

        hint = Text(go.transform, "F1  Element test bench", 15f, new Vector2(12f, 10f), new Vector2(300f, 22f));
        hint.color = new Color(0.62f, 0.58f, 0.68f, 0.8f);

        panel = new GameObject("Panel", typeof(RectTransform));
        var rt = panel.GetComponent<RectTransform>();
        rt.SetParent(go.transform, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0f);
        rt.anchoredPosition = new Vector2(10f, 36f);
        rt.sizeDelta = new Vector2(330f, 300f);
        var bg = panel.AddComponent<Image>();
        bg.color = new Color(0.06f, 0.04f, 0.08f, 0.88f);
        bg.raycastTarget = false;
        help = Text(rt, "", 16f, new Vector2(12f, 10f), new Vector2(306f, 280f));
        help.alignment = TextAlignmentOptions.BottomLeft;
        panel.SetActive(false);
        RefreshHelp();
    }

    void RefreshHelp()
    {
        if (help == null) return;
        var dir = fromAbove ? "pour down from above" : "pour up from the ground";
        var charge = belt != null && belt.unlimitedCharge ? "ON" : "off";
        help.text =
            "<b>ELEMENT TEST BENCH</b>\n" +
            "<color=#ff8830>5</color> Fire  <color=#78cdff>6</color> Ice  <color=#b292ff>7</color> Lightning  <color=#b0dec8>8</color> Wind\n" +
            $"   open a rift in front of you\n" +
            $"9  rifts {dir}\n" +
            "0  close all rifts\n" +
            $"U  unlimited charge: {charge}\n" +
            "K  skeleton    C  crate    B  bramble\n" +
            "V  puddle underfoot\n" +
            "P  pendulum blade    L  rune lift\n\n" +
            "Pour your wand across a rift to\nsee the two elements react.";
    }

    TextMeshProUGUI Text(Transform parent, string text, float size, Vector2 position, Vector2 box)
    {
        var rt = new GameObject("Text", typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0f);
        rt.anchoredPosition = position;
        rt.sizeDelta = box;
        var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = new Color(0.85f, 0.82f, 0.9f);
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        if (belt != null && belt.hudFont != null && size > 20f) tmp.font = belt.hudFont;
        return tmp;
    }
}
