"""Composes and renders the game's music and ambience from scratch: no samples, no downloads.

    JumpCraftGame/Assets/Resources/Music/music_mountain.wav   the peaks: solo cello, low strings, war horns, a distant bell
    JumpCraftGame/Assets/Resources/Music/music_forest.wav     the forest: a warped music box, cluster choirs, whispers, a heartbeat
    JumpCraftGame/Assets/Resources/Music/music_combat.wav     a stem of war drums, horn stabs and driving cellos
    JumpCraftGame/Assets/Resources/Music/music_menu.wav       the title theme
    JumpCraftGame/Assets/Resources/Ambience/amb_mountain.wav  night wind, far thunder, something howling
    JumpCraftGame/Assets/Resources/Ambience/amb_forest.wav    moaning wind, creaking boughs, an owl, whispers, dripping

    python Tools/generate_music.py

Dark fantasy: D minor, slow (64 BPM), low registers, nothing bright. The progression leans on the
two darkest moves there are: the flat second (E-flat major over the D drone) and the major fifth
(A major, its C-sharp grinding toward home), a tritone apart.

The mountain, forest and combat tracks share that tempo, length and progression, so the game
plays them in lockstep and cross-fades between them (MusicDirector.cs): walking into the forest
the cello's theme passes to a warped music box, and when skeletons close in the drums come in on
the beat.

Every track is a seamless loop: each is rendered with its reverb and release tails, and the tail
is folded back over the start, which is exactly what is heard when it repeats.

Output is deterministic.
"""
from pathlib import Path

import numpy as np
from scipy import signal as sig

from sound_synth import (additive, bp, brown, darken, env_exp, env_points, formant_shape, glide_exp, hp,
                         lp, make_loop, modal, n_of, noise, norm_rms, note_freq, pan, pink, resonator,
                         reverb_ir, rng_for, saw_amps, sine, smooth_random, time, tv_bandpass,
                         tv_lowpass, wrap_tail, write_wav)

ROOT = Path(__file__).resolve().parent.parent / "JumpCraftGame/Assets/Resources"
MSR = 24000      # music sample rate: the score is dark, nothing lives above 9 kHz
ASR = 22050      # ambience sample rate

BPM = 64.0
BEAT = 60.0 / BPM
BAR = BEAT * 4
BARS = 16
LENGTH = BAR * BARS  # 60 s

# One chord per bar. The same progression underpins every in-game arrangement.
PROGRESSION = ["Dm", "Dm", "Bb", "A", "Dm", "Gm", "Eb", "A", "Dm", "Bb", "Gm", "Eb", "Bb", "Gm", "Eb", "A"]
CHORDS = {
    "Dm": ("D", [0, 3, 7]), "Gm": ("G", [0, 3, 7]), "Bb": ("Bb", [0, 4, 7]),
    "Eb": ("Eb", [0, 4, 7]), "A": ("A", [0, 4, 7]),
}

# The theme, from bar 5, in beats. Low and sparse: a lone voice, not a tune to whistle.
MELODY = [
    ("D4", 2), ("F4", 1), ("E4", 1),
    ("D4", 1.5), ("Bb3", 0.5), ("G3", 2),
    ("G3", 1), ("Bb3", 1), ("Eb4", 2),
    ("D4", 1), ("C#4", 3),
    ("A3", 1), ("D4", 1), ("F4", 1.5), ("E4", 0.5),
    ("D4", 2), ("F4", 2),
    ("G4", 2), ("F4", 1), ("D4", 1),
    ("Eb4", 3), ("D4", 1),
    ("F4", 1), ("D4", 1), ("Bb3", 2),
    ("G3", 1), ("Bb3", 1), ("D4", 1), ("F4", 1),
    ("G4", 2), ("Eb4", 1), ("Bb3", 1),
    ("A3", 2), ("C#4", 1), ("E4", 1),
]


def midi_of(name):
    return int(round(69 + 12 * np.log2(note_freq(name) / 440.0)))


def hz(midi):
    return 440.0 * 2 ** ((midi - 69) / 12)


def root_midi(chord, octave):
    return midi_of(f"{CHORDS[chord][0]}{octave}")


