"""Composes and renders the game's music and ambience from scratch: no samples, no downloads.

    JumpCraftGame/Assets/Resources/Music/music_mountain.wav   the mountains: harp, flute, warm pads, frame drum
    JumpCraftGame/Assets/Resources/Music/music_forest.wav     the spooky forest: music box, choir, low drone
    JumpCraftGame/Assets/Resources/Music/music_combat.wav     a stem of war drums and driving strings
    JumpCraftGame/Assets/Resources/Music/music_menu.wav       the title theme
    JumpCraftGame/Assets/Resources/Ambience/amb_mountain.wav  night wind over the peaks
    JumpCraftGame/Assets/Resources/Ambience/amb_forest.wav    crickets, an owl, creaking boughs

    python Tools/generate_music.py

The mountain, forest and combat tracks share one tempo, one length and one chord progression,
so the game can play them in lockstep and cross-fade between them: walking into the forest the
same tune turns eerie, and when skeletons attack the drums come in on the beat (MusicDirector.cs).

Every track is a seamless loop: each is rendered with its reverb and release tails, and the tail
is folded back over the start, which is exactly what is heard when it repeats.

Output is deterministic.
"""
from pathlib import Path

import numpy as np
from scipy import signal as sig

from sound_synth import (additive, bp, brown, env_exp, env_points, formant_shape, glide_exp, hp,
                         impulses, lp, make_loop, n_of, note_freq, pan, pink, pluck, resonator,
                         reverb_ir, rng_for, saw_amps, smooth_random, time, tv_bandpass, wrap_tail,
                         write_wav, noise, sine, modal)

ROOT = Path(__file__).resolve().parent.parent / "JumpCraftGame/Assets/Resources"
MSR = 32000      # music sample rate
ASR = 22050      # ambience sample rate

BPM = 90.0
BEAT = 60.0 / BPM
BAR = BEAT * 4
BARS = 16
LENGTH = BAR * BARS  # 42.67 s

# One chord per bar. The same progression underpins every in-game arrangement.
PROGRESSION = ["Dm", "C", "Bb", "C", "Dm", "F", "G", "A", "Dm", "C", "Bb", "F", "G", "Bb", "C", "A"]
CHORDS = {
    "Dm": ("D", [0, 3, 7]), "C": ("C", [0, 4, 7]), "Bb": ("Bb", [0, 4, 7]), "F": ("F", [0, 4, 7]),
    "G": ("G", [0, 4, 7]), "A": ("A", [0, 4, 7]),
}

# The theme, from bar 5 (beats from the start of bar 5). None is a rest.
MELODY = [
    ("A4", 1), ("D5", 1), ("E5", 1), ("F5", 1),
    ("E5", 1.5), ("C5", 0.5), ("A4", 2),
    ("B4", 1), ("D5", 1), ("G5", 1.5), ("F5", 0.5),
    ("E5", 3), (None, 1),
    ("F5", 1), ("E5", 0.5), ("D5", 0.5), ("A5", 2),
    ("G5", 1), ("E5", 1), ("C5", 1), ("E5", 1),
    ("F5", 1.5), ("D5", 0.5), ("Bb4", 1), ("D5", 1),
    ("C5", 2), ("A4", 1), ("C5", 1),
    ("D5", 1), ("G5", 1), ("B5", 1), ("A5", 1),
    ("F5", 2), ("D5", 1), ("F5", 1),
    ("G5", 1), ("E5", 1), ("C6", 1), ("G5", 1),
    ("A5", 2), ("E5", 1), ("C#5", 1),
]


def midi_of(name):
    return int(round(69 + 12 * np.log2(note_freq(name) / 440.0)))


def hz(midi):
    return 440.0 * 2 ** ((midi - 69) / 12)


def chord_midis(chord, octave):
    root, ints = CHORDS[chord]
    base = midi_of(f"{root}{octave}")
    return [base + i for i in ints]


