"""Generates every HellScape sound effect into Assets/Audio/Generated/HellScape/<Area>/.

    python Tools/sfx/make_sfx.py            (all of them)
    python Tools/sfx/make_sfx.py Prologue   (one area)

Each entry: (area, slot name in the Sound Library, [file names], generator). Several files = variations the
game picks between at random. Music is left alone: that's yours to choose.
"""
import os
import sys
import time
import numpy as np

import dsp
from dsp import *
from instruments import *

ROOT = os.path.join(os.path.dirname(__file__), '..', '..', 'Assets', 'Audio', 'Generated', 'HellScape')


def V(n, fn):
    """n variations of a sound."""
    return lambda: [fn(i) for i in range(n)]


# ------------------------------------------------------------------ the sounds

def start_pressed():
    return mix((boom(3.5, 36), 0, 1.0), (braam(3.0, 41.2), 0, 0.5), (string_shriek(1.5, 1900), 0.05, 0.25))


def heartbeat_loop():
    return loopable(heartbeat(8.5, 72, muffle=700), 0.3)


def sting():
    return mix((string_shriek(1.8, 1550), 0, 1.0), (boom(2.5, 40), 0, 0.8))


def tape_stabs():
    parts = []
    t = 0.2
    for k in range(6):
        parts.append((wet_stab(), t, dsp.rng.uniform(0.7, 1.0)))
        t += dsp.rng.uniform(0.28, 0.55)
    x = mix(*parts, (grunt(0.4, 105, 'uh', 0.6), 0.25, 0.5), (grunt(0.5, 98, 'a', 0.7), 1.3, 0.4))
    # through the flat's cheap CCTV camera: narrow, crunchy, hissy
    x = lowpass(bitcrush(bandpass(x, 1200, 0.35), 7, 3), 3500) + lowpass(white(len(x) / SR), 5000) * 0.012
    return reverb(x, 0.6, 0.2)


def street_ambience():
    d = 30.0
    traffic = lowpass(brown(d), 300) * 0.6
    passes = mix(*[(car_pass(5.0), dsp.rng.uniform(0, d - 5), dsp.rng.uniform(0.5, 1.0)) for _ in range(6)])
    return loopable(mix((traffic, 0, 1.0), (pad_to(passes, d), 0, 0.8), (birds(d, 12), 0, 0.35), (babble(d, 2, 2000) * 0.1, 0, 1.0)), 1.0)


def tv_murmur():
    d = 16.0
    x = babble(d, 3, 3200, 1.05) + pink(d) * 0.02
    x = eq(x, [(1800, 1500, 5), (180, 150, -8)])      # small speaker
    return loopable(x, 0.6)


def wind_loop():
    return loopable(wind(20.0), 1.0)


def fall_whisper():
    x = mix((whisper(3.0, 1.0), 0, 1.0), (whisper(3.0, 0.8), 0.3, 0.7), (whisper(2.5, 1.2), 0.6, 0.5))
    return reverb(x, 3.0, 0.55)


def push():
    whoosh = stft_filter(white(0.4), lambda f, t: peak(f, 600 + 3000 * t, 800)) * env_adsr(0.4, 0.15, 0.1, 0.6, 0.15)
    return mix((whoosh, 0, 0.6), (cloth_thud(), 0.12, 1.0), (grunt(0.35, 130, 'uh'), 0.1, 0.6))


def street_impact():
    return mix((lowpass(flesh_hit(1.0, 0.6), 3000), 0, 1.0), (boom(3.0, 34), 0, 0.9), (tinnitus(5.0, 6800), 0.3, 0.5))


def door_creak(i=0):
    return reverb(creak(1.4 + 0.3 * i, 50 + 12 * i, 1.6), 0.8, 0.2)


def door_bang():
    return mix((slam(False, 1.6), 0, 1.0), (creak(0.5, 90, 1.0), 0.1, 0.4))


def footsteps(heavy):
    return V(7, lambda i: pad_to(reverb(footstep(heavy), 0.45, 0.1, damp=2200), 0.5))


def wake_heartbeat():
    x = heartbeat(7.0, 54, 1.3, muffle=500)
    return mix((x, 0, 1.0), (tinnitus(7.0, 8200), 0, 1.0), (lowpass(brown(7.0), 120) * 0.2, 0, 1.0))


