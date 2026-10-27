# The score of Hollowmere: a ship of stone the sea has got into. Everything is in D, the key the drowned Choir
# sings in; two themes run through it, the Choir's hymn and the lullaby Agathe sang to Maren in the lighthouse.
# Every region has a bed (its own instruments: the hull's timbers groaning, dead machinery breathing, monks
# chanting with one voice going wrong, a drowned organ, a giant's heart, a throat swallowing, a music box running
# down), and over them the game lays three shared layers by how things stand: dread (something knows she is
# there), the chase (it is coming), and the tide (the sea's own choir, rising as the water does). Bell Rooms have
# their own music, the only warm thing on the island. Stingers mark the moments: seen, a scare, a discovery, a boss,
# death. The region ambience beds are the rooms themselves, with no music in them.
#
# Everything loops without a seam: events that run past the end of a loop, and the reverb's tail, fold back onto
# its start. Output: godot/audio/{score,amb,sting}/*.ogg.
# Usage: python3 tools/score/compose.py [cue ...] [--reel file.mp3]
import os, subprocess, sys, tempfile, time
import numpy as np
from dsp import *

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'godot', 'audio')

# ---- the themes -------------------------------------------------------------------------------------------------
# Agathe's lullaby, in 3: eight bars of D minor
LULLABY = [('A4', 1), ('F4', 1), ('G4', 1), ('A4', 2), ('D5', 1), ('C5', 1), ('A4', 1), ('G4', 1), ('F4', 3),
           ('G4', 1), ('A4', 1), ('Bb4', 1), ('A4', 1), ('F4', 1), ('D4', 1), ('E4', 1), ('F4', 1), ('E4', 1), ('D4', 3)]
# the Choir's hymn: the phrase the Hollow plays, and its chords
HYMN = [('A4', 2), ('G4', 2), ('F4', 2), ('D4', 4), ('D4', 2), ('F4', 2), ('A4', 2), ('D5', 4)]
HYMN_CHORDS = [['D2', 'D3', 'F3', 'A3'], ['Bb1', 'Bb2', 'D3', 'F3'], ['F2', 'A2', 'C3', 'F3'], ['C2', 'C3', 'E3', 'G3'],
               ['G1', 'G2', 'Bb2', 'D3'], ['A1', 'A2', 'C#3', 'E3'], ['D2', 'D3', 'F3', 'A3'], ['D2', 'A2', 'D3', 'F3']]

def melody(track, notes, start, beat, play, gain=1.0, p=0.0, legato=1.1):
    t = start
    for name, beats in notes:
        x = play(hz(name), beats * beat * legato)
        track.add(x, t, gain, p)
        t += beats * beat
    return t

def continuous(track, make, seconds, xf=6.0, gain=1.0, p=0.0):
    """something that never stops (a drone, the wind): rendered a crossfade longer than the loop, faded in
    and out with equal-power curves, so when the tail folds onto the head the seam is a crossfade"""
    x = make(seconds + xf)
    n = len(x)
    e = np.ones(n); a = n_(xf)
    e[:a] = np.sqrt(np.linspace(0, 1, a)); e[n - a:] *= np.sqrt(np.linspace(1, 0, a))
    x = x * (e[:, None] if x.ndim == 2 else e)
    track.add(x, 0, gain, p)

def finish(track, ir=None, wet=0.35, xf=6.0, target=-20.0, lowpass=None, highpass=None):
    buf = track.buf
    if ir is not None: buf = reverb(buf, ir, wet)[:len(track.buf)]
    if lowpass: buf = lp(buf, lowpass, 2)
    if highpass: buf = hp(buf, highpass, 2)
    track.buf = buf
    return master(track.loop(), target)

def one_shot(track, ir=None, wet=0.35, target=-16.0):
    buf = track.buf
    if ir is not None: buf = reverb(buf, ir, wet)[:len(track.buf)]
    out = buf
    # trim the silence at the end
    e = np.max(np.abs(out), 1); last = np.nonzero(e > 1e-4)[0]
    out = out[: (last[-1] + n_(0.2)) if len(last) else len(out)]
    fade = min(len(out), n_(0.3)); out[-fade:] *= np.linspace(1, 0, fade)[:, None]
    return master(out, target, 0.95)

R = lambda seed: np.random.default_rng(seed)

# =====================================================================================================================
# region beds (64 s loops)
# =====================================================================================================================
def bed_wrackline():
    # the sea caves and the drowned chapel: the hull's timbers groaning in the swell, the Saint calling from below,
    # and the lullaby, far off, on wet glass
    L = 64; T = Track(L, 14); r = R(11)
    breath = 0.6 + 0.4 * np.sin(2 * np.pi * tt(n_(L + 6)) / 16) ** 2          # the sea breathes every 16 s
    continuous(T, lambda d: strings(hz('D2'), d, 1.0, 6, 0.01, 0.01, 1.4, 900, seed=1) * breath[:n_(d), None], L, gain=0.7)
    continuous(T, lambda d: strings(hz('A1'), d, 1.0, 4, 0.01, 0.01, 1.4, 700, seed=2) * breath[:n_(d), None], L, gain=0.45)
    continuous(T, lambda d: surf(d, 1.0, 16, seed=3), L, gain=0.25)
    for k in range(9):
        T.add(creak(r.uniform(1.2, 2.6), 1.0, seed=100 + k), r.uniform(0, L), r.uniform(0.25, 0.6), r.uniform(-0.9, 0.9))
    T.add(lp(whale(46, 11, 1.0, (1.0, 1.55, 1.3, 0.62), seed=4), 900), 18, 0.55, -0.3)
    T.add(lp(whale(41, 9, 1.0, (0.9, 1.25, 0.7), seed=5), 600), 46, 0.35, 0.5)
    melody(T, LULLABY[:4], 6, 2.3, lambda f, d: glass(f, d, 1.0), 0.16, 0.25, 1.3)
    melody(T, LULLABY[4:8], 34, 2.6, lambda f, d: glass(f, d, 1.0, 1), 0.12, -0.3, 1.3)
    T.add(brass(hz('D2'), 7, 1.0, 0.5, seed=6), 55, 0.12, 0.6)             # a foghorn from the mainland, far off
    return finish(T, impulse(7.5, 0.35, 0.05, seed=7), 0.5, target=-22)

