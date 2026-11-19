# F12 — Correcciones de defectos detectados en la revisión de manuales (1.2.1)

Plan para corregir los defectos de la aplicación que encontró la revisión de manuales del 2026-10-13 (ver la entrada
"Revisión de manuales e instalador 1.2.0" de la bitácora en `AGENTS.md`). Hasta ahora los manuales los "sorteaban"
describiendo el comportamiento defectuoso; tras corregirlos, los manuales deben describir el comportamiento nuevo.

Rama: la actual (`feat/f9-empaquetado`). Sin migraciones: la versión pasa a **1.2.1** (PARCHE según `CHANGELOG.md`).

---

## 0. Defectos (hallazgos verificados en el código)

| # | Defecto | Dónde | Efecto para el usuario |
|---|---|---|---|
| E1 | Un fletero escrito y no elegido de la lista (varias coincidencias o ninguna) se graba **sin fletero y sin aviso**. | `ControlBusquedaCatalogo.EjecutarSalida` solo elige con una única coincidencia; `FrmPesaje.LeerDto` y `FrmEditarBoleta.GuardarAsync` envían `ctlFletero.Seleccion?.Id` (null). | Boletas sin fletero cuando el operador creyó haberlo puesto (pérdida silenciosa de un dato que va impreso y firma). |
| E2 | **Grabar (F12)** se ve verde aunque esté deshabilitado. | `FrmPesaje.ConstruirPanelAcciones`: `BackColor`/`ForeColor` fijos; WinForms no cambia el fondo de un botón deshabilitado. | El operador pulsa un botón que parece activo y no pasa nada. |
| E3 | La línea de atajos del inicio muestra **"F6 Buscar boletas"** a todos los roles. | `FrmPrincipal`, texto fijo de `lblAtajos`. El ítem F6 exige `Permiso.EditarBoleta` (Operador no lo tiene). | El Operador ve un atajo que no puede usar. |
| E4 | Con la balanza **vacía y estable** la guía dice **"Peso fuera de rango (10,0 a 600,0 kg)."**. Además, en media res con dos lados capturados, el aviso "… Use F8 para borrar el pesaje." casi nunca aparece. | `EvaluadorPantallaPesaje.CalcularMensaje`: el paso 8 no excluye el peso de reposo (≤ `ReglasPesaje.UmbralRetornoCeroKg`); el paso 11 (pesos incoherentes) va detrás de los pasos 6–10. | Mensaje alarmante en el estado normal entre animales; aviso de F8 oculto. |
| E5 | En **Anular boleta** la confirmación "¿Desea continuar?" aparece **antes** de validar el motivo. | `FrmAnularBoleta.AnularAsync`: confirma y recién después llama al servicio. | El usuario confirma y luego recibe un error de motivo. |
| E6 | La prueba de balanza informa **"Conexión correcta: 0 de 0 tramas reconocidas"** sin ninguna pista. | `FrmConfiguracionBalanza` (resultado de `ProbarAsync` con `Conecto = true` y `TramasRecibidas = 0`). | Parece correcta una configuración que no lee nada (caso típico: separación de tramas equivocada, p. ej. RX8 con "Fin de línea"). |

---

## 1. Decisiones

| # | Decisión |
|---|---|
| D1 | **E1 — bloquear, no adivinar.** `ControlBusquedaCatalogo` expone `TextoSinElegir` (= sin selección y con texto). En `FrmPesaje`, un fletero con texto sin elegir **impide grabar** (`PuedeGrabar = false`) y la guía muestra **"Elija el fletero de la lista o borre el texto."** (nivel Advertencia, prioridad inmediatamente después de ganadero y cliente). En `FrmEditarBoleta`, **Guardar (F12)** marca el campo con el `ErrorProvider` y el mismo texto, sin llamar al servicio. El fletero sigue siendo opcional: con el campo vacío se graba sin fletero como hoy. No se aplica a ganadero/cliente (ya bloquean con "Seleccione el ganadero/cliente…"). |
| D2 | **E2 — aspecto deshabilitado real.** `btnGrabar.EnabledChanged`: habilitado = verde (`46,139,87`) con texto blanco; deshabilitado = `SystemColors.Control` con texto `SystemColors.GrayText`. Sin cambiar tamaño, fuente ni posición. |
| D3 | **E3 — atajos según permisos.** La línea se arma con las teclas que el usuario puede usar: F2 y F4 (`Pesar`), F3 (`ReimprimirBoleta`), F6 (`EditarBoleta`). Si no queda ninguna, la etiqueta no se muestra. Mismos textos que hoy ("F2 Control de peso", "F3 Imprimir ticket", "F4 Historial", "F6 Buscar boletas") y mismo separador. |
| D4 | **E4 — guía en reposo y F8 visible.** (a) El paso 8 ("Peso fuera de rango") se omite cuando el peso estable está dentro del umbral de reposo (`|peso| ≤ UmbralRetornoCeroKg`, 2 kg): la guía cae en el paso 9/10 ("Capture el lado izquierdo (F5) y el derecho (F6).", "Capture un lado (F5 o F6)."…). Un peso de 2,1 a 9,9 kg sigue mostrando "Peso fuera de rango". (b) El caso "media res con los dos lados capturados" se evalúa **antes** del retorno a cero / estabilidad / rango, porque la única salida es F8. No cambian `PuedeCapturar*` ni `PuedeGrabar` ni `ControlCapturas`. |
| D5 | **E5 — validar antes de confirmar.** `FrmAnularBoleta` llama primero a `ValidadorGestionPesajes.ValidarAnulacion` (la misma regla del servicio, 5–100 caracteres); si hay errores los muestra en el `ErrorProvider` y no pregunta. La confirmación y la validación del servicio se mantienen (defensa en profundidad). |
| D6 | **E6 — aviso con 0 tramas.** Con `Conecto = true` y `TramasRecibidas = 0`: **"Se conectó, pero no llegó ninguna trama. Revise que el indicador esté encendido y transmitiendo, y la «Separación de tramas» (el RX8 usa «Inicio con '='»)."** en rojo oscuro. No se cambia el servicio ni el DTO (no hace falta contar bytes). El caso con tramas y 0 válidas ("Revise el patrón de lectura.") no cambia. |
| D7 | **Fuera de alcance** (anotados, a decidir con el dueño del producto): `Enter` en Usuario del login (hoy acciona Ingresar; el `KeyDown` que pasaría a Contraseña es inalcanzable), mensajes de validación inalcanzables por `MaxLength`, versión del manifiesto de la copia `premigracion`, separador decimal regional en pesaje/balanza, Salir sin confirmación, botón de calibración y aviso de impresora virtual cortados. |

