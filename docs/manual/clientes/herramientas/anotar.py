"""Anota las capturas de pantalla: recuadros rojos y circulos numerados (y recortes con ampliacion).

Entradas
  capturas.json    lista de {id, archivo, titulo, descripcion, ancho, alto, escalaDpi,
                   controles:[{nombre, tipo, texto, x, y, ancho, alto}]}  (coordenadas en pixeles de la imagen)
  anotaciones.json {"<id de captura>": [ {control, numero, tipo, leyenda, posicion} | {area:[x,y,ancho,alto], ...} ]}
Salida
  <salida>/<id>.png  (las capturas sin anotaciones se copian tal cual)

Uso
  python anotar.py [--capturas-json RUTA] [--originales CARPETA] [--anotaciones RUTA] [--salida CARPETA]
"""
from __future__ import annotations

import argparse
import json
import shutil
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

ROJO = (224, 32, 32, 255)
BLANCO = (255, 255, 255, 255)
SOBREDIMENSION = 2          # se dibuja al doble y se reduce (suavizado)
# Medidas en pixeles *tal como se ven en el documento* (96 por pulgada): una figura que se reduce o se
# amplia lleva circulos y trazos del mismo tamano fisico, siempre legibles.
GROSOR = 3
RADIO_ESQUINA = 6
RADIO_CIRCULO = 11
MARGEN_RECUADRO = 3
POSICIONES = ("arriba-izquierda", "arriba-derecha", "izquierda", "derecha")
ANCHO_PAGINA_PX = round(16 / 2.54 * 96)   # 16 cm = ancho maximo de una figura = 605 px
AMPLIACION_MAX = 2.0                      # un recorte se puede ampliar hasta 2x (remuestreo LANCZOS)

AQUI = Path(__file__).resolve().parent
RAIZ = AQUI.parent


class ErrorAnotacion(Exception):
    pass


def _fuente_negrita(tam: int) -> ImageFont.FreeTypeFont:
    for nombre in ("segoeuib.ttf", "calibrib.ttf", "arialbd.ttf", "DejaVuSans-Bold.ttf"):
        try:
            return ImageFont.truetype(nombre, tam)
        except OSError:
            continue
    return ImageFont.load_default()


def _rect_de(spec: dict, controles: dict[str, dict], id_captura: str) -> tuple[int, int, int, int]:
    if "area" in spec:
        x, y, w, h = spec["area"]
        return int(x), int(y), int(w), int(h)
    nombre = spec.get("control")
    if nombre not in controles:
        disponibles = ", ".join(sorted(controles)) or "(ninguno)"
        raise ErrorAnotacion(f"[{id_captura}] el control '{nombre}' no existe en capturas.json. Controles: {disponibles}")
    c = controles[nombre]
    return int(c["x"]), int(c["y"]), int(c["ancho"]), int(c["alto"])


def limitar_rect(rect, ancho_img: int, alto_img: int, id_captura: str) -> tuple[int, int, int, int]:
    x, y, w, h = rect
    x0, y0 = max(0, x), max(0, y)
    x1, y1 = min(ancho_img, x + w), min(alto_img, y + h)
    if x1 - x0 < 8 or y1 - y0 < 8:
        raise ErrorAnotacion(f"[{id_captura}] el recorte {tuple(rect)} queda fuera de la imagen ({ancho_img}x{alto_img}) o es demasiado pequeno")
    return x0, y0, x1 - x0, y1 - y0


def rect_de_controles(nombres: list[str], controles: dict[str, dict], margen: int, ancho_img: int, alto_img: int,
                      id_captura: str) -> tuple[int, int, int, int]:
    """Caja que contiene a todos los controles nombrados, con margen, recortada a la imagen."""
    x0 = y0 = 10 ** 9
    x1 = y1 = -10 ** 9
    for n in nombres:
        if n not in controles:
            disponibles = ", ".join(sorted(controles)) or "(ninguno)"
            raise ErrorAnotacion(f"[{id_captura}] recorte: el control '{n}' no existe en capturas.json. Controles: {disponibles}")
        c = controles[n]
        x0, y0 = min(x0, int(c["x"])), min(y0, int(c["y"]))
        x1, y1 = max(x1, int(c["x"]) + int(c["ancho"])), max(y1, int(c["y"]) + int(c["alto"]))
    return limitar_rect((x0 - margen, y0 - margen, x1 - x0 + 2 * margen, y1 - y0 + 2 * margen), ancho_img, alto_img, id_captura)


