# F15 — Cierre de brechas frente a la frase comercial (1.4.0)

Plan para que **Capataz** cumpla, de forma verificable y veraz, la frase comercial que el cliente usa en su propuesta.
Está escrito para que otro agente lo ejecute sin ambigüedad: cada tarea dice qué archivos toca, qué cambia, qué
pruebas agrega y cómo se acepta. Antes de ejecutar, lea `AGENTS.md` (§4 convenciones, §5 bitácora), este plan
completo y la sección 2 ("Decisiones que el usuario debe tomar"): **las tareas P1 y P2 no se empiezan sin las
respuestas del usuario a U1–U8**; las P0 sí (sus decisiones tienen una sola opción razonable o ya están tomadas).

Rama: la actual (`feat/f9-empaquetado`) o la que indique el usuario. Versión de partida: **1.3.1** más la entrada
"[Sin publicar] - Logo del producto" de `CHANGELOG.md`. Hay migraciones (P1 y P2): la versión pasa a **1.4.0** (MENOR).
Si el usuario decide entregar solo P0 primero, esa entrega es **1.3.2** si no lleva migración (P0 no la necesita) y
F15-P1/P2 sale después como 1.4.0.

---

## 0. La frase comercial y su estado real

Frase del cliente (resumida por partes). La columna "Estado" refleja el código al 2026-11-18; "Tarea" es lo que la cierra.

| # | Promesa | Estado verificado | Evidencia | Tarea |
|---|---|---|---|---|
| C1 | Registro de ganadero, cliente y fletero | Cumple | Catálogos F2/F10 | — |
| C2 | Captura **automática** del peso, media res o res completa | Parcial: el peso viene de la balanza, pero lo captura el operador con F5/F6 sobre una lectura estable | `FrmPesaje.cs:836-851`, `ControlCapturas.cs:47-112` | T6 |
| C3 | Boleta por pesaje con el logo de la empresa como **marca de agua** | Parcial: solo media carta | `DisenoBoleta.cs:43-48` (media carta) y `:286-408` (ticket sin `FondoLogoBoleta`) | T1 |
| C4 | Reportes: pesada del día, faeneo entre fechas, por ganadero, de clientes, por razas, **por boletas anuales**; PDF y Excel | Parcial: no hay reporte anual; entre fechas/ganadero/cliente solo por el combo | `ReporteDtos.cs:6-14`, `FrmPrincipal.cs:434-454` | T2 |
| C5 | Usuarios Operador / Supervisor / Administrador | Cumple | F1 | — |
| C6 | Modificación de boleta **indicando el motivo**, cada cambio en auditoría | Parcial: se audita, pero el motivo es **opcional** | `FrmEditarBoleta.cs:224-226,399`, `ValidadorGestionPesajes.cs:66-69`, `ServicioGestionPesajes.cs:335-339` | T3 |
| C7 | Peso tomado directo del equipo para **evitar manipulación** | Parcial: la UI no deja teclear pesos (`FrmPesaje.cs:504-526`, `ReadOnly`), pero el servicio graba los pesos que le manda la UI sin contrastarlos y la base SQLite es editable con herramientas externas | `ServicioPesajes.cs:99-221` (usa `dto.PesoIzquierdo/Derecho` tal cual, `:181-183`) | T5, T7, T8 |
| C8 | Compatible y escalable: indicadores por **serial, USB o Ethernet** | Parcial: serie, USB-serie (puerto COM virtual) y TCP cliente; solo transmisión continua; kg y g | `Enums.cs:10-15,60-71`, `LectorTcp.cs`, sin escritura al puerto (`LectorBalanzaBase.cs`) | T10, T11 |
| C9 | Copias automáticas | Cumple | F8 (`ProgramadorBackupAutomatico`) | — |
| C10 | Historial de pesaje | Cumple con matiz: un día a la vez (con selector de día) | `FrmHistorialPesajes.cs:32,277-280`, `IServicioPesajes.cs:11-12` | T9 |
| C11 | Impresión en media carta o ticket 80 mm | Cumple; falta la prueba física con papel personalizado | `ConfiguracionImpresionDtos.cs:3-7` | T12 (UAT) |

---

## 1. Hallazgos verificados (incluye los que corrigen la revisión original)

| # | Hallazgo | Evidencia | Consecuencia para el plan |
|---|---|---|---|
| H1 | La marca de agua es un perfil fijo de código: `LogoComoFondo = true`, `OpacidadFondoLogo = 0.13f`; solo `CrearMediaCarta` agrega `FondoLogoBoleta(48, 37, 120, 92, 0.13)`. | `DisenoBoleta.cs:11-15,45-48` | T1 la extiende al ticket y la hace configurable por equipo. |
| H2 | Un test **fija** que el ticket no lleva fondo. | `DisenoBoletaTests.cs:149` | T1 debe reescribir ese test (no borrarlo). |
| H3 | El pintor aplica la opacidad con `ColorMatrix.Matrix33` sobre el logo en color. En una térmica (1 bit) el driver lo trama: con 13 % puede no imprimirse nada o salir un punteado irregular. | `PintorBoleta.cs:118-142` | T1 agrega conversión a gris y un nivel propio para el ticket. |
| H4 | Vista previa, impresión y reimpresión comparten `DisenoBoleta.Crear` vía `ImpresorBoletasGdi` (único llamador). | `ImpresorBoletasGdi.cs:116-118,145-147` | Un solo punto de cambio para T1. |
| H5 | `impresion.json` exige `Version == 1` y se descarta entero si no valida. | `AlmacenConfiguracionImpresionJson.cs:66-92` | Los campos nuevos de T1 deben ser **opcionales con valor por defecto**, manteniendo `Version = 1`. |
| H6 | No existe reporte anual; `TipoReporte` tiene 6 valores. Límites: 20 000 consultables, 1 000 en grilla, 2 000 para imprimir/vista previa. | `ReporteDtos.cs:6-14`, `ReglasReportes.cs:17-19` | Un año de un matadero mediano (≈ 100 boletas/día) supera 20 000: el anual **de detalle** fallaría. T2 hace un **resumen mensual**. |
| H7 | El menú Reportes solo tiene "Reportes de pesaje" (abre en del día), "Pesajes por raza" y "Boletas anuladas". | `FrmPrincipal.cs:434-454` | T2 agrega entradas directas. |
| H8 | El motivo de edición es opcional en UI ("Opcional. Breve explicación…"), validador (solo máximo 100) y servicio (lo agrega solo si viene). La auditoría ya guarda el texto en `Auditoria.Motivo` (máx. 250) y el visor lo muestra en "Motivo / Detalle". | `FrmEditarBoleta.cs:224-226,399`; `ValidadorGestionPesajes.cs:66-69`; `ServicioGestionPesajes.cs:335-349`; `AuditoriaConfiguration.cs:33-34`; `FrmVisorAuditoria.cs:327,421` | T3 solo vuelve obligatorio el motivo; **no hace falta** campo nuevo en el snapshot ni cambio del visor (D3.3). |
| H9 | `ControlCapturas` (reglas antifraude de captura: estabilidad, rango, retorno a cero, simulador) vive en **WinForms**, no en Application. | `WinForms/Presentacion/Pesaje/ControlCapturas.cs:20-128` | La verificación en el servicio (T5) no puede "llamar a ControlCapturas"; necesita un registro de capturas en Application. |
| H10 | El servicio **no puede** contrastar el peso con la instantánea al grabar: entre la captura y F12 la media res ya se retiró del gancho (la balanza vuelve a ~0). | Flujo de `FrmPesaje` + `ReglasPesaje.ExigirRetornoACero` | Se descarta "contrastar con la instantánea al grabar"; T5 usa **recibos de captura**. |
| H11 | `ServicioBalanza` (Application) publica cada lectura en `Publicar(...)`. | `ServicioBalanza.cs:399,557` | Punto natural para alimentar el registro de capturas (T5). |
| H12 | `RegistrarAsync` se llama desde `FrmPesaje.cs:1012` y desde `SembradorDemo.cs:232,279` (build de capturas, `#if CAPTURAS_MANUAL`), y en 30 lugares de tests (6 archivos, incl. `Utilidades/Pesaje/ServicioPesajesFalso.cs`). | grep | T5 debe adaptar el sembrador y los tests. |
| H13 | La configuración de balanza por defecto es **Simulador**. | `Entities.cs:43` | Bloquear el simulador "siempre" impediría probar la instalación nueva: U5. |
| H14 | **Corrige la revisión:** las altas rápidas del Operador **ya se auditan** ("Alta rápida desde pesaje") y tienen test. | `ServicioGanaderos.cs:122-133`, `ServicioClientes.cs:124`, `ServicioGanaderosTests.cs:223`, `ServicioClientesTests.cs:195` | Sin tarea; solo se menciona en manuales/frase. |
| H15 | **Matiza la revisión:** el historial no es "solo de hoy": tiene selector de día (`dtpDia`), pero muestra un día por vez (tope 500). El buscador por rango existe pero exige `EditarBoleta` (el Operador no lo tiene). | `FrmHistorialPesajes.cs:32,277-280`; F6 D4 | T9 agrega rango corto al historial. |
| H16 | Toda escritura de `Pesaje` pasa por `PesajeDbContext.SaveChanges*` (que ya incrementa `Version`); no hay `ExecuteUpdate` sobre `Pesajes` (el único SQL crudo de importación toca la numeración). | `PesajeDbContext.cs:29-51`, `RepositorioImportacion.cs:114` | Un interceptor `SaveChanges` cubre altas, edición, anulación e importación (T7). |
| H17 | Los interceptores se registran en un único lugar compartido por DI, diseño y fixtures. | `SqliteOpcionesExtensiones.cs`, `SqlitePragmasInterceptor.cs` | T7 registra ahí el sellador. |
| H18 | El lector no escribe al puerto (sin `WriteFile` ni método de escritura en `IPuertoSerie`). | `PuertoSerieWin32.cs`, `IPuertoSerie.cs` | El sondeo bajo demanda (T10) necesita `Escribir` en `IPuertoSerie` y en el lector TCP. |
| H19 | CHECK actuales: `UnidadPeso IN (1, 2)`, `TipoConexion IN (1, 2, 3)`, `DelimitadorTrama IN (1, 2, 3)`. | `ConfiguracionBalanzaConfiguration.cs:16,28-29` | Libras / UDP exigen migración que amplíe los CHECK (T10/T11). |

