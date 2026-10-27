# The score's toolkit: oscillators, noise, filters, reverb and the instruments the score is played on, all in
# numpy so a minute of stereo renders in seconds. Every instrument returns a float32 array (mono, or (n, 2) stereo).
import math
import numpy as np

SR = 44100
RNG = np.random.default_rng(7)

NOTES = {'C': -9, 'C#': -8, 'Db': -8, 'D': -7, 'D#': -6, 'Eb': -6, 'E': -5, 'F': -4, 'F#': -3, 'Gb': -3,
         'G': -2, 'G#': -1, 'Ab': -1, 'A': 0, 'A#': 1, 'Bb': 1, 'B': 2}
def hz(name):
    """'D2' -> 73.42. A4 = 440."""
    i = 2 if len(name) > 2 and name[1] in '#b' else 1
    return 440.0 * 2 ** ((NOTES[name[:i]] + (int(name[i:]) - 4) * 12) / 12)

def n_(sec): return int(round(sec * SR))
def tt(n): return np.arange(n, dtype=np.float64) / SR

# ---- envelopes ---------------------------------------------------------------------------------------------------
def env(n, a=0.01, r=0.1, curve=1.0):
    """attack, hold, release (seconds); the release ends at the end of the buffer"""
    e = np.ones(n, np.float64)
    na, nr = max(1, n_(a)), max(1, n_(r))
    na = min(na, n); nr = min(nr, n)
    e[:na] = np.linspace(0, 1, na) ** curve
    e[n - nr:] *= np.linspace(1, 0, nr) ** curve
    return e

def swell(n, peak=0.5, curve=2.0):
    """rise to a peak at `peak` (fraction of the length), fall away"""
    x = np.linspace(0, 1, n)
    up = np.clip(x / peak, 0, 1); dn = np.clip((1 - x) / (1 - peak), 0, 1)
    return (np.minimum(up, dn) ** curve)

def decay(n, tau):
    return np.exp(-tt(n) / tau)

# ---- oscillators -------------------------------------------------------------------------------------------------
def phase(freq, n):
    """freq: number or array (Hz) -> phase in cycles"""
    f = np.broadcast_to(np.asarray(freq, np.float64), (n,))
    return np.cumsum(f) / SR

def sine(freq, n, ph0=0.0):
    return np.sin(2 * np.pi * (phase(freq, n) + ph0))

def additive(freq, n, amps, ph=None):
    """harmonics with the given amplitudes, band-limited below Nyquist"""
    p = phase(freq, n) if ph is None else ph
    f0 = float(np.max(np.asarray(freq)))
    out = np.zeros(n)
    for k, a in enumerate(amps, 1):
        if a == 0 or k * f0 > SR * 0.45: continue
        out += a * np.sin(2 * np.pi * k * p + k * 1.3)
    return out

def saw(freq, n, nh=None, bright=1.0):
    f0 = float(np.max(np.asarray(freq)))
    nh = nh or max(1, min(60, int(SR * 0.45 / max(f0, 1))))
    return additive(freq, n, [((-1) ** (k + 1)) / k ** bright for k in range(1, nh + 1)])

def vibrato(f, n, rate=5.0, depth=0.004, drift=0.002, seed=None):
    r = np.random.default_rng(seed)
    t = tt(n)
    wob = depth * np.sin(2 * np.pi * rate * t + r.random() * 6) + drift * smooth_noise(n, 0.7, r)
    return f * (1 + wob)

def smooth_noise(n, hz_=1.0, r=None):
    """a slow random wander in -1..1, about hz_ changes a second"""
    r = r or RNG
    k = max(2, int(n / SR * hz_) + 3)
    pts = r.uniform(-1, 1, k)
    x = np.linspace(0, k - 3, n)
    i = np.floor(x).astype(int); f = x - i
    f = f * f * (3 - 2 * f)
    return pts[i] * (1 - f) + pts[i + 1] * f

def noise(n, color='white', r=None):
    r = r or RNG
    w = r.standard_normal(n)
    if color == 'white': return w / 3
    W = np.fft.rfft(w); f = np.fft.rfftfreq(n, 1 / SR); f[0] = f[1]
    W *= 1 / np.sqrt(f) if color == 'pink' else 1 / f
    x = np.fft.irfft(W, n)
    return x / (np.std(x) * 3 + 1e-9)