def bed_saltworks():
    # the salt halls: steel bowed by the wind, the dead machinery still breathing, the low brass of the pans
    L = 64; T = Track(L, 12); r = R(21)
    for i, (note, at) in enumerate([('D2', 0), ('Eb2', 8), ('D2', 16), ('C#2', 24), ('D2', 32), ('Eb2', 40), ('D2', 48), ('C2', 56)]):
        T.add(bowed_metal(hz(note), 12, 1.0, seed=200 + i), at, 0.5, r.uniform(-0.6, 0.6))
    t = 0.0; k = 0
    while t < L:
        # a piston somewhere, out of time, as if the engine had forgotten how to run
        if r.random() > 0.18:
            T.add(lp(drum(46, 1.4, 1.0, seed=300 + k), 300), t, 0.35, -0.2)
            if r.random() < 0.55: T.add(lp(bell(hz('D4') * r.choice([1, 1.5, 2.1]), 1.2, 1.0, 0.4, seed=400 + k), 2200), t + 0.21, 0.12, r.uniform(-0.8, 0.8))
        t += 1.6 + r.uniform(-0.12, 0.18); k += 1
    for note, at in [('D2', 10), ('Eb2', 30), ('C2', 50)]:
        T.add(brass(hz(note), 8, 1.0, 0.55, seed=int(at)), at, 0.22, 0)
    for k in range(5):
        st = bp(noise(n_(3), 'white', R(500 + k)), 3200, 1.2) * swell(n_(3), 0.2, 1.2)
        T.add(st, r.uniform(0, L), 0.08, r.uniform(-1, 1))
    continuous(T, lambda d: hp(noise(n_(d), 'pink', R(23)), 5000, 2) * (0.5 + 0.5 * smooth_noise(n_(d), 0.3, R(24))), L, gain=0.03)
    return finish(T, impulse(6.0, 0.45, 0.06, seed=25), 0.45, target=-22)

def bed_cloister():
    # the cloister: the brothers chanting the Rule under the arches. Halfway through, one of them stops singing
    # the right note and slides down, slowly, and does not stop
    L = 64; T = Track(L, 12); r = R(31)
    continuous(T, lambda d: choir(hz('D2'), d, 'm', 1.0, 3, 0.01, 0.01, 0.004, 0.08, 0.9, seed=32), L, gain=0.55)
    chant = [('D3', 4), ('E3', 3), ('F3', 3), ('E3', 2), ('D3', 4), ('A2', 4), ('C3', 3), ('D3', 5),
             ('F3', 3), ('G3', 3), ('F3', 2), ('E3', 2), ('D3', 6), ('C3', 3), ('D3', 9)]
    t = 2.0
    for i, (name, d) in enumerate(chant):
        T.add(choir(hz(name), d + 1.2, 'o' if i % 3 else 'u', 1.0, 5, 0.6, 1.0, 0.006, 0.12, 0.9, seed=40 + i), t, 0.5)
        t += d
    # the one who goes wrong
    n = n_(26); f = hz('D3') * np.linspace(1.0, 0.94, n) ** 1.0
    v = additive(vibrato(f, n, 5.2, 0.009, 0.004, 9), n, list(vowel_gain('u', np.arange(1, 30) * hz('D3'), 0.9) / np.arange(1, 30) ** 0.6))
    T.add(pan(v * env(n, 3, 6), 0.55), 34, 0.09)
    for k in range(5):
        T.add(np.flip(whisper(r.uniform(1.2, 2.2), 1.0, seed=600 + k)), r.uniform(0, L), 0.05, r.choice([-0.95, 0.95]))
    continuous(T, lambda d: wind(d, 1.0, 700, 3, 0.2, 0.05, seed=33), L, gain=0.12)
    for k in range(12): T.add(drip(seed=700 + k), r.uniform(0, L), 0.2, r.uniform(-0.8, 0.8))
    return finish(T, impulse(6.5, 0.4, 0.05, seed=34), 0.55, target=-22)

