# -*- coding: utf-8 -*-
"""Roupas pintadas na textura + volume na malha. Cada peça diz, para cada ponto do corpo (em metros),
o quanto ele está "para dentro" da peça (margem > 0 = coberto). A mesma conta serve para a malha
(inchar onde tem roupa) e para cada pixel da textura (cor, costura, padrão)."""
import math
import zlib
import numpy as np

# ---------------- ruído 3D (para tecido, camuflado e sujeira) ----------------
_P = np.random.RandomState(1234).permutation(256)
_P = np.concatenate([_P, _P]).astype(np.int64)


def vnoise(P):
    Pi = np.floor(P).astype(np.int64)
    f = P - Pi
    u = f * f * (3 - 2 * f)
    x, y, z = Pi[:, 0] & 255, Pi[:, 1] & 255, Pi[:, 2] & 255

    def h(dx, dy, dz):
        return _P[_P[_P[(x + dx) & 255] + ((y + dy) & 255)] + ((z + dz) & 255)] / 255.0
    c000, c100, c010, c110 = h(0, 0, 0), h(1, 0, 0), h(0, 1, 0), h(1, 1, 0)
    c001, c101, c011, c111 = h(0, 0, 1), h(1, 0, 1), h(0, 1, 1), h(1, 1, 1)
    ux, uy, uz = u[:, 0], u[:, 1], u[:, 2]
    x00 = c000 + (c100 - c000) * ux; x10 = c010 + (c110 - c010) * ux
    x01 = c001 + (c101 - c001) * ux; x11 = c011 + (c111 - c011) * ux
    y0 = x00 + (x10 - x00) * uy; y1 = x01 + (x11 - x01) * uy
    return y0 + (y1 - y0) * uz


def fbm(P, octaves=4, seed=0.0):
    total = np.zeros(len(P)); amp = 1.0; norm = 0.0; freq = 1.0
    for o in range(octaves):
        total += amp * vnoise(P * freq + (seed + o * 17.13))
        norm += amp; amp *= 0.5; freq *= 2.03
    return total / norm


def smooth01(x):
    x = np.clip(x, 0, 1)
    return x * x * (3 - 2 * x)


def hex2rgb(c):
    if isinstance(c, str):
        c = c.lstrip('#')
        return np.array([int(c[i:i + 2], 16) for i in (0, 2, 4)], float) / 255.0
    return np.array(c, float) / (255.0 if max(c) > 1.0 else 1.0)


# ---------------- grupos de ossos ----------------
GROUPS = ['head', 'torso', 'pelvis', 'clav', 'uarm', 'farm', 'hand', 'thigh', 'calf', 'foot']


def group_of(name):
    if name in ('Head', 'neck_01'): return 'head'
    if name.startswith('spine'): return 'torso'
    if name in ('pelvis', 'root'): return 'pelvis'
    if name.startswith('clavicle'): return 'clav'
    if name.startswith('upperarm'): return 'uarm'
    if name.startswith('lowerarm'): return 'farm'
    if name.startswith(('hand', 'thumb', 'index', 'middle', 'ring', 'pinky')): return 'hand'
    if name.startswith('thigh'): return 'thigh'
    if name.startswith('calf'): return 'calf'
    if name.startswith(('foot', 'ball')): return 'foot'
    return 'torso'


def group_weights(src_names, joints, weights):
    gi = np.array([GROUPS.index(group_of(n)) for n in src_names])
    GW = np.zeros((len(joints), len(GROUPS)))
    for c in range(4):
        np.add.at(GW, (np.arange(len(joints)), gi[joints[:, c]]), weights[:, c])
    return GW


# ---------------- medidas do corpo ----------------
class Body:
    def __init__(self, sk, eyes_pos, body_pos):
        p = sk.pos
        self.pelvis, self.sp1, self.sp3 = p('pelvis'), p('spine_01'), p('spine_03')
        self.neck, self.head = p('neck_01'), p('Head')
        self.sh, self.el, self.wr, self.kn = p('upperarm_l'), p('lowerarm_l'), p('hand_l'), p('middle_01_l')
        self.hip, self.knee, self.ank, self.toe = p('thigh_l'), p('calf_l'), p('foot_l'), p('ball_l')
        self.xs, self.xw, self.xk = abs(self.sh[0]), abs(self.wr[0]), abs(self.kn[0])
        self.armlen = self.xw - self.xs
        self.yhip, self.yank = self.hip[1], self.ank[1]
        self.leglen = self.yhip - self.yank
        self.waist = self.pelvis[1] + 0.55 * (self.sp1[1] - self.pelvis[1])
        self.chest = self.sp3[1] + 0.03
        self.shy = self.sh[1]
        self.eyes = eyes_pos.mean(0)
        self.top = body_pos[:, 1].max()
        self.xleg = abs(self.hip[0])
        self.xknee = abs(self.knee[0])


