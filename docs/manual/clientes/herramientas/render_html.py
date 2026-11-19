"""Genera el HTML (con su CSS integrado) a partir del arbol de fuente_md.analizar().

El mismo HTML se convierte a PDF con Microsoft Edge (ver construir-manuales.py).
"""
from __future__ import annotations

import html
import os
from pathlib import Path

CSS = r"""
:root {
  --azul: #1F4E79;
  --azul-claro: #DCE9F5;
  --gris: #555;
  --linea: #C9D3DD;
  --rojo: #D32F2F;
  --importante: #B26A00;   --importante-fondo: #FFF4DC;
  --error: #B71C1C;        --error-fondo: #FDE9E9;
  --consejo: #1B6E3C;      --consejo-fondo: #E6F4EA;
}
@page {
  size: A4;
  margin: 24mm 20mm 22mm 20mm;
  @top-left { content: "%(encabezado)s"; font: 600 9.5pt Calibri, 'Segoe UI', Arial, sans-serif; color: #555;
              border-bottom: 1px solid #C9D3DD; padding-bottom: 2mm; vertical-align: bottom; width: 80%%; }
  @top-right { content: "Versi\00F3n %(version)s"; font: 9.5pt Calibri, 'Segoe UI', Arial, sans-serif; color: #555;
               border-bottom: 1px solid #C9D3DD; padding-bottom: 2mm; vertical-align: bottom; }
  @bottom-center { content: "P\00E1gina " counter(page) " de " counter(pages);
                   font: 10pt Calibri, 'Segoe UI', Arial, sans-serif; color: #555; }
}
@page portada {
  margin: 20mm;
  @top-left { content: none; border: none; } @top-right { content: none; border: none; } @bottom-center { content: none; }
}
* { box-sizing: border-box; }
html { -webkit-print-color-adjust: exact; print-color-adjust: exact; }
body { font-family: Calibri, 'Segoe UI', Arial, sans-serif; font-size: 12pt; line-height: 1.5; color: #1b1b1b; margin: 0; }

/* ------------------------------------------------------------ portada */
.portada { page: portada; height: 255mm; display: flex; flex-direction: column; justify-content: space-between;
           break-after: page; }
.portada .franja { height: 10mm; background: var(--azul); }
.portada .centro { text-align: center; }
.portada .logo { max-width: 70mm; max-height: 45mm; margin: 0 auto 14mm; display: block; }
.portada .empresa-texto { font-size: 22pt; font-weight: 700; color: var(--azul); letter-spacing: .03em; margin-bottom: 14mm; }
.portada h1 { font-size: 40pt; line-height: 1.1; margin: 0 0 6mm; color: var(--azul); break-before: auto; }
.portada .producto { font-size: 20pt; color: #333; margin-bottom: 4mm; }
.portada .rol { display: inline-block; margin-top: 4mm; font-size: 14pt; font-weight: 700; color: #fff;
                background: var(--azul); border-radius: 6px; padding: 2mm 8mm; }
.portada .pie { text-align: center; font-size: 14pt; color: var(--gris); }
.portada .pie strong { color: #222; }

/* ------------------------------------------------------------ indice */
.indice { break-after: page; }
.indice h1.indice-titulo { font-size: 26pt; color: var(--azul); border-bottom: 3px solid var(--azul); padding-bottom: 3mm;
                           margin: 0 0 6mm; break-before: auto; }
ul.toc { list-style: none; margin: 0; padding: 0; }
ul.toc li { margin: 0; }
ul.toc a { display: flex; align-items: baseline; text-decoration: none; color: inherit; }
ul.toc .rel { flex: 1; border-bottom: 2px dotted #9aa7b4; margin: 0 .5em; transform: translateY(-.25em); }
ul.toc .n1 a { font-weight: 700; font-size: 12.5pt; margin-top: 3.2mm; }
ul.toc .n2 a { padding-left: 9mm; font-size: 11.5pt; color: #333; line-height: 1.4; }
ul.toc .n2 .rel { border-bottom-color: #c9d0d8; }

/* ------------------------------------------------------------ titulos */
h1, h2, h3 { color: var(--azul); line-height: 1.2; font-weight: 700; break-after: avoid; }
h1 { font-size: 26pt; margin: 0 0 7mm; padding-bottom: 3mm; border-bottom: 3px solid var(--azul); break-before: page; }
h1:first-of-type { break-before: auto; }
h2 { font-size: 18pt; margin: 9mm 0 3.5mm; }
h3 { font-size: 14pt; margin: 7mm 0 3mm; color: #17354f; }
h1 .num, h2 .num { color: var(--rojo); margin-right: .35em; }

p { margin: 0 0 4mm; orphans: 3; widows: 3; }
strong { font-weight: 700; }

/* ------------------------------------------------------------ teclas */
kbd { display: inline-block; font-family: 'Segoe UI', Calibri, Arial, sans-serif; font-weight: 700; font-size: .92em; line-height: 1.15;
      padding: .06em .5em .02em; margin: 0 .08em; color: #222; background: linear-gradient(#fdfdfd, #e9edf1);
      border: 1px solid #8995a1; border-bottom-width: 3px; border-radius: 5px; white-space: nowrap; }

/* ------------------------------------------------------------ listas */
ul { margin: 0 0 4mm; padding-left: 7mm; }
ul li { margin-bottom: 2mm; }
ul li::marker { color: var(--azul); }
ol.pasos { list-style: none; counter-reset: paso; margin: 0 0 5mm; padding: 0; }
ol.pasos > li { counter-increment: paso; position: relative; padding-left: 12mm; margin: 0 0 6mm; }
ol.pasos > li::before { content: counter(paso); position: absolute; left: 0; top: .02em; width: 8.5mm; height: 8.5mm;
                        border-radius: 50%%; background: var(--azul); color: #fff; font-weight: 700; font-size: 13pt;
                        text-align: center; line-height: 8.5mm; }
ol.pasos > li > p:first-child { font-size: 12.5pt; }
ol.pasos > li > p.junto { break-after: avoid; }
ol.pasos > li > :last-child { margin-bottom: 0; }

/* ------------------------------------------------------------ figuras */
figure { max-width: 100%%; margin: 4mm auto 6mm; break-inside: avoid; text-align: center; }
figure img { display: block; width: 100%%; height: auto; border: 1px solid #B8C2CC; box-shadow: 0 1px 4px rgba(0,0,0,.18); }
figcaption { font-style: italic; font-size: 10.5pt; color: #444; margin-top: 2.5mm; text-align: center; line-height: 1.35; }
.leyenda { font-size: 10.5pt; color: #333; text-align: center; margin-top: 1mm; }
.leyenda .c { display: inline-block; width: 5mm; height: 5mm; border-radius: 50%%; background: #E02020; color: #fff;
              font-size: 8.5pt; font-weight: 700; text-align: center; line-height: 5mm; margin: 0 1mm 0 3mm; }

/* ------------------------------------------------------------ recuadros */
.recuadro { margin: 5mm 0 6mm; padding: 3mm 5mm 3mm 5mm; border-left: 2.2mm solid; border-radius: 0 6px 6px 0; box-decoration-break: clone; }
.recuadro.corto { break-inside: avoid; }
.recuadro > :last-child { margin-bottom: 0; }
.recuadro .etq { font-weight: 700; font-size: 11pt; letter-spacing: .05em; text-transform: uppercase; margin-bottom: 1.5mm;
                 display: flex; align-items: center; break-after: avoid; }
.recuadro .ico { display: inline-block; width: 6.2mm; height: 6.2mm; border-radius: 50%%; color: #fff; text-align: center;
                 line-height: 6.2mm; font-size: 11pt; margin-right: 2.5mm; font-weight: 800; }
.recuadro.important { background: var(--importante-fondo); border-color: var(--importante); }
.recuadro.important .etq { color: var(--importante); } .recuadro.important .ico { background: var(--importante); }
.recuadro.error { background: var(--error-fondo); border-color: var(--error); }
.recuadro.error .etq { color: var(--error); } .recuadro.error .ico { background: var(--error); }
.recuadro.tip { background: var(--consejo-fondo); border-color: var(--consejo); }
.recuadro.tip .etq { color: var(--consejo); } .recuadro.tip .ico { background: var(--consejo); }
.recuadro ul, .recuadro ol { margin-bottom: 2mm; }

/* ------------------------------------------------------------ tablas */
table { border-collapse: collapse; width: 100%%; margin: 3mm 0 6mm; font-size: 11.5pt; }
th { background: var(--azul); color: #fff; text-align: left; padding: 2.2mm 3mm; border: 1px solid var(--azul); }
td { padding: 2.2mm 3mm; border: 1px solid var(--linea); vertical-align: top; }
tbody tr:nth-child(even) td { background: #F3F7FB; }
tr { break-inside: avoid; }
td p:last-child { margin-bottom: 0; }

/* ------------------------------------------------------------ glosario */
dl.glosario { margin: 0; }
dl.glosario .fila { display: flex; border-bottom: 1px solid var(--linea); padding: 3mm 0; break-inside: avoid; }
dl.glosario dt { width: 42mm; flex: none; font-weight: 700; color: var(--azul); padding-right: 4mm; }
dl.glosario dd { margin: 0; flex: 1; }

/* ------------------------------------------------------------ hoja de teclas */
section.teclas { break-before: page; break-inside: avoid; font-size: 14pt; }
section.teclas h1 { break-before: auto; text-align: center; font-size: 30pt; border-bottom: 4px solid var(--azul); }
section.teclas h2 { font-size: 17pt; margin: 4mm 0 2mm; }
section.teclas p { font-size: 13pt; text-align: center; color: #444; }
section.teclas table { font-size: 14pt; margin: 2mm 0 4mm; }
section.teclas th { font-size: 12pt; }
section.teclas td { padding: 2.6mm 4mm; border-color: #9fb2c4; vertical-align: middle; }
section.teclas td:first-child { width: 42mm; text-align: center; white-space: nowrap; background: #F3F7FB; }
section.teclas kbd { font-size: 19pt; padding: .1em .6em .04em; }
section.teclas .recuadro { font-size: 13pt; }
section.teclas .recuadro p { text-align: left; font-size: 13pt; color: inherit; }
"""


