# F10 — Observaciones del cliente (post-F9)

Plan de implementación para las observaciones recibidas tras la revisión del cliente.
Rama de trabajo: `feat/f9-empaquetado` (árbol de trabajo actual, **sin commits** y sin cambiar de rama).

> **Atención — cambios previos sin commit:** el árbol ya contiene trabajo en curso que hay que **conservar y
> respetar** (pantalla de inicio con logo y tarjetas): `FrmPrincipal.cs`, `FrmAsistentePuestaEnMarcha.cs`,
> `Controles/TarjetaAccion.cs`, `FrmPrincipalTests.cs`, `FrmAsistentePuestaEnMarchaTests.cs`. No revertirlos;
> integrarse con ellos.

---

## 0. Observaciones y decisiones

| # | Observación del cliente | Decisión |
|---|---|---|
| O1 | "Averigua sobre la secuencia de los tickets. La secuencia no debe moverse más que de forma automática, por auditoría." | Hallazgo: el Administrador puede escribir cualquier "Siguiente número de boleta" en Parámetros (`FrmParametros.numSiguienteBoleta`, `ServicioParametros.ActualizarAsync`). **D1:** el número de boleta solo lo mueven el grabado (`UPDATE … RETURNING`) y la importación (`max(…)`). En Parámetros se **muestra** el siguiente número (solo lectura). **Única excepción:** mientras no exista **ningún** pesaje en la BD se puede fijar el número inicial (continuidad con el talonario legado), auditado como hoy. La "Secuencia" por ganadero del día **no cambia** (el cliente solo pidió el número de boleta). |
| O2 | Botón "Borrar pesos (F8)" → no se borra un peso, se borra el pesaje. | **D2:** renombrar a "Borrar pesaje (F8)" (texto, nombres internos, mensajes guía, tests). Comportamiento igual: descarta las capturas del pesaje en curso. |
| O3 | El historial está dentro de Registro de pesaje; debe tener su propio apartado. | **D3:** nuevo formulario `FrmHistorialPesajes`; `FrmPesaje` queda solo con el registro. |
| O4 | Ganadero: "Marca" se llamará "Código". | **D4:** renombrar Marca → Código en todo el sistema (UI, boleta, reportes, importador, auditoría, código y columna de BD). |
| O5 | Pueden ser N fleteros, no solo 4. | **D5:** sin límite de fleteros; el fletero se **elige al pesar** (opcional) y la boleta imprime solo ese fletero. |
| O6 | Código no digitable. | **D6:** Ganadero, Cliente y Fletero reciben un código numérico correlativo **automático** al crearse; se muestra en solo lectura y no se puede cambiar. |
| O7 | Logo en el login, con el nombre de la empresa. | **D7:** encabezado de marca en `FrmLogin` (logo + nombre), leído sin sesión. |
| O8 | Reporte por raza. | **D8:** nuevo `TipoReporte.PorRaza`. |
| O9 | Fletero no está bien ubicado en la impresión de ejemplo. | **D9:** el fletero pasa a la zona de datos de la boleta; el recuadro central inferior pasa a ser la firma del fletero. Verificación visual renderizando la muestra a PNG. |

---

## 1. Tareas

Orden sugerido (dependencias): T1 → T2 → T3 → T4 → T5 → T6 → T7 → T8 → T9 → T10 → T11.
Después de cada tarea: `dotnet build src/PesajeFaeneo.sln -c Release` (0 warnings) y los tests afectados.

### T1 — Número de boleta automático (O1, D1)

- `ServicioParametros.ActualizarAsync`: si `dto.SiguienteNumeroBoleta != p.SiguienteNumeroBoleta` **y** existe al
  menos un pesaje (`ObtenerUltimoNumeroBoletaAsync` ≠ null, o un `Any` en `Pesajes` que incluya anulados),
  lanzar `ReglaNegocioException` con código nuevo `PAR_NUMERACION_AUTOMATICA` y mensaje claro
  ("El número de boleta es automático y no puede modificarse una vez emitida la primera boleta.").
  Defensa en profundidad: la UI no debe permitirlo, pero el servicio lo garantiza.
- Exponer en `ParametrosDto` un `bool NumeracionEditable` (true solo si no hay pesajes).
- `FrmParametros`: `numSiguienteBoleta` → `ReadOnly = true`, `Enabled = false` (o sustituirlo por una etiqueta) cuando
  `NumeracionEditable == false`, con una nota "Se asigna automáticamente al grabar cada pesaje". Mientras no haya
  pesajes queda editable con la nota "Solo puede fijarse antes de emitir la primera boleta".