class Track:
    """A stereo buffer with room for tails; parts are placed at times in seconds."""

    def __init__(self, length, sr, tail=7.0):
        self.sr = sr
        self.length = n_of(length, sr)
        self.buf = np.zeros((self.length + n_of(tail, sr), 2))
        self.send = np.zeros(len(self.buf))  # mono reverb send

    def add(self, x, at, position=0.0, gain=1.0, verb=0.0):
        i = int(round(at * self.sr))
        if i >= len(self.buf):
            return
        st = pan(x, position) * gain if x.ndim == 1 else x * gain
        j = min(len(self.buf), i + len(st))
        self.buf[i:j] += st[:j - i]
        if verb:
            mono = x if x.ndim == 1 else x.mean(axis=1)
            self.send[i:j] += mono[:j - i] * gain * verb

    def render(self, r, reverb_seconds=2.6, damp=0.65, level=0.2):
        ir = reverb_ir(reverb_seconds, r, self.sr, stereo=True, damp=damp, predelay=0.02)
        wet = np.stack([sig.fftconvolve(self.send, ir[:, c])[:len(self.buf)] for c in range(2)], axis=1)
        out = self.buf + wet * level * 4
        return wrap_tail(out, self.length)


def master(x, level=-18.0, peak=0.9):
    """Loudness-normalises a stereo track and limits its peaks softly."""
    mono = x.mean(axis=1)
    rms = np.sqrt(np.mean(mono ** 2)) + 1e-12
    x = x * (10 ** (level / 20) / rms)
    return np.tanh(x / peak) * peak


# ============================================================================ instruments

def warm_pad(freq, dur, r, sr, bright=1.6):
    t = time(dur, sr)
    out = np.zeros(len(t))
    for cents in (-7, 0, 7):
        vib = 1 + 0.0025 * np.sin(2 * np.pi * r.uniform(0.15, 0.35) * t + r.uniform(0, 6))
        out += additive(freq * vib, dur, saw_amps(14, bright), sr=sr, detune_cents=cents, r=r)
    return lp(out / 3, 2200, sr=sr)


def choir(freq, dur, r, sr, vowel="oo"):
    """An "ooh" choir: harmonics weighted by vowel formants, a few voices, slow vibrato."""
    formants = {
        "oo": [(320, 120, 1.0), (800, 160, 0.35), (2400, 220, 0.08)],
        "ah": [(700, 140, 1.0), (1150, 180, 0.6), (2600, 240, 0.15)],
    }[vowel]
    t = time(dur, sr)
    out = np.zeros(len(t))
    for v in range(4):
        vib = 1 + 0.004 * np.sin(2 * np.pi * r.uniform(4.6, 5.6) * t + r.uniform(0, 6))
        f = freq * 2 ** (r.uniform(-9, 9) / 1200)
        amps = [formant_shape(f * k, formants) / k ** 0.3 for k in range(1, 30)]
        out += additive(f * vib, dur, amps, sr=sr, r=r)
    breath = bp(noise(dur, r, sr), min(freq * 3, 3000), 1.5, sr=sr) * 0.04
    return out / 4 + breath


def harp(freq, dur, r, sr):
    x = pluck(freq, dur, r, sr, decay=0.9985, bright=0.45)
    body = lp(x, 4000, sr=sr)
    return body * env_points(dur, [(0, 1), (dur * 0.8, 0.7), (dur, 0)], sr=sr)


def flute(freq, dur, r, sr):
    t = time(dur, sr)
    vib_depth = 0.006 * np.clip((t - 0.25) / 0.4, 0, 1)
    f = freq * (1 + vib_depth * np.sin(2 * np.pi * 5.2 * t))
    tone = additive(f, dur, [1.0, 0.32, 0.12, 0.05, 0.02], sr=sr, r=r)
    breath = bp(noise(dur, r, sr), min(freq * 2, sr * 0.4), 2.0, sr=sr) * 0.12
    breath *= np.exp(-t / 0.15) * 1.5 + 0.35
    env = env_points(dur, [(0, 0), (0.05, 1), (max(0.06, dur - 0.12), 0.85), (dur, 0)], sr=sr)
    return (tone + breath) * env