---

## 2. Decisiones que el usuario debe tomar (bloquean P1 y P2)

Cada una lleva la recomendación del arquitecto (marcada **Rec.**). El agente ejecutor **pregunta** estas decisiones
al usuario antes de T4; si el usuario dice "lo que recomiendes", aplica la recomendación y lo anota en la bitácora.

| # | Decisión | Opciones | Rec. |
|---|---|---|---|
| U1 | **Alcance de la captura automática** | (a) No hacerla; corregir la frase a "captura desde el indicador con una tecla". (b) Captura automática **opcional** (parámetro del sistema, apagada por defecto): al estabilizarse y estar en rango, el sistema captura solo; el grabado sigue siendo F12 manual. (c) Como (b) más grabado automático al completar. | **(b)**. (c) quita la última revisión humana (ganadero/cliente/secuencia mal elegidos se graban igual) y multiplica boletas a anular. |
| U2 | **Verificación del peso en el servicio** | (a) Nada (confiar en la UI). (b) **Recibos de captura** emitidos por un registro en Application que observa la balanza; el servicio exige recibo vigente, no usado y con el mismo peso. (c) (b) + exigir que el recibo sea de los últimos N minutos. | **(c)** con N = 10 min (es (b) con vencimiento; costo igual). |
| U3 | **Integridad de la base** | (a) Solo auditoría (hoy). (b) **Sello HMAC por fila de `Pesaje`** + herramienta "Verificar integridad" (sellos y huecos de numeración) + aviso en boleta/reportes. (c) (b) + cadena de sellos (cada fila incluye el sello anterior) y sello de la tabla `Auditorias`. | **(b)** como nivel mínimo; (c) como opcional futuro. Ver límites honestos en D7.6. |
| U4 | **Reportes anuales: qué tipos** | (a) Solo botón "Año completo" que fija Desde/Hasta en los reportes de detalle (falla con > 20 000 boletas). (b) (a) + **Resumen anual por mes** (12 filas: vigentes, reses, kg; anuladas y kg anulados). (c) (b) + resumen anual por ganadero y por cliente (una fila por ganadero/cliente). | **(b)**; (c) si el cliente lo pide tras ver (b) (queda como T2-opcional). |
| U5 | **Simulador en producción** | (a) Dejar como está (solo Administrador; boleta marcada SIMULADO). (b) Parámetro "Permitir grabar pesajes simulados" (auditado); **apagado** por defecto en instalaciones nuevas al terminar la puesta en marcha, encendido en actualizaciones para no cambiar el comportamiento sin aviso; aviso fuerte en pantalla y motivo de auditoría explícito. (c) Bloquear siempre el grabado simulado fuera del build de capturas. | **(b)**. (c) impide probar una instalación nueva (la balanza nace como Simulador, H13). |
| U6 | **Editar pesos de una boleta** | (a) No (anular y volver a pesar). (b) Sí, con motivo y doble autorización. | **(a)**. Corregir un peso sin la balanza contradice C7; la salida auditada es anular (motivo obligatorio) y pesar de nuevo. Se documenta. |
| U7 | **Tope del PDF de detalle** (20 000 filas ≈ 2,9 GB de memoria, riesgo F7-Abierto) | (a) Mantener 20 000. (b) Tope de **5 000** filas para PDF (Excel sigue en 20 000) con mensaje que sugiere Excel o el resumen anual. (c) Dividir el PDF en varios archivos. | **(b)**: cambio pequeño y elimina el riesgo de `OutOfMemoryException`; (c) es más complejo y nadie lo pidió. |
| U8 | **Ampliación de indicadores (P2)** | (a) Nada ahora. (b) Libras + comando de solicitud (sondeo) por serie y TCP. (c) (b) + UDP. | **(b)**, solo si hay un indicador real que lo necesite para validar; UDP (c) únicamente con un equipo concreto en mano. |

Además, el usuario decide la **entrega**: P0 solo (1.3.2, sin migración) o P0+P1(+P2) juntos (1.4.0). **Rec.:** P0+P1
juntos en 1.4.0 (una sola actualización con resguardo `PreMigracion`); P2 cuando exista el hardware.

---

## 3. Decisiones de diseño (asumen las recomendaciones de §2; si el usuario elige otra opción, ajuste la tarea)

### D1 — Marca de agua en ticket 80 mm (T1)
1. La marca de agua deja de ser constante de código y pasa a la **configuración de impresión por equipo** (depende de la
   impresora): `ConfiguracionImpresion` gana `MarcaAguaLogo` (`bool`, por defecto `true`) e `IntensidadMarcaAgua`
   (`enum IntensidadMarcaAgua { Suave = 1, Media = 2, Fuerte = 3 }`, por defecto `Media`). Se agregan como parámetros
   **opcionales al final** del record para no romper llamadores; en `impresion.json` son opcionales (si faltan se usan los
   valores por defecto) y `Version` sigue en 1 (H5).
2. Opacidades (constantes en `ReglasImpresion`): media carta Suave 0,10 / **Media 0,13** (el valor actual) / Fuerte 0,18;
   ticket Suave 0,15 / **Media 0,22** / Fuerte 0,30 (el ticket es térmico: con menos de ~15 % el tramado del driver lo
   pierde). `DisenoBoleta.LogoComoFondo` y `OpacidadFondoLogo` se reemplazan por `ReglasImpresion.OpacidadMarcaAgua(formato, intensidad)`.
3. `DisenoBoleta.Crear` recibe un parámetro opcional `OpcionesMarcaAgua? marcaAgua = null` (null = por defecto: activada,
   Media). Firma nueva: `Crear(DatosBoleta datos, FormatoBoleta formato, int numeroCopia, int totalCopias, OpcionesMarcaAgua? marcaAgua = null)`;
   `OpcionesMarcaAgua(bool Activa, IntensidadMarcaAgua Intensidad)` en `Boletas/Diseno/ElementosBoleta.cs`.
4. Ticket: el fondo se agrega **al final del armado** con `elementos.Insert(0, …)` (debe quedar primero para pintarse
   detrás). Caja: X = 12, ancho = 56 mm, centrada verticalmente entre el primer separador posterior al encabezado y la
   primera línea de firma; alto = `min(56, alto disponible)`. No cubre el logo del encabezado ni las firmas.
5. Pintor: `PintarFondoLogo` convierte el logo a **escala de grises** (matriz de luminancia 0,299/0,587/0,114) además de
   aplicar la opacidad, en ambos formatos. En media carta cambia de color a gris: aceptable y más predecible en impresoras
   monocromáticas; se anota en el CHANGELOG.
6. `FrmConfiguracionImpresion`: casilla "Logo de la empresa como marca de agua" y combo "Intensidad" (habilitado si la
   casilla está marcada); la página de calibración no lleva marca de agua. La auditoría de configuración de impresión
   (`ConfiguracionImpresionAuditoria`) incluye los dos campos.
7. Sin logo cargado o con logo ilegible: no se dibuja fondo (como hoy).