class Ctx:
    """Pontos (vértices ou pixels) com posição P, normal N e pesos por grupo GW."""

    def __init__(self, body, P, N, GW):
        self.b = body; self.P = P; self.N = N; self.GW = GW
        self.x, self.y, self.z = P[:, 0], P[:, 1], P[:, 2]
        self.ax = np.abs(self.x)
        self.side = np.where(self.x >= 0, 1.0, -1.0)
        self.s = (self.ax - body.xs) / body.armlen           # braço: 0 ombro, 0.5 cotovelo, 1 pulso
        self.l = (body.yhip - self.y) / body.leglen           # perna: 0 quadril, 0.5 joelho, 1 tornozelo
        g = {n: GW[:, i] for i, n in enumerate(GROUPS)}
        self.g = g
        self.arm = g['uarm'] + g['farm'] + g['hand']
        self.leg = g['thigh'] + g['calf'] + g['foot']
        self.headw = g['head']
        # Ângulo em volta do braço (0 = em cima, braço em T com a palma para baixo) e da perna (0 = lado de fora).
        t = np.clip(self.s, 0, 1)
        ay = body.sh[1] + (body.wr[1] - body.sh[1]) * t
        az = body.sh[2] + (body.wr[2] - body.sh[2]) * t
        self.arm_theta = np.arctan2(self.z - az, self.y - ay)
        self.arm_r = np.hypot(self.z - az, self.y - ay)
        tl = np.clip(self.l, 0, 1)
        lx = body.xleg + (body.xknee - body.xleg) * np.clip(tl * 2, 0, 1)
        lz = body.hip[2] + (body.ank[2] - body.hip[2]) * tl
        self.leg_theta = np.arctan2(self.z - lz, (self.ax - lx))
        self.leg_r = np.hypot(self.z - lz, self.ax - lx)
        self.n = len(P)


BIG = 10.0


def band(v, lo, hi):
    """Margem de estar entre lo e hi (metros)."""
    return np.minimum(v - lo, hi - v)


