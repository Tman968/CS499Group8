using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

/// <summary>
/// Plays and mixes every sound effect. It creates itself before the first scene loads and lives
/// for the whole session, so a sound started as a scene ends (a menu click, a death sting) plays out.
///
/// The mix is built to stay clear however much is going on:
///   * Voice limits: each sound may only play so many times at once, and can't restart within its
///     cooldown; when the pool is full, important sounds take voices from unimportant ones.
///   * Repeat softening: the same sound fired rapidly (a stream of shards striking) gets quieter
///     with each repeat instead of stacking up.
///   * Bus compression: each bus (Player, Enemy, World, UI) is turned down as more of its sounds
///     pile up, and the whole mix is compressed again on top, so a hundred effects at once are
///     dense, not deafening.
///   * Ducking: while the player is attacking (or hurt), the Enemy and World buses, the ambience
///     and the music dip a few decibels, so the wizard's own spells always cut through.
///   * Placement: sounds are panned across the screen and fade out past its edges.
/// The engine does all of this itself rather than through an AudioMixer, so it behaves the same in
/// WebGL builds, which have no mixer effects.
/// </summary>
[DefaultExecutionOrder(1000)]
public class AudioDirector : MonoBehaviour
{
    public static AudioDirector Instance { get; private set; }

    const int Voices = 40;
    const int MaxLoops = 28;
    const float LoopTimeout = 0.1f;     // a loop not refreshed for this long starts to fade out

    // Per bus: the loudness (in combined amplitude) above which it is compressed.
    static readonly float[] BusKnee = { 1.1f, 0.75f, 0.9f, 1.2f };
    // Per bus: how far it dips (0..1) under foreground sounds.
    static readonly float[] BusDuckDepth = { 0f, 0.45f, 0.38f, 0f };
    const float MasterKnee = 1.3f;
    const float Ratio = 0.7f;           // 0 = no compression, 1 = a hard ceiling

    /// <summary>How hard the player's foreground sounds are ducking the rest right now (0..1).</summary>
    public static float Duck { get; private set; }

    public static float SfxVolume
    {
        get => PlayerPrefs.GetFloat("SfxVolume", 1f);
        set => PlayerPrefs.SetFloat("SfxVolume", Mathf.Clamp01(value));
    }

    class Voice
    {
        public AudioSource src;
        public SoundDef def;
        public float gain;          // the def's volume, the caller's volume and repeat softening
        public Vector2 pos;
        public bool flat;
        public float started;
        public float level;         // last computed level before bus gain, for metering
        // Loops only.
        public long key;
        public float fade;          // 0..1, fading in and out
        public float target;
        public float touched;
        public float pitch = 1f;
    }