def hell_ambience():
    d = 40.0
    drone = sine(36, d) * 0.4 + sine(36.7, d) * 0.4 + sine(54.3, d) * 0.25
    screams = mix(*[(reverb(lowpass(scream(dsp.rng.uniform(1.5, 2.5), dsp.rng.uniform(300, 600)), 2500), 4.0, 0.8), dsp.rng.uniform(0, d - 6), dsp.rng.uniform(0.05, 0.12)) for _ in range(7)])
    moans = mix(*[(reverb(moan(dsp.rng.uniform(2.5, 4.0), dsp.rng.uniform(70, 110)), 3.0, 0.7), dsp.rng.uniform(0, d - 6), dsp.rng.uniform(0.08, 0.15)) for _ in range(6)])
    x = mix((drone, 0, 1.0), (wind(d, 0.5), 0, 1.0), (rumble(d), 0, 0.6), (crackle(d, 25), 0, 0.15),
            (pad_to(screams, d), 0, 1.0), (pad_to(moans, d), 0, 1.0))
    return loopable(lowpass(x, 3500), 2.0)


def zombie_moan(i):
    v = [('u', 'o', 'a', 'o'), ('o', 'a', 'uh'), ('uh', 'a', 'o', 'u'), ('a', 'o'), ('u', 'uh', 'a')][i % 5]
    return reverb(moan(dsp.rng.uniform(2.2, 3.8), dsp.rng.uniform(70, 115), v, dsp.rng.uniform(0.9, 1.1)), 1.2, 0.2)


def zombie_scream(i):
    return reverb(mix((scream(dsp.rng.uniform(2.0, 2.8), dsp.rng.uniform(330, 480), dsp.rng.uniform(1.3, 1.6), ['a', 'aa', 'e'][i % 3]), 0, 1.0),
                      (growl(1.2, 70), 0, 0.3)), 1.5, 0.25)


def punch_hit(i):
    return flesh_hit(crunch=[0.0, 0.3, 0.7][i % 3])


def car_hits(i):
    thud = boom(0.8, 60) * 0.8
    crunch = flesh_hit(1.0, 0.5)
    glassy = lowpass(metal_hit(0.6, dsp.rng.uniform(2200, 3000), (1, 1.7, 2.9, 4.1), 0.08), 5000) * (0.2 if i == 2 else 0.06)
    return mix((thud, 0, 1.0), (crunch, 0.01, 1.0), (glassy, 0.02, 1.0), (lowpass(white(0.5), 800) * env_decay(0.5, 0.15), 0.08, 0.6))


def car_engine():
    d = 4.0
    t = t_axis(d)
    x = np.zeros(len(t), np.float32)
    f = 42.0
    for h in range(1, 18):
        x += (1.0 / h ** 0.8) * np.sin(2 * np.pi * f * h * t + dsp.rng.random() * 6).astype(np.float32) * (1 + 0.3 * smooth_random(d, 20, -1, 1))
    x = lowpass(x, 1800) + lowpass(brown(d), 250) * 0.3
    return loopable(drive(x * 0.6, 1.5), 0.4)


def tire_screech():
    d = 3.0
    tone = sine(1700 + 250 * smooth_random(d, 6, -1, 1) + 60 * lfo(23, d), d)
    noise = bandpass(white(d), 2400, 1.2)
    return loopable(drive(tone * 0.5 + noise * 0.7, 2.0), 0.3)


def ghost_whispers():
    d = 10.0
    x = mix(*[(whisper(dsp.rng.uniform(1.5, 3.0), dsp.rng.uniform(0.85, 1.15), 0.5), dsp.rng.uniform(0, d - 3), dsp.rng.uniform(0.5, 1.0)) for _ in range(7)])
    return loopable(reverb(pad_to(x, d), 2.5, 0.6), 1.0)


def fuse_clunk():
    return mix((metal_hit(0.8, 260, (1, 2.4, 3.8, 5.9), 0.2), 0, 0.8), (thump(90, 0.3, 1.5, 0.3), 0, 1.0),
               (buzz(0.5, 50) * env_decay(0.5, 0.15), 0.05, 0.5), (sparks(0.35, 14), 0.03, 0.6))


