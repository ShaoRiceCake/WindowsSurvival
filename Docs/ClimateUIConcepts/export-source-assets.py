"""Lossless export of an existing Unity sprite. No art recoloring or matting.

Uses the authored sprite rectangle and preserves every RGBA value from the PSD.
"""
from pathlib import Path
import re
from PIL import Image

root = Path(__file__).resolve().parents[2]
source = root / "Assets/Resources/Sprites/Resource.psd"
metadata = source.with_suffix(".psd.meta").read_text(encoding="utf-8")
match = re.search(
    r"name: Resource_1\s+rect:\s+serializedVersion: \d+\s+x: (\d+)\s+y: (\d+)\s+width: (\d+)\s+height: (\d+)",
    metadata,
)
if match is None:
    raise RuntimeError("Authored Resource_1 sprite rectangle is missing")
x, y, width, height = map(int, match.groups())
atlas = Image.open(source).convert("RGBA")
sprite = atlas.crop((x, atlas.height-y-height, x+width, atlas.height-y))
output = Path(__file__).resolve().parent / "assets/scrap.png"
sprite.save(output)
assert Image.open(output).tobytes() == sprite.tobytes()
assert sprite.getchannel("A").getextrema() == (0, 255)
print(f"Exported {output.name}: {width}x{height}, original transparent RGBA preserved")
