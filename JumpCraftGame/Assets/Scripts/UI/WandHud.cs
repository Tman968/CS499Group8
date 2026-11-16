using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The wand belt in the top-left corner of the screen: four carved-stone slots, one per wand, each with
/// its key, the wand itself and a charge meter. The wand in hand is raised and glows in its
/// element's color; a burnt-out wand is cracked and its meter pulses red while it recovers.
/// Built at runtime, so it needs nothing in the scene.
/// </summary>
public class WandHud : MonoBehaviour
{
    const int SlotPx = 22;
    const float Scale = 3f;

    class Slot
    {
        public RectTransform root;
        public Image frame, glow, icon, meterFill, crack;
        public TextMeshProUGUI key;
    }

    WandBelt belt;
    Slot[] slots;
    TextMeshProUGUI label;
    float labelTimer;

    public static WandHud Create(WandBelt belt)
    {
        var go = new GameObject("WandHud", typeof(RectTransform));
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;
        var hud = go.AddComponent<WandHud>();
        hud.belt = belt;
        hud.Build();
        belt.Switched += hud.OnSwitched;
        return hud;
    }

    void OnDestroy()
    {
        if (belt != null) belt.Switched -= OnSwitched;
    }

    void Build()
    {
        var frameSprite = MakeSprite(DrawFrame(false));
        var glowSprite = MakeSprite(DrawFrame(true));
        var crackSprite = MakeSprite(DrawCrack());
        var white = MakeSprite(Solid(2, 2, Color.white));

        var bar = new GameObject("Belt", typeof(RectTransform)).GetComponent<RectTransform>();
        bar.SetParent(transform, false);
        bar.anchorMin = bar.anchorMax = new Vector2(0f, 1f);
        bar.pivot = new Vector2(0f, 1f);
        bar.anchoredPosition = new Vector2(16f, -16f);
        var size = SlotPx * Scale;
        var gap = 10f;
        bar.sizeDelta = new Vector2(Elements.Count * size + (Elements.Count - 1) * gap, size + 30f);

        slots = new Slot[Elements.Count];
        for (var i = 0; i < Elements.Count; i++)
        {
            var e = (Element)i;
            var slot = new Slot();
            slot.root = new GameObject($"Slot{e}", typeof(RectTransform)).GetComponent<RectTransform>();
            slot.root.SetParent(bar, false);
            slot.root.anchorMin = slot.root.anchorMax = new Vector2(0f, 1f);
            slot.root.pivot = new Vector2(0.5f, 1f);
            slot.root.sizeDelta = new Vector2(size, size);
            slot.root.anchoredPosition = new Vector2(size * 0.5f + i * (size + gap), -6f);

            slot.glow = AddImage(slot.root, "Glow", glowSprite, new Vector2(size + 12f, size + 12f), Vector2.zero);
            slot.frame = AddImage(slot.root, "Frame", frameSprite, new Vector2(size, size), Vector2.zero);

            var icon = belt.Icon(e);
            slot.icon = AddImage(slot.root, "Wand", icon, icon != null ? icon.rect.size * 2f : Vector2.one * 40f, new Vector2(0f, 2f));
            slot.icon.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -35f);

            // A thin meter along the bottom of the slot.
            var meterBack = AddImage(slot.root, "MeterBack", white, new Vector2(size - 18f, 6f), new Vector2(0f, -size * 0.5f + 9f));
            meterBack.color = new Color(0.05f, 0.03f, 0.07f, 0.95f);
            slot.meterFill = AddImage(meterBack.rectTransform, "Meter", white, new Vector2(size - 20f, 4f), Vector2.zero);
            slot.meterFill.type = Image.Type.Filled;
            slot.meterFill.fillMethod = Image.FillMethod.Horizontal;

            slot.crack = AddImage(slot.root, "Crack", crackSprite, new Vector2(size, size), Vector2.zero);

            slot.key = AddText(slot.root, (i + 1).ToString(), 18f, new Vector2(-size * 0.5f + 10f, size * 0.5f - 11f), new Vector2(20f, 20f));
            slot.key.alignment = TextAlignmentOptions.Center;
            slots[i] = slot;
        }

