"""Synthesises every sound effect in the game from scratch: no samples, no downloads.

    JumpCraftGame/Assets/Resources/Audio/<name>.wav       one-shots and loops
    JumpCraftGame/Assets/Resources/Audio/<name>_<n>.wav   variants of the same sound

    python Tools/generate_sounds.py            all of them
    python Tools/generate_sounds.py jump ice   only names containing "jump" or "ice"

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

from sound_synth import (SR, additive, bp, brown, env_exp, env_points, finish, fm, glide_exp, hp,
                         impulses, lp, make_loop, mix, modal, noise, pink, place, ramp, resonator,
                         reverb, rng_for, saw_amps, silence, sine, smooth_random, soft_clip, time,
                         tv_bandpass, tv_lowpass, write_wav, n_of)

OUT = Path(__file__).resolve().parent.parent / "JumpCraftGame/Assets/Resources/Audio"

REGISTRY = []


def sound(name, variants=1, level=-16.0, loop=None):
    """Registers a sound. loop is the cross-fade in seconds for seamless loops."""
    def wrap(fn):
        REGISTRY.append((name, variants, level, loop, fn))
        return fn
    return wrap


# ============================================================================ building blocks

def whoosh(dur, f0, f1, r, width=0.8, peak_at=0.5, color=-3.0):
    """Air rushing past: noise through a band that sweeps from f0 to f1, swelling and fading."""
    n = pink(dur, r) if color == -3.0 else noise(dur, r)
    centers = glide_exp(dur, f0, f1)
    x = tv_bandpass(n, centers, width)
    e = env_points(dur, [(0, 0), (dur * peak_at, 1), (dur, 0)], curve=1.6)
    return x * e


def thump(dur, f0, f1, tau, sr=SR):
    """A low body hit: a sine falling in pitch."""
    return sine(glide_exp(dur, f0, f1), dur) * env_exp(dur, tau, attack=0.0015)


def click(r, center, dur=0.012, q=1.2):
    """A tiny transient: a burst of filtered noise."""
    return bp(noise(dur, r), center, q) * env_exp(dur, dur / 4, attack=0.0003)


def crackle(dur, rate, r, lo=1500, hi=6000, ring=0.0025, envelope=None, amp=(0.2, 1.0)):
    """Fire and electric crackle: random pops, each a tiny ringing burst."""
    # Each pop rings at its own pitch (a handful of kernels shared out at random), so the crackle
    # never settles into a tone.
    out = np.zeros(n_of(dur))
    bands = 7
    k = time(ring * 6)
    for b in range(bands):
        imp = impulses(dur, rate / bands, r, amp_range=amp, envelope=envelope)
        f = lo * (hi / lo) ** ((b + r.uniform(0, 1)) / bands)
        kernel = np.sin(2 * np.pi * f * k) * np.exp(-k / (ring * r.uniform(0.6, 1.4)))
        out += np.convolve(imp, kernel)[:len(imp)]
    return hp(out, lo * 0.7)


def bell(freq, dur, r, ratio=3.5, index=2.2, tau=0.6, bright_tau=0.15):
    """An FM bell: metallic partials that soften as it rings."""
    t = time(dur)
    idx_env = np.exp(-t / bright_tau)
    return fm(freq, dur, ratio, index, index_env=idx_env) * np.exp(-t / tau) * np.clip(t / 0.002, 0, 1)


def glass(freq, dur, r, tau=0.35):
    """A glassy chime: the inharmonic partials of a struck crystal."""
    ratios = [1.0, 2.76, 5.40, 8.93]
    decays = [tau, tau * 0.55, tau * 0.3, tau * 0.18]
    amps = [1.0, 0.5, 0.3, 0.15]
    return modal([freq * k for k in ratios], decays, amps, dur, r=r)


def bone_clack(r, pitch=1.0, dur=0.06, hollow=0.3):
    """Bone on bone or stone: a short, dry, woody knock."""
    base = r.uniform(950, 1350) * pitch
    m = modal([base, base * 1.83, base * 2.91, base * 4.12], [0.012, 0.008, 0.005, 0.003],
              [1.0, 0.6, 0.4, 0.25], dur, r=r, jitter=0.04)
    hollow_body = modal([base * 0.32], [0.02], [hollow], dur, r=r)
    return mix(m + hollow_body, click(r, 3200, 0.006) * 0.6)


def rattle(dur, count, r, pitch=1.0, spread_curve=1.0):
    """Many clacks scattered over dur: a skeleton shaking."""
    out = silence(dur + 0.08)
    for _ in range(count):
        at = (r.uniform(0, 1) ** spread_curve) * dur
        place(out, bone_clack(r, pitch * r.uniform(0.85, 1.2)), at, r.uniform(0.35, 1.0))
    return out


def zap(r, dur=0.09, buzz_hz=None, bright=3000):
    """A single electric discharge: a hard click, a hiss and a buzzing tail."""
    buzz_hz = buzz_hz or r.uniform(90, 180)
    t = time(dur)
    jitter = 1 + 0.15 * smooth_random(dur, 80, r, lo=-1, hi=1)
    buzz = additive(buzz_hz * jitter, dur, saw_amps(30, 0.8), r=r)
    buzz = bp(buzz, bright, 0.7) * np.exp(-t / (dur * 0.35))
    hiss = hp(noise(dur, r), 2500) * np.exp(-t / (dur * 0.2))
    return mix(click(r, 4000, 0.004) * 1.5, hiss * 0.7 + buzz * 0.9 + crackle(dur, 300, r, envelope=np.exp(-t / (dur * 0.3))) * 0.6)


def explosion(r, dur, size=1.0, low=55):
    """A blast: a falling sub thump, a burst of noise that darkens, debris and a rumble."""
    t = time(dur)
    sub = thump(dur, low * 2.2, low * 0.6, 0.18 * size)
    burst = tv_lowpass(brown(dur, r), glide_exp(dur, 6000, 250), 2.0) * env_exp(dur, 0.22 * size, attack=0.002)
    crackly = crackle(dur, 120 * size, r, 800, 4000, envelope=np.exp(-t / (0.25 * size)))
    rumble = lp(brown(dur, r), 180) * env_points(dur, [(0, 0), (0.05, 1), (dur, 0)], curve=2)
    return sub * 1.2 + burst * 1.4 + crackly * 0.5 + rumble * 0.8


def sizzle(dur, r, tau=0.08, center=5000):
    """Something hot meeting something wet: a bright, frying hiss."""
    t = time(dur)
    hiss = bp(noise(dur, r), center, 0.8) * np.exp(-t / tau)
    pops = crackle(dur, 260, r, 3000, 8000, envelope=np.exp(-t / tau))
    return hiss + pops * 0.8


def steam(dur, r, attack=0.01, tau=0.4):
    t = time(dur)
    e = np.clip(t / attack, 0, 1) * np.exp(-t / tau) * np.clip((dur - t) / (dur * 0.3), 0, 1)
    return hp(pink(dur, r), 1800) * e + bp(noise(dur, r), 6500, 1.2) * e * 0.5


def fire_bed(dur, r, roar=1.0, hiss=0.5, pops=1.0, pop_rate=30, rumble=0.4):
    """The body of a flame: a soft roar, its hiss, crackles, and a low rumble, all breathing."""
    breathe = 0.65 + 0.35 * smooth_random(dur, 6, r)
    roar_sig = lp(brown(dur, r), 900) * breathe
    hiss_sig = bp(pink(dur, r), 2400, 0.7) * (0.5 + 0.5 * smooth_random(dur, 9, r))
    pop_sig = crackle(dur, pop_rate, r, 1800, 7000, ring=0.0018)
    rum = lp(brown(dur, r), 120) * (0.6 + 0.4 * smooth_random(dur, 3, r))
    return roar_sig * roar + hiss_sig * hiss + pop_sig * pops + rum * rumble


def wind_bed(dur, r, lo=350, hi=1600, speed=1.2, whistle=0.0, width=0.9):
    """Moving air: pink noise in a band that wanders, swelling in gusts, with an optional whistle."""
    centers = lo + (hi - lo) * smooth_random(dur, speed, r)
    body = hp(tv_bandpass(pink(dur, r), centers, width), lo * 0.5)
    gust = 0.45 + 0.55 * smooth_random(dur, speed * 0.7, r)
    out = body * gust
    if whistle:
        w = resonator(noise(dur, r), r.uniform(900, 1400), 60) * smooth_random(dur, 0.5, r) ** 2
        out += w * whistle
    return out


# ============================================================================ the wizard

@sound("step_stone", variants=6, level=-24)
def step_stone(r, i):
    dur = 0.2
    heel = bp(noise(0.05, r), r.uniform(700, 1100), 0.9) * env_exp(0.05, 0.012)
    body = thump(0.12, r.uniform(140, 180), 70, 0.025) * 0.8
    out = silence(dur)
    place(out, heel, 0)
    place(out, body, 0)
    # The toe rolls down a moment after the heel; grit scuffs under the sole.
    toe = bp(noise(0.04, r), r.uniform(1400, 2200), 1.1) * env_exp(0.04, 0.008)
    place(out, toe, r.uniform(0.035, 0.055), 0.45)
    grit = hp(noise(0.07, r), 3000) * env_exp(0.07, 0.02) * 0.12
    place(out, grit, 0.01)
    return lp(out, 5500)


@sound("step_ice", variants=4, level=-24)
def step_ice(r, i):
    dur = 0.22
    t = time(dur)
    crunch = crackle(dur, 900, r, 2500, 7000, ring=0.0012, envelope=np.exp(-t / 0.05))
    slide = bp(noise(dur, r), 4500, 1.5) * env_points(dur, [(0, 0), (0.02, 1), (dur, 0)]) * 0.25
    tick = glass(r.uniform(2800, 3800), 0.15, r, tau=0.05) * 0.25
    body = thump(0.1, 160, 80, 0.02) * 0.4
    return mix(crunch, slide, tick, body)


@sound("step_water", variants=4, level=-23)
def step_water(r, i):
    dur = 0.3
    t = time(dur)
    splash = bp(noise(dur, r), r.uniform(1200, 1800), 0.6) * env_exp(dur, 0.05, attack=0.004)
    out = splash
    for _ in range(r.integers(3, 6)):
        bd = r.uniform(0.02, 0.05)
        f0 = r.uniform(500, 1100)
        bub = sine(glide_exp(bd, f0, f0 * r.uniform(1.6, 2.4)), bd) * env_exp(bd, bd / 3)
        place(out, bub, r.uniform(0.01, 0.15), r.uniform(0.2, 0.45))
    drip = hp(noise(dur, r), 5000) * np.exp(-t / 0.1) * 0.15
    return out + drip


@sound("step_embers", variants=3, level=-23)
def step_embers(r, i):
    dur = 0.35
    base = step_stone(r, i)
    s = sizzle(dur, r, tau=0.12) * 0.6
    return mix(base, s)


@sound("jump", variants=3, level=-20)
def jump(r, i):
    dur = 0.32
    robe = whoosh(dur, 380, 1700, r, width=0.6, peak_at=0.3)
    push = thump(0.08, 130, 70, 0.02) * 0.5
    # A faint magical lift under the robe.
    t = time(dur)
    shimmer = sine(glide_exp(dur, 620, 1240), dur) * env_points(dur, [(0, 0), (0.05, 1), (dur, 0)]) * 0.12
    shimmer += sine(glide_exp(dur, 930, 1860), dur) * env_points(dur, [(0, 0), (0.07, 1), (dur, 0)]) * 0.06
    return mix(robe, push, shimmer * (0.8 + 0.4 * np.sin(2 * np.pi * 18 * t)))


@sound("land", variants=3, level=-21)
def land(r, i):
    dur = 0.3
    body = thump(0.16, 110, 48, 0.04)
    dust = lp(noise(dur, r), 900) * env_exp(dur, 0.05)
    cloth = bp(noise(0.12, r), 1100, 0.8) * env_points(0.12, [(0, 0), (0.02, 1), (0.12, 0)])
    out = mix(body, dust * 0.7)
    place(out, cloth, 0.02, 0.4)
    return out


@sound("land_heavy", variants=2, level=-17)
def land_heavy(r, i):
    dur = 0.5
    body = thump(0.3, 95, 36, 0.08) * 1.3
    dust = lp(noise(dur, r), 700) * env_exp(dur, 0.1)
    grit = crackle(0.4, 220, r, 1500, 5000, envelope=np.exp(-time(0.4) / 0.08)) * 0.4
    cloth = bp(noise(0.2, r), 900, 0.7) * env_points(0.2, [(0, 0), (0.03, 1), (0.2, 0)])
    out = mix(body, dust, grit)
    place(out, cloth, 0.03, 0.5)
    return out


@sound("hurt", variants=3, level=-15)
def hurt(r, i):
    dur = 0.45
    t = time(dur)
    impact = mix(thump(0.2, 150, 55, 0.05) * 1.2, click(r, 1800, 0.03, 0.8) * 0.9)
    # A sour, falling tone: the wizard's ward cracking.
    f0 = r.uniform(480, 560)
    tone = additive(glide_exp(dur, f0, f0 * 0.48), dur, saw_amps(10, 1.4), r=r)
    tone += additive(glide_exp(dur, f0 * 1.06, f0 * 0.5), dur, saw_amps(10, 1.4), r=r)
    tone = lp(tone, 2400) * env_points(dur, [(0, 0), (0.01, 1), (0.12, 0.6), (dur, 0)], curve=1.5) * 0.35
    tone *= 1 - 0.4 * (0.5 + 0.5 * np.sin(2 * np.pi * 26 * t))
    crack = glass(r.uniform(1900, 2400), 0.3, r, tau=0.08) * 0.25
    return mix(impact, tone, crack)


@sound("death", level=-14)
def death(r, i):
    dur = 3.2
    t = time(dur)
    hit = mix(thump(0.4, 120, 32, 0.12) * 1.3, click(r, 1500, 0.04) * 0.8)
    # A cluster of tones collapsing downward as the light goes out of him.
    chord = np.zeros_like(t)
    for f in (293.7, 311.1, 440.0, 466.2):
        chord += additive(glide_exp(dur, f, f * 0.25), dur, saw_amps(12, 1.2), r=r)
    chord = tv_lowpass(chord, glide_exp(dur, 4000, 200)) * env_points(dur, [(0, 0), (0.05, 1), (1.2, 0.6), (dur, 0)]) * 0.25
    sparkles = np.zeros_like(t)
    for k in range(18):
        at = r.uniform(0.05, 1.8)
        f = r.uniform(1500, 4000) * (1 - at / 3)
        place(sparkles, bell(f, 0.6, r, tau=0.2) * 0.12, at)
    out = mix(hit, chord, sparkles)
    return reverb(out, 2.6, 0.5, r)


@sound("heartbeat", level=-19, loop=0.02)
def heartbeat(r, i):
    period = 60 / 72
    beats = 4
    dur = period * beats + 0.02
    out = silence(dur)
    for b in range(beats):
        at = b * period
        lub = mix(thump(0.18, 75, 42, 0.05), lp(noise(0.08, r), 200) * env_exp(0.08, 0.015) * 0.4)
        dub = thump(0.15, 64, 38, 0.04) * 0.75
        place(out, lub, at)
        place(out, dub, at + 0.26)
    return lp(out, 400)


# ============================================================================ wands

@sound("wand_swap_fire", level=-19)
def wand_swap_fire(r, i):
    dur = 0.6
    t = time(dur)
    flick = mix(click(r, 3500, 0.01) * 0.8, click(r, 2200, 0.008) * 0.5)
    puff = tv_lowpass(brown(dur, r), glide_exp(dur, 400, 3500) * np.exp(-t / 0.3) + 300) * env_points(dur, [(0, 0), (0.06, 1), (dur, 0)], curve=1.5)
    pops = crackle(dur, 40, r, envelope=np.exp(-t / 0.25)) * 0.5
    return mix(flick, puff, pops)


@sound("wand_swap_ice", level=-20)
def wand_swap_ice(r, i):
    dur = 0.9
    out = silence(dur)
    place(out, glass(1318.5, 0.8, r, tau=0.3), 0.0, 0.6)
    place(out, glass(1975.5, 0.8, r, tau=0.3), 0.07, 0.5)
    place(out, glass(2637.0, 0.6, r, tau=0.2), 0.13, 0.3)
    frost = crackle(0.3, 500, r, 3000, 9000, ring=0.001, envelope=np.exp(-time(0.3) / 0.08)) * 0.4
    place(out, frost, 0.0)
    return reverb(out, 1.0, 0.3, r)


@sound("wand_swap_lightning", level=-19)
def wand_swap_lightning(r, i):
    dur = 0.45
    t = time(dur)
    up = sine(glide_exp(dur, 300, 2400), dur) * env_points(dur, [(0, 0), (0.15, 1), (0.22, 0)]) * 0.25
    out = mix(zap(r, 0.12), up)
    place(out, zap(r, 0.08, bright=4500) * 0.6, 0.16)
    return out


@sound("wand_swap_wind", level=-20)
def wand_swap_wind(r, i):
    dur = 0.55
    swirl = whoosh(dur, 500, 2400, r, width=0.5, peak_at=0.45) + whoosh(dur, 2200, 700, r, width=0.5, peak_at=0.6) * 0.6
    t = time(dur)
    flute = sine(glide_exp(dur, 880, 1320), dur) * env_points(dur, [(0, 0), (0.2, 1), (dur, 0)]) * 0.08
    return mix(swirl, flute * (1 + 0.2 * np.sin(2 * np.pi * 6 * t)))


# Streams: start (the moment it bursts out), loop (while held) and end (as it is let go).

@sound("wand_fire_start", level=-14)
def wand_fire_start(r, i):
    dur = 0.55
    t = time(dur)
    whoomp = tv_lowpass(brown(dur, r), glide_exp(dur, 250, 4500) * np.exp(-t / 0.4) + 400) * env_exp(dur, 0.18, attack=0.015)
    low = thump(0.25, 90, 40, 0.08)
    pops = crackle(dur, 80, r, envelope=np.exp(-t / 0.2)) * 0.6
    return mix(whoomp * 1.3, low * 0.8, pops)


@sound("wand_fire_loop", level=-17, loop=0.25)
def wand_fire_loop(r, i):
    dur = 3.25
    bed = fire_bed(dur, r, roar=1.0, hiss=0.7, pops=0.9, pop_rate=45, rumble=0.55)
    # The jet itself: a turbulent, throaty "fffff" riding over the roar.
    jet = bp(pink(dur, r), 900, 0.6) * (0.7 + 0.3 * smooth_random(dur, 14, r)) * 0.8
    return bed + jet


@sound("wand_fire_end", level=-20)
def wand_fire_end(r, i):
    dur = 0.6
    t = time(dur)
    puff = tv_lowpass(brown(dur, r), glide_exp(dur, 2500, 150)) * env_exp(dur, 0.15, attack=0.005)
    pops = crackle(dur, 30, r, envelope=np.exp(-t / 0.2)) * 0.5
    return puff + pops


@sound("wand_ice_start", level=-16)
def wand_ice_start(r, i):
    dur = 0.6
    t = time(dur)
    burst = hp(noise(dur, r), 2500) * env_exp(dur, 0.06) * 0.8
    crack = click(r, 2500, 0.02, 0.6) * 1.2
    out = mix(burst, crack)
    for k in range(6):
        place(out, glass(r.uniform(1800, 4200), 0.4, r, tau=0.12) * 0.35, r.uniform(0, 0.08))
    cold = lp(noise(dur, r), 600) * env_exp(dur, 0.12) * 0.5
    return out + cold


@sound("wand_ice_loop", level=-19, loop=0.2)
def wand_ice_loop(r, i):
    # 14 shards a second leave the wand (WandStream.IceRate): each gets a crisp "tsk" and a glint.
    dur = 3.0 + 0.2
    t = time(dur)
    out = hp(pink(dur, r), 3200) * (0.25 + 0.1 * smooth_random(dur, 8, r))
    period = 1 / 14
    k = 0
    while k * period < dur:
        at = k * period + r.uniform(-0.006, 0.006)
        if at >= 0:
            tsk = bp(noise(0.04, r), r.uniform(4500, 7000), 1.2) * env_exp(0.04, 0.008)
            place(out, tsk, at, 0.6)
            if r.uniform() < 0.6:
                place(out, glass(r.uniform(2400, 5200), 0.25, r, tau=r.uniform(0.04, 0.09)), at + 0.004, r.uniform(0.12, 0.3))
        k += 1
    breath = lp(noise(dur, r), 700) * 0.25
    return out + breath


@sound("wand_ice_end", level=-22)
def wand_ice_end(r, i):
    dur = 0.6
    out = silence(dur)
    for k in range(4):
        place(out, glass(r.uniform(2200, 4500), 0.4, r, tau=0.1), k * 0.05 + r.uniform(0, 0.02), 0.5 - k * 0.1)
    return out


@sound("wand_lightning_start", level=-13)
def wand_lightning_start(r, i):
    dur = 0.7
    t = time(dur)
    crack = mix(click(r, 3000, 0.02, 0.5) * 2.0, hp(noise(dur, r), 1200) * env_exp(dur, 0.03) * 1.2)
    body = thump(0.3, 160, 50, 0.07) * 0.9
    tail = lp(brown(dur, r), 1500) * env_exp(dur, 0.2) * 0.6
    out = mix(crack, body, tail)
    place(out, zap(r, 0.2), 0.01, 0.9)
    return out


@sound("wand_lightning_loop", level=-17, loop=0.15)
def wand_lightning_loop(r, i):
    dur = 2.0 + 0.15
    t = time(dur)
    # The arc: a harsh, jittering buzz...
    f = 118 * (1 + 0.04 * smooth_random(dur, 30, r, -1, 1))
    hum = additive(f, dur, saw_amps(40, 0.7), r=r)
    hum = bp(hum, 1800, 0.5) * (0.5 + 0.5 * smooth_random(dur, 25, r)) * 0.5
    # ...a hiss of ionised air, a seethe of crackles...
    hiss = hp(noise(dur, r), 3500) * (0.25 + 0.2 * smooth_random(dur, 40, r))
    crk = crackle(dur, 400, r, 2000, 9000, ring=0.0008)
    # ...and a snap for every bolt (14 a second, WandStream.BoltInterval).
    out = hum + hiss + crk * 0.5
    k = 0
    while k / 14 < dur:
        place(out, zap(r, 0.06, bright=r.uniform(2500, 4500)), k / 14 + r.uniform(0, 0.01), r.uniform(0.35, 0.65))
        k += 1
    return out


@sound("wand_lightning_end", level=-20)
def wand_lightning_end(r, i):
    dur = 0.4
    t = time(dur)
    fizz = crackle(dur, 400, r, envelope=np.exp(-t / 0.1)) + hp(noise(dur, r), 4000) * np.exp(-t / 0.05) * 0.4
    pop = click(r, 1500, 0.02) * 0.8
    down = sine(glide_exp(0.2, 900, 120), 0.2) * env_exp(0.2, 0.06) * 0.25
    return mix(fizz, pop, down)


@sound("wand_wind_start", level=-15)
def wand_wind_start(r, i):
    dur = 0.6
    gust = whoosh(dur, 300, 2600, r, width=0.9, peak_at=0.18) * 1.2
    push = thump(0.2, 80, 40, 0.06) * 0.6
    return mix(gust, push)


@sound("wand_wind_loop", level=-18, loop=0.4)
def wand_wind_loop(r, i):
    dur = 3.4
    rush = wind_bed(dur, r, lo=500, hi=1800, speed=2.2, width=1.0)
    hiss = hp(pink(dur, r), 3000) * (0.4 + 0.3 * smooth_random(dur, 4, r)) * 0.5
    buffet = lp(brown(dur, r), 160) * (0.5 + 0.5 * smooth_random(dur, 5, r)) * 0.25
    return rush * 1.3 + hiss + buffet


@sound("wand_wind_end", level=-21)
def wand_wind_end(r, i):
    dur = 0.6
    return whoosh(dur, 1600, 300, r, width=0.8, peak_at=0.1)


@sound("wand_empty", variants=2, level=-22)
def wand_empty(r, i):
    dur = 0.3
    t = time(dur)
    clk = mix(click(r, 1800, 0.015), click(r, 900, 0.02) * 0.5)
    sput = crackle(dur, 60, r, 1000, 3000, envelope=np.exp(-t / 0.08)) * 0.6
    puff = lp(noise(dur, r), 1200) * env_exp(dur, 0.04) * 0.4
    return mix(clk, sput, puff)


@sound("wand_ready", level=-21)
def wand_ready(r, i):
    dur = 0.8
    out = silence(dur)
    place(out, bell(880.0, 0.6, r, tau=0.25), 0)
    place(out, bell(1318.5, 0.6, r, tau=0.3), 0.08)
    return reverb(out, 0.8, 0.3, r)


@sound("backfire_fire", level=-13)
def backfire_fire(r, i):
    dur = 1.2
    t = time(dur)
    blast = tv_lowpass(brown(dur, r), glide_exp(dur, 5000, 200)) * env_exp(dur, 0.25, attack=0.004) * 1.4
    low = thump(0.4, 100, 35, 0.12)
    pops = crackle(dur, 150, r, envelope=np.exp(-t / 0.3)) * 0.8
    return mix(blast, low, pops, hurt(r, i) * 0.4)


@sound("backfire_ice", level=-14)
def backfire_ice(r, i):
    dur = 1.0
    t = time(dur)
    crunch = crackle(0.5, 1800, r, 2000, 8000, ring=0.0012, envelope=np.clip(time(0.5) / 0.2, 0, 1) * np.exp(-time(0.5) / 0.3))
    clunk = mix(thump(0.2, 220, 90, 0.04), click(r, 1500, 0.02))
    out = silence(dur)
    place(out, crunch, 0)
    place(out, clunk, 0.2, 1.2)
    for k in range(8):
        place(out, glass(r.uniform(1500, 4500), 0.5, r, tau=0.15), 0.2 + r.uniform(0, 0.15), 0.3)
    return out


@sound("backfire_lightning", level=-12)
def backfire_lightning(r, i):
    dur = 1.1
    out = silence(dur)
    place(out, wand_lightning_start(r, i), 0)
    for k in range(10):
        place(out, zap(r, 0.1), r.uniform(0.02, 0.6), r.uniform(0.4, 0.9))
    t = time(dur)
    buzz = bp(additive(60, dur, saw_amps(40, 0.6), r=r), 900, 0.4) * env_points(dur, [(0, 0), (0.05, 1), (0.7, 0.5), (dur, 0)]) * 0.5
    return out + buzz


@sound("backfire_wind", level=-13)
def backfire_wind(r, i):
    dur = 0.9
    gust = whoosh(dur, 2400, 250, r, width=1.0, peak_at=0.12) * 1.4
    slam = thump(0.3, 90, 35, 0.08)
    return mix(gust, slam)


# ============================================================================ elements hitting things

@sound("fire_hit", variants=4, level=-24)
def fire_hit(r, i):
    return mix(sizzle(0.22, r, tau=0.05, center=r.uniform(3500, 6000)), lp(noise(0.1, r), 800) * env_exp(0.1, 0.02) * 0.5)


@sound("ice_hit", variants=5, level=-22)
def ice_hit(r, i):
    dur = 0.3
    tink = glass(r.uniform(2200, 4000), dur, r, tau=r.uniform(0.04, 0.08))
    clk = click(r, 5000, 0.008) * 0.8
    chip = crackle(0.08, 300, r, 4000, 9000, ring=0.0008, envelope=np.exp(-time(0.08) / 0.02)) * 0.5
    return mix(tink, clk, chip)


@sound("ice_shatter", variants=3, level=-15)
def ice_shatter(r, i):
    dur = 0.9
    t = time(dur)
    crash = hp(noise(dur, r), 2500) * env_exp(dur, 0.08) * 0.9
    crunch = mix(thump(0.15, 260, 120, 0.03) * 0.7, click(r, 1800, 0.03) * 0.8)
    out = mix(crash, crunch)
    for k in range(22):
        at = r.uniform(0, 1) ** 2 * 0.45
        place(out, glass(r.uniform(1800, 6500), 0.35, r, tau=r.uniform(0.03, 0.1)), at, r.uniform(0.15, 0.45) * (1 - at))
    return out


@sound("frost_form", variants=3, level=-22)
def frost_form(r, i):
    dur = 0.5
    t = time(dur)
    e = np.clip(t / 0.25, 0, 1) * np.exp(-np.maximum(t - 0.25, 0) / 0.08)
    return crackle(dur, 1400, r, 3000, 9000, ring=0.0009, envelope=e) + hp(noise(dur, r), 6000) * e * 0.15


@sound("zap_hit", variants=4, level=-18)
def zap_hit(r, i):
    out = zap(r, 0.16)
    out = mix(out, thump(0.1, 180, 80, 0.02) * 0.6)
    return out


@sound("zap_air", variants=3, level=-24)
def zap_air(r, i):
    return zap(r, 0.07, bright=r.uniform(3500, 5500)) * 0.8


# ============================================================================ characters' states

@sound("ignite", variants=2, level=-17)
def ignite(r, i):
    dur = 0.8
    t = time(dur)
    whoomp = tv_lowpass(brown(dur, r), glide_exp(dur, 200, 3000) * np.exp(-t / 0.5) + 300) * env_points(dur, [(0, 0), (0.08, 1), (dur, 0)], curve=1.4)
    pops = crackle(dur, 70, r, envelope=np.clip(t / 0.1, 0, 1) * np.exp(-t / 0.4)) * 0.6
    return whoomp * 1.2 + pops


@sound("burn_loop", level=-21, loop=0.3)
def burn_loop(r, i):
    dur = 3.3
    return fire_bed(dur, r, roar=0.7, hiss=0.4, pops=1.2, pop_rate=22, rumble=0.25)


@sound("freeze", variants=2, level=-17)
def freeze(r, i):
    dur = 1.0
    t = time(dur)
    grow = crackle(0.45, 1600, r, 2500, 8000, ring=0.001, envelope=np.clip(time(0.45) / 0.4, 0, 1))
    creak = bp(additive(glide_exp(0.45, 90, 160), 0.45, saw_amps(30, 0.8), r=r), 1500, 0.8) * np.clip(time(0.45) / 0.45, 0, 1) * 0.3
    lock = mix(thump(0.25, 240, 110, 0.05), click(r, 2200, 0.03) * 1.2)
    out = silence(dur)
    place(out, grow + creak, 0)
    place(out, lock, 0.42, 1.0)
    place(out, glass(1760, 0.6, r, tau=0.3), 0.43, 0.35)
    place(out, glass(2637, 0.6, r, tau=0.25), 0.46, 0.25)
    return out


@sound("thaw", variants=2, level=-20)
def thaw(r, i):
    dur = 0.9
    out = steam(dur, r, attack=0.02, tau=0.3)
    for k in range(4):
        bd = 0.04
        place(out, sine(glide_exp(bd, 700, 1500), bd) * env_exp(bd, 0.01), r.uniform(0.2, 0.7), 0.3)
    return out


@sound("shock", variants=2, level=-18)
def shock(r, i):
    dur = 0.4
    t = time(dur)
    buzz = bp(additive(140 * (1 + 0.1 * smooth_random(dur, 60, r, -1, 1)), dur, saw_amps(40, 0.7), r=r), 2200, 0.6)
    buzz *= env_points(dur, [(0, 0), (0.01, 1), (0.3, 0.6), (dur, 0)]) * 0.7
    return mix(zap(r, 0.1), buzz, crackle(dur, 350, r, envelope=np.exp(-t / 0.2)) * 0.5)


@sound("steam_puff", variants=3, level=-23)
def steam_puff(r, i):
    return steam(0.6, r, attack=0.01, tau=0.15)


# ============================================================================ skeletons

@sound("skel_step", variants=4, level=-27)
def skel_step(r, i):
    out = bone_clack(r, 0.8, 0.08, hollow=0.15)
    scrape = bp(noise(0.06, r), 1800, 1.0) * env_exp(0.06, 0.015) * 0.3
    return mix(out, scrape)


@sound("skel_throw", variants=3, level=-19)
def skel_throw(r, i):
    dur = 0.6
    t = time(dur)
    windup = rattle(0.12, 4, r, 1.0)
    # The bone whirring end over end as it leaves the hand.
    swish = whoosh(0.45, 500, 1400, r, width=0.6, peak_at=0.25)
    swish *= 0.55 + 0.45 * np.abs(np.sin(2 * np.pi * 7 * time(0.45)))
    out = silence(dur)
    place(out, windup, 0, 0.6)
    place(out, swish, 0.1, 1.0)
    return out


@sound("skel_hurt", variants=3, level=-18)
def skel_hurt(r, i):
    dur = 0.45
    knock = bone_clack(r, 0.55, 0.15, hollow=1.0) * 1.2
    hollow = resonator(noise(0.2, r) * env_exp(0.2, 0.02), r.uniform(330, 420), 12) * 0.5
    out = mix(knock, hollow, rattle(0.22, 9, r, 1.1, 1.5) * 0.7)
    return out


@sound("skel_death", variants=2, level=-15)
def skel_death(r, i):
    dur = 2.4
    out = silence(dur)
    # The collapse: bones cascading down, bouncing to rest.
    for k in range(46):
        at = (r.uniform(0, 1) ** 1.6) * 1.1
        place(out, bone_clack(r, r.uniform(0.6, 1.3), 0.08, hollow=0.4), at, r.uniform(0.3, 1.0) * (1.1 - at * 0.6))
    place(out, thump(0.25, 140, 60, 0.05), 0.08, 0.9)
    skull = bone_clack(r, 0.45, 0.2, hollow=1.2)
    for j, at in enumerate([0.55, 0.85, 1.05, 1.18, 1.27]):
        place(out, skull, at, 0.8 * 0.7 ** j)
    # A ghostly sigh leaving the bones.
    gd = 1.8
    t = time(gd)
    vib = 1 + 0.012 * np.sin(2 * np.pi * 5.5 * t)
    moan = additive(glide_exp(gd, 330, 150) * vib, gd, [1, 0.3, 0.12, 0.05], r=r)
    moan = lp(moan, 1200) * env_points(gd, [(0, 0), (0.3, 1), (gd, 0)], curve=1.5) * 0.22
    place(out, moan, 0.1)
    return reverb(out, 1.6, 0.35, r)


@sound("skel_alert", variants=2, level=-20)
def skel_alert(r, i):
    dur = 0.6
    out = silence(dur)
    rate = r.uniform(16, 21)
    k = 0
    while k / rate < 0.4:
        place(out, bone_clack(r, 0.9, 0.05, hollow=0.5), k / rate, 0.9 - k * 0.04)
        k += 1
    hiss = bp(noise(dur, r), 3000, 0.7) * env_points(dur, [(0, 0), (0.1, 1), (dur, 0)]) * 0.2
    return out + hiss


@sound("bone_hit", variants=3, level=-14)
def bone_hit(r, i):
    dur = 0.4
    crack = bone_clack(r, 0.7, 0.12, hollow=0.8) * 1.4
    body = thump(0.2, 140, 50, 0.05) * 1.2
    snap = click(r, 2600, 0.025, 0.7)
    return mix(crack, body, snap)


@sound("bone_clatter", variants=3, level=-21)
def bone_clatter(r, i):
    dur = 0.6
    out = silence(dur)
    at, gap, amp = 0.0, r.uniform(0.07, 0.11), 1.0
    for k in range(r.integers(3, 6)):
        place(out, bone_clack(r, r.uniform(0.8, 1.2)), at, amp)
        at += gap
        gap *= 0.65
        amp *= 0.6
    place(out, crackle(0.15, 300, r, 2000, 6000, envelope=np.exp(-time(0.15) / 0.04)) * 0.5, 0)
    return out


@sound("bone_deflect", variants=2, level=-19)
def bone_deflect(r, i):
    dur = 0.6
    out = whoosh(dur, 1800, 500, r, width=0.6, peak_at=0.2)
    place(out, bell(1568, 0.5, r, tau=0.15) * 0.4, 0.02)
    return out


# ============================================================================ items and the HUD

@sound("pickup", variants=3, level=-20)
def pickup(r, i):
    roots = [1046.5, 1174.7, 1318.5]
    f = roots[i % 3]
    out = silence(0.7)
    place(out, bell(f, 0.5, r, ratio=2.0, index=1.2, tau=0.15), 0)
    place(out, bell(f * 1.5, 0.5, r, ratio=2.0, index=1.2, tau=0.2), 0.06)
    for k in range(5):
        place(out, glass(r.uniform(3500, 6000), 0.2, r, tau=0.05) * 0.15, 0.04 + k * 0.03)
    return reverb(out, 0.7, 0.25, r)


@sound("pickup_rare", level=-18)
def pickup_rare(r, i):
    out = silence(1.2)
    for k, f in enumerate([659.3, 880.0, 1108.7, 1318.5]):
        place(out, bell(f, 0.9, r, ratio=2.0, index=1.5, tau=0.35), k * 0.07, 0.8)
    place(out, thump(0.3, 110, 60, 0.1) * 0.4, 0)
    return reverb(out, 1.4, 0.35, r)


@sound("key_get", level=-15)
def key_get(r, i):
    dur = 3.0
    out = silence(dur)
    # An arpeggio up a D major chord, then a shimmering chord that blooms and hangs.
    for k, f in enumerate([587.3, 740.0, 880.0, 1174.7, 1480.0]):
        place(out, bell(f, 1.6, r, ratio=2.0, index=1.6, tau=0.6), k * 0.09, 0.8)
    t = time(2.4)
    pad = np.zeros_like(t)
    for f in (293.7, 440.0, 587.3, 740.0):
        pad += additive(f * (1 + 0.003 * np.sin(2 * np.pi * 5 * t)), 2.4, saw_amps(12, 1.6), r=r)
    pad = lp(pad, 3000) * env_points(2.4, [(0, 0), (0.6, 1), (2.4, 0)], curve=1.3) * 0.12
    place(out, pad, 0.4)
    for k in range(24):
        place(out, glass(r.uniform(3000, 7000), 0.3, r, tau=0.08) * 0.12, 0.45 + r.uniform(0, 1.6))
    place(out, thump(0.5, 90, 45, 0.15) * 0.4, 0.45)
    return reverb(out, 2.2, 0.4, r)


@sound("weapon_get", level=-14)
def weapon_get(r, i):
    dur = 2.6
    out = silence(dur)
    rise = tv_bandpass(pink(0.7, r), glide_exp(0.7, 300, 5000), 0.6) * env_points(0.7, [(0, 0), (0.65, 1), (0.7, 0)], curve=2)
    place(out, rise, 0, 0.8)
    t = time(1.8)
    stab = np.zeros_like(t)
    for f in (146.8, 220.0, 293.7, 370.0, 440.0):
        stab += additive(f, 1.8, saw_amps(24, 1.0), r=r) + additive(f * 1.004, 1.8, saw_amps(24, 1.0), r=r)
    stab = tv_lowpass(stab, glide_exp(1.8, 6000, 600)) * env_exp(1.8, 0.6, attack=0.005) * 0.12
    place(out, stab, 0.68)
    place(out, thump(0.6, 80, 32, 0.2) * 1.0, 0.68)
    place(out, bell(1174.7, 1.4, r, tau=0.5) * 0.4, 0.7)
    place(out, bell(1760.0, 1.4, r, tau=0.5) * 0.3, 0.78)
    return reverb(out, 2.0, 0.4, r)


@sound("recipe_get", level=-18)
def recipe_get(r, i):
    dur = 1.4
    out = silence(dur)
    for at in (0.0, 0.12):
        flip = bp(noise(0.12, r), 2600, 0.6) * env_points(0.12, [(0, 0), (0.03, 1), (0.12, 0)], curve=1.5)
        flip *= 0.6 + 0.4 * smooth_random(0.12, 120, r)
        place(out, flip, at, 0.7)
    place(out, bell(1318.5, 1.0, r, ratio=2.0, index=1.0, tau=0.4) * 0.7, 0.25)
    place(out, bell(1975.5, 1.0, r, ratio=2.0, index=1.0, tau=0.4) * 0.5, 0.33)
    return reverb(out, 1.2, 0.3, r)


@sound("craft", level=-15)
def craft(r, i):
    dur = 2.0
    out = silence(dur)
    swirl = tv_bandpass(pink(0.9, r), glide_exp(0.9, 400, 4000), 0.5) * env_points(0.9, [(0, 0), (0.8, 1), (0.9, 0)], curve=1.8)
    swirl *= 0.7 + 0.3 * np.sin(2 * np.pi * np.cumsum(glide_exp(0.9, 4, 16)) / SR)
    place(out, swirl, 0, 0.7)
    poof = lp(noise(0.5, r), 2000) * env_exp(0.5, 0.08, attack=0.005)
    place(out, poof, 0.85, 0.9)
    place(out, thump(0.3, 160, 60, 0.06) * 0.8, 0.85)
    for k, f in enumerate([783.99, 987.77, 1174.66, 1567.98]):
        place(out, bell(f, 1.0, r, ratio=2.0, index=1.3, tau=0.4), 0.86 + k * 0.05, 0.6)
    for k in range(14):
        place(out, glass(r.uniform(3000, 7000), 0.25, r, tau=0.06) * 0.15, 0.9 + r.uniform(0, 0.7))
    return reverb(out, 1.6, 0.35, r)


@sound("item_drop", variants=2, level=-22)
def item_drop(r, i):
    dur = 0.4
    toss = whoosh(0.2, 800, 1500, r, width=0.6, peak_at=0.4) * 0.7
    out = silence(dur)
    place(out, toss, 0)
    place(out, bone_clack(r, 1.0) * 0.6, 0.18)
    return out


@sound("ui_click", variants=2, level=-22)
def ui_click(r, i):
    dur = 0.12
    tick = mix(click(r, 2800, 0.01) * 0.8, modal([1900, 3700], [0.01, 0.006], [1, 0.4], dur, r=r) * 0.5)
    blip = sine(1320 if i == 0 else 1480, dur) * env_exp(dur, 0.02) * 0.25
    return mix(tick, blip)


@sound("ui_hover", level=-30)
def ui_hover(r, i):
    dur = 0.08
    return mix(sine(2200, dur) * env_exp(dur, 0.012) * 0.6, click(r, 4500, 0.006) * 0.3)


@sound("ui_open", level=-22)
def ui_open(r, i):
    dur = 0.35
    out = whoosh(dur, 600, 2200, r, width=0.5, peak_at=0.6)
    place(out, bell(1568, 0.3, r, ratio=2.0, index=0.8, tau=0.1) * 0.3, 0.2)
    return out


@sound("ui_close", level=-23)
def ui_close(r, i):
    dur = 0.3
    out = whoosh(dur, 2000, 600, r, width=0.5, peak_at=0.35)
    place(out, click(r, 1500, 0.015) * 0.5, 0.25)
    return out


@sound("notify", level=-20)
def notify(r, i):
    out = silence(1.0)
    place(out, bell(783.99, 0.7, r, ratio=2.0, index=1.0, tau=0.3), 0)
    place(out, bell(1046.5, 0.8, r, ratio=2.0, index=1.0, tau=0.35), 0.1)
    return reverb(out, 1.0, 0.3, r)


@sound("menu_start", level=-14)
def menu_start(r, i):
    dur = 2.6
    out = silence(dur)
    rush = tv_bandpass(pink(1.0, r), glide_exp(1.0, 200, 6000), 0.6) * env_points(1.0, [(0, 0), (0.85, 1), (1.0, 0)], curve=2)
    place(out, rush, 0, 0.8)
    t = time(2.0)
    chord = np.zeros_like(t)
    for f in (146.8, 220.0, 293.7, 349.2, 440.0, 587.3):
        chord += additive(f * (1 + 0.002 * np.sin(2 * np.pi * 4.5 * t + f)), 2.0, saw_amps(16, 1.4), r=r)
    chord = tv_lowpass(chord, glide_exp(2.0, 400, 3500) * np.exp(-time(2.0) / 1.4) + 400) * env_exp(2.0, 0.9, attack=0.03) * 0.15
    place(out, chord, 0.95)
    place(out, thump(0.7, 70, 30, 0.25) * 0.9, 0.95)
    for k in range(20):
        place(out, bell(r.uniform(1500, 4000), 0.5, r, tau=0.2) * 0.1, 0.95 + r.uniform(0, 1.2))
    return reverb(out, 2.4, 0.45, r)


@sound("sting_death", level=-16)
def sting_death(r, i):
    dur = 4.0
    t = time(dur)
    out = silence(dur)
    chord = np.zeros(n_of(3.5))
    tt = time(3.5)
    for f in (146.8, 174.6, 220.0, 277.2):  # D minor with a sour C#
        chord += additive(f * (1 + 0.003 * np.sin(2 * np.pi * 4 * tt)), 3.5, saw_amps(14, 1.5), r=r)
    chord = lp(chord, 1400) * env_points(3.5, [(0, 0), (0.3, 1), (3.5, 0)], curve=1.4) * 0.15
    place(out, chord, 0.2)
    toll = bell(73.4, 3.5, r, ratio=1.4, index=3.0, tau=1.4, bright_tau=0.4)
    place(out, toll, 0, 0.6)
    return reverb(out, 3.0, 0.5, r)


# ============================================================================ reactions

@sound("steam_boom", variants=2, level=-11)
def steam_boom(r, i):
    dur = 2.8
    out = explosion(r, 1.4, 1.0)
    out = mix(out, silence(dur))
    hiss = steam(dur, r, attack=0.03, tau=0.9) * 0.9
    place(out, hiss, 0.02)
    return reverb(out, 1.8, 0.35, r)


@sound("steam_loop", level=-20, loop=0.4)
def steam_loop(r, i):
    dur = 3.4
    hiss = hp(pink(dur, r), 1500) * (0.6 + 0.4 * smooth_random(dur, 3, r))
    seethe = bp(noise(dur, r), 5500, 1.0) * (0.5 + 0.5 * smooth_random(dur, 7, r)) * 0.5
    out = hiss + seethe
    for k in range(int(dur * 9)):
        bd = r.uniform(0.02, 0.05)
        f0 = r.uniform(300, 700)
        place(out, sine(glide_exp(bd, f0, f0 * 2.2), bd) * env_exp(bd, bd / 3), r.uniform(0, dur - bd), 0.15)
    return out


@sound("plasma_boom", variants=2, level=-10)
def plasma_boom(r, i):
    dur = 2.2
    out = mix(explosion(r, 1.2, 1.1), silence(dur))
    t = time(1.2)
    wobble = fm(glide_exp(1.2, 600, 60), 1.2, 1.5, 4.0, index_env=np.exp(-t / 0.4)) * env_exp(1.2, 0.35, attack=0.003) * 0.35
    place(out, wobble, 0)
    for k in range(14):
        place(out, zap(r, 0.12), r.uniform(0, 0.7), r.uniform(0.3, 0.8))
    return reverb(out, 1.6, 0.35, r)


@sound("plasma_loop", level=-17, loop=0.2)
def plasma_loop(r, i):
    dur = 2.2
    t = time(dur)
    hum = fm(72 * (1 + 0.02 * smooth_random(dur, 6, r, -1, 1)), dur, 2.01, 1.6) * 0.35
    whine = sine(1450 * (1 + 0.03 * np.sin(2 * np.pi * 7 * t)), dur) * 0.04
    roar = fire_bed(dur, r, roar=0.6, hiss=0.5, pops=0.4, pop_rate=30, rumble=0.3) * 0.7
    crk = crackle(dur, 250, r, 2000, 9000, ring=0.0009) * 0.6
    out = hum + whine + roar + crk
    for k in range(int(dur * 5)):
        place(out, zap(r, 0.08), r.uniform(0, dur - 0.08), r.uniform(0.2, 0.5))
    return out


@sound("vortex_start", level=-12)
def vortex_start(r, i):
    dur = 1.6
    t = time(dur)
    roar = tv_lowpass(brown(dur, r), glide_exp(dur, 300, 3500) * env_points(dur, [(0, 0.2), (0.4, 1), (dur, 0.3)]) + 200)
    roar *= env_points(dur, [(0, 0), (0.3, 1), (dur, 0)], curve=1.3) * 1.3
    spin = whoosh(dur, 300, 1800, r, width=0.7, peak_at=0.4) * (0.6 + 0.4 * np.sin(2 * np.pi * np.cumsum(glide_exp(dur, 2, 6)) / SR))
    return mix(roar, spin, thump(0.5, 70, 35, 0.15) * 0.8)


@sound("vortex_loop", level=-15, loop=0.25)
def vortex_loop(r, i):
    dur = 3.25
    t = time(dur)
    bed = fire_bed(dur, r, roar=1.2, hiss=0.8, pops=0.8, pop_rate=40, rumble=0.7)
    # The fire whirling round: a band sweeping up and down a few times a second.
    whirl_rate = 2.0
    centers = 900 + 700 * np.sin(2 * np.pi * whirl_rate * t)
    whirl = tv_bandpass(pink(dur, r), centers, 0.5) * 1.0
    return bed + whirl


@sound("blizzard_start", level=-14)
def blizzard_start(r, i):
    dur = 1.4
    out = whoosh(dur, 300, 2200, r, width=0.9, peak_at=0.35) * 1.2
    place(out, glass(2637, 0.8, r, tau=0.3) * 0.4, 0.2)
    place(out, glass(3520, 0.8, r, tau=0.25) * 0.3, 0.26)
    return out


@sound("blizzard_loop", level=-17, loop=0.4)
def blizzard_loop(r, i):
    dur = 3.6
    howl = wind_bed(dur, r, lo=350, hi=1300, speed=1.0, whistle=0.6, width=0.5)
    hiss = hp(pink(dur, r), 2800) * (0.5 + 0.5 * smooth_random(dur, 2, r)) * 0.6
    hail = crackle(dur, 120, r, 3000, 8000, ring=0.0012) * 0.5
    out = howl + hiss + hail
    for k in range(int(dur * 3)):
        place(out, glass(r.uniform(2500, 6000), 0.3, r, tau=0.06) * 0.08, r.uniform(0, dur - 0.3))
    return out


@sound("frost_plate", variants=3, level=-16)
def frost_plate(r, i):
    dur = 1.2
    out = silence(dur)
    place(out, frost_form(r, i) * 0.9, 0)
    place(out, zap(r, 0.15), 0.12, 0.8)
    base = [1318.5, 1568.0, 1760.0][i % 3]
    for k, m in enumerate([1.0, 1.5, 2.0, 3.0]):
        place(out, glass(base * m, 0.8, r, tau=0.3), 0.14 + k * 0.04, 0.4 / (k + 1) ** 0.5)
    return reverb(out, 1.2, 0.3, r)


@sound("electric_hum_loop", level=-21, loop=0.2)
def electric_hum_loop(r, i):
    dur = 2.4
    t = time(dur)
    hum = additive(100 * (1 + 0.005 * smooth_random(dur, 4, r, -1, 1)), dur, saw_amps(30, 1.1), r=r)
    hum = lp(hum, 1500) * (0.8 + 0.2 * smooth_random(dur, 10, r)) * 0.6
    crk = crackle(dur, 40, r, 2500, 8000, ring=0.0009)
    out = hum + crk * 0.8
    for k in range(int(dur * 2)):
        place(out, zap_air(r, k) * 0.5, r.uniform(0, dur - 0.1))
    return out


@sound("storm_start", level=-10)
def storm_start(r, i):
    return thunder(r, 0) * 1.0


@sound("thunder", variants=3, level=-11)
def thunder(r, i):
    dur = 3.2
    t = time(dur)
    crack = mix(click(r, 2500, 0.03, 0.4) * 2.0, hp(noise(0.3, r), 1500) * env_exp(0.3, 0.04) * 1.2)
    out = mix(crack, silence(dur))
    place(out, zap(r, 0.25) * 0.9, 0.0)
    # The roll: brown noise in waves, each softer and darker than the last.
    roll = np.zeros_like(t)
    for k in range(r.integers(4, 7)):
        at = r.uniform(0.05, 1.4)
        ln = r.uniform(0.6, 1.5)
        seg = lp(brown(ln, r), r.uniform(250, 600)) * env_points(ln, [(0, 0), (ln * 0.2, 1), (ln, 0)], curve=1.5)
        place(roll, seg, at, r.uniform(0.5, 1.0) * (1 - at / 2))
    out += roll * 1.6
    out += thump(dur, 60, 28, 0.6) * 0.6
    return reverb(out, 2.5, 0.35, r)


@sound("storm_loop", level=-15, loop=0.4)
def storm_loop(r, i):
    dur = 3.6
    t = time(dur)
    roar = lp(brown(dur, r), 500) * (0.7 + 0.3 * smooth_random(dur, 2.5, r)) * 1.2
    spin = wind_bed(dur, r, lo=250, hi=1200, speed=3.0, width=0.6) * 0.9
    crk = crackle(dur, 60, r, 2000, 7000, ring=0.001) * 0.5
    out = roar + spin + crk
    for k in range(int(dur * 2.5)):
        place(out, zap(r, 0.1) * 0.4, r.uniform(0, dur - 0.1))
    return out


# ============================================================================ the world

@sound("prop_douse", level=-18)
def prop_douse(r, i):
    dur = 1.2
    return mix(sizzle(dur, r, tau=0.3, center=4500) * 0.8, steam(dur, r, attack=0.02, tau=0.4))


@sound("prop_burnaway", level=-15)
def prop_burnaway(r, i):
    dur = 1.6
    t = time(dur)
    out = silence(dur)
    for k in range(6):
        place(out, mix(click(r, r.uniform(500, 1500), 0.04, 0.6) * 1.2, thump(0.12, 160, 70, 0.03) * 0.5), r.uniform(0, 0.5), r.uniform(0.4, 0.9))
    collapse = lp(brown(dur, r), 1200) * env_exp(dur, 0.25, attack=0.02)
    out += collapse * 0.9
    out += crackle(dur, 120, r, envelope=np.exp(-t / 0.5)) * 0.7
    return out


@sound("pendulum_whoosh", variants=3, level=-17)
def pendulum_whoosh(r, i):
    dur = 0.7
    swoosh = whoosh(dur, 250, 900, r, width=0.6, peak_at=0.5) * 1.3
    hum = sine(glide_exp(dur, 90, 130), dur) * env_points(dur, [(0, 0), (0.35, 1), (dur, 0)], curve=1.8) * 0.25
    chain = crackle(dur, 20, r, 2500, 5000, ring=0.003, envelope=env_points(dur, [(0, 0.2), (0.35, 1), (dur, 0.2)])) * 0.3
    return mix(swoosh, hum, chain)


@sound("pendulum_hit", variants=2, level=-12)
def pendulum_hit(r, i):
    dur = 1.1
    base = r.uniform(420, 520)
    ring = modal([base, base * 2.32, base * 3.98, base * 5.81, base * 7.3], [0.5, 0.35, 0.22, 0.12, 0.08],
                 [1.0, 0.7, 0.5, 0.35, 0.2], dur, r=r) * 0.45
    slash = hp(noise(0.15, r), 2000) * env_exp(0.15, 0.03) * 1.0
    body = thump(0.25, 150, 60, 0.05)
    return mix(ring, slash, body)


@sound("lift_charge", level=-18)
def lift_charge(r, i):
    dur = 1.0
    t = time(dur)
    rise = additive(glide_exp(dur, 80, 220), dur, saw_amps(30, 1.0), r=r)
    rise = tv_lowpass(rise, glide_exp(dur, 300, 3000)) * env_points(dur, [(0, 0), (0.6, 1), (dur, 0)]) * 0.4
    out = mix(rise, zap(r, 0.1) * 0.6)
    for k, f in enumerate([523.3, 659.3, 784.0]):
        place(out, bell(f, 0.6, r, ratio=2.0, index=1.2, tau=0.25) * 0.35, 0.25 + k * 0.12)
    return out


@sound("lift_loop", level=-21, loop=0.3)
def lift_loop(r, i):
    dur = 2.3
    grind = lp(brown(dur, r), 600) * (0.6 + 0.4 * smooth_random(dur, 12, r))
    scrape = bp(noise(dur, r), 1400, 1.2) * (0.3 + 0.7 * smooth_random(dur, 18, r) ** 3) * 0.6
    grit = crackle(dur, 90, r, 1200, 4000, ring=0.002) * 0.5
    return grind + scrape + grit


@sound("lift_stop", level=-16)
def lift_stop(r, i):
    dur = 0.6
    return mix(thump(0.4, 110, 40, 0.09) * 1.3, lp(noise(dur, r), 1200) * env_exp(dur, 0.06), crackle(0.3, 200, r, 1500, 5000, envelope=np.exp(-time(0.3) / 0.05)) * 0.5)


# ============================================================================ output

def main(filters):
    OUT.mkdir(parents=True, exist_ok=True)
    made = 0
    for name, variants, level, loop, fn in REGISTRY:
        if filters and not any(f in name for f in filters):
            continue
        for v in range(variants):
            file_name = name if variants == 1 else f"{name}_{v + 1}"
            r = rng_for(file_name)
            x = np.asarray(fn(r, v), dtype=float)
            if loop:
                x = make_loop(x, loop)
                x = finish(x, level, trim=False)
            else:
                x = finish(x, level)
            write_wav(OUT / f"{file_name}.wav", x)
            made += 1
            print(f"  {file_name:28s} {len(x) / SR:5.2f}s")
    print(f"{made} sounds written to {OUT}")


if __name__ == "__main__":
    main(sys.argv[1:])
