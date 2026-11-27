# -*- coding: utf-8 -*-
"""
Gera os AGENTES jogáveis (modelo .glb + texturas das skins) a partir do
Universal Base Characters [Standard] da Quaternius (CC0, quaternius.com).

O que ele faz:
 1. Corpo masculino/feminino com os ossos renomeados para os do projeto (DEF-, iguais aos do manequim da
    Universal Animation Library) e com as orientações de repouso do manequim. Assim as animações do
    projeto servem direto; cada corpo mantém as proporções dele.
 2. Roupas pintadas na textura (camisa, calça, colete, botas, luvas, joelheira...) e com volume: a malha
    "incha" onde tem roupa, então as barras e o colete aparecem no contorno.
 3. Cabelo + sobrancelhas numa malha só (1 material, cor por skin) e os olhos em outra.
 4. Peças de cabeça (gorro, capacete, capacete de obra, boné, fone) como malhas separadas: cada skin liga as dela.
 5. Agentes.json: a lista que o menu "Rogue" da Unity lê para montar os personagens e as skins.

Uso (precisa de Python 3 + numpy + pillow + scipy):
    python gerar_personagens.py <pasta do UBC Standard> <AnimationLibrary_Godot_Standard.gltf> <pasta de saída> [--previa <pasta>]
A lista de agentes e as cores ficam em agentes.py.
"""
import argparse, json, os, math, sys, unicodedata
import numpy as np
from PIL import Image
from scipy import ndimage

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ubc_lib import Gltf, Skeleton, GlbWriter, render  # noqa: E402
import roupas as R  # noqa: E402
import pecas as PC  # noqa: E402
from agentes import AGENTES  # noqa: E402

RES = 1024          # textura do corpo (por skin)
ACC_RES = 256       # textura das peças da cabeça (por skin)
HAIR_MEAN = 0.68    # brilho médio do cabelo (a cor vem do material)


def slug(text):
    t = unicodedata.normalize('NFKD', text).encode('ascii', 'ignore').decode()
    return ''.join(ch for ch in t.title() if ch.isalnum())


def load_rgb(path, size=None):
    im = Image.open(path).convert('RGB')
    if size: im = im.resize((size, size), Image.LANCZOS)
    return np.asarray(im).astype(np.float64) / 255.0


# ---------------- textura: posição de cada pixel no corpo ----------------
def raster(uv, tri, res):
    tri_id = np.full((res, res), -1, np.int32)
    bary = np.zeros((res, res, 3), np.float32)
    px = uv[:, 0] * res - 0.5
    py = uv[:, 1] * res - 0.5
    for ti in range(len(tri)):
        a, b, c = tri[ti]
        x0, y0, x1, y1, x2, y2 = px[a], py[a], px[b], py[b], px[c], py[c]
        area = (x1 - x0) * (y2 - y0) - (x2 - x0) * (y1 - y0)
        if abs(area) < 1e-12: continue
        xmin = max(int(math.floor(min(x0, x1, x2))), 0); xmax = min(int(math.ceil(max(x0, x1, x2))), res - 1)
        ymin = max(int(math.floor(min(y0, y1, y2))), 0); ymax = min(int(math.ceil(max(y0, y1, y2))), res - 1)
        if xmin > xmax or ymin > ymax: continue
        gx, gy = np.meshgrid(np.arange(xmin, xmax + 1, dtype=np.float64), np.arange(ymin, ymax + 1, dtype=np.float64))
        w0 = ((x1 - gx) * (y2 - gy) - (x2 - gx) * (y1 - gy)) / area
        w1 = ((x2 - gx) * (y0 - gy) - (x0 - gx) * (y2 - gy)) / area
        w2 = 1 - w0 - w1
        m = (w0 >= -0.02) & (w1 >= -0.02) & (w2 >= -0.02)
        sub = tri_id[ymin:ymax + 1, xmin:xmax + 1]
        inside = (w0 >= 0) & (w1 >= 0) & (w2 >= 0)
        put = m & ((sub < 0) | inside)
        sub[put] = ti
        bsub = bary[ymin:ymax + 1, xmin:xmax + 1]
        bsub[put] = np.stack([w0[put], w1[put], w2[put]], -1)
    return tri_id, bary


