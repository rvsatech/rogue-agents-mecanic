"""Gera todos os efeitos sonoros provisórios (síntese pura, sem samples de terceiros).
Uso: python3 make_sfx.py <pasta_saida>
Nome dos arquivos: <prefixo>_<NN>.wav — o menu Rogue junta as variações pelo prefixo."""
import os
import sys as _sys
_sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import sys
import json
import numpy as np
from dsp import *  # noqa: F401,F403

OUT = sys.argv[1]
os.makedirs(OUT, exist_ok=True)
REPORT = []


def save(name, x, **kw):
    y = finalize(x, **kw)
    write(os.path.join(OUT, name + '.wav'), y)
    w = secs(0.05)
    rms = np.sqrt(np.convolve(y ** 2, np.ones(w) / w, mode='same')).max()
    REPORT.append((name, len(y) / SR, 20 * np.log10(np.max(np.abs(y)) + 1e-12), 20 * np.log10(rms + 1e-12)))


def variants(prefix, fn, count, seed, **kw):
    for i in range(count):
        rng = np.random.default_rng(seed * 100 + i)
        save(f'{prefix}_{i + 1:02d}', fn(rng), **kw)


# ======================= MOVIMENTO =======================
def footstep(rng, weight=1.0):
    buf = np.zeros(secs(0.35))
    place(buf, thump(rng, 0.12, rng.uniform(110, 140), rng.uniform(60, 75), rng.uniform(0.014, 0.02)), 0.0, 0.55 * weight)
    place(buf, noise_burst(rng, 0.06, rng.uniform(0.006, 0.010), lo=rng.uniform(900, 1400), hi=rng.uniform(5000, 7000)), 0.001, 0.45)
    place(buf, noise_burst(rng, 0.1, 0.02, hi=350, attack=0.001), 0.0, 0.5 * weight)
    place(buf, noise_burst(rng, 0.09, 0.025, lo=600, hi=3200, attack=0.008, pink=True), rng.uniform(0.02, 0.035), 0.22)
    place(buf, noise_burst(rng, 0.04, 0.005, lo=1500, hi=6000), rng.uniform(0.03, 0.05), 0.2)
    return reverb(buf, rng, 0.35, 0.10, 3500)


def jump(rng):
    buf = np.zeros(secs(0.5))
    place(buf, noise_burst(rng, 0.08, 0.02, lo=700, hi=4000, attack=0.004), 0.0, 0.35)
    place(buf, thump(rng, 0.1, 120, 70, 0.018), 0.0, 0.35)
    d = rng.uniform(0.28, 0.34)
    sw = swept_noise(rng, d, lambda u: 450 + 1300 * u ** 0.7, bw_oct=1.2) * bell(secs(d), 0.35)
    place(buf, sw, 0.02, 0.6)
    place(buf, rustle(rng, 0.25), 0.01, 0.25)
    return reverb(buf, rng, 0.3, 0.06)


def land(rng):
    buf = np.zeros(secs(0.55))
    for t0 in [0.0, rng.uniform(0.012, 0.03)]:
        place(buf, thump(rng, 0.18, rng.uniform(90, 110), rng.uniform(48, 58), rng.uniform(0.03, 0.045)), t0, 0.7)
        place(buf, noise_burst(rng, 0.12, 0.03, hi=300), t0, 0.6)
        place(buf, noise_burst(rng, 0.06, 0.008, lo=1000, hi=6000), t0 + 0.001, 0.4)
    place(buf, rustle(rng, 0.25, 600, 3500), 0.01, 0.25)
    place(buf, rattle(rng, 0.15), 0.02, 0.12)
    return reverb(buf, rng, 0.45, 0.12)


def slide_start(rng):
    buf = np.zeros(secs(0.55))
    place(buf, thump(rng, 0.15, 100, 55, 0.04), 0.0, 0.5)
    place(buf, noise_burst(rng, 0.1, 0.025, hi=400), 0.0, 0.45)
    place(buf, scrape(rng, 0.4) * bell(secs(0.4), 0.15, 1.2), 0.0, 0.8)
    place(buf, rustle(rng, 0.3), 0.0, 0.3)
    return reverb(buf, rng, 0.35, 0.08)


