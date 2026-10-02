"""Horror sound building blocks, each an original synthesis in the style of a genre staple."""
import numpy as np
from dsp import *
import dsp


# ------------------------------------------------------------------ impacts and bodies

def thump(freq=55, dur=0.35, drop=2.2, click=0.4):
    """a low body hit: a sine that falls in pitch, plus a short noise click on top."""
    f = glide(freq * drop, freq, dur, curve=0.25)
    body = sine(f, dur) * env_decay(dur, dur / 4)
    c = highpass(white(0.02), 800) * env_decay(0.02, 0.004) * click
    return mix((body, 0, 1.0), (c, 0, 1.0))


def boom(dur=2.5, freq=38):
    """sub-bass cinematic impact (trailer hit)."""
    sub = sine(glide(freq * 2.5, freq, dur, 0.2), dur) * env_decay(dur, dur / 3.5)
    rumble = lowpass(brown(dur), 180) * env_decay(dur, dur / 4) * 1.5
    crack = bandpass(white(0.15), 2500, 1.2) * env_decay(0.15, 0.03)
    return drive(mix((sub, 0, 1.0), (rumble, 0, 0.8), (crack, 0, 0.35)), 1.5)


def cloth_thud(dur=0.3):
    body = lowpass(white(dur), 400) * env_decay(dur, 0.05)
    return mix((body, 0, 1.0), (thump(90, 0.25, 1.6, 0.1), 0, 0.8))


def flesh_hit(crunch=0.0, dur=0.4):
    """punch / body blow: a thump, a slap, optionally a bone crunch."""
    slap = bandpass(white(0.06), 1400, 1.2) * env_decay(0.06, 0.012)
    body = thump(110 + dsp.rng.uniform(-15, 15), 0.3, 1.8, 0.2)
    parts = [(body, 0, 1.0), (slap, 0, 0.7)]
    if crunch > 0:
        crackle = np.zeros(int(0.12 * SR), np.float32)
        for _ in range(int(25 * crunch)):
            i = dsp.rng.integers(0, len(crackle) - 40)
            crackle[i:i + 30] += dsp.rng.standard_normal(30) * dsp.rng.uniform(0.3, 1.0)
        parts.append((bandpass(crackle, 1800, 1.0) * env_decay(0.12, 0.05), 0.005, crunch))
    return mix(*parts)


def wet_stab():
    """knife into flesh: a dull thud and a short, wet, band-limited squelch that bends down."""
    d = 0.35
    sq = white(d)
    sq = stft_filter(sq, lambda f, t: peak(f, 1400 - 2500 * t, 700) + 0.1 * peak(f, 300, 200))
    sq *= env_decay(d, 0.07, attack=0.004)
    return mix((thump(70, 0.25, 1.5, 0.05), 0, 0.9), (sq, 0.005, 0.9))


def footstep(heavy=False, surface='concrete'):
    heel = lowpass(white(0.09), 1800 if surface == 'concrete' else 900) * env_decay(0.09, 0.015)
    body = thump(80 if heavy else 120, 0.12, 1.4, 0.0) * (1.0 if heavy else 0.5)
    scuff = bandpass(white(0.14), 2200, 1.5) * env_decay(0.14, 0.03) * dsp.rng.uniform(0.03, 0.1)
    x = mix((heel, 0, 1.0), (body, 0, 0.6), (scuff, 0.03, 1.0))
    return eq(x, [(dsp.rng.uniform(400, 900), 400, dsp.rng.uniform(-3, 3))])


# ------------------------------------------------------------------ doors, metal, machines

def creak(dur=1.6, base=55, spread=1.8, body=(420, 900, 1900), roughness=1.0):
    """stick-slip creak: an irregular pulse train (the hinge catching and slipping) exciting a wooden body."""
    n = int(dur * SR)
    f0 = base * (1 + spread * (smooth_random(dur, 3, 0, 1) ** 2)) * glide(1.0, 1.35, dur, 1.0)
    x = np.zeros(n, np.float32)
    ph = 0.0
    for i in range(n):
        ph += f0[i] / SR * (1 + roughness * 0.35 * dsp.rng.standard_normal())
        if ph >= 1.0:
            ph -= 1.0
            x[i] = dsp.rng.uniform(0.5, 1.0)
    res = shape(x, lambda f: sum(peak(f, b, b * 0.25) * g for b, g in zip(body, (1.0, 0.6, 0.3))) + 0.03)
    grain = bandpass(white(dur), 2800, 1.5) * 0.05
    e = env_adsr(dur, a=0.12, d=0.2, s=0.8, r=0.3) * (0.7 + 0.3 * smooth_random(dur, 5, 0, 1))
    return (res + grain) * e