# ---- filters (in frequency: fast, exact, for whole buffers) -------------------------------------------------------
def fft_filter(x, H):
    """H(freqs) -> gain; applied to a mono or stereo buffer"""
    if x.ndim == 2: return np.stack([fft_filter(x[:, c], H) for c in range(x.shape[1])], -1)
    n = len(x); X = np.fft.rfft(x); f = np.fft.rfftfreq(n, 1 / SR)
    return np.fft.irfft(X * H(f), n)

def lp(x, fc, order=2): return fft_filter(x, lambda f: 1 / np.sqrt(1 + (f / fc) ** (2 * order)))
def hp(x, fc, order=2): return fft_filter(x, lambda f: 1 / np.sqrt(1 + (fc / np.maximum(f, 1e-3)) ** (2 * order)))
def bp(x, fc, q=2.0): return fft_filter(x, lambda f: 1 / (1 + (q * (f / fc - fc / np.maximum(f, 1e-3))) ** 2))

def resonances(f, peaks):
    """sum of resonant peaks (centre, bandwidth, gain) as a gain curve"""
    g = np.zeros_like(f)
    for c, bw, a in peaks: g += a / (1 + ((f - c) / (bw / 2)) ** 2)
    return g

def stft_filter(x, gain, frame=2048, hop=512):
    """time-varying filter: gain(t_seconds, freqs) -> gains, frame by frame, overlap-added"""
    if x.ndim == 2: return np.stack([stft_filter(x[:, c], gain, frame, hop) for c in range(2)], -1)
    n = len(x); win = np.hanning(frame)
    out = np.zeros(n + frame); norm = np.zeros(n + frame)
    xp = np.concatenate([x, np.zeros(frame)])
    f = np.fft.rfftfreq(frame, 1 / SR)
    for s in range(0, n, hop):
        seg = xp[s:s + frame] * win
        Y = np.fft.irfft(np.fft.rfft(seg) * gain((s + frame / 2) / SR, f), frame)
        out[s:s + frame] += Y * win; norm[s:s + frame] += win * win
    return (out / np.maximum(norm, 1e-6))[:n]

# vowels as formant peaks (centre, bandwidth, gain)
VOWELS = {
    'a': [(730, 90, 1.0), (1090, 110, 0.5), (2440, 160, 0.25), (3400, 250, 0.12)],
    'o': [(570, 80, 1.0), (840, 100, 0.45), (2410, 160, 0.15), (3300, 250, 0.08)],
    'u': [(300, 60, 1.0), (870, 90, 0.3), (2240, 150, 0.1), (3300, 250, 0.05)],
    'e': [(530, 80, 1.0), (1840, 120, 0.5), (2480, 160, 0.3), (3500, 250, 0.12)],
    'i': [(270, 60, 1.0), (2290, 130, 0.45), (3010, 170, 0.35), (3700, 250, 0.15)],
    'm': [(250, 60, 1.0), (900, 200, 0.15), (2200, 300, 0.05)],
    'uh': [(520, 90, 1.0), (1190, 110, 0.4), (2390, 160, 0.2), (3300, 250, 0.08)],
}
def vowel_gain(v, f, shift=1.0):
    return resonances(f, [(c * shift, bw * shift, a) for c, bw, a in VOWELS[v]])

# ---- space ----------------------------------------------------------------------------------------------------
def impulse(seconds=4.0, damp=0.5, predelay=0.03, early=8, width=1.0, seed=3, bright=1.0):
    """a stereo room: early reflections, then a dense tail whose highs die first"""
    r = np.random.default_rng(seed)
    n = n_(seconds)
    t = tt(n)
    ir = np.zeros((n, 2))
    # the tail, band by band, the high bands dying fastest
    bands = [(0, 250, 1.0), (250, 800, 0.85), (800, 2500, 0.6 * bright), (2500, 7000, 0.35 * bright), (7000, 20000, 0.15 * bright)]
    for c in range(2):
        w = r.standard_normal(n)
        W = np.fft.rfft(w); f = np.fft.rfftfreq(n, 1 / SR)
        tail = np.zeros(n)
        for lo, hi, life in bands:
            m = (f >= lo) & (f < hi)
            b = np.fft.irfft(W * m, n)
            tau = seconds / 6.9 * life * (1 - damp * 0.5)
            tail += b * np.exp(-t / tau)
        tail *= np.clip(t / 0.06, 0, 1)
        ir[:, c] = tail
    ir /= np.max(np.abs(ir)) + 1e-9
    ir *= 0.5
    for k in range(early):
        d = predelay + r.uniform(0.004, 0.09)
        i = n_(d)
        if i < n: ir[i, k % 2] += r.uniform(0.3, 0.7) * (1 - k / early)
    pd = n_(predelay)
    ir = np.concatenate([np.zeros((pd, 2)), ir])[:n]
    # width: blend toward mono
    m = ir.mean(1, keepdims=True)
    return m + (ir - m) * width