def slide_loop(rng):
    L, X = 1.2, 0.2
    y = scrape(rng, L + X)
    n, nx = secs(L), secs(X)
    out = y[:n].copy()
    out[:nx] = y[:nx] * np.sqrt(np.linspace(0, 1, nx)) + y[n:n + nx] * np.sqrt(np.linspace(1, 0, nx))
    return out


def roll(rng):
    buf = np.zeros(secs(0.9))
    place(buf, thump(rng, 0.2, 95, 50, 0.05), 0.0, 0.6)
    place(buf, noise_burst(rng, 0.15, 0.04, hi=350), 0.0, 0.5)
    t1 = rng.uniform(0.17, 0.22)
    place(buf, thump(rng, 0.2, 80, 45, 0.06), t1, 0.4)
    place(buf, noise_burst(rng, 0.15, 0.05, hi=300), t1, 0.35)
    place(buf, rustle(rng, 0.55, 500, 4000, 30), 0.0, 0.45)
    place(buf, rattle(rng, 0.4, 6), 0.05, 0.12)
    place(buf, footstep(rng, 0.8), rng.uniform(0.42, 0.5), 0.55)
    return reverb(buf, rng, 0.4, 0.08)


def cloth(rng):
    buf = np.zeros(secs(0.45))
    place(buf, rustle(rng, rng.uniform(0.22, 0.32), 500, 4000, 25), 0.0, 1.0)
    place(buf, rattle(rng, 0.2, 3), 0.02, 0.08)
    return buf


def prone_down(rng):
    buf = np.zeros(secs(0.9))
    place(buf, rustle(rng, 0.3, 500, 4000), 0.0, 0.4)
    place(buf, thump(rng, 0.25, 85, 45, 0.07), 0.12, 0.75)
    place(buf, noise_burst(rng, 0.2, 0.06, hi=280), 0.12, 0.6)
    place(buf, noise_burst(rng, 0.08, 0.01, lo=900, hi=5000), 0.121, 0.25)
    place(buf, rattle(rng, 0.25, 5), 0.13, 0.15)
    place(buf, rustle(rng, 0.25, 500, 3500), 0.2, 0.25)
    return reverb(buf, rng, 0.45, 0.1)


def prone_up(rng):
    buf = np.zeros(secs(0.8))
    place(buf, rustle(rng, 0.45, 500, 4200, 30), 0.0, 0.7)
    place(buf, noise_burst(rng, 0.08, 0.02, lo=700, hi=3500, attack=0.005), 0.15, 0.25)
    place(buf, footstep(rng, 0.7), 0.32, 0.45)
    place(buf, rattle(rng, 0.25, 4), 0.1, 0.1)
    return reverb(buf, rng, 0.4, 0.08)


def ledge_grab(rng):
    buf = np.zeros(secs(0.45))
    for t0 in [0.0, rng.uniform(0.025, 0.05)]:
        place(buf, noise_burst(rng, 0.05, 0.008, lo=900, hi=5500), t0, 0.6)
        place(buf, thump(rng, 0.08, 220, 130, 0.012), t0, 0.35)
    place(buf, rustle(rng, 0.25, 600, 4000), 0.01, 0.3)
    place(buf, rattle(rng, 0.2, 4), 0.03, 0.12)
    return reverb(buf, rng, 0.35, 0.1)


def wallrun_start(rng):
    buf = np.zeros(secs(0.55))
    place(buf, footstep(rng, 0.8), 0.0, 0.7)
    sw = swept_noise(rng, 0.4, lambda u: 700 + 1500 * u, bw_oct=1.0) * bell(secs(0.4), 0.3)
    place(buf, sw, 0.0, 0.45)
    place(buf, rustle(rng, 0.2), 0.02, 0.2)
    return reverb(buf, rng, 0.3, 0.08)