def music_box(freq, dur, r, sr):
    t = time(dur, sr)
    tine = modal([freq, freq * 2.0, freq * 5.8, freq * 9.2], [1.4, 0.5, 0.12, 0.05],
                 [1.0, 0.2, 0.12, 0.05], dur, sr=sr, r=r)
    return tine * np.clip(t / 0.002, 0, 1)


def bell_tone(freq, dur, r, sr):
    t = time(dur, sr)
    ph = 2 * np.pi * freq * t
    idx = 1.8 * np.exp(-t / 0.2)
    return np.sin(ph + idx * np.sin(ph * 3.5)) * np.exp(-t / 0.9) * np.clip(t / 0.002, 0, 1)


def bass_note(freq, dur, r, sr):
    t = time(dur, sr)
    tone = additive(freq, dur, [1.0, 0.35, 0.15, 0.06], sr=sr, r=r)
    return lp(tone, 900, sr=sr) * env_points(dur, [(0, 0), (0.01, 1), (dur * 0.6, 0.6), (dur, 0)], sr=sr)


def bowed_bass(freq, dur, r, sr):
    t = time(dur, sr)
    vib = 1 + 0.003 * np.sin(2 * np.pi * 5 * t)
    tone = additive(freq * vib, dur, saw_amps(20, 1.1), sr=sr, r=r)
    return lp(tone, 700, sr=sr) * env_points(dur, [(0, 0), (0.25, 1), (dur - 0.3, 0.8), (dur, 0)], sr=sr)


def string_stab(freq, dur, r, sr):
    tone = additive(freq, dur, saw_amps(24, 1.0), sr=sr, r=r) + additive(freq, dur, saw_amps(24, 1.0), sr=sr, detune_cents=8, r=r)
    return lp(tone, 1800, sr=sr) * env_exp(dur, 0.09, sr=sr, attack=0.006) * 0.5


def frame_drum(r, sr, accent=1.0):
    dur = 0.6
    t = time(dur, sr)
    body = sine(glide_exp(dur, 110, 62, sr), dur, sr) * np.exp(-t / 0.16)
    skin = lp(noise(dur, r, sr), 700, sr=sr) * np.exp(-t / 0.03) * 0.6
    return (body + skin) * accent


def shaker(r, sr, accent=1.0):
    dur = 0.08
    t = time(dur, sr)
    return hp(noise(dur, r, sr), 6000, sr=sr) * np.clip(t / 0.01, 0, 1) * np.exp(-t / 0.02) * accent


def taiko(r, sr, accent=1.0, pitch=1.0):
    dur = 1.2
    t = time(dur, sr)
    body = sine(glide_exp(dur, 85 * pitch, 42 * pitch, sr), dur, sr) * np.exp(-t / 0.35)
    slap = lp(noise(dur, r, sr), 1400, sr=sr) * np.exp(-t / 0.025) * 0.9
    room = lp(noise(dur, r, sr), 300, sr=sr) * np.exp(-t / 0.2) * 0.3
    return (body * 1.2 + slap + room) * accent


def tom(r, sr, pitch, accent=1.0):
    dur = 0.6
    t = time(dur, sr)
    body = sine(glide_exp(dur, pitch * 1.4, pitch, sr), dur, sr) * np.exp(-t / 0.18)
    slap = lp(noise(dur, r, sr), 2000, sr=sr) * np.exp(-t / 0.02) * 0.5
    return (body + slap) * accent


def rim(r, sr, accent=1.0):
    dur = 0.12
    t = time(dur, sr)
    return (bp(noise(dur, r, sr), 2200, 3.0, sr=sr) * 3 + sine(820, dur, sr) * 0.4) * np.exp(-t / 0.015) * accent


# ============================================================================ arrangements

def melody_events(start_bar=4):
    t = start_bar * BAR
    for note, beats in MELODY:
        if note is not None:
            yield note, t, beats * BEAT
        t += beats * BEAT


def harp_pattern(chord):
    root, ints = CHORDS[chord]
    base = midi_of(f"{root}3")
    third = ints[1]
    return [base, base + 7, base + 12, base + 12 + third, base + 19, base + 12, base + 7, base + third]


