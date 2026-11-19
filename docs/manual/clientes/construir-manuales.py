"""Punto unico de entrada para generar los manuales de cliente (Word, HTML y PDF).

    python construir-manuales.py                  # todos los fuente/manual-*.md (menos manual-ejemplo.md)
    python construir-manuales.py --solo Operador  # un solo manual
    python construir-manuales.py --origen CARPETA # copia primero las capturas originales desde CARPETA
    python construir-manuales.py --muestra        # compila fuente/manual-ejemplo.md con imagenes de muestra

Detalles en README.md. Requisitos: Python 3.10+ con Pillow, Node.js, Microsoft Edge y, opcionalmente,
PyMuPDF (herramientas/.venv) para poner numeros de pagina en el indice del PDF.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import os
import shutil
import subprocess
import sys
import tempfile
import time
from pathlib import Path

RAIZ = Path(__file__).resolve().parent
HERR = RAIZ / "herramientas"
sys.path.insert(0, str(HERR))

# Si PyMuPDF no esta en este Python pero existe el entorno virtual local, se relanza con el.
try:
    import pymupdf  # noqa: F401
except ImportError:
    venv_py = HERR / ".venv" / "Scripts" / "python.exe"
    if venv_py.exists() and os.environ.get("MANUALES_REEJECUTADO") != "1" and Path(sys.executable).resolve() != venv_py.resolve():
        os.environ["MANUALES_REEJECUTADO"] = "1"
        sys.exit(subprocess.call([str(venv_py), str(Path(__file__).resolve()), *sys.argv[1:]]))
    pymupdf = None  # type: ignore

from PIL import Image, ImageDraw  # noqa: E402

import anotar  # noqa: E402
import fuente_md  # noqa: E402
import render_html  # noqa: E402

EDGE_CANDIDATOS = [
    r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
    r"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
]
# Carpeta donde el guion de capturas deja sus PNG (se usa solo si capturas/originales esta vacia).
ORIGEN_POR_DEFECTO = os.environ.get("PESALAB_CAPTURAS_ORIGEN", "")
ANCHO_MAX_CM = 16.0
TMP = HERR / "_tmp"


def info(msg: str) -> None:
    print(msg, flush=True)


# ----------------------------------------------------------------------------- capturas
def copiar_originales(origen: Path | None, destino: Path) -> None:
    if origen is None or not origen.exists():
        return
    destino.mkdir(parents=True, exist_ok=True)
    n = 0
    for f in origen.iterdir():
        if f.is_file() and f.suffix.lower() in (".png", ".json", ".jpg"):
            d = destino / f.name
            if not d.exists() or f.stat().st_mtime > d.stat().st_mtime or f.stat().st_size != d.stat().st_size:
                shutil.copyfile(f, d)
                n += 1
    info(f"Capturas originales copiadas desde {origen}: {n} archivo(s) nuevos o actualizados")


class Capturas:
    """Localiza imagenes por id y conoce su escala (capturas.json) y sus anotaciones."""

    def __init__(self, carpetas: list[Path], capturas_json: Path | None, anotaciones: Path | None, permitir_faltantes: bool,
                 originales: Path, recortes: Path):
        self.carpetas = carpetas
        self.originales = originales
        self.recortes = recortes
        self.meta: dict[str, dict] = {}
        if capturas_json and capturas_json.exists():
            datos = anotar.cargar_json(capturas_json)
            if isinstance(datos, dict):
                datos = datos.get("capturas", [])
            self.meta = {c["id"]: c for c in datos}
        self.anot: dict = anotar.cargar_json(anotaciones) if anotaciones and anotaciones.exists() else {}
        self.permitir_faltantes = permitir_faltantes
        self.faltantes: list[str] = []

    def localizar(self, idimg: str) -> Path | None:
        for c in self.carpetas:
            for ext in (".png", ".jpg"):
                p = c / f"{idimg}{ext}"
                if p.exists():
                    return p
        return None

    def faltantes_contiene(self, ruta: Path) -> bool:
        return ruta.parent == TMP

    def marcador(self, idimg: str) -> Path:
        TMP.mkdir(parents=True, exist_ok=True)
        p = TMP / f"FALTA-{idimg}.png"
        im = Image.new("RGB", (900, 420), (240, 240, 240))
        d = ImageDraw.Draw(im)
        d.rectangle([2, 2, 897, 417], outline=(200, 0, 0), width=4)
        d.text((40, 180), f"FALTA LA CAPTURA: {idimg}", fill=(200, 0, 0))
        im.save(p)
        return p


def recortar(b: dict, caps: "Capturas", meta: dict, specs: list[dict]):
    """Recorta (y amplia hasta 2x) una captura ANTES de dibujar las anotaciones. Devuelve (ruta, w, h, cm, omitidas)."""
    original = caps.originales / meta.get("archivo", f"{b['id']}.png")
    if not original.exists():
        raise anotar.ErrorAnotacion(f"[{b['id']}] no existe el archivo original para recortar: {original.name}")
    with Image.open(original) as im:
        W, H = im.size
    controles = {k["nombre"]: k for k in meta.get("controles", [])}
    rc = b["recorte"]
    if rc["modo"] == "rect":
        rect = anotar.limitar_rect(rc["rect"], W, H, b["id"])
    else:
        rect = anotar.rect_de_controles(rc["controles"], controles, rc["margen"], W, H, b["id"])
    tope_px = b["ancho_cm"] / 2.54 * 96 if b["ancho_cm"] else None
    f, ancho_px = anotar.plan_de_recorte(rect[2], tope_px)
    clave = hashlib.sha1(json.dumps([rect, round(f, 4), round(ancho_px, 2), specs], sort_keys=True).encode()).hexdigest()[:8]
    destino = caps.recortes / f"{b['id']}~{clave}.png"
    res = anotar.anotar_imagen(original, destino, specs, controles, b["id"], recorte=rect, ampliacion=f, ancho_en_pantalla=ancho_px)
    with Image.open(destino) as im:
        w, h = im.size
    return destino, w, h, ancho_px / 96.0 * 2.54, res["omitidas"]


def preparar_imagenes(ast: dict, caps: Capturas, nombre: str) -> None:
    figura = 0
    errores: list[str] = []
    for b in fuente_md.recorrer_imagenes(ast["bloques"]):
        figura += 1
        b["numero_figura"] = figura
        b["epigrafe_plano"] = fuente_md.texto_plano(b["epigrafe"])
        ruta = caps.localizar(b["id"])
        if ruta is None:
            if not caps.permitir_faltantes:
                errores.append(f"{nombre}:{b['linea']}: no existe la captura '{b['id']}'")
                b.update({"ruta": "", "w_px": 1, "h_px": 1, "ancho_final_cm": 1, "leyenda": []})
                continue
            caps.faltantes.append(b["id"])
            ruta = caps.marcador(b["id"])
        meta = caps.meta.get(b["id"], {})
        specs = caps.anot.get(b["id"], [])
        por_numero = {s["numero"]: s for s in specs if s.get("numero") is not None}
        omitidas: list = []
        if b.get("recorte") and not caps.faltantes_contiene(ruta):
            try:
                ruta, w, h, ancho, omitidas = recortar(b, caps, meta, specs)
            except anotar.ErrorAnotacion as e:
                errores.append(f"{nombre}:{b['linea']}: {e}")
                b.update({"ruta": "", "w_px": 1, "h_px": 1, "ancho_final_cm": 1, "leyenda": []})
                continue
        else:
            with Image.open(ruta) as im:
                w, h = im.size
            esc = float(meta.get("escalaDpi") or 1.0)
            esc = esc / 96.0 if esc > 5 else esc          # acepta 1.25 o 120
            nativo_cm = w / (96.0 * esc) * 2.54
            ancho = min(nativo_cm, ANCHO_MAX_CM)
            if b["ancho_cm"]:
                ancho = min(ancho, b["ancho_cm"])         # nunca se agranda por encima del tamano nativo
        b.update({"ruta": str(ruta), "w_px": w, "h_px": h, "ancho_final_cm": round(ancho, 2)})
        leyenda = []
        for n in b["callouts"]:
            if n not in por_numero:
                errores.append(f"{nombre}:{b['linea']}: la imagen '{b['id']}' pide el numero {n} pero el archivo de anotaciones no lo define")
            elif n in omitidas:
                info(f"  AVISO: {nombre}:{b['linea']}: el numero {n} de '{b['id']}' cae fuera del recorte y no se dibuja")
            elif por_numero[n].get("leyenda"):
                leyenda.append([n, por_numero[n]["leyenda"]])
        for n in omitidas:
            if n not in b["callouts"]:
                info(f"  AVISO: {nombre}:{b['linea']}: la anotacion {n} de '{b['id']}' cae fuera del recorte y se omite")
        b["leyenda"] = leyenda
    if errores:
        raise fuente_md.ErrorFormato("\n".join(errores))


# ----------------------------------------------------------------------------- PDF
def encontrar_edge() -> str:
    for c in EDGE_CANDIDATOS:
        if Path(c).exists():
            return c
    raise SystemExit("No se encontro Microsoft Edge (msedge.exe).")


def html_a_pdf(html: Path, pdf: Path) -> None:
    perfil = Path(tempfile.mkdtemp(prefix="edge-manual-"))
    try:
        if pdf.exists():
            pdf.unlink()
        cmd = [encontrar_edge(), "--headless", "--disable-gpu", "--no-first-run", f"--user-data-dir={perfil}",
               "--no-pdf-header-footer", "--hide-scrollbars", f"--print-to-pdf={pdf}", html.resolve().as_uri()]
        # Edge a veces termina al instante sin escribir el PDF (sobre todo en la primera llamada): se reintenta.
        for _ in range(5):
            subprocess.run(cmd, check=False, timeout=240, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
            if pdf.exists() and pdf.stat().st_size >= 1000:
                return
            time.sleep(3)
        raise SystemExit(f"Edge no genero el PDF: {pdf}")
    finally:
        shutil.rmtree(perfil, ignore_errors=True)


def paginas_del_indice(pdf: Path, toc: list[dict]) -> dict[str, int] | None:
    """Lee del PDF los destinos con nombre (los titulos con id) y devuelve {id de titulo: numero de pagina}."""
    if pymupdf is None:
        return None
    doc = pymupdf.open(pdf)
    destinos = doc.resolve_names()
    doc.close()
    res = {}
    for e in toc:
        d = destinos.get(e["id"])
        if d is None or d.get("page", -1) < 0:
            info(f"  AVISO: el PDF no tiene el destino '{e['id']}'; el indice queda sin numeros de pagina")
            return None
        res[e["id"]] = d["page"] + 1
    return res


# ----------------------------------------------------------------------------- un manual
def construir(fuente: Path, args, entorno: dict, salida: Path, logo: Path | None) -> list[str]:
    ast = fuente_md.analizar(fuente.read_text(encoding="utf-8"), fuente.name)
    rol = ast["meta"]["rol"].replace(" ", "-")
    nombre = f"Manual-{rol}"
    # Anotaciones propias de este manual: fuente/anotaciones-<rol>.json -> capturas/anotadas/<rol>/<id>.png
    anotaciones = entorno["anotaciones"](rol)
    anotadas = entorno["anotadas"](rol)
    capturas_json = entorno["capturas_json"]
    if capturas_json and capturas_json.exists():
        if not anotaciones.exists():
            info(f"  AVISO: no existe {anotaciones.name}; las capturas de {rol} se usan sin anotar")
        r = anotar.procesar(capturas_json, entorno["originales"], anotaciones, anotadas)
        info(f"  Anotaciones ({anotaciones.name}): {r['anotadas']} anotadas, {r['copiadas']} copiadas sin cambios, "
             f"{len(r['faltantes'])} sin archivo original")
    else:
        info("  AVISO: no hay capturas.json; se usaran las imagenes ya existentes sin anotar.")
    caps = Capturas([anotadas, entorno["originales"]], capturas_json, anotaciones, args.permitir_faltantes,
                    entorno["originales"], anotadas / "recortes")
    preparar_imagenes(ast, caps, fuente.name)
    ast["meta"]["logo_ruta"] = str(logo) if logo else ""
    if logo:
        with Image.open(logo) as im:
            ast["meta"]["logo_dim"] = list(im.size)
    carpeta_html = salida / "html"
    carpeta_html.mkdir(parents=True, exist_ok=True)
    ruta_html = carpeta_html / f"{nombre}.html"
    info(f"== {nombre}  ({len(ast['toc'])} entradas de indice, "
         f"{sum(1 for _ in fuente_md.recorrer_imagenes(ast['bloques']))} figuras)")

    # HTML (+ PDF en dos pasadas para que el indice lleve numeros de pagina reales)
    ruta_html.write_text(render_html.generar_html(ast, carpeta_html, str(logo) if logo else None), encoding="utf-8", newline="\r\n")
    info(f"  HTML: {ruta_html.relative_to(RAIZ) if ruta_html.is_relative_to(RAIZ) else ruta_html}")
    if not args.sin_pdf:
        pdf = salida / f"{nombre}.pdf"
        html_a_pdf(ruta_html, pdf)
        pags = paginas_del_indice(pdf, ast["toc"])
        if pags:
            ruta_html.write_text(render_html.generar_html(ast, carpeta_html, str(logo) if logo else None, pags),
                                 encoding="utf-8", newline="\r\n")
            html_a_pdf(ruta_html, pdf)
            if pymupdf:
                d = pymupdf.open(pdf)
                info(f"  PDF: {pdf.name}  ({len(d)} paginas)")
                d.close()
        else:
            info(f"  PDF: {pdf.name}  (indice sin numeros de pagina)")

    if not args.sin_docx:
        TMP.mkdir(parents=True, exist_ok=True)
        arbol = TMP / f"{nombre}.json"
        arbol.write_text(json.dumps(ast, ensure_ascii=False), encoding="utf-8")
        docx = salida / f"{nombre}.docx"
        node_dir = HERR
        if not (node_dir / "node_modules" / "docx").exists():
            info("  Instalando dependencias de Node (npm install)...")
            subprocess.run(["npm", "install", "--no-audit", "--no-fund"], cwd=node_dir, check=True, shell=True)
        r = subprocess.run(["node", str(HERR / "generar-docx.js"), str(arbol), str(docx)], capture_output=True, text=True, encoding="utf-8")
        if r.returncode != 0:
            raise SystemExit("Fallo la generacion de Word:\n" + r.stderr)
        info("  " + r.stdout.strip())
    return caps.faltantes


def main() -> int:
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--solo", help="Rol a construir (Operador, Supervisor, Administrador)")
    p.add_argument("--origen", type=Path, help="carpeta con las capturas originales y capturas.json del guion de capturas")
    p.add_argument("--muestra", action="store_true", help="compila fuente/manual-ejemplo.md con las imagenes de herramientas/muestras")
    p.add_argument("--sin-pdf", action="store_true")
    p.add_argument("--sin-docx", action="store_true")
    p.add_argument("--permitir-faltantes", action="store_true", help="si falta una captura, pone un marcador rojo en vez de fallar")
    args = p.parse_args()

    faltantes: list[str] = []
    try:
        if args.muestra:
            base = HERR / "muestras"
            salida = base / "salida"
            fuentes = [RAIZ / "fuente" / "manual-ejemplo.md"]
            entorno = {"capturas_json": base / "capturas.json", "originales": base,
                       "anotaciones": lambda rol: base / "anotaciones.json",
                       "anotadas": lambda rol: base / "anotadas"}
            logo = base / "logo-ejemplo.png"
        else:
            salida = RAIZ
            originales = RAIZ / "capturas" / "originales"
            origen = args.origen or (Path(ORIGEN_POR_DEFECTO) if ORIGEN_POR_DEFECTO else None)
            copiar_originales(origen, originales)
            capturas_json = next((c for c in (originales / "capturas.json", RAIZ / "capturas" / "capturas.json") if c.exists()), None)
            entorno = {"capturas_json": capturas_json, "originales": originales,
                       "anotaciones": lambda rol: RAIZ / "fuente" / f"anotaciones-{rol}.json",
                       "anotadas": lambda rol: RAIZ / "capturas" / "anotadas" / rol}
            logo = RAIZ / "capturas" / "logo-ejemplo.png"
            fuentes = sorted(f for f in (RAIZ / "fuente").glob("manual-*.md") if f.name != "manual-ejemplo.md")
            if args.solo:
                fuentes = [f for f in fuentes if f.stem.lower() == f"manual-{args.solo.lower()}"]
            if not fuentes:
                raise SystemExit("No hay fuente/manual-*.md para construir.")
        logo = logo if logo.exists() else None

        for f in fuentes:
            faltantes += construir(f, args, entorno, salida, logo)
        if faltantes:
            info("AVISO: capturas que faltan (marcador rojo en el documento): " + ", ".join(sorted(set(faltantes))))
    except (fuente_md.ErrorFormato, anotar.ErrorAnotacion) as e:
        print(f"ERROR: {e}", file=sys.stderr)
        return 1
    finally:
        if not faltantes:                      # los marcadores de captura faltante viven en _tmp
            shutil.rmtree(TMP, ignore_errors=True)
    info("Listo.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