### D2 — Reportes anuales y menús (T2)
1. `TipoReporte.ResumenAnual = 7`. Usa solo el **año** (`FiltroReporte.Desde.Year`); el servicio fija el rango
   1-ene a 31-dic local e ignora `Hasta`. Valida año entre 2000 y año actual + 1 (`CamposReportes.FechaDesde`,
   "Seleccione un año entre 2000 y {n}.").
2. Lectura liviana: `IPesajeRepositorio.ListarParaResumenAsync(DateTime desdeUtc, DateTime hastaUtc, int maximo, CancellationToken)`
   devuelve `IReadOnlyList<PesajeParaResumen>` (record en `Abstracciones/Persistencia/CriteriosPersistencia.cs`:
   `FechaHora, Estado, TipoDesposte, PesoIzquierdo, PesoDerecho, PesoTotal, OrigenPeso`), `AsNoTracking`, proyección sin
   navegaciones, vigentes **y** anuladas, tope `ReglasReportes.MaximoFilasResumen = 500_000` (memoria ≈ 40 MB). Se respeta
   D3 de F7: **sin agregados SQL**, sumas en memoria con `decimal`.
3. `CalculadoraResumenAnual` (pura, `Application/Reportes`): agrupa por mes **local** (`TimeZoneInfo` del `TimeProvider`),
   12 filas siempre (meses sin datos en cero) + totales; reses con `ReglasReportes.Reses` (media res = 0,5); vigentes y
   anuladas separadas (D2 de F7): columnas `Mes, Boletas, ResCompleta, MediaRes, Reses, KgIzquierdo, KgDerecho, KgNeto,
   Simuladas, Anuladas, KgNetoAnulado`.
4. DTO: `FilaResumenMensual` y una propiedad opcional nueva en `ReportePesajes`:
   `IReadOnlyList<FilaResumenMensual>? ResumenMensual = null` (al final, con valor por defecto) y
   `public bool EsResumenAnual => Tipo == TipoReporte.ResumenAnual;`. En el resumen `Filas` va vacía y `Totales` se arma
   con la suma de los meses (vigentes).
5. Columnas declarativas: `ColumnasReporte.ResumenAnual(bool mostrarDesposte)` (sin "Res completa"/"Media res" cuando
   `MostrarDesposte = false`, como F11 D9). PDF (`DisenoPdfReporte`) y Excel (`GeneradorExcelReportes`, lista de
   `ColumnaExcel`) ramifican por `EsResumenAnual`; título "Resumen anual {año}", subtítulo "Año {año} — boletas vigentes
   por mes; anuladas en columnas aparte". Nombre de archivo `Resumen_anual_{año}.pdf/.xlsx`.
6. `FrmReportes`: el combo suma "Resumen anual"; con ese tipo se muestra `nudAnio` (NumericUpDown 2000..año+1, por
   defecto el año actual) y se deshabilitan Desde/Hasta/ganadero/cliente/raza. Para los tipos de detalle (excepto "del día")
   aparece el botón **"Año completo"** que fija Desde = 1-ene y Hasta = 31-dic del año de `dtpDesde` (invalida el reporte
   como cualquier cambio de filtro). Si el detalle supera 20 000 boletas, el mensaje existente se amplía: "…Reduzca el rango
   de fechas o use el Resumen anual." (`TextosReporte.MaximoSuperado`). `ActualizarAcciones` considera "hay datos" cuando
   `Filas.Count > 0` **o** algún mes tiene boletas o anuladas.
7. Menú **Reportes** (todas con `Permiso.VerReportes`, en este orden): "Pesajes del &día" (antes "Reportes de pesaje"),
   "Pesajes &entre fechas", "Pesajes por &ganadero", "Pesajes por &cliente", "Pesajes por &raza", "Resumen &anual",
   separador, "Boletas a&nuladas". Cada uno llama `AbrirReportes(tipo)`. Revise que las letras `&` no se repitan.
8. (U7 = b) `ReglasReportes.MaximoFilasPdf = 5_000`: `btnPdf` se deshabilita por encima con aviso naranja "El PDF admite
   hasta 5.000 boletas; use Excel o el Resumen anual."; el servicio de reportes no cambia (el tope es de exportación).
9. (U4 = c, opcional) `ResumenAnualPorGanadero`/`PorCliente`: mismo patrón con agrupación por clave; **no** se hace salvo
   pedido explícito.

### D3 — Motivo obligatorio al editar (T3)
1. Reglas: `ReglasGestionPesajes.MotivoEdicionMinimo = 5` (nuevo), `MotivoEdicionMaximo = 100` (existe). Mismas reglas y
   normalización que la anulación.
2. `ValidarEdicion`: "Ingrese el motivo del cambio (mínimo 5 caracteres)." / "El motivo no puede superar 100 caracteres.".
   El motivo es obligatorio **cada vez que se guarda**, aunque luego resulte que no cambió nada (regla simple, coherente con
   la frase; el mensaje "No se modificó ningún dato." sigue existiendo y no audita).
3. Auditoría: se mantiene el formato `"Edición de boleta {n}: {campos} — {MOTIVO}"` en `Auditoria.Motivo` (cabe: ≤ ~210 de
   250). **No** se agrega campo al snapshot `PesajeAuditoria` ni cambia `DiferenciasAuditoria`: el visor ya muestra el
   motivo en su columna (H8). Se agrega un test que lo garantice.
4. UI (`FrmEditarBoleta`): etiqueta "Motivo del cambio (*):", pista "Obligatorio (5 a 100 caracteres). Queda en la
   auditoría.", y **validación antes de llamar al servicio** con `ValidadorGestionPesajes.ValidarEdicion` (patrón F12 E5):
   errores al `ErrorProvider`, foco en el campo, sin llamar al servicio. El servicio sigue validando (defensa en profundidad).
5. Pesos no editables (U6 = a): se documenta en manual del Supervisor y en la frase (§7).

### D4 — Parámetros nuevos del sistema (T4)
1. `ParametrosSistema`: `CapturaAutomatica` (`bool`, por defecto `false`), `PermitirPesajeSimulado` (`bool`),
   `SelloInicialAplicado` (`bool`, por defecto `false`, uso interno de T7), `IdInstalacion` (`string?`, GUID en minúsculas,
   uso interno de T7).
2. Migración `F15_IntegridadYCaptura` (una sola para P1): las cuatro columnas de `ParametrosSistema` + `Pesajes.Sello`
   (`TEXT NULL`, `HasMaxLength(64)`, CHECK `Sello IS NULL OR length(Sello) = 64`). Relleno en SQL: `PermitirPesajeSimulado = 1`
   en las filas existentes (actualización: mismo comportamiento que hoy), `IdInstalacion` lo genera el código en el primer
   arranque (T7) porque SQLite no genera GUID estándar. Instalación nueva: el asistente pone `PermitirPesajeSimulado = 0` al
   **Finalizar** si el administrador no lo marcó explícitamente (U5 = b).
3. Servicio: `IServicioParametros.ObtenerOpcionesPesajeAsync()` (solo sesión, lo usa el Operador) →
   `OpcionesPesaje(bool CapturaAutomatica, bool PermitirPesajeSimulado)`; `CambiarOpcionesPesajeAsync(OpcionesPesaje)`
   exige `GestionarParametros` y audita `CambioParametros` con `antes/despues` de esas dos claves (patrón de
   `CambiarModoDesposteAsync`, F11 D5); `DiferenciasAuditoria.EtiquetaCampo` las muestra como "Captura automática" y
   "Permitir pesajes simulados".
4. UI: `FrmOpcionesPesaje` en **Administración › Opciones de pesaje** (permiso `GestionarParametros`) con las dos casillas y
   una explicación de una línea cada una. No se agrega paso al asistente (se puede abrir desde Administración).

### D5 — Recibos de captura (T5)
1. `Application/Balanza/RegistroCapturas.cs`: `IRegistroCapturas` + `RegistroCapturas` (singleton, `lock` interno,
   `TimeProvider` inyectado). Lo alimenta `ServicioBalanza.Publicar` con cada instantánea (`Observar`), así no depende de
   `IServicioBalanza` (sin ciclo de DI).
2. `ResultadoRecibo Emitir(TipoDesposte tipo)`: usa **su última instantánea observada** (no una que pase la UI) y aplica las
   mismas reglas que `ControlCapturas.Evaluar`: capturable (`EsCapturable(ahora)`), peso de lado en rango, retorno a cero
   tras el recibo anterior (`ReglasPesaje.ExigirRetornoACero`). Devuelve `ReciboCaptura(Guid Id, decimal PesoKg,
   DateTimeOffset Instante, bool EsSimulador, long SecuenciaLectura)` o el motivo de rechazo. La regla de "lado no
   permitido en media res" y la de simulador por rol siguen en `ControlCapturas` (son de pantalla) **y** en el servicio
   (simulador, ya existe en `ServicioPesajes.cs:117-120`).
