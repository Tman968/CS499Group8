"""A small synthesis toolkit shared by generate_sounds.py and generate_music.py.

Everything is plain numpy/scipy on float64 arrays in -1..1. Mono signals are 1-D; stereo signals
are (n, 2). Nothing here is random on its own: callers pass a numpy Generator, so the generators
stay deterministic (rerunning produces identical files).
"""
import wave
import zlib
from pathlib import Path

import numpy as np
from scipy import signal as sig

SR = 44100


def rng_for(name: str) -> np.random.Generator:
    """A generator seeded from a sound's name, so each sound is stable across reruns."""
    return np.random.default_rng(zlib.crc32(name.encode()))


def n_of(dur, sr=SR):
    return max(1, int(round(dur * sr)))


def time(dur, sr=SR):
    return np.arange(n_of(dur, sr)) / sr


def silence(dur, sr=SR):
    return np.zeros(n_of(dur, sr))


# ---------------------------------------------------------------------------- sources

def noise(dur, r, sr=SR):
    return r.uniform(-1.0, 1.0, n_of(dur, sr))


def colored(dur, r, slope, sr=SR):
    """Noise with a spectral slope in dB/octave: -3 is pink, -6 is brown."""
    n = n_of(dur, sr)
    spec = np.fft.rfft(r.standard_normal(n))
    f = np.fft.rfftfreq(n, 1.0 / sr)
    f[0] = f[1]
    spec *= (f / 1000.0) ** (slope / 6.0206)
    x = np.fft.irfft(spec, n)
    return x / (np.max(np.abs(x)) + 1e-12)


def pink(dur, r, sr=SR):
    return colored(dur, r, -3.0, sr)


def brown(dur, r, sr=SR):
    return colored(dur, r, -6.0, sr)


def phase_of(freq, n, sr=SR):
    """Phase (radians) of an oscillator whose frequency may be a curve."""
    f = np.broadcast_to(np.asarray(freq, dtype=float), (n,))
    return 2.0 * np.pi * np.cumsum(f) / sr


def sine(freq, dur, sr=SR, phase0=0.0):
    n = n_of(dur, sr)
    return np.sin(phase_of(freq, n, sr) + phase0)


def additive(freq, dur, amps, sr=SR, detune_cents=0.0, r=None):
    """Sum of harmonics with the given amplitudes, never above Nyquist (no aliasing)."""
    n = n_of(dur, sr)
    f = np.broadcast_to(np.asarray(freq, dtype=float), (n,)) * 2 ** (detune_cents / 1200.0)
    ph = 2.0 * np.pi * np.cumsum(f) / sr
    out = np.zeros(n)
    fmax = float(np.max(f))
    for k, a in enumerate(amps, start=1):
        if a == 0 or fmax * k >= sr * 0.45:
            continue
        p0 = r.uniform(0, 2 * np.pi) if r is not None else 0.0
        out += a * np.sin(k * ph + p0)
    return out


def saw_amps(count, rolloff=1.0):
    return [1.0 / (k ** rolloff) for k in range(1, count + 1)]


def square_amps(count):
    return [(1.0 / k if k % 2 else 0.0) for k in range(1, count + 1)]


def fm(freq, dur, ratio, index, sr=SR, index_env=None):
    """Two-operator FM: a carrier at freq, modulated by ratio*freq with the given index."""
    n = n_of(dur, sr)
    ph = phase_of(freq, n, sr)
    idx = index if index_env is None else index * index_env[:n]
    return np.sin(ph + idx * np.sin(ph * ratio))


def modal(freqs, decays, amps, dur, sr=SR, r=None, jitter=0.0):
    """Struck object: decaying sines at the given (usually inharmonic) frequencies."""
    t = time(dur, sr)
    out = np.zeros_like(t)
    for f, d, a in zip(freqs, decays, amps):
        if f >= sr * 0.45:
            continue
        if r is not None and jitter:
            f *= 1.0 + r.uniform(-jitter, jitter)
        p0 = r.uniform(0, 2 * np.pi) if r is not None else 0.0
        out += a * np.sin(2 * np.pi * f * t + p0) * np.exp(-t / d)
    return out


def pluck(freq, dur, r, sr=SR, decay=0.996, bright=0.6):
    """Karplus-Strong plucked string, vectorised one period at a time."""
    period = max(2, int(round(sr / freq - 0.5)))
    n = n_of(dur, sr)
    y = np.zeros(n + period + 1)
    burst = r.uniform(-1, 1, period)
    # A softer pluck has fewer highs in the initial burst.
    soft = np.convolve(burst, np.ones(3) / 3, mode="same")
    y[1:period + 1] = bright * burst + (1 - bright) * soft
    start = period + 1
    while start < len(y):
        end = min(start + period, len(y))
        idx = np.arange(start, end)
        y[start:end] = decay * (0.5 * y[idx - period] + 0.5 * y[idx - period - 1])
        start = end
    out = y[1:n + 1]
    return out - np.mean(out)


