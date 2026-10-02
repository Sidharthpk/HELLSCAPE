"""A tiny offline synth for HellScape's sound effects (numpy only).

Everything works on float32 mono arrays at SR. Filters are FFT based: static ones multiply the spectrum,
time-varying ones (formant glides, sweeps) run on overlapping frames (STFT overlap-add).
"""
import wave
import numpy as np

SR = 44100
rng = np.random.default_rng(1304)


def seed(n):
    global rng
    rng = np.random.default_rng(n)


def t_axis(dur):
    return np.arange(int(dur * SR)) / SR


def silence(dur):
    return np.zeros(int(dur * SR), np.float32)


# ------------------------------------------------------------------ sources

def white(dur):
    return rng.standard_normal(int(dur * SR)).astype(np.float32)


def pink(dur):
    return shape(white(dur), lambda f: 1.0 / np.sqrt(np.maximum(f, 20.0)))


def brown(dur):
    return shape(white(dur), lambda f: 1.0 / np.maximum(f, 15.0))


def sine(f, dur, phase=0.0):
    """f: number or per-sample array (glides)."""
    n = int(dur * SR)
    f = np.broadcast_to(np.asarray(f, np.float64), (n,)) if np.ndim(f) else np.full(n, float(f))
    ph = 2 * np.pi * np.cumsum(f) / SR + phase
    return np.sin(ph).astype(np.float32)


def saw(f, dur, harmonics_cap=None):
    """band-limited-ish saw: naive saw, then a gentle low-pass at 12 kHz to take the edge off aliasing."""
    n = int(dur * SR)
    f = np.broadcast_to(np.asarray(f, np.float64), (n,)) if np.ndim(f) else np.full(n, float(f))
    ph = np.cumsum(f) / SR + rng.random()
    x = (2.0 * (ph % 1.0) - 1.0).astype(np.float32)
    return lowpass(x, 12000)


def square(f, dur):
    return np.sign(sine(f, dur)).astype(np.float32)


def glide(a, b, dur, curve=1.0):
    """per-sample value from a to b (curve > 1 lingers at a, < 1 rushes)."""
    k = np.linspace(0, 1, int(dur * SR)) ** curve
    return (a + (b - a) * k).astype(np.float32)


def lfo(rate, dur, depth=1.0, phase=0.0):
    return (depth * np.sin(2 * np.pi * rate * t_axis(dur) + phase)).astype(np.float32)


def smooth_random(dur, rate, lo=-1.0, hi=1.0):
    """slowly wandering random value (control signal)."""
    n = int(dur * SR)
    pts = max(2, int(dur * rate) + 2)
    knots = rng.uniform(lo, hi, pts)
    return np.interp(np.linspace(0, pts - 1, n), np.arange(pts), knots).astype(np.float32)


# ------------------------------------------------------------------ envelopes

def env_adsr(dur, a=0.01, d=0.1, s=0.7, r=0.2):
    n = int(dur * SR)
    t = np.arange(n) / SR
    e = np.empty(n, np.float32)
    rs = dur - r
    e[:] = s
    m = t < a
    e[m] = t[m] / max(a, 1e-6)
    m = (t >= a) & (t < a + d)
    e[m] = 1 - (1 - s) * (t[m] - a) / max(d, 1e-6)
    m = t >= rs
    e[m] = s * np.clip((dur - t[m]) / max(r, 1e-6), 0, 1)
    return e


def env_decay(dur, tau, attack=0.002):
    t = t_axis(dur)
    e = np.exp(-t / tau)
    if attack > 0:
        e *= np.clip(t / attack, 0, 1)
    return e.astype(np.float32)


def fade(x, fin=0.005, fout=0.02):
    x = x.copy()
    a, b = int(fin * SR), int(fout * SR)
    if a > 0:
        x[:a] *= np.linspace(0, 1, a)
    if b > 0:
        x[-b:] *= np.linspace(1, 0, b)
    return x


# ------------------------------------------------------------------ filters

def _freqs(n):
    return np.fft.rfftfreq(n, 1 / SR)


def shape(x, resp):
    """static filter: resp(freqs) -> gain."""
    n = len(x)
    X = np.fft.rfft(x)
    X *= resp(_freqs(n))
    return np.fft.irfft(X, n).astype(np.float32)


def lowpass(x, fc, order=2):
    return shape(x, lambda f: 1 / np.sqrt(1 + (f / fc) ** (2 * order)))


def highpass(x, fc, order=2):
    return shape(x, lambda f: 1 / np.sqrt(1 + (fc / np.maximum(f, 1e-3)) ** (2 * order)))


def bandpass(x, fc, q=2.0):
    return shape(x, lambda f: peak(f, fc, fc / q))


def peak(f, fc, bw):
    return np.exp(-0.5 * ((f - fc) / (bw / 2.0)) ** 2)


def eq(x, bands):
    """bands: [(fc, bw, gain_db)] added on top of a flat response."""
    def r(f):
        g = np.ones_like(f)
        for fc, bw, db in bands:
            g *= 10 ** (db / 20 * peak(f, fc, bw))
        return g
    return shape(x, r)


