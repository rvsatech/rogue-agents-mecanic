# -*- coding: utf-8 -*-
"""Leitura/escrita de glTF, esqueleto e prévia (renderizador simples) — parte do gerador de personagens."""
import json, struct, os, re, math
import numpy as np

CT = {5120: np.int8, 5121: np.uint8, 5122: np.int16, 5123: np.uint16, 5125: np.uint32, 5126: np.float32}
NC = {'SCALAR': 1, 'VEC2': 2, 'VEC3': 3, 'VEC4': 4, 'MAT4': 16}


class Gltf:
    """Leitor mínimo de .gltf/.glb (só o que o gerador usa)."""

    def __init__(self, path):
        if path.lower().endswith('.glb'):
            b = open(path, 'rb').read()
            clen, _ = struct.unpack('<I4s', b[12:20])
            self.j = json.loads(b[20:20 + clen])
            off = 20 + clen
            blen, _ = struct.unpack('<I4s', b[off:off + 8])
            self.bins = [b[off + 8:off + 8 + blen]]
        else:
            self.j = json.load(open(path, encoding='utf-8'))
            d = os.path.dirname(path)
            self.bins = [open(os.path.join(d, bf['uri']), 'rb').read() for bf in self.j['buffers']]
        self.nodes = self.j['nodes']
        self.names = [n.get('name', '') for n in self.nodes]
        self.parent = [-1] * len(self.nodes)
        for i, n in enumerate(self.nodes):
            for c in n.get('children', []):
                self.parent[c] = i

    def acc(self, i):
        a = self.j['accessors'][i]
        bv = self.j['bufferViews'][a['bufferView']]
        buf = self.bins[bv.get('buffer', 0)]
        off = bv.get('byteOffset', 0) + a.get('byteOffset', 0)
        n = NC[a['type']]; dt = CT[a['componentType']]
        cnt = a['count']; isz = np.dtype(dt).itemsize * n
        stride = bv.get('byteStride', 0)
        if stride and stride != isz:
            arr = np.array([np.frombuffer(buf, dt, n, off + k * stride) for k in range(cnt)])
        else:
            arr = np.frombuffer(buf, dt, cnt * n, off).reshape(cnt, n)
        arr = arr.astype(np.float64)
        if a.get('normalized'):
            if dt == np.uint8: arr = arr / 255.0
            elif dt == np.uint16: arr = arr / 65535.0
            elif dt == np.int16: arr = np.maximum(arr / 32767.0, -1)
            elif dt == np.int8: arr = np.maximum(arr / 127.0, -1)
        return arr

    def local(self, i):
        n = self.nodes[i]
        if 'matrix' in n:
            return np.array(n['matrix'], float).reshape(4, 4).T
        return trs(n.get('translation', [0, 0, 0]), n.get('rotation', [0, 0, 0, 1]), n.get('scale', [1, 1, 1]))

    def world(self):
        W = [None] * len(self.nodes)

        def rec(i, M):
            W[i] = M @ self.local(i)
            for c in self.nodes[i].get('children', []):
                rec(c, W[i])
        for r in self.j['scenes'][self.j.get('scene', 0)]['nodes']:
            rec(r, np.eye(4))
        return W

    def primitive(self, mesh_index, prim=0):
        p = self.j['meshes'][mesh_index]['primitives'][prim]
        a = p['attributes']
        out = {'pos': self.acc(a['POSITION']), 'nrm': self.acc(a['NORMAL']),
               'uv': self.acc(a['TEXCOORD_0']),
               'joints': self.acc(a['JOINTS_0']).astype(np.int64), 'weights': self.acc(a['WEIGHTS_0']),
               'idx': self.acc(p['indices']).astype(np.int64).reshape(-1, 3), 'material': p.get('material')}
        w = out['weights']; s = w.sum(1, keepdims=True); s[s == 0] = 1
        out['weights'] = w / s
        return out

    def anim(self, name):
        for a in self.j.get('animations', []):
            if a.get('name') == name:
                return a

    def tracks_by_name(self, a):
        tr = {}
        for ch in a['channels']:
            s = a['samplers'][ch['sampler']]
            node = ch['target'].get('node')
            tr[(self.names[node], ch['target']['path'])] = (self.acc(s['input'])[:, 0], self.acc(s['output']))
        return tr