3. `Descartar(IEnumerable<Guid>)` (F8 "Borrar pesaje"): invalida esos recibos y, si incluye el último emitido, levanta la
   exigencia de retorno a cero (espejo de `ControlCapturas.BorrarPesaje`, `ControlCapturas.cs:114-120`).
4. `bool Reservar(Guid id, decimal pesoEsperado, out string codigoError)` / `Confirmar(Guid)` / `Liberar(Guid)`. Vida del
   recibo: `ReglasPesaje.VidaReciboCaptura = 10 min` (U2 = c). Recibos vencidos se purgan en cada `Emitir`.
5. `RegistrarPesaje` gana `Guid? ReciboIzquierdo = null, Guid? ReciboDerecho = null` (al final). `CapturadoConSimulador`
   queda **obsoleto** (se ignora; se marca `[Obsolete]` solo si no rompe `TreatWarningsAsErrors`; si rompe, se elimina y
   se ajustan llamadores). `ServicioPesajes.RegistrarAsync`, **antes de la transacción** (la reserva del número sigue
   siendo su primera sentencia): para cada lado con peso > 0 exige recibo; reserva; peso del recibo = peso del DTO
   (redondeado a 0,1); `origen = Simulador` si algún recibo `EsSimulador` o la instantánea actual lo es. Errores:
   `PES_CAPTURA_REQUERIDA` "Capture el peso desde la balanza.", `PES_CAPTURA_VENCIDA` "La captura venció (más de 10
   minutos). Borre el pesaje (F8) y capture de nuevo.", `PES_PESO_NO_CAPTURADO` "El peso no coincide con la lectura de la
   balanza. Borre el pesaje (F8) y capture de nuevo.". `Confirmar` tras `ConfirmarAsync`; `Liberar` en `finally` si falló.
6. `ControlCapturas.Capturar` recibe el recibo emitido por el registro (la pantalla llama `Emitir` y, si hay recibo, lo
   guarda junto al peso); **las reglas de `ControlCapturas` no cambian** (regla antifraude intacta; el registro las
   duplica en Application).
7. Importación legada (`OrigenPeso.Importado`) no usa `RegistrarAsync`: no se afecta. `SembradorDemo` (`#if CAPTURAS_MANUAL`)
   obtiene recibos alimentando el registro con instantáneas sintéticas (método `#if CAPTURAS_MANUAL` `ObservarParaDemo`).
8. Límite honesto: es una verificación dentro del mismo proceso. Impide que un error o una pantalla futura graben pesos no
   leídos; no detiene a quien modifique el ejecutable o la memoria del proceso.

### D6 — Captura automática (T6)
1. Solo con `CapturaAutomatica = true`. Clase pura `Presentacion/Pesaje/CapturaAutomatica.cs` que, con cada
   `AlActualizarBalanza`, decide si dispara la captura: ganadero **y** cliente elegidos, sin fletero pendiente
   (`FleteroSinElegir = false`), no ocupado, `ControlCapturas.Evaluar(lado) == Ninguno`, y la lectura lleva **estable al
   menos `ReglasPesaje.PermanenciaCapturaAutomaticaMs = 1500`** (se mide con `InstanteLectura`/`Secuencia` sucesivas; se
   reinicia al perder estabilidad o si el peso varía más que la tolerancia).
2. Lado: res completa → izquierdo y luego derecho (con retorno a cero entre ambos, ya exigido). Media res → el lado que
   indica un selector "Lado de la media res" (Izquierdo/Derecho, visible solo en modo automático, recordado en la sesión
   del formulario). F5/F6 manuales siguen funcionando.
3. Doble captura: el retorno a cero obligatorio (`EsperandoRetornoACero`) y el recibo (D5) impiden capturar dos veces la
   misma media res; además, tras una captura automática se ignoran disparos durante 1 s.
4. Grabado: **manual** (F12) siempre (U1 = b). Al completar los pesos la guía dice "Pesos capturados automáticamente.
   Revise los datos y grabe (F12)." y suena un aviso corto.
5. Indicador visible en la pantalla: "CAPTURA AUTOMÁTICA" junto al estado de la balanza. Con simulador y rol sin
   `ConfigurarBalanza` nunca dispara (lo impide `Evaluar`).

### D7 — Sello de integridad por fila (T7)
1. `Application/Integridad/SelloPesaje.cs` (puro, como `FirmaLicencia`): cadena canónica invariante de
   `NumeroBoleta|FechaHora(ticks UTC)|GanaderoId|ClienteId|FleteroId|Direccion|Secuencia|ColorRaza|Sexo|TipoDesposte|
   PesoIzquierdo(F1)|PesoDerecho(F1)|PesoTotal(F1)|OrigenPeso|Observacion|Estado|MotivoAnulacion|AnuladoPorId|
   AnuladoEn(ticks)|UsuarioId|Version|IdInstalacion` → `HMACSHA256` → hex en minúsculas (64). No incluye `Id` (no se
   conoce antes del INSERT) ni `ModificadoEn`/`CreadoEn`. Pesos con `F1` invariante tras el redondeo de `DecimalARealConverter`.
2. Clave: `IClaveIntegridad` (Application) implementada en `Infrastructure/Integridad/ClaveIntegridad.cs` con una constante
   de 32 bytes del código combinada con `IdInstalacion` (vía HKDF/HMAC). Viaja con la base en los backups → una base
   restaurada en otra PC sigue verificando.
3. `Infrastructure/Persistencia/SelladorPesajesInterceptor.cs` (`SaveChangesInterceptor`, `SavingChanges[Async]`):
   para cada `Pesaje` `Added`/`Modified` calcula `Sello` **después** de `IncrementarVersionPesajes` (el override corre antes
   que los interceptores). Registro en `SqliteOpcionesExtensiones.ConfigurarSqlite` (H17). Necesita `IdInstalacion`: lo lee
   una vez por contexto de `ParametrosSistema` (cacheado en el interceptor por cadena de conexión).
4. Sellado inicial: en `ActualizadorBaseDatos` (después de migrar y sembrar) si `SelloInicialAplicado = false`: genera
   `IdInstalacion` si falta, sella todas las filas en lotes de 1 000 y pone `SelloInicialAplicado = true` en la **misma**
   transacción; audita `ActualizacionSistema` "Sellado inicial de N boletas". Nunca vuelve a sellar filas existentes (si no,
   "curaría" una alteración).
5. Verificación: `IServicioIntegridad` (Application, permiso `VerAuditoria`): `VerificarAsync()` → filas con sello
   inválido o nulo y **huecos** en `NumeroBoleta` (entre el mínimo y `SiguienteNumeroBoleta - 1`, descontando los rangos
   importados si los hay). Pantalla **Utilitarios › Verificar integridad** (`FrmVerificarIntegridad`, solo lectura, exporta
   CSV con el patrón del informe de importación). Además: `ServicioBoletas` marca `DatosBoleta.IntegridadFallida` → leyenda
   "REGISTRO ALTERADO — VERIFIQUE" (se suma a `TextosBoleta.Leyendas`, nunca reemplaza otras); `ServicioReportes` cuenta
   filas alteradas → nota al pie "(!) N boleta(s) con verificación de integridad fallida." y marca "!" junto al número;
   la edición y la anulación de una fila alterada se rechazan (`PES_INTEGRIDAD`) para que nadie "regularice" con un sello
   nuevo. Cada detección se registra en el log (Warning) una vez por boleta y arranque.
6. **Límites honestos** (van al manual técnico y a la guía de soporte): detecta ediciones con herramientas externas
   (DB Browser, scripts) hechas por quien no tiene el código; **no** resiste a quien descompile el ejecutable (.NET se
   descompila con facilidad; la clave está en el binario, igual que la sal de licencias, F13 D12), ni el borrado de la base
   completa o su reemplazo por un backup anterior. La auditoría también es editable (U3 = c lo mitigaría).
7. Costo: ~1 HMAC por escritura (microsegundos), una columna de 64 caracteres por fila (≈ 1,3 MB cada 20 000 boletas), una
   verificación por lectura en boleta/reporte (≈ 20 ms cada 20 000 filas).

### D8 — Simulador endurecido (T8)
1. Con `PermitirPesajeSimulado = false`: `ServicioPesajes.RegistrarAsync` rechaza `PES_SIMULADOR_DESHABILITADO` "Los pesajes
   simulados están deshabilitados (Administración › Opciones de pesaje)." para **cualquier** rol; `FrmPesaje` deshabilita
   F5/F6 con simulador y la guía lo explica.
2. Con `true`: al abrir `FrmPesaje` con la balanza en simulador se muestra **una vez** una confirmación roja "La balanza
   está en modo SIMULADOR: los pesos no son reales y la boleta saldrá marcada SIN VALOR COMERCIAL. ¿Continuar?" (No →
   cierra la pantalla). El motivo de auditoría del alta pasa a `"Alta de pesaje boleta {n} (PESO SIMULADO)"`.
