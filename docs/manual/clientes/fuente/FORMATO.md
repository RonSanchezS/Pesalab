# Formato de los manuales fuente

Cada manual es un archivo `fuente/manual-<Rol>.md` escrito en un Markdown reducido. El mismo archivo produce el
Word, el HTML y el PDF. Todo lo que no esté en esta lista **no se interpreta** (no use HTML ni otras extensiones).
Los archivos son UTF-8 (sin BOM) y pueden llevar saltos de línea CRLF.

El archivo `fuente/manual-ejemplo.md` usa todos los elementos; no se compila con el resto
(`python construir-manuales.py --muestra` lo compila para probar).

## 1. Cabecera (obligatoria)

La primera línea del archivo es `---`. Cierra con otra línea `---`.

```
---
titulo: Manual del Operador
rol: Operador
version: 1.1.0
fecha: 2026-10-05
---
```

| Campo | Uso |
| :--- | :--- |
| `titulo` | Título de la portada y del encabezado de cada página. |
| `rol` | Etiqueta de la portada y nombre de los archivos: `Manual-<rol>.docx/.pdf` (use `Operador`, `Supervisor`, `Administrador`). |
| `version` | Versión del sistema (portada, encabezado). |
| `fecha` | `AAAA-MM-DD`. Opcional: si falta se usa la fecha del día. Se escribe en español en la portada. |
| `empresa`, `producto` | Opcionales. Por defecto `FRIGORÍFICO EJEMPLO S.R.L.` y `Capataz`. |

## 2. Títulos

```
# Capítulo                 (empieza página nueva; se numera 1, 2, 3...)
## Sección                 (se numera 1.1, 1.2...)
### Subsección             (sin número; no entra en el índice)
```

* La numeración la pone el armado: **no** escriba el número a mano.
* El índice (Word y PDF) se genera solo con los capítulos y las secciones.
* Dos capítulos especiales, que no llevan número: `# Glosario {glosario}` y `# Hoja de teclas rápidas {teclas}`
  (ver secciones 9 y 10).

## 3. Texto

* Un párrafo es uno o más renglones seguidos; se separa del siguiente con una línea en blanco.
* `**Texto**` (negrita): **nombres que aparecen en pantalla**: botones, campos, menús, mensajes. Escríbalos
  exactamente como se ven en la aplicación. Ejemplo: `Pulse **Ingresar**.`
* `*Texto*` (cursiva): énfasis suave o términos nuevos. Úsela poco.
* `` `F12` ``: una **tecla**; se dibuja como tecla. Combinaciones: `` `Ctrl+P` `` dibuja dos teclas unidas con «+».
  Para teclas con más de un carácter use el nombre tal cual: `` `Enter` ``, `` `Tab` ``, `` `Esc` ``.
* Para escribir un `*`, `` ` `` o `\` literal anteponga `\`: `\*`.
* Un comentario `<!-- texto -->` en una línea propia no aparece en los documentos.

## 4. Pasos y listas

Pasos numerados (cada paso, una sola acción):

```
1. Abra **Capataz**.
2. Escriba su **Usuario**.

   ![Pantalla Iniciar sesión](capturas/op-01-login.png){callouts=1,2}