def lay_pads(tr, r, sr, voice, gain, verb, octave=3, spread=0.5):
    for b, chord in enumerate(PROGRESSION):
        start = b * BAR
        dur = BAR + 0.9
        midis = chord_midis(chord, octave) + [chord_midis(chord, octave)[0] + 12]
        for k, m in enumerate(midis):
            x = voice(hz(m), dur, r, sr)
            x *= env_points(dur, [(0, 0), (0.5, 1), (dur - 0.9, 1), (dur, 0)], sr=sr)
            tr.add(x, start, position=spread * (k / (len(midis) - 1) * 2 - 1), gain=gain, verb=verb)


def mountain(r):
    sr = MSR
    tr = Track(LENGTH, sr)
    lay_pads(tr, r, sr, warm_pad, 0.11, 0.6)
    for b, chord in enumerate(PROGRESSION):
        bar0 = b * BAR
        # Harp: rolling eighths through the chord.
        for k, m in enumerate(harp_pattern(chord)):
            tr.add(harp(hz(m), 1.6, r, sr), bar0 + k * BEAT / 2 + r.uniform(0, 0.008), position=-0.35, gain=0.32 if k else 0.4, verb=0.5)
        # Bass: root on the one, fifth on the three.
        root_m = midi_of(f"{CHORDS[chord][0]}2")
        tr.add(bass_note(hz(root_m), BEAT * 1.8, r, sr), bar0, gain=0.5, verb=0.1)
        tr.add(bass_note(hz(root_m + 7), BEAT * 1.6, r, sr), bar0 + 2 * BEAT, gain=0.38, verb=0.1)
        # Frame drum and shaker, gently.
        if b >= 2:
            tr.add(frame_drum(r, sr), bar0, gain=0.5, verb=0.3)
            tr.add(frame_drum(r, sr, 0.6), bar0 + 2 * BEAT, gain=0.5, verb=0.3)
            tr.add(frame_drum(r, sr, 0.35), bar0 + 3.5 * BEAT, gain=0.5, verb=0.3)
            for e in range(8):
                acc = 1.0 if e % 2 else 0.55
                tr.add(shaker(r, sr, acc), bar0 + e * BEAT / 2, position=0.45, gain=0.12, verb=0.2)
        # A bell on the first beat of every other bar, high and far.
        if b % 2 == 0:
            m = chord_midis(chord, 6)[r.integers(0, 3)]
            tr.add(bell_tone(hz(m), 2.0, r, sr), bar0 + r.choice([0, 1.5, 2.5]) * BEAT, position=r.uniform(-0.7, 0.7), gain=0.06, verb=0.9)
    for note, at, dur in melody_events():
        tr.add(flute(note_freq(note), dur + 0.06, r, sr), at, position=0.18, gain=0.3, verb=0.55)
    # An echo of the theme an octave down in the intro, on the flute, very softly.
    for k, (note, at, dur) in enumerate(melody_events()):
        if at - 4 * BAR >= 4 * BAR:
            break
        tr.add(flute(note_freq(note) / 2, dur + 0.06, r, sr), at - 4 * BAR, position=-0.2, gain=0.13, verb=0.7)
    return master(tr.render(r, 2.4, 0.6, 0.22), -19)