def esc(s: str) -> str:
    return html.escape(s, quote=True)


def inline(tramos: list[dict]) -> str:
    out = []
    for t in tramos:
        v = esc(t["v"])
        if t["t"] == "key":
            out.append(f"<kbd>{v}</kbd>")
            continue
        if t.get("i"):
            v = f"<em>{v}</em>"
        if t.get("b"):
            v = f"<strong>{v}</strong>"
        out.append(v)
    return "".join(out)


def _longitud(bloques: list[dict]) -> int:
    """Largo aproximado (caracteres) del contenido de un recuadro; una imagen cuenta como contenido muy largo."""
    n = 0
    for b in bloques:
        if b["tipo"] == "parrafo":
            n += len(inline_plano(b["inl"]))
        elif b["tipo"] == "lista":
            n += sum(_longitud(it["bloques"]) + 40 for it in b["items"])
        elif b["tipo"] == "imagen":
            n += 10000
        else:
            n += 300
    return n


def inline_plano(tramos: list[dict]) -> str:
    return "".join(t["v"] for t in tramos)


def _cm(v: float) -> str:
    return f"{v:.2f}".rstrip("0").rstrip(".") + "cm"


class _Render:
    def __init__(self, carpeta_html: Path):
        self.carpeta = carpeta_html
        self.abierta_teclas = False

    def ruta_rel(self, p: str) -> str:
        return Path(os.path.relpath(p, self.carpeta)).as_posix()

    def bloques(self, bloques: list[dict]) -> str:
        out = []
        for b in bloques:
            out.append(self.bloque(b))
        return "\n".join(out)

    def item(self, bloques: list[dict], ordenada: bool) -> str:
        """Un paso: el texto se mantiene junto a su primera figura solo si esta no es muy alta (<= 9 cm)."""
        out = []
        for i, x in enumerate(bloques):
            sig = bloques[i + 1] if i + 1 < len(bloques) else None
            if (ordenada and i == 0 and x["tipo"] == "parrafo" and sig and sig["tipo"] == "imagen"
                    and sig["ancho_final_cm"] * sig["h_px"] / sig["w_px"] <= 9.0):
                out.append('<p class="junto">' + inline(x["inl"]) + "</p>")
            else:
                out.append(self.bloque(x))
        return "\n".join(out)

    def bloque(self, b: dict) -> str:
        t = b["tipo"]
        if t == "titulo":
            nivel = b["nivel"]
            pre = ""
            if nivel == 1 and self.abierta_teclas:
                pre = "</section>\n"
                self.abierta_teclas = False
            if nivel == 1 and b["especial"] == "teclas":
                pre += '<section class="teclas">\n'
                self.abierta_teclas = True
            num = f'<span class="num">{esc(b["numero"])}{"." if nivel == 1 else ""}</span>' if b["numero"] else ""
            return f'{pre}<h{nivel} id="{b["id"]}">{num}{inline(b["inl"])}</h{nivel}>'
        if t == "parrafo":
            return f"<p>{inline(b['inl'])}</p>"
        if t == "salto":
            return '<div style="break-after: page"></div>'
        if t == "lista":
            tag = "ol" if b["ordenada"] else "ul"
            cls = ' class="pasos"' if b["ordenada"] else ""
            items = "".join(f"<li>{self.item(it['bloques'], b['ordenada'])}</li>" for it in b["items"])
            return f"<{tag}{cls}>{items}</{tag}>"
        if t == "recuadro":
            corto = " corto" if _longitud(b["bloques"]) < 520 else ""
            return (f'<div class="recuadro {b["clase"]}{corto}"><div class="etq"><span class="ico">{esc(b["icono"])}</span>'
                    f'{esc(b["etiqueta"])}</div>{self.bloques(b["bloques"])}</div>')
        if t == "imagen":
            leyenda = ""
            if b.get("leyenda"):
                leyenda = '<div class="leyenda">' + "".join(
                    f'<span class="c">{esc(str(n))}</span>{esc(txt)}' for n, txt in b["leyenda"]) + "</div>"
            return (f'<figure style="width:{_cm(b["ancho_final_cm"])}"><img src="{esc(self.ruta_rel(b["ruta"]))}" '
                    f'alt="{esc(b["epigrafe_plano"])}"><figcaption>Figura {b["numero_figura"]}. '
                    f'{inline(b["epigrafe"])}</figcaption>{leyenda}</figure>')
        if t == "tabla":
            ths = "".join(f'<th style="text-align:{a}">{inline(c)}</th>' for c, a in zip(b["cabecera"], b["alineacion"]))
            trs = "".join(
                "<tr>" + "".join(f'<td style="text-align:{a}">{inline(c)}</td>' for c, a in zip(f, b["alineacion"])) + "</tr>"
                for f in b["filas"])
            return f"<table><thead><tr>{ths}</tr></thead><tbody>{trs}</tbody></table>"
        if t == "glosario":
            filas = "".join(f'<div class="fila"><dt>{esc(f["termino"])}</dt><dd>{inline(f["inl"])}</dd></div>' for f in b["filas"])
            return f'<dl class="glosario">{filas}</dl>'
        raise ValueError(f"bloque desconocido {t}")


