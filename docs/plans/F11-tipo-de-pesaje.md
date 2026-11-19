# F11 — Tipo de pesaje configurable (res completa / media res)

Plan de implementación para la solicitud: *"Que el pesaje de media res o res entera se determine en la configuración
(la pantalla que se abre al principio cuando no hay configuración). Hay clientes que solo pesan reses enteras y no les
interesa ver nada relacionado con media res en los reportes ni en toda la aplicación."*

Rama de trabajo: la actual (`feat/f9-empaquetado`). **Atención:** el árbol contiene todo F10 sin commit; se recomienda
commitearlo (o al menos crear un punto de control) antes de empezar F11, para poder revisar este cambio por separado.

---

## 0. Situación actual (hallazgos)

- `TipoDesposte` (`MediaRes = 1`, `ResCompleta = 2`) se elige **en cada pesaje** con `FrmPesaje.cmbDesposte`
  (por defecto "RES COMPLETA"). No hay ninguna configuración global.
- El tipo condiciona reglas reales, no solo textos: `ReglasPesaje.ValidarPesos` (res completa = dos lados y total
  10–1200 kg; media res = un lado), `ControlCapturas` (retorno a cero entre lados solo en res completa, `LadoNoPermitido`
  en media res) y `EvaluadorPantallaPesaje` (mensajes guía "Media res: capture un lado…").
- "Media res" aparece en la interfaz en: pesaje (combo), boleta (media carta: campo "Desposte:"; ticket: línea
  "Desposte:"), Editar / Anular / Reimprimir boleta (etiqueta de desposte), Buscar boletas e Historial (columna),
  reportes (columna "Tipo Corte" en grilla/PDF/Excel, línea "Boletas: N (res completa: x · media res: y)", fila Excel
  "Res completa / media res"), importador (campo "Tipo de desposte"), auditoría (campo "Tipo de desposte").
- El asistente de puesta en marcha (`FrmAsistentePuestaEnMarcha`) es un **centro de pasos**: cada paso muestra su
  estado y abre el formulario existente que lo resuelve; "Finalizar" exige los pasos obligatorios
  (`ReglasPuestaEnMarcha.PuedeFinalizar`) y marca `ParametrosSistema.ConfiguracionInicialCompleta`.

---

## 1. Decisiones