- Revisar `FrmAsistentePuestaEnMarcha`/`ServicioPuestaEnMarcha`: si algún paso habla de "numeración", ajustar el texto.
- La importación (`RepositorioImportacion`, `max(...)`) no cambia.
- Tests: servicio rechaza cambio con pesajes (incluye pesaje anulado), acepta sin pesajes, acepta guardar sin cambiar el
  número; formulario deshabilita el control con pesajes y lo habilita sin ellos.

### T2 — "Borrar pesaje (F8)" (O2, D2)

- `FrmPesaje`: `btnBorrarPesos` → `btnBorrarPesaje`, texto "Borrar pesaje (F8)", método `BorrarPesaje()`.
- `EvaluadorPantallaPesaje` / `ControlCapturas`: renombrar `PuedeBorrarPesos`/`BorrarPesos` a `…Pesaje` y cualquier
  mensaje guía que diga "pesos borrados" → "pesaje borrado". Confirmar si hoy hay diálogo de confirmación; mantener el
  comportamiento actual.
- Actualizar tests y el manual (`docs/manual/`) donde se mencione "Borrar pesos".

### T3 — Historial en su propio apartado (O3, D3)

- Nuevo `WinForms/Formularios/Pesaje/FrmHistorialPesajes.cs` (`FormBase`, creado por `IFormFactory`, no registrado en DI):
  - Filtro de **día** (`DateTimePicker` con formato fijo `dd/MM/yyyy` vía `DisenoFiltros`, por defecto hoy) y botón
    "Actualizar" (F5). Usa la misma consulta que hoy alimenta `dgvHoy` (`ServicioPesajes`, rango con `RangoDiaLocal`);
    si el servicio solo admite "hoy", agregar un parámetro de fecha sin romper la firma existente.
  - Grilla con las columnas actuales de `dgvHoy` (con "Código" en lugar de "Marca", ver T4, y "Fletero", ver T6),
    columnas `NotSortable` si la fila se resuelve por índice, filas anuladas en gris (como `FrmBuscarBoletas`).
  - Acción "Reimprimir (F9)" sobre la fila seleccionada usando el flujo de reimpresión existente (auditoría
    `Reimprimir`, permiso `ReimprimirBoleta`); oculto/deshabilitado sin permiso.
  - Token de versión para descartar respuestas tardías (patrón `_versionSugerencia`), regla 0.10 de reentrada.
  - Escalado DPI: `TableLayoutPanel` con `ColumnStyle` en columnas `Fill`, `AutoSize` en etiquetas y botones.
- Permiso: el mismo que hoy permite ver la grilla del día dentro del pesaje (el Operador debe poder verlo). Verificar en
  `MatrizPermisos` y `AutorizadorMenu`.
- `FrmPrincipal`: ítem de menú "Historial de pesajes" en el menú de pesaje con un atajo libre (verificar los ya usados:
  F3 reimprimir, F6 buscador, etc.). Si la pantalla de inicio (trabajo sin commit) tiene espacio, agregar una tarjeta
  "Historial" con el mismo estilo de `TarjetaAccion`; si no, solo menú.
- `FrmPesaje`: quitar `dgvHoy`, `CargarGrillaHoyAsync` y el panel de grilla. **El simulador sigue en `FrmPesaje`**
  (reubicar su grupo en el panel izquierdo/inferior). La pantalla debe aprovechar el ancho liberado (display de peso y
  datos). `F9 Imprimir` en el pesaje pasa a imprimir **el último pesaje grabado en esta pantalla** (ya no depende de la
  fila seleccionada); para otros, el historial o Reimprimir (F3).
- Tests: layout de `FrmPesaje` sin grilla y sin recortes a 96 DPI y `Scale(1,25)` (`VerificadorLayout`), F9 tras grabar,
  `FrmHistorialPesajes` (carga del día, cambio de día, respuesta tardía descartada, reimprimir llama al servicio,
  sin permiso oculto, layout), menú visible para Operador, `ContenedorDITests` si aplica.

### T4 — Ganadero: "Marca" → "Código" (O4, D4)

- Dominio: `Ganadero.Marca` → `Ganadero.Codigo` (sigue siendo `string`, máx. 5, `NOCASE`, único) para conservar los
  códigos alfanuméricos ya importados del sistema legado.
- Migración nueva `F10_ObservacionesCliente` (o separar por tema si queda más claro) que **renombre la columna**
  `Ganaderos.Marca` → `Codigo` conservando datos, el índice único y la CHECK (renombrada `CK_Ganaderos_Codigo`,
  `length(Codigo) BETWEEN 1 AND 5`). Si EF reconstruye la tabla, verificar `NOCASE`, índices y
  `PRAGMA foreign_key_check` como en F2.