class BodyData:
    def __init__(self, ubc_dir, man, sex):
        name = 'Superhero_Male_FullBody' if sex == 'M' else 'Superhero_Female_FullBody'
        self.sex = sex
        self.src = Gltf(os.path.join(ubc_dir, 'Base Characters', 'Godot - UE', name + '.gltf'))
        self.sk = Skeleton(self.src, man)
        prims = [self.src.primitive(i) for i in range(len(self.src.j['meshes']))]
        mesh_names = [m['name'] for m in self.src.j['meshes']]
        node_mesh = {n['mesh']: n.get('name', '') for n in self.src.nodes if 'mesh' in n}
        self.body = max(prims, key=lambda p: len(p['pos']))
        self.brows = prims[[i for i in range(len(prims)) if node_mesh.get(i) == 'Eyebrows'][0]]
        self.eyes = prims[[i for i in range(len(prims)) if node_mesh.get(i) == 'Eyes'][0]]
        self.brows['tex'] = self._tex_of(self.brows['material'])
        tdir = os.path.join(ubc_dir, 'Base Characters', 'Textures')
        base = 'T_Superhero_Male_Ligh.png' if sex == 'M' else 'T_Superhero_Female_Light_BaseColor.png'
        self.base = load_rgb(os.path.join(tdir, base), RES)
        self.normal = load_rgb(os.path.join(tdir, 'Normals Unity - Godot',
                                            'T_Superhero_%s_Normal.png' % ('Male' if sex == 'M' else 'Female')), RES) * 2 - 1
        self.measure = R.Body(self.sk, self.eyes['pos'], self.body['pos'])
        b = self.body
        self.gw = R.group_weights(self.sk.src_names, b['joints'], b['weights'])
        print(f'  corpo {sex}: rasterizando a textura...', flush=True)
        tri_id, bary = raster(b['uv'], b['idx'], RES)
        self.inside = tri_id >= 0
        _, (iy, ix) = ndimage.distance_transform_edt(~self.inside, return_indices=True)
        tri_id = tri_id[iy, ix]; bary = bary[iy, ix]
        T = b['idx'][tri_id.reshape(-1)]
        B = bary.reshape(-1, 3).astype(np.float64)

        def interp(attr):
            return (attr[T[:, 0]] * B[:, 0:1] + attr[T[:, 1]] * B[:, 1:2] + attr[T[:, 2]] * B[:, 2:3])
        self.tP = interp(b['pos'])
        tN = interp(b['nrm']); self.tN = tN / np.maximum(np.linalg.norm(tN, axis=1, keepdims=True), 1e-9)
        self.tGW = interp(self.gw)
        self.tctx = R.Ctx(self.measure, self.tP, self.tN, self.tGW)
        # Cor média da pele (sem a roupa de baixo cinza).
        px = self.base.reshape(-1, 3)
        sat = px.max(1) - px.min(1)
        skin = self.inside.reshape(-1) & (sat > 0.12) & (self.tGW[:, R.GROUPS.index('head')] < 0.5)
        self.skin_mean = px[skin].mean(0)
        # Normais "soldadas" por posição (para inchar sem abrir a malha nas costuras do UV).
        key = np.round(b['pos'] / 1e-5).astype(np.int64)
        _, self.weld = np.unique(key, axis=0, return_inverse=True)
        self.weld = self.weld.reshape(-1)

    def _tex_of(self, mat_index):
        mat = self.src.j['materials'][mat_index]
        tex = mat['pbrMetallicRoughness']['baseColorTexture']['index']
        uri = self.src.j['images'][self.src.j['textures'][tex]['source']]['uri']
        return 2 if 'Hair_2' in uri else 1


