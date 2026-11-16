"""Synthesises every sound effect in the game from scratch: no samples, no downloads.

    JumpCraftGame/Assets/Resources/Audio/<name>.wav       one-shots and loops
    JumpCraftGame/Assets/Resources/Audio/<name>_<n>.wav   variants of the same sound

    python Tools/generate_sounds.py            all of them
    python Tools/generate_sounds.py jump ice   only names containing "jump" or "ice"

The palette is dark fantasy: weight in the lows and low mids, highs held back, nothing chirpy.
Magic sounds like low bells, swells and breath, not sparkles. Every sound passes through
darken() (sound_synth.py) before it is written, which turns down everything above about 3 kHz;
`dark` on each sound sets how much of the top survives.

Fire is built the way fire actually sounds: a low, fluttering combustion roar with dry,
broadband snaps of wood and ember (snaps()). Ringing pops (crackle()) read as droplets and
bubbles, so they are kept for electricity and ice, never fire.

SoundBank.cs groups files by name (dropping the _<n> suffix) and picks a variant at random each
time a sound plays; the mixing (bus, volume, voice limits) is set there, not here. Here each
sound is normalised to a loudness that suits what it is, so a footstep is quieter than a blast
even before the mixer touches it.

Loops are written seamless (their ends are cross-faded into their starts) and are never trimmed.

Output is deterministic: every sound is seeded from its own name.
"""
import sys
from pathlib import Path

import numpy as np

from sound_synth import (SR, additive, bp, brown, darken, env_exp, env_points, finish, fm,
                         glide_exp, hp, impulses, lp, make_loop, mix, modal, noise, norm_rms, pink,
                         place, resonator, reverb, rng_for, saw_amps, silence, sine, smooth_random,
                         soft_clip, time, tv_bandpass, tv_lowpass, write_wav, n_of)

OUT = Path(__file__).resolve().parent.parent / "JumpCraftGame/Assets/Resources/Audio"

REGISTRY = []


def sound(name, variants=1, level=-16.0, loop=None, dark=0.45):
    """Registers a sound. loop is the cross-fade in seconds for seamless loops; dark is how much
    of the top end (above ~3 kHz) survives, 1 being all of it."""
    def wrap(fn):
        REGISTRY.append((name, variants, level, loop, dark, fn))
        return fn
    return wrap


# ============================================================================ building blocks

def whoosh(dur, f0, f1, r, width=0.8, peak_at=0.5):
    """Air rushing past: noise through a band that sweeps from f0 to f1, swelling and fading."""
    x = tv_bandpass(pink(dur, r), glide_exp(dur, f0, f1), width)
    return x * env_points(dur, [(0, 0), (dur * peak_at, 1), (dur, 0)], curve=1.6)


def thump(dur, f0, f1, tau):
    """A low body hit: a sine falling in pitch."""
    return sine(glide_exp(dur, f0, f1), dur) * env_exp(dur, tau, attack=0.0015)


def boom(dur, r, f0=70, f1=28, tau=0.5):
    """A deep impact felt more than heard: a falling sub, a soft thud of air, a long rumble."""
    t = time(dur)
    sub = thump(dur, f0, f1, tau) * 1.2
    air = lp(noise(dur, r), 400) * env_exp(dur, 0.06, attack=0.004)
    rumble = lp(brown(dur, r), 160) * env_points(dur, [(0, 0), (0.06, 1), (dur, 0)], curve=2)
    return sub + air * 0.8 + norm_rms(rumble, 0.12)


def click(r, center, dur=0.012, q=1.2):
    """A tiny transient: a burst of filtered noise."""
    return bp(noise(dur, r), center, q) * env_exp(dur, dur / 4, attack=0.0003)


def crackle(dur, rate, r, lo=1200, hi=4500, ring=0.0022, envelope=None, amp=(0.2, 1.0)):
    """Electric and icy crackle: random pops that each ring briefly at their own pitch."""
    out = np.zeros(n_of(dur))
    bands = 7
    k = time(ring * 6)
    for b in range(bands):
        imp = impulses(dur, rate / bands, r, amp_range=amp, envelope=envelope)
        f = lo * (hi / lo) ** ((b + r.uniform(0, 1)) / bands)
        kernel = np.sin(2 * np.pi * f * k) * np.exp(-k / (ring * r.uniform(0.6, 1.4)))
        out += np.convolve(imp, kernel)[:len(imp)]
    return hp(out, lo * 0.7)


def snaps(dur, rate, r, lo=450, hi=2600, envelope=None, amp=(0.15, 1.0)):
    """Wood and ember snapping in a fire: tiny dry bursts of noise, not tones."""
    out = np.zeros(n_of(dur))
    bands = 5
    for b in range(bands):
        imp = impulses(dur, rate / bands, r, amp_range=amp, envelope=envelope)
        k = n_of(r.uniform(0.002, 0.006))
        kernel = r.standard_normal(k) * np.exp(-np.arange(k) / (k / 5))
        f = lo * (hi / lo) ** ((b + r.uniform()) / bands)
        kernel = bp(np.concatenate([kernel, np.zeros(96)]), f, 0.6)
        out += np.convolve(imp, kernel)[:len(imp)]
    peak = np.max(np.abs(out)) + 1e-9
    return out / peak


def flame(dur, r, roar=1.0, breath=0.5, snap=0.8, snap_rate=12, rumble=0.3, flutter=20, warmth=900):
    """A fire: a low combustion roar that flutters irregularly, the breath of the flame, the
    rumble beneath it, and wood snapping. warmth is how high the roar reaches (Hz)."""
    # The flutter is what makes a flame a flame: fast, irregular, never smooth like water.
    flut = (0.45 + 0.55 * smooth_random(dur, flutter, r)) * (0.75 + 0.25 * smooth_random(dur, flutter * 3.1, r))
    # The roar lives between ~120 Hz and warmth; below that is only a supporting rumble.
    body = norm_rms(hp(lp(brown(dur, r), warmth), 120), 0.25)
    body = soft_clip(body * flut * 1.6, 1.4) * 0.6
    swell = 0.75 + 0.25 * smooth_random(dur, 1.3, r)
    br = norm_rms(bp(pink(dur, r), 340, 0.7), 0.25) * (0.3 + 0.7 * smooth_random(dur, 5, r) ** 2)
    rum = norm_rms(bp(brown(dur, r), 75, 1.0), 0.25) * (0.6 + 0.4 * smooth_random(dur, 2, r))
    sn = snaps(dur, snap_rate, r, 600, 3200)
    return (body * roar + br * breath) * swell + rum * rumble + sn * snap


def whoomp(dur, r, top=1600, attack=0.03, tau=0.25, low=55):
    """Something catching fire all at once: a muffled rush that opens and closes, with a low thud."""
    t = time(dur)
    cutoff = 140 + top * env_points(dur, [(0, 0), (attack * 2.5, 1), (dur, 0.05)], curve=1.5)
    rush = hp(tv_lowpass(brown(dur, r), cutoff), 100) * env_points(dur, [(0, 0), (attack, 1), (dur, 0)], curve=1.3)
    rush *= 0.7 + 0.3 * smooth_random(dur, 25, r)
    return norm_rms(rush, 0.25) + thump(dur, low * 1.8, low * 0.8, tau * 0.4) * 0.35