# ======================= VOZ =======================
def voice_effort(rng):
    dur = rng.uniform(0.58, 0.72)
    f0 = rng.uniform(102, 118)
    n = secs(dur)
    t = tt(n)
    u = t / dur
    f0c = f0 * (1 + 0.18 * np.sin(np.pi * np.clip(u / 0.7, 0, 1)) ** 1.5 - 0.15 * np.clip((u - 0.7) / 0.3, 0, 1))
    f0c *= 1 + 0.02 * np.sin(2 * np.pi * rng.uniform(6, 8) * t)
    src = glottal(rng, f0c, tilt=0.95, jitter=0.03)
    closed = vowel_filter(src, 'nn')
    opened = vowel_filter(src, 'uh')
    mix = np.clip((u - 0.55) / 0.25, 0, 1)
    voiced = norm01(closed) * (1 - mix) + norm01(opened) * mix * 0.9
    venv = np.clip(u / 0.12, 0, 1) ** 1.5 * np.clip((1 - u) / 0.25, 0, 1) ** 1.2
    trem = lp(rng.standard_normal(n), 12)
    venv *= 1 + 0.15 * trem / (np.std(trem) + 1e-12)
    breath = bp(rng.standard_normal(n), 700, 5000)
    benv = 0.5 * np.exp(-((u - 0.05) / 0.05) ** 2) + 0.8 * np.exp(-((u - 0.86) / 0.1) ** 2)
    breath = (norm01(vowel_filter(breath, 'ah')) * 0.7 + norm01(breath) * 0.2) * benv
    y = voiced * venv + 0.35 * breath
    y = np.tanh(2.2 * norm01(y))
    return reverb(y, rng, 0.3, 0.06)


def voice_short(rng):
    dur = rng.uniform(0.18, 0.26)
    f0 = rng.uniform(112, 132)
    vowel = ['uh', 'eh', 'ah'][rng.integers(0, 3)]
    n = secs(dur)
    u = tt(n) / dur
    src = glottal(rng, f0 * (1.0 - 0.22 * u), tilt=1.05, jitter=0.02)
    v = norm01(vowel_filter(src, vowel))
    env = np.clip(u / 0.08, 0, 1) * np.clip((1 - u) / 0.5, 0, 1) ** 1.5
    breath = bp(rng.standard_normal(n), 800, 5000)
    benv = 0.6 * np.exp(-((u - 0.04) / 0.04) ** 2) + 0.4 * np.clip((u - 0.5) / 0.5, 0, 1) * env
    y = v * env + 0.25 * norm01(vowel_filter(breath, 'ah')) * benv
    y = np.tanh(1.8 * norm01(y))
    return reverb(y, rng, 0.3, 0.05)


# ======================= ARMAS =======================
def gunshot(rng, pistol=False):
    if pistol:
        boom, body_tau, crack_tau, rt, wet, dur = (rng.uniform(220, 245), 75, 0.05), 0.032, 0.009, 0.8, 0.3, 1.1
    else:
        boom, body_tau, crack_tau, rt, wet, dur = (rng.uniform(160, 180), 52, 0.075), 0.05, 0.012, 1.1, 0.35, 1.5
    buf = np.zeros(secs(dur))
    place(buf, noise_burst(rng, 0.02, 0.0025, attack=0.0002), 0, 1.0)
    place(buf, noise_burst(rng, 0.08, crack_tau, lo=1800, hi=12000, attack=0.0003), 0, 0.8)
    place(buf, noise_burst(rng, 0.25, body_tau, lo=120, hi=3000, attack=0.0008), 0, 1.0)
    place(buf, thump(rng, 0.35, *boom, attack=0.0005), 0, 1.1)
    place(buf, metal_click(rng, rng.uniform(2400, 3000), 0.01), rng.uniform(0.045, 0.06), 0.12)
    buf = np.tanh(2.0 * norm01(buf))
    out = reverb(buf, rng, rt, wet, 2500, 0.02)
    echo = lp(out, 2000) * 0.18
    place(out, echo.copy(), rng.uniform(0.06, 0.09))
    return out