def metal_hit(dur=1.8, fund=180, partials=(1.0, 2.76, 5.40, 8.93, 13.34), decay=0.6, bright=1.0):
    """inharmonic ringing metal (a clank, a latch, a door, a rung)."""
    t = t_axis(dur)
    x = np.zeros(len(t), np.float32)
    for i, p in enumerate(partials):
        f = fund * p * dsp.rng.uniform(0.985, 1.015)
        if f > 16000:
            continue
        a = (0.8 ** i) * (bright if i > 1 else 1.0)
        x += (a * np.sin(2 * np.pi * f * t) * np.exp(-t / (decay / (1 + 0.6 * i)))).astype(np.float32)
    hit = highpass(white(0.03), 2000) * env_decay(0.03, 0.004)
    return mix((x, 0, 1.0), (hit, 0, 0.6))


def latch():
    a = metal_hit(0.35, 1200, (1.0, 2.3, 3.9, 5.6), decay=0.08)
    b = metal_hit(0.45, 900, (1.0, 2.6, 4.3), decay=0.12)
    return mix((a, 0, 0.8), (b, 0.09, 1.0), (thump(160, 0.1, 1.2, 0.2), 0.09, 0.5))


def slam(metal=True, dur=2.8):
    body = boom(dur, 45)
    ring = metal_hit(dur, 95, (1.0, 2.1, 3.3, 5.2, 7.9), decay=1.2) if metal else lowpass(white(dur), 900) * env_decay(dur, 0.1)
    rattle = np.zeros(int(0.6 * SR), np.float32)
    for k in range(10):
        rattle = mix((rattle, 0, 1), (metal_hit(0.2, dsp.rng.uniform(600, 1300), (1, 2.4, 3.9), 0.05), 0.05 + 0.045 * k * (1 + 0.3 * dsp.rng.random()), 0.25 * (0.8 ** k)))
    return mix((body, 0, 1.0), (ring, 0, 0.5), (rattle, 0.05, 0.6))


def grind(dur=1.4, center=700):
    """heavy doors dragging in their track."""
    x = white(dur)
    y = stft_filter(x, lambda f, t: peak(f, center * (1 + 0.3 * np.sin(t * 23) + 0.2 * np.sin(t * 7)), center * 0.5) + 0.1 * peak(f, 150, 120))
    return drive(y * env_adsr(dur, 0.1, 0.2, 0.8, 0.3), 2.5)


def motor_hum(dur, f=48):
    t = t_axis(dur)
    x = np.zeros(len(t), np.float32)
    for h, a in [(1, 1.0), (2, 0.6), (3, 0.35), (4, 0.25), (6, 0.12)]:
        x += a * np.sin(2 * np.pi * f * h * t + dsp.rng.random() * 6).astype(np.float32)
    return x * (0.85 + 0.15 * lfo(0.7, dur))


def buzz(dur, f=50):
    x = square(f, dur) * 0.4 + saw(f * 2, dur) * 0.3
    return bandpass(x, 900, 0.6)


def sparks(dur=0.4, n=18):
    x = np.zeros(int(dur * SR), np.float32)
    for _ in range(n):
        i = dsp.rng.integers(0, len(x) - 200)
        x[i:i + 150] += bandpass(white(150 / SR), 3500, 1.5)[:150] * dsp.rng.uniform(0.2, 1.0)
    return x


# ------------------------------------------------------------------ voices

def voice(dur, f0, vowel, scale=1.0, breath=0.15, rough=0.0, jitter=0.02, sub=0.0):
    """a buzzy glottal source shaped by formants. f0 and vowel may vary over time."""
    n = int(dur * SR)
    f = np.asarray(f0, np.float32) if np.ndim(f0) else np.full(n, f0, np.float32)
    f = f * (1 + jitter * smooth_random(dur, 18))
    src = saw(f, dur)
    if sub > 0:
        src += sub * saw(f / 2, dur)
    if rough > 0:
        src *= 1 + rough * np.sign(sine(f * 0.5 + 23, dur)) * 0.5
    src += breath * white(dur)
    track = vowel if callable(vowel) else (lambda t, v=vowel: v)
    return formants(src, track, scale=scale)


