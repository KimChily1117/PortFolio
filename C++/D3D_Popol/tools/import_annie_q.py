"""Import Annie base Q assets; no third-party package required.

SCB layout reference (format only):
https://github.com/LeagueToolkit/LeagueToolkit/blob/main/src/LeagueToolkit/Core/Mesh/StaticMesh.cs
https://github.com/LeagueToolkit/LeagueToolkit/blob/main/src/LeagueToolkit/Core/Mesh/StaticMeshFace.cs
"""
import argparse
import hashlib
import json
import subprocess
from pathlib import Path
import shutil
import struct

TEXTURES = """annie_base_fireshapes_00 annie_base_glow2 annie_base_glow3
annie_base_glow4 annie_base_noise2 annie_base_q_ash annie_base_q_radgrad
annie_base_q_smoke_2x2 annie_base_q_sparks_2x2 annie_base_q_mis_01
annie_base_q_mis_flames annie_base_q_mis_smoke_trail annie_base_q_mis_trail
annie_base_shockwave annie_base_smokeerode annie_base_smoke_01
annie_base_wall_erosion annie_spirit_phoenix_flames
annie_spirit_phoenix_flames_mult annie_fire_glow""".split()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('extract', type=Path)
    args = parser.parse_args()
    source = args.extract / 'assets/characters/annie/skins/base/particles'
    target = Path(__file__).resolve().parents[1] / 'Resources/Textures/Annie/Particles/OriginalQ'
    names = [name + '.dds' for name in TEXTURES] + ['annie_base_q_mis_01.scb']
    # Validate everything before changing the destination.
    contents = {name: (source / name).read_bytes() for name in names}
    for name in names[:-1]:
        if contents[name][:4] != b'DDS ':
            raise ValueError('Invalid DDS: ' + name)
    root = Path(__file__).resolve().parents[1]
    tool = root / 'Binaries/AssimpTool.exe'
    baked = root / 'Resources/Models/Annie/Vfx/annie_base_q_mis_01.vfxmesh'
    subprocess.run([str(tool), '--convert-scb-vfx', str(source / names[-1]), str(baked)],
                   cwd=tool.parent, check=True)
    mesh = baked.read_bytes()
    magic, version, stride, vertex_count, index_count = struct.unpack_from('<4s4I', mesh)
    if magic != b'VFXM' or version != 1 or stride != 44 or len(mesh) != 20 + vertex_count * stride + index_count * 4:
        raise ValueError('Invalid AssimpTool output')
    target.mkdir(parents=True, exist_ok=True)
    for name in names:
        shutil.copyfile(source / name, target / name)
    manifest = {'source': str(source), 'effects': ['Annie_Base_Q_mis', 'Annie_Base_Q_tar'],
                'files': {name: hashlib.sha256(contents[name]).hexdigest() for name in names},
                'converter': 'AssimpTool --convert-scb-vfx', 'mesh_vertices': vertex_count,
                'mesh_indices': index_count, 'mesh_bytes': len(mesh),
                'baked_mesh_sha256': hashlib.sha256(mesh).hexdigest()}
    (target / 'manifest.json').write_text(json.dumps(manifest, indent=2), encoding='utf-8')
    print(f'Imported {len(names)} originals; converted {manifest["mesh_vertices"]} mesh vertices.')


if __name__ == '__main__':
    main()