def bell(freq, dur, r, ratio=1.4, index=1.6, tau=0.9, bright_tau=0.12):
    """A dark, heavy bell: inharmonic FM partials that soften quickly as it rings."""
    t = time(dur)
    idx_env = np.exp(-t / bright_tau)
    return fm(freq, dur, ratio, index, index_env=idx_env) * np.exp(-t / tau) * np.clip(t / 0.003, 0, 1)


def toll(freq, dur, r, tau=2.5):
    """A great bell far off: the strike, the hum an octave down, the slow beating as it dies."""
    t = time(dur)
    strike = bell(freq, dur, r, ratio=1.41, index=2.2, tau=tau, bright_tau=0.25)
    hum = sine(freq * 0.5, dur) * np.exp(-t / (tau * 1.4)) * 0.5 * (1 + 0.15 * np.sin(2 * np.pi * 1.3 * t))
    return lp(strike + hum, 2500)


def glass(freq, dur, r, tau=0.35):
    """A struck crystal: inharmonic partials. Kept low for this world: ice is heavy here."""
    ratios = [1.0, 2.76, 5.40]
    decays = [tau, tau * 0.45, tau * 0.2]
    amps = [1.0, 0.35, 0.12]
    return modal([freq * k for k in ratios], decays, amps, dur, r=r)


def bone_clack(r, pitch=1.0, dur=0.06, hollow=0.3):
    """Bone on bone or stone: a short, dry, woody knock."""
    base = r.uniform(700, 1050) * pitch
    m = modal([base, base * 1.83, base * 2.91], [0.012, 0.008, 0.004], [1.0, 0.55, 0.3], dur, r=r, jitter=0.04)
    body = modal([base * 0.32], [0.025], [hollow], dur, r=r)
    return mix(m + body, click(r, 2200, 0.006) * 0.5)


def rattle(dur, count, r, pitch=1.0, spread_curve=1.0):
    """Many clacks scattered over dur: a skeleton shaking."""
    out = silence(dur + 0.08)
    for _ in range(count):
        at = (r.uniform(0, 1) ** spread_curve) * dur
        place(out, bone_clack(r, pitch * r.uniform(0.85, 1.2)), at, r.uniform(0.35, 1.0))
    return out


def zap(r, dur=0.09, buzz_hz=None, bright=1800):
    """A single electric discharge: a hard snap, a hiss and a buzzing, growling tail."""
    buzz_hz = buzz_hz or r.uniform(70, 130)
    t = time(dur)
    jitter = 1 + 0.15 * smooth_random(dur, 80, r, lo=-1, hi=1)
    buzz = additive(buzz_hz * jitter, dur, saw_amps(30, 0.8), r=r)
    buzz = bp(buzz, bright, 0.6) * np.exp(-t / (dur * 0.35))
    hiss = bp(noise(dur, r), 2400, 0.8) * np.exp(-t / (dur * 0.2))
    body = thump(dur, 160, 70, dur * 0.25) * 0.5
    return mix(click(r, 2500, 0.004) * 1.4,
               hiss * 0.5 + buzz * 1.0 + body + crackle(dur, 260, r, 900, 3800, envelope=np.exp(-t / (dur * 0.3))) * 0.5)


def explosion(r, dur, size=1.0, low=50):
    """A blast: a falling sub, a burst of noise that darkens, debris and a rumble."""
    t = time(dur)
    sub = thump(dur, low * 2.2, low * 0.55, 0.22 * size)
    burst = tv_lowpass(brown(dur, r), glide_exp(dur, 3500, 150), 2.0) * env_exp(dur, 0.22 * size, attack=0.002)
    debris = snaps(dur, 90 * size, r, 400, 2200, envelope=np.exp(-t / (0.3 * size))) * 0.35
    rumble = lp(brown(dur, r), 150) * env_points(dur, [(0, 0), (0.05, 1), (dur, 0)], curve=2)
    return sub * 1.3 + burst * 1.4 + debris + rumble * 0.9


def sizzle(dur, r, tau=0.08, center=2600):
    """Heat meeting water: a frying hiss, held low."""
    t = time(dur)
    hiss = bp(noise(dur, r), center, 0.7) * np.exp(-t / tau)
    fry = snaps(dur, 220, r, 1200, 3800, envelope=np.exp(-t / tau)) * 0.3
    return hiss + fry


def steam(dur, r, attack=0.01, tau=0.4):
    t = time(dur)
    e = np.clip(t / attack, 0, 1) * np.exp(-t / tau) * np.clip((dur - t) / (dur * 0.3), 0, 1)
    return bp(pink(dur, r), 1600, 0.6) * e + bp(noise(dur, r), 3500, 1.0) * e * 0.3


def wind_bed(dur, r, lo=200, hi=900, speed=1.2, whistle=0.0, width=0.9):
    """Moving air: pink noise in a band that wanders, swelling in gusts, with an optional moan."""
    centers = lo + (hi - lo) * smooth_random(dur, speed, r)
    body = hp(tv_bandpass(pink(dur, r), centers, width), lo * 0.5)
    gust = 0.45 + 0.55 * smooth_random(dur, speed * 0.7, r)
    out = norm_rms(body, 0.25) * gust
    if whistle:
        w = resonator(noise(dur, r), r.uniform(380, 620), 50) * smooth_random(dur, 0.5, r) ** 2
        out += norm_rms(w, 0.25) * whistle
    return out


def swell(dur, r, f0, f1, width=0.5):
    """A reversed rush: air drawn in, rising to a point. Magic gathering."""
    x = tv_bandpass(pink(dur, r), glide_exp(dur, f0, f1), width)
    return x * env_points(dur, [(0, 0), (dur * 0.92, 1), (dur, 0)], curve=2.2)


def dark_chord(freqs, dur, r, cutoff=900, attack=0.3, rolloff=1.1):
    """Low strings or brass: detuned saws, softly filtered, swelling in."""
    t = time(dur)
    out = np.zeros(len(t))
    for f in freqs:
        for cents in (-6, 5):
            vib = 1 + 0.002 * np.sin(2 * np.pi * r.uniform(4, 5.5) * t + r.uniform(0, 6))
            out += additive(f * vib, dur, saw_amps(24, rolloff), r=r, detune_cents=cents)
    out = lp(out / (2 * len(freqs)), cutoff)
    return out * env_points(dur, [(0, 0), (attack, 1), (dur * 0.6, 0.8), (dur, 0)], curve=1.3)


# ============================================================================ the wizard

@sound("step_stone", variants=6, level=-24)
def step_stone(r, i):
    dur = 0.2
    heel = bp(noise(0.05, r), r.uniform(380, 650), 0.9) * env_exp(0.05, 0.012)
    body = thump(0.12, r.uniform(110, 140), 55, 0.028) * 0.9
    out = silence(dur)
    place(out, heel, 0)
    place(out, body, 0)
    toe = bp(noise(0.04, r), r.uniform(800, 1300), 1.1) * env_exp(0.04, 0.008)
    place(out, toe, r.uniform(0.035, 0.055), 0.4)
    grit = bp(noise(0.07, r), 1900, 1.0) * env_exp(0.07, 0.02) * 0.08
    place(out, grit, 0.01)
    return out


@sound("step_ice", variants=4, level=-24)
def step_ice(r, i):
    dur = 0.22
    t = time(dur)
    crunch = snaps(dur, 600, r, 900, 3200, envelope=np.exp(-t / 0.05)) * 0.6
    slide = bp(noise(dur, r), 2200, 1.2) * env_points(dur, [(0, 0), (0.02, 1), (dur, 0)]) * 0.12
    tick = glass(r.uniform(1200, 1700), 0.15, r, tau=0.04) * 0.2
    body = thump(0.1, 140, 70, 0.022) * 0.5
    return mix(crunch, slide, tick, body)