def chord_midis(chord, octave):
    base = root_midi(chord, octave)
    return [base + i for i in CHORDS[chord][1]]


class Track:
    """A stereo buffer with room for tails; parts are placed at times in seconds."""

    def __init__(self, length, sr, tail=8.0):
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

    def add_loop(self, make, position=0.0, gain=1.0, verb=0.0, xf=3.0):
        """A layer that runs the whole track (a drone, a wind): rendered long, its overhang
        cross-fades into its start when the track folds, so it never clicks at the loop point."""
        total = self.length / self.sr + xf
        x = make(total)
        k = n_of(xf, self.sr)
        x[:k] *= np.linspace(0, 1, k) if x.ndim == 1 else np.linspace(0, 1, k)[:, None]
        x[-k:] *= np.linspace(1, 0, k) if x.ndim == 1 else np.linspace(1, 0, k)[:, None]
        self.add(x, 0.0, position, gain, verb)

    def render(self, r, reverb_seconds=3.0, damp=0.75, level=0.25):
        ir = reverb_ir(reverb_seconds, r, self.sr, stereo=True, damp=damp, predelay=0.03)
        wet = np.stack([sig.fftconvolve(self.send, ir[:, c])[:len(self.buf)] for c in range(2)], axis=1)
        out = self.buf + wet * level * 4
        return wrap_tail(out, self.length)


def master(x, sr, level=-20.0, peak=0.9):
    """Darkens, loudness-normalises and softly limits a stereo track."""
    x = darken(x, corner=2800.0, high_gain=0.5, air=7500.0, sr=sr)
    rms = np.sqrt(np.mean(x.mean(axis=1) ** 2)) + 1e-12
    x = x * (10 ** (level / 20) / rms)
    return np.tanh(x / peak) * peak


# ============================================================================ instruments

CELLO_BODY = [(240, 160, 1.0), (560, 240, 0.8), (1150, 380, 0.4), (2500, 700, 0.12)]


def cello(freq, dur, r, sr, attack=0.16, vibrato=0.005, release=0.3):
    """A bowed cello: a rich saw-like string coloured by the resonances of a wooden body, with
    a vibrato that blooms as the note is held, and the rasp of the bow."""
    t = time(dur, sr)
    depth = vibrato * np.clip((t - 0.25) / 0.5, 0, 1)
    f = freq * (1 + depth * np.sin(2 * np.pi * r.uniform(4.8, 5.6) * t + r.uniform(0, 6)))
    amps = [(1.0 / k) * (0.2 + formant_shape(freq * k, CELLO_BODY)) for k in range(1, 36)]
    tone = additive(f, dur, amps, sr=sr, r=r)
    tone /= np.max(np.abs(tone)) + 1e-9
    bow = norm_rms(bp(noise(dur, r, sr), min(freq * 5, 2200), 1.2, sr=sr), 0.03)
    hold = max(attack + 0.02, dur - release)
    env = env_points(dur, [(0, 0), (attack, 1), (hold, 0.85), (dur, 0)], sr=sr)
    return (tone + bow) * env


def marcato(freq, dur, r, sr, accent=1.0):
    """A short, dug-in stroke for ostinatos: bite, then it rings off."""
    t = time(dur, sr)
    x = cello(freq, dur, r, sr, attack=0.012, vibrato=0.0, release=0.05)
    return x * np.exp(-t / (dur * 0.45)) * accent


def low_strings(freqs, dur, r, sr, cutoff=750):
    """The section beneath it all: detuned bowed strings, slow to swell, softly filtered."""
    t = time(dur, sr)
    out = np.zeros(len(t))
    for f in freqs:
        for cents in (-7, 0, 6):
            vib = 1 + 0.0025 * np.sin(2 * np.pi * r.uniform(4, 5.5) * t + r.uniform(0, 6))
            out += additive(f * vib, dur, saw_amps(20, 1.25), sr=sr, detune_cents=cents, r=r)
    return lp(out / (3 * len(freqs)), cutoff, sr=sr)