| # | Decisión |
|---|---|
| D1 | **Nuevo enum de dominio `ModoDesposte`**: `SoloMediaRes = 1`, `SoloResCompleta = 2`, `Ambos = 3` (los dos primeros alineados con los valores de `TipoDesposte`). Se ofrece también "Solo media res" porque la solicitud habla de "media res **o** res entera" y el costo es marginal; "Ambos" conserva el comportamiento actual para los clientes existentes. |
| D2 | **Se guarda en la base de datos**, en `ParametrosSistema.ModoDesposte` (`INTEGER NULL`, CHECK `IN (1, 2, 3)`), no en `config\*.json` por equipo: es una decisión del negocio, afecta a reportes sobre datos compartidos y debe viajar en los backups. `null` = **sin definir**; en tiempo de ejecución se trata como `Ambos` (nunca se bloquea el pesaje por no haberlo definido). |
| D3 | **Paso obligatorio del asistente**: "3. Tipo de pesaje (obligatorio)". Mientras `ModoDesposte` sea `null` el paso figura "Pendiente" y "Finalizar" queda deshabilitado (`ReglasPuestaEnMarcha.PuedeFinalizar`). Se usa `null` en lugar de un valor por defecto para que la elección sea **explícita**: guardar los datos de la empresa no debe marcar este paso como hecho. |
| D4 | **Formulario propio `FrmTipoPesaje`** (Administración › Tipo de pesaje, permiso `GestionarParametros`), abierto también desde el paso del asistente. No se mete dentro de `FrmParametros` por D3 (guardar la empresa no debe "elegir" el modo). |
| D5 | **El modo se puede cambiar después** (solo Administrador), con confirmación y auditoría `CambioParametros` (antes/después). Cambiarlo **nunca modifica boletas ya grabadas**. |
| D6 | **"Ocultar, nunca falsear"**: la configuración oculta lo relativo al desposte solo cuando el dato es redundante. Regla única `ReglasDesposte.MostrarDesposte(modo, tiposPresentes)` = `modo == Ambos` **o** hay en los datos mostrados algún tipo distinto del tipo único del modo (boletas históricas o importadas). Así un reporte con boletas antiguas de media res sigue mostrando la columna y el desglose. |
| D7 | **Defensa en profundidad**: `ServicioPesajes.RegistrarAsync` rechaza un `TipoDesposte` no permitido por el modo (`ReglaNegocioException` `PES_DESPOSTE_NO_PERMITIDO`), leyendo el modo en la misma unidad de trabajo, **antes** de abrir la transacción (la reserva del número de boleta debe seguir siendo su primera sentencia), junto con la validación de ganadero y cliente. La UI ya no lo ofrece, pero el servicio lo garantiza (cubre un cambio de modo con la pantalla de pesaje abierta). |
| D8 | **Reglas de pesaje intactas**: res completa sigue exigiendo los dos lados y el retorno a cero; media res, un solo lado. Lo único que cambia es quién decide el tipo (la configuración en lugar del combo). |
| D9 | **Totales de reportes**: se mantiene la regla de F7-D3 (res completa = 1, media res = 0,5) y la etiqueta "Cantidad Reses". Cuando `MostrarDesposte` es `false` se omiten la línea "Boletas: N (res completa: x · media res: y)" (PDF/grilla) y la fila "Res completa / media res" (Excel), y se muestra "Boletas: N". Ver P1 para "Solo media res". |
| D10 | **Importación sin cambios de comportamiento**: importa el tipo que traiga el archivo (o el deducido). Si no está permitido por el modo actual, agrega una incidencia de nivel **Advertencia** (no rechaza la fila: es historial). |
| D11 | **Lectura del modo para cualquier rol**: `IServicioParametros.ObtenerModoDesposteAsync()` solo exige sesión (`ExigirSesion`), porque lo necesitan el Operador (pesaje, historial) y el Supervisor (reportes, boletas), que no tienen `GestionarParametros`. Devuelve el modo **efectivo** (`null` → `Ambos`). |

---

## 2. Comportamiento por pantalla

| Lugar | Ambos (actual) | Solo res completa | Solo media res |
|---|---|---|---|
| Pesaje (`FrmPesaje`) | Combo "Desposte" | Fila del combo **oculta**; tipo fijo `ResCompleta` | Fila oculta; tipo fijo `MediaRes` |
| Mensajes guía | Sin cambios | Sin mensajes de media res (no son alcanzables) | "Capture un lado (F5 o F6)." |
| Boleta media carta / ticket | "Desposte: …" | Campo/línea omitidos si la boleta coincide con el modo (D6) | Igual |
| Editar / Anular / Reimprimir | Etiqueta "Desposte" | Oculta si coincide (D6) | Igual |
| Buscar boletas / Historial | Columna "Desposte" | Columna oculta si todas las filas cargadas coinciden (D6) | Igual |
| Reportes (grilla, PDF, Excel) | Columna "Tipo Corte" + desglose | Columna y desglose ocultos si todas las filas coinciden (D6, D9) | Igual |
| Importador | Sin cambios | Advertencia por fila de media res (D10) | Advertencia por fila de res completa |
| Asistente | — | Paso "Tipo de pesaje" | Paso "Tipo de pesaje" |

---

## 3. Tareas

Orden: T1 → T2 → T3 → T4 → (T5, T6, T7, T8 en cualquier orden) → T9 → T10.
Después de cada tarea: `dotnet build src/PesajeFaeneo.sln -c Release` (0 warnings) y los tests afectados.

### T1 — Dominio, persistencia y migración (D1, D2)