# ---------- matemática ----------
def q2m(q):
    x, y, z, w = [float(v) for v in q]
    return np.array([[1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
                     [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
                     [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)]])


def m2q(m):
    t = m[0, 0] + m[1, 1] + m[2, 2]
    if t > 0:
        s = math.sqrt(t + 1.0) * 2
        q = [(m[2, 1] - m[1, 2]) / s, (m[0, 2] - m[2, 0]) / s, (m[1, 0] - m[0, 1]) / s, 0.25 * s]
    elif m[0, 0] > m[1, 1] and m[0, 0] > m[2, 2]:
        s = math.sqrt(1.0 + m[0, 0] - m[1, 1] - m[2, 2]) * 2
        q = [0.25 * s, (m[0, 1] + m[1, 0]) / s, (m[0, 2] + m[2, 0]) / s, (m[2, 1] - m[1, 2]) / s]
    elif m[1, 1] > m[2, 2]:
        s = math.sqrt(1.0 + m[1, 1] - m[0, 0] - m[2, 2]) * 2
        q = [(m[0, 1] + m[1, 0]) / s, 0.25 * s, (m[1, 2] + m[2, 1]) / s, (m[0, 2] - m[2, 0]) / s]
    else:
        s = math.sqrt(1.0 + m[2, 2] - m[0, 0] - m[1, 1]) * 2
        q = [(m[0, 2] + m[2, 0]) / s, (m[1, 2] + m[2, 1]) / s, 0.25 * s, (m[1, 0] - m[0, 1]) / s]
    q = np.array(q)
    q /= np.linalg.norm(q)
    if q[3] < 0: q = -q
    return q


def trs(t, q, s=(1, 1, 1)):
    M = np.eye(4)
    M[:3, :3] = q2m(q) @ np.diag(np.array(s, float))
    M[:3, 3] = t
    return M


def ortho(R):
    """Tira escala/ruído de uma matriz de rotação."""
    u, _, vt = np.linalg.svd(R)
    r = u @ vt
    if np.linalg.det(r) < 0:
        u[:, -1] *= -1; r = u @ vt
    return r


def map_name(n):
    """Nome do osso no padrão UE (UBC/UAL) -> nome DEF- do manequim do projeto (None = sem par)."""
    m = {'Head': 'DEF-head', 'neck_01': 'DEF-neck', 'spine_01': 'DEF-spine.001', 'spine_02': 'DEF-spine.002',
         'spine_03': 'DEF-spine.003', 'pelvis': 'DEF-hips', 'root': 'root'}
    if n in m: return m[n]
    r = re.match(r'^(index|middle|pinky|ring|thumb)_0(\d)_(l|r)$', n)
    if r:
        f, k, s = r.groups()
        pre = 'DEF-thumb' if f == 'thumb' else 'DEF-f_' + f
        return f'{pre}.0{k}.{s.upper()}'
    r = re.match(r'^(hand|lowerarm|upperarm|clavicle|ball|foot|calf|thigh)_(l|r)$', n)
    if r:
        b, s = r.groups()
        b = {'hand': 'hand', 'lowerarm': 'forearm', 'upperarm': 'upper_arm', 'clavicle': 'shoulder', 'ball': 'toe',
             'foot': 'foot', 'calf': 'shin', 'thigh': 'thigh'}[b]
        return f'DEF-{b}.{s.upper()}'
    return None


# ---------- esqueleto novo ----------
class Skeleton:
    """Esqueleto do UBC com nomes DEF- e as orientações de repouso do manequim (posições das juntas do UBC).
    Assim as rotações das animações do projeto valem do mesmo jeito; o comprimento dos ossos é o do UBC."""

    def __init__(self, ubc, man):
        sk = ubc.j['skins'][0]
        self.joint_nodes = sk['joints']
        ibm = ubc.acc(sk['inverseBindMatrices']).reshape(-1, 4, 4).transpose(0, 2, 1)
        bind = [np.linalg.inv(m) for m in ibm]
        self.src_names = [ubc.names[j] for j in self.joint_nodes]
        self.names = [map_name(n) or n for n in self.src_names]
        Wm = man.world()
        man_idx = {n: i for i, n in enumerate(man.names)}
        node_to_k = {j: k for k, j in enumerate(self.joint_nodes)}
        self.parent = []
        for j in self.joint_nodes:
            p = ubc.parent[j]
            self.parent.append(node_to_k.get(p, -1))
        self.world = []
        self.ubc_world = []
        for k, name in enumerate(self.names):
            Wu = bind[k].copy()
            Wu[:3, :3] = ortho(Wu[:3, :3])
            self.ubc_world.append(Wu)
            W = Wu.copy()
            if name in man_idx:
                W[:3, :3] = ortho(Wm[man_idx[name]][:3, :3])
            self.world.append(W)
        self.local = []
        for k in range(len(self.names)):
            p = self.parent[k]
            self.local.append(self.world[k] if p < 0 else np.linalg.inv(self.world[p]) @ self.world[k])
        self.ibm = [np.linalg.inv(W) for W in self.world]
        self.index = {n: k for k, n in enumerate(self.names)}
        # Ordem com pais antes dos filhos.
        self.order = []
        seen = set()

        def visit(k):
            if k in seen: return
            if self.parent[k] >= 0: visit(self.parent[k])
            seen.add(k); self.order.append(k)
        for k in range(len(self.names)): visit(k)
        # Quadril do manequim (as animações movem o quadril nessa escala).
        hm = man.local(man_idx['DEF-hips'])[:3, 3]
        self.hips_scale = float(np.linalg.norm(self.local[self.index['DEF-hips']][:3, 3]) / np.linalg.norm(hm))

    def pos(self, src_or_def):
        n = src_or_def if src_or_def in self.index else map_name(src_or_def)
        return self.world[self.index[n]][:3, 3].copy()

    def pose(self, tracks, t):
        """Mundo de cada osso numa pose da animação do manequim (rotações; posição só no quadril, escalada)."""
        W = [None] * len(self.names)
        for k in self.order:
            L = self.local[k].copy()
            name = self.names[k]
            rt = tracks.get((name, 'rotation'))
            if rt is not None:
                L[:3, :3] = q2m(sample(rt, t, True))
            if name == 'DEF-hips':
                tt = tracks.get((name, 'translation'))
                if tt is not None:
                    L[:3, 3] = sample(tt, t, False) * self.hips_scale
            p = self.parent[k]
            W[k] = L if p < 0 else W[p] @ L
        return W

    def skin(self, W, pos, joints, weights):
        S = np.stack([W[k] @ self.ibm[k] for k in range(len(W))])
        P = np.c_[pos, np.ones(len(pos))]
        out = np.zeros((len(pos), 3))
        for c in range(4):
            M = S[joints[:, c]]
            out += weights[:, c:c + 1] * np.einsum('nij,nj->ni', M, P)[:, :3]
        return out


def sample(track, t, quat):
    times, vals = track
    if len(times) == 1 or t <= times[0]: v = vals[0]
    elif t >= times[-1]: v = vals[-1]
    else:
        i = int(np.searchsorted(times, t)) - 1
        f = (t - times[i]) / max(times[i + 1] - times[i], 1e-9)
        a, b = vals[i], vals[i + 1]
        if quat and np.dot(a, b) < 0: b = -b
        v = a * (1 - f) + b * f
    if quat: v = v / np.linalg.norm(v)
    return v


# ---------- escrita de GLB ----------
class GlbWriter:
    def __init__(self):
        self.bin = bytearray(); self.views = []; self.accessors = []

    def _add(self, arr, ctype, typ, minmax=False, target=None):
        data = np.ascontiguousarray(arr).tobytes()
        while len(self.bin) % 4: self.bin.append(0)
        view = {'buffer': 0, 'byteOffset': len(self.bin), 'byteLength': len(data)}
        if target: view['target'] = target
        self.views.append(view); self.bin.extend(data)
        acc = {'bufferView': len(self.views) - 1, 'componentType': ctype, 'count': int(arr.shape[0]), 'type': typ}
        if minmax:
            flat = np.asarray(arr, np.float64).reshape(arr.shape[0], -1)
            acc['min'] = [float(x) for x in flat.min(0)]; acc['max'] = [float(x) for x in flat.max(0)]
        self.accessors.append(acc)
        return len(self.accessors) - 1

    def primitive(self, pos, nrm, uv, joints, weights, idx, material):
        a = {'POSITION': self._add(pos.astype(np.float32), 5126, 'VEC3', True, 34962),
             'NORMAL': self._add(nrm.astype(np.float32), 5126, 'VEC3', False, 34962),
             'TEXCOORD_0': self._add(uv.astype(np.float32), 5126, 'VEC2', False, 34962),
             'JOINTS_0': self._add(joints.astype(np.uint16), 5123, 'VEC4', False, 34962),
             'WEIGHTS_0': self._add(weights.astype(np.float32), 5126, 'VEC4', False, 34962)}
        ind = self._add(idx.reshape(-1).astype(np.uint32), 5125, 'SCALAR', False, 34963)
        return {'attributes': a, 'indices': ind, 'material': material}

    def matrices(self, mats):
        arr = np.stack([m.T.reshape(16) for m in mats]).astype(np.float32)  # coluna por coluna
        return self._add(arr, 5126, 'MAT4')

    def save(self, path, skel, meshes, materials):
        """meshes: lista de (nome, primitiva). Nó 0 = 'Rig' (pai do 'root' e das malhas)."""
        nodes = [{'name': 'Rig', 'children': []}]
        bone_node = {}
        for k in range(len(skel.names)):
            bone_node[k] = len(nodes)
            L = skel.local[k]
            q = m2q(ortho(L[:3, :3]))
            nodes.append({'name': skel.names[k], 'translation': [float(x) for x in L[:3, 3]],
                          'rotation': [float(x) for x in q]})
        for k in range(len(skel.names)):
            p = skel.parent[k]
            if p < 0: nodes[0]['children'].append(bone_node[k])
            else: nodes[bone_node[p]].setdefault('children', []).append(bone_node[k])
        skin = {'joints': [bone_node[k] for k in range(len(skel.names))],
                'inverseBindMatrices': self.matrices(skel.ibm), 'skeleton': bone_node[skel.index['root']]}
        gl_meshes = []
        for name, prim in meshes:
            gl_meshes.append({'name': name, 'primitives': [prim]})
            nodes.append({'name': name, 'mesh': len(gl_meshes) - 1, 'skin': 0})
            nodes[0]['children'].append(len(nodes) - 1)
        while len(self.bin) % 4: self.bin.append(0)
        # Segunda raiz vazia: com 2 raízes o glTFast (importador da Unity) cria um objeto-pai em cima do "Rig",
        # igual ao do manequim (que tem animação). Os caminhos das animações ("Rig/root/DEF-hips/...") partem
        # desse pai; sem ele, a raiz do modelo seria o próprio "Rig" e as animações não achariam os ossos.
        nodes.append({'name': 'Info_Agente'})
        gltf = {'asset': {'version': '2.0', 'generator': 'Rogue CharGen (UBC Quaternius CC0)'},
                'scene': 0, 'scenes': [{'name': 'Agente', 'nodes': [0, len(nodes) - 1]}], 'nodes': nodes,
                'meshes': gl_meshes, 'skins': [skin],
                'materials': materials, 'accessors': self.accessors, 'bufferViews': self.views,
                'buffers': [{'byteLength': len(self.bin)}]}
        js = json.dumps(gltf, separators=(',', ':')).encode('utf-8')
        while len(js) % 4: js += b' '
        out = struct.pack('<III', 0x46546C67, 2, 12 + 8 + len(js) + 8 + len(self.bin))
        out += struct.pack('<I4s', len(js), b'JSON') + js + struct.pack('<I4s', len(self.bin), b'BIN\x00') + bytes(self.bin)
        open(path, 'wb').write(out)


# ---------- prévia (rasterizador simples) ----------
def render(parts, yaw_deg, size=(360, 520), height=1.95, light=(-0.45, 0.55, 0.7), bg=(236, 238, 241),
           cull=False, center=None):
    """parts: lista de (pos Nx3, nrm Nx3, uv Nx2, tri Mx3, textura HxWx3 float 0..1 ou cor)."""
    W, H = size
    img = np.empty((H, W, 3)); img[:] = np.array(bg) / 255.0
    zbuf = np.full((H, W), -1e9)
    a = math.radians(yaw_deg)
    R = np.array([[math.cos(a), 0, math.sin(a)], [0, 1, 0], [-math.sin(a), 0, math.cos(a)]])
    scale = H / height
    L = np.array(light, float); L /= np.linalg.norm(L)
    for pos, nrm, uv, tri, tex in parts:
        v = pos @ R.T; n = nrm @ R.T
        if center is not None:
            v = v - (np.asarray(center) @ R.T) + np.array([0, height * 0.5, 0])
        sx = W * 0.5 + v[:, 0] * scale
        sy = H - 12 - v[:, 1] * scale
        if cull:
            fz = np.cross(v[tri[:, 1]] - v[tri[:, 0]], v[tri[:, 2]] - v[tri[:, 0]])[:, 2]
            tri = tri[fz > 0]
        sz = v[:, 2]
        lit = np.clip(n @ L, 0, 1) * 0.75 + 0.33 + 0.12 * np.clip(n[:, 2], 0, 1)
        textured = isinstance(tex, np.ndarray) and tex.ndim == 3
        if textured: th, tw = tex.shape[:2]
        for t in tri:
            i0, i1, i2 = t
            x0, y0, x1, y1, x2, y2 = sx[i0], sy[i0], sx[i1], sy[i1], sx[i2], sy[i2]
            area = (x1 - x0) * (y2 - y0) - (x2 - x0) * (y1 - y0)
            if abs(area) < 1e-9: continue
            xmin = max(int(math.floor(min(x0, x1, x2))), 0); xmax = min(int(math.ceil(max(x0, x1, x2))), W - 1)
            ymin = max(int(math.floor(min(y0, y1, y2))), 0); ymax = min(int(math.ceil(max(y0, y1, y2))), H - 1)
            if xmin > xmax or ymin > ymax: continue
            gx, gy = np.meshgrid(np.arange(xmin, xmax + 1) + 0.5, np.arange(ymin, ymax + 1) + 0.5)
            w0 = ((x1 - gx) * (y2 - gy) - (x2 - gx) * (y1 - gy)) / area
            w1 = ((x2 - gx) * (y0 - gy) - (x0 - gx) * (y2 - gy)) / area
            w2 = 1 - w0 - w1
            m = (w0 >= -1e-4) & (w1 >= -1e-4) & (w2 >= -1e-4)
            if not m.any(): continue
            z = w0 * sz[i0] + w1 * sz[i1] + w2 * sz[i2]
            ys, xs = np.nonzero(m)
            ys = ys + ymin; xs = xs + xmin
            zz = z[m]
            closer = zz > zbuf[ys, xs]
            if not closer.any(): continue
            ys = ys[closer]; xs = xs[closer]; zz = zz[closer]
            b0 = w0[m][closer]; b1 = w1[m][closer]; b2 = w2[m][closer]
            sh = b0 * lit[i0] + b1 * lit[i1] + b2 * lit[i2]
            if textured:
                u = b0 * uv[i0, 0] + b1 * uv[i1, 0] + b2 * uv[i2, 0]
                vv = b0 * uv[i0, 1] + b1 * uv[i1, 1] + b2 * uv[i2, 1]
                col = tex[np.clip((vv * th).astype(int), 0, th - 1), np.clip((u * tw).astype(int), 0, tw - 1)]
            else:
                col = np.tile(np.array(tex, float), (len(zz), 1))
            zbuf[ys, xs] = zz
            img[ys, xs] = np.clip(col * sh[:, None], 0, 1)
    return (img * 255).astype(np.uint8)
