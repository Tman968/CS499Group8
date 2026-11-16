using UnityEngine;

/// <summary>The four wand elements. The order is the wand belt's order and its number keys.</summary>
public enum Element
{
    Fire = 0,
    Ice = 1,
    Lightning = 2,
    Wind = 3,
}

/// <summary>
/// Names and the shared palette for the elements. Colors are in sRGB, as an artist picks them;
/// ArcaneParticles converts them for the linear-space renderer.
/// </summary>
public static class Elements
{
    public const int Count = 4;

    public static string Name(Element e) => e switch
    {
        Element.Fire => "Fire",
        Element.Ice => "Ice",
        Element.Lightning => "Lightning",
        _ => "Wind",
    };

    /// <summary>The color of the element's light and HUD accents.</summary>
    public static Color Glow(Element e) => e switch
    {
        Element.Fire => Rgb(255, 136, 48),
        Element.Ice => Rgb(120, 205, 255),
        Element.Lightning => Rgb(178, 146, 255),
        _ => Rgb(176, 222, 200),
    };

    // Fire: a white-hot core cooling through yellow and orange to blood red, then soot.
    public static readonly Color FireWhite = Rgb(255, 250, 222);
    public static readonly Color FireYellow = Rgb(255, 214, 92);
    public static readonly Color FireOrange = Rgb(255, 126, 34);
    public static readonly Color FireRed = Rgb(200, 46, 24);
    public static readonly Color FireDeep = Rgb(104, 18, 20);
    public static readonly Color Soot = Rgb(30, 22, 34);

    // Ice: white glints over pale cyan, blue in the shadows.
    public static readonly Color IceWhite = Rgb(240, 252, 255);
    public static readonly Color IcePale = Rgb(176, 232, 255);
    public static readonly Color IceCyan = Rgb(96, 184, 236);
    public static readonly Color IceDeep = Rgb(42, 88, 168);
    public static readonly Color FrostMist = Rgb(160, 206, 232);

    // Lightning: a white core in a violet sheath, the same family as the skull wand's glow.
    public static readonly Color BoltCore = Rgb(248, 244, 255);
    public static readonly Color BoltViolet = Rgb(176, 136, 255);
    public static readonly Color BoltBlue = Rgb(98, 118, 255);
    public static readonly Color BoltDeep = Rgb(62, 36, 150);

    // Wind: pale, spectral wisps, and the dead leaves and grave dust they carry.
    public static readonly Color WispPale = Rgb(214, 238, 226);
    public static readonly Color WispMid = Rgb(148, 186, 176);
    public static readonly Color WispDark = Rgb(72, 98, 98);
    public static readonly Color Leaf = Rgb(98, 72, 44);
    public static readonly Color LeafDark = Rgb(58, 44, 30);

    // Water and steam.
    public static readonly Color WaterDark = Rgb(22, 46, 62);
    public static readonly Color WaterMid = Rgb(44, 96, 118);
    public static readonly Color WaterShine = Rgb(132, 196, 210);
    public static readonly Color Steam = Rgb(206, 210, 222);

    public static Color Rgb(int r, int g, int b, float a = 1f) => new(r / 255f, g / 255f, b / 255f, a);

    public static Color WithAlpha(this Color c, float a) => new(c.r, c.g, c.b, a);
}