def dry_fire(rng):
    buf = np.zeros(secs(0.15))
    place(buf, metal_click(rng, rng.uniform(2800, 3300), 0.004), 0, 1.0)
    place(buf, thump(rng, 0.04, 400, 250, 0.005), 0, 0.3)
    return reverb(buf, rng, 0.25, 0.08, 6000)


def mag_out(rng):
    buf = np.zeros(secs(0.4))
    place(buf, metal_click(rng, 2600, 0.006), 0.0, 0.6)
    place(buf, scrape(rng, 0.12, 900, 5000) * bell(secs(0.12), 0.3), 0.03, 0.5)
    place(buf, metal_click(rng, 1800, 0.008), 0.13, 0.35)
    return reverb(buf, rng, 0.25, 0.08, 6000)


def mag_in(rng):
    buf = np.zeros(secs(0.35))
    place(buf, scrape(rng, 0.08, 900, 5000) * bell(secs(0.08), 0.4), 0.0, 0.35)
    place(buf, thump(rng, 0.08, 260, 160, 0.012), 0.07, 0.6)
    place(buf, noise_burst(rng, 0.05, 0.008, lo=900, hi=6000), 0.07, 0.6)
    place(buf, metal_click(rng, 2200, 0.012), 0.072, 0.5)
    return reverb(buf, rng, 0.25, 0.08, 6000)


def rack(rng):
    buf = np.zeros(secs(0.55))
    place(buf, scrape(rng, 0.07, 1200, 6000) * bell(secs(0.07), 0.4), 0.0, 0.4)
    place(buf, metal_click(rng, 1900, 0.015), 0.06, 0.6)
    place(buf, scrape(rng, 0.04, 1500, 7000) * bell(secs(0.04), 0.4), 0.14, 0.3)
    place(buf, metal_click(rng, 1500, 0.03), 0.17, 1.0)
    place(buf, thump(rng, 0.08, 300, 180, 0.01), 0.17, 0.4)
    place(buf, modal(rng, 0.25, [520, 1340, 2710], [0.06, 0.04, 0.03], [0.2, 0.15, 0.1]), 0.17, 0.3)
    return reverb(buf, rng, 0.3, 0.1, 6000)


def equip_gun(rng):
    buf = np.zeros(secs(0.5))
    place(buf, rustle(rng, 0.2, 600, 4000), 0.0, 0.5)
    place(buf, rattle(rng, 0.15, 4, base=(1800, 3500)), 0.03, 0.35)
    place(buf, metal_click(rng, 2000, 0.012), 0.17, 0.6)
    place(buf, thump(rng, 0.06, 240, 150, 0.01), 0.17, 0.3)
    return reverb(buf, rng, 0.3, 0.08, 6000)


def knife_draw(rng):
    from scipy import signal as sg
    dur = 0.6
    n = secs(dur)
    u = tt(n) / dur
    x = hp(rng.standard_normal(n), 2500)
    base = rng.uniform(2900, 3300)
    res = np.zeros(n)
    for r, q, a in [(1.0, 40, 1.0), (1.51, 45, 0.8), (2.07, 50, 0.6), (2.86, 60, 0.4)]:
        b, a_ = sg.iirpeak(base * r, q, fs=SR)
        res += a * sg.lfilter(b, a_, x)
    env = np.clip(u / 0.06, 0, 1) * np.exp(-np.clip(u - 0.1, 0, None) / 0.12)
    shing = norm01(res) * env
    ring = modal(rng, dur, [base * 1.02, base * 1.53, base * 2.1, base * 2.9], [0.25, 0.18, 0.12, 0.08],
                 [0.25, 0.18, 0.12, 0.08]) * np.clip(u / 0.08, 0, 1)
    buf = shing + norm01(ring) * 0.35
    place(buf, rustle(rng, 0.08, 600, 3000), 0.0, 0.15)
    return reverb(buf, rng, 0.4, 0.1, 6000)


def whoosh(rng):
    dur = rng.uniform(0.22, 0.3)
    peak = rng.uniform(0.4, 0.55)
    top = rng.uniform(2000, 2800)
    sw = swept_noise(rng, dur, lambda u: 500 + top * np.sin(np.pi * u) ** 1.5, bw_oct=0.9)
    return sw * bell(secs(dur), peak, 2.2)