- `Domain/Enums/Enums.cs`: `public enum ModoDesposte { SoloMediaRes = 1, SoloResCompleta = 2, Ambos = 3 }`.
- `ParametrosSistema.ModoDesposte` (`ModoDesposte?`, sin valor inicial).
- `Domain/Reglas/ReglasDesposte.cs` (pura, sin dependencias):
  - `ModoDesposte Efectivo(ModoDesposte? modo)` → `modo ?? Ambos`.
  - `TipoDesposte? TipoUnico(ModoDesposte modo)` → `ResCompleta` / `MediaRes` / `null`.
  - `bool Permite(ModoDesposte modo, TipoDesposte tipo)`.
  - `bool MostrarDesposte(ModoDesposte modo, IEnumerable<TipoDesposte> presentes)` (D6; con colección vacía y modo
    único → `false`).
  - `string Texto(ModoDesposte modo)` para la UI y la auditoría ("Res completa y media res", "Solo res completa",
    "Solo media res").
- `ParametrosSistemaConfiguration`: propiedad opcional con conversión a entero y
  `HasCheckConstraint("CK_ParametrosSistema_ModoDesposte", "ModoDesposte IS NULL OR ModoDesposte IN (1, 2, 3)")`.
  `HasData` no lo fija (queda `null`).
- Migración `F11_ModoDesposte`: `AddColumn` nullable (sin reconstruir la tabla si EF lo permite; si la CHECK obliga a
  reconstruir, verificar como en F2/F10: datos, `PRAGMA foreign_key_check`) y, **para instalaciones ya puestas en
  marcha**, `UPDATE ParametrosSistema SET ModoDesposte = 3 WHERE ConfiguracionInicialCompleta = 1` (conservan "Ambos"
  de forma explícita; las nuevas quedan en `null` y el asistente lo pide).
- `dotnet ef migrations has-pending-model-changes` limpio.
- Tests: `ReglasDesposteTests` (todas las combinaciones, colección vacía, tipos mezclados), migración (columna nullable,
  CHECK rechaza 0 y 4, `UPDATE` aplicado solo con `ConfiguracionInicialCompleta = 1`), comparación modelo/snapshot.

### T2 — Servicio de parámetros (D4, D5, D11)

- `IServicioParametros`:
  - `Task<ModoDesposte> ObtenerModoDesposteAsync(...)` — solo `ExigirSesion` (D11); devuelve el modo efectivo.
  - `Task<ConfiguracionDesposte> ObtenerConfiguracionDesposteAsync(...)` — `GestionarParametros`; DTO
    `ConfiguracionDesposte(ModoDesposte? Modo, int BoletasResCompleta, int BoletasMediaRes)` (conteo de vigentes, para
    avisar en el formulario qué boletas existentes seguirán apareciendo).
  - `Task CambiarModoDesposteAsync(ModoDesposte modo, ...)` — `GestionarParametros`; valida el enum
    (`ValidacionException` si no es 1–3); si no cambia (y no era `null`), no guarda ni audita; si cambia, actualiza
    `ModificadoEn` y registra `AccionAuditoria.CambioParametros` con `antes/despues = { ModoDesposte }` en la misma
    llamada a `GuardarCambiosAsync`.
- `IPesajeRepositorio.ContarVigentesPorTipoDesposteAsync` (dos conteos con `GROUP BY`; sin cargar filas).
- `DiferenciasAuditoria.EtiquetaCampo`: `"ModoDesposte" => "Tipo de pesaje"`, valor mostrado con
  `ReglasDesposte.Texto` (y "Sin definir" para `null`).
- Tests: permisos (Operador/Supervisor pueden leer el modo y no cambiarlo), `null` → `Ambos`, cambio auditado con
  antes/después, sin cambio no audita, primer guardado desde `null` sí audita, valor inválido rechazado.

### T3 — Formulario `FrmTipoPesaje` y menú (D4, D5)