def stft_filter(x, resp, frame=2048, hop=512):
    """time-varying filter: resp(freqs, t_seconds) -> gain for that frame."""
    n = len(x)
    win = np.hanning(frame).astype(np.float32)
    out = np.zeros(n + frame, np.float32)
    norm = np.zeros(n + frame, np.float32)
    xp = np.concatenate([x, np.zeros(frame, np.float32)])
    f = _freqs(frame)
    for start in range(0, n, hop):
        seg = xp[start:start + frame] * win
        S = np.fft.rfft(seg) * resp(f, (start + frame / 2) / SR)
        out[start:start + frame] += np.fft.irfft(S, frame).astype(np.float32) * win
        norm[start:start + frame] += win ** 2
    norm[norm < 1e-6] = 1
    return (out / norm)[:n]


# formant tables (Hz, bandwidth): an adult male voice; scale up for female/child/shrieks
VOWELS = {
    'a': [(800, 90), (1150, 110), (2900, 160), (3900, 200)],
    'o': [(450, 80), (800, 90), (2830, 150), (3800, 200)],
    'u': [(325, 70), (700, 90), (2530, 150), (3500, 200)],
    'e': [(400, 70), (1600, 100), (2700, 150), (3300, 200)],
    'i': [(290, 60), (1870, 100), (2800, 150), (3300, 200)],
    'aa': [(700, 110), (1220, 120), (2600, 170), (3500, 220)],
    'uh': [(600, 100), (1000, 110), (2400, 160), (3300, 200)],
}


def formants(x, vowel_track, scale=1.0, floor=0.02, gains=(1.0, 0.7, 0.35, 0.2)):
    """vowel_track(t) -> vowel name, or list of (weight, name) to blend. Shapes a buzzy source into a voice."""
    def resp(f, t):
        v = vowel_track(t)
        blend = v if isinstance(v, list) else [(1.0, v)]
        g = np.full_like(f, floor)
        for w, name in blend:
            for (fc, bw), gain in zip(VOWELS[name], gains):
                g += w * gain * peak(f, fc * scale, bw * scale * 1.3)
        return g
    return stft_filter(x, resp)


# ------------------------------------------------------------------ effects

def reverb(x, decay=1.5, wet=0.35, predelay=0.02, damp=3200, tail=True):
    """convolution with a decaying-noise impulse (a room, a stairwell, a silo)."""
    ir_len = decay * 1.4
    ir = white(ir_len) * env_decay(ir_len, decay / 6.9, attack=0.0)
    ir = lowpass(ir, damp)
    pre = np.zeros(int(predelay * SR), np.float32)
    ir = np.concatenate([pre, ir])
    ir /= np.sqrt(np.sum(ir ** 2)) + 1e-9
    n = len(x) + len(ir) if tail else len(x)
    size = 1 << int(np.ceil(np.log2(len(x) + len(ir))))
    y = np.fft.irfft(np.fft.rfft(x, size) * np.fft.rfft(ir, size), size)[:n].astype(np.float32)
    dry = np.concatenate([x, np.zeros(n - len(x), np.float32)])
    return (1 - wet) * dry + wet * y * 3.0


def drive(x, amount=2.0, tone=6000):
    """warm saturation: level the input first (so 'amount' means the same for every sound), then soften the
    extra harmonics it makes so it grits rather than fizzes."""
    m = np.max(np.abs(x)) + 1e-9
    y = np.tanh(x / m * amount) / np.tanh(amount) * m
    return lowpass(y.astype(np.float32), tone)


def bitcrush(x, bits=8, down=3):
    q = 2 ** (bits - 1)
    y = np.round(x * q) / q
    y = np.repeat(y[::down], down)[:len(x)]
    return y.astype(np.float32)


def mix(*parts):
    """parts: (array, offset_seconds, gain). Result is as long as the longest."""
    end = max(int(off * SR) + len(a) for a, off, g in parts)
    out = np.zeros(end, np.float32)
    for a, off, g in parts:
        s = int(off * SR)
        out[s:s + len(a)] += a * g
    return out


def pad_to(x, dur):
    n = int(dur * SR)
    return x[:n] if len(x) >= n else np.concatenate([x, np.zeros(n - len(x), np.float32)])


def loopable(x, xfade=0.5):
    """make the end run seamlessly into the start (equal-power crossfade)."""
    c = int(xfade * SR)
    head, body, tail = x[:c], x[c:-c], x[-c:]
    k = np.linspace(0, np.pi / 2, c)
    seam = tail * np.cos(k) + head * np.sin(k)
    return np.concatenate([body, seam]).astype(np.float32)


def normalize(x, peak_db=-1.0):
    m = np.max(np.abs(x)) + 1e-9
    return (x / m * 10 ** (peak_db / 20)).astype(np.float32)


def write(path, x, peak_db=-1.0):
    import os
    os.makedirs(os.path.dirname(path), exist_ok=True)
    x = normalize(highpass(x, 25, 1), peak_db)
    pcm = (np.clip(x, -1, 1) * 32767).astype(np.int16)
    with wave.open(path, 'wb') as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(pcm.tobytes())