def forest(r):
    sr = MSR
    tr = Track(LENGTH, sr)
    lay_pads(tr, r, sr, choir, 0.13, 0.9, octave=3, spread=0.7)
    # A low drone under everything, swelling and sinking.
    t = time(LENGTH, sr)
    drone = additive(hz(midi_of("D2")) * (1 + 0.002 * np.sin(2 * np.pi * 0.1 * t)), LENGTH, saw_amps(16, 1.3), sr=sr, r=r)
    drone = lp(drone, 500, sr=sr) * (0.6 + 0.4 * np.sin(2 * np.pi * t / (BAR * 4)) ** 2)
    tr.add(drone, 0, gain=0.09, verb=0.4)
    for b, chord in enumerate(PROGRESSION):
        bar0 = b * BAR
        # Music-box arpeggio, sparse: quarter notes, high and glassy, drifting left and right.
        pat = harp_pattern(chord)
        for k in range(4):
            m = pat[(k * 2 + b) % len(pat)] + 24
            tr.add(music_box(hz(m), 2.0, r, sr), bar0 + k * BEAT + r.uniform(0, 0.015), position=np.sin(b + k) * 0.6, gain=0.13, verb=0.8)
        root_m = midi_of(f"{CHORDS[chord][0]}2")
        tr.add(bowed_bass(hz(root_m), BAR + 0.3, r, sr), bar0, gain=0.22, verb=0.3)
        # A slow, soft heartbeat drum.
        if b >= 2:
            tr.add(frame_drum(r, sr, 0.8), bar0, gain=0.45, verb=0.5)
            tr.add(frame_drum(r, sr, 0.45), bar0 + 0.35 * BEAT, gain=0.45, verb=0.5)
            if b % 4 == 3:
                tr.add(rim(r, sr, 0.5), bar0 + 3.5 * BEAT, position=0.6, gain=0.25, verb=0.9)
    # The theme on the music box, an octave up, with a choir "ah" doubling it far below.
    for note, at, dur in melody_events():
        tr.add(music_box(note_freq(note) * 2, max(1.5, dur + 1.0), r, sr), at, position=0.15, gain=0.24, verb=0.85)
        tr.add(choir(note_freq(note) / 2, dur + 0.2, r, sr, "ah") * env_points(dur + 0.2, [(0, 0), (0.15, 1), (dur, 0.8), (dur + 0.2, 0)], sr=sr),
               at, position=-0.15, gain=0.05, verb=0.9)
    return master(tr.render(r, 3.4, 0.75, 0.3), -20)


def combat(r):
    sr = MSR
    tr = Track(LENGTH, sr)
    for b, chord in enumerate(PROGRESSION):
        bar0 = b * BAR
        # War drums: the one, the and-of-two, the three; a tom fill to close every fourth bar.
        tr.add(taiko(r, sr, 1.0), bar0, gain=0.6, verb=0.35)
        tr.add(taiko(r, sr, 0.55, 1.15), bar0 + 1.5 * BEAT, position=0.2, gain=0.6, verb=0.35)
        tr.add(taiko(r, sr, 0.8), bar0 + 2 * BEAT, gain=0.6, verb=0.35)
        if b % 4 == 3:
            for k, (beat, p) in enumerate([(3.0, 180), (3.25, 160), (3.5, 140), (3.75, 120)]):
                tr.add(tom(r, sr, p, 0.7 + k * 0.1), bar0 + beat * BEAT, position=0.5 - k * 0.33, gain=0.45, verb=0.3)
        else:
            tr.add(taiko(r, sr, 0.4, 1.3), bar0 + 3.5 * BEAT, position=-0.2, gain=0.6, verb=0.35)
        for e in range(8):
            tr.add(rim(r, sr, 0.9 if e % 2 == 0 else 0.5), bar0 + e * BEAT / 2, position=-0.4, gain=0.12, verb=0.2)
        # Low strings driving eighths on the root, leaping to the octave.
        root_m = midi_of(f"{CHORDS[chord][0]}2")
        for e in range(8):
            m = root_m + (12 if e in (3, 6) else 0)
            tr.add(string_stab(hz(m), 0.3, r, sr), bar0 + e * BEAT / 2, position=0.3, gain=0.22 if e % 2 == 0 else 0.16, verb=0.25)
    return master(tr.render(r, 1.8, 0.6, 0.15), -19)


