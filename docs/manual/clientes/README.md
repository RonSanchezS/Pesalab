# Manuales de usuario para clientes

Tres manuales (Operador, Supervisor, Administrador) del Capataz, en Word (`.docx`) y PDF.
Los tres salen de una **fuente única en Markdown** (`fuente/manual-*.md`) y de las mismas capturas, de modo que
Word, HTML y PDF dicen siempre lo mismo.

## Cómo regenerar

Requisitos (ya instalados en la PC de desarrollo):

* Python 3.10 o superior con **Pillow**.
* **Node.js** (el armado ejecuta `npm install` la primera vez para bajar `docx` dentro de `herramientas/`).
* **Microsoft Edge** (genera el PDF desde el HTML con `msedge --headless --print-to-pdf`).
* Opcional: **PyMuPDF** en `herramientas/.venv` (el armado se relanza solo con ese Python si existe). Con él, el
  índice del PDF lleva números de página. Instalación: `python -m venv herramientas\.venv` y
  `herramientas\.venv\Scripts\python -m pip install pymupdf pillow`.

Comando (desde esta carpeta o desde cualquier otra):

```
python docs\manual\clientes\construir-manuales.py
```

Opciones:

| Opción | Efecto |
| :--- | :--- |
| `--origen CARPETA` | Antes de armar, copia a `capturas/originales/` los PNG y el `capturas.json` que dejó el guion de capturas. También se puede fijar con la variable de entorno `PESALAB_CAPTURAS_ORIGEN`. |
| `--solo Operador` | Arma un solo manual. |
| `--sin-pdf`, `--sin-docx` | Omite ese formato. |
| `--permitir-faltantes` | Si falta una captura pone un marcador rojo en lugar de detenerse (útil mientras se redacta). |
| `--muestra` | Compila `fuente/manual-ejemplo.md` con imágenes de muestra y deja el resultado en `herramientas/muestras/salida/`. Sirve para probar la herramienta; no toca los manuales reales. |

El armado: 1) copia las capturas originales, 2) lee cada
`fuente/manual-*.md` y, por manual, dibuja sus anotaciones (`herramientas/anotar.py`, desde `fuente/anotaciones-<rol>.json` hacia `capturas/anotadas/<rol>/`), 3) escribe `html/Manual-<Rol>.html`, 4) lo convierte en `Manual-<Rol>.pdf` con Edge (dos
pasadas, para poner los números de página en el índice) y 5) escribe `Manual-<Rol>.docx` con `docx-js`.

## Carpetas

```
docs/manual/clientes/
  construir-manuales.py        punto único de entrada
  Manual-Operador.docx/.pdf    resultados (idem Supervisor y Administrador)
  README.md
  fuente/
    FORMATO.md                 sintaxis que deben usar quienes redactan (leer primero)
    manual-<Rol>.md            un archivo por manual
    manual-ejemplo.md          ejemplo con todos los elementos (no se arma por omisión)
    anotaciones-<rol>.json     un archivo por manual: qué recuadros y círculos numerados lleva cada captura
  capturas/
    originales/                PNG del guion de capturas + capturas.json (copiados por el armado)
    anotadas/<rol>/            PNG con círculos y recuadros de cada manual (una subcarpeta por rol)
    logo-ejemplo.png           (opcional) logo genérico de la portada; sin él se usa un título de texto
  html/                        HTML de cada manual (CSS integrado, imágenes con ruta relativa)
  herramientas/
    anotar.py  fuente_md.py  render_html.py  generar-docx.js
    revisar-paginas.py         informa cuántas páginas de cada PDF quedan con mucho espacio vacío
    vista-previa.py  montaje.py  páginas del PDF a PNG, y varias páginas en una sola imagen
    muestras/                  imágenes falsas para probar (marcadas "MUESTRA - NO ES UNA CAPTURA REAL")
    .venv/ node_modules/       entorno local, ignorados por git
```

## Cómo agregar capturas y numeritos

1. El guion de capturas deja los PNG y un `capturas.json`. Cada captura tiene `id`, `archivo`, `escalaDpi` y la
   lista `controles` con `nombre` y su rectángulo (`x, y, ancho, alto`, en píxeles de la imagen).
2. En `fuente/anotaciones-<rol>.json` se escribe, para cada `id`, qué controles se señalan y con qué número
   (ver la sección 11 de `fuente/FORMATO.md`). Ejemplo: `{ "op-01-login": [ { "control": "txtUsuario", "numero": 1 } ] }`.
3. En el manual se inserta la imagen por su `id`:
   `![Pantalla de inicio de sesión](capturas/op-01-login.png){callouts=1,2,3}`
   y en el texto de los pasos se alude a «el recuadro **1**» o «el círculo **2**».
4. Se ejecuta de nuevo el armado. Si un número o un control no existe, el armado se detiene con un mensaje claro.

Cada manual usa solo su archivo de anotaciones (así tres personas trabajan en paralelo y la misma captura puede señalarse distinto en cada manual). Una captura que no figura en él se usa sin anotar. Para mostrar solo una zona de una captura y verla más grande, use `recorte` (ver `fuente/FORMATO.md`). Las imágenes enteras nunca se agrandan por encima de su
tamaño real (a 100 % de escala de Windows) y se reducen a 16 cm como máximo; los recortes sí se amplían, hasta 2x.

## Notas

* **Revisar el reparto de páginas:** `herramientas\.venv\Scripts\python herramientasevisar-paginas.py Manual-*.pdf --detalle` cuenta las páginas de cada PDF y lista las que quedan con más de 40 % de espacio vacío (aparte de las que cierran un capítulo).

* **Índice en Word:** es un campo automático. Ya viene escrito con los títulos, pero **Word actualiza los números de
  página al abrir** el archivo (preguntará si desea actualizar los campos: responder «Sí»). En Vista protegida
  hay que pulsar «Habilitar edición» primero. Si no se ven los números: clic derecho sobre el índice ›
  Actualizar campo.
* **Índice en PDF:** lleva números de página reales (leídos del propio PDF); sin PyMuPDF el índice sale sin números,
  con enlaces.
* Los `.docx` no se pudieron abrir en Word en la PC de desarrollo (no hay Word ni LibreOffice); se validaron contra el
  esquema OOXML con la herramienta de la skill `docx`. Conviene abrir cada manual una vez en Word y revisar saltos de página.
* Los archivos de texto son UTF-8 sin BOM con saltos CRLF (`.editorconfig`).