def kessler_whisper():
    d = 5.0
    ws = mix(*[(whisper(dsp.rng.uniform(1.5, 2.5), dsp.rng.uniform(0.7, 1.1)), dsp.rng.uniform(0, d - 2.5), 0.6 + 0.4 * k / 10) for k in range(10)])
    drone = string_cluster(d, 110)
    x = mix((pad_to(ws, d) * glide(0.4, 1.3, d), 0, 1.0), (drone, 0, 0.5))
    return reverb(x, 2.5, 0.5)


def kessler_turns():
    return mix((boom(3.0, 32), 0, 1.0), (braam(2.5, 36.7), 0, 0.6), (growl(2.2, 58, 'a', 0.8), 0.05, 0.9), (scream(1.5, 260, 1.1), 0.1, 0.4))


def kessler_lunge():
    return mix((growl(0.9, 80, 'a', 0.9), 0, 1.0), (flesh_hit(0.3), 0.25, 0.8))


def killer_sting():
    return mix((braam(2.8, 49), 0, 1.0), (string_shriek(1.4, 1300), 0, 0.5), (metal_hit(2.0, 70, (1, 2.3, 3.7, 5.5), 1.0), 0, 0.4))


def lift_slam():
    return reverb(slam(True, 3.0), 1.5, 0.3)


def lift_doors():
    return reverb(mix((grind(1.2, 650), 0, 0.8), (metal_hit(1.4, 110, (1, 2.2, 3.6, 5.1), 0.6), 1.05, 0.9), (thump(70, 0.4, 1.5, 0.2), 1.05, 0.8)), 1.2, 0.25)


def lift_rumble():
    d = 8.0
    rattles = mix(*[(metal_hit(0.15, dsp.rng.uniform(500, 1400), (1, 2.5, 4.0), 0.03), dsp.rng.uniform(0, d - 0.2), dsp.rng.uniform(0.05, 0.2)) for _ in range(30)])
    x = mix((motor_hum(d, 46), 0, 0.5), (rumble(d, 46), 0, 1.0), (pad_to(rattles, d), 0, 1.0), (grind(d, 400) * 0.15, 0, 1.0))
    return loopable(lowpass(x, 2800), 0.5)


def ladder_rung(i):
    return reverb(metal_hit(0.7, [310, 355, 290][i % 3], (1.0, 2.63, 5.1, 8.4), 0.25), 1.0, 0.25)


def chase_hit():
    return mix((flesh_hit(0.5), 0, 1.0), (grunt(0.4, 125, 'uh', 0.5), 0.03, 0.8))


def silo_thud():
    return reverb(mix((thump(50, 0.6, 2.0, 0.3), 0, 1.0), (cloth_thud(0.35), 0, 0.7)), 2.5, 0.35)


def killer_hurt(i):
    return grunt(dsp.rng.uniform(0.35, 0.6), dsp.rng.uniform(95, 125), ['uh', 'a', 'o'][i % 3], 0.5)


def rifle_shot():
    return gunshot(3.0)


def rifle_reload():
    return reverb(mag_clicks(), 1.2, 0.2)


def blood_flood():
    d = 12.0
    gush = bandpass(pink(d), 700, 0.5) * (0.7 + 0.3 * smooth_random(d, 0.8, 0, 1))
    x = mix((rumble(d, 38), 0, 1.0), (gush, 0, 0.6), (bubbles(d, 18), 0, 0.4), (bubbles(d, 6, 60, 150), 0, 0.6))
    return loopable(reverb(x, 3.0, 0.4, tail=False), 1.0)


def swim_stroke(i):
    """a kick through thick blood: a heavy slosh, a suck of liquid, a few fat bubbles."""
    d = 0.7
    slosh = lowpass(bandpass(pink(d), dsp.rng.uniform(260, 380), 0.6), 1200) * env_adsr(d, 0.03, 0.15, 0.4, 0.35)
    suck = bandpass(white(d), dsp.rng.uniform(700, 950), 1.5) * env_decay(d, 0.08, 0.02) * 0.4
    x = mix((slosh, 0, 1.0), (suck, 0.02, 1.0), (bubbles(d, 14, 90, 260), 0.1, 0.5),
            (thump(dsp.rng.uniform(55, 70), 0.3, 1.5, 0.05), 0, 0.35))
    return reverb(x, 1.8, 0.25)