- `WinForms/Formularios/Administracion/FrmTipoPesaje.cs` (`FormBase`, creado por `IFormFactory`):
  - Tres `RadioButton` con una línea de explicación cada uno:
    - **Res completa y media res** — "Se elige en cada pesaje."
    - **Solo res completa** — "Siempre se pesan los dos lados. No se muestra «media res» en el pesaje, las boletas ni
      los reportes."
    - **Solo media res** — "Se pesa un solo lado por boleta."
  - Con modo `null`, ningún botón marcado y "Guardar" deshabilitado hasta elegir (D3: elección explícita).
  - Nota fija: "Cambiar esta opción no modifica las boletas ya grabadas." Si hay boletas vigentes de un tipo que el modo
    elegido ocultaría, aviso en naranja: "Hay N boletas de media res; seguirán apareciendo en los reportes que las
    incluyan."
  - Cambio desde un modo ya definido → confirmación (`MessageBox` fuera de `EjecutarAsync`, regla 0.10) con gancho
    `...ParaTest`.
  - Layout: `TableLayoutPanel` con `ColumnStyle`, `AutoSize` en etiquetas y botones (convenciones DPI de §4).
- `FrmPrincipal`: ítem "Administración › &Tipo de pesaje" debajo de "Parámetros del sistema", permiso
  `GestionarParametros` en `AutorizadorMenu`.
- Tests: carga con `null` (nada marcado, Guardar deshabilitado), guardar llama al servicio con el modo elegido,
  confirmación al cambiar (aceptar/cancelar), aviso de boletas existentes, layout sin recortes a 96 DPI y con
  `Scale(1,25)` (`VerificadorLayout`), menú oculto para Operador y Supervisor.

### T4 — Asistente de puesta en marcha (D3)

- `EstadoPuestaEnMarcha`: nuevo campo `ModoDesposte? ModoDesposte` (leído en `ServicioPuestaEnMarcha.ObtenerEstadoAsync`).
- `ReglasPuestaEnMarcha.PuedeFinalizar`: además exige `ModoDesposte is not null`. `CamposPuestaEnMarcha.TipoPesaje`.
- `ServicioPuestaEnMarcha.MarcarCompletaAsync`: `ValidacionException(CamposPuestaEnMarcha.TipoPesaje,
  "Elija el tipo de pesaje antes de finalizar.")` si es `null` (defensa en profundidad).
- `FrmAsistentePuestaEnMarcha`:
  - Nuevo `PasoPuestaEnMarcha.TipoPesaje = 8` (valor nuevo al final para no alterar los existentes) ubicado
    **tercero** en el arreglo `Pasos` (después de "Datos de la empresa"); renumerar los títulos visibles 1–8.
  - Título "3. Tipo de pesaje (obligatorio)", detalle "Res completa, media res o ambos. Define qué se muestra al pesar,
    en las boletas y en los reportes.", permiso `GestionarParametros`, abre `FrmTipoPesaje`.
  - Estado: "Pendiente" (naranja) si `null`; si no, "Listo" + texto corto del modo (verde).
  - `lblResumen`: "Falta: elegir el tipo de pesaje." (respetando el orden de los mensajes existentes).
- Tests: `ReglasPuestaEnMarchaTests` (no finaliza sin modo), servicio (estado incluye el modo; `MarcarCompletaAsync`
  rechaza `null`), formulario (paso visible en la posición 3, estado Pendiente/Listo, Finalizar deshabilitado sin modo,
  `AbrirPasoParaTest(TipoPesaje)`), layout del asistente con un paso más (alto mínimo `820×600` sigue sin recortes).

### T5 — Pantalla de pesaje y servicio de grabado (D7, D8)