def convolve(x, ir):
    """stereo x (n,2) or mono, with a stereo impulse; output keeps len(x) + len(ir)"""
    if x.ndim == 1: x = np.stack([x, x], -1)
    n = len(x) + len(ir)
    N = 1 << (n - 1).bit_length()
    out = np.zeros((n, 2))
    for c in range(2):
        out[:, c] = np.fft.irfft(np.fft.rfft(x[:, c], N) * np.fft.rfft(ir[:, c], N), N)[:n]
    return out

def reverb(x, ir, wet=0.35, dry=1.0):
    if x.ndim == 1: x = np.stack([x, x], -1)
    w = convolve(x, ir)
    w *= np.sqrt(np.mean(x ** 2) + 1e-12) / (np.sqrt(np.mean(w[:len(x)] ** 2)) + 1e-12)
    out = w * wet
    out[:len(x)] += x * dry
    return out

# ---- placing and mixing ------------------------------------------------------------------------------------------
def pan(x, p=0.0):
    a = (np.clip(p, -1, 1) + 1) * np.pi / 4
    return np.stack([x * np.cos(a), x * np.sin(a)], -1)

def widen(x, amt=0.6, delay_ms=11.0):
    """a mono or narrow sound made wide: a short delay on one side, mixed back by amt"""
    if x.ndim == 1: x = np.stack([x, x], -1)
    d = n_(delay_ms / 1000)
    out = x.copy()
    out[d:, 1] = x[d:, 1] * (1 - amt * 0.5) + x[:-d, 0] * amt * 0.5
    return out

class Track:
    """a stereo bus of a given length; events are added at times, then the whole is finished"""
    def __init__(self, seconds, tail=8.0):
        self.L = n_(seconds); self.T = n_(tail)
        self.buf = np.zeros((self.L + self.T, 2))
    def add(self, x, at=0.0, gain=1.0, p=0.0):
        if x.ndim == 1: x = pan(x, p)
        i = n_(at)
        if i < 0: i += self.L          # just before the start of a loop is the end of it
        j = min(len(self.buf), i + len(x))
        if i < len(self.buf): self.buf[i:j] += x[:j - i] * gain
        # whatever runs past the tail wraps to the start (loops)
        over = i + len(x) - len(self.buf)
        if over > 0: self.buf[:over] += x[len(x) - over:] * gain
    def loop(self, xfade=3.0):
        """fold the tail onto the head so the buffer loops without a seam"""
        L, T = self.L, self.T
        out = self.buf[:L].copy()
        out[:T] += self.buf[L:L + T]
        return out

def rms(x): return float(np.sqrt(np.mean(np.asarray(x, np.float64) ** 2)) + 1e-12)
def db(x): return 20 * math.log10(x + 1e-12)

def master(x, target_db=-20.0, ceiling=0.89):
    """cut what no speaker can play, level to a loudness, then a soft limiter keeps the peaks under the ceiling"""
    x = hp(x, 30, 3)
    x = x * (10 ** (target_db / 20) / rms(x))
    pk = np.max(np.abs(x))
    if pk > ceiling * 0.7:
        k = ceiling * 0.7
        over = np.abs(x) > k
        x = np.where(over, np.sign(x) * (k + (ceiling - k) * np.tanh((np.abs(x) - k) / (ceiling - k))), x)
    return x.astype(np.float32)