def plan_de_recorte(w: int, ancho_max_px: float | None = None) -> tuple[float, float]:
    """Para un recorte de w px: devuelve (ampliacion, ancho en pantalla en px). Hasta 2x, sin pasar de 16 cm."""
    tope = min(ANCHO_PAGINA_PX, ancho_max_px or ANCHO_PAGINA_PX)
    f = min(AMPLIACION_MAX, max(1.0, tope / w))
    return f, min(tope, w * f)


def _centro_circulo(rect, posicion, r, ancho_img, alto_img):
    x, y, w, h = rect
    if posicion == "arriba-derecha":
        cx, cy = x + w, y
    elif posicion == "izquierda":
        cx, cy = x, y + h / 2
    elif posicion == "derecha":
        cx, cy = x + w, y + h / 2
    else:  # arriba-izquierda, en la esquina exterior
        cx, cy = x, y
    # desplaza hacia fuera del control para que el circulo no tape el contenido
    d = r * 0.8
    if posicion == "arriba-izquierda":
        cx, cy = cx - d, cy - d
    elif posicion == "arriba-derecha":
        cx, cy = cx + d, cy - d
    elif posicion == "izquierda":
        cx = cx - r - 2
    elif posicion == "derecha":
        cx = cx + r + 2
    cx = min(max(cx, r + 1), ancho_img - r - 1)
    cy = min(max(cy, r + 1), alto_img - r - 1)
    return cx, cy