def drowning():
    """going under: a choked gasp, blood rushing into the ears, the gurgle, then the muffled heartbeat stopping."""
    gasp = voice(0.9, glide(260, 170, 0.9), 'aa', breath=1.4, rough=0.5, jitter=0.1) * env_adsr(0.9, 0.05, 0.2, 0.7, 0.4)
    choke = voice(1.6, 140 * (1 + 0.3 * smooth_random(1.6, 12, -1, 1)), 'uh', breath=0.6, rough=1.3, jitter=0.2)
    choke = lowpass(choke * np.clip(smooth_random(1.6, 9, -0.4, 1.0), 0, 1), 900)
    rush = lowpass(pink(5.0), 500) * env_adsr(5.0, 0.4, 0.5, 0.8, 2.5)
    gurgle = lowpass(bubbles(3.0, 45, 70, 260) + bubbles(3.0, 10, 40, 110), 900) * env_adsr(3.0, 0.1, 0.5, 0.7, 1.5)
    hb = heartbeat(4.0, 52, 1.0, 300) * glide(1.0, 0.0, int(4.0 * SR) / SR, 0.6)
    x = mix((highpass(gasp, 200), 0, 0.7), (choke, 0.6, 0.8), (rush, 0.5, 0.8), (gurgle, 0.7, 1.0), (hb, 1.4, 0.9))
    return reverb(x, 2.5, 0.3)


def survivor_drowning(i):
    """Maya (0) or Sam (1): a cry cut off under the blood."""
    f0, scale = (330, 1.3) if i == 0 else (170, 1.0)
    cry = scream(1.1, f0, scale, 'a') * env_adsr(1.1, 0.02, 0.2, 0.9, 0.15)
    under = voice(1.4, f0 * 0.7 * (1 + 0.3 * smooth_random(1.4, 10, -1, 1)), 'u', scale=scale, breath=0.5, rough=1.0)
    under = lowpass(under * env_adsr(1.4, 0.02, 0.3, 0.6, 0.8), 600)
    gurgle = lowpass(bubbles(2.5, 35, 80, 300), 1000) * env_adsr(2.5, 0.05, 0.4, 0.6, 1.4)
    splash = lowpass(bandpass(pink(0.6), 400, 0.6), 1500) * env_decay(0.6, 0.12, 0.01)
    return reverb(mix((cry, 0, 0.8), (splash, 0.95, 0.8), (under, 1.0, 0.8), (gurgle, 1.05, 1.0)), 3.0, 0.35)


def turns_lost():
    """a drowned soul drifting up: a breathy choir swelling out of the blood, whispers round it, a cold shimmer."""
    d = 7.0
    ch = pad_to(choir(d, (196.0, 233.1, 293.7, 392.0), 'u'), d) * env_adsr(d, 2.5, 0.5, 0.8, 2.5)
    ch = lowpass(ch, 3000)
    wh = mix((whisper(3.0, 1.1), 0.8, 0.5), (whisper(3.0, 0.9), 3.2, 0.4))
    shimmer = highpass(pink(d), 5000) * env_adsr(d, 3.0, 0.5, 0.5, 2.5) * 0.15
    under = lowpass(bubbles(2.5, 30, 80, 250), 900) * env_decay(2.5, 0.8, 0.05)
    return reverb(mix((ch, 0, 0.9), (pad_to(wh, d), 0, 0.8), (shimmer, 0, 1.0), (under, 0, 0.8)), 4.0, 0.45)


def turns_damned():
    """the turn: a breath that curdles into a growl, bones and flesh wrenching, a rising shriek, then the hit."""
    d = 6.0
    breath = pad_to(voice(2.0, glide(200, 120, 2.0), 'aa', scale=1.25, breath=1.0, rough=0.3), 2.0) * env_adsr(2.0, 0.3, 0.3, 0.7, 0.6)
    gr = pad_to(growl(3.2, 55, 'o', 0.8), 3.2) * env_adsr(3.2, 1.2, 0.3, 1.0, 0.5)
    cracks = mix(*[(flesh_hit(0.8, 0.3), 1.6 + i * 0.37 + dsp.rng.uniform(0, 0.15), 0.5) for i in range(7)])
    shriek = pad_to(string_shriek(2.2, 1700), 2.2) * glide(0.0, 1.0, 2.2, 2.0)
    hit = mix((boom(2.5, 34), 0, 1.0), (braam(2.0, 36.7), 0, 0.5))
    return mix((highpass(breath, 150), 0, 0.6), (gr, 1.2, 0.8), (cracks, 0, 0.8), (shriek, 2.0, 0.35), (hit, 4.1, 1.0))