@sound("step_water", variants=4, level=-23)
def step_water(r, i):
    dur = 0.3
    t = time(dur)
    out = bp(noise(dur, r), r.uniform(700, 1100), 0.6) * env_exp(dur, 0.05, attack=0.004)
    for _ in range(r.integers(3, 6)):
        bd = r.uniform(0.02, 0.05)
        f0 = r.uniform(350, 750)
        bub = sine(glide_exp(bd, f0, f0 * r.uniform(1.5, 2.0)), bd) * env_exp(bd, bd / 3)
        place(out, bub, r.uniform(0.01, 0.15), r.uniform(0.15, 0.35))
    return out + thump(dur, 120, 60, 0.03) * 0.4


@sound("step_embers", variants=3, level=-23)
def step_embers(r, i):
    return mix(step_stone(r, i), sizzle(0.35, r, tau=0.12) * 0.5, snaps(0.3, 30, r, envelope=np.exp(-time(0.3) / 0.1)) * 0.25)


@sound("jump", variants=3, level=-21)
def jump(r, i):
    dur = 0.32
    robe = whoosh(dur, 200, 850, r, width=0.6, peak_at=0.3)
    push = thump(0.09, 110, 55, 0.025) * 0.6
    breath = bp(noise(0.12, r), 520, 1.5) * env_points(0.12, [(0, 0), (0.02, 1), (0.12, 0)]) * 0.25
    return mix(robe, push, breath)


@sound("land", variants=3, level=-21)
def land(r, i):
    dur = 0.3
    body = thump(0.16, 95, 40, 0.045)
    dust = lp(noise(dur, r), 700) * env_exp(dur, 0.05)
    cloth = bp(noise(0.12, r), 750, 0.8) * env_points(0.12, [(0, 0), (0.02, 1), (0.12, 0)])
    out = mix(body, dust * 0.7)
    place(out, cloth, 0.02, 0.4)
    return out


@sound("land_heavy", variants=2, level=-16)
def land_heavy(r, i):
    dur = 0.6
    body = boom(dur, r, 85, 30, 0.12)
    grit = snaps(0.4, 160, r, 500, 2200, envelope=np.exp(-time(0.4) / 0.08)) * 0.3
    cloth = bp(noise(0.2, r), 700, 0.7) * env_points(0.2, [(0, 0), (0.03, 1), (0.2, 0)])
    out = mix(body, grit)
    place(out, cloth, 0.03, 0.4)
    return out


@sound("hurt", variants=3, level=-15)
def hurt(r, i):
    dur = 0.55
    t = time(dur)
    impact = mix(thump(0.25, 130, 45, 0.06) * 1.3, click(r, 1100, 0.03, 0.8) * 0.8)
    # The ward cracking: a low, dissonant groan that sinks.
    f0 = r.uniform(140, 160)
    groan = additive(glide_exp(dur, f0, f0 * 0.62), dur, saw_amps(16, 1.2), r=r)
    groan += additive(glide_exp(dur, f0 * 1.06, f0 * 0.64), dur, saw_amps(16, 1.2), r=r)
    groan = lp(groan, 1100) * env_points(dur, [(0, 0), (0.01, 1), (0.15, 0.5), (dur, 0)], curve=1.5) * 0.3
    crack = glass(r.uniform(800, 1000), 0.3, r, tau=0.07) * 0.2
    return mix(impact, groan, crack)


@sound("death", level=-14)
def death(r, i):
    dur = 3.6
    t = time(dur)
    hit = mix(boom(1.5, r, 90, 26, 0.5), click(r, 1000, 0.04) * 0.6)
    # The light going out of him: a cluster collapsing downward into the dark.
    chord = np.zeros_like(t)
    for f in (146.8, 155.6, 220.0, 233.1):
        chord += additive(glide_exp(dur, f, f * 0.3), dur, saw_amps(12, 1.3), r=r)
    chord = tv_lowpass(chord, glide_exp(dur, 2000, 150)) * env_points(dur, [(0, 0), (0.05, 1), (1.4, 0.6), (dur, 0)]) * 0.22
    sigh = bp(pink(dur, r), 450, 1.2) * env_points(dur, [(0, 0), (0.4, 1), (dur, 0)], curve=1.5) * 0.2
    return reverb(mix(hit, chord, sigh), 3.0, 0.55, r)


@sound("heartbeat", level=-19, loop=0.02)
def heartbeat(r, i):
    period = 60 / 72
    beats = 4
    dur = period * beats + 0.02
    out = silence(dur)
    for b in range(beats):
        at = b * period
        lub = mix(thump(0.18, 70, 38, 0.05), lp(noise(0.08, r), 180) * env_exp(0.08, 0.015) * 0.4)
        dub = thump(0.15, 60, 34, 0.04) * 0.75
        place(out, lub, at)
        place(out, dub, at + 0.26)
    return lp(out, 350)


# ============================================================================ wands

@sound("wand_swap_fire", level=-19)
def wand_swap_fire(r, i):
    dur = 0.7
    scrape = bp(noise(0.08, r), 1300, 1.0) * env_exp(0.08, 0.02)
    out = mix(scrape * 0.6, silence(dur))
    place(out, whoomp(0.6, r, top=900, attack=0.05, low=60), 0.03, 0.8)
    place(out, snaps(0.5, 25, r, envelope=np.exp(-time(0.5) / 0.2)) * 0.3, 0.05)
    return out


@sound("wand_swap_ice", level=-20)
def wand_swap_ice(r, i):
    dur = 1.1
    out = silence(dur)
    place(out, glass(440.0, 1.0, r, tau=0.4), 0.0, 0.6)
    place(out, glass(659.3, 1.0, r, tau=0.35), 0.06, 0.4)
    place(out, crackle(0.3, 400, r, 1500, 4000, ring=0.001, envelope=np.exp(-time(0.3) / 0.08)) * 0.25, 0.0)
    place(out, thump(0.2, 180, 90, 0.04) * 0.4, 0.0)
    return reverb(out, 1.2, 0.35, r)


@sound("wand_swap_lightning", level=-19)
def wand_swap_lightning(r, i):
    dur = 0.6
    t = time(dur)
    thrum = additive(glide_exp(dur, 55, 110), dur, saw_amps(30, 0.9), r=r)
    thrum = bp(thrum, 700, 0.6) * env_points(dur, [(0, 0), (0.12, 1), (dur, 0)], curve=1.5) * 0.5
    out = mix(zap(r, 0.14), thrum)
    place(out, zap(r, 0.08, bright=1400) * 0.5, 0.18)
    return out


@sound("wand_swap_wind", level=-20)
def wand_swap_wind(r, i):
    dur = 0.65
    swirl = whoosh(dur, 220, 1100, r, width=0.5, peak_at=0.45) + whoosh(dur, 1000, 300, r, width=0.5, peak_at=0.6) * 0.6
    t = time(dur)
    moan = sine(glide_exp(dur, 196, 220), dur) * env_points(dur, [(0, 0), (0.25, 1), (dur, 0)]) * 0.07
    return mix(swirl, moan * (1 + 0.25 * np.sin(2 * np.pi * 5 * t)))


# Streams: start (the moment it bursts out), loop (while held) and end (as it is let go).

