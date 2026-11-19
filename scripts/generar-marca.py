"""Genera los recursos de marca (logo "C con pesa") en src/PesajeFaeneo.WinForms/Recursos.

Uso: python scripts/generar-marca.py
Salida: marca-icono.svg/.png, marca-horizontal.svg/.png, app.ico, instalador-pequeno.bmp
Solo requiere Pillow. Es una herramienta de desarrollo: el build normal no la ejecuta.
"""
import math
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

RAIZ = Path(__file__).resolve().parent.parent
SALIDA = RAIZ / "src" / "PesajeFaeneo.WinForms" / "Recursos"
INSTALADOR = RAIZ / "instalador"

ROJO = (139, 30, 45)
ORO = (200, 150, 46)
CARBON = (43, 43, 43)
GRIS = (107, 101, 88)
CREMA = (245, 242, 235)
SS = 4  # supersampling

# Geometria del icono en un lienzo de 256 px: C abierta a la derecha (+-50 grados) y pesa dorada dentro.
C0, R, GROSOR, PESA_DX, PESA_R = 128, 76, 28, 6, 18
ABERTURA = 50


def dibujar_icono(d, k, cx=C0, cy=C0, escala=1.0):
    r, g = R * escala * k, GROSOR * escala * k
    cx, cy = cx * k, cy * k
    ro = r + g / 2  # Pillow dibuja el grosor hacia adentro del recuadro: el radio exterior es r + g/2
    caja = [cx - ro, cy - ro, cx + ro, cy + ro]
    d.arc(caja, ABERTURA, 360 - ABERTURA, fill=ROJO, width=round(g))
    for ang in (ABERTURA, 360 - ABERTURA):
        x = cx + r * math.cos(math.radians(ang))
        y = cy + r * math.sin(math.radians(ang))
        d.ellipse([x - g / 2, y - g / 2, x + g / 2, y + g / 2], fill=ROJO)
    px, pr = cx + PESA_DX * escala * k, PESA_R * escala * k
    d.ellipse([px - pr, cy - pr, px + pr, cy + pr], fill=ORO)


def lienzo(w, h, fondo=None):
    return Image.new("RGBA", (w * SS, h * SS), fondo or (0, 0, 0, 0))


def reducir(img, w, h):
    return img.resize((w, h), Image.LANCZOS)


def icono_transparente(tam=256):
    img = lienzo(256, 256)
    dibujar_icono(ImageDraw.Draw(img), SS)
    return reducir(img, tam, tam)


def icono_con_baldosa(tam):
    # Baldosa crema de esquinas redondeadas para que se vea en barras oscuras y claras.
    img = lienzo(256, 256)
    d = ImageDraw.Draw(img)
    d.rounded_rectangle([8 * SS, 8 * SS, 248 * SS, 248 * SS], radius=44 * SS, fill=CREMA, outline=(216, 210, 196), width=2 * SS)
    dibujar_icono(d, SS, escala=0.86)
    return reducir(img, tam, tam)


def fuente(ruta, tam):
    return ImageFont.truetype(f"C:/Windows/Fonts/{ruta}", tam * SS)


def texto_espaciado(d, xy, txt, fnt, relleno, espacio):
    x, y = xy
    for ch in txt:
        d.text((x, y), ch, font=fnt, fill=relleno)
        x += d.textlength(ch, font=fnt) + espacio * SS
    return x


def horizontal():
    W, H = 900, 260
    img = lienzo(W, H)
    d = ImageDraw.Draw(img)
    dibujar_icono(d, SS, cx=130, cy=130, escala=0.86)
    fb, fn = fuente("arialbd.ttf", 104), fuente("arial.ttf", 30)
    texto_espaciado(d, (270 * SS, 62 * SS), "CAPATAZ", fb, CARBON, 8)
    texto_espaciado(d, (276 * SS, 178 * SS), "PESAJE DE FAENEO", fn, GRIS, 6)
    return reducir(img, W, H)


def svg_icono(solo_c=True):
    return (
        '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 256 256" width="256" height="256">'
        f'<path d="M{128 + R * math.cos(math.radians(ABERTURA)):.1f} {128 - R * math.sin(math.radians(ABERTURA)):.1f} '
        f'A{R} {R} 0 1 0 {128 + R * math.cos(math.radians(ABERTURA)):.1f} {128 + R * math.sin(math.radians(ABERTURA)):.1f}" '
        f'fill="none" stroke="#8B1E2D" stroke-width="{GROSOR}" stroke-linecap="round"/>'
        f'<circle cx="{128 + PESA_DX}" cy="128" r="{PESA_R}" fill="#C8962E"/></svg>\n'
    )


def svg_horizontal():
    e = 0.86
    r, g = R * e, GROSOR * e
    x1 = 130 + r * math.cos(math.radians(ABERTURA))
    y1 = 130 - r * math.sin(math.radians(ABERTURA))
    y2 = 130 + r * math.sin(math.radians(ABERTURA))
    return (
        '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 900 260" width="900" height="260">'
        f'<path d="M{x1:.1f} {y1:.1f} A{r:.1f} {r:.1f} 0 1 0 {x1:.1f} {y2:.1f}" fill="none" stroke="#8B1E2D" '
        f'stroke-width="{g:.1f}" stroke-linecap="round"/>'
        f'<circle cx="{130 + PESA_DX * e:.1f}" cy="130" r="{PESA_R * e:.1f}" fill="#C8962E"/>'
        '<text x="270" y="140" font-family="Arial, Helvetica, sans-serif" font-size="104" font-weight="700" '
        'letter-spacing="8" fill="#2B2B2B">CAPATAZ</text>'
        '<text x="276" y="204" font-family="Arial, Helvetica, sans-serif" font-size="30" letter-spacing="6" '
        'fill="#6B6558">PESAJE DE FAENEO</text></svg>\n'
    )


def main():
    SALIDA.mkdir(parents=True, exist_ok=True)
    (SALIDA / "marca-icono.svg").write_text(svg_icono(), encoding="utf-8", newline="\n")
    (SALIDA / "marca-horizontal.svg").write_text(svg_horizontal(), encoding="utf-8", newline="\n")
    icono_transparente(256).save(SALIDA / "marca-icono.png")
    horizontal().save(SALIDA / "marca-horizontal.png")
    ico = icono_con_baldosa(256)
    ico.save(SALIDA / "app.ico", sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])
    peq = Image.new("RGB", (55, 55), (255, 255, 255))
    peq.paste(icono_con_baldosa(55), (0, 0), icono_con_baldosa(55))
    peq.save(INSTALADOR / "instalador-pequeno.bmp")
    print("Recursos generados en", SALIDA)


if __name__ == "__main__":
    main()
