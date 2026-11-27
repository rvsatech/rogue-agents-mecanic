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
    """Cambalhota de 0,8 s: ombro no chão, costas rolando, pé no chão no fim."""
    buf = np.zeros(secs(1.15))
    place(buf, thump(rng, 0.2, 95, 50, 0.05), 0.0, 0.6)
    place(buf, noise_burst(rng, 0.15, 0.04, hi=350), 0.0, 0.5)
    t1 = rng.uniform(0.24, 0.3)
    place(buf, thump(rng, 0.2, 80, 45, 0.06), t1, 0.4)
    place(buf, noise_burst(rng, 0.15, 0.05, hi=300), t1, 0.35)
    place(buf, rustle(rng, 0.74, 500, 4000, 30), 0.0, 0.45)
    place(buf, rattle(rng, 0.55, 6), 0.05, 0.12)
    place(buf, footstep(rng, 0.8), rng.uniform(0.58, 0.66), 0.55)
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


def awp_shot(rng):
    """AWP: estouro grave e longo (eco de área aberta) + ferrolho puxado/empurrado ~0,55 s depois."""
    dur = 2.4
    buf = np.zeros(secs(dur))
    place(buf, noise_burst(rng, 0.02, 0.003, attack=0.0002), 0, 1.0)
    place(buf, noise_burst(rng, 0.12, 0.016, lo=1500, hi=12000, attack=0.0003), 0, 0.9)
    place(buf, noise_burst(rng, 0.4, 0.075, lo=80, hi=2500, attack=0.0008), 0, 1.0)
    place(buf, thump(rng, 0.6, rng.uniform(120, 135), 38, 0.12, attack=0.0005), 0, 1.3)
    shot = np.tanh(2.4 * norm01(buf))
    out = reverb(shot, rng, 1.8, 0.45, 2200, 0.025)
    echo = lp(out, 1500) * 0.22
    place(out, echo.copy(), rng.uniform(0.11, 0.15))
    # ferrolho: puxa (clique + raspada) e empurra (batida)
    bolt = np.zeros(secs(0.5))
    place(bolt, scrape(rng, 0.09, 1200, 6000) * bell(secs(0.09), 0.4), 0.0, 0.35)
    place(bolt, metal_click(rng, 1700, 0.02), 0.05, 0.8)
    place(bolt, scrape(rng, 0.08, 1200, 6000) * bell(secs(0.08), 0.4), 0.16, 0.3)
    place(bolt, metal_click(rng, 1400, 0.03), 0.24, 1.0)
    place(bolt, thump(rng, 0.08, 320, 190, 0.012), 0.24, 0.4)
    bolt = reverb(bolt, rng, 0.3, 0.08, 6000)
    place(out, norm01(bolt) * 0.18 * np.max(np.abs(out)), 0.55)
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


def headshot_marker(rng):
    """'Tink' agudo e brilhante: acertou a cabeça."""
    n = secs(0.16)
    t = tt(n)
    y = (np.sin(2 * np.pi * 2600 * t) + 0.6 * np.sin(2 * np.pi * 5200 * t) + 0.35 * np.sin(2 * np.pi * 7900 * t)) * np.exp(-t / 0.035)
    y += 0.4 * np.sin(2 * np.pi * 3900 * t) * np.exp(-t / 0.07)
    a = secs(0.001)
    y[:a] *= np.linspace(0, 1, a)
    y += noise_burst(rng, 0.16, 0.002, lo=4000) * 0.35
    return y


def kill_confirm(rng):
    """Confirmação de abate: baque curto + dois toques subindo."""
    buf = np.zeros(secs(0.5))
    place(buf, thump(rng, 0.12, 180, 90, 0.03), 0.0, 0.6)
    for t0, f in [(0.0, 1320.0), (0.085, 1980.0)]:
        n = secs(0.3)
        t = tt(n)
        tone = (np.sin(2 * np.pi * f * t) + 0.3 * np.sin(2 * np.pi * 2 * f * t)) * np.exp(-t / 0.09)
        a = secs(0.002)
        tone[:a] *= np.linspace(0, 1, a)
        place(buf, tone, t0, 0.8)
    return reverb(buf, rng, 0.3, 0.08, 8000)