3. Pulse **Ingresar**.
```

* Los números se reinician en 1 en cada lista; los pone el armado.
* Todo lo que pertenezca a un paso (otro párrafo, una imagen, un recuadro, una sublista) va **sangrado con 3
  espacios** y separado con una línea en blanco, como en el ejemplo. Un paso se sigue de otra línea sangrada.
* Viñetas: `- elemento` (también sangrando 2 espacios para el contenido de un elemento). Se permiten
  sublistas sangradas.
* Una lista nueva debe empezar sin sangría.

## 5. Imágenes (capturas)

```
![Epígrafe de la figura](capturas/<id>.png){callouts=1,2,3}
```

* `<id>` es el `id` de la captura en `capturas.json` (por ejemplo `op-03-pesaje-captura-izq`). La ruta
  `capturas/...` es solo una convención: lo que cuenta es el **nombre del archivo sin extensión**. El armado usa
  la versión **anotada** (`capturas/anotadas/<id>.png`).
* El **epígrafe** (texto entre corchetes) sale en cursiva como «Figura N. epígrafe»; la numeración de figuras es
  automática y continua. Debe describir qué se ve, en una frase.
* `{callouts=1,2,3}` es opcional y declara qué números de círculo rojo se ven en la imagen. El armado comprueba
  que esos números existan para esa captura en el archivo `fuente/anotaciones-<rol>.json` del propio manual y, si cada anotación tiene una
  `leyenda`, la imprime bajo el epígrafe (● 1 Escriba su usuario  ● 2 ...). Si pide un número que no existe,
  el armado se detiene con un error.
* `{ancho=10}` es opcional: ancho máximo en centímetros. Nunca se agranda una imagen entera más de su tamaño
  natural y nunca pasa de 16 cm (ancho de página).
* Los atributos se combinan separados por `;` o por un espacio: `{callouts=1,2; ancho=12}`.
  Un atributo desconocido detiene el armado con un error.
* El tamaño de los círculos y de los trazos rojos es **siempre el mismo sobre el papel** (unos 6 mm), aunque la
  imagen se reduzca o se amplíe.

### Recortar y ampliar (`recorte`)

Una captura de ventana completa (1000–1500 px) reducida a 16 cm queda con letra diminuta. Con `recorte` se muestra
solo la parte que importa, y **más grande**:

```
![Filtros y resultados](capturas/su-30-buscar-boletas.png){recorte=control:grpFiltros+dgvResultados}
![Filtros y resultados](capturas/su-30-buscar-boletas.png){recorte=control:grpFiltros+dgvResultados, margen=24}
![Zona de pesos](capturas/op-15-pesaje-izquierdo-capturado.png){callouts=1,2; recorte=700,90,520,260}
```

| Sintaxis | Qué recorta |
| :--- | :--- |
| `recorte=control:<nombre>[+<nombre>...]` | La caja que contiene a esos controles (los `nombre` de `capturas.json`), con un margen de **12 px** alrededor. Varios nombres se unen con `+` y sin espacios. |
| `recorte=control:<nombres>, margen=N` (o `margen=N` aparte) | Igual, con un margen de N píxeles (por omisión 12). |
| `recorte=x,y,ancho,alto` | Un rectángulo en **píxeles de la captura original** (esquina superior izquierda y tamaño). Se ajusta a los bordes de la imagen. |

Reglas:

* **El recorte se hace antes de dibujar.** Los números de `anotaciones-<rol>.json` siguen valiendo: el armado
  desplaza las coordenadas solo, y la anotación de un control que queda **por completo fuera** del recorte no se
  dibuja (el armado avisa con `AVISO: ... cae fuera del recorte`). Si su número está en `{callouts=...}`, tampoco
  sale en la leyenda de la figura.
* Un recorte se **amplía hasta 2 veces** (remuestreo de alta calidad en el armado, no se ve borroso) para llegar
  a 16 cm de ancho. Un recorte de 250 px o menos de ancho se muestra a 2x (13 cm); uno de 400 px, a 1,5x (16 cm);
  uno de 605 px o más, sin ampliar. `ancho=N` limita el ancho final (en cm) también en recortes.
* Si un nombre de `control:` no existe, el armado se detiene y lista los controles de esa captura.
* Un recorte demasiado pequeño o fuera de la imagen es un error.
* El Word y el PDF usan el mismo recorte: el archivo recortado queda en `capturas/anotadas/<rol>/recortes/`.
* Consejo: en pantallas de pesaje, filtros o parámetros, recorte la zona que explica el paso (dos o tres
  controles con `control:`) en vez de mostrar la ventana entera. Reserve la ventana completa para ubicar al lector
  («así se ve la pantalla de inicio»).

### Cómo se reparte la página (para evitar huecos)

* El texto de un paso se mantiene **pegado a su primera figura** solo si esta mide 9 cm de alto o menos. Con una
  figura más alta el texto puede quedar al final de una página y la figura al inicio de la siguiente; recortar la
  imagen evita el problema.
* Los recuadros cortos no se parten entre páginas; los largos (más o menos más de 500 caracteres o con una
  figura dentro) sí pueden partirse.
* Evite `<!-- salto -->` salvo que de verdad quiera una página nueva: deja el resto de la página en blanco.
* Una imagen va **sola en su línea**. Dentro de un paso, sangrada con 3 espacios.

## 6. Recuadros

```
> [!IMPORTANTE]
> No comparta su contraseña con nadie.

> [!SI-ALGO-SALE-MAL]
> Si aparece **Usuario o contraseña incorrectos.**:
>
> 1. Revise las mayúsculas.
> 2. Consulte a su administrador del sistema.