- Renombrar en todas las capas: DTOs (`CatalogoDtos`, `PesajeDtos`, `GestionPesajesDtos`, `ReporteDtos`, `BoletaDtos`),
  `CamposCatalogo.Marca` → `Codigo`, `ReglasCatalogos.*Marca*`, repositorio (`ObtenerPorMarca…` → `…PorCodigo`),
  `ServicioGanaderos`, `ServicioPesajes`/`ServicioGestionPesajes`, `FabricaAuditoria` (snapshots), `DiferenciasAuditoria`,
  `ColumnasReporte`/`ServicioReportes`/`GeneradorExcelReportes`, `TextosBoleta.Ganadero`, `MotorBusqueda`/`ElementoBusqueda`
  (el código sigue siendo el criterio de mayor prioridad), formularios (`FrmGanaderos`, `FrmGanaderoEdicion`, `FrmPesaje`,
  `FrmBuscarBoletas`, `FrmAnularBoleta`, `FrmReportes`, `FrmAsistentePuestaEnMarcha`), `ServicioPuestaEnMarcha`.
- **Cuidado con falsos positivos:** "Marca de agua" (boleta), `NonSpacingMark`, marcas en backup/restauración/logs
  (`AplicadorRestauracion`, `SqliteBackupService`, `ManifiestoBackup`, `FrmVisorLogs`, `FrmConfiguracionBalanza`,
  `ISesionActual`, `AutopruebaInstalacion`) **no** se refieren al ganadero; revisar cada coincidencia antes de cambiarla.
- Importador: el campo de importación de ganaderos se llama "Código" (`CamposImportacion`), pero `SugeridorMapeo` debe
  seguir reconociendo columnas legadas llamadas `MARCA` (y `CODIGO`). La importación **sí** conserva el código del
  archivo legado (es el único camino no automático; documentarlo).
- Auditoría histórica: los JSON antiguos tienen la clave `Marca`; el visor debe mostrarlos sin romperse (mostrar
  "Código" para ambas claves en `DiferenciasAuditoria` si es trivial).
- Mensajes y validaciones: "La marca…" → "El código…".

### T5 — Códigos automáticos en catálogos (O6, D6)

Regla común: al **crear**, el servicio asigna el código; el DTO de alta ya no lo recibe (o lo ignora); la edición no
puede cambiarlo. Asignación dentro de la misma transacción (`ITransaccion`) que el alta y su auditoría; ante
`ConflictoException` por índice único (dos altas simultáneas) reintentar una vez con el siguiente código.

- **Ganadero:** siguiente = (mayor código **numérico** existente, incluidos inactivos) + 1; los códigos alfanuméricos
  legados se ignoran para el cálculo. Si se supera 99999 → `ReglaNegocioException`. Implementar el cálculo en el
  repositorio (`ObtenerSiguienteCodigoAsync`); el catálogo es pequeño, se puede filtrar en memoria.
- **Cliente:** ya existe la sugerencia del siguiente código (D6 de F2); convertirla en asignación obligatoria
  (max + 1, incluidos inactivos).
- **Fletero:** ver T6 (Orden → Codigo).
- `CrearRapidoAsync` (alta rápida desde el pesaje, F4-07) también asigna el código automático; quitar el campo de
  código del diálogo de alta rápida.
- UI de edición (`FrmGanaderoEdicion`, `FrmClienteEdicion`, `FrmFleteroEdicion`): el código se muestra en solo lectura
  ("Se asignará al guardar" en altas, el valor real en ediciones); el foco inicial pasa a Nombre.
- Eliminar la lógica D5 de F2 "la clave ya existe inactiva, ¿reactivar?" en el alta (ya no se escribe la clave); la
  reactivación sigue disponible desde la lista.
- Validadores: quitar la validación de formato/rango del código en altas manuales (se mantiene para importación).
- Tests: asignación correlativa, ignora alfanuméricos, incluye inactivos, reintento ante conflicto, edición no cambia el
  código, alta rápida, formulario sin campo editable.

### T6 — N fleteros y fletero por pesaje (O5, D5)

- Dominio: `Fletero.Orden` → `Fletero.Codigo` (`int`, automático, ver T5). Quitar `OrdenFleteroMaximo` y
  `MaximoFleterosActivos` y toda regla que limite a 4.
- Migración: renombrar columna `Orden` → `Codigo`; CHECK `Codigo >= 1` (en lugar de `BETWEEN 1 AND 4`); índice único
  **sin filtro** (hoy es único solo entre activos). Antes de crear el índice, el SQL de la migración debe renumerar los
  inactivos que choquen con otro código (p. ej. `max(Codigo) + Id`) para no fallar con datos existentes.
