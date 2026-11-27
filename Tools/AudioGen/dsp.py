"""Ferramentas de síntese de áudio (numpy/scipy) para os efeitos provisórios do protótipo."""
import numpy as np
from scipy import signal
from scipy.io import wavfile

SR = 44100


def secs(d):
    return max(1, int(round(d * SR)))


def tt(n):
    return np.arange(n) / SR


def _sos(kind, f, order):
    nyq = SR / 2
    if kind == 'bandpass':
        lo, hi = f
        hi = min(hi, nyq * 0.95)
        lo = max(lo, 10)
        return signal.butter(order, [lo, hi], btype='bandpass', fs=SR, output='sos')
    return signal.butter(order, min(f, nyq * 0.95), btype=kind, fs=SR, output='sos')


def lp(x, f, o=2):
    return signal.sosfilt(_sos('lowpass', f, o), x)


def hp(x, f, o=2):
    return signal.sosfilt(_sos('highpass', f, o), x)


def bp(x, lo, hi, o=2):
    return signal.sosfilt(_sos('bandpass', (lo, hi), o), x)


def pinkify(x):
    b = [0.049922035, -0.095993537, 0.050612699, -0.004408786]
    a = [1, -2.494956002, 2.017265875, -0.522189400]
    return signal.lfilter(b, a, x) * 4.0


def place(buf, x, start_s, gain=1.0):
    i = secs(start_s) if start_s > 0 else 0
    n = min(len(x), len(buf) - i)
    if n > 0:
        buf[i:i + n] += x[:n] * gain
    return buf


def env_exp(n, tau, attack=0.0008):
    e = np.exp(-tt(n) / tau)
    a = secs(attack)
    if a > 1:
        e[:a] *= np.linspace(0, 1, a)
    return e


def bell(n, peak=0.3, sharp=2.0):
    u = np.linspace(0, 1, n)
    rise = (np.clip(u / peak, 0, 1)) ** sharp
    fall = (np.clip((1 - u) / (1 - peak), 0, 1)) ** sharp
    return np.where(u < peak, rise, fall)


def norm01(x):
    m = np.max(np.abs(x))
    return x / m if m > 1e-12 else x


def thump(rng, dur, f0, f1, tau, attack=0.0015):
    """Baque grave com o tom caindo (corpo, pé, coronha)."""
    n = secs(dur)
    t = tt(n)
    f = f1 + (f0 - f1) * np.exp(-t / max(1e-4, tau * 0.6))
    ph = 2 * np.pi * np.cumsum(f) / SR
    return np.sin(ph + rng.uniform(0, 2 * np.pi)) * env_exp(n, tau, attack)


def noise_burst(rng, dur, tau, lo=None, hi=None, attack=0.0005, pink=False):
    n = secs(dur)
    x = rng.standard_normal(n)
    if pink:
        x = pinkify(x)
    if lo and hi:
        x = bp(x, lo, hi)
    elif lo:
        x = hp(x, lo)
    elif hi:
        x = lp(x, hi)
    return x * env_exp(n, tau, attack)


def modal(rng, dur, freqs, taus, amps, attack=0.0003):
    """Soma de senos amortecidos: metal, vidro, gelo."""
    n = secs(dur)
    t = tt(n)
    y = np.zeros(n)
    for f, ta, am in zip(freqs, taus, amps):
        if f >= SR * 0.45:
            continue
        y += am * np.sin(2 * np.pi * f * t + rng.uniform(0, 2 * np.pi)) * np.exp(-t / ta)
    a = secs(attack)
    if a > 1:
        y[:a] *= np.linspace(0, 1, a)
    return y


def metal_click(rng, base, tau, dur=0.08):
    ratios = np.array([1.0, 1.59, 2.14, 2.83, 3.67])
    freqs = base * ratios * (1 + rng.uniform(-0.03, 0.03, 5))
    taus = tau * np.array([1, 0.8, 0.6, 0.45, 0.35])
    amps = np.array([1, 0.7, 0.5, 0.35, 0.25])
    y = modal(rng, dur, freqs, taus, amps)
    y += noise_burst(rng, dur, tau * 0.3, lo=2000) * 0.5
    return y


def rattle(rng, dur, count=5, base=(2200, 4200), tau=(0.003, 0.008)):
    """Equipamento chacoalhando (cliques metálicos pequenos)."""
    n = secs(dur)
    y = np.zeros(n)
    for _ in range(count):
        c = metal_click(rng, rng.uniform(*base), rng.uniform(*tau))
        place(y, c * rng.uniform(0.3, 1.0), rng.uniform(0, dur * 0.8))
    return y


def rustle(rng, dur, lo=500, hi=4500, bumps_hz=25.0, peak=0.3):
    """Tecido/roupa farfalhando."""
    n = secs(dur)
    x = bp(pinkify(rng.standard_normal(n)), lo, hi)
    m = np.abs(lp(rng.standard_normal(n), bumps_hz))
    m = norm01(m)
    return x * (0.3 + 0.7 * m) * bell(n, peak, 1.5)