# ===================================================================================================================
# the instruments
# ===================================================================================================================
def strings(freq, dur, vol=1.0, players=5, attack=1.5, release=2.0, bright=1.3, cutoff=2600, vib=0.004, seed=0):
    """a section of bowed strings: detuned saws, each with its own vibrato, through a dark body"""
    n = n_(dur); r = np.random.default_rng(seed)
    out = np.zeros((n, 2))
    for k in range(players):
        f = vibrato(freq * (1 + r.uniform(-0.006, 0.006)), n, r.uniform(4.6, 5.8), vib * r.uniform(0.7, 1.3), 0.0015, r.integers(1 << 30))
        s = saw(f, n, bright=bright)
        out += pan(s, r.uniform(-0.7, 0.7))
    out = lp(out, cutoff, 2)
    # the bow on the string
    bow = bp(noise(n, 'pink', r), min(4000, freq * 6), 1.5) * 0.15
    out += pan(bow, 0) * 1
    return out * env(n, attack, release, 1.5)[:, None] * vol / players

def tremolo_strings(freq, dur, vol=1.0, rate=11.0, **kw):
    s = strings(freq, dur, vol, **kw)
    n = len(s); t = tt(n)
    trem = 0.55 + 0.45 * np.sign(np.sin(2 * np.pi * rate * t + np.sin(t * 3)))
    return s * lp(trem, 60, 1)[:, None]

def harmonics_scratch(freq, dur, vol=1.0, seed=0):
    """sul ponticello harmonics: glassy, scratchy, high"""
    n = n_(dur); r = np.random.default_rng(seed)
    f = vibrato(freq, n, 6.5, 0.006, 0.003, seed)
    s = additive(f, n, [0.3, 0.2, 1.0, 0.6, 0.8, 0.3, 0.5, 0.2])
    scr = bp(noise(n, 'white', r), freq * 3, 3) * 0.6 * (0.5 + 0.5 * smooth_noise(n, 9, r))
    return pan((s * 0.5 + scr) * env(n, 0.6, 1.2), r.uniform(-0.5, 0.5)) * vol

def choir(freq, dur, vowel='a', vol=1.0, voices=6, attack=2.0, release=2.5, spread=0.009, breath=0.15, shift=1.0, seed=0, vowel_to=None):
    """voices singing a vowel: a glottal pulse train, every harmonic weighted by the vowel's formants"""
    n = n_(dur); r = np.random.default_rng(seed)
    out = np.zeros((n, 2))
    for k in range(voices):
        f0 = freq * (1 + r.uniform(-spread, spread))
        f = vibrato(f0, n, r.uniform(4.8, 5.8), 0.006, 0.003, r.integers(1 << 30))
        nh = max(1, min(48, int(5200 / f0)))
        fh = np.arange(1, nh + 1) * f0
        g = vowel_gain(vowel, fh, shift) / np.arange(1, nh + 1) ** 0.6
        s = additive(f, n, list(g))
        if vowel_to:
            g2 = vowel_gain(vowel_to, fh, shift) / np.arange(1, nh + 1) ** 0.6
            s2 = additive(f, n, list(g2))
            m = np.linspace(0, 1, n) ** 1.5
            s = s * (1 - m) + s2 * m
        out += pan(s, r.uniform(-0.8, 0.8))
    # breath through the vowel
    br = fft_filter(noise(n, 'white', r), lambda fq: vowel_gain(vowel, fq, shift) + 0.02) * breath * voices * 0.4
    out += pan(br, 0)
    out = hp(out, 70, 2)
    return out * env(n, attack, release, 1.6)[:, None] * vol / voices

def organ(freq, dur, vol=1.0, drowned=1.0, seed=0):
    """a pipe organ (16', 8', 4', 2 2/3'); drowned: heard through water, the pipes beating against each other"""
    n = n_(dur); r = np.random.default_rng(seed)
    out = np.zeros((n, 2))
    for ratio, a in [(0.5, 0.7), (1, 1.0), (2, 0.5), (3, 0.25), (4, 0.18)]:
        for side in (-0.4, 0.4):
            f = freq * ratio * (1 + r.uniform(-0.002, 0.002) * (1 + 2 * drowned))
            wob = 1 + drowned * 0.004 * np.sin(2 * np.pi * r.uniform(0.2, 0.5) * tt(n) + r.random() * 6)
            s = additive(f * wob, n, [1, 0.45, 0.25, 0.12, 0.08, 0.04])
            out += pan(s * a, side)
    out = lp(out, 4000 - 3200 * drowned, 2)
    chiff = bp(noise(n, 'white', r), freq * 4, 2) * env(n, 0.005, n / SR - 0.06) * decay(n, 0.08) * 0.3
    out += pan(chiff, 0)
    return out * env(n, 0.35 + drowned * 0.6, 1.8)[:, None] * vol * 0.15