@sound("wand_fire_start", level=-14)
def wand_fire_start(r, i):
    dur = 0.7
    out = whoomp(dur, r, top=1800, attack=0.035, tau=0.3, low=48)
    return mix(out, snaps(dur, 70, r, envelope=np.exp(-time(dur) / 0.2)) * 0.4)


@sound("wand_fire_loop", level=-16, loop=0.25)
def wand_fire_loop(r, i):
    dur = 3.25
    t = time(dur)
    bed = flame(dur, r, roar=1.0, breath=0.5, snap=0.7, snap_rate=12, rumble=0.35, flutter=24, warmth=1000)
    # The jet: a throaty, guttural growl of burning gas, chopped by fast, ragged turbulence.
    chop = np.abs(smooth_random(dur, 45, r, -1, 1)) ** 0.7
    throat = norm_rms(bp(brown(dur, r), 220, 0.7), 0.25) * (0.4 + 0.6 * chop)
    return bed + throat * 0.5


@sound("wand_fire_end", level=-20)
def wand_fire_end(r, i):
    dur = 0.7
    t = time(dur)
    puff = hp(tv_lowpass(brown(dur, r), glide_exp(dur, 1400, 160)), 110) * env_exp(dur, 0.16, attack=0.005)
    return norm_rms(puff, 0.25) + snaps(dur, 25, r, 600, 3000, envelope=np.exp(-t / 0.25)) * 0.5


@sound("wand_ice_start", level=-16)
def wand_ice_start(r, i):
    dur = 0.7
    t = time(dur)
    burst = bp(noise(dur, r), 1500, 0.6) * env_exp(dur, 0.07) * 0.8
    crack = click(r, 1400, 0.025, 0.6) * 1.2
    out = mix(burst, crack, thump(0.25, 150, 60, 0.05) * 0.7)
    for k in range(5):
        place(out, glass(r.uniform(700, 1500), 0.5, r, tau=0.15) * 0.3, r.uniform(0, 0.08))
    cold = lp(noise(dur, r), 450) * env_exp(dur, 0.15) * 0.5
    return out + cold


@sound("wand_ice_loop", level=-19, loop=0.2)
def wand_ice_loop(r, i):
    # 14 shards a second leave the wand (WandStream.IceRate): each gets a crunch and a dull glint.
    dur = 3.0 + 0.2
    out = norm_rms(bp(pink(dur, r), 1400, 0.6), 0.25) * (0.3 + 0.15 * smooth_random(dur, 8, r))
    period = 1 / 14
    k = 0
    while k * period < dur:
        at = k * period + r.uniform(-0.006, 0.006)
        if at >= 0:
            crunch = bp(noise(0.03, r), r.uniform(1200, 2400), 1.0) * env_exp(0.03, 0.006)
            place(out, crunch, at, 0.7)
            if r.uniform() < 0.45:
                place(out, glass(r.uniform(900, 1900), 0.2, r, tau=r.uniform(0.03, 0.06)), at + 0.004, r.uniform(0.1, 0.22))
        k += 1
    cold = norm_rms(bp(pink(dur, r), 380, 0.7), 0.25) * 0.45
    return out + cold


@sound("wand_ice_end", level=-22)
def wand_ice_end(r, i):
    dur = 0.7
    out = silence(dur)
    for k in range(4):
        place(out, glass(r.uniform(800, 1600), 0.5, r, tau=0.12), k * 0.05 + r.uniform(0, 0.02), 0.5 - k * 0.1)
    return out


@sound("wand_lightning_start", level=-13)
def wand_lightning_start(r, i):
    dur = 0.8
    crack = mix(click(r, 2000, 0.02, 0.5) * 2.0, bp(noise(dur, r), 1800, 0.6) * env_exp(dur, 0.03) * 1.1)
    out = mix(crack, boom(dur, r, 120, 40, 0.1) * 0.8)
    place(out, zap(r, 0.25), 0.01, 0.9)
    return out


@sound("wand_lightning_loop", level=-17, loop=0.15)
def wand_lightning_loop(r, i):
    dur = 2.0 + 0.15
    t = time(dur)
    # The arc: a heavy, jittering electrical growl...
    f = 82 * (1 + 0.05 * smooth_random(dur, 30, r, -1, 1))
    growl = additive(f, dur, saw_amps(40, 0.75), r=r)
    growl = norm_rms(bp(growl, 900, 0.5), 0.25) * (0.5 + 0.5 * smooth_random(dur, 25, r))
    throb = norm_rms(lp(additive(55, dur, saw_amps(10, 1.0), r=r), 300), 0.25) * 0.4
    hiss = norm_rms(bp(noise(dur, r), 2600, 0.8), 0.25) * (0.15 + 0.1 * smooth_random(dur, 40, r))
    crk = crackle(dur, 300, r, 1000, 3800, ring=0.0009)
    out = growl + throb + hiss + norm_rms(crk, 0.25) * 0.35
    # ...and a snap for every bolt (14 a second, WandStream.BoltInterval).
    k = 0
    while k / 14 < dur:
        place(out, zap(r, 0.06, bright=r.uniform(1300, 2400)) * 1.5, k / 14 + r.uniform(0, 0.01), r.uniform(0.35, 0.65))
        k += 1
    return out


@sound("wand_lightning_end", level=-20)
def wand_lightning_end(r, i):
    dur = 0.45
    t = time(dur)
    fizz = crackle(dur, 300, r, 900, 3500, envelope=np.exp(-t / 0.1)) + bp(noise(dur, r), 2400, 0.8) * np.exp(-t / 0.05) * 0.3
    down = sine(glide_exp(0.3, 400, 60), 0.3) * env_exp(0.3, 0.08) * 0.35
    return mix(fizz, click(r, 1000, 0.02) * 0.8, down)


@sound("wand_wind_start", level=-15)
def wand_wind_start(r, i):
    dur = 0.7
    return mix(whoosh(dur, 180, 1300, r, width=0.9, peak_at=0.18) * 1.2, thump(0.25, 70, 32, 0.07) * 0.7)


@sound("wand_wind_loop", level=-18, loop=0.4)
def wand_wind_loop(r, i):
    dur = 3.4
    rush = wind_bed(dur, r, lo=220, hi=950, speed=2.2, width=1.0, whistle=0.25)
    hiss = norm_rms(bp(pink(dur, r), 1600, 0.6), 0.25) * (0.3 + 0.3 * smooth_random(dur, 4, r))
    buffet = norm_rms(lp(brown(dur, r), 140), 0.25) * (0.4 + 0.6 * smooth_random(dur, 5, r)) * 0.55
    return rush * 1.2 + hiss * 0.5 + buffet


@sound("wand_wind_end", level=-21)
def wand_wind_end(r, i):
    return whoosh(0.7, 900, 170, r, width=0.8, peak_at=0.1)


@sound("wand_empty", variants=2, level=-22)
def wand_empty(r, i):
    dur = 0.35
    t = time(dur)
    clk = mix(click(r, 1100, 0.015), click(r, 600, 0.02) * 0.6)
    sput = snaps(dur, 40, r, envelope=np.exp(-t / 0.08)) * 0.35
    puff = lp(noise(dur, r), 700) * env_exp(dur, 0.05) * 0.4
    return mix(clk, sput, puff)


@sound("wand_ready", level=-21)
def wand_ready(r, i):
    dur = 1.4
    out = silence(dur)
    place(out, swell(0.35, r, 250, 900) * 0.4, 0)
    place(out, bell(220.0, 1.2, r, tau=0.6), 0.33)
    place(out, bell(329.6, 1.0, r, tau=0.5) * 0.5, 0.36)
    return reverb(out, 1.2, 0.35, r)


