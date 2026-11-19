"""Genera capturas FALSAS (marcadas como muestra) para probar el armado de los manuales.

No son capturas reales de la aplicacion: sirven solo para ejercitar anotar.py y construir-manuales.py --muestra.
"""
import json
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

AQUI = Path(__file__).resolve().parent


def fuente(n, negrita=False):
    for f in (("segoeuib.ttf" if negrita else "segoeui.ttf"), "arial.ttf"):
        try:
            return ImageFont.truetype(f, n)
        except OSError:
            pass
    return ImageFont.load_default()


def ventana(nombre, ancho, alto, titulo, campos, botones):
    """campos: [(nombre, etiqueta, valor)], botones: [(nombre, texto)]. Devuelve (imagen, controles)."""
    im = Image.new("RGB", (ancho, alto), (240, 240, 240))
    d = ImageDraw.Draw(im)
    d.rectangle([0, 0, ancho, 30], fill=(255, 255, 255))
    d.text((10, 5), titulo, font=fuente(15), fill=(0, 0, 0))
    d.text((ancho - 150, alto - 22), "MUESTRA - NO ES UNA CAPTURA REAL", font=fuente(10), fill=(190, 0, 0))
    controles = []
    y = 55
    for nom, etq, val in campos:
        d.text((20, y), etq, font=fuente(15), fill=(0, 0, 0))
        d.rectangle([20, y + 24, ancho - 20, y + 54], fill=(255, 255, 255), outline=(120, 120, 120))
        d.text((28, y + 28), val, font=fuente(16), fill=(30, 30, 30))
        controles.append({"nombre": nom, "texto": etq, "x": 20, "y": y + 24, "ancho": ancho - 40, "alto": 30})
        y += 75
    x = ancho - 20
    for nom, txt in reversed(botones):
        w = 24 + 9 * len(txt)
        d.rectangle([x - w, alto - 70, x, alto - 34], fill=(225, 225, 225), outline=(120, 120, 120))
        d.text((x - w + 12, alto - 64), txt, font=fuente(15), fill=(0, 0, 0))
        controles.append({"nombre": nom, "texto": txt, "x": x - w, "y": alto - 70, "ancho": w, "alto": 36})
        x -= w + 12
    im.save(AQUI / f"{nombre}.png")
    return controles


capturas = []
c1 = ventana("muestra-01-login", 640, 330, "Iniciar sesión", [("txtUsuario", "Usuario", "operador"), ("txtPassword", "Contraseña", "••••••")],
             [("btnIngresar", "Ingresar"), ("btnSalir", "Salir")])
capturas.append({"id": "muestra-01-login", "archivo": "muestra-01-login.png", "titulo": "Inicio de sesión (MUESTRA)", "descripcion": "Muestra",
                 "ancho": 640, "alto": 330, "escalaDpi": 1.0, "controles": c1})
c2 = ventana("muestra-02-ancha", 1400, 520, "Pesaje de faeneo", [("txtGanadero", "Ganadero", "GANADERÍA EL PORVENIR"), ("txtCliente", "Cliente", "CARNES DEL SUR")],
             [("btnGrabar", "Grabar (F12)"), ("btnBorrar", "Borrar pesaje (F8)")])
capturas.append({"id": "muestra-02-ancha", "archivo": "muestra-02-ancha.png", "titulo": "Pantalla ancha (MUESTRA)", "descripcion": "Más ancha que la página: se reduce",
                 "ancho": 1400, "alto": 520, "escalaDpi": 1.0, "controles": c2})
c3 = ventana("muestra-03-sin-anotar", 420, 200, "Aviso", [("txtMensaje", "Mensaje", "Sin anotaciones")], [("btnAceptar", "Aceptar")])
capturas.append({"id": "muestra-03-sin-anotar", "archivo": "muestra-03-sin-anotar.png", "titulo": "Sin anotaciones (MUESTRA)", "descripcion": "Se copia tal cual",
                 "ancho": 420, "alto": 200, "escalaDpi": 1.0, "controles": c3})
(AQUI / "capturas.json").write_text(json.dumps(capturas, ensure_ascii=False, indent=2), encoding="utf-8")

anot = {
    "muestra-01-login": [
        {"control": "txtUsuario", "numero": 1, "tipo": "recuadro", "leyenda": "Escriba su usuario"},
        {"control": "txtPassword", "numero": 2, "tipo": "recuadro", "leyenda": "Escriba su contraseña"},
        {"control": "btnIngresar", "numero": 3, "tipo": "circulo", "leyenda": "Pulse Ingresar"},
    ],
    "muestra-02-ancha": [
        {"control": "btnGrabar", "numero": 1, "tipo": "recuadro", "posicion": "arriba-derecha"},
        {"area": [0, 0, 120, 30], "numero": 2, "tipo": "recuadro"},
    ],
}
(AQUI / "anotaciones.json").write_text(json.dumps(anot, ensure_ascii=False, indent=2), encoding="utf-8")

# logo de muestra
logo = Image.new("RGBA", (360, 220), (0, 0, 0, 0))
d = ImageDraw.Draw(logo)
d.rounded_rectangle([5, 5, 355, 215], radius=30, fill=(31, 78, 121, 255))
d.text((180, 85), "LOGO", font=fuente(70, True), fill="white", anchor="mm")
d.text((180, 160), "(muestra)", font=fuente(30), fill="white", anchor="mm")
logo.save(AQUI / "logo-ejemplo.png")
print("Muestras generadas")