def knife_hit(rng):
    buf = np.zeros(secs(0.3))
    place(buf, thump(rng, 0.12, 180, 90, 0.025), 0.0, 0.8)
    place(buf, noise_burst(rng, 0.12, 0.02, lo=400, hi=3000), 0.0, 0.6)
    place(buf, noise_burst(rng, 0.03, 0.004, lo=2000, hi=8000), 0.0, 0.3)
    return reverb(buf, rng, 0.3, 0.08)


def equip_gadget(rng):
    buf = np.zeros(secs(0.5))
    place(buf, rustle(rng, 0.15), 0, 0.3)
    place(buf, noise_burst(rng, 0.03, 0.004, lo=1500, hi=6000), 0.08, 0.6)
    place(buf, thump(rng, 0.03, 420, 300, 0.006), 0.08, 0.3)
    n = secs(0.25)
    place(buf, hp(rng.standard_normal(n), 4500) * bell(n, 0.2), 0.1, 0.25)
    place(buf, modal(rng, 0.3, [4200, 6100, 7900], [0.08, 0.06, 0.05], [0.3, 0.2, 0.15]), 0.1, 0.25)
    return reverb(buf, rng, 0.35, 0.1, 7000)


def impact_surface(rng):
    buf = np.zeros(secs(0.35))
    place(buf, noise_burst(rng, 0.03, 0.003, lo=1500, hi=10000), 0, 1.0)
    place(buf, thump(rng, 0.06, 320, 180, 0.008), 0, 0.5)
    for _ in range(int(rng.integers(5, 9))):
        t0 = 0.01 + rng.exponential(0.04)
        place(buf, noise_burst(rng, 0.02, rng.uniform(0.001, 0.003), lo=2000, hi=9000), t0,
              rng.uniform(0.08, 0.25) * np.exp(-t0 / 0.08))
    return reverb(buf, rng, 0.5, 0.15, 4000)


def hit_target(rng):
    buf = np.zeros(secs(0.3))
    place(buf, thump(rng, 0.1, 150, 85, 0.02), 0, 0.9)
    place(buf, noise_burst(rng, 0.1, 0.018, lo=300, hi=2500), 0, 0.7)
    place(buf, noise_burst(rng, 0.02, 0.003, lo=2500, hi=9000), 0, 0.3)
    return reverb(buf, rng, 0.3, 0.06)


def hitmarker(rng):
    n = secs(0.09)
    t = tt(n)
    y = (np.sin(2 * np.pi * 1750 * t) + 0.5 * np.sin(2 * np.pi * 3500 * t)) * np.exp(-t / 0.018)
    a = secs(0.001)
    y[:a] *= np.linspace(0, 1, a)
    y += noise_burst(rng, 0.09, 0.002, lo=3000) * 0.3
    return y


# ======================= GELO =======================
def ice_create(rng):
    buf = np.zeros(secs(1.0))
    place(buf, noise_burst(rng, 0.25, 0.04, lo=1000, hi=8000, attack=0.002), 0.0, 0.7)
    place(buf, thump(rng, 0.25, 130, 55, 0.06), 0.0, 0.9)
    place(buf, noise_burst(rng, 0.15, 0.03, hi=500), 0.0, 0.5)
    for _ in range(40):
        t0 = 0.01 + rng.exponential(0.12)
        if t0 > 0.7:
            continue
        f = rng.uniform(2500, 8000)
        c = modal(rng, 0.06, [f, f * 1.47, f * 2.03], [0.012, 0.008, 0.006], [1, 0.6, 0.4])
        place(buf, c, t0, rng.uniform(0.05, 0.2) * np.exp(-t0 / 0.3))
    place(buf, modal(rng, 0.7, [3100, 4650, 6230, 7800], [0.25, 0.2, 0.15, 0.12], [0.25, 0.18, 0.12, 0.08]), 0.0, 0.3)
    return reverb(buf, rng, 0.6, 0.15, 7000)


