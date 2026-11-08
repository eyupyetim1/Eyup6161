#!/usr/bin/env python3
"""PPM önizlemelerini docs/ui altına PNG olarak kopyalar."""

import sys
from pathlib import Path

from PIL import Image

src = Path(sys.argv[1])
dst = Path(sys.argv[2])
dst.mkdir(parents=True, exist_ok=True)
for ppm in sorted(src.glob("*.ppm")):
    image = Image.open(ppm)
    image.save(dst / (ppm.stem + ".png"))
    print(ppm.name, image.size)