def impulses(dur, rate, r, sr=SR, amp_range=(0.3, 1.0), envelope=None):
    """Random clicks (a Poisson process) at rate per second, optionally shaped by envelope."""
    n = n_of(dur, sr)
    out = np.zeros(n)
    count = r.poisson(rate * dur)
    pos = r.integers(0, n, count)
    amps = r.uniform(*amp_range, count) * r.choice([-1, 1], count)
    if envelope is not None:
        amps *= envelope[pos]
    np.add.at(out, pos, amps)
    return out


# ---------------------------------------------------------------------------- envelopes

def env_exp(dur, tau, sr=SR, attack=0.001):
    t = time(dur, sr)
    a = np.clip(t / max(attack, 1e-6), 0, 1)
    return a * np.exp(-t / tau)


def env_adsr(dur, a, d, s, rel, sr=SR):
    n = n_of(dur, sr)
    t = np.arange(n) / sr
    e = np.empty(n)
    rel_start = max(dur - rel, a + d)
    e = np.where(t < a, t / max(a, 1e-6),
        np.where(t < a + d, 1 - (1 - s) * (t - a) / max(d, 1e-6),
        np.where(t < rel_start, s, s * np.clip(1 - (t - rel_start) / max(rel, 1e-6), 0, 1))))
    return e


def env_points(dur, points, sr=SR, curve=1.0):
    """Piecewise envelope through (time, value) points; curve > 1 bends segments exponentially."""
    t = time(dur, sr)
    ts, vs = zip(*points)
    e = np.interp(t, ts, vs)
    return np.sign(e) * np.abs(e) ** curve


def ramp(dur, a, b, sr=SR, shape=1.0):
    t = np.linspace(0, 1, n_of(dur, sr))
    return a + (b - a) * t ** shape


def glide_exp(dur, f0, f1, sr=SR):
    """A frequency curve sliding exponentially (in pitch) from f0 to f1."""
    t = np.linspace(0, 1, n_of(dur, sr))
    return f0 * (f1 / f0) ** t


def smooth_random(dur, rate, r, sr=SR, lo=0.0, hi=1.0):
    """A smooth random curve that wanders between lo and hi, changing about rate times a second."""
    n = n_of(dur, sr)
    k = max(4, int(dur * rate) + 4)
    pts = r.uniform(lo, hi, k)
    x = np.linspace(0, k - 3, n)
    i = np.floor(x).astype(int)
    f = x - i
    # Cosine interpolation between points.
    w = (1 - np.cos(np.pi * f)) / 2
    return pts[i] * (1 - w) + pts[i + 1] * w


# ---------------------------------------------------------------------------- filters

def _sos(kind, f, sr, order):
    nyq = sr / 2
    if kind == "bandpass":
        lo, hi = f
        lo = max(10.0, lo)
        hi = min(nyq * 0.95, hi)
        return sig.butter(order, [lo / nyq, hi / nyq], btype="bandpass", output="sos")
    f = min(nyq * 0.95, max(10.0, f))
    return sig.butter(order, f / nyq, btype=kind, output="sos")


def lp(x, f, order=2, sr=SR):
    return sig.sosfilt(_sos("lowpass", f, sr, order), x, axis=0)


def hp(x, f, order=2, sr=SR):
    return sig.sosfilt(_sos("highpass", f, sr, order), x, axis=0)


def bp(x, center, q=1.0, order=2, sr=SR):
    """Bandpass around center with roughly the given Q (center / bandwidth)."""
    bw = center / q
    return sig.sosfilt(_sos("bandpass", (center - bw / 2, center + bw / 2), sr, order), x, axis=0)


def resonator(x, freq, q, sr=SR):
    """A single sharp resonance (a ringing peak), for whistles and hollow bodies."""
    b, a = sig.iirpeak(min(freq, sr * 0.45), q, fs=sr)
    return sig.lfilter(b, a, x)


def _stft_filter(x, gain_of, sr=SR, nper=1024):
    hop = nper // 4
    f, t, z = sig.stft(x, fs=sr, nperseg=nper, noverlap=nper - hop, boundary="even", padded=True)
    frame_time = t
    g = gain_of(f[:, None], frame_time[None, :])
    _, y = sig.istft(z * g, fs=sr, nperseg=nper, noverlap=nper - hop, boundary=True)
    y = y[:len(x)]
    if len(y) < len(x):
        y = np.pad(y, (0, len(x) - len(y)))
    return y