3. Sin cambios en la leyenda/marca de agua SIMULADO de la boleta ni en el asterisco de reportes.

### D9 — Historial por rango (T9)
`IServicioPesajes.ListarPorRangoAsync(DateOnly desde, DateOnly hasta)` (permiso `Pesar`, máximo 31 días,
`ReglasPesaje.HistorialDiasMaximo = 31`, tope 500 como hoy). `FrmHistorialPesajes`: "Desde"/"Hasta" en lugar de "Día"
(por defecto hoy–hoy). `ListarPorDiaAsync` se conserva (la usa el inicio de la pantalla de pesaje si aplica) delegando en
la nueva.

### D10 — Indicadores (T10, T11; P2)
1. `UnidadPesoTrama.Libras = 3` (factor exacto 0,45359237, luego el redondeo a 0,1 kg de siempre).
2. Sondeo bajo demanda (R22): `ConfiguracionBalanza.ComandoSolicitud` (`string?`, máx. 20, admite escapes `\r`, `\n`,
   `\x05` para ENQ) e `IntervaloSondeoMs` (`int`, 200..5 000, por defecto 500). Vacío = transmisión continua (hoy).
   `IPuertoSerie.Escribir(ReadOnlySpan<byte>)` (`WriteFile` en `PuertoSerieWin32`) y escritura en `LectorTcp`;
   `LectorBalanzaBase` envía el comando con un `PeriodicTimer` mientras está conectado. `TimeoutMs` debe ser > intervalo
   (validación).
3. Migración `F15_ProtocolosBalanza`: `UnidadPeso IN (1, 2, 3)`, columnas nuevas con CHECK, filas existentes sin comando.
4. UDP (T11, solo con U8 = c): `TipoConexion.Udp = 4`, `LectorUdp` (`UdpClient` escuchando en `PuertoTcp`), CHECK ampliado
   en la misma migración. Sin hardware concreto **no se implementa**.
5. Todo P2 requiere validación con hardware real (pesa patrón) antes de declararlo compatible.

---

## 4. Tareas

Convenciones para todas: `.cs`/`.md` en UTF-8 sin BOM y CRLF; `.ps1`/`.iss`/`.txt` de `scripts/` e `instalador/` en ASCII
puro; xUnit solo `Assert`; `TimeProvider` inyectado (`FakeTimeProvider` en tests); sin renombrar namespaces; marca
"Capataz"; `ArquitecturaTests` en verde (QuestPDF/ClosedXML solo en `Infrastructure.Reportes`; Application sin WinForms
ni EF). Tras cada tarea: `dotnet build src/PesajeFaeneo.sln -c Release` sin advertencias y la suite completa en verde.

### P0 — Cumplimiento directo de la frase (sin migración)

#### T1 — Marca de agua del logo en ticket 80 mm y configurable (D1) · Esfuerzo **M**
- **Archivos:** `Application/Boletas/Diseno/DisenoBoleta.cs`, `Application/Boletas/Diseno/ElementosBoleta.cs`,
  `Application/Boletas/ReglasImpresion.cs`, `Application/Boletas/Dtos/ConfiguracionImpresionDtos.cs`,
  `Application/Boletas/ValidadorConfiguracionImpresion.cs`, `Application/Auditoria/FabricaAuditoria.cs`
  (`ConfiguracionImpresionAuditoria`), `Infrastructure/Archivos/AlmacenConfiguracionImpresionJson.cs` (campos opcionales en
  `ArchivoConfiguracion`), `WinForms/Impresion/PintorBoleta.cs`, `WinForms/Impresion/ImpresorBoletasGdi.cs` (pasa
  `OpcionesMarcaAgua` desde la configuración), `WinForms/Formularios/Administracion/FrmConfiguracionImpresion.cs`.
- **Cambios:** D1.1–D1.7.
- **Pruebas:** `DisenoBoletaTests`: reescribir `FondoLogo_SoloEnMediaCartaConLogo_…` (`:139-151`) como
  `FondoLogo_EnAmbosFormatosConLogo_PrimeroYDentroDeLaPagina` (teoría por formato); sin logo → sin fondo; `Activa = false`
  → sin fondo; opacidad por formato e intensidad; en el ticket la caja no se superpone al `LogoBoleta` del encabezado ni a
  las líneas de firma; `SinSolapes_…` (`:316`) y `Ticket80mm_SinDesposte_…` (`:456`) siguen verdes con fondo.
  `AlmacenConfiguracionImpresionJsonTests`: archivo v1 **sin** los campos nuevos → valores por defecto (no se descarta);
  ida y vuelta con los campos. `ValidadorConfiguracionImpresionTests`: intensidad fuera del enum → error.
  `ServicioConfiguracionImpresionTests`: el snapshot de auditoría cambia al tocar la marca de agua. Nuevo
  `MuestraBoletaPngTests` (WinForms, `[SupportedOSPlatform("windows")]`): renderiza media carta y ticket, con y sin logo, a
  `Bitmap` con `PintorBoleta` y los guarda en `Path.Combine(Path.GetTempPath(), "capataz-muestras-boleta")`; aserta que con
  logo hay píxeles no blancos y no negros en el centro de la caja del fondo y que sin logo esa zona (fuera de textos) es
  blanca.
- **Aceptación:** vista previa, impresión y reimpresión de un ticket muestran el logo atenuado detrás de datos y pesos;
  media carta igual que hoy (en gris); la casilla lo apaga en ambos formatos; un `impresion.json` de 1.3.x se lee sin
  perder la configuración; PNG de muestra revisados a ojo (ver §6).

#### T2 — Resumen anual, "Año completo" y menús directos (D2) · Esfuerzo **L**
- **Archivos:** `Domain/Reglas/ReglasReportes.cs` (`MaximoFilasResumen`, `MaximoFilasPdf` si U7 = b),
  `Application/Reportes/Dtos/ReporteDtos.cs`, `Application/Reportes/ServicioReportes.cs`,
  `Application/Reportes/CalculadoraResumenAnual.cs` (nuevo), `Application/Reportes/ColumnasReporte.cs`,
  `Application/Reportes/TextosReporte.cs`, `Application/Abstracciones/Persistencia/IPesajeRepositorio.cs`,
  `Application/Abstracciones/Persistencia/CriteriosPersistencia.cs`,
  `Infrastructure/Persistencia/Repositorios/PesajeRepositorio.cs`, `Infrastructure/Reportes/DisenoPdfReporte.cs`,
  `Infrastructure/Reportes/GeneradorPdfReportes.cs`, `Infrastructure/Reportes/GeneradorExcelReportes.cs`,
  `WinForms/Formularios/Reportes/FrmReportes.cs`, `WinForms/Formularios/FrmPrincipal.cs` (menú, `:434-454`).
- **Cambios:** D2.1–D2.8.
- **Pruebas:** `CalculadoraResumenAnualTests` (12 meses siempre; media res = 0,5; anuladas fuera de los totales vigentes;
  frontera de mes en hora local con zona UTC-4: una boleta del 31-ene 23:30 local cae en enero aunque en UTC sea febrero;
  1 000 sumas de 0,1 exactas). `ServicioReportesTests` (resumen: ignora `Hasta`; año inválido → `ValidacionException`;
  supera `MaximoFilasResumen` → mensaje; `MostrarDesposte` según F11; permiso `VerReportes`). `ReporteRepositorioTests`
  (`ListarParaResumenAsync` trae vigentes y anuladas, respeta el rango y el tope; `PlanConsultasReportesTests`: usa índice
  por `FechaHora`). `ColumnasReporteTests`, `TextosReporteTests` (título, subtítulo, `NombreArchivo`, `NombreTipo`).
  `GeneradorPdfReportesTests`/`GeneradorExcelReportesTests` (resumen con y sin desposte; 12 filas + totales; números reales
  en Excel). `FrmReportesTests` (combo incluye "Resumen anual"; `nudAnio` visible solo con ese tipo; "Año completo" fija
  1-ene/31-dic e invalida; acciones habilitadas con meses con datos; PDF deshabilitado sobre 5 000 si U7 = b).
  Test de menú en `FrmPrincipal` (los 7 ítems con `VerReportes`; Operador no los ve; cada uno abre el tipo correcto vía un
  gancho `AbrirReportesParaTest` si no existe uno).
- **Aceptación:** con datos de dos años, "Resumen anual 2026" muestra 12 filas y totales que cuadran con la suma de
  "Pesajes entre fechas" 1-ene/31-dic (vigentes) y con "Boletas anuladas" del mismo rango; exporta PDF y Excel; los menús
  abren directo cada reporte.