---

## 2. Tareas

| T | Contenido | Archivos principales |
|---|---|---|
| T1 | E1: `ControlBusquedaCatalogo.TextoSinElegir`; `DatosPantallaPesaje.FleteroSinElegir` + regla en `EvaluadorPantallaPesaje` (bloquea grabar y mensaje); `FrmPesaje` lo alimenta; `FrmEditarBoleta.GuardarAsync` valida antes del servicio. | `Controles/ControlBusquedaCatalogo.cs`, `Presentacion/Pesaje/EvaluadorPantallaPesaje.cs`, `Formularios/Pesaje/FrmPesaje.cs`, `Formularios/Boletas/FrmEditarBoleta.cs` |
| T2 | E2: colores de `btnGrabar` según `Enabled`. | `FrmPesaje.cs` |
| T3 | E3: línea de atajos por permisos. | `Formularios/FrmPrincipal.cs` |
| T4 | E4: orden y condición de los mensajes guía. | `EvaluadorPantallaPesaje.cs` |
| T5 | E5: validación previa en anulación. | `Formularios/Boletas/FrmAnularBoleta.cs` |
| T6 | E6: aviso de 0 tramas. | `Formularios/Administracion/FrmConfiguracionBalanza.cs` |
| T7 | Pruebas: `EvaluadorPantallaPesajeTests` (reposo 0 kg y 2,0 kg → mensaje de captura; 5 kg → fuera de rango; fletero sin elegir bloquea; media res con dos lados → F8 aun esperando retorno a cero), `ControlBusquedaCatalogo` (`TextoSinElegir`), `FrmPesaje` (colores de Grabar), `FrmEditarBoleta` (fletero sin elegir no llama al servicio), `FrmAnularBoleta` (motivo corto: no confirma), `FrmPrincipal` (atajos por rol), `FrmConfiguracionBalanza` (0 tramas). | `tests/PesajeFaeneo.Tests` |
| T8 | Manuales: Operador (fletero sin elegir ahora avisa y bloquea; Grabar gris; guía en reposo; F6 ya no aparece), Supervisor (anulación valida antes; fletero en edición; guía en reposo), Administrador (prueba de balanza con 0 tramas), `manual-usuario.md`, guía de balanza (§5/§8). Guion de capturas: `op-13b` con un texto de **varias** coincidencias para mostrar el aviso nuevo; `su-06c` sin el estado imposible (Ganadero vacío → "Seleccione el ganadero."). Regenerar capturas y manuales; `revisar-paginas.py`. | `docs/manual/**`, `WinForms/Capturas/*` |
| T9 | Versión 1.2.1 (`Directory.Build.props`), `CHANGELOG.md`, casos UAT nuevos en `checklist-uat.md` (UAT-F12-01 a 06), bitácora y estado de fases en `AGENTS.md`, instalador `PesajeFaeneo-Setup-1.2.1-x64.exe` y carpeta `Desktop\Instaladores\PesajeFaeneo-1.2.1`. | — |

---

## 3. Criterios de aceptación

1. Pesaje: escribir un fletero con varias coincidencias y no elegirlo → Grabar deshabilitado (gris) y guía "Elija el fletero de la lista o borre el texto."; borrar el texto o elegirlo → se puede grabar.
2. Editar boleta: lo mismo al pulsar Guardar (F12); el servicio no se invoca.
3. Grabar se ve gris cuando está deshabilitado y verde cuando está habilitado.
4. Operador: la línea de atajos no incluye F6; Supervisor y Administrador sí.
5. Balanza en 0,0 kg estable con ganadero y cliente elegidos → "Capture el lado izquierdo (F5) y el derecho (F6)." (res completa) o "Capture un lado (F5 o F6)." (solo media res); con 5,0 kg estable → "Peso fuera de rango (10,0 a 600,0 kg).".
6. Anular con motivo de menos de 5 caracteres → error en el campo y **ninguna** confirmación.
7. Prueba de balanza que conecta sin recibir tramas → aviso rojo con la pista de "Separación de tramas".
8. `dotnet build -c Release` sin advertencias; todas las pruebas en verde (dos corridas); capturas y manuales regenerados sin fallos; autoprueba de la publicación 5/5.

---

## 4. Riesgos

- Bloquear el grabado por el fletero cambia el flujo del operador: si escribe algo y no lo elige, ahora debe resolverlo. Es el comportamiento deseado (antes perdía el dato sin saberlo), pero conviene mencionarlo en la capacitación.
- Las capturas de los manuales cambian (botón Grabar gris, guía en reposo, línea de atajos del Operador): hay que regenerarlas todas y revisar las anotaciones afectadas.