> [!CONSEJO]
> Puede pasar de un campo a otro con `Tab`.
```

Tipos válidos: `IMPORTANTE` (ámbar, «!»), `SI-ALGO-SALE-MAL` (rojo, «?») y `CONSEJO` (verde, «i»). Cada línea del
recuadro empieza con `>`. Dentro pueden ir párrafos, listas e imágenes. Una línea `>` vacía separa párrafos.
Los recuadros también pueden ir dentro de un paso (sangrados con 3 espacios).

## 7. Tablas

```
| Tecla | Para qué sirve |
| :--- | :--- |
| `F5` | Captura el peso del lado izquierdo |
```

Tabla de barras. La segunda línea define la alineación de cada columna: `:---` izquierda, `:---:` centro,
`---:` derecha. La primera fila es el encabezado (fondo azul). Dentro de las celdas sirven negritas, cursivas y
teclas, pero no listas ni imágenes. Para escribir una barra `|` dentro de una celda use `\|`.

## 8. Salto de página

```
<!-- salto -->
```

Fuerza una página nueva. No hace falta antes de un capítulo (`#`): ya empieza en página nueva.

## 9. Glosario

```
# Glosario {glosario}

- **Boleta**: documento impreso que respalda un pesaje.
- **Media res**: la mitad de un animal faenado.
```

El capítulo se dibuja como tabla «Término | Definición», **ordenada alfabéticamente** por el armado. Cada elemento
empieza con el término en negrita, dos puntos y la definición.

## 10. Hoja de teclas rápidas (última página)

```
# Hoja de teclas rápidas {teclas}

Imprima esta hoja y péguela junto a la balanza.

## Al pesar

| Tecla | Qué hace |
| :--- | :--- |
| `F5` | Captura el peso del **lado izquierdo** |
```

Es el **último capítulo** del manual y se arma en **una página propia** con letra y teclas más grandes y el título
centrado. Para que quepa en una sola hoja: una frase de introducción, entre 2 y 3 tablas con `##` y, en total,
unas 12 teclas como máximo. Puede llevar un recuadro al final.

## 11. Anotaciones de las capturas (`fuente/anotaciones-<rol>.json`)

Cada manual tiene **su propio** archivo de anotaciones: `fuente/anotaciones-Operador.json`,
`fuente/anotaciones-Supervisor.json` y `fuente/anotaciones-Administrador.json` (el nombre del rol es el `rol` de la
cabecera). Así tres personas pueden trabajar a la vez, y una misma captura se puede señalar distinto en cada manual.
Cada manual usa **solo** su archivo; las imágenes dibujadas van a `capturas/anotadas/<rol>/<id>.png`.
Si el archivo no existe, las capturas de ese manual se usan sin anotar.

Ejemplo real (`anotaciones-Operador.json`):

```json
{
  "comun-01-login": [
    { "control": "txtUsuario",  "numero": 1, "tipo": "recuadro", "leyenda": "Escriba su usuario" },
    { "control": "txtPassword", "numero": 2, "tipo": "recuadro", "leyenda": "Escriba su contraseña" },
    { "control": "btnIngresar", "numero": 3, "tipo": "recuadro", "leyenda": "Pulse Ingresar" }
  ],
  "op-01-inicio": [
    { "control": "tarjetaPesaje",    "numero": 1, "tipo": "recuadro" },
    { "control": "lblStatusBalanza", "numero": 2, "tipo": "recuadro", "posicion": "arriba-derecha" }
  ]
}
```

| Campo | Significado |
| :--- | :--- |
| clave (`comun-01-login`) | `id` de la captura en `capturas/originales/capturas.json`. |
| `control` | `nombre` de un elemento de la lista `controles` de esa captura (error si no existe; el mensaje lista los disponibles). |
| `area` | En lugar de `control`: `[x, y, ancho, alto]` en píxeles de la imagen, para señalar una zona sin control. |
| `numero` | Número que va dentro del círculo rojo (opcional: sin número solo se dibuja el recuadro). |
| `tipo` | `recuadro` (por omisión): rectángulo rojo redondeado alrededor del control **y** círculo numerado en su esquina superior izquierda, por fuera. `circulo`: solo el círculo numerado, sin rectángulo. |
| `posicion` | Opcional, dónde va el círculo: `arriba-izquierda` (por omisión), `arriba-derecha`, `izquierda`, `derecha`. Úsela si dos círculos se pisan o tapan un texto. |
| `leyenda` | Opcional: texto corto que se imprime bajo la figura cuando la imagen declara `{callouts=...}`. |

La validación de `{callouts=...}` (sección 5) se hace contra el archivo **del propio manual**. Después de editar el
archivo se vuelve a ejecutar el armado (`python construir-manuales.py --solo <rol>`): él mismo vuelve a dibujar las
anotaciones. Las capturas sin entradas se usan tal cual.