def bell(freq, dur, vol=1.0, drowned=0.0, seed=0):
    """a cast bell: hum, prime, tierce, quint, nominal and the strike"""
    n = n_(dur); r = np.random.default_rng(seed)
    t = tt(n); out = np.zeros(n)
    for ratio, a, life in [(0.5, 0.8, 1.0), (1.0, 1.0, 0.6), (1.183, 0.6, 0.45), (1.506, 0.35, 0.35), (2.0, 0.5, 0.3), (2.514, 0.25, 0.2), (2.662, 0.2, 0.18), (3.011, 0.15, 0.12), (4.166, 0.1, 0.08)]:
        f = freq * ratio * (1 + r.uniform(-0.001, 0.001))
        beat = 1 + 0.25 * np.sin(2 * np.pi * r.uniform(0.5, 2.0) * t)
        out += a * np.sin(2 * np.pi * f * t) * np.exp(-t / (dur * life * 0.5)) * beat
    strike = hp(noise(n, 'white', r), 2000, 1) * decay(n, 0.01) * 0.8
    out = out + strike
    if drowned > 0:
        out = lp(out, 2400 - 1900 * drowned, 2)
        out *= 1 + drowned * 0.3 * np.sin(2 * np.pi * 3.3 * t)
    return out * vol * 0.25

def tine(freq, dur=2.0, vol=1.0, wow=0.0, seed=0):
    """a music box tine: the note, the bar's high partials; wow: the mechanism running down"""
    n = n_(dur); r = np.random.default_rng(seed)
    t = tt(n)
    f = freq * (1 + wow * smooth_noise(n, 1.3, r) * 0.03 - wow * 0.02 * t / dur)
    p = phase(f, n)
    s = np.sin(2 * np.pi * p) + 0.25 * np.sin(2 * np.pi * 5.4 * p) * np.exp(-t * 18) + 0.12 * np.sin(2 * np.pi * 2.76 * p) * np.exp(-t * 8)
    click = hp(noise(n, 'white', r), 4000, 1) * decay(n, 0.003) * 0.3
    return (s * np.exp(-t * 4.5 / dur) * np.clip(t / 0.002, 0, 1) + click) * vol

def glass(freq, dur, vol=1.0, seed=0):
    """a glass harmonica: a wet finger on a rim, swelling in, beating softly"""
    n = n_(dur); r = np.random.default_rng(seed)
    f = vibrato(freq, n, 0.0, 0.0, 0.0015, seed)
    s = additive(f, n, [1.0, 0.0, 0.12, 0.0, 0.04])
    s2 = sine(f * 1.0018, n)
    return (s + s2 * 0.5) * swell(n, 0.35, 1.6) * vol * 0.4

def flute(freq, dur, vol=1.0, breath=0.5, seed=0):
    """a bone flute: a breathy, hollow tone that wavers"""
    n = n_(dur); r = np.random.default_rng(seed)
    f = vibrato(freq, n, 4.2, 0.008, 0.006, seed)
    tone = additive(f, n, [1.0, 0.08, 0.2, 0.03])
    air = bp(noise(n, 'white', r), freq * 2, 2.5) * breath
    return (tone + air) * env(n, 0.25, 0.6, 1.4) * vol * 0.5