def blood_wave():
    d = 10.0
    roar = lowpass(pink(d), 1400) + lowpass(brown(d), 200) * 0.8
    x = mix((roar, 0, 1.0), (growl(d, 45, 'o', 0.8) * 0.25, 0, 1.0), (bubbles(d, 25, 100, 400), 0, 0.3))
    return loopable(drive(x, 1.6, 2500), 1.0)


def angel_choir():
    return choir(8.0, (220.0, 277.2, 329.6, 440.0, 554.4), 'aa')


def demon_rises():
    d = 6.5
    return mix((rumble(d, 30) * env_adsr(d, 1.5, 0.5, 1.0, 1.0), 0, 1.0), (growl(4.5, 42, 'o', 0.7), 1.2, 1.0),
               (boom(3.0, 30), 1.0, 0.8), (braam(4.0, 32.7), 1.4, 0.5))


def dialogue_blip(i):
    """a low mumbled syllable (a tired man's voice, not a beep): a short voiced vowel, muffled, soft attack and
    release. Several variations: the box picks one at random per blip."""
    d = 0.1
    f0 = [98.0, 108.0, 92.0, 116.0, 102.0, 88.0][i % 6]
    vowel = ['uh', 'o', 'a', 'u', 'e', 'uh'][i % 6]
    f = glide(f0 * 1.06, f0 * 0.94, d)
    x = pad_to(voice(d, f, vowel, scale=0.95, breath=0.25, rough=0.2, jitter=0.02, sub=0.3), d)
    x = x * env_adsr(d, 0.012, 0.03, 0.7, 0.045)
    return lowpass(x, 1600) * 0.8


def dt_latch():
    return reverb(latch(), 0.9, 0.2)


def dt_creak():
    return reverb(creak(2.4, 42, 1.4, (380, 820, 1700)), 1.5, 0.3)


def dt_step():
    return pad_to(reverb(footstep(True), 0.6, 0.15, damp=2200), 0.7)