def generar_html(ast: dict, carpeta_html: Path, logo: str | None, paginas: dict | None = None) -> str:
    m = ast["meta"]
    r = _Render(carpeta_html)
    cuerpo = r.bloques(ast["bloques"])
    if r.abierta_teclas:
        cuerpo += "\n</section>"
    paginas = paginas or {}
    toc = []
    for e in ast["toc"]:
        num = f'{esc(e["numero"])}{"." if e["nivel"] == 1 and e["numero"] else ""} ' if e["numero"] else ""
        pg = paginas.get(e["id"], "")
        toc.append(f'<li class="n{e["nivel"]}"><a href="#{e["id"]}"><span class="tt">{num}{esc(e["texto"])}</span>'
                   f'<span class="rel"></span><span class="pg">{pg}</span></a></li>')
    if logo:
        marca = f'<img class="logo" src="{esc(r.ruta_rel(logo))}" alt="{esc(m["empresa"])}">'
    else:
        marca = f'<div class="empresa-texto">{esc(m["empresa"])}</div>'
    css = CSS % {"encabezado": f'{m["titulo"]} \\00B7  {m["producto"]}'.replace('"', '\\"'), "version": m["version"]}
    return f"""<!DOCTYPE html>
<html lang="es">
<head>
<meta charset="utf-8">
<title>{esc(m["titulo"])} - {esc(m["producto"])}</title>
<style>{css}</style>
</head>
<body>
<section class="portada">
  <div class="franja"></div>
  <div class="centro">
    {marca}
    <h1>{esc(m["titulo"])}</h1>
    <div class="producto">{esc(m["producto"])}</div>
    <div class="rol">{esc(m["rol"])}</div>
  </div>
  <div class="pie"><div><strong>Versión {esc(m["version"])}</strong></div><div>{esc(m["fecha_texto"])}</div>
  <div style="margin-top:3mm;font-size:12pt">{esc(m["empresa"])}</div></div>
</section>
<section class="indice">
  <h1 class="indice-titulo">Contenido</h1>
  <ul class="toc">
{chr(10).join(toc)}
  </ul>
</section>
<main>
{cuerpo}
</main>
</body>
</html>
"""