def menu(r):
    """The title theme: slower and closer, the melody over harp and choir, no drums."""
    global BEAT, BAR
    old = BEAT, BAR
    BEAT = 60.0 / 74.0
    BAR = BEAT * 4
    length = BAR * BARS
    sr = MSR
    tr = Track(length, sr)
    lay_pads(tr, r, sr, choir, 0.1, 0.9, octave=3, spread=0.8)
    lay_pads(tr, r, sr, warm_pad, 0.05, 0.6, octave=2, spread=0.3)
    for b, chord in enumerate(PROGRESSION):
        bar0 = b * BAR
        for k, m in enumerate(harp_pattern(chord)):
            tr.add(harp(hz(m), 2.2, r, sr), bar0 + k * BEAT / 2 + r.uniform(0, 0.01), position=-0.3 + 0.1 * (k % 3), gain=0.3, verb=0.7)
        root_m = midi_of(f"{CHORDS[chord][0]}2")
        tr.add(bowed_bass(hz(root_m), BAR + 0.3, r, sr), bar0, gain=0.18, verb=0.3)
        if b % 4 == 0:
            tr.add(bell_tone(hz(chord_midis(chord, 5)[0]), 3.0, r, sr), bar0, position=0.5, gain=0.08, verb=1.0)
    for note, at, dur in melody_events():
        tr.add(flute(note_freq(note), dur + 0.08, r, sr), at, position=0.15, gain=0.28, verb=0.7)
        tr.add(music_box(note_freq(note) * 2, 1.6, r, sr), at, position=0.4, gain=0.05, verb=0.9)
    out = master(tr.render(r, 3.0, 0.7, 0.28), -20)
    BEAT, BAR = old
    return out


# ============================================================================ ambience

def stereo_bed(make, r, dur, sr, width=0.6):
    a = make(r)
    b = make(r)
    mid = (a + b) / 2
    return np.stack([mid + (a - mid) * width, mid + (b - mid) * width], axis=1)


def wind_layer(r, dur, sr, lo, hi, speed, whistle=0.0):
    centers = lo + (hi - lo) * smooth_random(dur, speed, r, sr=sr)
    body = hp(tv_bandpass(pink(dur, r, sr), centers, 0.8, sr=sr), lo * 0.5, sr=sr)
    gust = (0.35 + 0.65 * smooth_random(dur, speed * 0.6, r, sr=sr)) ** 1.5
    out = body * gust
    if whistle:
        w = resonator(noise(dur, r, sr), r.uniform(700, 1100), 80, sr=sr) * smooth_random(dur, 0.3, r, sr=sr) ** 3
        out += w * whistle
    return out


def amb_mountain(r):
    sr = ASR
    dur = 40.0 + 2.0
    bed = stereo_bed(lambda rr: wind_layer(rr, dur, sr, 250, 1100, 0.25, whistle=0.5), r, dur, sr)
    rumble = lp(brown(dur, r, sr), 120, sr=sr) * 0.5
    bed += pan(rumble, 0)
    hiss = stereo_bed(lambda rr: hp(pink(dur, rr, sr), 3000, sr=sr) * (0.3 + 0.7 * smooth_random(dur, 0.2, rr, sr=sr)) * 0.15, r, dur, sr)
    bed = make_loop(bed + hiss, 2.0, sr)
    length = len(bed)
    # Pebbles trickling down a far slope now and then, and a distant hawk's cry once.
    tr = Track(length / sr, sr, tail=4.0)
    for k in range(5):
        at = r.uniform(0, length / sr)
        trickle = np.zeros(n_of(1.5, sr))
        tt = 0.0
        while tt < 1.2:
            clk = modal([r.uniform(900, 1800), r.uniform(2400, 3600)], [0.01, 0.006], [1, 0.5], 0.05, sr=sr, r=r)
            i = n_of(tt, sr)
            trickle[i:i + len(clk)] += clk[:len(trickle) - i] * r.uniform(0.2, 1)
            tt += r.uniform(0.03, 0.2)
        tr.add(lp(trickle, 2500, sr=sr), at, position=r.uniform(-0.8, 0.8), gain=0.15, verb=1.0)
    cry_d = 1.4
    tc = time(cry_d, sr)
    cry = additive(glide_exp(cry_d, 2400, 1700, sr) * (1 + 0.02 * np.sin(2 * np.pi * 22 * tc)), cry_d, [1, 0.3, 0.1], sr=sr, r=r)
    cry *= env_points(cry_d, [(0, 0), (0.1, 1), (0.5, 0.6), (cry_d, 0)], sr=sr)
    tr.add(lp(cry, 3500, sr=sr), r.uniform(5, 30), position=0.6, gain=0.035, verb=1.2)
    events = tr.render(r, 3.0, 0.7, 0.3)
    return master(bed + events, -24, 0.8)