@sound("backfire_fire", level=-12)
def backfire_fire(r, i):
    dur = 1.4
    t = time(dur)
    blast = whoomp(dur, r, top=2200, attack=0.02, tau=0.4, low=45) * 1.3
    return mix(blast, snaps(dur, 140, r, envelope=np.exp(-t / 0.35)) * 0.6, hurt(r, i) * 0.4)


@sound("backfire_ice", level=-14)
def backfire_ice(r, i):
    dur = 1.1
    crunch = crackle(0.5, 1400, r, 1000, 3500, ring=0.0012, envelope=np.clip(time(0.5) / 0.2, 0, 1) * np.exp(-time(0.5) / 0.3))
    clunk = mix(thump(0.25, 180, 70, 0.05), click(r, 1000, 0.02))
    out = silence(dur)
    place(out, crunch, 0)
    place(out, clunk, 0.2, 1.2)
    for k in range(7):
        place(out, glass(r.uniform(600, 1700), 0.6, r, tau=0.18), 0.2 + r.uniform(0, 0.15), 0.3)
    return out


@sound("backfire_lightning", level=-12)
def backfire_lightning(r, i):
    dur = 1.2
    out = silence(dur)
    place(out, wand_lightning_start(r, i), 0)
    for k in range(10):
        place(out, zap(r, 0.1), r.uniform(0.02, 0.6), r.uniform(0.4, 0.9))
    buzz = bp(additive(50, dur, saw_amps(40, 0.6), r=r), 600, 0.4) * env_points(dur, [(0, 0), (0.05, 1), (0.7, 0.5), (dur, 0)]) * 0.5
    return out + buzz


@sound("backfire_wind", level=-13)
def backfire_wind(r, i):
    dur = 1.0
    return mix(whoosh(dur, 1300, 140, r, width=1.0, peak_at=0.12) * 1.4, boom(0.6, r, 80, 30, 0.1))


# ============================================================================ elements hitting things

@sound("fire_hit", variants=4, level=-24)
def fire_hit(r, i):
    dur = 0.25
    puff = norm_rms(hp(lp(brown(dur, r), 900), 130), 0.25) * env_exp(dur, 0.05, attack=0.006)
    return mix(puff, snaps(dur, 60, r, 600, 3000, envelope=np.exp(-time(dur) / 0.06)) * 0.6)


@sound("ice_hit", variants=5, level=-22)
def ice_hit(r, i):
    dur = 0.3
    tink = glass(r.uniform(1000, 1800), dur, r, tau=r.uniform(0.04, 0.07))
    knock = modal([r.uniform(450, 650)], [0.02], [0.6], dur, r=r)
    return mix(tink, knock, click(r, 1800, 0.008) * 0.6)


@sound("ice_shatter", variants=3, level=-15)
def ice_shatter(r, i):
    dur = 1.0
    crash = bp(noise(dur, r), 1800, 0.5) * env_exp(dur, 0.09) * 0.8
    crunch = mix(thump(0.2, 200, 80, 0.04) * 0.9, click(r, 1100, 0.03) * 0.8)
    out = mix(crash, crunch)
    for k in range(18):
        at = r.uniform(0, 1) ** 2 * 0.45
        place(out, glass(r.uniform(700, 2600), 0.4, r, tau=r.uniform(0.03, 0.1)), at, r.uniform(0.15, 0.4) * (1 - at))
    return out


@sound("frost_form", variants=3, level=-22)
def frost_form(r, i):
    dur = 0.5
    t = time(dur)
    e = np.clip(t / 0.25, 0, 1) * np.exp(-np.maximum(t - 0.25, 0) / 0.08)
    return crackle(dur, 1100, r, 1200, 3800, ring=0.0009, envelope=e) + bp(noise(dur, r), 2500, 1.0) * e * 0.1


@sound("zap_hit", variants=4, level=-18)
def zap_hit(r, i):
    return mix(zap(r, 0.18), thump(0.12, 150, 60, 0.025) * 0.7)


@sound("zap_air", variants=3, level=-24)
def zap_air(r, i):
    return zap(r, 0.07, bright=r.uniform(1500, 2500)) * 0.8


# ============================================================================ characters' states

@sound("ignite", variants=2, level=-17)
def ignite(r, i):
    dur = 0.9
    t = time(dur)
    return mix(whoomp(dur, r, top=1300, attack=0.07, tau=0.35, low=52),
               snaps(dur, 60, r, envelope=np.clip(t / 0.1, 0, 1) * np.exp(-t / 0.4)) * 0.4)


@sound("burn_loop", level=-21, loop=0.3)
def burn_loop(r, i):
    return flame(3.3, r, roar=0.75, breath=0.4, snap=1.0, snap_rate=18, rumble=0.2, flutter=16, warmth=750)


@sound("freeze", variants=2, level=-17)
def freeze(r, i):
    dur = 1.1
    grow = crackle(0.45, 1300, r, 1000, 3500, ring=0.001, envelope=np.clip(time(0.45) / 0.4, 0, 1))
    creak = bp(additive(glide_exp(0.45, 70, 120), 0.45, saw_amps(30, 0.8), r=r), 900, 0.8) * np.clip(time(0.45) / 0.45, 0, 1) * 0.35
    lock = mix(thump(0.3, 200, 80, 0.06), click(r, 1300, 0.03) * 1.2)
    out = silence(dur)
    place(out, grow + creak, 0)
    place(out, lock, 0.42, 1.0)
    place(out, glass(587.3, 0.7, r, tau=0.35), 0.43, 0.35)
    place(out, glass(880.0, 0.7, r, tau=0.3), 0.46, 0.2)
    return out


@sound("thaw", variants=2, level=-20)
def thaw(r, i):
    dur = 0.9
    out = steam(dur, r, attack=0.02, tau=0.3)
    for k in range(4):
        bd = 0.04
        place(out, sine(glide_exp(bd, 450, 950), bd) * env_exp(bd, 0.01), r.uniform(0.2, 0.7), 0.3)
    return out


@sound("shock", variants=2, level=-18)
def shock(r, i):
    dur = 0.45
    t = time(dur)
    buzz = bp(additive(95 * (1 + 0.1 * smooth_random(dur, 60, r, -1, 1)), dur, saw_amps(40, 0.7), r=r), 1300, 0.6)
    buzz *= env_points(dur, [(0, 0), (0.01, 1), (0.3, 0.6), (dur, 0)]) * 0.7
    return mix(zap(r, 0.12), buzz, crackle(dur, 280, r, 900, 3500, envelope=np.exp(-t / 0.2)) * 0.4)


@sound("steam_puff", variants=3, level=-23)
def steam_puff(r, i):
    return steam(0.6, r, attack=0.01, tau=0.15)


# ============================================================================ skeletons

@sound("skel_step", variants=4, level=-27)
def skel_step(r, i):
    return mix(bone_clack(r, 0.75, 0.08, hollow=0.2), bp(noise(0.06, r), 1100, 1.0) * env_exp(0.06, 0.015) * 0.3)


@sound("skel_throw", variants=3, level=-19)
def skel_throw(r, i):
    dur = 0.6
    windup = rattle(0.12, 4, r, 0.9)
    # The bone whirring end over end as it leaves the hand.
    swish = whoosh(0.45, 300, 900, r, width=0.6, peak_at=0.25)
    swish *= 0.55 + 0.45 * np.abs(np.sin(2 * np.pi * 7 * time(0.45)))
    out = silence(dur)
    place(out, windup, 0, 0.6)
    place(out, swish, 0.1, 1.0)
    return out