def moan(dur=3.0, f0=95, vowels=('u', 'o', 'a', 'o'), scale=1.0):
    """zombie moan: a low, slow, breathy groan that sags and rises."""
    f = f0 * (1 + 0.25 * smooth_random(dur, 1.2, -1, 1)) * glide(1.05, 0.85, dur)
    def v(t, vs=vowels):
        k = t / dur * (len(vs) - 1)
        i = min(int(k), len(vs) - 2)
        w = k - i
        return [(1 - w, vs[i]), (w, vs[i + 1])]
    x = voice(dur, f, v, scale=scale, breath=0.35, rough=0.6, jitter=0.05, sub=0.4)
    x *= env_adsr(dur, 0.25, 0.3, 0.8, 0.8) * (0.75 + 0.25 * smooth_random(dur, 4, 0, 1))
    return lowpass(drive(x, 1.8, 3500), 4000)


def scream(dur=2.4, f0=420, scale=1.45, vowel='a'):
    """shriek: high, strained, distorted, with a crack in it: rises fast, holds, sags as the breath runs out."""
    t = t_axis(dur)
    shape_ = np.minimum(1.0, 0.8 + t / 0.25 * 0.35) * (1.0 - 0.25 * (t / dur) ** 2)
    f = (f0 * shape_ * (1 + 0.08 * smooth_random(dur, 9, -1, 1))).astype(np.float32)
    x = voice(dur, f, vowel, scale=scale, breath=0.5, rough=0.9, jitter=0.08, sub=0.25)
    x *= env_adsr(dur, 0.06, 0.2, 0.9, 0.6)
    return drive(highpass(x, 250), 3.0, 6500)


def grunt(dur=0.45, f0=120, vowel='uh', breath=0.4):
    f = glide(f0 * 1.25, f0 * 0.8, dur)
    x = voice(dur, f, vowel, breath=breath, rough=0.4, jitter=0.04)
    return drive(x * env_decay(dur, dur / 3, attack=0.015), 2.0, 4000)


def growl(dur=2.0, f0=65, vowel='o', scale=0.85):
    f = f0 * (1 + 0.15 * smooth_random(dur, 6, -1, 1))
    x = voice(dur, f, vowel, scale=scale, breath=0.6, rough=1.2, jitter=0.1, sub=0.8)
    return lowpass(drive(x * env_adsr(dur, 0.08, 0.2, 0.85, 0.4), 3.0, 2500), 3000)


def whisper(dur=2.5, scale=1.0, sibilance=0.6):
    """unvoiced speech: noise through wandering formants, with 'sss' and 'shh' bursts."""
    vs = ['e', 'a', 'i', 'o', 'u', 'aa']
    seq = [vs[dsp.rng.integers(len(vs))] for _ in range(int(dur * 5) + 2)]
    track = lambda t: seq[min(int(t * 5), len(seq) - 1)]
    x = formants(white(dur), track, scale=scale, floor=0.01)
    syll = np.clip(smooth_random(dur, 7, -0.5, 1.2), 0, 1)
    s = bandpass(white(dur), 5500, 2.0) * np.clip(smooth_random(dur, 4, -2.0, 1.0), 0, 1) * sibilance * 0.5
    return (x * syll + s) * env_adsr(dur, 0.1, 0.1, 0.9, 0.3)


def choir(dur=8.0, notes=(220.0, 277.2, 329.6, 440.0), vowel='aa'):
    """voices on a slow chord, vibrato and a soft attack: the heavenly kind."""
    out = np.zeros(int(dur * SR), np.float32)
    for n in notes:
        for k in range(3):
            f = n * (1 + dsp.rng.uniform(-0.004, 0.004)) * (1 + 0.006 * lfo(dsp.rng.uniform(4.5, 5.5), dur, phase=dsp.rng.random() * 6))
            scale = 1.25 if n > 300 else 1.1
            out += voice(dur, f, vowel, scale=scale, breath=0.08, jitter=0.01)
    out *= env_adsr(dur, 2.0, 0.5, 0.9, 2.0)
    return reverb(out, decay=4.5, wet=0.5, predelay=0.04, damp=6000)