- `FrmPesaje`:
  - Al cargar (dentro del `EjecutarAsync` de carga inicial, junto a lo que ya se lee) obtener
    `ObtenerModoDesposteAsync()`.
  - Modo único: `lblDesp` y `cmbDesposte` con `Visible = false` (la fila `AutoSize` colapsa; verificar que el
    `TabIndex` siguiente siga en orden) y `TipoSeleccionado` devuelve `ReglasDesposte.TipoUnico(modo)`.
  - "Ambos": comportamiento actual sin cambios.
  - `RestablecerCampos` tras grabar no debe volver a mostrar el combo.
- `EvaluadorPantallaPesaje`: recibir el modo (o un `bool DesposteFijo`) para que en "Solo media res" el mensaje sea
  "Capture un lado (F5 o F6)." sin el prefijo "Media res:"; el resto de mensajes no cambia.
- `ServicioPesajes.RegistrarAsync`: antes de abrir la transacción (junto a ganadero y cliente), leer `Parametros.ModoDesposte` y rechazar con
  `ReglaNegocioException("PES_DESPOSTE_NO_PERMITIDO", "La configuración solo permite pesar {texto}. Cierre y vuelva a
  abrir la pantalla de pesaje.")` si `!ReglasDesposte.Permite(...)`. Sin cambios en `ReglasPesaje` ni `ControlCapturas`.
- Tests: formulario (combo oculto en cada modo único y visible en "Ambos", el tipo enviado al servicio es el del modo,
  captura de un lado en "Solo media res", retorno a cero exigido en "Solo res completa"), layout de `FrmPesaje` sin
  la fila (96 DPI y 1,25), servicio (rechaza tipo no permitido, acepta el permitido, `null` permite ambos),
  evaluador (mensaje sin prefijo).

### T6 — Boleta (D6)

- `DatosBoleta` (`BoletaDtos`): `bool MostrarDesposte = true` (valor por defecto para no romper llamadas existentes).
- `ServicioBoletas`: al armar los datos, leer el modo de `Parametros` (ya lee la empresa) y fijar
  `MostrarDesposte = ReglasDesposte.MostrarDesposte(modo, [p.TipoDesposte])`. La boleta de muestra/calibración usa
  `TipoUnico(modo) ?? ResCompleta`.
- `DisenoBoleta`:
  - Media carta: si `!MostrarDesposte`, no agregar "Desposte:" ni su valor (fila 2, columna derecha queda vacía; no
    mover las demás filas para no alterar la calibración del papel).
  - Ticket: omitir la línea "Desposte: …" (las alturas ya se calculan por línea con `CajaTexto`).
- Tests: `DisenoBoletaTests` (sin "Desposte" con `MostrarDesposte = false` en ambos formatos, sin solapes, resto de
  elementos en las mismas coordenadas), `ServicioBoletasTests` (boleta de media res con modo "Solo res completa" →
  sí muestra el desposte, por D6). Verificación visual renderizando la muestra a PNG, como en F10-D9.

### T7 — Reportes (D6, D9)

- `ReportePesajes`: nuevo `bool MostrarDesposte` (calculado en `ServicioReportes` con el modo leído junto a
  `Parametros` y los tipos de las filas del reporte).
- `ColumnasReporte.Para(bool anuladas, bool mostrarDesposte)` y `Textos(f, anuladas, mostrarDesposte)`: omitir la
  columna "Desposte" ("Tipo Corte"). La columna "Dirección" (normal) y "Motivo" (anuladas) son relativas y absorben
  el ancho liberado. Mantener la sobrecarga actual o actualizar todos los llamadores.
- `TextosReporte.DetalleBoletas`: con `MostrarDesposte = false` → "Boletas: N".
- `DisenoPdfReporte`: usa las columnas de `ColumnasReporte` (ya es declarativo); solo cambia el pie de totales.
- `GeneradorExcelReportes`: hoy escribe por **índice fijo** (14 = Desposte, 15 = Origen; anuladas 8 = Desposte).
  Refactor mínimo: calcular el índice de cada columna a partir de una lista de columnas visibles (o desplazar las
  posteriores cuando se oculta). Omitir la fila de totales "Res completa / media res" y ajustar `ultimaFilaUsada`.
  La columna de totales (`colT = 13`, Total) no se desplaza porque Desposte va después.
