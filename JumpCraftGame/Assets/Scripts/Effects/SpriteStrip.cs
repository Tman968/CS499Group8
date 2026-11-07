using UnityEngine;

/// <summary>
/// Cuts a horizontal strip of equal-width animation frames (as made by the Tools/generate_*.py
/// scripts) into sprites at runtime.
/// </summary>
public static class SpriteStrip
{
    public const float PixelsPerUnit = 16f;

    /// <param name="frameWidth">Width of one frame in pixels. 0 means square frames.</param>
    /// <param name="pivot">Pivot as a 0-1 fraction of the frame, from the bottom-left.</param>
    public static Sprite[] Slice(Texture2D strip, int frameWidth, Vector2 pivot)
    {
        if (strip == null) return new Sprite[0];
        if (frameWidth <= 0) frameWidth = strip.height;
        var count = Mathf.Max(1, strip.width / frameWidth);
        var frames = new Sprite[count];
        for (var i = 0; i < count; i++)
        {
            var rect = new Rect(i * frameWidth, 0, frameWidth, strip.height);
            frames[i] = Sprite.Create(strip, rect, pivot, PixelsPerUnit, 0, SpriteMeshType.FullRect);
            frames[i].name = $"{strip.name}_{i}";
        }
        return frames;
    }

    /// <summary>Square frames with a centered pivot, for effects.</summary>
    public static Sprite[] SliceCentered(Texture2D strip) => Slice(strip, 0, new Vector2(0.5f, 0.5f));
}
