import sys
from pathlib import Path
from PIL import Image
carpeta = Path(sys.argv[1]); desde = int(sys.argv[2]); hasta = int(sys.argv[3]); out = Path(sys.argv[4])
ims = [Image.open(carpeta / f"p{i:02d}.png") for i in range(desde, hasta + 1) if (carpeta / f"p{i:02d}.png").exists()]
w, h = ims[0].size
m = Image.new("RGB", (w * len(ims) + 10 * (len(ims) - 1), h), (120, 120, 120))
for k, im in enumerate(ims):
    m.paste(im, (k * (w + 10), 0))
m.save(out)