def horn(freqs, dur, r, sr, peak=1200):
    """War horns: a brassy swell that opens up and closes again."""
    t = time(dur, sr)
    out = np.zeros(len(t))
    for f in freqs:
        for cents in (-6, 0, 7):
            out += additive(f, dur, saw_amps(28, 0.95), sr=sr, detune_cents=cents, r=r)
    cutoff = 180 + peak * env_points(dur, [(0, 0), (dur * 0.45, 1), (dur, 0.05)], sr=sr, curve=1.4)
    out = tv_lowpass(out / (3 * len(freqs)), cutoff, sr=sr)
    return out * env_points(dur, [(0, 0), (dur * 0.4, 1), (dur, 0)], sr=sr, curve=1.2)


def horn_stab(freqs, dur, r, sr):
    t = time(dur, sr)
    out = np.zeros(len(t))
    for f in freqs:
        for cents in (-6, 6):
            out += additive(f, dur, saw_amps(28, 0.95), sr=sr, detune_cents=cents, r=r)
    out = tv_lowpass(out / (2 * len(freqs)), 250 + 1400 * np.exp(-t / 0.12), sr=sr)
    return out * env_exp(dur, 0.22, sr=sr, attack=0.012)


def choir(freq, dur, r, sr, vowel="oo"):
    """A choir: harmonics weighted by vowel formants, several voices, slow vibrato."""
    formants = {
        "oo": [(300, 110, 1.0), (750, 150, 0.3), (2300, 220, 0.05)],
        "ah": [(650, 140, 1.0), (1080, 180, 0.5), (2500, 240, 0.08)],
    }[vowel]
    t = time(dur, sr)
    out = np.zeros(len(t))
    for v in range(4):
        vib = 1 + 0.004 * np.sin(2 * np.pi * r.uniform(4.4, 5.4) * t + r.uniform(0, 6))
        f = freq * 2 ** (r.uniform(-10, 10) / 1200)
        amps = [formant_shape(f * k, formants) / k ** 0.3 for k in range(1, 28)]
        out += additive(f * vib, dur, amps, sr=sr, r=r)
    return out / 4


def toll(freq, dur, r, sr, tau=3.0):
    """A great bell, far off: the strike, the hum an octave below, the slow beating as it dies."""
    t = time(dur, sr)
    ph = 2 * np.pi * freq * t
    strike = np.sin(ph + 2.2 * np.exp(-t / 0.3) * np.sin(ph * 1.41)) * np.exp(-t / tau)
    hum = np.sin(ph * 0.5) * np.exp(-t / (tau * 1.3)) * 0.5 * (1 + 0.2 * np.sin(2 * np.pi * 1.1 * t))
    return lp((strike + hum) * np.clip(t / 0.004, 0, 1), 2000, sr=sr)


def music_box(freq, dur, r, sr):
    """A music box gone wrong: its pitch sags and drifts, as if the spring were dying."""
    t = time(dur, sr)
    warp = 1 + 0.009 * smooth_random(dur, 1.1, r, sr=sr, lo=-1, hi=1) - 0.006 * (t / dur)
    ph = 2 * np.pi * np.cumsum(freq * warp) / sr
    tine = np.sin(ph) * np.exp(-t / 1.7) + 0.16 * np.sin(2 * ph) * np.exp(-t / 0.5) + 0.06 * np.sin(5.8 * ph) * np.exp(-t / 0.1)
    return lp(tine * np.clip(t / 0.002, 0, 1), 2800, sr=sr)


def tremolo_strings(freq, dur, r, sr):
    """Sul ponticello tremolo: thin, glassy, nervous bowing high above everything."""
    t = time(dur, sr)
    tone = additive(freq * (1 + 0.003 * np.sin(2 * np.pi * 0.3 * t)), dur, saw_amps(24, 0.7), sr=sr, r=r)
    tone = norm_rms(bp(tone, 1700, 1.0, sr=sr), 0.25)
    trem = 0.45 + 0.55 * np.abs(np.sin(2 * np.pi * 5.5 * t + r.uniform(0, 3)))
    return tone * trem * env_points(dur, [(0, 0), (dur * 0.5, 1), (dur, 0)], sr=sr, curve=1.5)


