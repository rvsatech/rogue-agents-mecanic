# -*- coding: utf-8 -*-
"""
Texturas do mapa Pátio Industrial, feitas por código (sem baixar nada): concreto, asfalto, metal ondulado,
container, tijolo, madeira, listras de perigo, pintura gasta do chão e fachada com janelas.
Todas repetem sem emenda (a borda da esquerda casa com a da direita, a de cima com a de baixo).
Uso: python gerar_texturas_mapa.py <pasta de saída>   (precisa de numpy + pillow)
"""
import os, sys, math
import numpy as np
from PIL import Image

S = 512
rng = np.random.RandomState(42)


def pnoise(u, v, period, seed=0):
    """Ruído suave que repete a cada 'period' células (u, v em 0..1)."""
    r = np.random.RandomState(seed)
    g = r.rand(period, period)
    x = u * period; y = v * period
    x0 = np.floor(x).astype(int); y0 = np.floor(y).astype(int)
    fx = x - x0; fy = y - y0
    fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy)
    x0 %= period; y0 %= period; x1 = (x0 + 1) % period; y1 = (y0 + 1) % period
    a = g[y0, x0]; b = g[y0, x1]; c = g[y1, x0]; d = g[y1, x1]
    return (a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy


def fbm(u, v, base=4, octaves=5, seed=0):
    t = 0; amp = 1; norm = 0
    for o in range(octaves):
        t = t + amp * pnoise(u, v, base * 2 ** o, seed + o * 31)
        norm += amp; amp *= 0.5
    return t / norm


V, U = np.mgrid[0:S, 0:S]
u = (U + 0.5) / S
v = (V + 0.5) / S


def normal_from_height(h, strength):
    dx = np.roll(h, -1, 1) - np.roll(h, 1, 1)
    dy = np.roll(h, -1, 0) - np.roll(h, 1, 0)
    n = np.stack([-dx * strength, dy * strength, np.ones_like(h)], -1)
    n /= np.linalg.norm(n, axis=-1, keepdims=True)
    return n


def save(img, name, out, normal=False):
    arr = img * 0.5 + 0.5 if normal else img
    im = Image.fromarray((np.clip(arr, 0, 1) * 255 + 0.5).astype(np.uint8))
    if normal or name.endswith('_Mascara'):
        im.save(os.path.join(out, name + '.png'), optimize=True)
    else:
        im.save(os.path.join(out, name + '.jpg'), quality=90)


def rgb(*c):
    return np.array(c, float) / 255.0


def concreto(out):
    big = fbm(u, v, 3, 5, 1)
    grain = fbm(u, v, 64, 2, 2)
    stains = np.clip((fbm(u, v, 5, 4, 3) - 0.58) * 4, 0, 1)
    base = 0.53 + 0.1 * (big - 0.5) + 0.06 * (grain - 0.5) - 0.12 * stains
    col = np.stack([base * 1.0, base * 0.99, base * 0.96], -1)
    # Juntas das placas na borda (a textura cobre 4 m = uma placa).
    edge = np.minimum(np.minimum(u, 1 - u), np.minimum(v, 1 - v))
    joint = edge < 2.0 / S
    col[joint] *= 0.55
    # Rachaduras finas.
    crack = (np.abs(fbm(u, v, 6, 4, 9) - 0.5) < 0.0025) & (fbm(u, v, 2, 3, 10) > 0.58)
    col[crack] *= 0.7
    h = 0.4 * grain + 0.2 * big - 1.5 * joint - 0.6 * crack
    save(col, 'T_Concreto_Cor', out); save(normal_from_height(h, 2.2), 'T_Concreto_Normal', out, True)


def asfalto(out):
    grain = fbm(u, v, 96, 2, 4)
    speck = (pnoise(u, v, 256, 5) > 0.82) * 0.08
    big = fbm(u, v, 4, 4, 6)
    base = 0.2 + 0.05 * (big - 0.5) + 0.07 * (grain - 0.5) + speck
    col = np.stack([base, base, base * 1.02], -1)
    save(col, 'T_Asfalto_Cor', out)


def metal_ondulado(out):
    # 16 ondas por repetição (2 m -> 12,5 cm cada).
    wave = 0.5 + 0.5 * np.cos(u * 2 * math.pi * 16)
    streak = fbm(u * 1.0, v * 0.15, 8, 4, 11)
    rust = np.clip((fbm(u, v, 6, 5, 12) - 0.6) * 3, 0, 1) * (0.4 + 0.6 * v)
    base = 0.62 + 0.12 * (streak - 0.5) - 0.12 * (1 - wave) * 0.5
    col = np.stack([base, base * 1.01, base * 1.03], -1)
    col = col * (1 - rust[..., None]) + rust[..., None] * rgb(120, 70, 40)
    seam = np.minimum(v, 1 - v) < 3.0 / S
    col[seam] *= 0.7
    h = wave * 1.0 - 2 * seam
    save(col, 'T_Metal_Cor', out); save(normal_from_height(h, 3.0), 'T_Metal_Normal', out, True)


def container(out):
    # Lateral de container: ondas trapezoidais + trilhos em cima e embaixo (V = altura do container).
    ph = (u * 10) % 1.0
    trap = np.clip(np.minimum(ph, 1 - ph) * 6 - 0.6, 0, 1)
    rail = (v < 0.07) | (v > 0.93)
    dirt = fbm(u * 1.0, v * 0.3, 6, 4, 21)
    rust = np.clip((fbm(u, v, 5, 5, 22) - 0.62) * 4, 0, 1)
    drip = np.clip((fbm(u * 3, v * 0.2, 16, 3, 23) - 0.55) * 3, 0, 1) * (1 - v)
    base = 0.86 - 0.08 * (1 - trap) - 0.1 * (dirt - 0.5) - 0.12 * drip
    col = np.stack([base, base, base], -1)
    col[rail] *= 0.72
    col = col * (1 - rust[..., None] * 0.85) + rust[..., None] * 0.85 * rgb(110, 60, 35)
    h = np.where(rail, 1.4, trap * 1.0)
    save(col, 'T_Container_Cor', out); save(normal_from_height(h, 3.5), 'T_Container_Normal', out, True)


def tijolo(out):
    rows = 24; cols = 8
    rv = v * rows; ri = np.floor(rv).astype(int)
    shift = (ri % 2) * 0.5
    cu = u * cols + shift; ci = np.floor(cu).astype(int)
    fu = cu - ci; fv = rv - ri
    mortar = (fu < 0.05) | (fv < 0.12)
    r = np.random.RandomState(7)
    tone = r.rand(rows, cols + 1)
    t = tone[ri % rows, ci % cols]
    brick = rgb(150, 72, 50) * (0.78 + 0.35 * t[..., None])
    grain = fbm(u, v, 48, 2, 8)
    brick = brick * (0.9 + 0.2 * grain[..., None])
    soot = fbm(u, v, 3, 4, 13)
    col = np.where(mortar[..., None], rgb(165, 158, 148) * (0.85 + 0.2 * grain[..., None]), brick)
    col *= (0.85 + 0.25 * soot[..., None])
    h = np.where(mortar, -1.0, 0.2 * grain)
    save(col, 'T_Tijolo_Cor', out); save(normal_from_height(h, 2.5), 'T_Tijolo_Normal', out, True)


def madeira(out):
    planks = 6
    pv = v * planks; pi = np.floor(pv).astype(int); fv = pv - pi
    gap = (fv < 0.04) | (fv > 0.97)
    r = np.random.RandomState(3)
    tone = r.rand(planks)
    grain = np.sin((u * 40 + 3 * fbm(u, v, 8, 3, 14) + pi * 1.7) * 2 * math.pi) * 0.5 + 0.5
    base = rgb(170, 128, 82) * (0.8 + 0.25 * tone[pi % planks][..., None]) * (0.88 + 0.14 * grain[..., None])
    nails = np.zeros_like(u, bool)
    for cx in (0.08, 0.92):
        for k in range(planks):
            cy = (k + 0.5) / planks
            nails |= (np.hypot((u - cx) * S, (v - cy) * S) < 3.5)
    col = np.where(gap[..., None], rgb(50, 38, 28), base)
    col[nails] = rgb(70, 70, 72)
    h = np.where(gap, -1.5, 0.25 * grain)
    save(col, 'T_Madeira_Cor', out); save(normal_from_height(h, 2.5), 'T_Madeira_Normal', out, True)


def listras(out):
    d = (u + v) * 4
    stripe = (d % 1.0) < 0.5
    wear = fbm(u, v, 8, 4, 31)
    col = np.where(stripe[..., None], rgb(232, 182, 30), rgb(28, 28, 30))
    worn = wear > 0.68
    col = np.where(worn[..., None], col * 0.55 + 0.45 * rgb(120, 118, 112), col)
    col *= (0.92 + 0.1 * fbm(u, v, 32, 2, 32))[..., None]
    save(col, 'T_Listras_Cor', out)


def tinta(out):
    # Pintura gasta do chão (cinza claro; a cor vem do material): onde gastou aparece o concreto.
    wear = fbm(u, v, 6, 5, 41)
    grain = fbm(u, v, 64, 2, 42)
    paint = 0.92 + 0.06 * (grain - 0.5)
    under = 0.45 + 0.1 * (grain - 0.5)
    m = np.clip((wear - 0.62) * 5, 0, 1)
    base = paint * (1 - m) + under * m
    col = np.stack([base, base, base], -1)
    save(col, 'T_Tinta_Cor', out)


def janelas(out):
    # Fachada de fábrica: 4 janelas por andar, 2 andares (8 m).
    col = np.ones((S, S, 3)) * rgb(120, 116, 110)
    grain = fbm(u, v, 16, 3, 51)
    col *= (0.85 + 0.25 * grain[..., None])
    r = np.random.RandomState(5)
    for fy in range(2):
        for fx in range(4):
            x0, x1 = fx / 4 + 0.04, (fx + 1) / 4 - 0.04
            y0, y1 = fy / 2 + 0.1, (fy + 1) / 2 - 0.12
            win = (u > x0) & (u < x1) & (v > y0) & (v < y1)
            lit = r.rand() < 0.25
            glass = rgb(255, 214, 150) * 0.9 if lit else rgb(40, 52, 60)
            col[win] = glass * (0.85 + 0.2 * fbm(u, v, 24, 2, 52 + fx)[win][..., None])
            # Caixilhos.
            gx = ((u - x0) / (x1 - x0) * 3) % 1.0
            gy = ((v - y0) / (y1 - y0) * 2) % 1.0
            frame = win & ((gx < 0.05) | (gy < 0.05))
            col[frame] = rgb(60, 60, 62)
            sill = (u > x0 - 0.01) & (u < x1 + 0.01) & (v > y1) & (v < y1 + 0.015)
            col[sill] = rgb(150, 146, 140)
    save(col, 'T_Janelas_Cor', out)


def main():
    out = sys.argv[1]
    os.makedirs(out, exist_ok=True)
    for f in (concreto, asfalto, metal_ondulado, container, tijolo, madeira, listras, tinta, janelas):
        f(out)
        print('ok', f.__name__)


if __name__ == '__main__':
    main()