- `Pesaje.FleteroId` (`int?`, FK a `Fleteros`, `OnDelete Restrict`) + navegación `Fletero?`; índice simple por FK.
- Registro de pesaje: `RegistrarPesaje` (DTO) con `FleteroId?`; `ServicioPesajes` valida dentro de la transacción que el
  fletero exista y esté activo (como ganadero/cliente); `PesajeAuditoria` incluye código y nombre del fletero.
- `FrmPesaje`: selector de fletero **opcional** con `ControlBusquedaCatalogo` (búsqueda por código o nombre), ubicado
  tras Cliente/Dirección en el orden de tabulación y en el flujo Enter; se limpia junto con el resto tras grabar.
  Respetar `ProcessCmdKey`, `IsInputKey` y la política de foco existentes.
- Edición de boleta (F6): el fletero es atributo descriptivo editable (`FrmEditarBoleta`, `ServicioGestionPesajes`,
  `ReglasGestionPesajes`, detección de "sin cambios", diff de auditoría).
- `FrmFleteros`/`FrmFleteroEdicion`/`ServicioFleteros`: lista y ABM sin límite; columnas Código | Nombre | Celular | Activo.
- Boleta: `DatosBoleta.Fleteros` (lista) → `DatosBoleta.Fletero` (`FleteroBoleta?` con código, nombre y celular);
  `ServicioBoletas` lo carga desde el pesaje (y la muestra usa un fletero de ejemplo). Ver T7 para el diseño.
- Reportes: columna "Fletero" en `ColumnasReporte` (grilla, PDF y Excel), vacía si no hay; ajustar anchos para que el
  PDF siga entrando en la página (verificar con los tests de PDF existentes).
- Buscador de boletas e historial: columna "Fletero".
- Tests: migración con datos (inactivos duplicados, pesajes existentes con `FleteroId` nulo), alta del 5.º y 10.º
  fletero, grabado con y sin fletero, fletero inactivo rechazado, edición de fletero auditada, reportes.

### T7 — Ubicación del fletero en la boleta (O9, D9)

1. **Antes de tocar nada:** escribir un test/utilidad temporal (o un test que guarde el PNG en la carpeta de salida
   de tests) que pinte la **muestra** (`ServicioBoletas.CrearMuestraAsync` → `DisenoBoleta.Crear` → `PintorBoleta`)
   en un `Bitmap` para media carta y ticket 80 mm, abrir los PNG y anotar qué está desalineado (solapes con bordes de
   recuadros, texto cortado por alto de caja < alto de línea de la fuente, "Fleteros:" pegado al borde superior en
   y=107,5 con el recuadro en y=107, etc.).
2. Media carta (`DisenoBoleta.CrearMediaCarta`):
   - Agregar "Fletero:" en la fila 4, columna derecha (hoy libre: X=126/150, Y=65), con
     `TextosBoleta.Fletero(f)` = "NOMBRE — Cel. NNN" (o vacío si no hay fletero).
   - Recuadro central del bloque inferior: pasa a ser la firma del fletero (`AgregarFirma` con cargo "Fletero",
     y el nombre del fletero sobre la línea si existe). Se eliminan la lista con casillas, "Otros" y `SinFletero`.
   - Ajustar alturas de caja para que ningún texto quede cortado (alto de caja ≥ ~1,25 × tamaño en pt × 0,3528 mm).
3. Ticket 80 mm: línea "Fletero: …" en la zona de datos y firma "Fletero" junto a las otras firmas; sin lista.
4. Volver a renderizar los PNG y verificar visualmente; agregar tests en `DisenoBoletaTests` que comprueben que todos
   los elementos quedan dentro de la página y que ningún `TextoBoleta` de una sola línea se solapa con otro
   (intersección de rectángulos) y que el fletero aparece en la zona de datos.
5. Copiar los PNG finales a la carpeta de scratchpad / `TestResults` (no al repositorio) e indicar la ruta en el informe.

### T8 — Logo y nombre de empresa en el login (O7, D7)

- `IServicioParametros.ObtenerIdentidadEmpresaAsync()` (nuevo, **sin** `ExigirSesion`): devuelve solo
  `NombreEmpresa` y la ruta completa del logo (datos no sensibles). Documentar por qué no exige sesión.
- Extraer la carga del logo de `FrmPrincipal` (`FileStream` → `Image.FromStream` → `new Bitmap`, tolerante a errores)
  a un helper compartido en `WinForms/Infraestructura` y usarlo en `FrmPrincipal` (trabajo sin commit) y `FrmLogin`.
