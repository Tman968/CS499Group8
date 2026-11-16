using UnityEngine;

/// <summary>
/// The one place gameplay code goes to make a sound. Names are the files in Resources/Audio (see
/// SoundBank for how each is mixed).
///
///   Sfx.Play("jump", feet);                       a one-shot at a place in the world
///   Sfx.PlayFlat("ui_click");                     a one-shot that isn't anywhere (UI, fanfares)
///   Sfx.Loop(this, "burn_loop", center, 0.8f);    call every frame to keep a loop going; stop
///                                                 calling and it fades out on its own
/// </summary>
public static class Sfx
{
    public static void Play(string name, Vector2 position, float volume = 1f, float pitch = 1f)
    {
        if (AudioDirector.Instance != null) AudioDirector.Instance.Play(name, position, volume, pitch, false);
    }

    public static void PlayFlat(string name, float volume = 1f, float pitch = 1f)
    {
        if (AudioDirector.Instance != null) AudioDirector.Instance.Play(name, Vector2.zero, volume, pitch, true);
    }

    /// <summary>Keeps a looping sound going for owner while called each frame (from Update, not FixedUpdate).</summary>
    public static void Loop(object owner, string name, Vector2 position, float volume = 1f, float pitch = 1f)
    {
        if (AudioDirector.Instance != null) AudioDirector.Instance.Loop(owner, name, position, volume, pitch);
    }

    /// <summary>A footfall, sounding like whatever is underfoot: stone, frost, a puddle or embers.</summary>
    public static void Footstep(Vector2 feet, float volume = 1f)
    {
        var name = "step_stone";
        var under = SurfaceGrid.Probe(feet);
        if (under.found)
        {
            if (under.coat == SurfaceCoat.Water) name = "step_water";
            else if (under.coat == SurfaceCoat.Embers) name = "step_embers";
            else if (under.slippery) name = "step_ice";
        }
        Play(name, feet, volume);
    }

    /// <summary>Lowercase element names, for building sound names without allocating each frame.</summary>
    public static readonly string[] ElementKey = { "fire", "ice", "lightning", "wind" };
}
