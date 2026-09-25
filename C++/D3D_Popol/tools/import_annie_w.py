"""Bake Annie W originals with the project's headless AssimpTool."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import struct
import subprocess

MESHES = 'annie_base_w_cone_1 annie_base_w_cone_1_edge annie_base_w_cone_1_edge2 annie_base_w_cone_2'.split()
TEXTURES = '''annie_base_flames2 annie_base_fireshapes_00 annie_base_w_mask
annie_base_w_erosionpack annie_base_w_grounddecalfinal annie_base_e_small_mote'''.split()

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('extract', type=Path)
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    source = args.extract / 'assets/characters/annie/skins/base/particles'
    target = root / 'Resources/Textures/Annie/Particles/OriginalW'
    names = [n + '.dds' for n in TEXTURES] + [n + '.scb' for n in MESHES]
    data = {name: (source / name).read_bytes() for name in names}
    for name in TEXTURES:
        assert data[name + '.dds'][:4] == b'DDS ', name
    tool = root / 'Binaries/AssimpTool.exe'
    manifest = {'source': str(source), 'effect': 'Annie_Base_W_cas_Right',
                'converter': 'AssimpTool --convert-scb-vfx source output --keep-axes',
                'files': {n: hashlib.sha256(d).hexdigest() for n, d in data.items()}, 'meshes': {}}
    for name in MESHES:
        baked = root / 'Resources/Models/Annie/Vfx' / (name + '.vfxmesh')
        subprocess.run([str(tool), '--convert-scb-vfx', str(source / (name + '.scb')),
                        str(baked), '--keep-axes'], cwd=tool.parent, check=True)
        mesh = baked.read_bytes()
        magic, version, stride, vertices, indices = struct.unpack_from('<4s4I', mesh)
        assert (magic, version, stride) == (b'VFXM', 1, 44)
        assert len(mesh) == 20 + vertices * stride + indices * 4
        manifest['meshes'][name] = {'vertices': vertices, 'indices': indices, 'bytes': len(mesh),
                                    'sha256': hashlib.sha256(mesh).hexdigest()}
    target.mkdir(parents=True, exist_ok=True)
    for name in names:
        shutil.copyfile(source / name, target / name)
    (target / 'manifest.json').write_text(json.dumps(manifest, indent=2), encoding='utf-8')
    print(json.dumps(manifest['meshes'], indent=2))

if __name__ == '__main__':
    main()