def bed_choir():
    # the sunken choir: the organ still playing under the water, the hymn in it, the bells drowned
    L = 64; T = Track(L, 14); r = R(41)
    for i, chord in enumerate(HYMN_CHORDS):
        for name in chord:
            T.add(organ(hz(name), 9.5, 1.0, 1.0, seed=800 + i * 10 + len(name)), i * 8, 0.6)
    melody(T, HYMN[:4], 14, 3.0, lambda f, d: choir(f, d, 'a', 1.0, 6, 1.0, 1.6, 0.007, 0.1, 1.12, seed=int(f)), 0.32, 0, 1.25)
    for note, at, p in [('D3', 0, -0.4), ('A2', 32, 0.4), ('D3', 48, 0)]:
        T.add(bell(hz(note), 7, 1.0, 1.0, seed=int(at)), at, 0.4, p)
    for k in range(14):
        b = np.sin(2 * np.pi * np.cumsum(np.linspace(r.uniform(400, 700), r.uniform(900, 1600), n_(0.08))) / SR) * decay(n_(0.08), 0.03)
        T.add(b, r.uniform(0, L), 0.05, r.uniform(-1, 1))
    return finish(T, impulse(8.0, 0.3, 0.06, seed=42, bright=0.6), 0.55, target=-22, lowpass=2600)

def bed_ribvault():
    # inside the giant: the wind through its ribs, its breath, its heart still beating, a bone flute
    L = 64; T = Track(L, 12); r = R(51)
    continuous(T, lambda d: wind(d, 1.0, 420, 4, 0.18, 0.22, seed=52), L, gain=0.22)
    continuous(T, lambda d: wind(d, 1.0, 950, 6, 0.3, 0.12, seed=53), L, gain=0.1)
    breath = lambda d: lp(noise(n_(d), 'brown', R(54)), 300, 2) * np.sin(np.pi * (tt(n_(d)) % 16) / 16) ** 2
    continuous(T, breath, L, gain=0.35)
    for k in range(16): T.add(heartbeat(1.2, 0.34, 1.0, 33), k * 4.0, 0.55)
    continuous(T, lambda d: strings(hz('D2'), d, 1.0, 4, 0.01, 0.01, 1.4, 800, seed=55), L, gain=0.35)
    for name, at, d in [('D5', 9, 2.5), ('F5', 12, 2.0), ('A4', 14.5, 3.5), ('G4', 40, 2.0), ('F4', 42.5, 2.5), ('D4', 45.5, 4.0)]:
        T.add(flute(hz(name) * (1 + r.uniform(-0.012, 0.012)), d, 1.0, 0.7, seed=int(at)), at, 0.16, 0.35)
    for k in range(7): T.add(creak(r.uniform(0.4, 0.9), 1.0, r.uniform(500, 900), seed=900 + k), r.uniform(0, L), 0.2, r.uniform(-0.9, 0.9))
    return finish(T, impulse(7.0, 0.4, 0.07, seed=56), 0.5, target=-22)

def bed_gullet():
    # the throat: a sub-bass throb that swallows every six and a half seconds, wet, and the many throats in it
    L = 64; T = Track(L, 10); r = R(61)
    period = L / 10
    def throb(d):
        n = n_(d); t = tt(n)
        pulse = np.exp(-((t % period) - 0.8) ** 2 / 0.35)
        return np.sin(2 * np.pi * 33 * t) * (0.35 + 0.65 * pulse) + np.sin(2 * np.pi * 49.5 * t) * 0.3 * pulse
    continuous(T, throb, L, gain=0.6)
    for k in range(10):
        at = k * period
        sw = stft_filter(noise(n_(1.6), 'pink', R(1000 + k)), lambda ts, f: 1 / (1 + (3 * (f / (200 * 2 ** (ts * 1.3)) - (200 * 2 ** (ts * 1.3)) / np.maximum(f, 1))) ** 2))
        T.add(sw * swell(n_(1.6), 0.6, 1.0), at, 0.35, r.uniform(-0.3, 0.3))
        g = n_(0.35); gt = tt(g)
        gulp = np.sin(2 * np.pi * np.cumsum(320 * np.exp(-gt / 0.12) + 90) / SR) * np.exp(-gt / 0.1) + lp(noise(g, 'white', R(1100 + k)), 900) * np.exp(-gt / 0.05)
        T.add(gulp, at + 1.5, 0.4, r.uniform(-0.3, 0.3))
    for i, note in enumerate(['D2', 'Eb2', 'A1']):
        def throats(d, note=note, i=i):
            c = choir(hz(note), d, 'u', 1.0, 4, 0.01, 0.01, 0.015, 0.35, 0.8, seed=60 + i)
            bub = np.clip(smooth_noise(n_(d), 7, R(70 + i)), 0, 1)
            return c * (1 - 0.6 * bub)[:, None]
        continuous(T, throats, L, gain=0.28)
    for at, note in [(20, 'D6'), (44, 'Eb6')]:
        T.add(harmonics_scratch(hz(note), 7, 1.0, seed=at), at, 0.05)
    for k in range(30):
        q = n_(r.uniform(0.05, 0.2))
        sq = bp(noise(q, 'white', R(1200 + k)), r.uniform(700, 2200), 3) * swell(q, 0.2, 1.0)
        T.add(sq, r.uniform(0, L), 0.15, r.uniform(-1, 1))
    return finish(T, impulse(5.0, 0.2, 0.03, seed=62, bright=0.5), 0.5, target=-22, lowpass=3000)