@sound("skel_hurt", variants=3, level=-18)
def skel_hurt(r, i):
    knock = bone_clack(r, 0.5, 0.15, hollow=1.0) * 1.2
    hollow = resonator(noise(0.25, r) * env_exp(0.25, 0.02), r.uniform(240, 320), 12) * 0.5
    return mix(knock, hollow, rattle(0.22, 9, r, 1.0, 1.5) * 0.7)


@sound("skel_death", variants=2, level=-15)
def skel_death(r, i):
    dur = 2.6
    out = silence(dur)
    # The collapse: bones cascading down, bouncing to rest.
    for k in range(46):
        at = (r.uniform(0, 1) ** 1.6) * 1.1
        place(out, bone_clack(r, r.uniform(0.55, 1.15), 0.08, hollow=0.4), at, r.uniform(0.3, 1.0) * (1.1 - at * 0.6))
    place(out, thump(0.3, 120, 50, 0.06), 0.08, 1.0)
    skull = bone_clack(r, 0.4, 0.2, hollow=1.2)
    for j, at in enumerate([0.55, 0.85, 1.05, 1.18, 1.27]):
        place(out, skull, at, 0.8 * 0.7 ** j)
    # A ghostly breath leaving the bones, sinking.
    gd = 2.0
    t = time(gd)
    vib = 1 + 0.015 * np.sin(2 * np.pi * 5 * t)
    moan = additive(glide_exp(gd, 220, 95) * vib, gd, [1, 0.35, 0.15, 0.06], r=r)
    moan = lp(moan, 800) * env_points(gd, [(0, 0), (0.3, 1), (gd, 0)], curve=1.5) * 0.24
    place(out, moan, 0.1)
    return reverb(out, 1.8, 0.4, r)


@sound("skel_alert", variants=2, level=-20)
def skel_alert(r, i):
    dur = 0.7
    out = silence(dur)
    rate = r.uniform(15, 19)
    k = 0
    while k / rate < 0.45:
        place(out, bone_clack(r, 0.8, 0.05, hollow=0.5), k / rate, 0.9 - k * 0.04)
        k += 1
    hiss = bp(noise(dur, r), 1700, 0.7) * env_points(dur, [(0, 0), (0.1, 1), (dur, 0)]) * 0.18
    return out + hiss


@sound("bone_hit", variants=3, level=-14)
def bone_hit(r, i):
    return mix(bone_clack(r, 0.6, 0.12, hollow=0.8) * 1.4, thump(0.22, 120, 42, 0.06) * 1.3, click(r, 1500, 0.025, 0.7))


@sound("bone_clatter", variants=3, level=-21)
def bone_clatter(r, i):
    out = silence(0.6)
    at, gap, amp = 0.0, r.uniform(0.07, 0.11), 1.0
    for k in range(r.integers(3, 6)):
        place(out, bone_clack(r, r.uniform(0.75, 1.1)), at, amp)
        at += gap
        gap *= 0.65
        amp *= 0.6
    return out


@sound("bone_deflect", variants=2, level=-19)
def bone_deflect(r, i):
    out = whoosh(0.6, 1000, 260, r, width=0.6, peak_at=0.2)
    place(out, bell(311.1, 0.6, r, tau=0.25) * 0.35, 0.02)
    return out


# ============================================================================ items and the HUD
# Nothing chirpy: finds are low bells and breath, as if the wizard draws something in.

@sound("pickup", variants=3, level=-20)
def pickup(r, i):
    base = [293.66, 349.23, 220.0][i % 3]
    out = silence(1.3)
    place(out, swell(0.3, r, 250, 1000) * 0.5, 0)
    hum = additive(base, 0.3, [1, 0.4, 0.15], r=r) * env_points(0.3, [(0, 0), (0.3, 1)], curve=2.5) * 0.2
    place(out, hum, 0)
    place(out, bell(base, 1.0, r, tau=0.4), 0.29)
    place(out, thump(0.2, 110, 55, 0.05) * 0.3, 0.29)
    return reverb(out, 1.4, 0.35, r)


@sound("pickup_rare", level=-18)
def pickup_rare(r, i):
    out = silence(2.0)
    place(out, swell(0.45, r, 150, 800) * 0.6, 0)
    place(out, bell(146.8, 1.6, r, tau=0.8), 0.43)
    place(out, bell(220.0, 1.4, r, tau=0.6) * 0.5, 0.46)
    place(out, boom(0.8, r, 70, 35, 0.2) * 0.5, 0.43)
    return reverb(out, 2.0, 0.4, r)


@sound("key_get", level=-15)
def key_get(r, i):
    dur = 4.0
    out = silence(dur)
    # Something old answers: the air draws in, a great bell tolls, a minor chord rises from below.
    place(out, swell(0.8, r, 120, 1400) * 0.7, 0)
    place(out, toll(73.4, 3.2, r, tau=2.2), 0.78, 1.0)
    place(out, boom(1.2, r, 65, 28, 0.4) * 0.7, 0.78)
    chord = dark_chord([73.4, 110.0, 146.8, 174.6, 220.0], 3.0, r, cutoff=700, attack=0.8)
    place(out, chord * 0.5, 0.8)
    return reverb(out, 3.2, 0.5, r)


@sound("weapon_get", level=-14)
def weapon_get(r, i):
    dur = 3.2
    out = silence(dur)
    rise = tv_lowpass(brown(0.8, r), glide_exp(0.8, 150, 2500)) * env_points(0.8, [(0, 0), (0.75, 1), (0.8, 0)], curve=2)
    place(out, norm_rms(rise, 0.25), 0, 0.8)
    t = time(2.2)
    brass = dark_chord([73.4, 110.0, 146.8], 2.2, r, attack=0.05, rolloff=0.9)
    brass = tv_lowpass(brass, 300 + 1300 * np.exp(-t / 0.5))
    place(out, brass * 0.8, 0.78)
    place(out, boom(1.5, r, 75, 26, 0.45), 0.78)
    ring = modal([180, 180 * 2.32, 180 * 3.98], [1.2, 0.6, 0.3], [1.0, 0.5, 0.25], 2.0, r=r) * 0.25
    place(out, ring, 0.8)
    return reverb(out, 2.6, 0.45, r)


@sound("recipe_get", level=-18)
def recipe_get(r, i):
    out = silence(1.8)
    for at in (0.0, 0.12):
        flip = bp(noise(0.12, r), 1500, 0.6) * env_points(0.12, [(0, 0), (0.03, 1), (0.12, 0)], curve=1.5)
        flip *= 0.6 + 0.4 * smooth_random(0.12, 120, r)
        place(out, flip, at, 0.7)
    place(out, bell(174.6, 1.4, r, tau=0.6) * 0.7, 0.25)
    place(out, bell(220.0, 1.2, r, tau=0.5) * 0.4, 0.3)
    return reverb(out, 1.4, 0.35, r)


@sound("craft", level=-15)
def craft(r, i):
    dur = 2.6
    out = silence(dur)
    swirl = tv_bandpass(pink(1.0, r), glide_exp(1.0, 120, 1500), 0.5) * env_points(1.0, [(0, 0), (0.9, 1), (1.0, 0)], curve=1.8)
    swirl *= 0.7 + 0.3 * np.sin(2 * np.pi * np.cumsum(glide_exp(1.0, 3, 12)) / SR)
    place(out, swirl, 0, 0.8)
    place(out, boom(1.0, r, 80, 30, 0.25), 0.95, 0.9)
    for k, f in enumerate([146.8, 220.0, 293.7]):
        place(out, bell(f, 1.5, r, tau=0.7), 0.96 + k * 0.04, 0.6)
    return reverb(out, 2.0, 0.45, r)


