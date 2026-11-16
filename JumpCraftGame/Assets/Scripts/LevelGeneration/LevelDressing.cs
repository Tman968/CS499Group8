using UnityEngine;

/// <summary>
/// Furnishes each new platform from PlatformGenerator with things the wands can work on: black
/// puddles (freeze them, boil them, electrify them), rotten crates and bramble thickets (burn
/// them, blow crates away), swinging pendulum blades (slow them with ice), skeletons, and in some
/// gaps a dormant rune lift that lightning brings to life.
///
/// Platforms are split into three slots so things don't land on top of each other. Everything is
/// parented to its platform, so it is cleaned up with it.
/// </summary>
public class LevelDressing : MonoBehaviour
{
    public static LevelDressing Instance { get; private set; }

    [Header("Art (Art/Sprites/Props)")]
    public Texture2D crateArt;
    public Texture2D brambleArt;
    public Texture2D bladeArt;
    public Texture2D liftArt;

    [Header("Chances per platform")]
    [Range(0f, 1f)] public float puddleChance = 0.35f;
    [Range(0f, 1f)] public float crateChance = 0.25f;
    [Range(0f, 1f)] public float brambleChance = 0.15f;
    [Range(0f, 1f)] public float pendulumChance = 0.15f;
    [Range(0f, 1f)] public float skeletonChance = 0.3f;
    [Tooltip("Chance of a rune lift in the gap before a platform.")]
    [Range(0f, 1f)] public float liftChance = 0.3f;
    [Tooltip("Platforms after the start that are left bare, to ease the player in.")]
    public int barePlatforms = 1;

    GameObject skeletonTemplate;
    int platformsSeen;

    void Awake() => Instance = this;

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start() => EnsureTemplate();

    // Keeps an inactive copy of the scene's skeleton to spawn more from. Made on first use, since
    // PlatformGenerator may furnish its first platforms before this object's Start.
    void EnsureTemplate()
    {
        if (skeletonTemplate != null) return;
        var original = FindAnyObjectByType<Skeleton>();
        if (original == null) return;
        var wasActive = original.gameObject.activeSelf;
        original.gameObject.SetActive(false);
        skeletonTemplate = Instantiate(original.gameObject, transform);
        skeletonTemplate.name = "SkeletonTemplate";
        original.gameObject.SetActive(wasActive);
    }

    /// <summary>Furnishes a platform just spawned, and maybe the gap before it.</summary>
    public void Decorate(GameObject platform, GameObject previous)
    {
        platformsSeen++;
        if (platformsSeen <= barePlatforms) return;
        EnsureTemplate();
        Physics2D.SyncTransforms();
        var col = platform.GetComponentInChildren<Collider2D>();
        if (col == null) return;
        var b = col.bounds;
        var top = b.max.y;
        var slotWidth = b.size.x / 3f;
        var free = new[] { true, true, true };
        float SlotX(int slot) => b.min.x + slotWidth * (slot + 0.5f);
        int TakeSlot()
        {
            var start = Random.Range(0, 3);
            for (var i = 0; i < 3; i++)
            {
                var s = (start + i) % 3;
                if (!free[s]) continue;
                free[s] = false;
                return s;
            }
            return -1;
        }

        if (Random.value < puddleChance)
        {
            var width = Random.Range(1f, Mathf.Min(2.4f, b.size.x - 0.4f));
            var x0 = Random.Range(b.min.x + 0.2f, b.max.x - 0.2f - width);
            SurfaceGrid.AddPuddle(x0, x0 + width, top);
        }
        if (brambleArt != null && Random.value < brambleChance)
        {
            var slot = TakeSlot();
            if (slot >= 0) FlammableProp.Create(FlammableProp.Kind.Bramble, brambleArt, SlotX(slot), top, platform.transform);
        }
        if (crateArt != null && Random.value < crateChance)
        {
            var slot = TakeSlot();
            if (slot >= 0)
            {
                FlammableProp.Create(FlammableProp.Kind.Crate, crateArt, SlotX(slot), top, platform.transform);
                if (Random.value < 0.35f)
                    FlammableProp.Create(FlammableProp.Kind.Crate, crateArt, SlotX(slot), top + 1.05f, platform.transform);
            }
        }
        if (bladeArt != null && Random.value < pendulumChance)
            PendulumHazard.Create(bladeArt, new Vector2(b.center.x + Random.Range(-0.5f, 0.5f), top + 5.2f), 3.2f, platform.transform);
        if (skeletonTemplate != null && Random.value < skeletonChance)
        {
            var slot = TakeSlot();
            if (slot >= 0) SpawnSkeleton(new Vector2(SlotX(slot), top + 0.3f), platform.transform);
        }

        if (liftArt != null && previous != null && Random.value < liftChance) AddLift(previous, b, platform.transform);
    }

    void AddLift(GameObject previous, Bounds next, Transform parent)
    {
        var prevCol = previous.GetComponentInChildren<Collider2D>();
        if (prevCol == null) return;
        var prev = prevCol.bounds;
        var gapLeft = prev.max.x;
        var gapRight = next.min.x;
        var liftWidth = liftArt.width / 16f;
        if (gapRight - gapLeft < liftWidth + 0.4f) return;
        var lowY = Mathf.Min(prev.max.y, next.max.y) - 2f;
        var highY = Mathf.Max(prev.max.y, next.max.y) + 2.6f;
        RuneLift.Create(liftArt, (gapLeft + gapRight) * 0.5f, lowY, highY, parent);
    }

    // ------------------------------------------------------------------ spawning on demand (the test bench)

    public GameObject SpawnSkeleton(Vector2 at, Transform parent = null)
    {
        EnsureTemplate();
        if (skeletonTemplate == null) return null;
        var skeleton = Instantiate(skeletonTemplate, at, Quaternion.identity, parent);
        skeleton.name = "Skeleton";
        skeleton.SetActive(true);
        return skeleton;
    }

    public void SpawnCrate(Vector2 at) => FlammableProp.Create(FlammableProp.Kind.Crate, crateArt, at.x, at.y, null);

    public void SpawnBramble(Vector2 at) => FlammableProp.Create(FlammableProp.Kind.Bramble, brambleArt, at.x, at.y, null);

    public void SpawnPendulum(Vector2 surface) => PendulumHazard.Create(bladeArt, surface + new Vector2(0f, 5.2f), 3.2f, null);

    public void SpawnLift(Vector2 surface) => RuneLift.Create(liftArt, surface.x, surface.y - 1.5f, surface.y + 3f, null);
}
