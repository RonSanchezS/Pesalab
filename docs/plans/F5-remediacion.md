# Plan de remediación F5 — Boleta de pesaje (revisión QA post-ejecución)

**Fecha:** 2026-09-28
**Base:** árbol preparado de `feat/f5-boleta` sobre `b28d0e3` (F4). Build Release 0/0 y 810 tests en verde, verificados por el revisor antes de tocar nada.
**Fuente:** revisión del informe final de F5 contra `docs/plans/F5-boleta-pesaje.md` y el código preparado.

## 1. Hallazgos

| # | Sev. | Hallazgo | Evidencia | Regla / sección |
|---|---|---|---|---|
| H1 | Alta (cumplimiento) | El informe dice que la compuerta E.4 pasó, pero los **48 archivos nuevos** de F5 están en LF en el árbol de trabajo. La comprobación `git ls-files --eol … \| grep "w/lf"` debía salir vacía y lista los 48. | `.editorconfig` exige `end_of_line = crlf`; los archivos existentes están `w/crlf`. | Regla 0.4, E.4 |
| H2 | Alta (antifraude) | `TextosBoleta.Leyendas` encadena las leyendas de estado con `else if`, así que se excluyen entre sí: una boleta **anulada** de peso **simulado** no imprime "PESO SIMULADO — SIN VALOR COMERCIAL". El test "todas activas" de 6.5 se reemplazó por casos aislados que no detectan el defecto. | `TextosBoleta.cs`, `TextosBoletaTests.Leyendas_OrdenExactoYPrioiridades` | 6.2 ("solo las que apliquen"), 6.5 |
| H3 | Media (antifraude/UX) | En `FrmReimprimirBoleta`, si se cambia el número sin volver a buscar, `_datos` sigue apuntando a la boleta anterior: F9/F4 imprimen o previsualizan una boleta distinta de la que muestra el campo. Tampoco se descarta una búsqueda que termina cuando el número ya cambió. | `FrmReimprimirBoleta.cs` (sin manejador de cambio de `numBoleta`) | D17, 0.12 |
| H4 | Media (robustez) | `ImpresorBoletasGdi.ConstruirDocumento` solo traduce "impresora no válida". Un `InvalidPrinterException`/`Win32Exception` lanzado al leer `PaperSizes`/`DefaultPageSettings` (impresora que desaparece tras `IsValid`, cola de impresión detenida) sale como "error inesperado" en vez del `IMP_ERROR_IMPRESORA` que espera la fachada. | `ImpresorBoletasGdi.cs` | R23, R39, 7.5 |
| H5 | Baja (spec) | El botón de calibración dice "Calibración" en lugar de "Imprimir página de calibración", que es el texto que citan 11.1 y el checklist manual del propio informe. | `FrmConfiguracionImpresion.cs` | 11.1 |
| H6 | Baja (diseño) | Página de calibración: en media carta, la instrucción larga (≈111 caracteres a 9 pt en 176 mm, `NoWrap`) se trunca con "…"; en el ticket, las instrucciones se superponen a los números de la regla izquierda (X 13,5–23,5). | `DisenoCalibracion.cs` | D19, R18 |
| H7 | Baja (informe) | Cifras inconsistentes: la tabla del informe suma **+150** tests (9+21+28+24+23+10+9+15+7+4), pero el delta real es **+146** (664 → 810). `ValidadorConfiguracionImpresion` (T1 en el plan) aparece en T3 y no se declara como desviación. Las acciones se nombran "ImprimirBoleta/ReimprimirBoleta" (en el código son `Imprimir`/`Reimprimir`). Todas las corridas de todas las tareas dicen "1 m 33 s"; una corrida real tarda unos 8 s, así que la bitácora no parece copiar la salida literal que exige E.5. | Informe, `../pesalab-f5-bitacora.md` | E.5, 13.5 |
| H8 | Baja (doc) | `Agents.md §4`: la viñeta de capas de WinForms no dice por qué la impresión GDI+ vive en WinForms (D2), como pedía 13.1. Falta una nota para F6 sobre leyendas largas (ver R-F6-1). | `Agents.md` | 13.1 |
| H9 | Aceptado | Media carta agrega `TextoBoleta` con texto vacío (color/raza, dirección u operador vacíos), aunque 6.3 pide omitirlos. `PintorBoleta` ya ignora los textos vacíos, así que no cambia lo impreso. Se deja como está. | `DisenoBoleta.cs` | 6.3 |

