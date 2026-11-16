using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Draws lightning: jagged bolts built by midpoint displacement, with forking branches, drawn as
/// stepped pixel lines (a white core in a violet sheath). Every few frames each bolt re-rolls its
/// zigzag so it crawls and crackles, then it fades to a violet afterimage. Bolts are only
/// visuals; whoever spawns one deals the damage.
/// </summary>
[DefaultExecutionOrder(900)]
public class LightningBolts : MonoBehaviour
{
    const int MaxBolts = 160;
    const float RerollSeconds = 0.035f;

    class Bolt
    {
        public Vector2 from, to;
        public float age, life, width, reroll;
        public bool branches;
        public readonly List<Vector2> main = new();
        public readonly List<List<Vector2>> forks = new();
    }

    static LightningBolts instance;
    readonly List<Bolt> bolts = new();
    readonly Stack<Bolt> spare = new();

    static LightningBolts Instance
    {
        get
        {
            if (instance == null && Application.isPlaying) new GameObject("LightningBolts").AddComponent<LightningBolts>();
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
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    /// <summary>
    /// A bolt from one point to another lasting life seconds. width 1 is a stream bolt; 2 or more
    /// is a heavy strike. branches adds forks.
    /// </summary>
    public static void Spawn(Vector2 from, Vector2 to, float life, float width = 1f, bool branches = true)
    {
        var self = Instance;
        if (self == null) return;
        if (self.bolts.Count >= MaxBolts) self.Recycle(0);
        var bolt = self.spare.Count > 0 ? self.spare.Pop() : new Bolt();
        bolt.from = from;
        bolt.to = to;
        bolt.age = 0f;
        bolt.life = life;
        bolt.width = width;
        bolt.branches = branches;
        bolt.reroll = 0f;
        Build(bolt);
        self.bolts.Add(bolt);
    }

    /// <summary>Fills points with a jagged path from a to b.</summary>
    public static void Jag(List<Vector2> points, Vector2 a, Vector2 b, float roughness = 0.2f)
    {
        points.Clear();
        points.Add(a);
        points.Add(b);
        var length = Vector2.Distance(a, b);
        var levels = length < 0.6f ? 2 : length < 2f ? 3 : length < 5f ? 4 : 5;
        var displace = length * roughness;
        for (var level = 0; level < levels; level++)
        {
            for (var i = points.Count - 1; i > 0; i--)
            {
                var p0 = points[i - 1];
                var p1 = points[i];
                var d = p1 - p0;
                var perp = new Vector2(-d.y, d.x).normalized;
                points.Insert(i, (p0 + p1) * 0.5f + perp * Random.Range(-displace, displace));
            }
            displace *= 0.5f;
        }
    }

    static void Build(Bolt bolt)
    {
        Jag(bolt.main, bolt.from, bolt.to);
        foreach (var fork in bolt.forks) fork.Clear();
        if (!bolt.branches || bolt.main.Count < 5) return;

        var forks = Random.Range(1, 4);
        while (bolt.forks.Count < forks) bolt.forks.Add(new List<Vector2>());
        for (var f = 0; f < forks; f++)
        {
            var i = Random.Range(bolt.main.Count / 5, bolt.main.Count * 4 / 5);
            var start = bolt.main[i];
            var along = (bolt.main[Mathf.Min(i + 1, bolt.main.Count - 1)] - start).normalized;
            var angle = Random.Range(25f, 55f) * (Random.value < 0.5f ? -1f : 1f);
            var dir = Quaternion.Euler(0f, 0f, angle) * along;
            var length = Vector2.Distance(start, bolt.to) * Random.Range(0.18f, 0.45f);
            Jag(bolt.forks[f], start, start + (Vector2)dir * length, 0.28f);
        }
    }

    void Recycle(int index)
    {
        spare.Push(bolts[index]);
        bolts.RemoveAt(index);
    }

    void LateUpdate()
    {
        var dt = Time.deltaTime;
        for (var i = bolts.Count - 1; i >= 0; i--)
        {
            var bolt = bolts[i];
            bolt.age += dt;
            if (bolt.age >= bolt.life)
            {
                Recycle(i);
                continue;
            }
            bolt.reroll -= dt;
            if (bolt.reroll <= 0f && bolt.age < bolt.life * 0.6f)
            {
                bolt.reroll = RerollSeconds;
                Build(bolt);
            }
            Draw(bolt);
        }
    }

    static void Draw(Bolt bolt)
    {
        var t = bolt.age / bolt.life;
        // Bright while live, then a dimming violet afterimage.
        var live = t < 0.6f;
        var fade = live ? 1f : 1f - (t - 0.6f) / 0.4f;
        var core = live ? Elements.BoltCore : Color.Lerp(Elements.BoltViolet, Elements.BoltDeep, 1f - fade);
        core.a = live ? 1f : fade * 0.8f;
        var sheath = Elements.BoltViolet.WithAlpha((live ? 0.55f : 0.3f) * fade);
        var halo = Elements.BoltBlue.WithAlpha(0.22f * fade);
        var coreWidth = Mathf.Max(1, Mathf.RoundToInt(bolt.width));
        var sheathWidth = coreWidth + 2;

        DrawPath(bolt.main, sheathWidth, sheath, coreWidth, core);
        foreach (var fork in bolt.forks)
        {
            if (fork.Count == 0) continue;
            DrawPath(fork, Mathf.Max(1, sheathWidth - 1), sheath.WithAlpha(sheath.a * 0.8f),
                     Mathf.Max(1, coreWidth - 1), core.WithAlpha(core.a * 0.85f));
        }
        if (live)
        {
            // Soft glow strung along the bolt.
            for (var i = 0; i < bolt.main.Count; i += 4)
                ArcaneParticles.DrawHalo(FxLayer.Glow, bolt.main[i], 10f * bolt.width, halo);
            ArcaneParticles.DrawHalo(FxLayer.Glow, bolt.to, 14f * bolt.width, halo.WithAlpha(halo.a * 1.6f));
        }
    }

    static void DrawPath(List<Vector2> points, int sheathWidth, Color sheath, int coreWidth, Color core)
    {
        for (var i = 1; i < points.Count; i++)
            ArcaneParticles.DrawLine(FxLayer.Glow, points[i - 1], points[i], sheathWidth, sheath);
        for (var i = 1; i < points.Count; i++)
            ArcaneParticles.DrawLine(FxLayer.Glow, points[i - 1], points[i], coreWidth, core);
    }
}