@sound("item_drop", variants=2, level=-22)
def item_drop(r, i):
    out = silence(0.4)
    place(out, whoosh(0.2, 450, 900, r, width=0.6, peak_at=0.4) * 0.7, 0)
    place(out, bone_clack(r, 0.8) * 0.6, 0.18)
    return out


@sound("ui_click", variants=2, level=-22)
def ui_click(r, i):
    dur = 0.1
    pitch = 0.9 if i == 0 else 1.05
    knock = modal([720 * pitch, 1650 * pitch], [0.012, 0.006], [1, 0.35], dur, r=r)
    return mix(knock, click(r, 1200, 0.008) * 0.5, thump(0.05, 180, 120, 0.01) * 0.3)


@sound("ui_hover", level=-31)
def ui_hover(r, i):
    return modal([950], [0.008], [1], 0.05, r=r)


@sound("ui_open", level=-22)
def ui_open(r, i):
    out = whoosh(0.4, 250, 1100, r, width=0.5, peak_at=0.6)
    place(out, bp(noise(0.1, r), 1300, 0.7) * env_exp(0.1, 0.03) * 0.4, 0.25)
    return out


@sound("ui_close", level=-23)
def ui_close(r, i):
    out = whoosh(0.35, 1000, 230, r, width=0.5, peak_at=0.35)
    place(out, thump(0.1, 130, 70, 0.02) * 0.6, 0.28)
    return out


@sound("notify", level=-20)
def notify(r, i):
    out = silence(1.5)
    place(out, bell(293.66, 1.2, r, tau=0.5), 0)
    place(out, bell(349.23, 1.2, r, tau=0.55), 0.14)
    return reverb(out, 1.3, 0.35, r)


@sound("menu_start", level=-13)
def menu_start(r, i):
    dur = 3.5
    out = silence(dur)
    place(out, swell(1.0, r, 100, 1800) * 0.8, 0)
    place(out, boom(2.0, r, 70, 24, 0.6) * 1.1, 0.97)
    place(out, dark_chord([73.4, 110.0, 146.8, 174.6, 220.0], 2.4, r, cutoff=800, attack=0.06) * 0.6, 0.97)
    place(out, toll(146.8, 2.4, r, tau=1.6) * 0.6, 0.98)
    return reverb(out, 2.8, 0.5, r)


@sound("sting_death", level=-16)
def sting_death(r, i):
    dur = 4.5
    out = silence(dur)
    chord = dark_chord([73.4, 77.8, 110.0, 138.6], 3.8, r, cutoff=700, attack=0.4)
    place(out, chord * 0.6, 0.2)
    place(out, toll(55.0, 3.8, r, tau=2.0), 0, 0.8)
    return reverb(out, 3.2, 0.55, r)


# ============================================================================ reactions

@sound("steam_boom", variants=2, level=-11)
def steam_boom(r, i):
    dur = 2.8
    out = mix(explosion(r, 1.4, 1.0), silence(dur))
    place(out, steam(dur, r, attack=0.03, tau=0.9) * 0.8, 0.02)
    return reverb(out, 2.0, 0.4, r)


@sound("steam_loop", level=-20, loop=0.4)
def steam_loop(r, i):
    dur = 3.4
    hiss = norm_rms(bp(pink(dur, r), 1400, 0.6), 0.25) * (0.6 + 0.4 * smooth_random(dur, 3, r))
    seethe = norm_rms(bp(noise(dur, r), 2800, 1.0), 0.25) * (0.5 + 0.5 * smooth_random(dur, 7, r)) * 0.35
    out = hiss + seethe
    for k in range(int(dur * 7)):
        bd = r.uniform(0.02, 0.05)
        f0 = r.uniform(220, 500)
        place(out, sine(glide_exp(bd, f0, f0 * 2.0), bd) * env_exp(bd, bd / 3), r.uniform(0, dur - bd), 0.12)
    return out


@sound("plasma_boom", variants=2, level=-10)
def plasma_boom(r, i):
    dur = 2.4
    out = mix(explosion(r, 1.3, 1.1), silence(dur))
    t = time(1.2)
    wobble = fm(glide_exp(1.2, 300, 40), 1.2, 1.5, 4.0, index_env=np.exp(-t / 0.4)) * env_exp(1.2, 0.35, attack=0.003) * 0.4
    place(out, lp(wobble, 2000), 0)
    for k in range(12):
        place(out, zap(r, 0.12), r.uniform(0, 0.7), r.uniform(0.3, 0.8))
    return reverb(out, 1.8, 0.4, r)


@sound("plasma_loop", level=-17, loop=0.2)
def plasma_loop(r, i):
    dur = 2.2
    hum = norm_rms(fm(58 * (1 + 0.02 * smooth_random(dur, 6, r, -1, 1)), dur, 2.01, 1.6), 0.25) * 0.8
    roar = flame(dur, r, roar=0.7, breath=0.3, snap=0.3, snap_rate=12, rumble=0.4, flutter=26) * 0.8
    crk = norm_rms(crackle(dur, 220, r, 900, 3500, ring=0.0009), 0.25) * 0.4
    out = hum + roar + crk
    for k in range(int(dur * 4)):
        place(out, zap(r, 0.08), r.uniform(0, dur - 0.08), r.uniform(0.2, 0.5))
    return out


@sound("vortex_start", level=-12)
def vortex_start(r, i):
    dur = 1.8
    roar = whoomp(dur, r, top=1500, attack=0.4, tau=0.6, low=40) * 1.2
    spin = whoosh(dur, 160, 900, r, width=0.7, peak_at=0.4) * (0.6 + 0.4 * np.sin(2 * np.pi * np.cumsum(glide_exp(dur, 2, 6)) / SR))
    return mix(roar, spin)


@sound("vortex_loop", level=-15, loop=0.25)
def vortex_loop(r, i):
    dur = 3.25
    t = time(dur)
    bed = flame(dur, r, roar=1.2, breath=0.6, snap=0.7, snap_rate=14, rumble=0.45, flutter=22, warmth=1000)
    # The fire whirling round: a band sweeping up and down twice a second.
    whirl = norm_rms(tv_bandpass(pink(dur, r), 500 + 300 * np.sin(2 * np.pi * 2.0 * t), 0.5), 0.25)
    return bed + whirl * 0.6


@sound("blizzard_start", level=-14)
def blizzard_start(r, i):
    out = whoosh(1.5, 180, 1200, r, width=0.9, peak_at=0.35) * 1.2
    place(out, glass(659.3, 1.0, r, tau=0.35) * 0.35, 0.2)
    place(out, glass(880.0, 1.0, r, tau=0.3) * 0.25, 0.26)
    return out


@sound("blizzard_loop", level=-17, loop=0.4)
def blizzard_loop(r, i):
    dur = 3.6
    howl = wind_bed(dur, r, lo=200, hi=800, speed=1.0, whistle=0.7, width=0.5)
    hiss = norm_rms(bp(pink(dur, r), 1600, 0.6), 0.25) * (0.5 + 0.5 * smooth_random(dur, 2, r)) * 0.4
    hail = snaps(dur, 110, r, 1000, 3000) * 0.3
    out = howl + hiss + hail
    for k in range(int(dur * 2)):
        place(out, glass(r.uniform(800, 1600), 0.3, r, tau=0.06) * 0.06, r.uniform(0, dur - 0.3))
    return out