def bowed_metal(freq, dur, vol=1.0, seed=0):
    """a bowed steel plate or a cable under strain: inharmonic partials, slow beating, a grind under it"""
    n = n_(dur); r = np.random.default_rng(seed)
    t = tt(n); out = np.zeros(n)
    for ratio in [1.0, 1.593, 2.136, 2.296, 2.653, 2.918, 3.156, 3.501, 4.15]:
        a = r.uniform(0.2, 1.0) / ratio ** 0.7
        f = freq * ratio * (1 + 0.003 * smooth_noise(n, 0.4, r))
        out += a * np.sin(2 * np.pi * np.cumsum(f) / SR) * (0.5 + 0.5 * smooth_noise(n, r.uniform(0.2, 1.5), r))
    grind = bp(noise(n, 'pink', r), freq * 2.5, 4) * 0.3 * (0.5 + 0.5 * smooth_noise(n, 6, r))
    return (out + grind) * swell(n, r.uniform(0.3, 0.6), 1.5) * vol * 0.25

def brass(freq, dur, vol=1.0, rise=0.4, seed=0):
    """low brass: saws whose filter opens as the note swells, a blat at the front"""
    n = n_(dur); r = np.random.default_rng(seed)
    out = np.zeros((n, 2))
    for k in range(3):
        f = vibrato(freq * (1 + r.uniform(-0.004, 0.004)), n, 5.0, 0.003, 0.002, r.integers(1 << 30))
        out += pan(saw(f, n, bright=1.0), r.uniform(-0.5, 0.5))
    e = swell(n, rise, 1.2)
    out = stft_filter(out, lambda ts, fq: 1 / np.sqrt(1 + (fq / (200 + 2600 * float(np.interp(ts, np.linspace(0, dur, n), e)))) ** 4))
    return out * e[:, None] * vol / 3

def drum(freq=55, dur=2.0, vol=1.0, seed=0):
    """a great low drum, or a fist on a hull: a falling thump and the skin's slap"""
    n = n_(dur); r = np.random.default_rng(seed)
    t = tt(n)
    f = freq * (1 + 1.2 * np.exp(-t / 0.03))
    body = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t / (dur * 0.25))
    slap = lp(noise(n, 'white', r), 1800, 2) * np.exp(-t / 0.025) * 0.6
    return (body + slap) * vol

def heartbeat(dur=1.0, bpm_gap=0.32, vol=1.0, low=40.0, seed=0):
    """lub-dub: two soft, deep thumps"""
    n = n_(dur)
    out = np.zeros(n)
    for at, f, a in [(0.0, low, 1.0), (bpm_gap, low * 1.2, 0.7)]:
        i = n_(at); m = n - i
        t = tt(m)
        out[i:] += a * np.sin(2 * np.pi * np.cumsum(f * (1 + 0.6 * np.exp(-t / 0.02))) / SR) * np.exp(-t / 0.09) * np.clip(t / 0.004, 0, 1)
    return lp(out, 140, 2) * vol

def creak(dur=1.6, vol=1.0, base=None, seed=0):
    """timber under strain: stick and slip, the wood's modes ringing at every slip"""
    r = np.random.default_rng(seed)
    n = n_(dur)
    rate = 18 + 90 * np.abs(smooth_noise(n, r.uniform(1.0, 3.0), r)) * swell(n, r.uniform(0.3, 0.7), 1.0)
    ph = np.cumsum(rate / SR)
    imp = np.zeros(n)
    idx = np.nonzero(np.diff(np.floor(ph)) > 0)[0]
    imp[idx] = r.uniform(0.4, 1.0, len(idx))
    base = base or r.uniform(150, 320)
    m = n_(0.08); t = tt(m)
    modes = sum(a * np.sin(2 * np.pi * base * k * t) * np.exp(-t / (0.03 / k ** 0.5)) for k, a in [(1, 1.0), (2.37, 0.6), (4.1, 0.35), (6.3, 0.2)])
    out = np.convolve(imp, modes)[:n]
    return out * swell(n, 0.4, 0.6) * vol * 0.2

def whale(f0, dur, vol=1.0, contour=(1.0, 1.6, 0.7), seed=0):
    """the Saint calling from under the island: a vast low voice gliding through a contour"""
    n = n_(dur); r = np.random.default_rng(seed)
    x = np.linspace(0, 1, n)
    c = np.interp(x, np.linspace(0, 1, len(contour)), contour)
    c = lp(c - c.mean(), 3, 1) + c.mean()
    f = f0 * c * (1 + 0.01 * smooth_noise(n, 2, r))
    s = additive(f, n, [1.0, 0.7, 0.5, 0.4, 0.25, 0.15, 0.1, 0.06])
    s = fft_filter(s, lambda fq: resonances(fq, [(f0 * 3, f0 * 2, 1.0), (f0 * 7, f0 * 3, 0.4)]) + 0.05)
    sub = np.sin(2 * np.pi * np.cumsum(f * 0.5) / SR) * 0.5
    return (s + sub) * swell(n, 0.45, 1.3) * vol