def ice_impact(rng):
    buf = np.zeros(secs(0.45))
    place(buf, noise_burst(rng, 0.02, 0.002, lo=2500, hi=12000), 0, 0.8)
    f = rng.uniform(2200, 3400)
    place(buf, modal(rng, 0.35, [f, f * 1.42, f * 2.11, f * 2.73, f * 3.55], [0.09, 0.07, 0.05, 0.04, 0.03],
                     [1, 0.7, 0.5, 0.35, 0.2]), 0, 0.45)
    place(buf, thump(rng, 0.06, 400, 250, 0.008), 0, 0.3)
    for _ in range(4):
        g = rng.uniform(3000, 8000)
        place(buf, modal(rng, 0.04, [g, g * 1.5], [0.006, 0.004], [1, 0.5]), rng.uniform(0.005, 0.08), 0.15)
    return reverb(buf, rng, 0.5, 0.15, 7000)


def ice_break(rng):
    buf = np.zeros(secs(1.4))
    place(buf, noise_burst(rng, 0.06, 0.008, lo=800, hi=10000), 0, 1.0)
    place(buf, thump(rng, 0.3, 150, 50, 0.06), 0, 0.9)
    place(buf, noise_burst(rng, 0.4, 0.09, lo=1500, hi=9000), 0, 0.4)
    for _ in range(70):
        t0 = rng.exponential(0.12) + (0.15 if rng.uniform() < 0.3 else 0.0)
        if t0 > 1.05:
            continue
        f = rng.uniform(1800, 9000)
        c = modal(rng, 0.12, [f, f * 1.37, f * 2.17], [rng.uniform(0.01, 0.04), 0.012, 0.008], [1, 0.5, 0.3])
        place(buf, c, t0, rng.uniform(0.05, 0.3) * np.exp(-t0 / 0.4))
    for _ in range(6):
        place(buf, thump(rng, 0.05, 300, 200, 0.006), rng.uniform(0.25, 0.7), 0.15)
    return reverb(buf, rng, 0.7, 0.18, 6000)


# ======================= GERAR =======================
variants('passo', footstep, 6, 1)
variants('pulo', jump, 3, 2)
variants('aterrissagem', land, 3, 3)
variants('slide_inicio', slide_start, 2, 4)
save('slide_loop', slide_loop(np.random.default_rng(500)), loud_db=-20.0, loop=True)
variants('cambalhota', roll, 2, 6)
variants('roupa', cloth, 3, 7)
variants('deitar', prone_down, 2, 8)
variants('levantar', prone_up, 2, 9)
variants('agarrar', ledge_grab, 2, 10)
variants('parede', wallrun_start, 2, 11)
variants('voz_esforco', voice_effort, 3, 12)
variants('voz_curta', voice_short, 4, 13)
variants('rifle_tiro', lambda r: gunshot(r, False), 3, 14)
variants('pistola_tiro', lambda r: gunshot(r, True), 3, 15)
variants('sem_bala', dry_fire, 1, 16)
variants('recarga_tira', mag_out, 1, 17)
variants('recarga_poe', mag_in, 1, 18)
variants('recarga_engatilha', rack, 1, 19)
variants('sacar_arma', equip_gun, 2, 20)
variants('sacar_faca', knife_draw, 2, 21)
variants('faca_golpe', whoosh, 3, 22)
variants('faca_acerto', knife_hit, 2, 23)
variants('sacar_gelo', equip_gadget, 1, 24)
variants('impacto', impact_surface, 4, 25)
variants('acerto_alvo', hit_target, 2, 26)
variants('hitmarker', hitmarker, 1, 27, loud_db=-18.0)
variants('gelo_criar', ice_create, 2, 28)
variants('gelo_impacto', ice_impact, 3, 29)
variants('gelo_quebrar', ice_break, 2, 30)

for name, dur, pk, rms in REPORT:
    print(f'{name:24s} {dur:5.2f}s  pico {pk:6.1f} dB  volume {rms:6.1f} dB')
print('total', len(REPORT), 'arquivos')