def tv_bandpass(x, center, width_oct=0.7, sr=SR):
    """A bandpass whose center (Hz, a curve over the signal) moves: wind, whooshes, sweeps."""
    centers = np.asarray(center, dtype=float)
    if centers.ndim == 0:
        centers = np.full(len(x), float(centers))
    dur = len(x) / sr

    def gain(f, t):
        c = np.interp(t, np.linspace(0, dur, len(centers)), centers)
        lf = np.log2(np.maximum(f, 1.0) / c)
        return np.exp(-0.5 * (lf / width_oct) ** 2)

    return _stft_filter(x, gain, sr)


def tv_lowpass(x, cutoff, slope=2.0, sr=SR):
    """A lowpass whose cutoff (Hz, a curve) moves: muffled to bright and back."""
    cut = np.asarray(cutoff, dtype=float)
    if cut.ndim == 0:
        cut = np.full(len(x), float(cut))
    dur = len(x) / sr

    def gain(f, t):
        c = np.interp(t, np.linspace(0, dur, len(cut)), cut)
        return 1.0 / np.sqrt(1.0 + (f / c) ** (2 * slope))

    return _stft_filter(x, gain, sr)


def formant_shape(f, formants):
    """Gain at frequency f from a set of (center, bandwidth, gain) vowel formants."""
    g = np.zeros_like(np.asarray(f, dtype=float))
    for c, bw, a in formants:
        g += a / (1.0 + ((f - c) / (bw / 2)) ** 2)
    return g


# ---------------------------------------------------------------------------- effects

def soft_clip(x, drive=1.0):
    return np.tanh(x * drive) / np.tanh(drive)


def delay(x, seconds, feedback=0.35, mix=0.3, sr=SR, tail=0.0):
    d = n_of(seconds, sr)
    n = len(x) + n_of(tail, sr)
    y = np.zeros(n)
    y[:len(x)] = x
    out = y.copy()
    tap = y.copy()
    gain = mix
    for _ in range(12):
        tap = np.concatenate([np.zeros(d), tap[:-d]])
        out += tap * gain
        gain *= feedback
        if gain < 0.01:
            break
    return out


def reverb_ir(seconds, r, sr=SR, stereo=False, damp=0.6, predelay=0.012, density_ms=0.0):
    """A synthetic room: decaying noise that loses its highs as it dies, like a stone hall."""
    n = n_of(seconds, sr)
    t = np.arange(n) / sr
    chans = []
    for _ in range(2 if stereo else 1):
        w = r.standard_normal(n)
        bright = lp(w, 9000, sr=sr)
        dark = lp(w, 1800, sr=sr)
        mixk = np.clip(t / seconds, 0, 1) ** 0.6 * damp
        ir = (bright * (1 - mixk) + dark * mixk) * np.exp(-6.9 * t / seconds)
        pre = n_of(predelay, sr)
        ir = np.concatenate([np.zeros(pre), ir])[:n]
        ir /= np.sqrt(np.sum(ir ** 2)) + 1e-12
        chans.append(ir)
    return np.stack(chans, axis=1) if stereo else chans[0]


def reverb(x, seconds, wet, r, sr=SR, damp=0.6, predelay=0.012):
    """Adds a reverb tail (mono in, mono out), extending the sound by the tail length."""
    ir = reverb_ir(seconds, r, sr, False, damp, predelay)
    tail = np.zeros(len(x) + len(ir) - 1)
    tail[:len(x)] = x
    wet_sig = sig.fftconvolve(x, ir)
    return tail * (1 - wet * 0.3) + wet_sig * wet


def place(dst, src, at_seconds, gain=1.0, sr=SR):
    """Mixes src into dst starting at at_seconds (growing nothing; extra is cut)."""
    i = int(round(at_seconds * sr))
    if i >= len(dst):
        return dst
    j = min(len(dst), i + len(src))
    dst[i:j] += src[:j - i] * gain
    return dst


def mix(*parts):
    n = max(len(p) for p in parts)
    out = np.zeros((n,) + parts[0].shape[1:])
    for p in parts:
        out[:len(p)] += p
    return out


def pan(x, position):
    """Mono to stereo with equal-power pan: -1 left, 1 right. position may be a curve."""
    p = np.clip(np.asarray(position, dtype=float), -1, 1)
    a = (p + 1) * np.pi / 4
    return np.stack([x * np.cos(a), x * np.sin(a)], axis=1)