def bed_skull():
    # the Skull: the lighthouse's clockwork still ticking, the wind outside the eye sockets, and the lullaby on a
    # music box that is running down
    L = 64; T = Track(L, 10); r = R(71)
    for k in range(64):
        if k % 9 == 8: continue                          # it misses a tick, now and then
        f = 3800 if k % 2 == 0 else 2600
        tick = hp(noise(n_(0.03), 'white', R(1300 + k)), f, 2) * decay(n_(0.03), 0.004)
        tick += np.sin(2 * np.pi * f * 0.31 * tt(n_(0.03))) * decay(n_(0.03), 0.006) * 0.4
        T.add(tick, k + r.uniform(-0.02, 0.02), 0.25, 0.15)
    for at in (15.5, 47.5):
        T.add(np.concatenate([hp(noise(n_(0.02), 'white', R(int(at) + j)), 3000) * decay(n_(0.02), 0.004) for j in range(12)]), at, 0.3, -0.2)   # the ratchet winding
    continuous(T, lambda d: wind(d, 1.0, 600, 3, 0.25, 0.3, seed=72), L, gain=0.15)
    # the box: the whole lullaby, then the second time through it slows and sinks
    melody(T, LULLABY, 10, 0.62, lambda f, d: tine(f, 2.2, 1.0, 0.0), 0.3, 0.3, 1.0)
    t = 10 + 24 * 0.62 + 2
    for i, (name, beats) in enumerate(LULLABY[:11]):
        slow = 1 + i * 0.09
        T.add(tine(hz(name) * (1 - i * 0.006), 2.2, 1.0, 0.6 + i * 0.1, seed=i), t, 0.28 * (1 - i * 0.05), 0.3)
        t += beats * 0.62 * slow
    for at in (0, 32): T.add(lp(bell(hz('D2'), 8, 1.0, 0.3, seed=int(at)), 1200), at, 0.4, -0.5)
    continuous(T, lambda d: strings(hz('D2'), d, 1.0, 4, 0.01, 0.01, 1.4, 900, seed=73), L, gain=0.3)
    T.add(strings(hz('F2'), 20, 1.0, 4, 6, 6, 1.4, 900, seed=74), 36, 0.25)
    return finish(T, impulse(4.0, 0.45, 0.03, seed=75), 0.45, target=-22)

def bed_outdoor():
    # the shore at night: low strings in the storm, the Saint far out under the reef, a foghorn from the mainland
    L = 64; T = Track(L, 12); r = R(81)
    continuous(T, lambda d: strings(hz('D2'), d, 1.0, 6, 0.01, 0.01, 1.4, 700, seed=82), L, gain=0.45)
    continuous(T, lambda d: strings(hz('A1'), d, 1.0, 4, 0.01, 0.01, 1.4, 600, seed=83), L, gain=0.3)
    T.add(tremolo_strings(hz('D3'), 18, 1.0, 9.0, attack=6, release=6, cutoff=1600, seed=84), 20, 0.2)
    T.add(tremolo_strings(hz('Eb3'), 14, 1.0, 9.5, attack=5, release=5, cutoff=1600, seed=85), 28, 0.12)
    T.add(lp(whale(39, 12, 1.0, (1.0, 1.4, 1.2, 0.55), seed=86), 500), 6, 0.6, 0.4)
    T.add(brass(hz('D2'), 8, 1.0, 0.5, seed=87), 44, 0.14, -0.7)
    return finish(T, impulse(5.0, 0.5, 0.04, seed=88), 0.35, target=-23)

# =====================================================================================================================
# the shared layers
# =====================================================================================================================
def dread():
    # something knows she is there: a semitone cluster trembling, glass harmonics scraping above it, a pitch that
    # rises forever, and a pulse like a heart that is not hers
    L = 48; T = Track(L, 10); r = R(91)
    for i, (note, at, d) in enumerate([('D3', 0, 30), ('Eb3', 8, 28), ('E3', 18, 26), ('D3', 26, 26), ('Eb3', 34, 22)]):
        T.add(tremolo_strings(hz(note), d, 1.0, 10 + i, attack=7, release=8, cutoff=2200, seed=1400 + i), at, 0.32, r.uniform(-0.6, 0.6))
    for note, at in [('A5', 6), ('Bb5', 22), ('A5', 38)]:
        T.add(harmonics_scratch(hz(note), 9, 1.0, seed=at), at, 0.07, r.uniform(-0.8, 0.8))
    continuous(T, lambda d: shepard(d, 1.0, 1 / 48, False, 55, 6), L, gain=0.16)
    for k in range(40): T.add(heartbeat(1.0, 0.3, 1.0, 44), k * 1.2, 0.25)
    for at in (0, 24): T.add(lp(drum(30, 4, 1.0, seed=at), 120), at, 0.6)
    for k in range(4): T.add(np.flip(whisper(r.uniform(1.0, 2.0), 1.0, seed=1500 + k)), r.uniform(0, L), 0.04, r.choice([-1, 1]))
    return finish(T, impulse(5.5, 0.4, 0.04, seed=92), 0.45, target=-22)

