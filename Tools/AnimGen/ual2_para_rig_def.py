"""
Converte animações da Universal Animation Library 2 [Standard] (Quaternius, CC0) para o esqueleto "DEF-" do
manequim da UAL1 que o projeto usa (Assets/ThirdParty/Quaternius/UniversalAnimationLibrary).

Por que dá certo só renomeando: as duas versões têm o MESMO esqueleto (mesmas posições e rotações de repouso,
conferido osso a osso); só os nomes mudam (pelvis -> DEF-hips, upperarm_r -> DEF-upper_arm.R ...).
Os ossos "leaf" (pontas dos dedos e do pé) não existem no manequim do projeto e ficam de fora.

Saída: um .glb só com os ossos (sem malha) e as animações escolhidas. O glTFast importa como Legacy e o
menu Rogue > Criar cena Sandbox_Movement liga os clipes no personagem.

Uso (Git Bash, com Python 3 + numpy):
  python Tools/AnimGen/ual2_para_rig_def.py <UAL2_Standard.glb> <AnimationLibrary_Godot_Standard.gltf> <saida.glb> Slide_Loop ...
"""
import json
import os
import re
import struct
import sys

import numpy as np

COMPONENT = {5120: np.int8, 5121: np.uint8, 5122: np.int16, 5123: np.uint16, 5125: np.uint32, 5126: np.float32}
WIDTH = {'SCALAR': 1, 'VEC2': 2, 'VEC3': 3, 'VEC4': 4, 'MAT4': 16}


class Gltf:
    """Leitor mínimo de .glb/.gltf (só o que precisa: nós e animações)."""

    def __init__(self, path):
        if path.lower().endswith('.glb'):
            data = open(path, 'rb').read()
            json_len, _ = struct.unpack('<I4s', data[12:20])
            self.j = json.loads(data[20:20 + json_len])
            start = 20 + json_len
            bin_len, _ = struct.unpack('<I4s', data[start:start + 8])
            self.buffers = [data[start + 8:start + 8 + bin_len]]
        else:
            self.j = json.load(open(path, encoding='utf-8'))
            folder = os.path.dirname(path)
            self.buffers = [open(os.path.join(folder, b['uri']), 'rb').read() for b in self.j['buffers']]
        self.nodes = self.j['nodes']
        self.names = [n.get('name') for n in self.nodes]

    def accessor(self, index):
        a = self.j['accessors'][index]
        view = self.j['bufferViews'][a['bufferView']]
        buffer = self.buffers[view.get('buffer', 0)]
        offset = view.get('byteOffset', 0) + a.get('byteOffset', 0)
        width = WIDTH[a['type']]
        kind = COMPONENT[a['componentType']]
        stride = view.get('byteStride', 0)
        size = np.dtype(kind).itemsize * width
        if stride and stride != size:
            rows = [np.frombuffer(buffer, kind, width, offset + k * stride) for k in range(a['count'])]
            values = np.array(rows)
        else:
            values = np.frombuffer(buffer, kind, a['count'] * width, offset).reshape(a['count'], width)
        values = values.astype(np.float64)
        if a.get('normalized'):
            if kind == np.int16: values = np.maximum(values / 32767.0, -1)
            elif kind == np.int8: values = np.maximum(values / 127.0, -1)
        return values

    def animation(self, name):
        for a in self.j.get('animations', []):
            if a.get('name') == name:
                return a
        return None

    def tracks(self, animation):
        result = {}
        for channel in animation['channels']:
            sampler = animation['samplers'][channel['sampler']]
            key = (channel['target'].get('node'), channel['target']['path'])
            result[key] = (self.accessor(sampler['input'])[:, 0], self.accessor(sampler['output']))
        return result


def to_def_name(name):
    """Nome do osso na UAL2 (estilo Unreal) -> nome no manequim do projeto (DEF-). None = não existe lá."""
    fixed = {'Head': 'DEF-head', 'neck_01': 'DEF-neck', 'spine_01': 'DEF-spine.001', 'spine_02': 'DEF-spine.002',
             'spine_03': 'DEF-spine.003', 'pelvis': 'DEF-hips', 'root': 'root', 'Mannequin': 'Mannequin',
             'Armature': 'Rig'}
    if name in fixed:
        return fixed[name]
    m = re.match(r'^(index|middle|pinky|ring|thumb)_0(\d)_(l|r)$', name)
    if m:
        finger, k, side = m.groups()
        prefix = 'DEF-thumb' if finger == 'thumb' else 'DEF-f_' + finger
        return f'{prefix}.0{k}.{side.upper()}'
    m = re.match(r'^(hand|lowerarm|upperarm|clavicle|ball|foot|calf|thigh)_(l|r)$', name)
    if m:
        bone, side = m.groups()
        bone = {'hand': 'hand', 'lowerarm': 'forearm', 'upperarm': 'upper_arm', 'clavicle': 'shoulder',
                'ball': 'toe', 'foot': 'foot', 'calf': 'shin', 'thigh': 'thigh'}[bone]
        return f'DEF-{bone}.{side.upper()}'
    return None