- `FrmReportes`: ya reconstruye las columnas de la grilla (`dgvVista.Columns.Clear()` + `ColumnasReporte.Para`);
  pasarle también `reporte.MostrarDesposte` y verificar que se reconstruyan **por reporte generado** (no solo al
  cambiar de tipo), conservando el `Tag` por fila y el índice de la columna fecha (`idxFecha`, buscado por clave).
- Tests: servicio (modo único con todas las filas coincidentes → `false`; con una boleta histórica distinta → `true`;
  "Ambos" → `true`; reporte vacío con modo único → `false`), `ColumnasReporte` (11/12 columnas normales, 9/10 anuladas,
  anchos), PDF (sin el texto "Tipo Corte" ni "media res" con `MostrarDesposte = false`), Excel (Origen en la columna
  correcta en ambos casos, sin la fila de desglose, formatos de celda intactos), `FrmReportes` (columnas reconstruidas
  al cambiar de un reporte con desposte a otro sin él), `AutopruebaInstalacion` sigue en verde.

### T8 — Boletas, historial e importación (D6, D10)

- `FrmBuscarBoletas` y `FrmHistorialPesajes`: obtener el modo al abrir; tras cada carga, `Visible` de la columna
  "Desposte" = `MostrarDesposte(modo, filas.Select(f => f.TipoDesposte))`.
- `FrmEditarBoleta`, `FrmAnularBoleta`, `FrmReimprimirBoleta`: ocultar la etiqueta/fragmento "Desposte" si
  `MostrarDesposte(modo, [tipo])` es `false` (en `FrmAnularBoleta`, quitar el fragmento del texto del resumen).
  Preferible que el modo llegue en el DTO que cada formulario ya carga (agregar `bool MostrarDesposte` al DTO de
  detalle en `ServicioGestionPesajes`/`ServicioBoletas`) para no sumar una llamada más por formulario.
- `PreparadorImportacion`: leer el modo una vez por importación; por cada pesaje cuyo tipo no esté permitido, incidencia
  `NivelIncidencia.Advertencia` "Tipo de desposte «MEDIA RES» distinto del tipo de pesaje configurado (se importa
  igual)." Simulacro e importación real siguen siendo idénticos (D14 de F8: misma huella).
- Tests: columnas ocultas/visibles en buscador e historial según modo y datos, etiquetas en los tres formularios,
  importación con advertencia (no error) y conteo de filas importadas sin cambios.

### T9 — Herramienta de capturas, manuales y UAT

- `SembradorDemo`: fijar `ModoDesposte = Ambos` antes de sembrar (sus pesajes mezclan ambos tipos); agregar al guion
  escenas de `FrmTipoPesaje`, del asistente con el paso nuevo y de `FrmPesaje` en "Solo res completa" (sin combo).
  Implementado como escenas `ad-25` a `ad-27` (`ad-21`/`ad-22` ya existían).
- Manuales (`docs/manual/clientes/fuente/manual-*.md` y anotaciones): sección "Tipo de pesaje" en el manual del
  Administrador (asistente y menú); en Operador/Supervisor, nota "Si su empresa pesa solo res completa, no verá la
  opción de desposte". Regenerar con `construir-manuales.py`. `docs/manual/manual-usuario.md` (referencia técnica).
- `docs/puesta-en-marcha/checklist-uat.md`: casos UAT-F11-01… (asistente exige el paso; cada modo en pesaje, boleta
  impresa, reportes PDF/Excel; cambio de modo con boletas históricas; importación con advertencia; restaurar un backup
  previo a F11).

### T10 — Cierre

- Versión `1.2.0` en `Directory.Build.props`; entrada en `CHANGELOG.md`; sección en `README.md`.
- `Agents.md`: fila F11 en §2, HUs en §3, viñeta "Tipo de pesaje (F11)" en §4 y bitácora en §5.
- Compuertas: build Release 0 warnings, suite completa en verde dos corridas seguidas,
  `has-pending-model-changes` limpio, archivos nuevos en UTF-8 sin BOM y CRLF (`.editorconfig`).