@sound("frost_plate", variants=3, level=-16)
def frost_plate(r, i):
    out = silence(1.4)
    place(out, frost_form(r, i) * 0.9, 0)
    place(out, zap(r, 0.15), 0.12, 0.8)
    base = [293.7, 329.6, 220.0][i % 3]
    for k, m in enumerate([1.0, 1.5, 2.0]):
        place(out, glass(base * m, 1.0, r, tau=0.4), 0.14 + k * 0.04, 0.4 / (k + 1) ** 0.5)
    return reverb(out, 1.4, 0.35, r)


@sound("electric_hum_loop", level=-21, loop=0.2)
def electric_hum_loop(r, i):
    dur = 2.4
    hum = additive(73 * (1 + 0.005 * smooth_random(dur, 4, r, -1, 1)), dur, saw_amps(30, 1.1), r=r)
    hum = norm_rms(lp(hum, 1100), 0.25) * (0.8 + 0.2 * smooth_random(dur, 10, r))
    out = hum + norm_rms(crackle(dur, 35, r, 1200, 3800, ring=0.0009), 0.25) * 0.3
    for k in range(int(dur * 2)):
        place(out, zap_air(r, k) * 0.5, r.uniform(0, dur - 0.1))
    return out


@sound("storm_start", level=-10)
def storm_start(r, i):
    return thunder(r, 0)


@sound("thunder", variants=3, level=-11)
def thunder(r, i):
    dur = 3.4
    t = time(dur)
    crack = mix(click(r, 1600, 0.03, 0.4) * 2.0, bp(noise(0.3, r), 1500, 0.5) * env_exp(0.3, 0.04) * 1.1)
    out = mix(crack, silence(dur))
    place(out, zap(r, 0.25) * 0.8, 0.0)
    # The roll: brown noise in waves, each softer and darker than the last.
    roll = np.zeros_like(t)
    for k in range(r.integers(4, 7)):
        at = r.uniform(0.05, 1.4)
        ln = r.uniform(0.6, 1.5)
        seg = lp(brown(ln, r), r.uniform(200, 450)) * env_points(ln, [(0, 0), (ln * 0.2, 1), (ln, 0)], curve=1.5)
        place(roll, seg, at, r.uniform(0.5, 1.0) * (1 - at / 2))
    out += roll * 1.6
    out += thump(dur, 55, 24, 0.7) * 0.7
    return reverb(out, 2.8, 0.4, r)


@sound("storm_loop", level=-15, loop=0.4)
def storm_loop(r, i):
    dur = 3.6
    roar = norm_rms(lp(brown(dur, r), 400), 0.25) * (0.7 + 0.3 * smooth_random(dur, 2.5, r))
    spin = wind_bed(dur, r, lo=150, hi=700, speed=3.0, width=0.6) * 0.8
    out = roar + spin + norm_rms(crackle(dur, 50, r, 1000, 3500, ring=0.001), 0.25) * 0.3
    for k in range(int(dur * 2.5)):
        place(out, zap(r, 0.1) * 0.4, r.uniform(0, dur - 0.1))
    return out


# ============================================================================ the world

@sound("prop_douse", level=-18)
def prop_douse(r, i):
    dur = 1.2
    return mix(sizzle(dur, r, tau=0.3) * 0.8, steam(dur, r, attack=0.02, tau=0.4))


@sound("prop_burnaway", level=-15)
def prop_burnaway(r, i):
    dur = 1.8
    t = time(dur)
    out = silence(dur)
    for k in range(6):
        place(out, mix(click(r, r.uniform(350, 900), 0.04, 0.6) * 1.2, thump(0.14, 130, 55, 0.035) * 0.6), r.uniform(0, 0.5), r.uniform(0.4, 0.9))
    out += norm_rms(lp(brown(dur, r), 900), 0.25) * env_exp(dur, 0.3, attack=0.02)
    out += snaps(dur, 90, r, envelope=np.exp(-t / 0.5)) * 0.5
    return out


@sound("pendulum_whoosh", variants=3, level=-17)
def pendulum_whoosh(r, i):
    dur = 0.75
    swoosh = whoosh(dur, 160, 600, r, width=0.6, peak_at=0.5) * 1.3
    hum = sine(glide_exp(dur, 70, 100), dur) * env_points(dur, [(0, 0), (0.37, 1), (dur, 0)], curve=1.8) * 0.3
    chain = snaps(dur, 16, r, 1200, 2800, envelope=env_points(dur, [(0, 0.2), (0.37, 1), (dur, 0.2)])) * 0.2
    return mix(swoosh, hum, chain)


@sound("pendulum_hit", variants=2, level=-12)
def pendulum_hit(r, i):
    dur = 1.3
    base = r.uniform(240, 300)
    ring = modal([base, base * 2.32, base * 3.98, base * 5.81], [0.7, 0.45, 0.25, 0.12], [1.0, 0.6, 0.35, 0.2], dur, r=r) * 0.45
    slash = bp(noise(0.15, r), 1800, 0.6) * env_exp(0.15, 0.03)
    return mix(ring, slash, thump(0.3, 130, 45, 0.06) * 1.2)


@sound("lift_charge", level=-18)
def lift_charge(r, i):
    dur = 1.4
    rise = additive(glide_exp(dur, 45, 110), dur, saw_amps(30, 1.0), r=r)
    rise = tv_lowpass(rise, glide_exp(dur, 200, 1500)) * env_points(dur, [(0, 0), (0.8, 1), (dur, 0)]) * 0.45
    out = mix(rise, zap(r, 0.1) * 0.5)
    place(out, bell(146.8, 1.0, r, tau=0.5) * 0.35, 0.5)
    place(out, bell(220.0, 0.9, r, tau=0.45) * 0.25, 0.62)
    return out


@sound("lift_loop", level=-21, loop=0.3)
def lift_loop(r, i):
    dur = 2.3
    grind = norm_rms(lp(brown(dur, r), 500), 0.25) * (0.6 + 0.4 * smooth_random(dur, 12, r))
    scrape = norm_rms(bp(noise(dur, r), 900, 1.2), 0.25) * (0.3 + 0.7 * smooth_random(dur, 18, r) ** 3) * 0.5
    return grind + scrape + snaps(dur, 70, r, 500, 2000) * 0.3


@sound("lift_stop", level=-16)
def lift_stop(r, i):
    dur = 0.6
    return mix(boom(dur, r, 95, 35, 0.1) * 1.2, snaps(0.3, 160, r, envelope=np.exp(-time(0.3) / 0.05)) * 0.3)


# ============================================================================ output

def main(filters):
    OUT.mkdir(parents=True, exist_ok=True)
    made = 0
    for name, variants, level, loop, dark, fn in REGISTRY:
        if filters and not any(f in name for f in filters):
            continue
        for v in range(variants):
            file_name = name if variants == 1 else f"{name}_{v + 1}"
            r = rng_for(file_name)
            x = darken(np.asarray(fn(r, v), dtype=float), high_gain=dark)
            if loop:
                x = finish(make_loop(x, loop), level, trim=False)
            else:
                x = finish(x, level)
            write_wav(OUT / f"{file_name}.wav", x)
            made += 1
    print(f"{made} sounds written to {OUT}")


if __name__ == "__main__":
    main(sys.argv[1:])