def check_same_rest(source, target):
    """Confere que o esqueleto é o mesmo (repouso igual) antes de só renomear."""
    index = {n: i for i, n in enumerate(target.names)}
    worst = 0.0
    for i, name in enumerate(source.names):
        mapped = to_def_name(name)
        if mapped is None or mapped not in index:
            continue
        a, b = source.nodes[i], target.nodes[index[mapped]]
        ta, tb = np.array(a.get('translation', [0, 0, 0])), np.array(b.get('translation', [0, 0, 0]))
        ra, rb = np.array(a.get('rotation', [0, 0, 0, 1])), np.array(b.get('rotation', [0, 0, 0, 1]))
        worst = max(worst, np.abs(ta - tb).max(), min(np.abs(ra - rb).max(), np.abs(ra + rb).max()))
    if worst > 1e-3:
        raise SystemExit(f'Esqueletos diferentes (diferença {worst:.4f}): precisa de retarget de verdade.')


def main():
    if len(sys.argv) < 5:
        raise SystemExit(__doc__)
    source, target = Gltf(sys.argv[1]), Gltf(sys.argv[2])
    output, clips = sys.argv[3], sys.argv[4:]
    check_same_rest(source, target)

    nodes = []
    for n in target.nodes:  # mesmos nomes e hierarquia do manequim, sem malha/skin
        node = {'name': n['name']}
        for key in ('translation', 'rotation', 'scale', 'children'):
            if key in n:
                node[key] = n[key]
        nodes.append(node)
    target_index = {n: i for i, n in enumerate(target.names)}
    scene_nodes = target.j['scenes'][target.j.get('scene', 0)]['nodes']

    parts, accessors, views = [], [], []
    offset = [0]

    def add(values, kind, minmax=False):
        values = np.ascontiguousarray(values, dtype=np.float32)
        raw = values.tobytes()
        pad = (-len(raw)) % 4
        views.append({'buffer': 0, 'byteOffset': offset[0], 'byteLength': len(raw)})
        parts.append(raw + b'\x00' * pad)
        offset[0] += len(raw) + pad
        acc = {'bufferView': len(views) - 1, 'componentType': 5126, 'count': int(values.shape[0]), 'type': kind}
        if minmax:
            flat = values.reshape(values.shape[0], -1)
            acc['min'] = [float(x) for x in flat.min(0)]
            acc['max'] = [float(x) for x in flat.max(0)]
        accessors.append(acc)
        return len(accessors) - 1

    animations = []
    for clip in clips:
        anim = source.animation(clip)
        if anim is None:
            raise SystemExit(f'Animação "{clip}" não existe no arquivo da UAL2.')
        tracks = source.tracks(anim)
        duration = max(t[-1] for t, _ in tracks.values())
        constant_times = add(np.array([[0.0], [duration]]), 'SCALAR', True)
        time_cache, channels, samplers = {}, [], []
        for (node, path), (times, values) in sorted(tracks.items(), key=lambda kv: kv[0]):
            if path == 'scale':
                continue  # sempre 1
            mapped = to_def_name(source.names[node])
            if mapped is None or mapped in ('Rig', 'Mannequin'):
                continue
            values = values.copy()
            if path == 'rotation':
                for k in range(1, len(values)):  # sem troca de sinal entre quadros
                    if np.dot(values[k - 1], values[k]) < 0:
                        values[k] = -values[k]
                values /= np.linalg.norm(values, axis=1, keepdims=True)
            if np.abs(values - values[0]).max() < 1e-6:  # constante: só 2 chaves
                inp, out = constant_times, np.stack([values[0], values[0]])
            else:
                key = (len(times), float(times[0]), float(times[-1]))
                if key not in time_cache:
                    time_cache[key] = add(times.reshape(-1, 1), 'SCALAR', True)
                inp, out = time_cache[key], values
            samplers.append({'input': inp, 'output': add(out, 'VEC4' if path == 'rotation' else 'VEC3'),
                             'interpolation': 'LINEAR'})
            channels.append({'sampler': len(samplers) - 1, 'target': {'node': target_index[mapped], 'path': path}})
        animations.append({'name': clip, 'channels': channels, 'samplers': samplers})
        print(f'  {clip}: {duration:.2f} s, {len(channels)} canais')

    binary = b''.join(parts)
    gltf = {
        'asset': {'version': '2.0', 'generator': 'Rogue: UAL2 [Standard] (Quaternius, CC0) renomeado para o esqueleto DEF-'},
        'scene': 0,
        'scenes': [{'name': 'Scene', 'nodes': scene_nodes}],
        'nodes': nodes,
        'animations': animations,
        'accessors': accessors,
        'bufferViews': views,
        'buffers': [{'byteLength': len(binary)}],
    }
    text = json.dumps(gltf, separators=(',', ':')).encode()
    text += b' ' * ((-len(text)) % 4)
    total = 12 + 8 + len(text) + 8 + len(binary)
    with open(output, 'wb') as f:
        f.write(struct.pack('<4sII', b'glTF', 2, total))
        f.write(struct.pack('<I4s', len(text), b'JSON'))
        f.write(text)
        f.write(struct.pack('<I4s', len(binary), b'BIN\x00'))
        f.write(binary)
    print(f'ok: {output} ({total} bytes)')


if __name__ == '__main__':
    main()