# ======================= A 12 E GRANADAS =======================
def shotgun_shot(rng):
    """A 12: estouro grave e largo (muitos chumbos) + a bomba puxada e empurrada ~0,45 s depois."""
    buf = np.zeros(secs(1.9))
    place(buf, noise_burst(rng, 0.025, 0.004, attack=0.0002), 0, 1.0)
    place(buf, noise_burst(rng, 0.12, 0.02, lo=900, hi=9000, attack=0.0003), 0, 0.9)
    place(buf, noise_burst(rng, 0.35, 0.07, lo=70, hi=2200, attack=0.0008), 0, 1.0)
    place(buf, thump(rng, 0.5, rng.uniform(105, 120), 40, 0.1, attack=0.0005), 0, 1.4)
    shot = np.tanh(2.6 * norm01(buf))
    out = reverb(shot, rng, 1.3, 0.4, 2200, 0.02)
    place(out, lp(out, 1600) * 0.2, rng.uniform(0.08, 0.11))
    pump = np.zeros(secs(0.4))
    place(pump, scrape(rng, 0.08, 600, 3500) * bell(secs(0.08), 0.4), 0.0, 0.5)
    place(pump, metal_click(rng, 1100, 0.025), 0.07, 0.9)
    place(pump, scrape(rng, 0.07, 700, 4000) * bell(secs(0.07), 0.4), 0.16, 0.45)
    place(pump, metal_click(rng, 950, 0.03), 0.23, 1.0)
    place(pump, thump(rng, 0.08, 260, 160, 0.012), 0.23, 0.5)
    pump = reverb(pump, rng, 0.3, 0.08, 5000)
    place(out, norm01(pump) * 0.2 * np.max(np.abs(out)), rng.uniform(0.42, 0.48))
    return out


def grenade_pin(rng):
    """Granada: pino saindo (raspada metálica + anel tilintando) e a alavanca estalando."""
    buf = np.zeros(secs(0.6))
    place(buf, scrape(rng, 0.09, 2500, 9000) * bell(secs(0.09), 0.5), 0.0, 0.35)
    place(buf, modal(rng, 0.35, [3100, 4700, 6900], [0.08, 0.06, 0.04], [0.3, 0.2, 0.12]), 0.08, 0.35)
    place(buf, metal_click(rng, 2600, 0.012), 0.09, 0.5)
    place(buf, metal_click(rng, 1800, 0.02), 0.22, 0.7)
    return reverb(buf, rng, 0.3, 0.08, 7000)


def grenade_bounce(rng):
    """Granada quicando no chão: batida metálica curta e oca."""
    buf = np.zeros(secs(0.45))
    place(buf, thump(rng, 0.07, rng.uniform(380, 460), 220, 0.012), 0.0, 0.6)
    place(buf, modal(rng, 0.3, [rng.uniform(1300, 1600), rng.uniform(2700, 3300), rng.uniform(4800, 5600)],
                     [0.05, 0.035, 0.025], [0.5, 0.3, 0.15]), 0.0, 0.5)
    place(buf, noise_burst(rng, 0.03, 0.004, lo=1500, hi=8000), 0.0, 0.35)
    return reverb(buf, rng, 0.35, 0.12, 6000)


def grenade_explosion(rng):
    """Granada: explosão grave e longa, com estilhaços caindo depois."""
    buf = np.zeros(secs(3.2))
    place(buf, noise_burst(rng, 0.03, 0.006, attack=0.0003), 0, 1.0)
    place(buf, noise_burst(rng, 0.6, 0.12, lo=40, hi=1800, attack=0.001, pink=True), 0, 1.0)
    place(buf, thump(rng, 1.1, rng.uniform(70, 85), 28, 0.25, attack=0.0008), 0, 1.6)
    place(buf, noise_burst(rng, 0.2, 0.03, lo=800, hi=6000, attack=0.0005), 0, 0.5)
    boom = np.tanh(2.2 * norm01(buf))
    out = reverb(boom, rng, 2.2, 0.5, 1500, 0.03)
    for _ in range(int(rng.integers(8, 14))):
        place(out, noise_burst(rng, 0.03, rng.uniform(0.004, 0.01), lo=1500, hi=7000) * rng.uniform(0.04, 0.12),
              rng.uniform(0.35, 1.6))
    return out


def flash_bang(rng):
    """Flash: estalo seco e muito alto (agudo) com um anel metálico curto."""
    buf = np.zeros(secs(1.6))
    place(buf, noise_burst(rng, 0.015, 0.002, attack=0.0001), 0, 1.0)
    place(buf, noise_burst(rng, 0.08, 0.012, lo=2000, hi=14000, attack=0.0002), 0, 0.9)
    place(buf, thump(rng, 0.25, rng.uniform(160, 190), 60, 0.04, attack=0.0004), 0, 0.9)
    place(buf, modal(rng, 0.6, [2900, 4300, 6100], [0.12, 0.09, 0.06], [0.15, 0.1, 0.06]), 0.005, 0.5)
    bang = np.tanh(3.0 * norm01(buf))
    return reverb(bang, rng, 1.2, 0.4, 5000, 0.015)