Verificado **sin** defectos: capas y `ArquitecturaTests`, sin `DateTime.Now`/`CurrentCulture` en el diseño, `StandardPrintController` + `OriginAtMargins = false` + reinicio en `OnBeginPrint`, compensación de `HardMargin` solo al imprimir, auditoría posterior a `Print()`, orden atómico de `GuardarAsync` (R13), sin `EjecutarAsync` anidados en `FrmPesaje`/`FrmReimprimirBoleta`/`FrmConfiguracionImpresion`, `btnImprimir.TabStop = false`, cierre bloqueado con `_imprimiendo`, `Contains` ausente en `AuditoriaRepositorio`, sin migraciones, `AutorizadorMenuTests` intacto, DI registrado en T6.

## 2. Tareas

| Tarea | Corrige | Archivos | Verificación |
|---|---|---|---|
| R1 | H2 | `Application/Boletas/Diseno/TextosBoleta.cs`, `tests/.../TextosBoletaTests.cs` | Test nuevo: con muestra + anulada + simulado + reimpresión + copia salen las 5 leyendas en el orden de 6.2; anulada + importada salen juntas. La marca de agua mantiene su prioridad única. |
| R2 | H3 | `WinForms/Formularios/Boletas/FrmReimprimirBoleta.cs`, `tests/.../FrmReimprimirBoletaTests.cs` | Tests nuevos: cambiar `Value` o `Text` tras buscar deshabilita F4/F9 y limpia el resumen; F9 no llama a la impresión; una búsqueda que termina después de cambiar el número se descarta (token de versión, como `_versionSugerencia` de F4). |
| R3 | H4 | `WinForms/Impresion/ImpresorBoletasGdi.cs` | `InvalidPrinterException`/`Win32Exception` durante la construcción → `ReglaNegocioException("IMP_ERROR_IMPRESORA", "No se pudo imprimir en '{impresora}': {mensaje}")`, desechando el documento. Los tests existentes siguen en verde; no hay test determinista sin impresora (regla 0.11). |
| R4 | H5 | `WinForms/Formularios/Administracion/FrmConfiguracionImpresion.cs`, `tests/.../FrmConfiguracionImpresionTests.cs` | El texto del botón es "Imprimir página de calibración" (con aserción). |
| R5 | H6 | `Application/Boletas/Diseno/DisenoCalibracion.cs` | Instrucciones de media carta en varias líneas; textos del ticket corridos a la derecha de la regla. Los `DisenoCalibracionTests` de límites siguen en verde. Test nuevo: ningún texto de instrucciones pisa la columna de números de la regla izquierda. |
| R6 | H1 | Los 48 archivos nuevos + los que tocan R1–R5 | Se convierten a CRLF; `git ls-files --eol … \| grep "w/lf"` sale vacío. |
| R7 | H7, H8 | `Agents.md`, `README.md` (si cambian las cifras) | Entrada "Fase F5 — Remediación post-revisión" en §5 con cifras **medidas**; motivo D2 en la viñeta de capas; nota R-F6-1. |

Compuerta tras R1–R7: `dotnet build -c Release` (0 warnings), `dotnet test` ×3 con la salida real, las comprobaciones `grep` de E.3/E.4 y `has-pending-model-changes`. Los cambios quedan preparados (`git add`) sin commit, igual que en F5.

## 3. Riesgos que pasan a fases futuras

- **R-F6-1:** en media carta, las leyendas van en una sola línea de 200 mm (`NoWrap` + "…"). En F6, un motivo de anulación largo puede cortar las leyendas que siguen ("PESO SIMULADO…", "REIMPRESIÓN n"). Al implementar la anulación, hay que limitar el motivo que se imprime en la leyenda o pasar esa fila a dos líneas.