# ---------------- peças ----------------
def margin(kind, c, o):
    """Margem (m) de cada ponto estar dentro da peça. o = opções da peça."""
    b = c.b
    if kind == 'camisa':
        sleeve = o.get('manga', 0.97)
        m = c.y - (b.waist - o.get('barra', 0.0))
        collar_y = b.neck[1] + o.get('gola', 0.025) - o.get('decote', 0.035) * np.clip((c.z - b.neck[2]) / 0.08, -1, 1)
        m = np.minimum(m, collar_y - c.y)
        m = np.minimum(m, (sleeve - c.s) * b.armlen)
        ab = o.get('aberta', 0.0)
        if ab > 0:
            front = c.z > b.sp3[2] - 0.02
            m = np.where(front & (c.y > b.waist - o.get('barra', 0.0) - 0.01), np.minimum(m, c.ax - ab), m)
        return m
    if kind == 'calca':
        m = (b.waist + o.get('cintura', 0.0)) - c.y
        m = np.minimum(m, (o.get('comprimento', 1.0) - c.l) * b.leglen)
        return m
    if kind == 'bota':
        return (b.yank + o.get('altura', 0.12)) - c.y + np.where(c.y > b.waist, -BIG, 0)
    if kind == 'luva':
        m = (c.s - o.get('punho', 0.985)) * b.armlen
        if not o.get('dedos', True):
            m = np.minimum(m, ((b.xk - b.xs) / b.armlen + 0.02 - c.s) * b.armlen)
        return m
    if kind == 'colete':
        lo = b.waist + 0.05
        hi = b.shy - 0.035
        plates = np.minimum(band(c.y, lo, hi), 0.155 - c.ax)
        cummer = np.minimum(band(c.y, lo, b.waist + 0.23), (b.xs - 0.02) - c.ax)
        straps = np.minimum(0.028 - np.abs(c.ax - 0.105), band(c.y, hi - 0.06, b.neck[1] + 0.07))
        straps = np.minimum(straps, 0.2 - c.ax)
        return np.maximum(np.maximum(plates, cummer), straps)
    if kind == 'colete_leve':
        lo = b.chest - 0.15
        hi = b.chest + 0.06
        front = c.z - (b.sp3[2] + 0.02)
        plate = np.minimum(np.minimum(band(c.y, lo, hi), 0.13 - c.ax), front)
        strapf = np.minimum(np.minimum(0.022 - np.abs(c.ax - 0.1), band(c.y, hi - 0.03, b.neck[1] + 0.07)), 0.2 - c.ax)
        belt = np.minimum(band(c.y, lo - 0.005, lo + 0.035), (b.xs - 0.02) - c.ax)
        return np.maximum(np.maximum(plate, strapf), belt)
    if kind == 'cinto':
        y0 = b.waist + o.get('altura', -0.005)
        return np.minimum(o.get('largura', 0.042) * 0.5 - np.abs(c.y - y0), (b.xs - 0.02) - c.ax)
    if kind == 'joelheira':
        dy = 0.058 - np.abs(c.y - b.knee[1] + 0.01)
        dx = 0.062 - np.abs(c.ax - b.xknee)
        dz = c.z - (b.knee[2] + 0.015)
        return np.minimum(np.minimum(dy, dx), dz) + np.where(c.y > b.waist, -BIG, 0)
    if kind == 'cotoveleira':
        ds = 0.05 - np.abs((c.s - 0.5) * b.armlen)
        back = -(c.arm_theta) + 0.0  # lado de trás do cotovelo: z < eixo
        return np.minimum(ds, np.minimum(np.cos(c.arm_theta + math.pi / 2) - 0.35, 1) * 0.06)
    if kind == 'bolso_coxa':
        yp = b.yhip - 0.33 * b.leglen
        m = np.minimum(0.085 - np.abs(c.y - yp), 0.062 - np.abs(c.z - b.hip[2] - 0.005))
        m = np.minimum(m, c.ax - (b.xleg + 0.05))
        return m + np.where(c.y > b.waist, -BIG, 0)
    if kind == 'bolso_peito':
        yc = b.chest + 0.02
        m = np.minimum(0.048 - np.abs(c.ax - 0.095), 0.055 - np.abs(c.y - yc))
        return np.minimum(m, c.z - (b.sp3[2] + 0.04))
    if kind == 'mascara':
        m = np.minimum((b.eyes[1] - 0.03) - c.y, c.y - (b.neck[1] - 0.01))
        return m + np.where(c.headw < 0.5, -BIG, 0)
    if kind == 'balaclava':
        m = c.y - (b.neck[1] - 0.02)
        eye = np.minimum(np.minimum(0.021 - np.abs(c.y - b.eyes[1] + 0.002), 0.07 - c.ax), c.z - (b.eyes[2] - 0.03))
        return np.minimum(m, -eye) + np.where(c.headw < 0.5, -BIG, 0)
    raise ValueError(kind)


# Grossura padrão (m) de cada tipo de peça (volume na malha).
THICK = {'camisa': 0.006, 'calca': 0.006, 'bota': 0.013, 'luva': 0.003, 'colete': 0.022, 'colete_leve': 0.016,
         'cinto': 0.011, 'joelheira': 0.017, 'cotoveleira': 0.012, 'bolso_coxa': 0.013, 'bolso_peito': 0.011,
         'mascara': 0.0, 'balaclava': 0.0}


# Quanto alisar os músculos embaixo de cada tipo de peça (0 = colado no corpo).
LOOSE = {'camisa': 1.0, 'calca': 1.0, 'bota': 1.0, 'colete': 0.6, 'colete_leve': 0.4, 'cinto': 0.3,
         'joelheira': 0.3, 'cotoveleira': 0.2, 'bolso_coxa': 0.5, 'bolso_peito': 0.5, 'luva': 0.0,
         'mascara': 0.0, 'balaclava': 0.0}


def looseness(layers, c):
    w = np.zeros(c.n)
    for L in layers:
        f = L.get('folga', LOOSE[L['tipo']])
        if f <= 0: continue
        m = margin(L['tipo'], c, L)
        w = np.maximum(w, f * smooth01((m + 0.004) / 0.025))
    return np.clip(w, 0, 1)