def whisper(dur, r, sr):
    """Breath shaped into nearly-words: noise through shifting vowel formants, in syllables."""
    n = noise(dur, r, sr)
    f1 = r.uniform(450, 700)
    f2 = r.uniform(1000, 1700)
    x = bp(n, f1, 4.0, sr=sr) + bp(n, f2, 5.0, sr=sr) * 0.7 + bp(n, 2400, 2.0, sr=sr) * 0.5
    syllables = smooth_random(dur, 7, r, sr=sr) ** 2
    return norm_rms(x, 0.25) * syllables * env_points(dur, [(0, 0), (0.1, 1), (dur - 0.15, 1), (dur, 0)], sr=sr)


def rev_swell(freqs, dur, r, sr):
    """Something rising out of the dark toward the downbeat: a reversed rush and a chord."""
    rush = tv_bandpass(pink(dur, r, sr), glide_exp(dur, 150, 1400, sr), 0.6, sr=sr)
    chord = low_strings(freqs, dur, r, sr, cutoff=900)
    env = env_points(dur, [(0, 0), (dur * 0.95, 1), (dur, 0)], sr=sr, curve=2.6)
    return (norm_rms(rush, 0.25) * 0.6 + norm_rms(chord, 0.25)) * env


def drone(freq, dur, r, sr, beat=0.0):
    t = time(dur, sr)
    f = freq * (1 + 0.002 * np.sin(2 * np.pi * 0.07 * t))
    out = additive(f, dur, saw_amps(18, 1.4), sr=sr, r=r)
    if beat:
        out += additive(f + beat, dur, saw_amps(18, 1.4), sr=sr, r=r)
    return norm_rms(lp(out, 320, sr=sr), 0.25) * (0.65 + 0.35 * smooth_random(dur, 0.12, r, sr=sr))


def war_drum(r, sr, accent=1.0, pitch=1.0):
    """A great skin drum: the boom, the head's own higher ring, and the slap of the beater,
    so it carries on small speakers instead of being only felt."""
    dur = 1.4
    t = time(dur, sr)
    body = sine(glide_exp(dur, 78 * pitch, 44 * pitch, sr), dur, sr) * np.exp(-t / 0.35)
    head = sine(glide_exp(dur, 165 * pitch, 118 * pitch, sr), dur, sr) * np.exp(-t / 0.12) * 0.55
    skin = bp(noise(dur, r, sr), 320, 0.8, sr=sr) * np.exp(-t / 0.045) * 2.2
    slap = bp(noise(dur, r, sr), 950, 0.7, sr=sr) * np.exp(-t / 0.014) * 1.6
    room = lp(noise(dur, r, sr), 250, sr=sr) * np.exp(-t / 0.25) * 0.3
    return hp(body * 0.9 + head + skin + slap + room, 45, sr=sr) * accent


def tom(r, sr, pitch, accent=1.0):
    dur = 0.7
    t = time(dur, sr)
    body = sine(glide_exp(dur, pitch * 1.4, pitch, sr), dur, sr) * np.exp(-t / 0.2)
    slap = lp(noise(dur, r, sr), 1300, sr=sr) * np.exp(-t / 0.02) * 0.5
    return (body + slap) * accent


def frame_drum(r, sr, accent=1.0):
    dur = 0.7
    t = time(dur, sr)
    body = sine(glide_exp(dur, 95, 58, sr), dur, sr) * np.exp(-t / 0.18)
    head = sine(glide_exp(dur, 190, 140, sr), dur, sr) * np.exp(-t / 0.08) * 0.4
    skin = bp(noise(dur, r, sr), 400, 0.8, sr=sr) * np.exp(-t / 0.03) * 1.6
    return (body + head + skin) * accent


def heartbeat(r, sr, accent=1.0):
    out = np.zeros(n_of(1.0, sr))
    a = frame_drum(r, sr, 1.0)
    b = frame_drum(r, sr, 0.6)
    out[:len(a)] += a
    i = n_of(0.3, sr)
    out[i:i + len(b)] += b[:len(out) - i]
    return lp(out, 300, sr=sr) * accent


# ============================================================================ arrangements

def melody_events(start_bar=4):
    t = start_bar * BAR
    for note, beats in MELODY:
        if note is not None:
            yield note, t, beats * BEAT
        t += beats * BEAT