def wind(dur, vol=1.0, centre=500, q=1.5, gust=0.25, whistle=0.0, seed=0):
    """wind through an opening: a band of noise that moves; a whistle where it crosses an edge"""
    n = n_(dur); r = np.random.default_rng(seed)
    x = noise(n, 'pink', r)
    w1 = smooth_noise(n, gust, r); w2 = smooth_noise(n, gust * 2.3, r)
    ts_ = np.linspace(0, dur, n)
    def g(ts, f):
        c = centre * 2 ** (1.2 * np.interp(ts, ts_, w1))
        return 1 / (1 + (q * (f / c - c / np.maximum(f, 1))) ** 2)
    out = stft_filter(x, g)
    out *= 0.6 + 0.4 * (w2 * 0.5 + 0.5)
    if whistle > 0:
        f = centre * 3 * 2 ** (0.4 * w1)
        out += np.sin(2 * np.pi * np.cumsum(f) / SR) * whistle * np.clip(w2, 0, 1) ** 2
    return out * vol

def drip(freq=None, vol=1.0, seed=0):
    r = np.random.default_rng(seed)
    freq = freq or r.uniform(700, 1500)
    n = n_(0.25); t = tt(n)
    f = freq * (1 + 1.4 * (1 - np.exp(-t / 0.02)))
    return np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t / 0.04) * np.clip(t / 0.001, 0, 1) * vol * 0.4

def surf(dur, vol=1.0, period=9.0, seed=0):
    """waves on stone, far off: a slow swell of low noise, a hiss as each one breaks"""
    n = n_(dur); r = np.random.default_rng(seed)
    t = tt(n)
    phase_ = (t / period + 0.3 * smooth_noise(n, 0.1, r)) % 1
    shape = np.exp(-((phase_ - 0.55) / 0.18) ** 2)
    low = lp(noise(n, 'brown', r), 400, 2) * (0.3 + shape)
    hiss = bp(noise(n, 'white', r), 2500, 0.8) * shape ** 2 * 0.35
    return (low + hiss) * vol

def whisper(dur, vol=1.0, syllables=None, seed=0):
    """breath shaped into vowels and consonants: speech with no voice in it"""
    r = np.random.default_rng(seed)
    syllables = syllables or [r.choice(['a', 'o', 'u', 'e', 'i']) for _ in range(r.integers(2, 5))]
    n = n_(dur)
    x = noise(n, 'white', r)
    k = len(syllables)
    ts_ = np.linspace(0, dur, n)
    def g(ts, f):
        i = min(k - 1, int(ts / dur * k))
        j = min(k - 1, i + 1); u = (ts / dur * k) % 1
        return vowel_gain(syllables[i], f, 1.15) * (1 - u) + vowel_gain(syllables[j], f, 1.15) * u + 0.05 * (f > 4000)
    out = stft_filter(x, g)
    syl = np.zeros(n)
    for i in range(k):
        a, b = n_(i * dur / k), n_((i + 1) * dur / k)
        syl[a:b] = swell(b - a, r.uniform(0.2, 0.5), 1.0)
    # sibilants between syllables
    sib = hp(noise(n, 'white', r), 4500, 2) * (1 - syl) ** 4 * 0.6
    return (out * syl + sib * (r.random() < 0.7)) * vol

def shepard(dur, vol=1.0, rate=1 / 24.0, down=False, base=55.0, octaves=6, seed=0):
    """the Shepard-Risset glissando: a pitch that rises (or falls) forever and never arrives"""
    n = n_(dur); t = tt(n)
    out = np.zeros(n)
    for k in range(octaves):
        pos = (k / octaves + (t * rate if not down else -t * rate)) % 1
        f = base * 2 ** (pos * octaves)
        a = np.exp(-((pos - 0.5) / 0.22) ** 2)
        out += a * np.sin(2 * np.pi * np.cumsum(f) / SR)
    return out * vol / octaves * 2