def amb_forest(r):
    sr = ASR
    dur = 40.0 + 2.0
    rustle = stereo_bed(lambda rr: hp(pink(dur, rr, sr), 2000, sr=sr) * (0.2 + 0.8 * smooth_random(dur, 0.35, rr, sr=sr) ** 2) * 0.35, r, dur, sr, 0.9)
    moan = stereo_bed(lambda rr: wind_layer(rr, dur, sr, 180, 600, 0.15), r, dur, sr) * 0.6
    bed = make_loop(rustle + moan, 2.0, sr)
    length = len(bed)
    total = length / sr
    tr = Track(total, sr, tail=4.0)
    # Crickets: each its own pitch, place and rhythm of chirps.
    for c in range(4):
        f = r.uniform(4200, 5200)
        pos = r.uniform(-0.9, 0.9)
        gap = r.uniform(0.6, 1.1)
        gain = r.uniform(0.02, 0.045)
        at = r.uniform(0, gap)
        while at < total:
            pulses = r.integers(2, 5)
            chirp = np.zeros(n_of(0.2, sr))
            for p in range(pulses):
                pd = 0.018
                pulse = sine(f, pd, sr) * np.sin(np.pi * np.linspace(0, 1, n_of(pd, sr)))
                i = n_of(p * 0.033, sr)
                chirp[i:i + len(pulse)] += pulse
            # Every so often a cricket falls silent for a while.
            if r.uniform() > 0.12:
                tr.add(chirp, at, position=pos, gain=gain, verb=0.4)
            at += gap * r.uniform(0.9, 1.1)
    # An owl, far off: "hoo ... hoo-hoo".
    for at in (r.uniform(4, 12), r.uniform(22, 32)):
        for k, (off, d) in enumerate([(0, 0.45), (0.75, 0.22), (1.05, 0.5)]):
            tt = time(d, sr)
            hoot = additive(glide_exp(d, 410, 360, sr), d, [1, 0.15, 0.05], sr=sr, r=r)
            hoot *= np.sin(np.pi * np.clip(tt / d, 0, 1)) ** 0.7
            tr.add(lp(hoot, 900, sr=sr), at + off, position=-0.55, gain=0.07, verb=1.3)
    # Boughs creaking in the wind.
    for k in range(3):
        d = r.uniform(0.8, 1.6)
        f = glide_exp(d, r.uniform(60, 90), r.uniform(90, 130), sr)
        creak = additive(f, d, saw_amps(40, 0.6), sr=sr, r=r)
        creak = bp(creak, 1200, 1.2, sr=sr) * (0.4 + 0.6 * smooth_random(d, 25, r, sr=sr)) * np.sin(np.pi * np.linspace(0, 1, n_of(d, sr)))
        tr.add(creak, r.uniform(0, total - 2), position=r.uniform(-0.8, 0.8), gain=0.04, verb=0.9)
    events = tr.render(r, 3.0, 0.7, 0.3)
    return master(bed + events, -25, 0.8)


def main():
    music = ROOT / "Music"
    amb = ROOT / "Ambience"
    for name, fn, folder, sr in [
        ("music_mountain", mountain, music, MSR),
        ("music_forest", forest, music, MSR),
        ("music_combat", combat, music, MSR),
        ("music_menu", menu, music, MSR),
        ("amb_mountain", amb_mountain, amb, ASR),
        ("amb_forest", amb_forest, amb, ASR),
    ]:
        x = fn(rng_for(name))
        write_wav(folder / f"{name}.wav", x, sr)
        print(f"  {name:16s} {len(x) / sr:6.2f}s  peak {np.max(np.abs(x)):.2f}")


if __name__ == "__main__":
    main()
