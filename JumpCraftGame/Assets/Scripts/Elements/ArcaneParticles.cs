using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Which batch a particle is drawn in, back to front.</summary>
public enum FxLayer
{
    /// <summary>Alpha blended, behind the characters: smoke, mist, surface coats, ice blocks.</summary>
    Back = 0,
    /// <summary>Alpha blended, in front of the characters: shards, debris, snow.</summary>
    Front = 1,
    /// <summary>Additive light: flames, sparks, lightning, glints.</summary>
    Glow = 2,
    /// <summary>Alpha blended over everything: steam and blizzard clouds that hide what is inside.</summary>
    Veil = 3,
}

public enum FxShape : byte
{
    /// <summary>A square of whole art pixels, snapped to the pixel grid.</summary>
    Pixel,
    /// <summary>A soft, stepped glow.</summary>
    Halo,
    /// <summary>A bar stretched along the particle's velocity.</summary>
    Streak,
}

/// <summary>Everything a particle does over its life. Colors are sRGB; sizes are art pixels.</summary>
public struct Spark
{
    public Vector2 pos, vel;
    public float life;
    public float size, sizeEnd;
    public Color colorStart, colorMid, colorEnd;
    /// <summary>Fraction of velocity lost per second.</summary>
    public float drag;
    /// <summary>Downward acceleration; negative floats up like heat or smoke.</summary>
    public float gravity;
    /// <summary>Acceleration of a wandering, noisy drift.</summary>
    public float turbulence;
    /// <summary>Degrees per second the velocity turns, for curls and spirals.</summary>
    public float curl;
    /// <summary>Streaks: art pixels of length per unit/s of speed.</summary>
    public float stretch;
    /// <summary>How strongly the AirField moves it, 0 to 1.</summary>
    public float wind;
    /// <summary>Random brightness flicker, 0 to 1.</summary>
    public float flicker;
    public FxShape shape;
}

/// <summary>
/// A Noita-style pixel particle system: thousands of tiny glowing pixels, simulated on the CPU and
/// drawn as one mesh per layer. Anything can also draw single-frame shapes (lines, pixels, halos)
/// that are gone next frame, which is how lightning, ice shards and surface coats are drawn.
///
/// It creates itself the first time it is used, so nothing needs to be placed in the scene.
/// </summary>
[DefaultExecutionOrder(1000)]
public class ArcaneParticles : MonoBehaviour
{
    public const float Ppu = 16f;
    const float Px = 1f / Ppu;

    static readonly int[] SortingOrders = { 4, 14, 17, 20 };
    static readonly bool[] AdditiveLayer = { false, false, true, false };
    static readonly int[] Capacity = { 4000, 3500, 9000, 2500 };

    struct Particle
    {
        public Spark s;
        public float age;
        public float seed;
    }

    class Layer
    {
        public Particle[] items;
        public int count;
        public Vector3[] verts;
        public Color32[] colors;
        public Vector2[] uvs;
        public int[] tris;
        public int quads;
        public int maxQuads;
        public Mesh mesh;
    }

    static ArcaneParticles instance;
    static bool linear;
    static readonly Vector2 SolidUv = new(1f / 6f, 0.5f);
    const int AtlasCell = 32;

    Layer[] layers;
    Texture2D atlas;

    public static ArcaneParticles Instance
    {
        get
        {
            if (instance == null && Application.isPlaying) new GameObject("ArcaneParticles").AddComponent<ArcaneParticles>();
            return instance;
        }
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
        atlas = BuildAtlas();

        var alphaBase = Resources.Load<Material>("Elements/ElementAlpha");
        var glowBase = Resources.Load<Material>("Elements/ElementGlow");
        if (alphaBase == null || glowBase == null)
            Debug.LogError("ArcaneParticles: missing Resources/Elements/ElementAlpha or ElementGlow material.");

        layers = new Layer[SortingOrders.Length];
        for (var i = 0; i < layers.Length; i++)
        {
            var maxQuads = Capacity[i] + 3000; // room for single-frame shapes on top of particles
            var layer = new Layer
            {
                items = new Particle[Capacity[i]],
                verts = new Vector3[maxQuads * 4],
                colors = new Color32[maxQuads * 4],
                uvs = new Vector2[maxQuads * 4],
                tris = new int[maxQuads * 6],
                maxQuads = maxQuads,
                mesh = new Mesh { name = $"ArcaneParticles_{(FxLayer)i}", indexFormat = IndexFormat.UInt32 },
            };
            layer.mesh.MarkDynamic();
            for (var q = 0; q < maxQuads; q++)
            {
                var v = q * 4;
                var t = q * 6;
                layer.tris[t] = v;
                layer.tris[t + 1] = v + 1;
                layer.tris[t + 2] = v + 2;
                layer.tris[t + 3] = v;
                layer.tris[t + 4] = v + 2;
                layer.tris[t + 5] = v + 3;
            }

            var go = new GameObject(((FxLayer)i).ToString());
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = layer.mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            var source = AdditiveLayer[i] ? glowBase : alphaBase;
            if (source != null)
            {
                var material = new Material(source) { mainTexture = atlas };
                renderer.sharedMaterial = material;
            }
            renderer.sortingOrder = SortingOrders[i];
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            layers[i] = layer;
        }
    }

