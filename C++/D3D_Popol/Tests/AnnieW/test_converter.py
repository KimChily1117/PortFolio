"""Check every W triangle's native axes, UV seams and source BGRA colors."""
from pathlib import Path
import json
import math
import struct
import subprocess

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'Resources/Textures/Annie/Particles/OriginalW'
OUTPUT = ROOT / 'Tests/AnnieW/results'
OUTPUT.mkdir(parents=True, exist_ok=True)
TOOL = ROOT / 'Binaries/AssimpTool.exe'
report = {}
for path in SOURCE.glob('*.scb'):
    baked = OUTPUT / (path.stem + '.vfxmesh')
    subprocess.run([str(TOOL), '--convert-scb-vfx', str(path), str(baked), '--keep-axes'],
                   cwd=TOOL.parent, check=True, timeout=15)
    source, mesh = path.read_bytes(), baked.read_bytes()
    assert mesh == (ROOT / 'Resources/Models/Annie/Vfx' / baked.name).read_bytes()
    count, faces = struct.unpack_from('<II', source, 140)
    colors, = struct.unpack_from('<I', source, 176)
    assert colors == 1
    magic, version, stride, vertices, indices = struct.unpack_from('<4s4I', mesh)
    assert (magic, version, stride, indices) == (b'VFXM', 1, 44, faces * 3)
    assert len(mesh) == 20 + stride * vertices + indices * 4
    index_data = struct.unpack_from('<' + 'I' * indices, mesh, 20 + vertices * stride)
    alpha = []
    for face in range(faces):
        offset = 180 + count * 16 + 12 + face * 100
        source_indices = struct.unpack_from('<3I', source, offset)
        uv = struct.unpack_from('<6f', source, offset + 76)
        for corner, original in enumerate(source_indices):
            position = struct.unpack_from('<3f', source, 180 + original * 12)
            b,g,r,a = struct.unpack_from('<4B', source, 180 + count * 12 + original * 4)
            actual = struct.unpack_from('<11f', mesh, 20 + index_data[face * 3 + corner] * stride)
            expected = (*position, uv[corner], uv[corner + 3], r/255, g/255, b/255, a/255, 0, 0)
            assert all(math.isclose(x, y, abs_tol=1e-6) for x,y in zip(actual, expected)), (path, face, corner)
            alpha.append(actual[8])
    assert min(alpha) == 0 and max(alpha) == 1 and any(0 < a < 1 for a in alpha)
    report[path.stem] = {'triangle_position_uv_rgba': 'pass', 'deterministic_bake': 'pass',
                        'source_vertices': count, 'baked_vertices': vertices, 'bytes': len(mesh)}
assert len(report) == 4
(OUTPUT / 'converter-report.json').write_text(json.dumps(report, indent=2))
print(json.dumps(report))
