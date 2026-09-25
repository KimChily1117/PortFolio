"""Exercise the actual AssimpTool converter, including malformed SCB inputs."""
from pathlib import Path
import hashlib
import json
import math
import struct
import subprocess

ROOT = Path(__file__).resolve().parents[2]
TOOL = ROOT / 'Binaries/AssimpTool.exe'
SOURCE = ROOT / 'Resources/Textures/Annie/Particles/OriginalQ/annie_base_q_mis_01.scb'
OUTPUT = ROOT / 'Tests/AnnieQ/results/converter'
OUTPUT.mkdir(parents=True, exist_ok=True)
source = SOURCE.read_bytes()
cases = {'valid': source, 'truncated': source[:-1], 'bad_magic': b'BADMAGIC' + source[8:]}
bad_index = bytearray(source)
count, faces = struct.unpack_from('<II', source, 140)
struct.pack_into('<I', bad_index, 180 + count * 12 + 12, count)
cases['bad_index'] = bad_index
bad_float = bytearray(source)
struct.pack_into('<f', bad_float, 180, math.nan)
cases['nan_vertex'] = bad_float
results = {}
for name, data in cases.items():
    input_path = OUTPUT / (name + '.scb')
    output_path = OUTPUT / (name + '.vfxmesh')
    input_path.write_bytes(data)
    process = subprocess.run([str(TOOL), '--convert-scb-vfx', str(input_path), str(output_path)],
                             cwd=TOOL.parent, timeout=15)
    accepted = process.returncode == 0
    assert accepted == (name == 'valid'), name
    results[name] = 'pass'
mesh = (OUTPUT / 'valid.vfxmesh').read_bytes()
assert mesh == (ROOT / 'Resources/Models/Annie/Vfx/annie_base_q_mis_01.vfxmesh').read_bytes()
magic, version, stride, vertices, indices = struct.unpack_from('<4s4I', mesh)
assert (magic, version, stride) == (b'VFXM', 1, 44)
assert indices == faces * 3
assert len(mesh) == 20 + vertices * stride + indices * 4
# Compare every expanded output triangle's position/UV with the source. This
# catches welding across UV seams, swapped U/V arrays, and wrong axis conversion.
index_data = struct.unpack_from('<' + 'I' * indices, mesh, 20 + vertices * stride)
for face in range(faces):
    offset = 180 + count * 12 + 12 + face * 100
    source_indices = struct.unpack_from('<3I', source, offset)
    uv = struct.unpack_from('<6f', source, offset + 76)
    for corner, original in enumerate(source_indices):
        x, y, z = struct.unpack_from('<3f', source, 180 + original * 12)
        actual = struct.unpack_from('<5f', mesh, 20 + index_data[face * 3 + corner] * stride)
        assert actual == (x, z, y, uv[corner], uv[corner + 3])
results['triangle_position_uv_equivalence'] = 'pass'
results['deterministic_bake'] = 'pass'
results['vertices'] = vertices
results['bytes'] = len(mesh)
results['sha256'] = hashlib.sha256(mesh).hexdigest()
(OUTPUT / 'report.json').write_text(json.dumps(results, indent=2))
print(json.dumps(results))