        label = AddText(bar, "", 26f, Vector2.zero, new Vector2(400f, 34f));
        label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0f, 1f);
        label.rectTransform.pivot = new Vector2(0f, 1f);
        label.rectTransform.anchoredPosition = new Vector2(4f, -size - 22f);
        label.alignment = TextAlignmentOptions.Left;
    }

    void OnSwitched(Element e)
    {
        labelTimer = 1.6f;
        if (label != null) label.text = $"{Elements.Name(e)} Wand";
    }

    void Update()
    {
        if (belt == null)
        {
            Destroy(gameObject);
            return;
        }
        var time = Time.unscaledTime;
        for (var i = 0; i < slots.Length; i++)
        {
            var e = (Element)i;
            var slot = slots[i];
            var selected = belt.Current == e;
            var burnt = belt.IsBurntOut(e);
            var glowColor = Elements.Glow(e);

            var lift = selected ? 2f : -6f;
            var pos = slot.root.anchoredPosition;
            pos.y = Mathf.Lerp(pos.y, lift, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
            slot.root.anchoredPosition = pos;

            slot.glow.enabled = selected;
            slot.glow.color = glowColor.WithAlpha(0.55f + 0.2f * Mathf.Sin(time * 4f));
            slot.frame.color = selected ? Color.Lerp(Color.white, glowColor, 0.25f) : new Color(0.7f, 0.66f, 0.75f);
            slot.icon.color = burnt ? new Color(0.35f, 0.3f, 0.32f) : selected ? Color.white : new Color(0.75f, 0.72f, 0.8f);

            slot.meterFill.fillAmount = belt.Charge(e);
            slot.meterFill.color = burnt
                ? Color.Lerp(new Color(0.55f, 0.08f, 0.08f), new Color(1f, 0.3f, 0.2f), 0.5f + 0.5f * Mathf.Sin(time * 9f))
                : glowColor;
            slot.crack.enabled = burnt;
            slot.key.color = selected ? glowColor : new Color(0.6f, 0.56f, 0.64f);
        }

        labelTimer -= Time.unscaledDeltaTime;
        if (label != null)
        {
            var c = Elements.Glow(belt.Current);
            label.color = c.WithAlpha(Mathf.Clamp01(labelTimer / 0.5f));
        }
    }

    Image AddImage(RectTransform parent, string name, Sprite sprite, Vector2 size, Vector2 position)
    {
        var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.sizeDelta = size;
        rt.anchoredPosition = position;
        var image = rt.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        return image;
    }

    TextMeshProUGUI AddText(RectTransform parent, string text, float fontSize, Vector2 position, Vector2 size)
    {
        var rt = new GameObject("Text", typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.sizeDelta = size;
        rt.anchoredPosition = position;
        var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (belt.hudFont != null) tmp.font = belt.hudFont;
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        return tmp;
    }

    // ------------------------------------------------------------------ pixel art drawn in code

    static Sprite MakeSprite(Texture2D tex) =>
        Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 16f, 0, SpriteMeshType.FullRect);

    static Texture2D Solid(int w, int h, Color c)
    {
        var tex = NewTexture(w, h);
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++) tex.SetPixel(x, y, c);
        tex.Apply();
        return tex;
    }

    static Texture2D NewTexture(int w, int h)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        var clear = new Color(0f, 0f, 0f, 0f);
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++) tex.SetPixel(x, y, clear);
        return tex;
    }

    // A carved stone slot: dark recess, bevelled rim, iron studs at the corners. The glow version
    // is a soft square halo drawn behind the selected slot.
    static Texture2D DrawFrame(bool glow)
    {
        const int n = SlotPx;
        var tex = NewTexture(n, n);
        if (glow)
        {
            for (var y = 0; y < n; y++)
            {
                for (var x = 0; x < n; x++)
                {
                    var d = Mathf.Max(Mathf.Abs(x - (n - 1) * 0.5f), Mathf.Abs(y - (n - 1) * 0.5f)) / (n * 0.5f);
                    var a = d > 0.98f ? 0f : d > 0.85f ? 0.35f : d > 0.7f ? 0.7f : 0f;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            return tex;
        }

        var outline = Elements.Rgb(13, 9, 17);
        var rimLight = Elements.Rgb(92, 80, 104);
        var rimMid = Elements.Rgb(62, 52, 74);
        var rimDark = Elements.Rgb(36, 28, 44);
        var recess = Elements.Rgb(20, 15, 26, 0.92f);
        var recessEdge = Elements.Rgb(12, 9, 16, 0.95f);
        for (var y = 0; y < n; y++)
        {
            for (var x = 0; x < n; x++)
            {
                Color c;
                var edge = x == 0 || y == 0 || x == n - 1 || y == n - 1;
                var rim = x <= 2 || y <= 2 || x >= n - 3 || y >= n - 3;
                if (edge) c = outline;
                else if (rim)
                {
                    // Lit from the top-left.
                    var lit = (y >= n - 3 || x <= 2) && !(y <= 2 || x >= n - 3);
                    c = lit ? rimLight : (y <= 2 || x >= n - 3) ? rimDark : rimMid;
                    if ((x * 7 + y * 13) % 11 == 0) c = rimMid;
                }
                else c = x == 3 || y == n - 4 ? recessEdge : recess;
                tex.SetPixel(x, y, c);
            }
        }
        var stud = Elements.Rgb(132, 120, 140);
        foreach (var (x, y) in new[] { (1, 1), (n - 2, 1), (1, n - 2), (n - 2, n - 2) }) tex.SetPixel(x, y, stud);
        tex.Apply();
        return tex;
    }

    static Texture2D DrawCrack()
    {
        const int n = SlotPx;
        var tex = NewTexture(n, n);
        var dark = Elements.Rgb(10, 6, 10);
        var ember = Elements.Rgb(200, 46, 24);
        int[,] path = { { 4, 18 }, { 6, 16 }, { 7, 15 }, { 8, 13 }, { 10, 12 }, { 11, 10 }, { 12, 9 }, { 13, 7 }, { 15, 6 }, { 16, 4 }, { 17, 3 } };
        for (var i = 0; i < path.GetLength(0); i++)
        {
            tex.SetPixel(path[i, 0], path[i, 1], dark);
            tex.SetPixel(path[i, 0] + 1, path[i, 1], i % 3 == 1 ? ember : dark);
        }
        int[,] fork = { { 10, 12 }, { 9, 10 }, { 8, 8 }, { 8, 7 } };
        for (var i = 0; i < fork.GetLength(0); i++) tex.SetPixel(fork[i, 0], fork[i, 1], dark);
        tex.Apply();
        return tex;
    }
}