    void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
            AirField.Clear();
        }
    }

    // A 96x32 atlas of three cells: solid white; a round glow falling off in a few flat bands
    // with ordered dithering between them (the way a pixel artist shades a glow); and the same
    // glow in more bands without dithering, for big halos, where a stretched dither pattern
    // would read as a checkerboard.
    static Texture2D BuildAtlas()
    {
        const int n = AtlasCell;
        var tex = new Texture2D(n * 3, n, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            name = "ArcaneParticleAtlas",
        };
        int[,] bayer = { { 0, 8, 2, 10 }, { 12, 4, 14, 6 }, { 3, 11, 1, 9 }, { 15, 7, 13, 5 } };
        const int Bands = 5;
        for (var y = 0; y < n; y++)
        {
            for (var x = 0; x < n; x++)
            {
                tex.SetPixel(x, y, Color.white);
                var d = new Vector2(x - (n - 1) * 0.5f, y - (n - 1) * 0.5f).magnitude / (n * 0.5f);
                var falloff = Mathf.Pow(Mathf.Clamp01(1f - d), 1.7f);
                var level = falloff * Bands;
                var band = Mathf.Floor(level);
                var threshold = (bayer[y & 3, x & 3] + 0.5f) / 16f;
                if (level - band > threshold) band += 1f;
                var a = d >= 1f ? 0f : band / Bands;
                tex.SetPixel(n + x, y, new Color(1f, 1f, 1f, a));
                var smooth = d >= 1f ? 0f : Mathf.Round(falloff * 8f) / 8f;
                tex.SetPixel(2 * n + x, y, new Color(1f, 1f, 1f, smooth));
            }
        }
        tex.Apply(false, true);
        return tex;
    }

    // ------------------------------------------------------------------ emitting

    /// <summary>Spawns a particle. Silently drops it when the layer is full.</summary>
    public static void Emit(FxLayer layer, Spark s)
    {
        var self = Instance;
        if (self == null) return;
        var l = self.layers[(int)layer];
        if (l.count >= l.items.Length) return;
        s.colorStart = ToLinear(s.colorStart);
        s.colorMid = ToLinear(s.colorMid);
        s.colorEnd = ToLinear(s.colorEnd);
        if (s.life <= 0f) s.life = 0.01f;
        l.items[l.count++] = new Particle { s = s, age = 0f, seed = Random.value * 100f };
    }

    /// <summary>Particles alive in a layer, for throttling emitters when the screen is busy.</summary>
    public static float Fullness(FxLayer layer)
    {
        if (instance == null) return 0f;
        var l = instance.layers[(int)layer];
        return (float)l.count / l.items.Length;
    }

    // ------------------------------------------------------------------ single-frame shapes

    /// <summary>A square of whole art pixels at pos, for this frame only.</summary>
    public static void DrawPixel(FxLayer layer, Vector2 pos, int sizePx, Color color)
    {
        var self = Instance;
        if (self == null) return;
        self.PutPixel(self.layers[(int)layer], pos, sizePx, ToLinear32(color));
    }

    /// <summary>A soft glow sizePx art pixels across, for this frame only.</summary>
    public static void DrawHalo(FxLayer layer, Vector2 pos, float sizePx, Color color)
    {
        var self = Instance;
        if (self == null) return;
        var half = Mathf.Max(1f, Mathf.Round(sizePx)) * 0.5f * Px;
        var c = SnapCenter(pos, Mathf.Max(1, Mathf.RoundToInt(sizePx)));
        self.PutQuad(self.layers[(int)layer], c, new Vector2(half, 0f), new Vector2(0f, half), ToLinear32(color), true);
    }

    /// <summary>A bar from center along dir, lengthPx by widthPx, for this frame only.</summary>
    public static void DrawStreak(FxLayer layer, Vector2 center, Vector2 dir, float lengthPx, float widthPx, Color color)
    {
        var self = Instance;
        if (self == null) return;
        if (dir.sqrMagnitude < 1e-6f) dir = Vector2.right;
        dir.Normalize();
        var ax = dir * (lengthPx * 0.5f * Px);
        var ay = new Vector2(-dir.y, dir.x) * (widthPx * 0.5f * Px);
        self.PutQuad(self.layers[(int)layer], center, ax, ay, ToLinear32(color), false);
    }

    /// <summary>A rectangle in world units, for this frame only. Used for coats and blocks.</summary>
    public static void DrawRect(FxLayer layer, Vector2 min, Vector2 max, Color color)
    {
        var self = Instance;
        if (self == null) return;
        var center = (min + max) * 0.5f;
        var half = (max - min) * 0.5f;
        self.PutQuad(self.layers[(int)layer], center, new Vector2(half.x, 0f), new Vector2(0f, half.y), ToLinear32(color), false);
    }

    /// <summary>
    /// A line stepped across the pixel grid like a hand-drawn pixel line, thicknessPx wide,
    /// for this frame only. Runs of pixels in a row are merged into one quad.
    /// </summary>
    public static void DrawLine(FxLayer layer, Vector2 a, Vector2 b, int thicknessPx, Color color)
    {
        var self = Instance;
        if (self == null) return;
        var l = self.layers[(int)layer];
        var c = ToLinear32(color);
        var x0 = Mathf.FloorToInt(a.x * Ppu);
        var y0 = Mathf.FloorToInt(a.y * Ppu);
        var x1 = Mathf.FloorToInt(b.x * Ppu);
        var y1 = Mathf.FloorToInt(b.y * Ppu);
        var dx = Mathf.Abs(x1 - x0);
        var dy = -Mathf.Abs(y1 - y0);
        var sx = x0 < x1 ? 1 : -1;
        var sy = y0 < y1 ? 1 : -1;
        var err = dx + dy;
        var t = Mathf.Max(1, thicknessPx);
        var lo = -(t - 1) / 2;
        var steep = dy * -1 > dx;

        // Walk the line; flush a run whenever the row (or column, for steep lines) changes.
        int runStartX = x0, runStartY = y0, lastX = x0, lastY = y0;
        var guard = 0;
        while (guard++ < 4096)
        {
            var e2 = 2 * err;
            var done = x0 == x1 && y0 == y1;
            int nx = x0, ny = y0;
            if (!done)
            {
                if (e2 >= dy) { err += dy; nx += sx; }
                if (e2 <= dx) { err += dx; ny += sy; }
            }
            var breakRun = done || (steep ? nx != runStartX : ny != runStartY);
            lastX = x0;
            lastY = y0;
            if (breakRun)
            {
                int minX = Mathf.Min(runStartX, lastX), maxX = Mathf.Max(runStartX, lastX);
                int minY = Mathf.Min(runStartY, lastY), maxY = Mathf.Max(runStartY, lastY);
                if (steep) { minX += lo; maxX += lo + t - 1; }
                else { minY += lo; maxY += lo + t - 1; }
                var min = new Vector2(minX * Px, minY * Px);
                var max = new Vector2((maxX + 1) * Px, (maxY + 1) * Px);
                self.PutQuad(l, (min + max) * 0.5f, new Vector2((max.x - min.x) * 0.5f, 0f),
                             new Vector2(0f, (max.y - min.y) * 0.5f), c, false);
                runStartX = nx;
                runStartY = ny;
            }
            if (done) break;
            x0 = nx;
            y0 = ny;
        }
    }

    // ------------------------------------------------------------------ simulation and drawing

    void LateUpdate()
    {
        var dt = Mathf.Min(Time.deltaTime, 0.05f);
        var time = Time.time;
        for (var li = 0; li < layers.Length; li++)
        {
            var l = layers[li];
            var i = 0;
            while (i < l.count)
            {
                ref var p = ref l.items[i];
                p.age += dt;
                if (p.age >= p.s.life)
                {
                    l.items[i] = l.items[--l.count];
                    continue;
                }
                Step(ref p, dt, time);
                Draw(l, ref p);
                i++;
            }

            var n = l.quads * 4;
            l.mesh.Clear(true);
            if (n > 0)
            {
                l.mesh.SetVertices(l.verts, 0, n);
                l.mesh.SetColors(l.colors, 0, n);
                l.mesh.SetUVs(0, l.uvs, 0, n);
                l.mesh.SetIndices(l.tris, 0, l.quads * 6, MeshTopology.Triangles, 0, false);
                l.mesh.bounds = new Bounds(Vector3.zero, new Vector3(100000f, 100000f, 10f));
            }
            l.quads = 0;
        }
    }

    static void Step(ref Particle p, float dt, float time)
    {
        ref var s = ref p.s;
        var v = s.vel;
        v.y -= s.gravity * dt;
        if (s.turbulence > 0f)
        {
            var angle = (Mathf.Sin(s.pos.x * 2.3f + time * 3.1f + p.seed) +
                         Mathf.Sin(s.pos.y * 2.9f - time * 2.6f + p.seed * 1.7f)) * 2.2f;
            v += new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (s.turbulence * dt);
        }
        if (s.curl != 0f)
        {
            var a = s.curl * Mathf.Deg2Rad * dt;
            var cos = Mathf.Cos(a);
            var sin = Mathf.Sin(a);
            v = new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
        }
        if (s.wind > 0f) v += AirField.At(s.pos) * (s.wind * dt);
        if (s.drag > 0f) v *= Mathf.Max(0f, 1f - s.drag * dt);
        s.vel = v;
        s.pos += v * dt;
    }

    void Draw(Layer l, ref Particle p)
    {
        ref var s = ref p.s;
        var t = p.age / s.life;
        var c = t < 0.5f ? Color.LerpUnclamped(s.colorStart, s.colorMid, t * 2f)
                         : Color.LerpUnclamped(s.colorMid, s.colorEnd, (t - 0.5f) * 2f);
        if (s.flicker > 0f)
        {
            var h = Mathf.Sin(p.age * 61f + p.seed * 13f) * 43758.5453f;
            h -= Mathf.Floor(h);
            c.a *= 1f - s.flicker * h;
        }
        if (c.a <= 0.004f) return;
        var size = Mathf.LerpUnclamped(s.size, s.sizeEnd, t);
        var c32 = (Color32)c;
        switch (s.shape)
        {
            case FxShape.Pixel:
                PutPixel(l, s.pos, Mathf.Max(1, Mathf.RoundToInt(size)), c32);
                break;
            case FxShape.Halo:
            {
                var n = Mathf.Max(1, Mathf.RoundToInt(size));
                var half = n * 0.5f * Px;
                PutQuad(l, SnapCenter(s.pos, n), new Vector2(half, 0f), new Vector2(0f, half), c32, true);
                break;
            }
            default:
            {
                var speed = s.vel.magnitude;
                var dir = speed > 0.001f ? s.vel / speed : Vector2.right;
                var length = Mathf.Max(size, speed * s.stretch);
                var ax = dir * (length * 0.5f * Px);
                var ay = new Vector2(-dir.y, dir.x) * (Mathf.Max(1f, size) * 0.5f * Px);
                PutQuad(l, s.pos, ax, ay, c32, false);
                break;
            }
        }
    }

    // Odd sizes sit on a pixel center, even sizes on a pixel corner, so squares stay on the grid.
    static Vector2 SnapCenter(Vector2 pos, int sizePx)
    {
        var x = pos.x * Ppu;
        var y = pos.y * Ppu;
        if ((sizePx & 1) == 1)
        {
            x = Mathf.Floor(x) + 0.5f;
            y = Mathf.Floor(y) + 0.5f;
        }
        else
        {
            x = Mathf.Round(x);
            y = Mathf.Round(y);
        }
        return new Vector2(x * Px, y * Px);
    }

    void PutPixel(Layer l, Vector2 pos, int sizePx, Color32 c)
    {
        var half = sizePx * 0.5f * Px;
        PutQuad(l, SnapCenter(pos, sizePx), new Vector2(half, 0f), new Vector2(0f, half), c, false);
    }

    void PutQuad(Layer l, Vector2 center, Vector2 ax, Vector2 ay, Color32 c, bool halo)
    {
        if (l.quads >= l.maxQuads) return;
        var v = l.quads * 4;
        l.verts[v] = center - ax - ay;
        l.verts[v + 1] = center - ax + ay;
        l.verts[v + 2] = center + ax + ay;
        l.verts[v + 3] = center + ax - ay;
        l.colors[v] = l.colors[v + 1] = l.colors[v + 2] = l.colors[v + 3] = c;
        if (halo)
        {
            // Big halos use the undithered cell.
            var big = ax.magnitude * Ppu > 20f;
            var u0 = big ? 2f / 3f : 1f / 3f;
            var u1 = big ? 1f : 2f / 3f;
            l.uvs[v] = new Vector2(u0, 0f);
            l.uvs[v + 1] = new Vector2(u0, 1f);
            l.uvs[v + 2] = new Vector2(u1, 1f);
            l.uvs[v + 3] = new Vector2(u1, 0f);
        }
        else
        {
            l.uvs[v] = l.uvs[v + 1] = l.uvs[v + 2] = l.uvs[v + 3] = SolidUv;
        }
        l.quads++;
    }

    static Color ToLinear(Color c)
    {
        if (!linear) return c;
        var l = c.linear;
        l.a = c.a;
        return l;
    }

    static Color32 ToLinear32(Color c) => ToLinear(c);
}