#### T3 — Motivo obligatorio al editar boleta (D3) · Esfuerzo **S**
- **Archivos:** `Domain/Reglas/ReglasGestionPesajes.cs`, `Application/Pesajes/ValidadorGestionPesajes.cs` (`:66-69`),
  `Application/Pesajes/ServicioGestionPesajes.cs` (`:335-339`: el motivo ya no es condicional),
  `WinForms/Formularios/Boletas/FrmEditarBoleta.cs` (`:224-226` etiqueta y pista; `:365-400` validación previa; `:399`
  envía el texto tal cual).
- **Pruebas:** `ValidadorGestionPesajesTests` (vacío, 4 caracteres, 5, 100, 101, solo espacios). `ServicioGestionPesajesTests`
  (sin motivo → `ValidacionException` y **sin** auditoría ni cambio de `Version`; con motivo → `Auditoria.Motivo` termina en
  `" — " + MOTIVO`; un test que relee la auditoría como lo hace `IServicioAuditoria` y verifica que el motivo llega al
  registro mostrado por el visor). `FrmEditarBoletaTests` (motivo corto → error en `txtMotivo`, el servicio falso no se
  invoca; motivo válido → se invoca). Ajustar los tests existentes que editan sin motivo (agregarles uno).
- **Aceptación:** Guardar sin motivo marca el campo y no llama al servicio; con motivo, el visor de auditoría muestra
  "Edición de boleta N: … — MOTIVO". Los pesos siguen sin poder editarse (U6).

### P1 — Integridad y captura automática (migración `F15_IntegridadYCaptura`)

#### T4 — Parámetros nuevos y migración (D4) · Esfuerzo **M** · Depende de: respuestas U1–U5
- **Archivos:** `Domain/Entities/Entities.cs` (`ParametrosSistema`, `Pesaje.Sello`),
  `Infrastructure/Persistencia/Configuraciones/ParametrosSistemaConfiguration.cs`, `…/PesajeConfiguration.cs` (columna y
  CHECK), migración nueva `Infrastructure/Persistencia/Migraciones/<timestamp>_F15_IntegridadYCaptura.cs` (+ Designer y
  snapshot, con `dotnet ef migrations add F15_IntegridadYCaptura`), `Application/Parametros/IServicioParametros.cs`,
  `ServicioParametros.cs`, `Dtos/ParametrosDtos.cs`, `Application/Auditoria/DiferenciasAuditoria.cs`,
  `Application/PuestaEnMarcha/ServicioPuestaEnMarcha.cs` (Finalizar con `PermitirPesajeSimulado = false` en instalación
  nueva), `WinForms/Formularios/Administracion/FrmOpcionesPesaje.cs` (nuevo), `WinForms/Formularios/FrmPrincipal.cs`
  (ítem de menú).
- **Pruebas:** `MigracionF15Tests` (base de F11/1.3.x con pesajes vigentes y anulados → migra; `PermitirPesajeSimulado = 1`,
  `CapturaAutomatica = 0`, `Sello` nulo, CHECK de longitud del sello rechaza 10 caracteres; `foreign_key_check` limpio);
  `ModeloVsMigracionTests` verde; `ServicioParametrosTests` (opciones: permisos, auditoría solo si cambia, etiquetas en
  `DiferenciasAuditoria`); `ServicioPuestaEnMarchaTests` (instalación nueva → simulado deshabilitado al finalizar);
  `LayoutFormulariosTests`/`VerificadorLayout` para `FrmOpcionesPesaje` a 96 DPI y 125 %.
- **Aceptación:** `dotnet ef migrations has-pending-model-changes` limpio; actualización desde 1.3.1 crea el resguardo
  `PreMigracion` y conserva el comportamiento (simulado permitido, captura manual).

#### T5 — Recibos de captura y verificación en el servicio (D5) · Esfuerzo **L** · Depende de T4 (solo por orden de migración/DTO; no usa columnas)
- **Archivos:** `Application/Balanza/RegistroCapturas.cs` (nuevo), `Application/Balanza/ServicioBalanza.cs` (`Publicar`,
  `:557`, llama `Observar`), `Application/DependencyInjection.cs` (singleton), `Domain/Reglas/ReglasPesaje.cs`
  (`VidaReciboCaptura`), `Application/Pesajes/Dtos/PesajeDtos.cs`, `Application/Pesajes/ServicioPesajes.cs` (`:114-120`,
  antes de `:161`), `WinForms/Presentacion/Pesaje/ControlCapturas.cs` (guarda recibos; reglas iguales),
  `WinForms/Formularios/Pesaje/FrmPesaje.cs` (`CapturarLado` `:836-851`, `BorrarPesaje` `:853-859`, `LeerDto` `~:1125-1138`),
  `WinForms/Capturas/SembradorDemo.cs` (`:232`, `:279`), `tests/…/Utilidades/Pesaje/ServicioPesajesFalso.cs` y un helper de
  tests `RecibosDePrueba` que alimenta un `RegistroCapturas` real.
- **Pruebas:** `RegistroCapturasTests` (no capturable → rechazo; peso fuera de rango → rechazo; sin retorno a cero →
  rechazo; tras observar ≤ 2 kg → emite; `Descartar` del último levanta la exigencia; vence a los 10 min con
  `FakeTimeProvider`; `Reservar` dos veces el mismo → falla; `Liberar` permite reintentar; concurrencia: 2 hilos reservando
  el mismo id → uno gana). `ServicioPesajesTests` (sin recibo → `PES_CAPTURA_REQUERIDA`; peso distinto →
  `PES_PESO_NO_CAPTURADO`; vencido → `PES_CAPTURA_VENCIDA`; recibo reutilizado tras grabar → rechazo; falla de BD → recibo
  liberado; recibo de simulador → `OrigenPeso.Simulador` aunque la instantánea actual sea real; la reserva del número
  sigue siendo la primera sentencia de la transacción). Actualizar los 30 llamados de tests (H12) con el helper.
  `ControlCapturasTests` sin cambios de reglas (deben seguir verdes tal cual). `FrmPesajeTests`: F8 descarta recibos.
- **Aceptación:** grabar un pesaje normal funciona igual para el operador; un DTO armado sin captura real no se graba; el
  build de capturas (`scripts\capturas-manual.ps1`) termina sin fallos.

#### T6 — Captura automática opcional (D6) · Esfuerzo **M** · Depende de T4 y T5
- **Archivos:** `WinForms/Presentacion/Pesaje/CapturaAutomatica.cs` (nuevo, puro), `…/EvaluadorPantallaPesaje.cs` (mensaje
  "Pesos capturados automáticamente…", indicador), `WinForms/Formularios/Pesaje/FrmPesaje.cs` (lee
  `ObtenerOpcionesPesajeAsync` al abrir; selector de lado en media res; dispara `CapturarLado`), `Domain/Reglas/ReglasPesaje.cs`
  (`PermanenciaCapturaAutomaticaMs`).
- **Pruebas:** `CapturaAutomaticaTests` (no dispara sin ganadero/cliente; no dispara antes de 1 500 ms estable; reinicia al
  perder estabilidad; res completa: izquierdo, exige cero, derecho; media res: respeta el lado elegido y no dispara un
  segundo lado; no dispara con simulador para Operador; no dispara con fletero sin elegir; antirrebote de 1 s).
  `FrmPesajeTests` (con la opción apagada nunca captura solo; encendida captura con un `IServicioBalanza` falso que publica
  lecturas estables; F12 sigue siendo manual). `EvaluadorPantallaPesajeTests` (mensajes nuevos con su prioridad).
- **Aceptación:** con la opción encendida, colgar una media res estable 1,5 s la captura sin teclas; bajar a cero y colgar
  la otra captura el derecho; nunca se graba sin F12.

#### T7 — Sello HMAC por fila y verificación de integridad (D7) · Esfuerzo **L** · Depende de T4 (columnas) · Solo si U3 = b/c
- **Archivos:** `Application/Integridad/SelloPesaje.cs`, `IClaveIntegridad.cs`, `IServicioIntegridad.cs`,
  `ServicioIntegridad.cs` (nuevos); `Infrastructure/Integridad/ClaveIntegridad.cs` (nuevo);
  `Infrastructure/Persistencia/SelladorPesajesInterceptor.cs` (nuevo), `SqliteOpcionesExtensiones.cs`;
  `Application/Arranque/ActualizadorBaseDatos.cs` (sellado inicial); `Application/Boletas/ServicioBoletas.cs`,
  `Dtos/BoletaDtos.cs` (`IntegridadFallida`), `Diseno/TextosBoleta.cs` (leyenda); `Application/Reportes/ServicioReportes.cs`,
  `Dtos/ReporteDtos.cs` (`FilaReporte.IntegridadFallida`, `TotalesReporte.Alteradas`), `TextosReporte.cs`, generadores
  PDF/Excel (nota y marca); `Application/Pesajes/ServicioGestionPesajes.cs` (rechazo `PES_INTEGRIDAD` en editar/anular);
  `WinForms/Formularios/Utilitarios/FrmVerificarIntegridad.cs` (nuevo) + menú en `FrmPrincipal`.
