r"""Convierte las paginas de un PDF a PNG para revisarlas a ojo.

    herramientas\.venv\Scripts\python herramientas\vista-previa.py Manual-Operador.pdf [carpeta] [dpi]
"""
import sys
from pathlib import Path

import pymupdf

pdf = Path(sys.argv[1])
salida = Path(sys.argv[2]) if len(sys.argv) > 2 else pdf.with_suffix("").parent / (pdf.stem + "-paginas")
dpi = int(sys.argv[3]) if len(sys.argv) > 3 else 70
salida.mkdir(parents=True, exist_ok=True)
doc = pymupdf.open(pdf)
for i, pagina in enumerate(doc, 1):
    pagina.get_pixmap(dpi=dpi).save(salida / f"p{i:02d}.png")
print(f"{len(doc)} paginas en {salida}")
