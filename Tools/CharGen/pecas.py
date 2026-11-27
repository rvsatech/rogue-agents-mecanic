# -*- coding: utf-8 -*-
"""Peças da cabeça (gorro, capacete, capacete de obra, boné, fone): malhas presas no osso da cabeça,
moldadas na cabeça + cabelo do agente (cada direção pega o ponto mais longe e soma a grossura).
Triângulos no sentido anti-horário vistos de fora (padrão do glTF)."""
import math
import numpy as np

# Paleta (região C da textura de acessórios, 4x4 quadradinhos de cor lisa).
PAL = {'lente': 0, 'alca': 1, 'metal': 2, 'destaque': 3, 'almofada': 4, 'faixa': 5, 'mic': 6, 'botao': 7}


def pal_uv(name):
    i = PAL[name]
    return (0.5 + (i % 4 + 0.5) * 0.125, 0.5 + (i // 4 + 0.5) * 0.125)


class Mesh:
    def __init__(self):
        self.v = []; self.uv = []; self.t = []

    def add(self, verts, uvs, tris):
        base = len(self.v)
        self.v.extend([np.asarray(p, float) for p in verts])
        self.uv.extend([tuple(u) for u in uvs])
        self.t.extend([(a + base, b + base, c + base) for a, b, c in tris])
        return base

    def arrays(self):
        V = np.array(self.v); T = np.array(self.t, np.int64); UV = np.array(self.uv, float)
        N = np.zeros_like(V)
        fn = np.cross(V[T[:, 1]] - V[T[:, 0]], V[T[:, 2]] - V[T[:, 0]])
        for k in range(3): np.add.at(N, T[:, k], fn)
        # Junta normais de vértices na mesma posição (costura do UV).
        key = np.round(V / 1e-5).astype(np.int64)
        _, inv = np.unique(key, axis=0, return_inverse=True)
        acc = np.zeros((inv.max() + 1, 3)); np.add.at(acc, inv, N); N = acc[inv]
        N /= np.maximum(np.linalg.norm(N, axis=1, keepdims=True), 1e-9)
        return V, N, UV, T


class HeadShape:
    """Raio da cabeça (com cabelo) em cada direção, a partir do centro."""

    def __init__(self, points, center):
        self.c = np.asarray(center, float)
        d = points - self.c
        self.r = np.linalg.norm(d, axis=1)
        self.u = d / np.maximum(self.r[:, None], 1e-9)

    def radius(self, dirs, cone=0.2):
        cosc = math.cos(cone)
        dots = dirs @ self.u.T                     # (M, K)
        proj = dots * self.r[None, :]
        proj = np.where(dots > cosc, proj, -1)
        r = proj.max(1)
        return r


def sph(theta, phi):
    """theta 0 = em cima; phi 0 = frente (+z), cresce para +x."""
    return np.stack([np.sin(theta) * np.sin(phi), np.cos(theta), np.sin(theta) * np.cos(phi)], -1)


def dome(shape, line_front, line_back, offset, n_phi=48, n_theta=14, cuff=None, raise_top=0.0):
    """Casco (malha + UV na região A). line_front/back: altura (m) da borda em relação ao centro."""
    c = shape.c
    phis = np.linspace(-math.pi, math.pi, n_phi + 1)           # costura atrás (phi = +-pi)
    tmax = np.zeros(n_phi + 1)
    ths = np.linspace(0.02, 2.4, 140)
    for j, ph in enumerate(phis):
        dirs = sph(ths, np.full_like(ths, ph))
        r = shape.radius(dirs)
        y = dirs[:, 1] * r
        yl = 0.5 * (line_front + line_back) + 0.5 * (line_front - line_back) * math.cos(ph)
        below = np.nonzero(y < yl)[0]
        tmax[j] = ths[below[0]] if len(below) else ths[-1]
    # Borda suave.
    for _ in range(3):
        tmax[:-1] = 0.25 * np.roll(tmax[:-1], 1) + 0.5 * tmax[:-1] + 0.25 * np.roll(tmax[:-1], -1)
        tmax[-1] = tmax[0]
    rows = []
    for k in range(1, n_theta + 1):
        f = k / n_theta
        th = tmax * f
        dirs = sph(th, phis)
        r = shape.radius(dirs)
        rows.append((dirs, r, f))
    # Raio suave entre vizinhos (tira caroços do cabelo).
    R = np.array([r for _, r, _ in rows])
    for _ in range(2):
        R[:, :-1] = 0.5 * R[:, :-1] + 0.25 * (np.roll(R[:, :-1], 1, 1) + np.roll(R[:, :-1], -1, 1))
        R[:, -1] = R[:, 0]
        R[1:-1] = 0.5 * R[1:-1] + 0.25 * (R[:-2] + R[2:])
    top_r = shape.radius(np.array([[0.0, 1.0, 0.0]]))[0]
    m = Mesh()
    verts = []; uvs = []
    pole = c + np.array([0, 1, 0]) * (top_r + offset + raise_top)
    verts.append(pole); uvs.append((0.25, 0.02))
    for k, (dirs, _, f) in enumerate(rows):
        extra = offset + raise_top * (1 - f) ** 2
        if cuff and f > cuff[0]: extra += cuff[1]
        for j in range(n_phi + 1):
            verts.append(c + dirs[j] * (R[k, j] + extra))
            uvs.append((0.02 + 0.46 * j / n_phi, 0.02 + 0.96 * f))
    tris = []
    for j in range(n_phi):
        tris.append((0, 1 + j, 1 + j + 1))
    for k in range(n_theta - 1):
        a0 = 1 + k * (n_phi + 1); a1 = 1 + (k + 1) * (n_phi + 1)
        for j in range(n_phi):
            tris.append((a0 + j, a1 + j, a0 + j + 1))
            tris.append((a0 + j + 1, a1 + j, a1 + j + 1))
    m.add(verts, uvs, tris)
    # Borda: anel de dentro, quase encostado na cabeça.
    dirs, _, _ = rows[-1]
    last = 1 + (n_theta - 1) * (n_phi + 1)
    inner = [c + dirs[j] * (R[-1, j] + offset * 0.1) for j in range(n_phi + 1)]
    iuv = [(0.02 + 0.46 * j / n_phi, 0.985) for j in range(n_phi + 1)]
    base = m.add(inner, iuv, [])
    rim = []
    for j in range(n_phi):
        rim.append((last + j, base + j, last + j + 1))
        rim.append((last + j + 1, base + j, base + j + 1))
    m.t.extend(rim)
    return m, (phis, dirs, R[-1] + offset + (cuff[1] if cuff else 0.0), c)


def brim_ring(m, ring, width_front, width_side, width_back, drop, thick, only_front=None, tilt=0.0):
    """Aba (capacete de obra: em volta toda; boné: só na frente). UV na região B."""
    phis, dirs, r, c = ring
    outer_base = []
    n = len(phis)
    top = []; bot = []; tuv = []; buv = []
    for j in range(n):
        ph = phis[j]
        p0 = c + dirs[j] * r[j]
        hor = np.array([math.sin(ph), 0, math.cos(ph)])
        w = width_side + (width_front - width_side) * max(math.cos(ph), 0) + (width_back - width_side) * max(-math.cos(ph), 0)
        if only_front is not None:
            a = abs(((ph + math.pi) % (2 * math.pi)) - math.pi)
            w = width_front * max(0.0, math.cos(min(a / only_front, 1.0) * math.pi / 2)) ** 0.6 if a < only_front else 0.0
        p1 = p0 + hor * w + np.array([0, -drop - tilt * w, 0])
        top.append(p0); top.append(p1)
        bot.append(p0 + np.array([0, -thick, 0]) - hor * 0.002); bot.append(p1 + np.array([0, -thick, 0]))
        u = 0.52 + 0.46 * j / (n - 1)
        tuv += [(u, 0.03), (u, 0.22)]; buv += [(u, 0.27), (u, 0.46)]
    bt = m.add(top, tuv, [])
    bb = m.add(bot, buv, [])
    for j in range(n - 1):
        a, b_, cc, d = bt + 2 * j, bt + 2 * j + 1, bt + 2 * j + 2, bt + 2 * j + 3
        m.t += [(a, b_, cc), (b_, d, cc)]
        a, b_, cc, d = bb + 2 * j, bb + 2 * j + 1, bb + 2 * j + 2, bb + 2 * j + 3
        m.t += [(a, cc, b_), (b_, cc, d)]
        # borda de fora
        m.t += [(bt + 2 * j + 1, bb + 2 * j + 1, bt + 2 * j + 3), (bb + 2 * j + 1, bb + 2 * j + 3, bt + 2 * j + 3)]


def cylinder(m, center, axis, radius, length, slot, segs=16, cap=True):
    axis = np.asarray(axis, float); axis /= np.linalg.norm(axis)
    a = np.cross(axis, [0, 1, 0] if abs(axis[1]) < 0.9 else [1, 0, 0]); a /= np.linalg.norm(a)
    b = np.cross(axis, a)
    uv = pal_uv(slot)
    ring0 = []; ring1 = []
    for k in range(segs):
        ang = 2 * math.pi * k / segs
        d = a * math.cos(ang) + b * math.sin(ang)
        ring0.append(center - axis * length * 0.5 + d * radius)
        ring1.append(center + axis * length * 0.5 + d * radius)
    base = m.add(ring0 + ring1, [uv] * (2 * segs), [])
    for k in range(segs):
        k2 = (k + 1) % segs
        m.t += [(base + k, base + k2, base + segs + k), (base + k2, base + segs + k2, base + segs + k)]
    if cap:
        c1 = m.add([center + axis * length * 0.5], [uv], [])
        c0 = m.add([center - axis * length * 0.5], [uv], [])
        for k in range(segs):
            k2 = (k + 1) % segs
            m.t += [(c1, base + segs + k, base + segs + k2), (c0, base + k2, base + k)]


def box(m, center, ax, ay, az, slot):
    """Caixa com eixos (meias medidas já nos vetores)."""
    uv = pal_uv(slot)
    c = np.asarray(center, float)
    corners = []
    for sx in (-1, 1):
        for sy in (-1, 1):
            for sz in (-1, 1):
                corners.append(c + sx * np.asarray(ax) + sy * np.asarray(ay) + sz * np.asarray(az))
    faces = [(0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1), (2, 3, 7, 6), (0, 2, 6, 4), (1, 5, 7, 3)]
    for f in faces:
        base = m.add([corners[i] for i in f], [uv] * 4, [])
        m.t += [(base, base + 1, base + 2), (base, base + 2, base + 3)]


def strap(m, shape, theta, half_width, offset, slot='alca', n_phi=48):
    """Faixa em volta do casco na altura theta (alça dos óculos)."""
    phis = np.linspace(-math.pi, math.pi, n_phi + 1)
    uv = pal_uv(slot)
    top = sph(np.full(n_phi + 1, theta - half_width), phis)
    bot = sph(np.full(n_phi + 1, theta + half_width), phis)
    rt = shape.radius(top) + offset; rb = shape.radius(bot) + offset
    vt = [shape.c + top[j] * rt[j] for j in range(n_phi + 1)]
    vb = [shape.c + bot[j] * rb[j] for j in range(n_phi + 1)]
    base = m.add(vt + vb, [uv] * (2 * (n_phi + 1)), [])
    n = n_phi + 1
    for j in range(n_phi):
        m.t += [(base + j, base + n + j, base + j + 1), (base + j + 1, base + n + j, base + n + j + 1)]


def build(kind, shape, body):
    """Monta uma peça. body: medidas (olhos, etc). Volta (V, N, UV, T)."""
    c = shape.c
    eye_rel = body.eyes[1] - c[1]
    if kind == 'Gorro':
        m, ring = dome(shape, eye_rel + 0.05, eye_rel - 0.005, 0.013, cuff=(0.72, 0.008), raise_top=0.012)
    elif kind == 'Capacete':
        m, ring = dome(shape, eye_rel + 0.055, eye_rel - 0.02, 0.026, raise_top=0.006)
        brim_ring(m, ring, 0.012, 0.006, 0.01, 0.0, 0.012)
        # Óculos em cima do capacete (lentes + alça).
        th = 0.92
        for side in (-1, 1):
            ph = side * 0.36
            d = sph(np.array([th]), np.array([ph]))[0]
            r = shape.radius(d[None])[0] + 0.026 + 0.012
            cylinder(m, c + d * r, d, 0.024, 0.016, 'lente')
            cylinder(m, c + d * (r - 0.004), d, 0.029, 0.012, 'alca', cap=False)
        strap(m, shape, th, 0.09, 0.031, 'alca')
    elif kind == 'CapaceteObra':
        m, ring = dome(shape, eye_rel + 0.065, eye_rel - 0.005, 0.03, raise_top=0.022)
        brim_ring(m, ring, 0.05, 0.028, 0.04, 0.006, 0.01, tilt=0.15)
    elif kind == 'Bone':
        m, ring = dome(shape, eye_rel + 0.052, eye_rel - 0.01, 0.009, raise_top=0.008)
        brim_ring(m, ring, 0.075, 0.0, 0.0, 0.004, 0.008, only_front=1.05, tilt=0.12)
        top = c + np.array([0, 1, 0]) * (shape.radius(np.array([[0, 1.0, 0]]))[0] + 0.009 + 0.008)
        cylinder(m, top, [0, 1, 0], 0.009, 0.008, 'botao')
    elif kind == 'Fone':
        m = Mesh()
        ear_y = body.eyes[1] - 0.025 - c[1]
        cups = []
        for side in (-1, 1):
            d = np.array([side * 1.0, 0, 0])
            d = d + np.array([0, ear_y / 0.09, -0.08]); d /= np.linalg.norm(d)
            r = shape.radius(d[None], cone=0.25)[0]
            p = c + d * (r + 0.014)
            axis = np.array([side, 0, 0.0])
            cylinder(m, p, axis, 0.037, 0.03, 'faixa')
            cylinder(m, p - axis * 0.018, axis, 0.032, 0.012, 'almofada', cap=False)
            cylinder(m, p + axis * 0.017, axis, 0.022, 0.006, 'destaque')
            cups.append(p)
        # Arco por cima da cabeça.
        n = 24
        verts = []; uvs = []
        for k in range(n + 1):
            a = -math.pi / 2 + math.pi * k / n
            d = np.array([math.sin(a), math.cos(a), -0.06]); d /= np.linalg.norm(d)
            r = shape.radius(d[None], cone=0.22)[0] + 0.016
            p = c + d * r
            if k == 0: p = cups[0] + np.array([0, 0.03, 0])
            if k == n: p = cups[1] + np.array([0, 0.03, 0])
            side = np.array([0, 0, 1.0]) * 0.014
            outn = d * 0.006
            verts += [p - side, p + side, p - side + outn, p + side + outn]
            uvs += [pal_uv('faixa')] * 4
        base = m.add(verts, uvs, [])
        for k in range(n):
            i0 = base + 4 * k; i1 = base + 4 * (k + 1)
            m.t += [(i0 + 2, i0 + 3, i1 + 2), (i1 + 2, i0 + 3, i1 + 3)]       # fora
            m.t += [(i0, i1, i0 + 1), (i1, i1 + 1, i0 + 1)]                   # dentro
            m.t += [(i0, i0 + 2, i1), (i1, i0 + 2, i1 + 2)]
            m.t += [(i0 + 1, i1 + 1, i0 + 3), (i1 + 1, i1 + 3, i0 + 3)]
        # Microfone (lado esquerdo do agente = +x).
        mouth = np.array([0.0, body.eyes[1] - 0.085, body.eyes[2] + 0.05])
        p0 = cups[1] + np.array([0, -0.02, 0.02])
        mid = (p0 + mouth) * 0.5
        dirv = mouth - p0; L = np.linalg.norm(dirv); dirv /= L
        cylinder(m, mid, dirv, 0.005, L * 0.9, 'mic', segs=8)
        cylinder(m, p0 + dirv * L * 0.95, dirv, 0.011, 0.02, 'mic', segs=10)
    else:
        raise ValueError(kind)
    return m.arrays()