- **Pruebas:** `SelloPesajeTests` (determinista; cambia al tocar cualquier campo sellado; no cambia con `Id`/`CreadoEn`;
  invariante de cultura con `es-BO` y `en-US`; pesos 120,05 → mismo sello que 120,1 tras el conversor). Interceptor
  (`SqliteDbFixture`): alta, edición, anulación e importación dejan sello válido; `UPDATE` directo por SQL → la verificación
  lo detecta. `ActualizadorBaseDatosTests` (sella una vez; segunda corrida no re-sella; fila alterada **después** del sellado
  inicial sigue detectada tras reiniciar). `ServicioIntegridadTests` (huecos de numeración; sello nulo; permiso
  `VerAuditoria`). `DisenoBoletaTests`/`TextosBoleta` (leyenda alterada se suma a ANULADA/SIMULADO). `ServicioReportesTests`
  (nota y conteo). `ServicioGestionPesajesTests` (editar/anular fila alterada → `PES_INTEGRIDAD`). Backup/restauración: una
  base restaurada desde zip verifica sin alertas (`IdInstalacion` viaja con la base).
- **Aceptación:** editar el peso de una boleta con DB Browser → la boleta reimpresa sale con "REGISTRO ALTERADO —
  VERIFIQUE", el reporte lo nota al pie y "Verificar integridad" la lista; borrar una fila → aparece como hueco.

#### T8 — Simulador endurecido (D8) · Esfuerzo **S** · Depende de T4 y T5 (mismos archivos: hacer después de T5)
- **Archivos:** `Application/Pesajes/ServicioPesajes.cs`, `WinForms/Formularios/Pesaje/FrmPesaje.cs`,
  `WinForms/Presentacion/Pesaje/EvaluadorPantallaPesaje.cs` (o `ControlCapturas` vía un flag de construcción
  `simuladoPermitido`, **sin** cambiar las reglas existentes, solo sumando el rechazo nuevo).
- **Pruebas:** `ServicioPesajesTests` (parámetro apagado → `PES_SIMULADOR_DESHABILITADO` incluso para Administrador;
  encendido → motivo "(PESO SIMULADO)"); `FrmPesajeTests` (confirmación una vez con gancho `ConfirmarParaTest`; "No" cierra);
  `ControlCapturasTests` siguen verdes.
- **Aceptación:** en una instalación nueva finalizada, el simulador sirve para probar la conexión pero no graba boletas
  hasta que el Administrador lo permita explícitamente.

### P2 — Compatibilidad ampliada y menores

#### T9 — Historial por rango de fechas (D9) · Esfuerzo **S** · Paralelo con todo P0/P1 salvo T5/T6/T8 (no toca `FrmPesaje`)
- **Archivos:** `Application/Pesajes/IServicioPesajes.cs`, `ServicioPesajes.cs` (`:68-74`), `Domain/Reglas/ReglasPesaje.cs`
  (`HistorialDiasMaximo`), `WinForms/Formularios/Pesaje/FrmHistorialPesajes.cs` (`:32`, `:277-289`).
  **Ojo:** `ServicioPesajes.cs` y `ReglasPesaje.cs` también los toca T5: si van en paralelo, T9 primero y T5 rebasa.
- **Pruebas:** `ServicioPesajesTests` (rango de 31 días OK, 32 → `ValidacionException`, Desde > Hasta → error, límites del día
  local en UTC); `FrmHistorialPesajesTests` (dos selectores, recarga con token de versión, aviso de tope 500).
- **Aceptación:** el Operador consulta la semana completa en una sola vista.

#### T10 — Libras y comando de solicitud (D10.1–D10.3) · Esfuerzo **M** · Solo si U8 = b/c · Migración `F15_ProtocolosBalanza` (después de T4)
- **Archivos:** `Domain/Enums/Enums.cs`, `Domain/Entities/Entities.cs` (`ConfiguracionBalanza`),
  `Infrastructure/Persistencia/Configuraciones/ConfiguracionBalanzaConfiguration.cs` (`:16,28-29` + nuevas), migración,
  `Application/Balanza/ParserTramas.cs` (`:130`), validador y auditoría de configuración de balanza,
  `Infrastructure/Balanza/IPuertoSerie.cs`, `PuertoSerieWin32.cs` (`WriteFile`), `LectorBalanzaBase.cs`, `LectorSerial.cs`,
  `LectorTcp.cs`, `Simulador/LectorSimulador.cs` (ignora el comando), `WinForms/Formularios/Administracion/FrmConfiguracionBalanza.cs`
  (combo "Libras (lb)", campos "Comando de solicitud" e "Intervalo (ms)" con pista).
- **Pruebas:** `ParserTramasTests` (lb → kg con redondeo AwayFromZero), `MigracionF15ProtocolosTests` (CHECK nuevos y filas
  existentes), lector con puerto falso (envía el comando cada intervalo solo conectado; deja de enviar al detener; escapes
  `\r`/`\x05` decodificados), `LectorTcp` con servidor de prueba local, validador (intervalo ≥ `TimeoutMs` → error),
  `FrmConfiguracionBalanza` layout 96/125 %.
- **Aceptación:** con un indicador real en modo "a pedido", la lectura funciona con comando e intervalo configurados;
  **validación obligatoria con pesa patrón** antes de anunciarlo.

#### T11 — UDP (D10.4) · Esfuerzo **M** · Solo si U8 = c y hay un equipo concreto · Misma migración que T10
- **Archivos:** `Enums.cs` (`TipoConexion.Udp = 4`), `Infrastructure/Balanza/LectorUdp.cs` (nuevo), `FabricaLectorBalanza.cs`,
  CHECK `TipoConexion IN (1, 2, 3, 4)`, `FrmConfiguracionBalanza`.
- **Pruebas:** `LectorUdp` con un `UdpClient` de prueba en loopback; fábrica; migración.

#### T12 — Documentación, capturas, UAT y frase comercial · Esfuerzo **M** · Al final de cada bloque entregado
Ver §5. Incluye el caso UAT de ticket con papel personalizado.

#### T13 — Versión, CHANGELOG, bitácora, instalador y compuerta · Esfuerzo **S** · Última
Ver §6.

### Orden y paralelismo

```
P0:  T1 ─┐
     T2 ─┼─(independientes entre sí: T1 = Boletas/Impresion; T2 = Reportes + menú; T3 = Gestión de pesajes)
     T3 ─┘
P1:  [U1–U5 respondidas] → T4 → T5 → T6 → T8
                              └→ T7 (paralelo a T5/T6/T8: toca Integridad, interceptor, Boletas, Reportes, Gestión)
P2:  T9 (paralelo; coordinar ServicioPesajes/ReglasPesaje con T5)
     T10 → T11 (después de T4: segunda migración; nunca dos migraciones en paralelo)
Cierre: T12 → T13
```
Archivos compartidos que obligan a serializar: `FrmPrincipal.cs` (T2 menú Reportes, T4 Administración, T7 Utilitarios),
`ServicioPesajes.cs` (T5, T8, T9), `FrmPesaje.cs` (T5, T6, T8), `ServicioGestionPesajes.cs` (T3, T7),
`ServicioReportes.cs`/`ReporteDtos.cs` (T2, T7), `DisenoBoleta.cs`/`TextosBoleta.cs` (T1, T7), el snapshot de modelo EF
(T4, T10). Si se usan agentes en paralelo, uno por bloque y los demás rebasan sobre él.

---

## 5. Impacto en documentación (T12)

- **Manuales de cliente** (`docs/manual/clientes/fuente/manual-*.md` + `anotaciones-*.json`):
  - Operador: captura automática (si está encendida) y selector de lado; historial por rango; mensajes nuevos
    (`PES_CAPTURA_VENCIDA`, `PES_PESO_NO_CAPTURADO`); aviso de simulador.
  - Supervisor: motivo obligatorio al editar (con captura nueva del formulario y del error); pesos no editables → anular y
    volver a pesar; resumen anual, botón "Año completo" y menús nuevos; nota de integridad en reportes; leyenda
    "REGISTRO ALTERADO".
  - Administrador: marca de agua e intensidad en Configuración de impresión; Opciones de pesaje; Verificar integridad;
    simulador deshabilitado tras la puesta en marcha; (P2) libras y comando de solicitud.
