using System.Collections.Generic;
using UnityEngine;

/// <summary>What lies on top of a surface.</summary>
public enum SurfaceCoat : byte
{
    None,
    /// <summary>A crust of frost from ice shards. Slippery. Thick enough, it grows into an ice block.</summary>
    Frost,
    /// <summary>Burning embers from fire. Burns whatever stands in them; the heat makes an updraft.</summary>
    Embers,
    /// <summary>A puddle. Soaks, conducts lightning, freezes into a sheet, boils off under fire.</summary>
    Water,
    /// <summary>Frozen water. Slippery and conductive; thaws back to water.</summary>
    IceSheet,
}

/// <summary>What a character is standing on, from SurfaceGrid.Probe.</summary>
public struct SurfaceInfo
{
    public bool found;
    public SurfaceCoat coat;
    public bool charged;
    public bool slippery;
    public bool onIceBlock;
}

/// <summary>
/// The lasting marks the wands leave on the world, on a grid of 4-pixel cells: coats lying on top
/// of platforms (frost, embers, water, ice sheets, any of which can carry an electric charge), and
/// solid ice blocks that shards build up into ledges, pillars and bridges. Blocks are real
/// colliders, so they can be stood on, and they melt away after a while (or at once under fire).
///
/// Coats are keyed by cell column and the art-pixel row of the surface they lie on, so they sit
/// exactly on a platform's top whatever its height. Blocks are keyed by their center in art pixels.
/// </summary>
[DefaultExecutionOrder(800)]
public class SurfaceGrid : MonoBehaviour
{
    public const float Cell = 0.25f;
    const int CellPx = 4;
    const float Px = 1f / 16f;
    const int MaxCoats = 900;
    const int MaxBlocks = 320;
    const float BlockLife = 18f;
    const float FrostLife = 16f;
    const float EmberLife = 5f;
    const float ChargeLife = 2.6f;

    class Coat
    {
        public int cx, row;
        public SurfaceCoat type;
        public float amount;
        public float timer;
        public float charge;
        public bool permanentWater;
        public float seed;
    }

    class Block
    {
        public int px, py;
        public BoxCollider2D col;
        public float timer, life;
        public float hp;
        public float charge;
        public float seed;
        public Vector2 Center => new(px * Px, py * Px);
    }

    static SurfaceGrid instance;

    readonly Dictionary<long, Coat> coats = new();
    readonly Dictionary<long, Block> blocks = new();
    readonly Dictionary<Collider2D, Block> blockByCollider = new();
    readonly Dictionary<long, float> pendingIce = new();
    readonly List<long> scratch = new();
    readonly Queue<(int kind, int a, int b)> flood = new();
    readonly HashSet<(int kind, int a, int b)> seen = new();
    readonly RaycastHit2D[] hits = new RaycastHit2D[8];
    readonly Collider2D[] overlaps = new Collider2D[8];
    GameObject iceBody;
    ContactFilter2D solidFilter;
    float pruneTimer;