def lay_strings(tr, r, sr, gain, verb, octave=2, spread=0.4):
    for b, chord in enumerate(PROGRESSION):
        dur = BAR + 1.2
        midis = chord_midis(chord, octave) + [root_midi(chord, octave) + 12]
        x = low_strings([hz(m) for m in midis], dur, r, sr)
        x *= env_points(dur, [(0, 0), (0.7, 1), (dur - 1.2, 1), (dur, 0)], sr=sr)
        tr.add(x, b * BAR, position=r.uniform(-spread, spread), gain=gain, verb=verb)


def lay_choir(tr, r, sr, gain, verb, octave=3, cluster=False, from_bar=0, vowel="oo"):
    for b, chord in enumerate(PROGRESSION):
        if b < from_bar:
            continue
        dur = BAR + 1.5
        midis = chord_midis(chord, octave)
        if cluster:
            midis = midis + [midis[0] + 1]  # the minor second above the root: dread
        for k, m in enumerate(midis):
            x = choir(hz(m), dur, r, sr, vowel)
            x *= env_points(dur, [(0, 0), (1.0, 1), (dur - 1.5, 1), (dur, 0)], sr=sr)
            tr.add(x, b * BAR, position=(k / max(1, len(midis) - 1) * 2 - 1) * 0.7, gain=gain, verb=verb)


def mountain(r):
    """Reserved and daring: a lone cello over low strings, a marcato pulse, horns on the turns."""
    sr = MSR
    tr = Track(LENGTH, sr)
    tr.add_loop(lambda d: drone(hz(midi_of("D1")), d, r, sr), gain=0.04, verb=0.2)
    tr.add_loop(lambda d: drone(hz(midi_of("D2")), d, r, sr), gain=0.04, verb=0.3)
    lay_strings(tr, r, sr, 0.12, 0.5)
    for b, chord in enumerate(PROGRESSION):
        bar0 = b * BAR
        root = root_midi(chord, 2)
        # The ostinato: "da . da-da da . da-da", digging in on the root and the fifth.
        if b >= 2:
            for e, (hit, note, acc) in enumerate([(1, 0, 1.0), (0, 0, 0), (1, 0, 0.6), (1, 7, 0.7),
                                                   (1, 0, 0.9), (0, 0, 0), (1, 0, 0.6), (1, 12, 0.75)]):
                if hit:
                    tr.add(marcato(hz(root + note), BEAT * 0.55, r, sr, acc), bar0 + e * BEAT / 2, position=-0.25, gain=0.13, verb=0.3)
        # War drum: the one, and a push into the next bar.
        tr.add(war_drum(r, sr, 0.9), bar0, gain=0.3, verb=0.4)
        if b >= 4:
            tr.add(war_drum(r, sr, 0.5, 1.1), bar0 + 3.5 * BEAT, position=0.2, gain=0.3, verb=0.4)
        if b >= 8:
            for e in (1, 3, 5, 7):
                tr.add(frame_drum(r, sr, 0.25), bar0 + e * BEAT / 2, position=0.35, gain=0.3, verb=0.3)
        # Horns swell on the darkest turns.
        if chord in ("A", "Eb") and b >= 3:
            tr.add(horn([hz(root), hz(root + 7), hz(root + 12)], BAR * 0.95, r, sr), bar0, position=0.1, gain=0.15, verb=0.6)
        # A great bell, far off, every four bars.
        if b % 4 == 0:
            tr.add(toll(hz(midi_of("D3")), 6.0, r, sr), bar0, position=-0.5, gain=0.13, verb=1.0)
    lay_choir(tr, r, sr, 0.045, 0.8, octave=3, from_bar=8)
    for note, at, dur in melody_events():
        tr.add(cello(note_freq(note), dur + 0.12, r, sr), at, position=0.15, gain=0.3, verb=0.55)
    return master(tr.render(r, 3.0, 0.75, 0.25), sr, -20)