def anotar_imagen(origen: Path, destino: Path, specs: list[dict], controles: dict[str, dict], id_captura: str,
                  recorte: tuple[int, int, int, int] | None = None, ampliacion: float = 1.0,
                  ancho_en_pantalla: float | None = None) -> dict:
    """Dibuja las anotaciones.

    Con `recorte` (x, y, ancho, alto en pixeles de la captura) se recorta ANTES de dibujar y solo se dibujan las
    anotaciones que caen dentro; `ampliacion` > 1 amplia el recorte con LANCZOS. `ancho_en_pantalla` es el ancho
    (px a 96 por pulgada) con que se vera la imagen en el documento: sirve para que circulos y trazos tengan siempre
    el mismo tamano fisico. Devuelve {"dibujadas": [numeros], "omitidas": [numeros]}."""
    img = Image.open(origen).convert("RGBA")
    ox = oy = 0
    if recorte:
        ox, oy, rw, rh = limitar_rect(recorte, img.width, img.height, id_captura)
        img = img.crop((ox, oy, ox + rw, oy + rh))
    if ampliacion != 1.0:
        img = img.resize((round(img.width * ampliacion), round(img.height * ampliacion)), Image.LANCZOS)
    ancho, alto = img.size
    visual = (ancho_en_pantalla or min(ancho, ANCHO_PAGINA_PX)) / ancho      # px de pantalla por px de imagen
    m = 1.0 / visual                                                        # factor de compensacion
    k = SOBREDIMENSION
    grande = img.resize((ancho * k, alto * k), Image.LANCZOS)
    capa = Image.new("RGBA", grande.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(capa)
    r = RADIO_CIRCULO * m
    fuente = _fuente_negrita(max(8, int(r * 1.25 * k)))
    grosor = max(1, round(GROSOR * m * k))
    circulos = []
    resultado = {"dibujadas": [], "omitidas": []}
    for spec in specs:
        tipo = spec.get("tipo", "recuadro")
        if tipo not in ("circulo", "recuadro"):
            raise ErrorAnotacion(f"[{id_captura}] tipo '{tipo}' no valido (use circulo o recuadro)")
        posicion = spec.get("posicion", "arriba-izquierda")
        if posicion not in POSICIONES:
            raise ErrorAnotacion(f"[{id_captura}] posicion '{posicion}' no valida: {', '.join(POSICIONES)}")
        x, y, w, h = _rect_de(spec, controles, id_captura)
        x, y, w, h = (x - ox) * ampliacion, (y - oy) * ampliacion, w * ampliacion, h * ampliacion
        if x + w <= 0 or y + h <= 0 or x >= ancho or y >= alto:       # fuera del recorte: no se dibuja
            resultado["omitidas"].append(spec.get("numero"))
            continue
        rect = (x, y, w, h)
        if tipo == "recuadro":
            mr = MARGEN_RECUADRO * m
            caja = [(x - mr) * k, (y - mr) * k, (x + w + mr) * k, (y + h + mr) * k]
            medio = grosor / 2
            caja[0], caja[1] = max(caja[0], medio), max(caja[1], medio)
            caja[2], caja[3] = min(caja[2], ancho * k - medio), min(caja[3], alto * k - medio)
            d.rounded_rectangle(caja, radius=RADIO_ESQUINA * m * k, outline=ROJO, width=grosor)
        if spec.get("numero") is not None:
            circulos.append((rect, str(spec["numero"]), posicion))
        resultado["dibujadas"].append(spec.get("numero"))
    for rect, numero, posicion in circulos:   # los circulos van encima de todos los recuadros
        cx, cy = _centro_circulo(rect, posicion, r, ancho, alto)
        d.ellipse([(cx - r) * k, (cy - r) * k, (cx + r) * k, (cy + r) * k], fill=ROJO, outline=BLANCO,
                  width=max(1, round(2 * m * k)))
        d.text((cx * k, cy * k), numero, font=fuente, fill=BLANCO, anchor="mm")
    grande = Image.alpha_composite(grande, capa)
    salida = grande.resize((ancho, alto), Image.LANCZOS).convert("RGB")
    destino.parent.mkdir(parents=True, exist_ok=True)
    salida.save(destino, "PNG", optimize=True)
    return resultado


def cargar_json(ruta: Path):
    # utf-8-sig: tolera el BOM que dejan algunas herramientas de Windows
    return json.loads(ruta.read_text(encoding="utf-8-sig"))


def procesar(capturas_json: Path, originales: Path, anotaciones: Path | None, salida: Path) -> dict:
    capturas = cargar_json(capturas_json)
    if isinstance(capturas, dict):
        capturas = capturas.get("capturas", [])
    specs_por_id = cargar_json(anotaciones) if anotaciones and anotaciones.exists() else {}
    por_id = {c["id"]: c for c in capturas}
    desconocidas = sorted(set(specs_por_id) - set(por_id))
    if desconocidas:
        raise ErrorAnotacion("el archivo de anotaciones menciona capturas que no existen en capturas.json: " + ", ".join(desconocidas))
    salida.mkdir(parents=True, exist_ok=True)
    resumen = {"anotadas": 0, "copiadas": 0, "faltantes": []}
    for c in capturas:
        origen = originales / c["archivo"]
        if not origen.exists():
            resumen["faltantes"].append(c["id"])
            continue
        destino = salida / f"{c['id']}.png"
        specs = specs_por_id.get(c["id"])
        if specs:
            controles = {k["nombre"]: k for k in c.get("controles", [])}
            anotar_imagen(origen, destino, specs, controles, c["id"])
            resumen["anotadas"] += 1
        else:
            shutil.copyfile(origen, destino)
            resumen["copiadas"] += 1
    return resumen


def main(argv=None) -> int:
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--originales", type=Path, default=RAIZ / "capturas" / "originales")
    p.add_argument("--capturas-json", type=Path, default=None, help="por defecto <originales>/capturas.json")
    p.add_argument("--anotaciones", type=Path, required=True, help="p. ej. fuente/anotaciones-Operador.json")
    p.add_argument("--salida", type=Path, required=True)
    a = p.parse_args(argv)
    cj = a.capturas_json
    if cj is None:
        for cand in (a.originales / "capturas.json", RAIZ / "capturas" / "capturas.json"):
            if cand.exists():
                cj = cand
                break
    if cj is None or not cj.exists():
        print("ERROR: no se encontro capturas.json (use --capturas-json)", file=sys.stderr)
        return 2
    try:
        r = procesar(cj, a.originales, a.anotaciones, a.salida)
    except ErrorAnotacion as e:
        print(f"ERROR: {e}", file=sys.stderr)
        return 1
    print(f"Anotadas: {r['anotadas']}  copiadas sin cambios: {r['copiadas']}  faltantes: {len(r['faltantes'])}")
    if r["faltantes"]:
        print("  Sin archivo original: " + ", ".join(r["faltantes"]))
    return 0


if __name__ == "__main__":
    sys.exit(main())