    public static SurfaceGrid Instance
    {
        get
        {
            if (instance == null && Application.isPlaying) new GameObject("SurfaceGrid").AddComponent<SurfaceGrid>();
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
        iceBody = new GameObject("IceBlocks");
        iceBody.transform.SetParent(transform, false);
        solidFilter = new ContactFilter2D { useTriggers = false };
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    static long Key(int a, int b) => ((long)a << 32) ^ (uint)b;

    /// <summary>True if the collider is one of the ice blocks.</summary>
    public static bool IsIce(Collider2D c) => instance != null && c != null && instance.blockByCollider.ContainsKey(c);

    // ------------------------------------------------------------------ queries

    /// <summary>What is underfoot at feet (the bottom-center of a character).</summary>
    public static SurfaceInfo Probe(Vector2 feet)
    {
        var info = new SurfaceInfo();
        if (instance == null) return info;
        var cx = Mathf.FloorToInt(feet.x / Cell);
        var row = Mathf.RoundToInt(feet.y * 16f);
        for (var r = row - 3; r <= row + 2; r++)
        {
            if (!instance.coats.TryGetValue(Key(cx, r), out var coat)) continue;
            info.found = true;
            info.coat = coat.type;
            info.charged = coat.charge > 0f;
            info.slippery = coat.type == SurfaceCoat.Frost || coat.type == SurfaceCoat.IceSheet;
            break;
        }
        // A block's top is 2 pixels above its center; blocks grown off a wall can sit off the grid,
        // so look along the whole cell.
        var fx = Mathf.RoundToInt(feet.x * 16f);
        for (var dx = -2; dx <= 2 && !info.onIceBlock; dx++)
        {
            for (var r = row - 3; r <= row + 2; r++)
            {
                if (!instance.blocks.TryGetValue(Key(fx + dx, r - 2), out var block)) continue;
                info.found = true;
                info.onIceBlock = true;
                info.slippery = true;
                info.charged |= block.charge > 0f;
                break;
            }
        }
        return info;
    }

    /// <summary>Casts down from a point to the first solid surface (not characters or loose objects).</summary>
    public static bool FindSurface(Vector2 from, float maxDistance, out RaycastHit2D surface)
    {
        surface = default;
        var self = Instance;
        if (self == null) return false;
        var n = Physics2D.Raycast(from, Vector2.down, self.solidFilter, self.hits, maxDistance);
        var best = float.MaxValue;
        for (var i = 0; i < n; i++)
        {
            var h = self.hits[i];
            if (!IsStatic(h.collider) || h.normal.y < 0.5f) continue;
            if (h.distance < best)
            {
                best = h.distance;
                surface = h;
            }
        }
        return best < float.MaxValue;
    }

    /// <summary>Solid ground: platforms, ice blocks, anything without a moving body.</summary>
    public static bool IsStatic(Collider2D c)
    {
        if (c == null || c.isTrigger) return false;
        var body = c.attachedRigidbody;
        return body == null || body.bodyType != RigidbodyType2D.Dynamic;
    }

    // ------------------------------------------------------------------ fire

    /// <summary>Fire touching the world at a hit. Melts ice, boils water, leaves embers.</summary>
    public static void ApplyFire(RaycastHit2D hit, float power)
    {
        var self = Instance;
        if (self == null) return;
        if (self.blockByCollider.TryGetValue(hit.collider, out var block))
        {
            block.hp -= power;
            if (Random.value < 0.3f) ElementFx.SteamPuff(hit.point, 1, 0.5f);
            if (block.hp <= 0f) self.MeltBlock(block, true);
            return;
        }
        if (hit.normal.y < 0.5f) return;
        self.FireOnSurface(hit.point, power);
    }

    void FireOnSurface(Vector2 point, float power)
    {
        var cx = Mathf.FloorToInt(point.x / Cell);
        var row = Mathf.RoundToInt(point.y * 16f);
        var coat = FindCoatNear(cx, row);
        if (coat == null)
        {
            Place(cx, row, SurfaceCoat.Embers, 1f, EmberLife * Random.Range(0.8f, 1.2f));
            return;
        }
        switch (coat.type)
        {
            case SurfaceCoat.Frost:
                coat.amount -= power * 2f;
                if (coat.amount <= 0f) Convert(coat, SurfaceCoat.Water, 0.6f, 12f);
                ElementFx.SteamPuff(Top(coat), 1, 0.6f);
                break;
            case SurfaceCoat.IceSheet:
                Convert(coat, SurfaceCoat.Water, 1f, coat.permanentWater ? 0f : 12f);
                ElementFx.SteamPuff(Top(coat), 2, 0.7f);
                break;
            case SurfaceCoat.Water:
                coat.amount -= power * 0.6f;
                if (Random.value < 0.5f) ElementFx.SteamPuff(Top(coat), 1, 0.8f);
                if (coat.amount <= 0f) Remove(coat);
                break;
            case SurfaceCoat.Embers:
                coat.timer = Mathf.Max(coat.timer, EmberLife * Random.Range(0.8f, 1.2f));
                coat.amount = Mathf.Min(1.5f, coat.amount + 0.1f);
                break;
        }
    }

    // ------------------------------------------------------------------ ice

    /// <summary>An ice shard striking the world. Frosts surfaces; enough frost or shards build blocks.</summary>
    public static void ApplyIce(RaycastHit2D hit, float mass)
    {
        var self = Instance;
        if (self == null) return;
        var n = hit.normal;
        if (self.blockByCollider.TryGetValue(hit.collider, out var block))
        {
            // Grow off whichever face was struck.
            if (n.y > 0.5f) self.Grow(block.px, block.py + CellPx, mass);
            else if (n.y < -0.5f) self.Grow(block.px, block.py - CellPx, mass * 0.7f);
            else self.Grow(block.px + (n.x > 0f ? CellPx : -CellPx), block.py, mass);
            return;
        }

        if (n.y > 0.5f)
        {
            var cx = Mathf.FloorToInt(hit.point.x / Cell);
            var row = Mathf.RoundToInt(hit.point.y * 16f);
            var coat = self.FindCoatNear(cx, row);
            if (coat == null)
            {
                self.Place(cx, row, SurfaceCoat.Frost, mass * 0.6f, FrostLife);
                return;
            }
            switch (coat.type)
            {
                case SurfaceCoat.Embers:
                    self.Remove(coat);
                    ElementFx.SteamPuff(Top(coat), 2, 0.6f);
                    break;
                case SurfaceCoat.Water:
                    self.Convert(coat, SurfaceCoat.IceSheet, 1f, coat.permanentWater ? 20f : FrostLife);
                    break;
                case SurfaceCoat.Frost:
                case SurfaceCoat.IceSheet:
                    coat.amount += mass * 0.6f;
                    coat.timer = Mathf.Max(coat.timer, FrostLife);
                    if (coat.amount >= 1.6f)
                    {
                        coat.amount = 1f;
                        self.Grow(cx * CellPx + 2, coat.row + 2, 1f);
                    }
                    break;
            }
            return;
        }

        // The side or underside of a platform: build out from it, flush against the face that was
        // struck (snapping to the grid would push the block into the platform).
        int px, py;
        if (Mathf.Abs(n.x) >= Mathf.Abs(n.y))
        {
            px = Mathf.RoundToInt((hit.point.x + Mathf.Sign(n.x) * Cell * 0.5f) * 16f);
            py = Mathf.FloorToInt(hit.point.y * 16f / CellPx) * CellPx + 2;
        }
        else
        {
            px = Mathf.FloorToInt(hit.point.x * 16f / CellPx) * CellPx + 2;
            py = Mathf.RoundToInt((hit.point.y - Cell * 0.5f) * 16f);
        }
        self.Grow(px, py, mass);
    }

    /// <summary>Freezes a puddle or frosts a surface at a point, as a blizzard does.</summary>
    public static void FrostAt(Vector2 surfacePoint, float amount)
    {
        var self = Instance;
        if (self == null) return;
        var cx = Mathf.FloorToInt(surfacePoint.x / Cell);
        var row = Mathf.RoundToInt(surfacePoint.y * 16f);
        var coat = self.FindCoatNear(cx, row);
        if (coat == null)
        {
            self.Place(cx, row, SurfaceCoat.Frost, amount, FrostLife);
            return;
        }
        if (coat.type == SurfaceCoat.Water) self.Convert(coat, SurfaceCoat.IceSheet, 1f, coat.permanentWater ? 20f : FrostLife);
        else if (coat.type == SurfaceCoat.Embers) self.Remove(coat);
        else
        {
            coat.amount = Mathf.Min(1.4f, coat.amount + amount);
            coat.timer = Mathf.Max(coat.timer, FrostLife);
        }
    }

    /// <summary>Builds a horizontal plate of ice blocks centered at a point, for charged frost.</summary>
    public static int BuildPlate(Vector2 center, int halfWidthCells, float life, float charge)
    {
        var self = Instance;
        if (self == null) return 0;
        var py = Mathf.FloorToInt(center.y * 16f / CellPx) * CellPx + 2;
        var pc = Mathf.FloorToInt(center.x * 16f / CellPx) * CellPx + 2;
        var made = 0;
        for (var i = -halfWidthCells; i <= halfWidthCells; i++)
        {
            var block = self.MakeBlock(pc + i * CellPx, py, life * Random.Range(0.9f, 1.1f));
            if (block == null) continue;
            block.charge = charge;
            made++;
        }
        return made;
    }

    void Grow(int px, int py, float mass)
    {
        var key = Key(px, py);
        if (blocks.ContainsKey(key)) return;
        pendingIce.TryGetValue(key, out var pending);
        pending += mass;
        if (pending < 1f)
        {
            pendingIce[key] = pending;
            // Show the ice starting to form there.
            ArcaneParticles.Emit(FxLayer.Glow, new Spark
            {
                pos = new Vector2(px * Px, py * Px) + Random.insideUnitCircle * 0.1f, life = 0.4f, size = 1, sizeEnd = 1,
                colorStart = Elements.IceWhite, colorMid = Elements.IcePale.WithAlpha(0.6f), colorEnd = Elements.IceCyan.WithAlpha(0f),
            });
            return;
        }
        pendingIce.Remove(key);
        MakeBlock(px, py, BlockLife * Random.Range(0.85f, 1.15f));
    }

    Block MakeBlock(int px, int py, float life)
    {
        var key = Key(px, py);
        if (blocks.ContainsKey(key)) return null;
        if (blocks.Count >= MaxBlocks) MeltOldest();

        var center = new Vector2(px * Px, py * Px);
        // Never trap a character or bury a block inside a platform.
        var n = Physics2D.OverlapBox(center, new Vector2(Cell * 0.7f, Cell * 0.7f), 0f, solidFilter, overlaps);
        for (var i = 0; i < n; i++)
        {
            var c = overlaps[i];
            if (blockByCollider.ContainsKey(c)) continue;
            var status = c.GetComponentInParent<ElementStatus>();
            if (status != null) status.ApplyChill(0.15f);
            return null;
        }

        var col = iceBody.AddComponent<BoxCollider2D>();
        col.size = new Vector2(Cell, Cell);
        col.offset = center;
        var block = new Block { px = px, py = py, col = col, life = life, timer = life, hp = 1.2f, seed = Random.value * 100f };
        blocks[key] = block;
        blockByCollider[col] = block;
        ElementFx.IceForm(center);
        return block;
    }

    void MeltOldest()
    {
        Block oldest = null;
        foreach (var b in blocks.Values)
            if (oldest == null || b.timer < oldest.timer) oldest = b;
        if (oldest != null) MeltBlock(oldest, false);
    }

    void MeltBlock(Block block, bool byFire)
    {
        blocks.Remove(Key(block.px, block.py));
        blockByCollider.Remove(block.col);
        Destroy(block.col);
        var center = block.Center;
        if (byFire)
        {
            ElementFx.SteamPuff(center, 3, 0.9f);
            // The meltwater pools on whatever is below.
            if (FindSurface(center, 3f, out var below)) WaterAt(below.point, 0.5f, 10f);
        }
        else
        {
            for (var i = 0; i < 4; i++)
            {
                ArcaneParticles.Emit(FxLayer.Front, new Spark
                {
                    pos = center + Random.insideUnitCircle * 0.12f, vel = new Vector2(Random.Range(-0.3f, 0.3f), 0f),
                    life = 0.7f, size = 1, sizeEnd = 1, gravity = 14f,
                    colorStart = Elements.IcePale, colorMid = Elements.WaterShine, colorEnd = Elements.WaterMid.WithAlpha(0f),
                });
            }
        }
    }

    // ------------------------------------------------------------------ water and lightning

    /// <summary>Lays water on the surface at a point.</summary>
    public static void WaterAt(Vector2 surfacePoint, float amount, float life)
    {
        var self = Instance;
        if (self == null) return;
        var cx = Mathf.FloorToInt(surfacePoint.x / Cell);
        var row = Mathf.RoundToInt(surfacePoint.y * 16f);
        var coat = self.FindCoatNear(cx, row);
        if (coat == null) self.Place(cx, row, SurfaceCoat.Water, amount, life);
        else if (coat.type == SurfaceCoat.Embers)
        {
            self.Remove(coat);
            ElementFx.SteamPuff(Top(coat), 2, 0.7f);
        }
        else if (coat.type == SurfaceCoat.Water) coat.amount = Mathf.Min(1.5f, coat.amount + amount);
    }

    /// <summary>A permanent puddle across a stretch of platform top, for level dressing.</summary>
    public static void AddPuddle(float x0, float x1, float surfaceY)
    {
        var self = Instance;
        if (self == null) return;
        var row = Mathf.RoundToInt(surfaceY * 16f);
        for (var cx = Mathf.CeilToInt(x0 / Cell); (cx + 1) * Cell <= x1; cx++)
        {
            var coat = self.Place(cx, row, SurfaceCoat.Water, 1f, 0f);
            if (coat != null) coat.permanentWater = true;
        }
    }

    /// <summary>
    /// Lightning striking a point: charges any conductive coat or ice there, and the current spreads
    /// through everything conductive that touches it. Returns how many cells were charged.
    /// </summary>
    public static int Electrify(Vector2 point, Collider2D struck, float seconds = ChargeLife)
    {
        var self = Instance;
        if (self == null) return 0;
        self.flood.Clear();
        self.seen.Clear();

        if (struck != null && self.blockByCollider.TryGetValue(struck, out var block))
            self.Enqueue(1, block.px, block.py);
        else
        {
            var cx = Mathf.FloorToInt(point.x / Cell);
            var row = Mathf.RoundToInt(point.y * 16f);
            for (var dx = -1; dx <= 1; dx++)
            {
                var coat = self.FindCoatNear(cx + dx, row);
                if (coat != null && Conductive(coat.type)) self.Enqueue(0, coat.cx, coat.row);
            }
        }

        var charged = 0;
        while (self.flood.Count > 0 && charged < 80)
        {
            var (kind, a, b) = self.flood.Dequeue();
            var isBlock = kind == 1;
            charged++;
            if (isBlock)
            {
                if (!self.blocks.TryGetValue(Key(a, b), out var bl)) continue;
                bl.charge = Mathf.Max(bl.charge, seconds);
                for (var d = -1; d <= 1; d += 2)
                {
                    self.Enqueue(1, a + d * CellPx, b);
                    self.Enqueue(1, a, b + d * CellPx);
                }
                // A coat lying on top of the block.
                self.EnqueueCoat((a - 2) / CellPx, b + 2);
            }
            else
            {
                if (!self.coats.TryGetValue(Key(a, b), out var coat)) continue;
                coat.charge = Mathf.Max(coat.charge, seconds);
                for (var d = -1; d <= 1; d += 2) self.EnqueueCoat(a + d, b);
                // The block the coat lies on, if it is ice.
                self.Enqueue(1, a * CellPx + 2, b - 2);
            }
        }
        return charged;
    }

    static bool Conductive(SurfaceCoat t) => t == SurfaceCoat.Water || t == SurfaceCoat.IceSheet || t == SurfaceCoat.Frost;

    void EnqueueCoat(int cx, int row)
    {
        for (var r = row - 2; r <= row + 2; r++)
        {
            if (coats.TryGetValue(Key(cx, r), out var coat) && Conductive(coat.type))
            {
                Enqueue(0, cx, r);
                return;
            }
        }
    }

    void Enqueue(int kind, int a, int b)
    {
        var key = Key(a, b);
        if (kind == 1 && !blocks.ContainsKey(key)) return;
        if (kind == 0 && !coats.ContainsKey(key)) return;
        if (seen.Add((kind, a, b))) flood.Enqueue((kind, a, b));
    }

    // ------------------------------------------------------------------ coat bookkeeping

    Coat FindCoatNear(int cx, int row)
    {
        for (var r = row - 2; r <= row + 2; r++)
            if (coats.TryGetValue(Key(cx, r), out var coat)) return coat;
        return null;
    }

    Coat Place(int cx, int row, SurfaceCoat type, float amount, float life)
    {
        if (coats.Count >= MaxCoats) return null;
        var coat = new Coat { cx = cx, row = row, type = type, amount = amount, timer = life, seed = Random.value * 100f };
        coats[Key(cx, row)] = coat;
        return coat;
    }

    void Convert(Coat coat, SurfaceCoat type, float amount, float life)
    {
        coat.type = type;
        coat.amount = amount;
        coat.timer = life;
    }

    void Remove(Coat coat) => coats.Remove(Key(coat.cx, coat.row));

    static Vector2 Top(Coat coat) => new((coat.cx + 0.5f) * Cell, coat.row * Px + 0.05f);

    // ------------------------------------------------------------------ update and drawing

    void Update()
    {
        var dt = Time.deltaTime;
        var time = Time.time;

        scratch.Clear();
        foreach (var pair in coats)
        {
            var coat = pair.Value;
            coat.charge = Mathf.Max(0f, coat.charge - dt);
            if (!(coat.type == SurfaceCoat.Water && coat.permanentWater) && coat.timer > 0f)
            {
                coat.timer -= dt;
                if (coat.timer <= 0f)
                {
                    if (coat.type == SurfaceCoat.IceSheet && coat.permanentWater) Convert(coat, SurfaceCoat.Water, 1f, 0f);
                    else if (coat.type == SurfaceCoat.Frost && coat.amount > 0.8f) Convert(coat, SurfaceCoat.Water, 0.5f, 8f);
                    else
                    {
                        scratch.Add(pair.Key);
                        continue;
                    }
                }
            }
            DrawCoat(coat, time);
        }
        foreach (var key in scratch) coats.Remove(key);

        EmberUpdrafts(dt);

        Block expired = null;
        foreach (var block in blocks.Values)
        {
            block.charge = Mathf.Max(0f, block.charge - dt);
            block.timer -= dt;
            if (block.timer <= 0f && expired == null) expired = block;
            DrawBlock(block, time);
        }
        if (expired != null) MeltBlock(expired, false);

        pruneTimer -= dt;
        if (pruneTimer <= 0f)
        {
            pruneTimer = 2f;
            PruneBehindCamera();
        }
    }

    // Runs of embers make columns of rising heat, and lick up flames and smoke.
    readonly List<Coat> embers = new();

    void EmberUpdrafts(float dt)
    {
        embers.Clear();
        foreach (var coat in coats.Values)
            if (coat.type == SurfaceCoat.Embers) embers.Add(coat);
        if (embers.Count == 0) return;
        embers.Sort((a, b) => a.row != b.row ? a.row.CompareTo(b.row) : a.cx.CompareTo(b.cx));

        var start = 0;
        for (var i = 1; i <= embers.Count; i++)
        {
            if (i < embers.Count && embers[i].row == embers[start].row && embers[i].cx == embers[i - 1].cx + 1) continue;
            var first = embers[start];
            var last = embers[i - 1];
            var x0 = first.cx * Cell;
            var x1 = (last.cx + 1) * Cell;
            var y = first.row * Px;
            var heat = Mathf.Clamp01((i - start) / 6f);
            // Strong enough to beat gravity near the embers and carry the wizard up a few units,
            // where it fades and he hangs: a boost to a higher ledge.
            AirField.AddColumn(new Vector2((x0 + x1) * 0.5f, y), (x1 - x0) * 0.5f + 0.35f, 3.2f + heat * 1.5f, 16f + heat * 8f);
            start = i;
        }

        // Fan the flames: wind spreads embers along the ground.
        foreach (var coat in embers)
        {
            var top = Top(coat);
            var air = AirField.At(top + Vector2.up * 0.2f);
            if (Mathf.Abs(air.x) > 6f && Random.value < dt * 0.8f)
            {
                var next = coat.cx + (air.x > 0f ? 1 : -1);
                if (FindCoatNear(next, coat.row) == null && FindSurface(new Vector2((next + 0.5f) * Cell, top.y + 0.2f), 0.4f, out _))
                    Place(next, coat.row, SurfaceCoat.Embers, 0.8f, EmberLife * 0.7f);
                coat.timer = Mathf.Max(coat.timer, 1.5f);
            }
        }
    }

    void DrawCoat(Coat coat, float time)
    {
        var x0 = coat.cx * Cell;
        var y = coat.row * Px;
        switch (coat.type)
        {
            case SurfaceCoat.Frost:
            {
                var thick = 1 + Mathf.Clamp(Mathf.FloorToInt(coat.amount * 2f), 0, 2);
                var fade = Mathf.Clamp01(coat.timer / 2f);
                for (var c = 0; c < CellPx; c++)
                {
                    var h = thick + (Hash(coat.cx * 7 + c) > 0.6f ? 1 : 0);
                    var x = x0 + c * Px;
                    ArcaneParticles.DrawRect(FxLayer.Back, new Vector2(x, y), new Vector2(x + Px, y + h * Px), Elements.IcePale.WithAlpha(0.85f * fade));
                    if (Hash(coat.cx * 13 + c) > 0.55f)
                        ArcaneParticles.DrawRect(FxLayer.Back, new Vector2(x, y + (h - 1) * Px), new Vector2(x + Px, y + h * Px), Elements.IceWhite.WithAlpha(0.9f * fade));
                }
                if (Random.value < 0.006f)
                    ElementFx.Glint(new Vector2(x0 + Random.Range(0, CellPx) * Px, y + thick * Px), 0.35f);
                break;
            }
            case SurfaceCoat.IceSheet:
            {
                ArcaneParticles.DrawRect(FxLayer.Back, new Vector2(x0, y), new Vector2(x0 + Cell, y + 2 * Px), Elements.IceCyan.WithAlpha(0.9f));
                ArcaneParticles.DrawRect(FxLayer.Back, new Vector2(x0, y + Px), new Vector2(x0 + Cell, y + 2 * Px), Elements.IcePale.WithAlpha(0.9f));
                if (Hash(coat.cx) > 0.5f)
                    ArcaneParticles.DrawRect(FxLayer.Back, new Vector2(x0 + Px, y + Px), new Vector2(x0 + 2 * Px, y + 2 * Px), Elements.IceWhite);
                if (Random.value < 0.004f) ElementFx.Glint(new Vector2(x0 + 2 * Px, y + 2 * Px), 0.3f);
                break;
            }
            case SurfaceCoat.Water:
            {
                var depth = coat.amount > 0.7f ? 2 : 1;
                var fade = coat.permanentWater ? 1f : Mathf.Clamp01(coat.timer / 2f);
                ArcaneParticles.DrawRect(FxLayer.Back, new Vector2(x0, y), new Vector2(x0 + Cell, y + depth * Px), Elements.WaterDark.WithAlpha(0.9f * fade));
                ArcaneParticles.DrawRect(FxLayer.Back, new Vector2(x0, y + (depth - 1) * Px), new Vector2(x0 + Cell, y + depth * Px), Elements.WaterMid.WithAlpha(0.9f * fade));
                // A glint of moonlight sliding over the surface.
                for (var c = 0; c < CellPx; c++)
                {
                    var phase = Mathf.Repeat(time * 6f + (coat.cx * CellPx + c) * 0.9f + Mathf.Sin(time * 1.3f + coat.cx) * 3f, 19f);
                    if (phase < 1f)
                    {
                        var x = x0 + c * Px;
                        ArcaneParticles.DrawRect(FxLayer.Back, new Vector2(x, y + (depth - 1) * Px), new Vector2(x + Px, y + depth * Px), Elements.WaterShine.WithAlpha(fade));
                    }
                }
                break;
            }
            case SurfaceCoat.Embers:
            {
                var life = Mathf.Clamp01(coat.timer / 1.5f);
                for (var c = 0; c < CellPx; c++)
                {
                    var x = x0 + c * Px;
                    ArcaneParticles.DrawRect(FxLayer.Back, new Vector2(x, y), new Vector2(x + Px, y + Px), Elements.Soot.WithAlpha(0.9f));
                    var heat = Mathf.PerlinNoise(time * 3f + coat.seed + c * 0.7f, coat.cx * 0.31f);
                    heat *= life;
                    if (heat > 0.25f)
                    {
                        var col = heat > 0.65f ? Elements.FireYellow : heat > 0.45f ? Elements.FireOrange : Elements.FireRed;
                        ArcaneParticles.DrawPixel(FxLayer.Glow, new Vector2(x + Px * 0.5f, y + Px * 0.5f), 1, col.WithAlpha(heat));
                    }
                }
                if (Random.value < 0.22f * life) ElementFx.EmberLick(new Vector2(x0 + Random.Range(0f, Cell), y + Px), life);
                break;
            }
        }

        if (coat.charge > 0f)
        {
            var k = Mathf.Clamp01(coat.charge / 0.6f);
            for (var c = 0; c < CellPx; c++)
            {
                if (Random.value > 0.35f) continue;
                var x = x0 + c * Px;
                ArcaneParticles.DrawPixel(FxLayer.Glow, new Vector2(x + Px * 0.5f, y + Px * (1.5f + Random.Range(0, 3))), 1,
                                          (Random.value < 0.3f ? Elements.BoltCore : Elements.BoltViolet).WithAlpha(k));
            }
            if (Random.value < 0.05f * k)
            {
                var from = new Vector2(x0 + Random.Range(0f, Cell), y + Px * 2f);
                LightningBolts.Spawn(from, from + new Vector2(Random.Range(-0.6f, 0.6f), Random.Range(0.1f, 0.5f)), 0.07f, 1f, false);
            }
        }
    }

    void DrawBlock(Block block, float time)
    {
        var c = block.Center;
        var min = c - new Vector2(Cell, Cell) * 0.5f;
        var max = c + new Vector2(Cell, Cell) * 0.5f;
        var fade = Mathf.Clamp01(block.timer / 2.5f);
        var a = 0.35f + 0.55f * fade;

        bool up = blocks.ContainsKey(Key(block.px, block.py + CellPx));
        bool down = blocks.ContainsKey(Key(block.px, block.py - CellPx));
        bool left = blocks.ContainsKey(Key(block.px - CellPx, block.py));
        bool right = blocks.ContainsKey(Key(block.px + CellPx, block.py));

        ArcaneParticles.DrawRect(FxLayer.Back, min, max, Elements.IceCyan.WithAlpha(a * 0.85f));
        // Shade the open faces so a mass of blocks reads as one chunk of ice.
        if (!down) ArcaneParticles.DrawRect(FxLayer.Back, min, new Vector2(max.x, min.y + Px), Elements.IceDeep.WithAlpha(a));
        if (!right) ArcaneParticles.DrawRect(FxLayer.Back, new Vector2(max.x - Px, min.y), max, Elements.IceDeep.WithAlpha(a * 0.9f));
        if (!up) ArcaneParticles.DrawRect(FxLayer.Back, new Vector2(min.x, max.y - Px), max, Elements.IcePale.WithAlpha(a));
        if (!left) ArcaneParticles.DrawRect(FxLayer.Back, min, new Vector2(min.x + Px, max.y), Elements.IcePale.WithAlpha(a * 0.8f));
        if (!up && !left) ArcaneParticles.DrawRect(FxLayer.Back, new Vector2(min.x, max.y - Px), new Vector2(min.x + Px, max.y), Elements.IceWhite.WithAlpha(a));
        // A fracture line or a bright inclusion inside some blocks.
        var h = Hash(block.px * 31 + block.py * 17);
        if (h > 0.7f) ArcaneParticles.DrawRect(FxLayer.Back, min + new Vector2(Px, Px * 2f), min + new Vector2(Px * 3f, Px * 3f), Elements.IcePale.WithAlpha(a));
        else if (h < 0.2f) ArcaneParticles.DrawRect(FxLayer.Back, min + new Vector2(Px * 2f, Px), min + new Vector2(Px * 3f, Px * 2f), Elements.IceWhite.WithAlpha(a));

        if (Random.value < 0.004f) ElementFx.Glint(c + new Vector2(Random.Range(-1, 2), Random.Range(-1, 2)) * Px, 0.4f);
        if (fade < 1f && Random.value < 0.02f)
        {
            ArcaneParticles.Emit(FxLayer.Front, new Spark
            {
                pos = new Vector2(Random.Range(min.x, max.x), min.y), life = 0.6f, size = 1, sizeEnd = 1, gravity = 14f,
                colorStart = Elements.IcePale, colorMid = Elements.WaterShine, colorEnd = Elements.WaterMid.WithAlpha(0f),
            });
        }
        if (block.charge > 0f)
        {
            var k = Mathf.Clamp01(block.charge / 0.6f);
            ArcaneParticles.DrawRect(FxLayer.Glow, min + new Vector2(Px, Px), max - new Vector2(Px, Px), Elements.BoltViolet.WithAlpha(0.25f * k));
            if (Random.value < 0.25f)
                ArcaneParticles.DrawPixel(FxLayer.Glow, new Vector2(Random.Range(min.x, max.x), Random.Range(min.y, max.y)), 1, Elements.BoltCore.WithAlpha(k));
            if (Random.value < 0.03f * k)
                LightningBolts.Spawn(c, c + Random.insideUnitCircle.normalized * Random.Range(0.3f, 0.7f), 0.06f, 1f, false);
        }
    }

    void PruneBehindCamera()
    {
        var cam = Camera.main;
        if (cam == null) return;
        var left = cam.transform.position.x - cam.orthographicSize * cam.aspect - 25f;
        scratch.Clear();
        foreach (var pair in coats)
            if ((pair.Value.cx + 1) * Cell < left) scratch.Add(pair.Key);
        foreach (var key in scratch) coats.Remove(key);

        var old = new List<Block>();
        foreach (var block in blocks.Values)
            if (block.Center.x < left) old.Add(block);
        foreach (var block in old) MeltBlock(block, false);
    }

    static float Hash(int n)
    {
        var h = Mathf.Sin(n * 12.9898f) * 43758.5453f;
        return h - Mathf.Floor(h);
    }
}