# ---------------------------------------------------------------------------- shaping and output

def fade(x, fin=0.002, fout=0.01, sr=SR):
    x = x.copy()
    a = min(len(x), n_of(fin, sr))
    b = min(len(x), n_of(fout, sr))
    ramp_in = np.linspace(0, 1, a)
    ramp_out = np.linspace(1, 0, b)
    if x.ndim == 2:
        ramp_in = ramp_in[:, None]
        ramp_out = ramp_out[:, None]
    x[:a] *= ramp_in
    x[len(x) - b:] *= ramp_out
    return x


def trim_tail(x, threshold_db=-60.0, sr=SR):
    """Cuts trailing near-silence (keeping a short fade), so files stay small."""
    mono = np.abs(x) if x.ndim == 1 else np.max(np.abs(x), axis=1)
    peak = np.max(mono) + 1e-12
    above = np.nonzero(mono > peak * 10 ** (threshold_db / 20))[0]
    if len(above) == 0:
        return x
    end = min(len(x), above[-1] + n_of(0.01, sr))
    return fade(x[:end], 0.0, 0.02, sr)


def loudness_db(x, sr=SR, window=0.1):
    """RMS (dBFS) of the loudest window: how loud the sound reads to the ear, roughly."""
    mono = x if x.ndim == 1 else x.mean(axis=1)
    w = max(1, n_of(window, sr))
    if len(mono) <= w:
        rms = np.sqrt(np.mean(mono ** 2))
    else:
        c = np.cumsum(np.concatenate([[0.0], mono ** 2]))
        rms = np.sqrt(np.max((c[w:] - c[:-w]) / w))
    return 20 * np.log10(rms + 1e-12)


def finish(x, level_db=-16.0, sr=SR, peak=0.94, trim=True):
    """Normalises the loudest part to level_db RMS, limits peaks softly, and trims the tail."""
    x = np.asarray(x, dtype=float)
    x = x - (np.mean(x, axis=0) if len(x) > sr else 0)
    gain = 10 ** ((level_db - loudness_db(x, sr)) / 20)
    x = x * gain
    # Soft knee: anything over 0.7 is bent smoothly toward the peak ceiling.
    knee = 0.7 * peak
    over = np.abs(x) > knee
    if np.any(over):
        s = np.sign(x[over])
        e = np.abs(x[over]) - knee
        x[over] = s * (knee + (peak - knee) * np.tanh(e / (peak - knee)))
    if not trim:
        return x  # a loop: fading its ends would put a dip at the seam
    x = trim_tail(x, sr=sr)
    return fade(x, 0.0005, 0.01, sr)


def make_loop(x, crossfade, sr=SR):
    """Turns a signal into a seamless loop: its last crossfade seconds are blended over its start."""
    xf = n_of(crossfade, sr)
    body = x[:len(x) - xf].copy()
    tail = x[len(x) - xf:]
    t = np.linspace(0, 1, xf)
    fin = np.sin(t * np.pi / 2)
    fout = np.cos(t * np.pi / 2)
    if x.ndim == 2:
        fin = fin[:, None]
        fout = fout[:, None]
    body[:xf] = body[:xf] * fin + tail * fout
    return body


def wrap_tail(x, length):
    """Folds everything past length back onto the start: what a loop sounds like once it repeats."""
    out = x[:length].copy()
    rest = x[length:]
    while len(rest):
        k = min(len(rest), length)
        out[:k] += rest[:k]
        rest = rest[k:]
    return out


def write_wav(path: Path, x, sr=SR):
    path.parent.mkdir(parents=True, exist_ok=True)
    x = np.asarray(x)
    chans = 1 if x.ndim == 1 else x.shape[1]
    pcm = np.clip(np.round(x * 32767), -32768, 32767).astype("<i2")
    with wave.open(str(path), "wb") as w:
        w.setnchannels(chans)
        w.setsampwidth(2)
        w.setframerate(sr)
        w.writeframes(pcm.tobytes())


# ---------------------------------------------------------------------------- music helpers

NOTE_INDEX = {"C": 0, "D": 2, "E": 4, "F": 5, "G": 7, "A": 9, "B": 11}


def note_freq(name: str) -> float:
    """'A4' -> 440, 'C#5', 'Bb3' ..."""
    letter = name[0]
    rest = name[1:]
    semis = NOTE_INDEX[letter]
    while rest and rest[0] in "#b":
        semis += 1 if rest[0] == "#" else -1
        rest = rest[1:]
    octave = int(rest)
    midi = 12 * (octave + 1) + semis
    return 440.0 * 2 ** ((midi - 69) / 12)