def forest(r):
    """Scary: the same tune, but a dying music box sings it over whispers and a heartbeat."""
    sr = MSR
    tr = Track(LENGTH, sr)
    tr.add_loop(lambda d: drone(hz(midi_of("D1")), d, r, sr, beat=0.7), gain=0.1, verb=0.3)
    lay_choir(tr, r, sr, 0.07, 1.0, octave=3, cluster=True)
    for b, chord in enumerate(PROGRESSION):
        bar0 = b * BAR
        root = root_midi(chord, 1)
        tr.add(cello(hz(root + 12), BAR + 0.5, r, sr, attack=0.8, vibrato=0.003, release=0.8), bar0, position=-0.1, gain=0.12, verb=0.4)
        if b >= 2:
            tr.add(heartbeat(r, sr, 1.0), bar0, gain=0.42, verb=0.5)
        # High, nervous strings breathing in and out over the darkest chords.
        if chord in ("Eb", "A", "Gm"):
            for k, m in enumerate(chord_midis(chord, 5)[1:]):
                tr.add(tremolo_strings(hz(m), BAR, r, sr), bar0, position=0.6 - k * 1.2, gain=0.02, verb=0.8)
        # Something rising toward each new phrase.
        if b in (3, 7, 11, 15):
            nxt = PROGRESSION[(b + 1) % len(PROGRESSION)]
            tr.add(rev_swell([hz(m) for m in chord_midis(nxt, 3)], BAR, r, sr), bar0, gain=0.07, verb=0.6)
    # The theme on a warped music box, an octave up, far away; a fragment of it in the intro.
    for k, (note, at, dur) in enumerate(melody_events()):
        tr.add(music_box(note_freq(note) * 2, max(2.0, dur + 1.2), r, sr), at, position=0.25, gain=0.2, verb=1.0)
        if k < 6:
            tr.add(music_box(note_freq(note) * 2, 2.5, r, sr), at - 4 * BAR + BEAT, position=-0.5, gain=0.07, verb=1.2)
    # Whispers, here and there, never where you're looking.
    for _ in range(9):
        d = r.uniform(0.8, 1.8)
        tr.add(whisper(d, r, sr), r.uniform(0, LENGTH - d), position=r.choice([-0.85, 0.85]) * r.uniform(0.6, 1), gain=0.03, verb=0.9)
    return master(tr.render(r, 4.2, 0.85, 0.35), sr, -21)


def combat(r):
    """Daring: war drums, horn stabs and cellos driving sixteenths, under the same chords."""
    sr = MSR
    tr = Track(LENGTH, sr)
    for b, chord in enumerate(PROGRESSION):
        bar0 = b * BAR
        root = root_midi(chord, 2)
        # Drums: the one, the and-of-two, the three, the and-of-four; a tom fill every fourth bar.
        tr.add(war_drum(r, sr, 1.0), bar0, gain=0.4, verb=0.35)
        tr.add(war_drum(r, sr, 0.55, 1.15), bar0 + 1.5 * BEAT, position=0.2, gain=0.4, verb=0.35)
        tr.add(war_drum(r, sr, 0.8), bar0 + 2 * BEAT, gain=0.4, verb=0.35)
        if b % 4 == 3:
            for k, beat in enumerate((3.0, 3.25, 3.5, 3.75)):
                tr.add(tom(r, sr, 150 - k * 18, 0.7 + k * 0.1), bar0 + beat * BEAT, position=0.5 - k * 0.33, gain=0.42, verb=0.3)
        else:
            tr.add(war_drum(r, sr, 0.45, 1.25), bar0 + 3.5 * BEAT, position=-0.2, gain=0.4, verb=0.35)
        # Horns bark on the downbeat.
        tr.add(horn_stab([hz(root), hz(root + 12), hz(root + 19), hz(root + 24)], 0.9, r, sr), bar0, position=0.1, gain=0.34, verb=0.4)
        # Cellos driving sixteenths on the root, leaping to the octave and the fifth.
        for e in range(16):
            leap = 12 if e in (6, 14) else 7 if e == 11 else 0
            acc = 1.0 if e % 4 == 0 else 0.6
            tr.add(marcato(hz(root + leap), BEAT * 0.3, r, sr, acc), bar0 + e * BEAT / 4, position=-0.3, gain=0.2, verb=0.2)
            # The violas double it an octave up, so the drive carries on small speakers.
            tr.add(marcato(hz(root + leap + 12), BEAT * 0.3, r, sr, acc), bar0 + e * BEAT / 4, position=0.35, gain=0.16, verb=0.2)
    return master(tr.render(r, 2.0, 0.7, 0.15), sr, -19)