def taubin(pos, idx, weld, weight, iters=14, lam=0.55, mu=-0.58):
    """Alisa a malha onde weight > 0 (pontos na mesma posição andam juntos)."""
    nu = weld.max() + 1
    P = np.zeros((nu, 3)); np.add.at(P, weld, pos); cnt = np.bincount(weld, minlength=nu)[:, None]; P /= cnt
    w = np.zeros(nu); np.maximum.at(w, weld, weight)
    e = np.concatenate([idx[:, [0, 1]], idx[:, [1, 2]], idx[:, [2, 0]]])
    e = weld[e]
    e = np.unique(np.sort(e, 1), axis=0)
    e = e[e[:, 0] != e[:, 1]]
    deg = np.bincount(e.ravel(), minlength=nu)[:, None].astype(float)
    for it in range(iters):
        for f in (lam, mu):
            acc = np.zeros_like(P)
            np.add.at(acc, e[:, 0], P[e[:, 1]]); np.add.at(acc, e[:, 1], P[e[:, 0]])
            lap = acc / np.maximum(deg, 1) - P
            P = P + (f * w)[:, None] * lap
    return P[weld]


def geometric_normals(pos, idx, weld):
    fn = np.cross(pos[idx[:, 1]] - pos[idx[:, 0]], pos[idx[:, 2]] - pos[idx[:, 0]])
    n = np.zeros_like(pos)
    for k in range(3): np.add.at(n, idx[:, k], fn)
    acc = np.zeros((weld.max() + 1, 3)); np.add.at(acc, weld, n)
    n = acc[weld]
    return n / np.maximum(np.linalg.norm(n, axis=1, keepdims=True), 1e-12)


# ---------------- cabelo ----------------
def load_hair_prim(ubc_dir, name):
    g = Gltf(os.path.join(ubc_dir, 'Hairstyles', 'Rigged to Head Bone', 'glTF (Godot -Unreal)', name + '.gltf'))
    p = g.primitive(0)
    mat = g.j['materials'][p['material']]
    uri = g.j['images'][g.j['textures'][mat['pbrMetallicRoughness']['baseColorTexture']['index']]['source']]['uri']
    p['tex'] = 2 if 'Hair_2' in uri else 1
    return p


def hair_mesh(bd, parts):
    """Junta cabelo (+barba) + sobrancelhas num pedaço; UV vai para o atlas (esq. = Hair_1, dir. = Hair_2)."""
    P, N, UV, I = [], [], [], []
    base = 0
    for p in parts:
        uv = np.clip(p['uv'], 0, 1).copy()
        uv[:, 0] = uv[:, 0] * 0.5 + (0.5 if p['tex'] == 2 else 0.0)
        uv[:, 0] = np.clip(uv[:, 0], (0.5 if p['tex'] == 2 else 0.0) + 0.001, (1.0 if p['tex'] == 2 else 0.5) - 0.001)
        P.append(p['pos']); N.append(p['nrm']); UV.append(uv); I.append(p['idx'] + base)
        base += len(p['pos'])
        # Cabelo e sobrancelha são "cartões" (planos): a cópia virada deixa ver dos dois lados.
        P.append(p['pos']); N.append(-p['nrm']); UV.append(uv); I.append(p['idx'][:, ::-1] + base)
        base += len(p['pos'])
    return np.concatenate(P), np.concatenate(N), np.concatenate(UV), np.concatenate(I)