def scrape(rng, dur, lo=350, hi=3000):
    """Raspagem áspera (slide, pente deslizando)."""
    n = secs(dur)
    base = bp(pinkify(rng.standard_normal(n)), lo, hi)
    grit = bp(rng.standard_normal(n), 2000, 7000)
    m1 = norm01(np.abs(lp(rng.standard_normal(n), 40)))
    m2 = norm01(np.abs(lp(rng.standard_normal(n), 120)))
    return norm01(base) * (0.55 + 0.45 * m1) + 0.35 * norm01(grit) * m2 ** 2


def swept_noise(rng, dur, fc_fn, bw_oct=1.0, pink=True):
    """Ruído passando por um filtro que se move (vento, 'whoosh')."""
    n = secs(dur)
    x = rng.standard_normal(n + 4096)
    if pink:
        x = pinkify(x)
    f, frames, z = signal.stft(x, fs=SR, nperseg=1024, noverlap=768)
    u = np.clip(frames / dur, 0, 1)
    fc = np.maximum(40.0, fc_fn(u))
    logf = np.log2(np.maximum(f, 20.0))[:, None]
    sigma = bw_oct / 2.355
    g = np.exp(-0.5 * ((logf - np.log2(fc)[None, :]) / sigma) ** 2)
    _, y = signal.istft(z * g, fs=SR, nperseg=1024, noverlap=768)
    return norm01(y[:n])


def reverb(x, rng, rt60=0.5, wet=0.2, lp_f=4000.0, predelay=0.008):
    """Reverberação simples (IR de ruído com decaimento) — dá 'espaço' ao som."""
    L = secs(rt60 * 1.2)
    ir = rng.standard_normal(L) * np.exp(-tt(L) / (rt60 / 6.9))
    ir = lp(ir, lp_f)
    ir = np.concatenate([np.zeros(secs(predelay)), ir])
    ir /= np.sqrt(np.sum(ir ** 2)) + 1e-12
    w = signal.fftconvolve(x, ir)
    out = np.zeros(len(w))
    out[:len(x)] += x
    rx = np.sqrt(np.mean(x ** 2)) + 1e-12
    rw = np.sqrt(np.mean(w ** 2)) + 1e-12
    out += wet * w * (rx / rw)
    return out


# ---------------- voz (síntese por formantes) ----------------
FORMANTS = {
    'uh': [(640, 90, 1.0), (1190, 110, 0.5), (2390, 150, 0.25), (3300, 250, 0.12)],
    'ah': [(730, 100, 1.0), (1090, 120, 0.55), (2440, 150, 0.25), (3400, 250, 0.1)],
    'eh': [(530, 80, 1.0), (1840, 120, 0.45), (2480, 150, 0.25), (3500, 250, 0.1)],
    'nn': [(280, 70, 1.0), (1000, 200, 0.12), (2200, 250, 0.08), (3000, 300, 0.05)],
}


def vowel_filter(x, vowel):
    y = np.zeros_like(x)
    for f, bwid, a in FORMANTS[vowel]:
        sos = signal.butter(1, [f - bwid / 2, f + bwid / 2], btype='bandpass', fs=SR, output='sos')
        y += a * signal.sosfilt(sos, x)
    return y


def glottal(rng, f0, tilt=1.0, jitter=0.02, shimmer=0.08):
    """Fonte da voz: soma de harmônicos com pitch variável (sem aliasing)."""
    n = len(f0)
    jit = lp(rng.standard_normal(n), 30)
    jit /= np.std(jit) + 1e-12
    f = f0 * (1 + jitter * jit)
    phase = np.cumsum(f) / SR
    kmax = int((SR * 0.45) / max(40.0, f.min()))
    kmax = min(kmax, 140)
    y = np.zeros(n)
    for k in range(1, kmax + 1):
        mask = (k * f) < SR * 0.45
        y += mask * np.sin(2 * np.pi * k * phase) / (k ** tilt)
    sh = lp(rng.standard_normal(n), 40)
    sh /= np.std(sh) + 1e-12
    return y * (1 + shimmer * sh)


# ---------------- saída ----------------
def finalize(x, loud_db=-16.0, peak_db=-1.0, loop=False):
    x = np.asarray(x, dtype=np.float64)
    if not loop:
        x = hp(x, 30)
        thr = np.max(np.abs(x)) * 10 ** (-58 / 20)
        idx = np.where(np.abs(x) > thr)[0]
        end = idx[-1] + 1 if len(idx) else len(x)
        x = x[:end]
    w = secs(0.05)
    rms = np.sqrt(np.convolve(x ** 2, np.ones(w) / w, mode='same'))
    cur = 20 * np.log10(rms.max() + 1e-12)
    x = x * 10 ** ((loud_db - cur) / 20)
    pk = np.max(np.abs(x))
    lim = 10 ** (peak_db / 20)
    if pk > lim:
        x *= lim / pk
    if not loop:
        f = min(secs(0.012), len(x) // 4)
        if f > 1:
            x[-f:] *= np.linspace(1, 0, f)
    return x


def write(path, x):
    data = np.clip(x, -1, 1)
    wavfile.write(path, SR, (data * 32767).astype(np.int16))