def menu(r):
    """The title: slower still, the cello's theme over a low choir, a bell, and the dark."""
    global BEAT, BAR
    old = BEAT, BAR
    BEAT = 60.0 / 58.0
    BAR = BEAT * 4
    length = BAR * BARS
    sr = MSR
    tr = Track(length, sr)
    tr.add_loop(lambda d: drone(hz(midi_of("D1")), d, r, sr, beat=0.4), gain=0.09, verb=0.3)
    lay_choir(tr, r, sr, 0.09, 1.0, octave=3)
    lay_strings(tr, r, sr, 0.06, 0.6, octave=2)
    for b, chord in enumerate(PROGRESSION):
        bar0 = b * BAR
        root = root_midi(chord, 2)
        if b % 4 == 0:
            tr.add(toll(hz(midi_of("D3")), 7.0, r, sr), bar0, position=-0.4, gain=0.15, verb=1.1)
            tr.add(war_drum(r, sr, 0.7, 0.85), bar0, gain=0.3, verb=0.8)
        if chord == "A":
            tr.add(horn([hz(root), hz(root + 7)], BAR, r, sr, peak=900), bar0, gain=0.11, verb=0.8)
    for note, at, dur in melody_events():
        tr.add(cello(note_freq(note), dur + 0.15, r, sr), at, position=0.1, gain=0.3, verb=0.7)
    for note, at, dur in list(melody_events())[:5]:
        tr.add(music_box(note_freq(note) * 2, 2.5, r, sr), at - 4 * BAR, position=0.5, gain=0.06, verb=1.2)
    out = master(tr.render(r, 4.0, 0.85, 0.32), sr, -21)
    BEAT, BAR = old
    return out


# ============================================================================ ambience

def stereo_bed(make, r, width=0.6):
    a = make(r)
    b = make(r)
    mid = (a + b) / 2
    return np.stack([mid + (a - mid) * width, mid + (b - mid) * width], axis=1)


def wind_layer(r, dur, sr, lo, hi, speed, moan=0.0):
    centers = lo + (hi - lo) * smooth_random(dur, speed, r, sr=sr)
    body = norm_rms(hp(tv_bandpass(pink(dur, r, sr), centers, 0.8, sr=sr), lo * 0.5, sr=sr), 0.25)
    gust = (0.3 + 0.7 * smooth_random(dur, speed * 0.6, r, sr=sr)) ** 1.6
    out = body * gust
    if moan:
        # A moan that slides about like wind in a gap, never settling on one note.
        pitch = r.uniform(240, 300) + 200 * smooth_random(dur, 0.18, r, sr=sr)
        w = tv_bandpass(noise(dur, r, sr), pitch, 0.07, sr=sr) * smooth_random(dur, 0.25, r, sr=sr) ** 3
        out += norm_rms(w, 0.25) * moan
    return out


def amb_mountain(r):
    sr = ASR
    dur = 40.0 + 2.0
    bed = stereo_bed(lambda rr: wind_layer(rr, dur, sr, 140, 650, 0.22, moan=0.6), r)
    rumble = norm_rms(lp(brown(dur, r, sr), 110, sr=sr), 0.25) * 0.5
    bed = make_loop(bed + pan(rumble, 0), 2.0, sr)
    length = len(bed)
    total = length / sr
    tr = Track(total, sr, tail=6.0)
    # Thunder rolling somewhere beyond the peaks.
    for _ in range(2):
        d = r.uniform(3, 5)
        roll = lp(brown(d, r, sr), 260, sr=sr) * env_points(d, [(0, 0), (0.4, 1), (d, 0)], sr=sr, curve=1.6)
        tr.add(norm_rms(roll, 0.25), r.uniform(2, total - 6), position=r.uniform(-0.6, 0.6), gain=0.3, verb=0.8)
    # Pebbles trickling down a far slope.
    for _ in range(4):
        trickle = np.zeros(n_of(1.5, sr))
        tt = 0.0
        while tt < 1.2:
            clk = modal([r.uniform(600, 1100), r.uniform(1500, 2200)], [0.01, 0.006], [1, 0.4], 0.05, sr=sr, r=r)
            i = n_of(tt, sr)
            trickle[i:i + len(clk)] += clk[:len(trickle) - i] * r.uniform(0.2, 1)
            tt += r.uniform(0.03, 0.2)
        tr.add(lp(trickle, 1800, sr=sr), r.uniform(0, total), position=r.uniform(-0.8, 0.8), gain=0.1, verb=1.0)
    # Something howling, very far away.
    hd = 3.2
    th = time(hd, sr)
    pitch = np.interp(th, [0, 0.6, 2.2, hd], [330, 520, 470, 360]) * (1 + 0.01 * np.sin(2 * np.pi * 5 * th))
    howl = additive(pitch, hd, [1, 0.35, 0.12], sr=sr, r=r) * env_points(hd, [(0, 0), (0.5, 1), (2.4, 0.8), (hd, 0)], sr=sr)
    tr.add(lp(howl, 1100, sr=sr), r.uniform(12, 26), position=-0.6, gain=0.025, verb=1.5)
    events = tr.render(r, 3.5, 0.8, 0.3)
    return master(bed + events, sr, -25, 0.8)