def flash_ring(rng):
    """Zumbido no ouvido depois da flash (agudo, some devagar)."""
    dur = 3.2
    n = secs(dur)
    t = tt(n)
    f = 3300.0
    tone = np.sin(2 * np.pi * f * t) + 0.35 * np.sin(2 * np.pi * (f * 1.004) * t) + 0.1 * np.sin(2 * np.pi * 2 * f * t)
    env = np.exp(-t / 1.1)
    env[:secs(0.05)] *= np.linspace(0, 1, secs(0.05))
    return tone * env * 0.5


def smoke_pop(rng):
    """Smoke: estalo pequeno e o chiado da fumaça saindo (uns 3 s, diminuindo)."""
    buf = np.zeros(secs(3.6))
    place(buf, noise_burst(rng, 0.03, 0.005, lo=300, hi=5000), 0, 0.7)
    place(buf, thump(rng, 0.12, 200, 110, 0.02), 0, 0.5)
    n = secs(3.4)
    t = tt(n)
    hiss = bp(pinkify(rng.standard_normal(n)), 1500, 9000) * np.exp(-t / 1.4)
    hiss[:secs(0.1)] *= np.linspace(0, 1, secs(0.1))
    place(buf, hiss * 0.35, 0.05)
    return reverb(buf, rng, 0.5, 0.15, 6000)


# ======================= KIT MÉDICO =======================
def medkit_open(rng):
    """Kit médico: zíper abrindo (dentes passando, acelerando) + embalagem de plástico."""
    buf = np.zeros(secs(0.95))
    t0 = 0.02
    teeth = int(rng.integers(26, 34))
    zip_dur = rng.uniform(0.34, 0.42)
    for i in range(teeth):
        u = i / teeth
        place(buf, noise_burst(rng, 0.006, 0.0012, lo=2500, hi=9000), t0 + zip_dur * (u ** 0.85),
              rng.uniform(0.35, 0.6))
    n = secs(zip_dur)
    place(buf, bp(rng.standard_normal(n), 1800, 6000) * bell(n, 0.5) * 0.12, t0)
    place(buf, rustle(rng, 0.35, 1500, 7000, 40), t0 + zip_dur * 0.8, 0.35)
    place(buf, noise_burst(rng, 0.05, 0.01, lo=1200, hi=5000), t0 + zip_dur + 0.05, 0.3)
    return reverb(buf, rng, 0.3, 0.08, 7000)


def medkit_heal(rng):
    """Curou: duas notas suaves subindo + um respiro aliviado."""
    buf = np.zeros(secs(1.4))
    for t0, f in [(0.0, 784.0), (0.11, 1175.0)]:
        n = secs(1.1)
        t = tt(n)
        tone = (np.sin(2 * np.pi * f * t) + 0.25 * np.sin(2 * np.pi * 2 * f * t)
                + 0.1 * np.sin(2 * np.pi * 3 * f * t)) * np.exp(-t / 0.22)
        a = secs(0.008)
        tone[:a] *= np.linspace(0, 1, a)
        place(buf, tone, t0, 0.5)
    n = secs(0.45)
    place(buf, bp(pinkify(rng.standard_normal(n)), 400, 2200) * bell(n, 0.35), 0.18, 0.25)
    return reverb(buf, rng, 0.5, 0.15, 6000)


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
variants('awp_tiro', awp_shot, 2, 33)
variants('kit_abrir', medkit_open, 2, 34)
variants('kit_cura', medkit_heal, 1, 35)
variants('doze_tiro', shotgun_shot, 2, 36)
variants('granada_pino', grenade_pin, 1, 37)
variants('granada_quica', grenade_bounce, 3, 38)
variants('granada_explosao', grenade_explosion, 2, 39)
variants('flash_estouro', flash_bang, 2, 40)
variants('flash_zumbido', flash_ring, 1, 41, loud_db=-20.0)
variants('smoke_estouro', smoke_pop, 1, 42)
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
variants('hitmarker_cabeca', headshot_marker, 1, 31, loud_db=-17.0)
variants('abate', kill_confirm, 1, 32, loud_db=-16.0)
variants('gelo_criar', ice_create, 2, 28)
variants('gelo_impacto', ice_impact, 3, 29)
variants('gelo_quebrar', ice_break, 2, 30)

for name, dur, pk, rms in REPORT:
    print(f'{name:24s} {dur:5.2f}s  pico {pk:6.1f} dB  volume {rms:6.1f} dB')
print('total', len(REPORT), 'arquivos')