# ---------------- pintura do corpo ----------------
def paint_body(bd, agent, skin):
    c = bd.tctx
    n = c.n
    col = np.zeros((n, 3)); hgt = np.zeros(n)
    owner = np.full(n, -1)
    layers = agent['roupas']
    margins = []
    for li, L in enumerate(layers):
        S = skin['roupas'].get(L['nome'])
        if S is None:
            if R.THICK.get(L['tipo'], 0) > 0 and L.get('grossura', 1) > 0:
                raise SystemExit(f"skin {skin['id']} de {agent['id']}: falta a cor da peça '{L['nome']}'")
            margins.append(None); continue
        m = R.margin(L['tipo'], c, L)
        margins.append(m)
        free = (owner < 0) & (m > 0)
        idx = np.nonzero(free)[0]
        if len(idx) == 0: continue
        owner[idx] = li
        sub = R.Ctx(c.b, c.P[idx], c.N[idx], c.GW[idx])
        cc, hh = R.paint_layer(L, S, sub, m[idx])
        col[idx] = cc; hgt[idx] = hh
    # Pele: textura original com o tom do agente.
    skin_px = owner < 0
    tone = R.hex2rgb(agent['pele'])
    base = bd.base.reshape(-1, 3)
    col[skin_px] = np.clip(base[skin_px] * (tone / bd.skin_mean), 0, 1)
    # Sombrinha de contato: perto da borda de uma peça que está por cima.
    ao = np.ones(n)
    for li, m in enumerate(margins):
        if m is None: continue
        below = (owner > li) | (owner < 0)
        near = below & (m < 0) & (m > -0.022)
        ao[near] = np.minimum(ao[near], 1 - 0.38 * (1 - (-m[near]) / 0.022) ** 2)
    col *= ao[:, None]
    return np.clip(col, 0, 1).reshape(RES, RES, 3), hgt.reshape(RES, RES), owner.reshape(RES, RES)


def normal_map(bd, hgt, owner):
    nb = bd.normal.copy()
    cloth = owner >= 0
    flat = np.array([0, 0, 1.0])
    nb[cloth] = nb[cloth] * 0.12 + flat * 0.88
    h = ndimage.gaussian_filter(hgt, 0.6)
    dy, dx = np.gradient(h)
    k = 0.55
    det = np.stack([-dx * k, dy * k, np.zeros_like(h)], -1)
    det[~cloth] = 0
    n = nb + det
    n /= np.maximum(np.linalg.norm(n, axis=-1, keepdims=True), 1e-9)
    return n