def amb_forest(r):
    sr = ASR
    dur = 40.0 + 2.0
    rustle = stereo_bed(lambda rr: norm_rms(bp(pink(dur, rr, sr), 1300, 0.7, sr=sr), 0.25) * (0.15 + 0.85 * smooth_random(dur, 0.3, rr, sr=sr) ** 2) * 0.35, r, 0.9)
    moan = stereo_bed(lambda rr: wind_layer(rr, dur, sr, 120, 450, 0.12, moan=0.8), r) * 0.7
    bed = make_loop(rustle + moan, 2.0, sr)
    length = len(bed)
    total = length / sr
    tr = Track(total, sr, tail=5.0)
    # An owl, far off: "hoo ... hoo-hoo".
    for at in (r.uniform(4, 12), r.uniform(24, 32)):
        for off, d in [(0, 0.5), (0.8, 0.24), (1.12, 0.55)]:
            tt = time(d, sr)
            hoot = additive(glide_exp(d, 360, 320, sr), d, [1, 0.12, 0.04], sr=sr, r=r)
            hoot *= np.sin(np.pi * np.clip(tt / d, 0, 1)) ** 0.7
            tr.add(lp(hoot, 800, sr=sr), at + off, position=-0.55, gain=0.06, verb=1.4)
    # Boughs creaking.
    for _ in range(4):
        d = r.uniform(0.9, 1.8)
        f = glide_exp(d, r.uniform(45, 70), r.uniform(70, 100), sr)
        creak = additive(f, d, saw_amps(40, 0.6), sr=sr, r=r)
        creak = bp(creak, 800, 1.2, sr=sr) * (0.4 + 0.6 * smooth_random(d, 22, r, sr=sr)) * np.sin(np.pi * np.linspace(0, 1, n_of(d, sr)))
        tr.add(creak, r.uniform(0, total - 2), position=r.uniform(-0.8, 0.8), gain=0.05, verb=0.9)
    # Whispers in the trees.
    for _ in range(4):
        d = r.uniform(0.9, 1.6)
        tr.add(whisper(d, r, sr), r.uniform(0, total - 2), position=r.choice([-1, 1]) * r.uniform(0.6, 0.95), gain=0.025, verb=1.0)
    # A twig snapping underfoot, somewhere close.
    for _ in range(3):
        snap = bp(noise(0.03, r, sr), r.uniform(900, 1600), 0.8, sr=sr) * env_exp(0.03, 0.006, sr=sr)
        tr.add(snap, r.uniform(0, total), position=r.uniform(-0.9, 0.9), gain=0.12, verb=0.6)
    # Water dripping in the dark.
    at = r.uniform(0, 3)
    while at < total:
        f0 = r.uniform(550, 900)
        dd = 0.08
        drip = sine(glide_exp(dd, f0, f0 * 1.6, sr), dd, sr) * env_exp(dd, 0.02, sr=sr)
        tr.add(drip, at, position=0.4, gain=0.03, verb=1.1)
        at += r.uniform(1.5, 4.5)
    events = tr.render(r, 3.5, 0.8, 0.3)
    return master(bed + events, sr, -25, 0.8)


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