SOUNDS = [
    ('Title', 'Start pressed', ['start_pressed'], start_pressed),

    ('Prologue', 'Heartbeat', ['heartbeat'], heartbeat_loop),
    ('Prologue', 'Sting (body, spotted)', ['sting'], sting),
    ('Prologue', 'Stabbing on the tape', ['tape_stabs'], tape_stabs),
    ('Prologue', 'Street ambience', ['street_morning'], street_ambience),
    ('Prologue', 'TV murmur', ['tv_murmur'], tv_murmur),
    ('Prologue', 'Wind (roof and fall)', ['wind'], wind_loop),
    ('Prologue', 'Whisper (the fall)', ['fall_whisper'], fall_whisper),
    ('Prologue', 'Push', ['push'], push),
    ('Prologue', 'Impact (hitting the street)', ['impact'], street_impact),
    ('Prologue', 'Door creak', ['door_creak_1', 'door_creak_2', 'door_creak_3'], V(3, door_creak)),
    ('Prologue', 'Door flung open', ['door_bang'], door_bang),
    ('Prologue', 'Killer footsteps', [f'killer_step_{i + 1}' for i in range(7)], footsteps(True)),

    ('Hell City', 'Wake heartbeat', ['wake_heartbeat'], wake_heartbeat),
    ('Hell City', 'Hell ambience', ['hell_ambience'], hell_ambience),
    ('Hell City', 'Zombie moans', [f'moan_{i + 1}' for i in range(5)], V(5, zombie_moan)),
    ('Hell City', 'Zombie scream', [f'scream_{i + 1}' for i in range(3)], V(3, zombie_scream)),
    ('Hell City', 'Punch hits', [f'punch_{i + 1}' for i in range(3)], V(3, punch_hit)),
    ('Hell City', 'Car hits a zombie', [f'car_hit_{i + 1}' for i in range(3)], V(3, car_hits)),
    ('Hell City', 'Car engine', ['car_engine'], car_engine),
    ('Hell City', 'Tire screech', ['tire_screech'], tire_screech),

    ('Office', 'Ghost whispers', ['ghost_whispers'], ghost_whispers),
    ('Office', 'Fuse clunk', ['fuse_clunk'], fuse_clunk),
    ('Office', 'Kessler door latch', ['kessler_latch'], dt_latch),
    ('Office', 'Kessler door creak', ['kessler_creak'], dt_creak),
    ('Office', 'Kessler turning (whisper)', ['kessler_turning'], kessler_whisper),
    ('Office', 'Kessler becomes the damned', ['kessler_turns'], kessler_turns),
    ('Office', 'Kessler lunges', ['kessler_lunge'], kessler_lunge),

    ('Silo Complex', 'Killer sting', ['killer_sting'], killer_sting),
    ('Silo Complex', 'Lift doors slam on him', ['lift_slam'], lift_slam),
    ('Silo Complex', 'Lift doors', ['lift_doors'], lift_doors),
    ('Silo Complex', 'Lift ride rumble', ['lift_rumble'], lift_rumble),
    ('Silo Complex', 'Ladder rung', [f'ladder_rung_{i + 1}' for i in range(3)], V(3, ladder_rung)),
    ('Silo Complex', 'Killer hits you (chase)', ['chase_hit'], chase_hit),

    ('Silo Fight', 'Push and fall thud', ['silo_thud'], silo_thud),
    ('Silo Fight', 'Killer hurt', [f'killer_hurt_{i + 1}' for i in range(3)], V(3, killer_hurt)),
    ('Silo Fight', 'Rifle shot', ['rifle_shot'], rifle_shot),
    ('Silo Fight', 'Rifle reload', ['rifle_reload'], rifle_reload),
    ('Silo Fight', 'Blood flood rumble', ['blood_flood'], blood_flood),
    ('Silo Fight', 'Swim stroke', [f'swim_stroke_{i + 1}' for i in range(4)], V(4, swim_stroke)),
    ('Silo Fight', 'Drowning', ['drowning'], drowning),
    ('Silo Fight', 'Colleague turns into the Lost', ['turns_lost'], turns_lost),
    ('Silo Fight', 'Colleague turns into the Damned', ['turns_damned'], turns_damned),
    ('Silo Fight', 'Survivor drowning', ['maya_drowning', 'sam_drowning'], V(2, survivor_drowning)),

    ('Escape', 'Blood wave roar', ['blood_wave'], blood_wave),

    ('Ending', 'Angel choir', ['angel_choir'], angel_choir),
    ('Ending', 'Demon rises', ['demon_rises'], demon_rises),

    ('General', 'Dialogue blip', [f'mumble_{i + 1}' for i in range(6)], V(6, dialogue_blip)),
    ('General', 'Player footsteps', [f'step_{i + 1}' for i in range(7)], footsteps(False)),
    ('General', 'Door transition latch', ['door_latch'], dt_latch),
    ('General', 'Door transition creak', ['door_creak_long'], dt_creak),
    ('General', 'Door transition step', ['door_step'], dt_step),
]


def main(only=None):
    mpath = os.path.join(ROOT, 'manifest.tsv')
    made = set()
    manifest = []
    for n, (area, slot, files, gen) in enumerate(SOUNDS):
        if only and area != only:
            continue
        dsp.seed(1000 + n)
        t0 = time.time()
        out = gen()
        clips = out if isinstance(out, list) else [out]
        made.add((area, slot))
        for name, x in zip(files, clips):
            path = os.path.join(ROOT, area, name + '.wav')
            write(path, x, peak_db=-6.0 if slot == 'Dialogue blip' else -1.0)
            manifest.append(f'{area}\t{slot}\tAssets/Audio/Generated/HellScape/{area}/{name}.wav')
        print(f'{area:13s} {slot:30s} {len(clips)} file(s)  {time.time() - t0:5.1f}s', flush=True)
    if only and os.path.exists(mpath):   # keep the other areas' lines
        with open(mpath, encoding='utf-8') as f:
            old = [l for l in f.read().splitlines() if l and tuple(l.split('	')[:2]) not in made]
        manifest = old + manifest
    with open(mpath, 'w', encoding='utf-8') as f:
        f.write('\n'.join(manifest))


if __name__ == '__main__':
    main(sys.argv[1] if len(sys.argv) > 1 else None)