def vertex_offsets(layers, c):
    """Quanto cada vértice sai para fora (m): a maior grossura entre as peças que cobrem o ponto."""
    off = np.zeros(c.n)
    for L in layers:
        t = L.get('grossura', THICK[L['tipo']])
        if t <= 0: continue
        m = margin(L['tipo'], c, L)
        off = np.maximum(off, t * smooth01(m / 0.008))
    # Embaixo do pé não desce (a sola não pode entrar no chão).
    down = np.clip(-c.N[:, 1], 0, 1)
    return off * np.clip(1.0 - down * 1.3, 0, 1)


# ---------------- pintura ----------------
def emblem_mask(shape, a, b, r):
    """Máscara (0..1) de um emblema simples desenhado nas coordenadas locais a,b (m), raio r."""
    u = a / r; v = b / r
    d = np.hypot(u, v)
    if shape == 'circulo':
        return ((d < 1) & (d > 0.62)) | (d < 0.3)
    if shape == 'triangulo':
        return (v > -0.55) & (v < 0.95 - 1.6 * np.abs(u) * 1.15) & ~((v > -0.2) & (v < 0.5 - 1.6 * np.abs(u) * 1.15))
    if shape == 'chevron':
        w = v + 0.9 * np.abs(u)
        return (np.abs(u) < 0.95) & (((w > -0.1) & (w < 0.35)) | ((w > 0.6) & (w < 1.05)))
    if shape == 'estrela':
        ang = np.arctan2(u, v)
        k = np.cos(5 * ang)
        rad = 0.5 + 0.45 * np.clip(k, 0, 1) ** 2
        return d < rad
    if shape == 'raio':
        p1 = (v > 0) & (v < 1) & (u > -0.2 - 0.4 * v) & (u < 0.25 - 0.4 * v)
        p2 = (v < 0.1) & (v > -1) & (u > -0.25 - 0.4 * v) & (u < 0.2 - 0.4 * v)
        return p1 | p2
    if shape == 'losango':
        q = np.abs(u) + np.abs(v)
        return ((q < 1) & (q > 0.6)) | (q < 0.25)
    if shape == 'olhos':
        d1 = np.hypot(u - 0.45, v); d2 = np.hypot(u + 0.45, v)
        return ((d1 < 0.42) & (d1 > 0.2)) | ((d2 < 0.42) & (d2 > 0.2)) | ((np.abs(u) < 0.12) & (v < -0.1) & (v > -0.55 + np.abs(u)))
    if shape == 'engrenagem':
        ang = np.arctan2(u, v)
        rad = 0.78 + 0.2 * (np.cos(8 * ang) > 0.2)
        return (d < rad) & (d > 0.38)
    return d < 1


def place_emblem(c, where):
    """Coordenadas locais (a, b) e máscara de lado para um emblema no peito, costas ou ombro."""
    b = c.b
    if where == 'peito':
        a = c.x - 0.095; bb = c.y - (b.chest + 0.035)
        ok = c.N[:, 2] > 0.35
        return -a, bb, ok
    if where == 'costas':
        a = c.x; bb = c.y - (b.chest - 0.0)
        ok = c.N[:, 2] < -0.35
        return a, bb, ok
    if where == 'ombro':
        a = c.ax - (b.xs + 0.2 * b.armlen); bb = c.z - b.sh[2]
        ok = (c.N[:, 1] > 0.3) & (c.s > 0)
        return a, bb, ok
    raise ValueError(where)


def camo(c, colors, scale, seed, digital=False):
    P = c.P / scale
    if digital:
        P = np.floor(c.P / (scale * 0.12)) * 0.12
    col = np.tile(hex2rgb(colors[0]), (c.n, 1))
    for i, cc in enumerate(colors[1:]):
        warp = fbm(P * 0.7 + 3.1 * i, 2, seed + 5 * i)
        n = fbm(P + warp[:, None] * 0.9, 3, seed + 11 * i)
        th = 0.52 + 0.035 * i
        mask = n > th
        col[mask] = hex2rgb(cc)
    return col