    readonly List<Voice> voices = new();
    readonly Dictionary<long, Voice> loops = new();
    readonly List<long> deadLoops = new();
    readonly Stack<AudioSource> spareLoopSources = new();
    readonly float[] busGain = { 1f, 1f, 1f, 1f };
    readonly float[] busLoad = new float[4];
    float masterGain = 1f;
    float sfxVolume = 1f;
    Camera cam;
    int camFrame = -1;
    bool paused;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        if (Instance != null) return;
        var go = new GameObject("Audio");
        DontDestroyOnLoad(go);
        go.AddComponent<AudioDirector>();
        go.AddComponent<MusicDirector>();
        go.AddComponent<UiSounds>();
    }

    void Awake()
    {
        Instance = this;
        sfxVolume = SfxVolume;
        for (var i = 0; i < Voices; i++) voices.Add(new Voice { src = MakeSource($"Voice{i}") });
    }

    AudioSource MakeSource(string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 0f;   // panned by hand: works the same everywhere, WebGL included
        src.dopplerLevel = 0f;
        return src;
    }

    // ------------------------------------------------------------------ playing

    public void Play(string name, Vector2 pos, float volume, float pitch, bool flat)
    {
        var def = SoundBank.Get(name);
        if (def == null || def.clips.Length == 0)
        {
#if UNITY_EDITOR
            Debug.LogWarning($"[Audio] No sound named '{name}' in Resources/Audio.");
#endif
            return;
        }
        if (def.loop) return;
        var now = Time.unscaledTime;
        if (now - def.lastPlayed < def.cooldown) return;

        flat |= def.flat;
        var place = flat ? 1f : Placement(pos, def.reach, out _);
        // Repeat softening: heat counts recent plays, decaying over a third of a second.
        var heat = def.heat * Mathf.Exp(-(now - def.lastPlayed) / 0.3f);
        var gain = def.volume * volume / Mathf.Sqrt(1f + 0.4f * heat);
        if (gain * place < 0.008f) return;

        var voice = FindVoice(def);
        if (voice == null) return;

        def.heat = heat + 1f;
        def.lastPlayed = now;
        voice.def = def;
        voice.gain = gain;
        voice.pos = pos;
        voice.flat = flat;
        voice.started = now;
        var src = voice.src;
        src.Stop();
        src.clip = def.PickClip();
        src.loop = false;
        src.pitch = pitch * (1f + Random.Range(-def.pitchJitter, def.pitchJitter));
        Apply(voice, 1f);
        src.Play();
    }

    Voice FindVoice(SoundDef def)
    {
        // Too many of this one already: cut off its oldest.
        Voice oldestSame = null;
        var same = 0;
        foreach (var v in voices)
        {
            if (v.def != def || !v.src.isPlaying) continue;
            same++;
            if (oldestSame == null || v.started < oldestSame.started) oldestSame = v;
        }
        if (same >= def.maxVoices) return oldestSame;

        foreach (var v in voices)
            if (!v.src.isPlaying) return v;

        // The pool is full: take the least important, quietest voice, if it matters less than this one.
        Voice victim = null;
        var worst = float.MaxValue;
        foreach (var v in voices)
        {
            if (v.def == null) return v;
            var score = v.def.priority * 10f + v.level * 5f + (Time.unscaledTime - v.started < 0.05f ? 5f : 0f);
            if (score < worst)
            {
                worst = score;
                victim = v;
            }
        }
        return victim != null && victim.def.priority <= def.priority ? victim : null;
    }

    public void Loop(object owner, string name, Vector2 pos, float volume, float pitch)
    {
        var def = SoundBank.Get(name);
        if (def == null || def.clips.Length == 0 || owner == null) return;
        var key = ((long)RuntimeHelpers.GetHashCode(owner) << 32) ^ (uint)name.GetHashCode();
        if (!loops.TryGetValue(key, out var v))
        {
            if (volume <= 0.001f || loops.Count >= MaxLoops) return;
            if (!def.flat && Placement(pos, def.reach, out _) < 0.01f) return;
            var running = 0;
            foreach (var l in loops.Values)
                if (l.def == def) running++;
            if (running >= def.maxVoices) return;

            var src = spareLoopSources.Count > 0 ? spareLoopSources.Pop() : MakeSource("Loop");
            src.clip = def.PickClip();
            src.loop = true;
            // Start each copy of a loop at a different point, so two fires don't phase together.
            src.timeSamples = Random.Range(0, Mathf.Max(1, src.clip.samples - 1));
            v = new Voice { src = src, def = def, key = key, flat = def.flat, started = Time.unscaledTime };
            loops[key] = v;
            v.pos = pos;
            Apply(v, 0f);
            src.Play();
        }
        v.target = Mathf.Clamp01(volume);
        v.gain = def.volume;
        v.pos = pos;
        v.pitch = pitch;
        v.touched = Time.unscaledTime;
    }

    // ------------------------------------------------------------------ mixing

    void LateUpdate()
    {
        var dt = Time.unscaledDeltaTime;
        var now = Time.unscaledTime;
        sfxVolume = SfxVolume;
        PauseWithGame();

        // Loops: fade toward their targets; those no longer refreshed fade out and are put away.
        deadLoops.Clear();
        foreach (var pair in loops)
        {
            var v = pair.Value;
            if (now - v.touched > LoopTimeout || v.src == null) v.target = 0f;
            var rising = v.target > v.fade;
            var tau = Mathf.Max(0.01f, rising ? v.def.attack : v.def.release);
            v.fade = Mathf.Lerp(v.fade, v.target, 1f - Mathf.Exp(-dt / tau * 3f));
            if (Mathf.Abs(v.fade - v.target) < 0.002f) v.fade = v.target;
            if (v.src != null) v.src.pitch = v.pitch;
            if (v.target <= 0f && v.fade < 0.004f) deadLoops.Add(pair.Key);
        }
        foreach (var key in deadLoops)
        {
            var v = loops[key];
            loops.Remove(key);
            if (v.src == null) continue;
            v.src.Stop();
            spareLoopSources.Push(v.src);
        }

        // Meter each bus, and how strongly the player's foreground sounds are playing.
        System.Array.Clear(busLoad, 0, busLoad.Length);
        var foreground = 0f;
        var total = 0f;
        foreach (var v in voices)
        {
            if (v.def == null || !v.src.isPlaying) continue;
            v.level = v.gain * (v.flat ? 1f : Placement(v.pos, v.def.reach, out _)) * Tail(v);
            busLoad[(int)v.def.bus] += v.level * v.level;
            if (v.def.foreground) foreground = Mathf.Max(foreground, v.level / Mathf.Max(0.01f, v.def.volume));
        }
        foreach (var v in loops.Values)
        {
            v.level = v.gain * v.fade * (v.flat ? 1f : Placement(v.pos, v.def.reach, out _));
            busLoad[(int)v.def.bus] += v.level * v.level;
            if (v.def.foreground) foreground = Mathf.Max(foreground, v.level / Mathf.Max(0.01f, v.def.volume));
        }

        // Duck quickly when the player acts, and recover gently after.
        var duckTarget = Mathf.Clamp01(foreground * 1.4f);
        Duck = Mathf.Lerp(Duck, duckTarget, 1f - Mathf.Exp(-dt / (duckTarget > Duck ? 0.04f : 0.45f)));

        for (var b = 0; b < busGain.Length; b++)
        {
            var load = Mathf.Sqrt(busLoad[b]);
            var target = Compress(load, BusKnee[b]) * (1f - BusDuckDepth[b] * Duck);
            busGain[b] = Smooth(busGain[b], target, dt);
            total += busLoad[b] * busGain[b] * busGain[b];
        }
        masterGain = Smooth(masterGain, Compress(Mathf.Sqrt(total), MasterKnee), dt);

        foreach (var v in voices)
            if (v.def != null && v.src.isPlaying) Apply(v, 1f);
        foreach (var v in loops.Values)
            if (v.src != null) Apply(v, v.fade);
    }

    // While the game is paused (time stopped), the world falls silent; menus keep their sounds.
    void PauseWithGame()
    {
        var stopped = Time.timeScale <= 0f;
        if (stopped == paused) return;
        paused = stopped;
        foreach (var v in voices)
            if (v.def != null && v.def.bus != Bus.UI) Hold(v.src);
        foreach (var v in loops.Values)
            if (v.def.bus != Bus.UI) Hold(v.src);
    }

    void Hold(AudioSource src)
    {
        if (src == null) return;
        if (paused) src.Pause();
        else src.UnPause();
    }

    // A one-shot's level falls with its clip, for metering: a long tail shouldn't hold the duck.
    static float Tail(Voice v)
    {
        var clip = v.src.clip;
        if (clip == null) return 0f;
        var t = v.src.time / Mathf.Max(0.01f, clip.length);
        return 1f - t * t;
    }

    static float Compress(float load, float knee) => load <= knee ? 1f : Mathf.Pow(knee / load, Ratio);

    // Compression clamps down fast and lets go slowly, so bursts don't pump.
    static float Smooth(float current, float target, float dt)
    {
        var tau = target < current ? 0.03f : 0.6f;
        return Mathf.Lerp(current, target, 1f - Mathf.Exp(-dt / tau));
    }

    void Apply(Voice v, float fade)
    {
        var pan = 0f;
        var place = v.flat ? 1f : Placement(v.pos, v.def.reach, out pan);
        v.src.panStereo = pan;
        v.src.volume = Mathf.Clamp01(v.gain * fade * place * busGain[(int)v.def.bus] * masterGain * sfxVolume);
    }

    /// <summary>
    /// How loud a sound at pos is from the camera (1 on screen, falling to 0 a little past the
    /// edge), and where it sits left to right.
    /// </summary>
    float Placement(Vector2 pos, float reach, out float pan)
    {
        if (camFrame != Time.frameCount || cam == null)
        {
            cam = Camera.main;
            camFrame = Time.frameCount;
        }
        pan = 0f;
        if (cam == null) return 1f;
        var c = (Vector2)cam.transform.position;
        var halfH = cam.orthographic ? cam.orthographicSize : 6f;
        var halfW = halfH * cam.aspect;
        var dx = (pos.x - c.x) / halfW;
        var dy = (pos.y - c.y) / halfH;
        pan = Mathf.Clamp(dx, -1f, 1f) * 0.6f;
        var d = Mathf.Sqrt(dx * dx + dy * dy);
        const float inner = 0.8f;
        if (d <= inner) return 1f;
        var k = Mathf.Clamp01((d - inner) / (1f + reach - inner));
        return (1f - k) * (1f - k);
    }
}
