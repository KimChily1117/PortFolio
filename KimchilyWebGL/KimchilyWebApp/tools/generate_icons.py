"""Generate the matching PNG app icons with Python's standard library only."""
from pathlib import Path
import struct
import zlib

OUTPUT = Path(__file__).resolve().parents[1] / 'icons'
INK, MINT, ACCENT = (21, 49, 49), (198, 241, 220), (139, 189, 114)
POLYGON = [(146, 132), (208, 132), (208, 244), (303, 132), (379, 132), (269, 257), (385, 380), (303, 380), (208, 273), (208, 380), (146, 380)]

def inside(x, y):
    result = False
    for i, (ax, ay) in enumerate(POLYGON):
        bx, by = POLYGON[i - 1]
        if (ay > y) != (by > y) and x < (bx - ax) * (y - ay) / (by - ay) + ax:
            result = not result
    return result

def write_png(path, size, maskable=False):
    rows = bytearray()
    for py in range(size):
        rows.append(0)
        for px in range(size):
            samples = []
            for dy, dx in ((.25, .25), (.25, .75), (.75, .25), (.75, .75)):
                x, y = (px + dx) * 512 / size, (py + dy) * 512 / size
                if maskable: x, y = (x - 256) / .82 + 256, (y - 256) / .82 + 256
                color = ACCENT if (x - 365) ** 2 + (y - 139) ** 2 < 18 ** 2 else MINT if inside(x, y) else INK
                samples.append(color)
            rows.extend(sum(color[channel] for color in samples) // 4 for channel in range(3))
    def chunk(kind, data):
        return struct.pack('!I', len(data)) + kind + data + struct.pack('!I', zlib.crc32(kind + data) & 0xffffffff)
    path.write_bytes(b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('!2I5B', size, size, 8, 2, 0, 0, 0)) + chunk(b'IDAT', zlib.compress(rows, 9)) + chunk(b'IEND', b''))

if __name__ == '__main__':
    OUTPUT.mkdir(parents=True, exist_ok=True)
    for name, size, maskable in [('icon-192.png', 192, False), ('icon-512.png', 512, False), ('icon-maskable-512.png', 512, True), ('apple-touch-icon.png', 180, False)]:
        write_png(OUTPUT / name, size, maskable)
