# F6 — Remediación de la prueba manual (UI y boleta)

**Estado:** ✅ Completado el 2026-09-30 (T1–T8). Bitácora en `Agents.md` §5.

**Origen:** prueba manual de punta a punta del 2026-09-30 (login → alta rápida → pesaje con simulador →
vista previa → edición → anulación → auditoría) en una PC con escalado de Windows al **125 %**, más la
comparación de la boleta con el formato legado del cliente (cotización "Sistema de pesaje para faeneo",
guía FRISUR).

## Defectos y causa raíz

| # | Defecto observado | Causa raíz |
|---|---|---|
| D1 | Buscar boletas y Visor de auditoría: etiquetas cortadas ("Des", "Hast", "Esta", "Ganad", "Clien", "Usua"), fechas recortadas ("9/30/2…"), botones justos. | (a) `FormBase` fija `Segoe UI 10 pt`, pero **ningún formulario** (salvo `FrmConfiguracionBalanza`) activa `AutoScaleMode.Dpi`: el texto crece con el escalado de Windows y los tamaños en píxeles no. (b) Los filtros usan posiciones absolutas con anchos que ya no alcanzan a 96 ppp con 10 pt (p. ej. "N° boleta:" en 50 px). |
| D2 | Fechas de los filtros en formato de EE. UU. (`M/d/yyyy`). | `DateTimePickerFormat.Short` usa la configuración regional del equipo; el resto de la app usa `es-BO` (`dd/MM/yyyy`). |
| D3 | Reimprimir boleta: "Impresiones: ún no se impr…". | "Impresiones:" (x = 300) y su valor (x = 390) comparten fila con la fecha en negrita; a 10 pt la etiqueta pisa el valor y el valor se sale del `GroupBox`. |
| D4 | Reimprimir boleta: el número queda en blanco tras buscar. | A reproducir tras D1 (posible recorte vertical del `NumericUpDown` sin escalar). Si persiste, se corrige aparte. |
| D5 | Pantalla de pesaje: **no se ve el peso en vivo**. | Fila del display de 130 px con cuatro franjas acopladas (24 + 24 + 25 + 25): quedan ~30 px para una fuente de 48 pt (~85 px de alto de línea). Un `Label` que no puede mostrar la línea completa no dibuja nada. Falla también a 100 %. |
| D6 | Pantalla de pesaje: no se ven los botones "Capturar izq./der." ni "Borrar pesos"; los pesos capturados salen cortados abajo. | El `GroupBox` "Pesos Capturados" está en una fila `AutoSize` pero no tiene `AutoSize`: conserva su alto por defecto (100 px) y recorta la tabla interna (títulos + pesos 16 pt + botones). |
| D7 | Boleta media carta sin el bloque del formato legado: firmas **Jefe de Despacho** y **Médico Veterinario** y casillas para marcar el fletero. | Diseño F5 con una sola firma "Recibí conforme" y fleteros como lista sin casillas. |

## Tareas

- **T1 — Escalado global por DPI.** `FormBase`: `AutoScaleDimensions = 96×96` y `AutoScaleMode = Dpi` (los tamaños del código se leen como píxeles a 96 ppp y se escalan en `OnLoad`). Quitar la copia local de `FrmConfiguracionBalanza`. `ControlBusquedaCatalogo`: fijar su alto al `PreferredHeight` del cuadro de texto en `SetBoundsCore` para que el escalado no lo duplique.
- **T2 — Filtros de `FrmBuscarBoletas`.** Reemplazar las posiciones absolutas por dos filas `FlowLayoutPanel` con etiquetas `AutoSize` y el `GroupBox` con `AutoSize`. Fechas con `CustomFormat = "dd/MM/yyyy"`.
- **T3 — Filtros de `FrmVisorAuditoria`.** Igual que T2.
- **T4 — `FrmReimprimirBoleta`.** Datos en un `TableLayoutPanel` (etiqueta | valor) con "Impresiones" en su propia fila; reproducir D4 tras T1.
- **T5 — `FrmPesaje`.** Display en una tabla de dos columnas: izquierda (próxima boleta, estado de balanza), derecha (peso en vivo + ESTABLE); franja del simulador arriba a lo ancho; alto de fila suficiente para 48 pt. "Pesos Capturados" con `AutoSize`/`GrowAndShrink` para que se vean los tres botones.
- **T6 — Boleta.** Media carta: bloque inferior en tres recuadros como el legado: firma **Jefe de Despacho** | fleteros F1..F4 con casilla y línea "Otros: …" | firma **Médico Veterinario**; teléfonos de reclamos debajo. Ticket 80 mm: fleteros con casilla `[ ]` y las dos líneas de firma al final. Todo dentro de la página (prueba R18).
- **T7 — Pruebas.** Prueba de layout reutilizable (ningún control visible desborda a su contenedor y ninguna etiqueta/botón corta su texto) para Buscar boletas, Visor de auditoría, Reimprimir y Pesaje; el peso en vivo tiene alto para su fuente; los botones de captura son visibles; formato de fecha. Diseño de boleta: firmas, casillas y límites.
- **T8 — Verificación.** Build Release 0/0, suite completa en verde, prueba manual a 125 % con capturas de las pantallas corregidas, bitácora en `Agents.md`.

## Fuera de alcance

- Rediseño general de pantallas no afectadas; solo se verifica que T1 no las rompa (captura de cada una).
- Título "GUÍA DE REMISIÓN CARNE BOVINA" del legado: se mantiene "BOLETA DE PESAJE" (decisión de negocio pendiente con el cliente, R29).