- **Guion de capturas** (`WinForms/Capturas/Guion*.cs`): escenas nuevas `su-` (editar con motivo vacío → error; resumen
  anual; menú Reportes), `ad-` (impresión con marca de agua; opciones de pesaje; verificar integridad con una fila
  alterada sembrada a propósito en `SembradorDemo` bajo `#if CAPTURAS_MANUAL`), `op-` (captura automática; historial por
  rango); actualizar `comun-` del menú Reportes. Regenerar con `scripts\capturas-manual.ps1`, armar con
  `construir-manuales.py` y revisar con `revisar-paginas.py`.
- **Guías:** `docs/manual/guia-balanza.md` (sondeo, libras, lista honesta de compatibilidad: serie RS-232, USB-serie con
  puerto COM virtual, Ethernet TCP; **no** USB HID ni protocolos propietarios); `docs/manual/guia-instalacion-soporte.md`
  (actualización a 1.4.0, sellado inicial en el primer arranque y su duración, límites del sello, qué hacer ante "registro
  alterado", simulador deshabilitado); `docs/manual/manual-usuario.md` (referencia técnica de todo lo anterior).
- **README** y **CHANGELOG** (entrada 1.4.0 con Agregado/Cambiado: marca de agua en gris, motivo obligatorio, recibos de
  captura, sello).
- **Checklist UAT** (`docs/puesta-en-marcha/checklist-uat.md`), sección F15 y total actualizado:
  - UAT-F15-01 ticket 80 mm con logo: vista previa e impresión **física** muestran la marca de agua legible sin tapar datos.
  - UAT-F15-02 ticket en impresora térmica con **papel personalizado** (cuando la impresora no ofrece 80 mm): no se corta
    ni el contenido ni el fondo; documentar el nombre de papel usado.
  - UAT-F15-03 intensidad Suave/Media/Fuerte y casilla desactivada.
  - UAT-F15-04 resumen anual: totales = suma de "Pesajes entre fechas" del año; PDF y Excel.
  - UAT-F15-05 menús directos de reportes y "Año completo"; aviso de más de 20 000.
  - UAT-F15-06 editar sin motivo → error sin confirmar; con motivo → visible en el visor de auditoría.
  - UAT-F15-07 captura automática res completa y media res; nunca graba sola.
  - UAT-F15-08 balanza desconectada tras capturar y esperar 11 min → `PES_CAPTURA_VENCIDA`.
  - UAT-F15-09 alterar una boleta con DB Browser → leyenda, nota en reporte y lista en "Verificar integridad".
  - UAT-F15-10 simulador en instalación nueva finalizada: no graba; tras permitirlo, confirma y marca.
  - UAT-F15-11 historial de una semana.
  - UAT-F15-12 (P2) indicador a pedido / libras con pesa patrón.
- **Frase comercial sugerida (veraz)** — para propuesta y manuales; ajustar según U1/U3/U8:

  > Capataz registra ganaderos, clientes y fleteros y toma el peso **directamente del indicador de la balanza**, sin
  > digitarlo: con una tecla o, si se activa, **automáticamente al estabilizarse**, para media res o res completa. Cada
  > pesaje emite su boleta en **media carta o ticket de 80 mm**, con el **logo de la empresa como marca de agua**. Incluye
  > reportes de pesada del día, faeneo entre fechas, por ganadero, por cliente, por raza, de boletas anuladas y **resumen
  > anual por mes**, exportables a PDF y Excel. Maneja usuarios con roles Operador, Supervisor y Administrador. Las
  > boletas se corrigen o anulan **indicando siempre el motivo**, y cada cambio queda en la auditoría; los pesos no se
  > pueden modificar. Cada boleta lleva un **sello de integridad** que delata cambios hechos fuera del sistema. Se
  > conecta a indicadores por **puerto serie, USB-serie o red (TCP/IP)** que transmitan el peso en texto, hace **copias de
  > seguridad automáticas** diarias y guarda el **historial de pesajes**.

  Lo que la frase **no** debe decir: "evita toda manipulación" (el sello detecta, no impide; ver D7.6), "compatible con
  cualquier indicador" (solo protocolos de texto; otros se evalúan con el equipo) ni "USB" a secas.
- **`AGENTS.md`:** fila F15 en la tabla de estado, mapa de fases (F13 → F14 guía → F15), §3 con T1..T13, viñeta "Cierre de
  brechas (F15)" en §4 con D1–D10 resumidas y entrada de bitácora en §5 (fecha, estado, migraciones, pruebas antes → después,
  instalador, pendientes).

---

## 6. Compuerta final (T13)

1. `Directory.Build.props` `<Version>1.4.0</Version>` (o 1.3.2 si solo se entrega P0, sin migración).
2. `dotnet build src/PesajeFaeneo.sln -c Release` con 0 advertencias y 0 errores; también con `-p:CapturasManual=true`.
3. Suite completa en verde en **dos corridas seguidas**; anotar el total antes y después.
4. `dotnet ef migrations has-pending-model-changes` limpio; `MigracionF15Tests` (y de protocolos si aplica) en verde.
   Prueba manual de actualización: copia de una base 1.3.1 real → arranque → resguardo `PesajeFaeneo_premigracion_*.zip`
   creado, sellado inicial auditado, sin alertas de integridad.
5. Renderizado PNG de la boleta de muestra (`MuestraBoletaPngTests`): media carta y ticket, con y sin logo, intensidades
   Suave/Media/Fuerte; abrir los PNG y revisar que la marca de agua se ve y no tapa datos. Copiar los PNG al scratchpad
   (no al repositorio) e indicar la ruta en el informe.
6. `scripts\publicar.ps1`: autoprueba 5/5 OK.
7. `scripts\construir-instalador.ps1`: `Capataz-Setup-1.4.0-x64.exe` sin advertencias; anotar tamaño y SHA-256.
8. Capturas y manuales regenerados sin fallos; `revisar-paginas.py` sin páginas vacías fuera de fin de capítulo.
9. Finales de línea y codificación: ningún `.cs`/`.md` nuevo con BOM ni LF; `.ps1`/`.iss`/`.txt` en ASCII.
10. Sin commits salvo que el usuario lo pida.

---

## 7. Riesgos y preguntas abiertas

**Riesgos**
- **R1 Marca de agua en térmicas:** el resultado depende del driver (tramado). Sin prueba física (UAT-F15-01/02) no se sabe
  qué intensidad sirve; por eso es configurable por equipo. Un logo con mucho negro puede ensuciar el ticket aun en Suave.
- **R2 Recibos y flujo del operador:** una captura que se deja más de 10 minutos sin grabar ahora se rechaza; mencionarlo en
  la capacitación. Si el cliente reporta rechazos legítimos, subir `VidaReciboCaptura` (constante).
- **R3 Captura automática:** con balanzas que oscilan cerca de la estabilidad (viento, gancho que se mece) puede disparar
  tarde o capturar un peso que el operador no quería; por eso es opcional y el grabado queda manual.
- **R4 Sello:** detecta y no impide; quien descompile puede re-sellar. Un fallo de cálculo (cultura, redondeo REAL) marcaría
  boletas legítimas como alteradas: por eso las pruebas de invariancia y de ida y vuelta con el conversor son obligatorias,
  y la leyenda dice "VERIFIQUE", no "FRAUDE".
- **R5 Sellado inicial:** sella como válidas las filas que ya hubieran sido alteradas antes de 1.4.0 (no hay forma de saberlo).
  En una base grande tarda (estimar ~2 s cada 20 000 filas); medirlo y documentarlo.
- **R6 Migración:** `Pesajes.Sello` con CHECK puede obligar a EF a reconstruir la tabla `Pesajes` en SQLite; verificar con
  `MigracionF15Tests` sobre datos reales (como F10) y que el resguardo `PreMigracion` se crea antes.
- **R7 Resumen anual:** si un año supera 500 000 boletas (improbable) se rechaza con mensaje; la memoria del resumen es baja.
- **R8 P2 sin hardware:** libras, sondeo y UDP no se pueden validar sin el indicador; no anunciarlos hasta UAT-F15-12.
- **R9 Capturas:** cambian muchas (menú Reportes, Editar boleta, Configuración de impresión, pesaje): regenerar todas y
  revisar anotaciones.

**Preguntas abiertas (para el cliente)**
- P1 ¿"Boletas anuales" significa el resumen por mes (recomendado) o una lista de todas las boletas del año? (Si es la
  lista, con más de 20 000 boletas solo sale en Excel o por tramos.)
- P2 ¿Qué intensidad de marca de agua prefiere tras ver la impresión física? ¿El ticket debe llevar también el logo arriba
  además del fondo?
- P3 ¿Quiere la captura automática encendida por defecto en su instalación?
- P4 ¿Necesita resumen anual por ganadero o por cliente (U4 = c)?
- P5 ¿Tiene algún indicador que trabaje "a pedido", en libras o por UDP? Marca y modelo.
- P6 ¿Acepta que el Supervisor no pueda corregir pesos (solo anular y volver a pesar)?