- `FrmLogin`: encabezado arriba de los campos con el logo (≈ 72×72 a 96 DPI, `Zoom`) y el nombre de la empresa
  (fuente grande, `AutoSize`); si no hay logo, solo el nombre; si el nombre es el valor sin configurar
  (`ReglasPuestaEnMarcha`, "NOMBRE DE EMPRESA") o vacío, mostrar "Sistema de Pesaje para Faeneo". El formulario debe
  crecer (`AutoSize` o alto recalculado) sin recortar nada a 125 %. Carga asíncrona sin bloquear el login; un error
  solo se registra en log.
- `FrmLogin` recibe `IServicioParametros` por constructor (lo crea `IFormFactory`; revisar `ContextoAplicacion`).
- Tests: con logo, sin logo, logo dañado, error del servicio (login sigue usable), layout a 96 DPI y 125 %.

### T9 — Reporte por raza (O8, D8)

- `TipoReporte.PorRaza = 6` y `FiltroReporte.ColorRaza` (string?).
- `IServicioReportes`: lista de valores distintos de `ColorRaza` (vigentes y anulados, normalizados con
  `Trim` + mayúsculas, sin vacíos) para el combo, igual que las listas de ganaderos/clientes (D17 de F7).
- `ServicioReportes`: filtro por rango de fechas + raza (comparación normalizada; considerar `TextoBusqueda` para
  acentos). Solo vigentes (D2), mismas reglas de totales (D3), simuladas con `*` (D4), límites D7.
- `TextosReporte`: título "Pesajes por raza: {RAZA}", nombre de archivo sugerido.
- `FrmReportes`: opción "Por raza" en el tipo, combo de razas (habilitado solo en ese tipo), invalidación del reporte
  al cambiarlo (D15), validación "seleccione una raza". Menú de reportes con el nuevo ítem si los otros tipos tienen uno.
- PDF/Excel reutilizan `ColumnasReporte`; verificar encabezados.
- Tests: servicio (filtra por raza, normaliza mayúsculas/espacios, excluye anuladas, totales), lista de razas,
  `FrmReportes` (combo habilitado solo en Por raza, invalidación, validación), texto/título.

### T10 — Documentación

- `docs/manual/` (manual de usuario): renombres (Código, Borrar pesaje), historial, fletero al pesar, reporte por raza,
  numeración automática, login. Checklist UAT (`docs/puesta-en-marcha/`): ajustar los casos afectados y agregar
  casos para O1–O9.
- `CHANGELOG.md`: sección nueva con estos cambios.
- `Agents.md`: entrada en la bitácora §5 "Observaciones del cliente (F10)" con decisiones D1–D9, migración, conteo de
  tests real medido y desviaciones; actualizar en §4 las viñetas que quedan desactualizadas (Marca → Código, F1..F4,
  D7 de F2, numeración en Parámetros) y la tabla de fases si corresponde.

### T11 — Verificación final

- `dotnet build src/PesajeFaeneo.sln -c Release` → 0 warnings, 0 errores.
- `dotnet test` completo, en verde, dos corridas seguidas.
- `dotnet ef migrations has-pending-model-changes` limpio (comando usado en fases previas; ver README/Agents.md).
- Test de migración desde una BD con datos de la versión anterior (ganaderos alfanuméricos, fleteros 1..4 con
  inactivos duplicados, pesajes) → datos conservados.
- Archivos nuevos/modificados: UTF-8 **sin BOM** salvo los que ya lo tenían, CRLF (`.editorconfig`); `.ps1`/`.iss`
  siguen ASCII puro.
- Sin `Thread.Sleep` en tests (compuerta E.4), sin `.Result`/`.Wait()` en la UI.

---

## 2. Riesgos y notas

- **R1 (D1):** si el cliente arranca sin importar el histórico y ya emitió la primera boleta, no podrá continuar la
  numeración del talonario legado. Mitigación: fijarla antes de la primera boleta (asistente/Parámetros) o importar.
- **R2 (D4/D6):** los códigos de ganadero legados alfanuméricos se conservan; los nuevos son numéricos. Ambos conviven
  en el mismo índice único.
- **R3 (D6):** el código automático no se reutiliza (incluye inactivos), igual que el número de boleta.
- **R4 (D9):** el diseño final de la boleta debe validarse con el cliente con una impresión física (R29 sigue abierta).
- **R5:** la auditoría histórica conserva claves `Marca`/`Orden` en sus JSON; no se migran (son registros inmutables).