# ------------------------------------------------------------------ musical stingers

def braam(dur=3.2, root=55.0):
    """the low brass blast (think every horror trailer since 2010): stacked detuned saws, a filter that opens and shuts."""
    x = np.zeros(int(dur * SR), np.float32)
    for mult in (1.0, 1.0, 1.5, 2.0, 2.0, 3.0):
        x += saw(root * mult * (1 + dsp.rng.uniform(-0.006, 0.006)), dur) * (0.6 if mult > 1.4 else 1.0)
    x = stft_filter(x, lambda f, t: 1 / np.sqrt(1 + (f / (180 + 1400 * np.exp(-((t - 0.25) / 0.5) ** 2))) ** 4))
    x *= env_adsr(dur, 0.04, 0.4, 0.75, 1.4)
    return drive(x, 2.2)


def string_shriek(dur=1.8, top=1500.0):
    """stabbing, shrieking violins (the shower scene school): high bowed saws, sawing tremolo, sliding down."""
    x = np.zeros(int(dur * SR), np.float32)
    for k in range(6):
        f = top * (1 + 0.03 * k) * glide(1.0, 0.8 - 0.03 * k, dur, 1.6) * (1 + 0.012 * lfo(6 + k, dur))
        x += saw(f, dur)
    bow = (0.55 + 0.45 * np.abs(np.sin(np.pi * 7.5 * t_axis(dur)))) ** 2   # the sawing strokes
    x = bandpass(x, 2600, 0.7) * bow + bandpass(white(dur), 3500, 1.5) * 0.15
    x *= env_adsr(dur, 0.01, 0.2, 0.8, 0.7)
    return reverb(x, 1.8, 0.35)


def string_cluster(dur=4.0, low=220.0):
    """a tense, dissonant sustain that swells (the dread before something happens)."""
    x = np.zeros(int(dur * SR), np.float32)
    for semis in (0, 1, 6, 7, 11, 13):
        f = low * 2 ** (semis / 12) * (1 + 0.004 * lfo(5 + semis * 0.3, dur))
        x += saw(f, dur)
    x = lowpass(x, 3500) * env_adsr(dur, dur * 0.7, 0.1, 1.0, 0.4)
    return reverb(x, 2.5, 0.4)


def tinnitus(dur=4.0, f=7800):
    return sine(f, dur) * env_decay(dur, dur / 2.5, attack=0.05) * 0.15


# ------------------------------------------------------------------ weather, crowds, places

def wind(dur, strength=1.0):
    x = pink(dur)
    center = 350 + 500 * smooth_random(dur, 0.4, 0, 1)
    y = stft_filter(x, lambda f, t: peak(f, np.interp(t, np.linspace(0, dur, len(center[::441])), center[::441]), 400) + 0.15 * peak(f, 120, 150))
    gust = 0.4 + 0.6 * smooth_random(dur, 0.35, 0, 1) ** 1.5
    whistle = sine(900 + 300 * smooth_random(dur, 0.5, -1, 1), dur) * 0.03 * np.clip(smooth_random(dur, 0.3, -1, 1), 0, 1)
    return (y * gust + whistle) * strength


def rumble(dur, f=45):
    return lowpass(brown(dur), 160) * (0.8 + 0.2 * smooth_random(dur, 0.5, 0, 1)) + motor_hum(dur, f) * 0.05


def bubbles(dur, rate=12, lo=180, hi=700):
    x = np.zeros(int(dur * SR), np.float32)
    for _ in range(int(dur * rate)):
        d = dsp.rng.uniform(0.03, 0.12)
        f0 = dsp.rng.uniform(lo, hi)
        b = sine(glide(f0, f0 * dsp.rng.uniform(1.6, 2.6), d, 0.6), d) * env_decay(d, d / 3, 0.003)
        s = dsp.rng.integers(0, max(1, len(x) - len(b)))
        x[s:s + len(b)] += b * dsp.rng.uniform(0.2, 1.0)
    return x


