r"""Revisa los PDF: cuenta paginas y senala las que tienen mucho espacio vacio.

    herramientas\.venv\Scripts\python herramientas\revisar-paginas.py [Manual-*.pdf ...] [--umbral 40] [--detalle]

Una pagina esta "vacia" si el contenido (texto, imagenes, dibujos) ocupa menos de (100 - umbral)% del area util.
Se separan las paginas que terminan un capitulo (la siguiente empieza con un titulo grande): ahi es normal que sobre
espacio. Se ignoran la portada, el indice y la hoja de teclas.
"""
import sys
from pathlib import Path

import pymupdf

MM = 72 / 25.4
ARRIBA, ABAJO = 24 * MM, 22 * MM


def contenido(pagina):
    alto = pagina.rect.height
    util_top, util_bot = ARRIBA, alto - ABAJO
    fondo = util_top
    for b in pagina.get_text("dict")["blocks"]:
        y0, y1 = b["bbox"][1], b["bbox"][3]
        if y0 < util_top - 2 or y1 > util_bot + 2:
            continue  # encabezado / pie
        fondo = max(fondo, y1)
    for d in pagina.get_drawings():
        y1 = d["rect"].y1
        if d["rect"].y0 >= util_top - 2 and y1 <= util_bot + 2 and d["rect"].width < pagina.rect.width * 0.99:
            fondo = max(fondo, y1)
    return (fondo - util_top) / (util_bot - util_top)


def empieza_capitulo(pagina):
    for b in pagina.get_text("dict")["blocks"]:
        if b["bbox"][1] < ARRIBA - 8 or b["bbox"][1] > pagina.rect.height - ABAJO or "lines" not in b:
            continue
        return max((s["size"] for l in b["lines"] for s in l["spans"]), default=0) >= 24
    return False


def revisar(ruta, umbral, detalle):
    doc = pymupdf.open(ruta)
    n = len(doc)
    huecos, fines = [], []
    primero = next((i for i in range(1, n) if empieza_capitulo(doc[i])), 1)   # salta portada e indice
    for i in range(primero, n):
        p = doc[i]
        if "Hoja de teclas" in p.get_text()[:400]:
            continue
        uso = contenido(p)
        if uso < (100 - umbral) / 100:
            es_fin = (i + 1 < n and empieza_capitulo(doc[i + 1])) or i == n - 1
            (fines if es_fin else huecos).append((i + 1, round(100 - uso * 100)))
    print(f"{Path(ruta).name}: {n} paginas | vacias (>{umbral}%) fuera de fin de capitulo: {len(huecos)} | en fin de capitulo: {len(fines)}")
    if detalle:
        print("   huecos:", ", ".join(f"p{p}({v}%)" for p, v in huecos) or "-")
    return n, len(huecos), len(fines)


if __name__ == "__main__":
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    umbral = 40
    if "--umbral" in sys.argv:
        umbral = int(sys.argv[sys.argv.index("--umbral") + 1])
        args = [a for a in args if a != str(umbral)]
    pdfs = args or sorted(str(p) for p in Path(".").glob("Manual-*.pdf"))
    for f in pdfs:
        revisar(f, umbral, "--detalle" in sys.argv)