def chase():
    # it is coming: fists on the hull, the strings hacking at one note, steel screaming, brass in a cluster
    L = 30; T = Track(L, 8); r = R(101)
    beat = 60 / 96
    hits = [0, 1.5, 2.75, 3, 4.5, 5.5, 6, 7.25]     # an uneven two-bar pattern on the hull
    for bar in range(0, 24, 2):
        for h in hits:
            at = (bar * 4 + h) * beat / 2
            if at < L: T.add(lp(drum(52 if h in (0, 3) else 64, 1.2, 1.0, seed=int(at * 100)), 900), at, 0.7 if h in (0, 3) else 0.45, r.uniform(-0.2, 0.2))
    for k in range(48):
        at = k * beat
        note = ['D2', 'D2', 'Eb2', 'D2'][k % 4] if (k // 16) % 2 == 0 else ['D2', 'Eb2', 'E2', 'Eb2'][k % 4]
        T.add(strings(hz(note), 0.22, 1.0, 4, 0.004, 0.1, 1.1, 2400, seed=k), at, 0.5, -0.3)
        T.add(strings(hz(note) * 2, 0.18, 1.0, 3, 0.004, 0.08, 1.1, 3000, seed=k + 100), at + beat / 2, 0.25, 0.3)
    for at in (0, 7.5, 15, 22.5):
        T.add(bowed_metal(hz('D4') * r.uniform(0.98, 1.02), 3.5, 1.0, seed=int(at)), at + 2, 0.25, r.uniform(-0.8, 0.8))
    for at in (5, 20):
        for note in ('D2', 'Eb2', 'A2'):
            T.add(brass(hz(note), 3.2, 1.0, 0.25, seed=int(at) + len(note)), at, 0.3)
    continuous(T, lambda d: shepard(d, 1.0, 1 / 30, False, 110, 5), L, 3.0, gain=0.12)
    return finish(T, impulse(3.5, 0.5, 0.02, seed=102), 0.3, xf=3.0, target=-17)

def tide():
    # the tide's choir: the drowned singing under the water, louder as it rises, in waves
    L = 48; T = Track(L, 10)
    wave = lambda d: (0.45 + 0.55 * np.sin(np.pi * (tt(n_(d)) % 12) / 12) ** 2)
    for i, note in enumerate(['D2', 'A2', 'D3']):
        continuous(T, lambda d, note=note, i=i: choir(hz(note), d, 'o', 1.0, 5, 0.01, 0.01, 0.01, 0.15, 0.85, seed=1600 + i) * wave(d)[:, None], L, gain=0.5 - i * 0.12)
    continuous(T, lambda d: surf(d, 1.0, 12, seed=1610), L, gain=0.3)
    T.add(lp(whale(36, 14, 1.0, (0.8, 1.3, 1.6, 0.7), seed=1611), 400), 16, 0.5)
    return finish(T, impulse(7.0, 0.35, 0.05, seed=1612, bright=0.6), 0.5, target=-22, lowpass=2400)

def sanctuary():
    # a Bell Room: the only warm air on the island. A cello plays the lullaby, the bell still humming
    L = 64; T = Track(L, 12)
    continuous(T, lambda d: strings(hz('D3'), d, 1.0, 5, 0.01, 0.01, 1.7, 1400, seed=1700), L, gain=0.35)
    continuous(T, lambda d: strings(hz('A2'), d, 1.0, 4, 0.01, 0.01, 1.7, 1200, seed=1701), L, gain=0.3)
    cello = lambda f, d: strings(f * 0.5, d, 1.0, 1, 0.35, 0.8, 1.2, 2200, 0.007, seed=int(f))
    end = melody(T, LULLABY, 6, 1.15, cello, 0.55, -0.15, 1.05)
    T.add(strings(hz('F3'), 10, 1.0, 4, 3, 4, 1.7, 1400, seed=1702), end - 4, 0.25)
    for at in (0, 32): T.add(bell(hz('D4'), 9, 1.0, 0.1, seed=at), at, 0.25, 0.4)
    return finish(T, impulse(5.0, 0.5, 0.04, seed=1703), 0.45, target=-21)

def title():
    # the title: the sea at night; the lullaby on the music box, then on glass, then the drowned choir comes in under
    # it and the Saint answers from the reef
    L = 96; T = Track(L, 14); r = R(111)
    continuous(T, lambda d: surf(d, 1.0, 10, seed=1800), L, gain=0.35)
    continuous(T, lambda d: wind(d, 1.0, 400, 2, 0.15, 0.04, seed=1801), L, gain=0.12)
    melody(T, LULLABY, 4, 0.75, lambda f, d: tine(f, 2.4, 1.0, 0.05), 0.38, 0.2, 1.0)
    melody(T, LULLABY, 26, 0.85, lambda f, d: tine(f, 2.4, 1.0, 0.3), 0.3, 0.2, 1.0)
    melody(T, LULLABY, 26, 0.85, lambda f, d: glass(f, d, 1.0), 0.14, -0.25, 1.5)
    T.add(strings(hz('D2'), 40, 1.0, 6, 10, 10, 1.4, 900, seed=1802), 30, 0.35)
    for i, (chord, at) in enumerate([(['D2', 'A2', 'D3', 'F3'], 50), (['Bb1', 'F2', 'D3', 'F3'], 60), (['A1', 'E2', 'C#3', 'A3'], 70), (['D2', 'A2', 'D3', 'F3'], 80)]):
        for note in chord:
            T.add(choir(hz(note), 12, 'o', 1.0, 4, 3, 4, 0.008, 0.12, 0.9, seed=1900 + i * 7 + len(note)), at, 0.22)
    T.add(lp(whale(44, 12, 1.0, (1.0, 1.5, 1.2, 0.6), seed=1803), 700), 66, 0.5, 0.4)
    return finish(T, impulse(7.0, 0.35, 0.05, seed=1804), 0.45, target=-20)

# =====================================================================================================================
# stingers
# =====================================================================================================================
def rev_cymbal(dur=1.2, seed=0):
    n = n_(dur)
    return hp(noise(n, 'white', R(seed)), 3000, 2) * np.linspace(0, 1, n) ** 3

def sting_seen():
    # it has seen her: a breath drawn in backwards, then the strings and the hull struck together
    T = Track(7, 7)
    T.add(rev_cymbal(1.1, 1) * 0.5, 0, 0.5)
    hit = 1.1
    T.add(lp(drum(38, 3.5, 1.0, seed=2), 400), hit, 1.0)
    for i, note in enumerate(['D4', 'Eb4', 'E4', 'F4', 'D3']):
        T.add(tremolo_strings(hz(note), 2.6, 1.0, 13, attack=0.01, release=2.0, cutoff=4000, bright=1.0, seed=10 + i), hit, 0.35, (i - 2) * 0.3)
    T.add(brass(hz('D2'), 2.0, 1.0, 0.1, seed=3), hit, 0.5)
    T.add(harmonics_scratch(hz('Bb5'), 2.0, 1.0, seed=4), hit + 0.05, 0.25)
    return one_shot(T, impulse(4.0, 0.4, 0.02, seed=5), 0.45, -15)

def sting_scare():
    # something right beside her: a shriek of strings sliding up, steel slammed, and the floor dropping away
    T = Track(6, 6); n = n_(1.4)
    f = hz('D5') * 2 ** (np.linspace(0, 1, n) ** 0.5 * 1.1)
    shriek = additive(vibrato(f, n, 9, 0.02, 0.01, 1), n, [1, 0.6, 0.8, 0.4, 0.5, 0.3]) + bp(noise(n, 'white', R(2)), 2500, 1.5) * 1.2
    T.add(widen(shriek * env(n, 0.01, 0.9)), 0, 0.5)
    slam = bell(hz('D3') * 1.37, 2.5, 1.0, 0, seed=3) + lp(noise(n_(2.5), 'white', R(4)), 2500) * decay(n_(2.5), 0.08)
    T.add(slam, 0.0, 0.8)
    m = n_(2.0); sub = np.sin(2 * np.pi * np.cumsum(np.linspace(60, 25, m)) / SR) * decay(m, 0.8)
    T.add(sub, 0.02, 0.9)
    return one_shot(T, impulse(3.5, 0.4, 0.015, seed=6), 0.35, -13)

def sting_discover():
    # a thing that matters: a chord blooms out of glass and voices, and does not resolve
    T = Track(8, 8)
    for i, note in enumerate(['D5', 'F5', 'A5', 'C#6']):
        T.add(glass(hz(note), 5.5, 1.0, seed=i), i * 0.18, 0.3, (i - 1.5) * 0.4)
    for i, note in enumerate(['D3', 'A3', 'F4']):
        T.add(choir(hz(note), 5.5, 'a', 1.0, 4, 1.2, 3.0, 0.006, 0.1, 1.1, seed=20 + i), 0.2, 0.3)
    return one_shot(T, impulse(5.0, 0.3, 0.04, seed=7), 0.55, -18)

def sting_boss():
    # the great ones: three blows on the hull, the brass in a cluster, the choir screaming over it
    T = Track(8, 7)
    for k, at in enumerate((0, 0.45, 0.9)):
        T.add(lp(drum(36, 3.0, 1.0, seed=k), 500), at, 1.0)
    for note in ('D2', 'Eb2', 'A2', 'Bb2'):
        T.add(brass(hz(note), 3.6, 1.0, 0.2, seed=len(note)), 0.9, 0.4)
    for i, note in enumerate(['D5', 'Eb5', 'A5']):
        T.add(choir(hz(note), 3.2, 'a', 1.0, 5, 0.15, 2.0, 0.02, 0.2, 1.15, seed=30 + i), 0.95, 0.25)
    return one_shot(T, impulse(5.0, 0.4, 0.03, seed=8), 0.45, -14)

def sting_death():
    # drowned: the choir falls away beneath her, bubbles going up, and something very large closing
    T = Track(10, 8)
    for i, note in enumerate(['D3', 'F3', 'A3', 'D4']):
        n = n_(6.5); f = hz(note) * np.linspace(1, 0.82, n) ** 1.4
        c = additive(vibrato(f, n, 5, 0.008, 0.004, i), n, list(vowel_gain('o', np.arange(1, 40) * hz(note), 1.0) / np.arange(1, 40) ** 0.6))
        T.add(pan(c * env(n, 0.3, 3.5), (i - 1.5) * 0.4), 0.2, 0.2)
    for k in range(18):
        m = n_(0.1); b = np.sin(2 * np.pi * np.cumsum(np.linspace(500 + k * 40, 1400 + k * 60, m)) / SR) * decay(m, 0.03)
        T.add(b, 0.3 + k * 0.17 + R(k).uniform(0, 0.1), 0.12, R(k + 50).uniform(-0.7, 0.7))
    T.add(lp(drum(28, 5.0, 1.0, seed=9), 200), 0.0, 1.0)
    T.add(lp(whale(34, 6, 1.0, (1.2, 0.8, 0.5), seed=10), 300), 1.5, 0.6)
    return one_shot(T, impulse(6.0, 0.3, 0.05, seed=11, bright=0.5), 0.6, -15)

def sting_turn():
    # the tide turns toward High: the Saint drawing breath under the whole island
    T = Track(9, 7)
    T.add(lp(whale(30, 8, 1.0, (0.7, 1.0, 1.5), seed=12), 350), 0, 0.9)
    m = n_(7); T.add(lp(noise(m, 'brown', R(13)), 220) * swell(m, 0.7, 1.5), 0.5, 0.9)
    for note in ('D2', 'A2'):
        T.add(choir(hz(note), 7, 'o', 1.0, 5, 3, 3, 0.01, 0.2, 0.85, seed=len(note)), 0.8, 0.35)
    return one_shot(T, impulse(6.0, 0.3, 0.05, seed=14, bright=0.5), 0.5, -17)

# =====================================================================================================================
# ambience beds (60 s): the rooms, with no music in them
# =====================================================================================================================
def amb_wrackline():
    L = 60; T = Track(L, 6); r = R(201)
    continuous(T, lambda d: surf(d, 1.0, 9, seed=202), L, gain=0.5)
    continuous(T, lambda d: bp(noise(n_(d), 'pink', R(203)), 420, 1.5) * (0.5 + 0.5 * smooth_noise(n_(d), 0.7, R(204))), L, gain=0.15)
    for k in range(26): T.add(drip(seed=2100 + k), r.uniform(0, L), r.uniform(0.15, 0.4), r.uniform(-1, 1))
    for k in range(4): T.add(creak(r.uniform(1, 2), 1.0, seed=2200 + k), r.uniform(0, L), 0.3, r.uniform(-1, 1))
    return finish(T, impulse(3.5, 0.4, 0.03, seed=205), 0.4, target=-24)

def amb_saltworks():
    L = 60; T = Track(L, 5); r = R(211)
    continuous(T, lambda d: hp(noise(n_(d), 'white', R(212)), 6000, 2) * (0.6 + 0.4 * smooth_noise(n_(d), 0.4, R(213))), L, gain=0.05)
    continuous(T, lambda d: (sine(50, n_(d)) + 0.5 * sine(100, n_(d)) + 0.25 * sine(150, n_(d))) * 0.3, L, gain=0.1)
    for k in range(60):
        tick = hp(noise(n_(0.02), 'white', R(2300 + k)), r.uniform(2500, 6000), 2) * decay(n_(0.02), 0.003)
        T.add(tick, r.uniform(0, L), r.uniform(0.05, 0.2), r.uniform(-1, 1))
    for k in range(3):
        ch = np.concatenate([bp(noise(n_(0.04), 'white', R(2400 + k * 20 + j)), r.uniform(2000, 4000), 4) * decay(n_(0.04), 0.01) for j in range(r.integers(8, 20))])
        T.add(ch, r.uniform(0, L), 0.25, r.uniform(-1, 1))
    for k in range(10): T.add(drip(seed=2500 + k), r.uniform(0, L), 0.15, r.uniform(-1, 1))
    return finish(T, impulse(4.5, 0.45, 0.04, seed=214), 0.4, target=-26)

def amb_cloister():
    L = 60; T = Track(L, 6); r = R(221)
    continuous(T, lambda d: wind(d, 1.0, 800, 2, 0.25, 0.05, seed=222), L, gain=0.35)
    continuous(T, lambda d: bp(noise(n_(d), 'white', R(223)), 3000, 1.2) * (0.4 + 0.6 * np.abs(smooth_noise(n_(d), 3, R(224)))), L, gain=0.05)
    for k in range(18): T.add(drip(seed=2600 + k), r.uniform(0, L), r.uniform(0.15, 0.35), r.uniform(-1, 1))
    return finish(T, impulse(4.5, 0.4, 0.04, seed=225), 0.45, target=-25)

def amb_choir():
    L = 60; T = Track(L, 7); r = R(231)
    continuous(T, lambda d: lp(noise(n_(d), 'brown', R(232)), 260, 2) * (0.6 + 0.4 * smooth_noise(n_(d), 0.2, R(233))), L, gain=0.5)
    continuous(T, lambda d: (sine(41, n_(d)) * 0.5 + sine(61.5, n_(d)) * 0.2), L, gain=0.12)
    for k in range(30):
        pl = drip(r.uniform(400, 900), 1.0, seed=2700 + k)
        T.add(pl, r.uniform(0, L), r.uniform(0.15, 0.3), r.uniform(-1, 1))
    return finish(T, impulse(7.0, 0.3, 0.06, seed=234, bright=0.6), 0.55, target=-25)

def amb_ribvault():
    L = 60; T = Track(L, 5); r = R(241)
    continuous(T, lambda d: wind(d, 1.0, 380, 2.5, 0.35, 0.2, seed=242), L, gain=0.45)
    for k in range(10):
        q = n_(r.uniform(0.3, 1.2)); dust = bp(noise(q, 'white', R(2800 + k)), 2500, 0.8) * swell(q, 0.2, 1.0)
        T.add(dust, r.uniform(0, L), 0.08, r.uniform(-1, 1))
    for k in range(8): T.add(creak(r.uniform(0.4, 1.0), 1.0, r.uniform(450, 900), seed=2900 + k), r.uniform(0, L), 0.25, r.uniform(-1, 1))
    return finish(T, impulse(5.0, 0.45, 0.05, seed=243), 0.4, target=-24)

def amb_gullet():
    L = 60; T = Track(L, 5); r = R(251)
    br = lambda d: lp(noise(n_(d), 'pink', R(252)), 700, 2) * (np.sin(np.pi * (tt(n_(d)) % 7.5) / 7.5) ** 3)
    continuous(T, br, L, gain=0.4)
    for k in range(24): T.add(drip(r.uniform(260, 480), 1.0, seed=3000 + k), r.uniform(0, L), r.uniform(0.2, 0.4), r.uniform(-1, 1))
    for k in range(16):
        m = n_(0.06); b = np.sin(2 * np.pi * np.cumsum(np.linspace(r.uniform(150, 260), r.uniform(300, 500), m)) / SR) * decay(m, 0.02)
        T.add(b, r.uniform(0, L), 0.25, r.uniform(-1, 1))
    return finish(T, impulse(3.0, 0.2, 0.02, seed=253, bright=0.5), 0.45, target=-24)

def amb_skull():
    L = 60; T = Track(L, 5); r = R(261)
    continuous(T, lambda d: wind(d, 1.0, 500, 3, 0.3, 0.35, seed=262), L, gain=0.45)
    continuous(T, lambda d: surf(d, 1.0, 11, seed=263), L, gain=0.15)
    for k in range(5): T.add(creak(r.uniform(1, 2), 1.0, seed=3100 + k), r.uniform(0, L), 0.3, r.uniform(-1, 1))
    return finish(T, impulse(3.0, 0.45, 0.03, seed=264), 0.35, target=-24)

def amb_outdoor():
    L = 60; T = Track(L, 6); r = R(271)
    continuous(T, lambda d: wind(d, 1.0, 340, 1.2, 0.4, 0.06, seed=272), L, gain=0.5)
    continuous(T, lambda d: surf(d, 1.0, 7, seed=273), L, gain=0.6)
    def rain(d):
        n = n_(d); x = hp(noise(n, 'white', R(274)), 3500, 2) * 0.5
        drops = (R(275).random(n) > 0.9985) * R(276).uniform(0.2, 1.0, n)
        return x + hp(drops, 1500, 1) * 0.6
    continuous(T, rain, L, gain=0.18)
    for at in (14, 41):
        m = n_(6); th = lp(noise(m, 'brown', R(int(at))), 160, 2) * (decay(m, 1.6) * np.clip(tt(m) / 0.3, 0, 1))
        T.add(th, at, 0.8, r.uniform(-0.6, 0.6))
    return finish(T, impulse(2.5, 0.5, 0.02, seed=277), 0.25, target=-22)


CUES = {
    'score/bed_wrackline': bed_wrackline, 'score/bed_saltworks': bed_saltworks, 'score/bed_cloister': bed_cloister,
    'score/bed_choir': bed_choir, 'score/bed_ribvault': bed_ribvault, 'score/bed_gullet': bed_gullet,
    'score/bed_skull': bed_skull, 'score/bed_outdoor': bed_outdoor,
    'score/dread': dread, 'score/chase': chase, 'score/tide': tide, 'score/sanctuary': sanctuary, 'score/title': title,
    'sting/seen': sting_seen, 'sting/scare': sting_scare, 'sting/discover': sting_discover, 'sting/boss': sting_boss,
    'sting/death': sting_death, 'sting/turn': sting_turn,
    'amb/wrackline': amb_wrackline, 'amb/saltworks': amb_saltworks, 'amb/cloister': amb_cloister, 'amb/choir': amb_choir,
    'amb/ribvault': amb_ribvault, 'amb/gullet': amb_gullet, 'amb/skull': amb_skull, 'amb/outdoor': amb_outdoor,
}

def encode(x, path, quality=3):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with tempfile.NamedTemporaryFile(suffix='.f32', delete=False) as f:
        f.write(np.ascontiguousarray(x, np.float32).tobytes()); raw = f.name
    subprocess.run(['ffmpeg', '-loglevel', 'error', '-y', '-f', 'f32le', '-ar', str(SR), '-ac', '2', '-i', raw,
                    '-c:a', 'libvorbis', '-q:a', str(quality), path], check=True)
    os.unlink(raw)

if __name__ == '__main__':
    args = sys.argv[1:]
    reel = None
    if '--reel' in args:
        i = args.index('--reel'); reel = args[i + 1]; args = args[:i] + args[i + 2:]
    names = args or list(CUES)
    excerpts = []
    for name in names:
        t0 = time.time()
        x = CUES[name]()
        q = 2 if name.startswith('amb/') else 3
        encode(x, os.path.join(ROOT, name + '.ogg'), q)
        print(f'{name}: {len(x) / SR:.1f} s, rms {db(rms(x)):.1f} dB, peak {db(float(np.max(np.abs(x)))):.1f} dB, {time.time() - t0:.1f} s')
        if reel: excerpts.append((name, x[: n_(24)] if not name.startswith('sting/') else x))
    if reel:
        gap = np.zeros((n_(1.0), 2), np.float32)
        allx = np.concatenate(sum([[x, gap] for _, x in excerpts], []))
        with tempfile.NamedTemporaryFile(suffix='.f32', delete=False) as f:
            f.write(np.ascontiguousarray(allx, np.float32).tobytes()); raw = f.name
        subprocess.run(['ffmpeg', '-loglevel', 'error', '-y', '-f', 'f32le', '-ar', str(SR), '-ac', '2', '-i', raw, '-c:a', 'libmp3lame', '-q:a', '4', reel], check=True)
        os.unlink(raw)
        print('reel:', reel, ' '.join(n for n, _ in excerpts))