def crackle(dur, rate=60):
    x = np.zeros(int(dur * SR), np.float32)
    for _ in range(int(dur * rate)):
        i = dsp.rng.integers(0, len(x) - 60)
        x[i:i + 50] += dsp.rng.standard_normal(50) * dsp.rng.uniform(0.05, 0.6)
    return bandpass(x, 1800, 0.8)


def birds(dur, n=10):
    x = np.zeros(int(dur * SR), np.float32)
    for _ in range(n):
        start = dsp.rng.uniform(0, dur - 1)
        f = dsp.rng.uniform(2800, 4800)
        for k in range(dsp.rng.integers(2, 6)):
            d = dsp.rng.uniform(0.05, 0.12)
            c = sine(glide(f, f * dsp.rng.uniform(1.1, 1.5), d, 0.5), d) * env_adsr(d, 0.01, 0.02, 0.7, 0.03)
            s = int((start + k * dsp.rng.uniform(0.1, 0.18)) * SR)
            if s + len(c) < len(x):
                x[s:s + len(c)] += c * dsp.rng.uniform(0.2, 0.5)
    return x


def car_pass(dur=5.0):
    """a car going by: noise and engine that swell and fade, pitch dropping as it passes."""
    t = t_axis(dur)
    k = np.exp(-((t - dur / 2) / (dur / 5)) ** 2).astype(np.float32)
    eng = motor_hum(dur, 60) * glide(1.0, 1.0, dur)
    doppler = sine(glide(95, 78, dur, 1.0), dur) * 0.4
    tyres = bandpass(white(dur), 900, 0.7) * 0.8
    return (lowpass(eng * 0.3 + doppler + tyres, 2500)) * k


def babble(dur, voices=3, lowpass_hz=2800, scale=1.0):
    """indistinct talking (a TV in the next room, a crowd)."""
    out = np.zeros(int(dur * SR), np.float32)
    vs = ['a', 'e', 'i', 'o', 'u', 'aa', 'uh']
    for k in range(voices):
        f0 = dsp.rng.uniform(100, 220) * (1 + 0.12 * smooth_random(dur, 3, -1, 1))
        seq = [vs[dsp.rng.integers(len(vs))] for _ in range(int(dur * 6) + 2)]
        v = voice(dur, f0, lambda t, s=seq: s[min(int(t * 6), len(s) - 1)], scale=scale * dsp.rng.uniform(0.95, 1.2), breath=0.1)
        gate = np.clip(smooth_random(dur, 5, -0.6, 1.2), 0, 1) * np.clip(smooth_random(dur, 0.6, -0.2, 1.5), 0, 1)
        out += v * gate
    return lowpass(highpass(out, 250), lowpass_hz)


# ------------------------------------------------------------------ guns

def gunshot(space=2.5):
    crack = highpass(white(0.004), 3000) * 2.0
    body = lowpass(white(0.35), 2500) * env_decay(0.35, 0.05)
    lo = sine(glide(140, 55, 0.4, 0.3), 0.4) * env_decay(0.4, 0.09)
    x = drive(mix((crack, 0, 1.0), (body, 0, 1.0), (lo, 0, 1.4)), 3.0)
    return reverb(x, space, 0.45, predelay=0.03, damp=3500)


def mag_clicks():
    a = metal_hit(0.25, 1600, (1, 2.2, 3.7), 0.04)
    b = metal_hit(0.3, 1100, (1, 2.5, 4.1), 0.05)
    c = metal_hit(0.35, 800, (1, 2.8, 4.6), 0.07)
    return mix((a, 0, 0.7), (b, 0.35, 0.9), (c, 0.75, 1.0), (thump(140, 0.1, 1.3, 0.3), 0.75, 0.4))


# ------------------------------------------------------------------ heart

def heartbeat(dur=8.0, bpm=72, strength=1.0, muffle=900):
    x = np.zeros(int(dur * SR), np.float32)
    period = 60.0 / bpm
    t = 0.05
    while t < dur - 0.4:
        lub = thump(52, 0.18, 1.9, 0.05)
        dub = thump(62, 0.14, 1.7, 0.05) * 0.7
        s = int(t * SR)
        x[s:s + len(lub)] += lub
        s2 = int((t + 0.27) * SR)
        x[s2:s2 + len(dub)] += dub
        t += period * dsp.rng.uniform(0.97, 1.03)
    return lowpass(x, muffle) * strength