# ---------------- textura das peças da cabeça ----------------
def paint_acc(acc):
    S = ACC_RES
    img = np.zeros((S, S, 3))
    yy, xx = np.mgrid[0:S, 0:S]
    u = (xx + 0.5) / S; v = (yy + 0.5) / S
    casco = R.hex2rgb(acc.get('casco', '#333333'))
    pat = acc.get('padrao', 'liso')
    P3 = np.stack([u * 6, v * 3, np.zeros_like(u)], -1).reshape(-1, 3)
    col = np.tile(casco, (S * S, 1))
    if pat == 'camuflado':
        class Fake: pass
        cols = [acc['casco']] + acc.get('cores', [])
        col = np.tile(R.hex2rgb(cols[0]), (S * S, 1))
        for i, cc in enumerate(cols[1:]):
            warp = R.fbm(P3 * 0.7 + 3.1 * i, 2, 5 * i)
            nn = R.fbm(P3 + warp[:, None] * 0.9, 3, 11 * i)
            col[nn > 0.52 + 0.035 * i] = R.hex2rgb(cc)
    grain = R.vnoise(P3 * 60)
    big = R.fbm(P3 * 1.5, 3, 4)
    col = col * (0.92 + 0.1 * big + 0.06 * (grain - 0.5))[:, None]
    col = col.reshape(S, S, 3)
    if pat == 'trico':
        rib = 0.5 + 0.5 * np.cos(u * 2 * math.pi * 110)
        col *= (0.8 + 0.25 * rib)[..., None]
    if 'emblema' in acc:
        e = acc['emblema']
        a = (u - 0.25) * 0.55; bb = (0.62 - v) * 0.18
        mk = R.emblem_mask(e['forma'], a, bb, 0.032)
        col[mk] = R.hex2rgb(e['cor'])
    if acc.get('faixa_casco'):
        pass
    img[:, :S // 2] = col[:, :S // 2]
    aba = R.hex2rgb(acc.get('aba', acc.get('casco', '#333333')))
    img[:S // 2, S // 2:] = aba * (0.94 + 0.08 * big.reshape(S, S)[:S // 2, S // 2:, None])
    pal = {'lente': '#0c0f12', 'alca': '#121212', 'metal': '#8d9196', 'destaque': acc.get('destaque', '#c9ccd1'),
           'almofada': acc.get('almofada', '#262626'), 'faixa': acc.get('faixa', acc.get('casco', '#333333')),
           'mic': '#151515', 'botao': acc.get('aba', acc.get('casco', '#333333'))}
    for name, i in PC.PAL.items():
        x0 = S // 2 + (i % 4) * S // 8; y0 = S // 2 + (i // 4) * S // 8
        img[y0:y0 + S // 8, x0:x0 + S // 8] = R.hex2rgb(pal[name])
    return np.clip(img, 0, 1)


def save_jpg(img, path, q=90):
    Image.fromarray((np.clip(img, 0, 1) * 255 + 0.5).astype(np.uint8)).save(path, quality=q, subsampling=0)


def save_png(img, path):
    Image.fromarray((np.clip(img, 0, 1) * 255 + 0.5).astype(np.uint8)).save(path, optimize=True)


def save_normal(n, path):
    Image.fromarray(((n * 0.5 + 0.5) * 255 + 0.5).clip(0, 255).astype(np.uint8)).save(path, optimize=True)


# ---------------- principal ----------------
def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('ubc'); ap.add_argument('manequim'); ap.add_argument('saida')
    ap.add_argument('--previa', default=None)
    ap.add_argument('--so', default=None, help='gera só este agente (id)')
    args = ap.parse_args()
    man = Gltf(args.manequim)
    out = args.saida
    os.makedirs(os.path.join(out, 'Comum'), exist_ok=True)
    tdir = os.path.join(args.ubc, 'Base Characters', 'Textures')

    # Texturas comuns: atlas do cabelo (Hair_1 | Hair_2) e olho.
    h1 = load_rgb(os.path.join(tdir, 'T_Hair_1_BaseColor.png'), 1024)
    h2 = load_rgb(os.path.join(tdir, 'T_Hair_2_BaseColor.png'), 1024)
    atlas = np.concatenate([h1 / h1.mean() * HAIR_MEAN, h2 / h2.mean() * HAIR_MEAN], 1)
    save_jpg(atlas, os.path.join(out, 'Comum', 'T_Cabelo_Cor.jpg'), 92)
    n1 = load_rgb(os.path.join(tdir, 'Normals Unity - Godot', 'T_Hair_1_Normal.png'), 1024)
    n2 = load_rgb(os.path.join(tdir, 'Normals Unity - Godot', 'T_Hair_2_Normal.png'), 1024)
    save_png(np.concatenate([n1, n2], 1), os.path.join(out, 'Comum', 'T_Cabelo_Normal.png'))
    save_png(load_rgb(os.path.join(tdir, 'T_Eye_Brown.png')), os.path.join(out, 'Comum', 'T_Olho_Cor.png'))
    save_png(load_rgb(os.path.join(tdir, 'Normals Unity - Godot', 'T_Eye_Normal.png')), os.path.join(out, 'Comum', 'T_Olho_Normal.png'))

    bodies = {}
    manifest = {'versao': 1, 'cabeloCor': 'Comum/T_Cabelo_Cor.jpg', 'cabeloNormal': 'Comum/T_Cabelo_Normal.png',
                'olhoCor': 'Comum/T_Olho_Cor.png', 'olhoNormal': 'Comum/T_Olho_Normal.png', 'agentes': []}
    previews = []
    idle = man.tracks_by_name(man.anim('Idle_Loop'))
    for agent in AGENTES:
        if args.so and agent['id'] != args.so: continue
        print(f"agente {agent['nome']}...", flush=True)
        if agent['corpo'] not in bodies:
            bodies[agent['corpo']] = BodyData(args.ubc, man, agent['corpo'])
        bd = bodies[agent['corpo']]
        sk = bd.sk
        folder = slug(agent['nome'])
        adir = os.path.join(out, folder)
        os.makedirs(adir, exist_ok=True)
        head_k = sk.index['DEF-head']

        # Corpo com volume das roupas (só peças com grossura).
        b = bd.body
        geo_layers = [L for L in agent['roupas'] if L.get('grossura', R.THICK[L['tipo']]) > 0]
        nv = geometric_normals(b['pos'], b['idx'], bd.weld)
        vctx = R.Ctx(bd.measure, b['pos'], nv, bd.gw)
        off = R.vertex_offsets(geo_layers, vctx)
        off = np.bincount(bd.weld, off) / np.bincount(bd.weld)
        off = off[bd.weld]
        # Roupa folgada: alisa os músculos embaixo do pano antes de inchar.
        loose = R.looseness(geo_layers, vctx)
        smoothed = taubin(b['pos'], b['idx'], bd.weld, loose)
        nv = geometric_normals(smoothed, b['idx'], bd.weld)
        pos = smoothed + nv * off[:, None]
        nv2 = geometric_normals(pos, b['idx'], bd.weld)
        nrm = b['nrm'] + (nv2 - nv)
        nrm /= np.maximum(np.linalg.norm(nrm, axis=1, keepdims=True), 1e-9)

        # Cabelo (+ barba) + sobrancelhas.
        hp = [load_hair_prim(args.ubc, agent['cabelo'])]
        if agent.get('barba'): hp.append(load_hair_prim(args.ubc, 'Hair_Beard'))
        hp.append(bd.brows)
        hpos, hnrm, huv, hidx = hair_mesh(bd, hp)

        # Peças da cabeça.
        hm = bd.gw[:, R.GROUPS.index('head')] > 0.5
        head_pts = np.concatenate([b['pos'][hm & (b['pos'][:, 1] > bd.measure.eyes[1] - 0.06)], hp[0]['pos']])
        center = np.array([0.0, bd.measure.eyes[1] + 0.035, bd.measure.eyes[2] - 0.085])
        shape = PC.HeadShape(head_pts, center)
        pieces = {name: PC.build(name, shape, bd.measure) for name in agent.get('pecas', [])}

        # GLB: Corpo, Cabelo, Olhos, Peca_*.
        W = GlbWriter()
        mats = [{'name': 'M_Corpo', 'pbrMetallicRoughness': {'metallicFactor': 0.0, 'roughnessFactor': 0.8}},
                {'name': 'M_Cabelo', 'pbrMetallicRoughness': {'metallicFactor': 0.0, 'roughnessFactor': 0.7}},
                {'name': 'M_Olhos', 'pbrMetallicRoughness': {'metallicFactor': 0.0, 'roughnessFactor': 0.2}},
                {'name': 'M_Acessorios', 'pbrMetallicRoughness': {'metallicFactor': 0.0, 'roughnessFactor': 0.6}}]
        meshes = [('Corpo', W.primitive(pos, nrm, b['uv'], b['joints'], b['weights'], b['idx'], 0))]
        one_j = lambda n_: np.tile([head_k, 0, 0, 0], (n_, 1))
        one_w = lambda n_: np.tile([1.0, 0, 0, 0], (n_, 1))
        meshes.append(('Cabelo', W.primitive(hpos, hnrm, huv, one_j(len(hpos)), one_w(len(hpos)), hidx, 1)))
        e = bd.eyes
        meshes.append(('Olhos', W.primitive(e['pos'], e['nrm'], e['uv'], e['joints'], e['weights'], e['idx'], 2)))
        for name, (V, N, UV, T) in pieces.items():
            meshes.append(('Peca_' + name, W.primitive(V, N, UV, one_j(len(V)), one_w(len(V)), T, 3)))
        W.save(os.path.join(adir, folder + '.glb'), sk, meshes, mats)

        # Texturas por skin + normal do agente.
        entry = {'id': agent['id'], 'nome': agent['nome'], 'descricao': agent['descricao'],
                 'modelo': f'{folder}/{folder}.glb', 'normal': f'{folder}/T_{folder}_Normal.png',
                 'alturaCorpo': float(b['pos'][:, 1].max() - min(b['pos'][:, 1].min(), 0.0)),
                 'escalaQuadril': round(sk.hips_scale, 5),
                 'pecas': ['Peca_' + p for p in agent.get('pecas', [])], 'skins': []}
        for si, skin in enumerate(agent['skins']):
            print(f"   skin {skin['nome']}", flush=True)
            col, hgt, owner = paint_body(bd, agent, skin)
            sname = slug(skin['nome'])
            save_jpg(col, os.path.join(adir, f'T_{folder}_{sname}.jpg'))
            if si == 0:
                save_normal(normal_map(bd, hgt, owner), os.path.join(adir, f'T_{folder}_Normal.png'))
            acc_path = ''
            if agent.get('pecas'):
                acc_img = paint_acc(skin.get('acessorios', {}))
                acc_path = f'{folder}/T_{folder}_{sname}_Acessorios.png'
                save_png(acc_img, os.path.join(out, acc_path))
            hc = R.hex2rgb(skin['cabelo'])
            entry['skins'].append({'id': skin['id'], 'nome': skin['nome'], 'cor': f'{folder}/T_{folder}_{sname}.jpg',
                                   'acessorios': acc_path, 'corCabelo': [round(float(x), 4) for x in hc],
                                   'pecas': ['Peca_' + p for p in skin.get('pecas', [])],
                                   'esconderCabelo': bool(skin.get('esconder_cabelo', False))})
            if args.previa:
                Wp = sk.pose(idle, 0.4)
                parts = []
                P = sk.skin(Wp, pos, b['joints'], b['weights'])
                Nn = sk.skin(Wp, pos + nrm * 0.01, b['joints'], b['weights']) - P
                parts.append((P, Nn / np.linalg.norm(Nn, axis=1, keepdims=True), b['uv'], b['idx'], col))
                Hh = Wp[head_k] @ sk.ibm[head_k]
                if not skin.get('esconder_cabelo'):
                    hcol = atlas * hc
                    parts.append(((hpos @ Hh[:3, :3].T) + Hh[:3, 3], hnrm @ Hh[:3, :3].T, huv, hidx, hcol))
                Pe = sk.skin(Wp, e['pos'], e['joints'], e['weights'])
                parts.append((Pe, e['nrm'], e['uv'], e['idx'], (0.25, 0.17, 0.12)))
                for pname in skin.get('pecas', []):
                    V, N, UV, T = pieces[pname]
                    parts.append(((V @ Hh[:3, :3].T) + Hh[:3, 3], N @ Hh[:3, :3].T, UV, T, acc_img))
                previews.append((agent['nome'], skin['nome'], render(parts, 25, size=(300, 470)),
                                 render(parts, 205, size=(300, 470))))
        manifest['agentes'].append(entry)

    with open(os.path.join(out, 'Agentes.json'), 'w', encoding='utf-8') as f:
        json.dump(manifest, f, ensure_ascii=False, indent=1)
    if args.previa and previews:
        os.makedirs(args.previa, exist_ok=True)
        rows = {}
        for a, s, f, bk in previews: rows.setdefault(a, []).append(np.concatenate([f, bk], 1))
        for a, imgs in rows.items():
            Image.fromarray(np.concatenate(imgs, 1)).save(os.path.join(args.previa, f'previa_{slug(a)}.png'))
    print('pronto:', out)


if __name__ == '__main__':
    main()