def paint_layer(L, S, c, m):
    """Cor (n x 3) e relevo (n) de uma peça no ponto. L = peça do agente, S = cores da skin para a peça."""
    kind = L['tipo']
    base = hex2rgb(S.get('cor', '#555555'))
    col = np.tile(base, (c.n, 1))
    h = np.zeros(c.n)
    pat = S.get('padrao', 'liso')
    seed = float(zlib.crc32(L.get('nome', kind).encode('utf-8')) % 97)   # semente fixa (mesmo resultado sempre)
    if pat == 'camuflado':
        col = camo(c, [S['cor']] + S['cores'], S.get('escala', 0.11), seed)
    elif pat == 'digital':
        col = camo(c, [S['cor']] + S['cores'], S.get('escala', 0.09), seed, digital=True)
    elif pat == 'jeans':
        fade = fbm(c.P * 9, 3, seed)
        col = col * (0.88 + 0.22 * fade[:, None])
        light = np.exp(-((c.l - 0.3) / 0.25) ** 2) * (c.z > c.b.hip[2]) * 0.18
        col = col + light[:, None] * (np.array([0.75, 0.8, 0.9]) - col)
    elif pat == 'acolchoado':
        ph = np.where(c.s > 0, (c.s * c.b.armlen) / 0.065, c.y / 0.062)
        ridge = 0.5 + 0.5 * np.cos(ph * 2 * math.pi)
        col = col * (0.82 + 0.22 * ridge[:, None])
        h += 0.9 * ridge
    # Listras ao longo de braço e perna (agasalho).
    if 'listras' in S:
        lc = hex2rgb(S['listras']); k = S.get('qtd_listras', 2); w = S.get('largura_listra', 0.011)
        offs = [(-1 + 2 * i / max(k - 1, 1)) * w * 1.4 for i in range(k)] if k > 1 else [0.0]
        stripe = np.zeros(c.n, bool)
        on_arm = (c.s > -0.02) & (c.s < 0.95)
        on_leg = (c.l > 0.02) & (c.l < 0.97) & (c.y < c.b.waist)
        for d in offs:
            stripe |= on_arm & (np.abs(c.arm_theta * np.maximum(c.arm_r, 0.03) - d) < w * 0.5)
            stripe |= on_leg & (np.abs(c.leg_theta * np.maximum(c.leg_r, 0.04) - d) < w * 0.5)
        col[stripe] = lc
    # Faixas refletivas (macacão).
    if 'faixas' in S:
        fc = hex2rgb(S['faixas']); ec = hex2rgb(S.get('borda_faixa', '#d8e030'))
        bands = [('s', 0.6, 0.68), ('l', 0.66, 0.74), ('y', c.b.chest - 0.045, c.b.chest + 0.0)]
        for axis, lo, hi in bands:
            v = c.s if axis == 's' else (c.l if axis == 'l' else c.y)
            if axis == 's': ok = c.s > 0
            elif axis == 'l': ok = c.y < c.b.waist
            else: ok = c.ax < c.b.xs
            unit = c.b.armlen if axis == 's' else (c.b.leglen if axis == 'l' else 1.0)
            mm = band(v, lo, hi) * unit
            inside = ok & (mm > 0)
            edge = inside & (mm < 0.006)
            col[inside] = fc; col[edge] = ec
            h[inside] += 0.25
    # Remendo de cor nos ombros (jaqueta de duas cores).
    if 'ombros' in S:
        sh = (c.y > c.b.chest + 0.07) | ((c.s > 0) & (c.arm_theta > -0.6) & (c.arm_theta < 0.6) & (c.s < 0.45))
        col[sh] = hex2rgb(S['ombros'])
    # Punho / barra em outra cor (canelado).
    trim = S.get('punho')
    if trim is not None or kind in ('camisa', 'calca', 'luva') and L.get('canelado'):
        tw = L.get('punho_largura', 0.03)
        cuff = (m > 0) & (m < tw)
        if trim is not None: col[cuff] = hex2rgb(trim)
        if L.get('canelado'):
            ang = np.where(c.s > 0, c.arm_theta * np.maximum(c.arm_r, 0.03),
                           np.where(c.y < c.b.waist, c.leg_theta * np.maximum(c.leg_r, 0.04), c.x))
            rib = 0.5 + 0.5 * np.cos(ang / 0.0045 * 2 * math.pi)
            col[cuff] *= (0.85 + 0.2 * rib[cuff, None])
            h[cuff] += 0.8 * rib[cuff]
    # Tipo de peça: detalhes próprios.
    if kind in ('colete', 'colete_leve'):
        web = (np.cos(c.y / 0.026 * 2 * math.pi) > 0.55)
        col[web] *= 0.82
        h += np.where(web, 0.6, 0)
        if L.get('bolsos', True) and kind == 'colete':
            for xc in (-0.068, 0.0, 0.068):
                pm = np.minimum(np.minimum(0.027 - np.abs(c.x - xc), band(c.y, c.b.waist + 0.065, c.b.waist + 0.19)),
                                c.z - (c.b.sp1[2] + 0.04))
                pin = pm > 0
                col[pin] = col[pin] * 0.92
                h[pin] += 1.2
                flap = pin & (c.y > c.b.waist + 0.15)
                col[flap] *= 0.85
                h += np.where(np.abs(pm) < 0.002, -1.0, 0)
    if kind == 'bota':
        sole = c.y < S.get('sola_altura', 0.026)
        col[sole] = hex2rgb(S.get('sola', '#2a2a2a'))
        if S.get('biqueira'):
            toe = (c.z > c.b.toe[2] - 0.015) & (c.y < 0.06)
            col[toe] = hex2rgb(S['biqueira'])
        if L.get('cadarco', True):
            lace = (np.abs(c.ax - c.b.xknee - 0.0) < 0.017) & (c.z > c.b.ank[2] + 0.03) & (c.y > 0.045) & \
                   (c.y < c.b.yank + L.get('altura', 0.12) - 0.01) & (c.N[:, 2] > 0.2)
            rung = np.cos((c.y + 0.3 * c.z) / 0.011 * 2 * math.pi) > 0.3
            col[lace & rung] = hex2rgb(S.get('cadarco', '#1a1a1a'))
            h += np.where(lace & rung, 0.7, 0)
    if kind == 'luva':
        knuckle = (c.N[:, 1] > 0.4) & (c.s > 1.05) & (c.s < 1.32)
        if S.get('protetor'): col[knuckle] = hex2rgb(S['protetor']); h += np.where(knuckle, 1.0, 0)
    if kind == 'cinto':
        buckle = (c.ax < 0.028) & (c.z > c.b.pelvis[2] + 0.02)
        col[buckle] = hex2rgb(S.get('fivela', '#9a9ea3'))
        h += np.where(buckle, 1.0, 0)
    if kind in ('joelheira', 'cotoveleira'):
        rim = (m > 0) & (m < 0.008)
        col[rim] *= 0.75
        h += np.where(m > 0.008, 1.0, 0)
    if kind == 'camisa' and L.get('ziper'):
        zip_ = (c.ax < 0.0045) & (c.z > c.b.sp3[2]) & (m > 0)
        teeth = np.cos(c.y / 0.005 * 2 * math.pi) > 0
        col[zip_] = hex2rgb(S.get('ziper', '#8d9196')) * np.where(teeth[zip_], 1.0, 0.7)[:, None]
        h += np.where(zip_, 0.5, 0)
    # Emblema.
    if 'emblema' in S:
        e = S['emblema']
        a, bb, ok = place_emblem(c, e.get('onde', 'peito'))
        mk = ok & (m > 0.004) & emblem_mask(e['forma'], a, bb, e.get('raio', 0.03))
        col[mk] = hex2rgb(e['cor'])
        h += np.where(mk, 0.3, 0)
    # Tecido: variação grande + grão fino.
    big = fbm(c.P * 6.0, 3, seed + 3)
    grain = vnoise(c.P * 480 + seed)
    tone = 0.93 + 0.12 * big + 0.05 * (grain - 0.5)
    col = col * tone[:, None]
    h += (grain - 0.5) * 0.35
    # Sujeira (skins gastas): mais embaixo e no joelho.
    if S.get('sujeira', 0) > 0:
        d = fbm(c.P * 5.5, 4, seed + 9)
        low = np.clip((0.7 - c.y) / 0.7, 0, 1) * 0.6 + 0.4
        amt = smooth01((d - 0.42) / 0.25) * low * S['sujeira']
        dirt = np.array([0.33, 0.27, 0.2])
        col = col + amt[:, None] * (dirt - col) * 0.75
    # Costura da barra e borda arredondada.
    if kind not in ('mascara', 'balaclava'):
        stitch = np.abs(m - 0.0075) < 0.0011
        col[stitch] *= 0.78
        h += np.where(stitch, -0.8, 0)
        h -= (1 - smooth01(m / 0.004)) * 1.0
    return col, h