---

## 4. Archivos principales

| Capa | Archivos |
|---|---|
| Domain | `Enums/Enums.cs`, `Entities/Entities.cs`, `Reglas/ReglasDesposte.cs` (nuevo) |
| Application | `Parametros/IServicioParametros.cs`, `ServicioParametros.cs`, `Dtos/ParametrosDtos.cs`; `PuestaEnMarcha/*`; `Pesajes/ServicioPesajes.cs`, `ServicioGestionPesajes.cs`, DTOs; `Boletas/ServicioBoletas.cs`, `Dtos/BoletaDtos.cs`, `Diseno/DisenoBoleta.cs`; `Reportes/ServicioReportes.cs`, `ColumnasReporte.cs`, `TextosReporte.cs`, `Dtos/ReporteDtos.cs`; `Auditoria/DiferenciasAuditoria.cs`; `Utilitarios/Importacion/PreparadorImportacion.cs`; `Abstracciones/Persistencia/IPesajeRepositorio.cs` |
| Infrastructure | `Persistencia/Configuraciones/ParametrosSistemaConfiguration.cs`, migración `F11_ModoDesposte`, repositorio de pesajes, `Reportes/GeneradorExcelReportes.cs`, `Reportes/DisenoPdfReporte.cs` |
| WinForms | `Administracion/FrmTipoPesaje.cs` (nuevo), `FrmPrincipal.cs`, `PuestaEnMarcha/FrmAsistentePuestaEnMarcha.cs`, `Pesaje/FrmPesaje.cs`, `Presentacion/Pesaje/EvaluadorPantallaPesaje.cs`, `Pesaje/FrmHistorialPesajes.cs`, `Boletas/FrmBuscarBoletas.cs`, `FrmEditarBoleta.cs`, `FrmAnularBoleta.cs`, `FrmReimprimirBoleta.cs`, `Reportes/FrmReportes.cs`, `Capturas/SembradorDemo.cs`, `Capturas/GuionOperador.cs` |

---

## 5. Riesgos

- **Cambio de modo con boletas existentes:** por D6 los reportes que mezclen tipos seguirán mostrando el desposte; es
  intencional (no se oculta un dato que distingue filas). Documentarlo en el manual del Administrador.
- **Excel por índices fijos:** el refactor de T7 es el punto con más probabilidad de regresión (columna Origen
  desplazada, formatos aplicados a la columna equivocada); cubrirlo con tests de celda.
- **Calibración de la media carta:** ocultar "Desposte:" no debe desplazar otros elementos (se deja el hueco).
- **Instalaciones con el asistente sin terminar:** verán un paso obligatorio nuevo; es el comportamiento buscado.

---

## 6. Preguntas abiertas (se asume la opción indicada)

| # | Pregunta | Supuesto |
|---|---|---|
| P1 | En "Solo media res", ¿el total debe decir "Cantidad Reses: 12,5" (regla actual, 0,5 por boleta) o "Medias reses: 25"? | Se mantiene la regla y la etiqueta de F7-D3. |
| P2 | ¿Hace falta la opción "Solo media res" o basta con "Solo res completa" / "Ambos"? | Se incluye (costo bajo, simétrico). |
| P3 | ¿Se debe impedir cambiar el modo una vez emitida la primera boleta (como la numeración en F10-D1)? | No: se permite al Administrador, con confirmación y auditoría (D5). |
| P4 | Al importar datos legados con un tipo no permitido, ¿advertir o rechazar la fila? | Advertir e importar (D10). |
| P5 | En "Solo res completa", ¿la boleta debe seguir imprimiendo "RES COMPLETA" como dato informativo? | No se imprime (D6); si el cliente lo quiere, basta con forzar `MostrarDesposte = true` en la boleta. |
