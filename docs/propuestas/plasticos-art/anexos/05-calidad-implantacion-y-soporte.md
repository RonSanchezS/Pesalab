# Anexo 05 – Calidad, implantación, capacitación y soporte
**Sistema de Producción – PLÁSTICOS ART** · Rol: Líder de QA e Implantación · Fecha: 2026-11-11

> **Errata de vocabulario y ajustes de UAT (subordinado al [Plan maestro](../Plan-Implementacion-Plasticos-ART.md), DEC-05 y sección 8.2):** estados oficiales de 02. Orden: Pendiente/Planificada = **Emitida**; Planificada→Liberada = **Emitida→Liberada**; En proceso = **EnProduccion**; se agrega **Terminada** antes de **Cerrada**; "Anular" = **Cancelar**. Pedido: "Pendiente" = **Registrado**. Casos UAT-1 que dependen de funciones posteriores: 07 (se congela v1 en el snapshot de la orden, sin cálculo de MP), 10 y 11 (solo hasta Liberada en S4), 12 (cancelar solo Emitida o Liberada; la liberación de reserva se verifica en S5) y 21 (se verifica en S6 con INF-02, no cuenta en el denominador de S4). Los casos 08, 10, 11 y 12 de abajo ya están reescritos.
>
> Parte de la Opción B del Anexo 01 (WinForms .NET 8 + PostgreSQL 16 en LAN, balanza leída en proceso).
> Convención: **[SUPUESTO]** = se da por cierto hasta confirmar; **[PREGUNTA]** = se pregunta al cliente; **[CLIENTE]** = tarea del cliente de la que depende el plazo.

---

## 0. Resumen ejecutivo

| Tema | Decisión |
|---|---|
| Equipo real | **1 desarrollador principal** hace también QA e implantación (el rol "QA" es un sombrero, no una persona). Un **usuario clave** del cliente hace de segundo par de ojos en UAT. |
| Estrategia | Automatizar lo que es **cálculo y regla** (MP por orden, kardex, peso→cantidad, permisos, estados); probar a mano lo que es **pantalla y flujo**; probar la balanza con **simulador primero y balanza real después** (semana 6 y semana 7). |
| UAT | Dos aceptaciones formales: **UAT-1 (semana 4)** módulos 1–5 + usuarios → dispara el 2.º pago (40 %); **UAT-2 (semana 8)** sistema completo → dispara el 3.er pago (30 %). 42 casos con ID (§3). |
| Datos reales | Semana 7: una semana de producción real **en paralelo** (el método actual sigue siendo el oficial), con criterios de salida numéricos (§4). |
| Corte | Cutover un lunes de semana 8 **después** de toma de inventario físico; paralelo 5 días hábiles; reversa posible durante 10 días hábiles sin perder datos (§6). |
| Capacitación | 4 sesiones por rol, total 9 h de aula + práctica en puesto; evaluación práctica con lista de verificación; 1 usuario clave (§7). |
| Soporte | 90 días corridos desde el acta de entrega final; severidades con tiempos realistas para una sola persona; corrección ≠ cambio (§8). |
| Horas de este anexo dentro de las 8 semanas | **≈ 66 h** (≈ 20 % de las ~320 h). Soporte de 90 días: ≈ 24 h adicionales **fuera** de las 8 semanas, ya incluidas en el precio. |

---

## 1. Estrategia de calidad (1 desarrollador, 8 semanas)

### 1.1 Principio
Con ~320 h totales no se puede aplicar una pirámide "de libro". Se invierte donde un error **cuesta dinero al cliente** (stock, consumo de materia prima, pesos) y se confía en el UAT manual para la presentación de pantallas. Heredamos de Pesalab una base de ~1 950 pruebas y las convenciones (`SqliteDbFixture`, builders, `FakeTimeProvider`, `ArquitecturaTests`) que se adaptan a PostgreSQL.

### 1.2 Pirámide acordada

| Nivel | Qué cubre | % esfuerzo QA | Herramienta | Quién |
|---|---|---:|---|---|
| Unitarias de dominio (reglas puras) | Cálculo de MP por orden, conversión peso→cantidad, tolerancias, máquina de estados, redondeos | 35 % | xUnit (solo `Assert`) | Dev |
| Integración de servicios + BD real | Kardex y stock con transacciones, concurrencia (2 salidas simultáneas), permisos por servicio, auditoría | 30 % | xUnit + **PostgreSQL 16 efímero** (Testcontainers si Docker está disponible en el PC de desarrollo; si no, BD `pruebas` local reseteada con script) | Dev |
| Arquitectura | Reglas de capas (reutiliza `ArquitecturaTests`) | 2 % | xUnit por reflexión | Dev |
| Layout de formularios | Textos cortados a 96 DPI y 125 % (reutiliza `VerificadorLayout`) | 3 % | xUnit | Dev |
| Manual guiado (UAT) | Flujos extremo a extremo, impresión, balanza real, instalación | 30 % | Casos de §3 | Dev + usuario clave |

**No se hace:** pruebas de carga formales (hasta 20 usuarios, volumen bajo: se hace **una** prueba de humo de concurrencia con 5 sesiones simultáneas), pruebas automatizadas de UI (costo/beneficio negativo), pruebas de penetración.

### 1.3 Qué se automatiza (obligatorio antes de cada entrega)

| Área | Reglas que se fijan con pruebas | Mínimo de pruebas |
|---|---|---:|
| Cálculo de materia prima por orden | `necesaria = cantidad_fórmula × cantidad_a_producir / rendimiento` (+ merma %); redondeo a la unidad de la MP (kg a 3 decimales, `decimal`); sin `double`; fórmula con N líneas; MP repetida; cantidad 0 o negativa rechazada; stock suficiente/faltante por línea; disponibilidad **descontando reservas** de otras órdenes abiertas [SUPUESTO: se calcula faltante contra stock físico menos reservado] | 25 |
| Kardex / stock | Saldo = Σ ingresos − Σ salidas ± ajustes; nunca negativo salvo ajuste autorizado; anulación = contra‑movimiento (no borrado); saldo recalculado desde movimientos coincide con saldo guardado; concurrencia (dos salidas que juntas exceden el stock: una falla) | 25 |
| Conversión peso→cantidad | `cantidad = peso_neto_lote / peso_unitario_estándar`; tolerancia ± x % del estándar [PREGUNTA]; peso neto = bruto − tara; unidad (g/kg) como en `UnidadPesoTrama`; resolución según balanza real | 15 |
| Estados de orden | Transiciones válidas/ inválidas (Emitida→Liberada→EnProduccion→Terminada→Cerrada o Cancelada), cierre solo con consumo registrado, no se edita orden cerrada | 12 |
| Fórmulas versionadas | Editar fórmula crea versión nueva; órdenes abiertas conservan la versión con que se calcularon (D‑QA1, ver §3 caso UAT‑1‑12) | 8 |
| Permisos y roles | Matriz Rol × Permiso exhaustiva (como `ReglasPermisosTests` de Pesalab) + `ExigirPermiso` en cada servicio | 40 (parametrizadas) |
| Balanza (reutilizado) | Parser, estabilidad, `EsCapturable` — ya cubiertos; se **añaden** solo casos del formato de la balanza real de Plásticos ART | 6 |
| Auditoría | Diff antes/después en altas, ediciones, anulaciones de movimiento | 6 |

Meta de entrada a cada entrega: **0 pruebas rojas**, 0 warnings (`TreatWarningsAsErrors`), `ArquitecturaTests` verde. Meta de cobertura: informativa (≥ 80 % en `Domain` y en los servicios de cálculo/inventario), no bloqueante.

### 1.4 Balanza real vs. simulador

| Fase | Con qué | Qué se valida | Criterio |
|---|---|---|---|
| Semana 1 | Checklist de protocolo + conexión de prueba (si el cliente la presta) | Modelo, puerto, 9600 8N1 u otro, continuo o bajo demanda, unidad, resolución | Checklist de balanza firmado (campo 3.4) |
| Semanas 2–5 | **Simulador** (`GeneradorPesoSimulado`, reutilizado) | Toda la lógica de módulos 7 y 8 sin hardware | Casos UAT‑2‑xx pasan con origen "Simulador" |
| Semana 6 | **Balanza real en la estación de pesaje** (1 jornada de sitio, ≈ 4 h) | Lectura continua 30 min sin pérdida de tramas, estabilidad, retorno a cero, pesa patrón conocida | 100 % tramas parseadas; error vs. pesa patrón ≤ 1 división de la balanza; CERO reconexiones espontáneas en 30 min |
| Semana 7 | Balanza real en producción | Uso real con operarios | ver §4 |
| Siempre | **Modo manual de respaldo** | Digitación del peso con motivo, marcado "MANUAL" en kardex y reportes | Caso UAT‑2‑09 |

**Regla antifraude heredada:** un peso simulado jamás se acepta como real en producción; solo el administrador puede usar el simulador y el registro queda con origen "Simulador". Se **prueba** que un operador no puede capturar con el simulador.

### 1.5 Datos de prueba realistas
- **Semana 1:** pedir al cliente 5 productos representativos con su fórmula, 10 materias primas con unidad y costo [CLIENTE], 3 operarios por turno y 2 turnos.
- **Juego de datos "ART‑demo"** (script de siembra, parte de `SembradorDemo` de Pesalab adaptado): 12 ítems, 15 MP, 8 fórmulas (una con 6 líneas, una con MP repetida), 10 pedidos, 6 órdenes en distintos estados, 6 operarios, 2 turnos, ≈ 60 movimientos de kardex con ajustes y anulaciones, 40 pesadas. Sirve para UAT, capacitación y capturas de manuales. **Nunca** se mezcla con la BD de producción (BD distinta `plasticosart_capacitacion`).
- **Casos numéricos "de oro"** con el resultado calculado en Excel por el usuario clave (ver §3.3, casos de cálculo): son la verdad contra la que se compara el sistema.

---

## 2. Responsabilidades y calendario de calidad

| Semana | Actividad de QA / implantación | Horas |
|---|---|---:|
| 1 | Plan de pruebas, checklist de balanza, plantilla de casos y acta, definición de datos "de oro", infraestructura de pruebas (BD efímera) | 8 |
| 2–3 | Pruebas automáticas junto al código de módulos 1–3 (se escriben **con** el código, no después) | 6 |
| 4 | Preparación y ejecución **UAT‑1** (incluye carga de datos maestros) y corrección | 8 |
| 5 | Pruebas de cálculo de MP; refinamiento de casos de oro | 3 |
| 6 | Jornada de balanza real, pruebas de reconexión | 4 |
| 7 | **Prueba con datos reales** (paralelo), conciliaciones diarias, ajustes | 10 |
| 8 | UAT‑2, toma de inventario, instalación, cutover, capacitación (7 h de aula), acta | 27 |
| **Total** | | **66** |

Capacitación y soporte inicial pesan en la semana 8; por eso se recomienda que la **preparación de materiales** (manuales cortos con capturas del juego ART‑demo) se haga en semana 7 (incluida en las 10 h de esa semana y en las 27 de la 8).

---

## 3. Plan de UAT con casos de prueba

### 3.1 Reglas del UAT
1. **Quién prueba:** usuario clave + 1 persona de almacén/producción; el desarrollador **observa y anota**, no guía con el mouse.
2. **Dónde:** instalación cliente‑servidor real en la LAN (o B‑mono), base `plasticosart_uat` con datos "ART‑demo" + datos reales que el cliente entregó.
3. **Resultado por caso:** `OK` / `FALLA` / `OBSERVACIÓN`. Severidad de falla según §8.2.
4. **Criterio de aceptación de entrega:** 100 % de casos **críticos** OK; ≥ 95 % del total OK; **0 fallas críticas o altas abiertas**; las medias/bajas quedan en lista con fecha. El cliente tiene **3 días hábiles** (propuesta §5) para firmar o listar objeciones; vencido el plazo sin respuesta se considera aceptada [SUPUESTO a confirmar al firmar].
5. **Acta:** una página con versión, fecha, casos ejecutados, resultado, lista de observaciones, firma del responsable del cliente. Es el documento que respalda el cobro del hito.

### 3.2 Formato de caso
`ID | Módulo | Prioridad (C=crítico, A=alto, M=medio) | Datos de entrada | Pasos resumidos | Resultado esperado`.

### 3.3 UAT‑1 (semana 4): módulos 1–5 + usuarios y permisos (22 casos)

| ID | Mód. | Pr. | Entrada | Resultado esperado |
|---|---|:-:|---|---|
| UAT‑1‑01 | Usuarios | C | Login admin/primer ingreso; contraseña inicial debe cambiarse | Obliga cambio; 6–100 caracteres; no igual al usuario |
| UAT‑1‑02 | Usuarios | C | 5 contraseñas erróneas seguidas en usuario "almacen1" | Bloqueo 5 min; mensaje único "Usuario o contraseña incorrectos"; auditoría sin contraseña |
| UAT‑1‑03 | Permisos | C | Operador de almacén intenta abrir Fórmulas (menú y atajo) | Opción oculta; si se invoca el servicio por prueba directa: `NoAutorizado` y auditado |
| UAT‑1‑04 | Permisos | A | Supervisor entra a Reportes y Órdenes; intenta ajustar inventario | Reportes y órdenes sí; ajuste denegado salvo permiso explícito |
| UAT‑1‑05 | Fórmulas | C | Crear fórmula "Bolsa 30x40": polietileno 8,000 kg, pigmento 0,200 kg, aditivo 0,050 kg por 1 000 unidades | Guarda; código automático; total = 8,250 kg por 1 000 u; auditoría del alta |
| UAT‑1‑06 | Fórmulas | A | Crear fórmula sin líneas / con cantidad 0 / MP duplicada | Rechaza sin líneas y con 0; MP duplicada se avisa y se fusiona o rechaza (según regla definida en semana 1) |
| UAT‑1‑07 | Fórmulas | C | Editar la fórmula (cambia pigmento a 0,250 kg) con una orden abierta ya calculada con la versión 1 | Se crea **versión 2**; la orden abierta sigue mostrando versión 1; órdenes nuevas toman versión 2; el diff queda en auditoría |
| UAT‑1‑08 | Pedidos | C | Pedido: ítem Bolsa 30x40, 5 000 u, fecha requerida mañana+7 | Número correlativo automático; estado "Registrado"; fecha requerida pasada se rechaza |
| UAT‑1‑09 | Pedidos | M | Buscar pedidos por ítem y rango de fechas; editar cantidad de pedido sin orden | Filtra con AND; edición permitida solo sin orden asociada |
| UAT‑1‑10 | Órdenes | C | Convertir el pedido 5 000 u en orden de producción | Orden nace "Emitida", enlazada al pedido; versión de fórmula congelada; no duplica (segunda conversión del mismo pedido total avisa) |
| UAT‑1‑11 | Órdenes | C | Avanzar estados: Emitida→Liberada (S4); EnProduccion→Terminada→Cerrada se prueban en UAT-2 (S5 en adelante) | Solo transiciones válidas; cada cambio con usuario y hora; no se puede volver a "Emitida" desde "Liberada" ni a "Liberada" desde "EnProduccion" |
| UAT‑1‑12 | Órdenes | A | Cancelar una orden Emitida o Liberada con motivo de 3 caracteres, luego con 20 | 3 rechaza (mínimo 5); 20 cancela; la liberación de reserva de MP se verifica en S5; queda auditada; no reversible |
| UAT‑1‑13 | Operarios | A | Alta de operario; asignar a Turno Mañana y a la orden OP‑0001 | Queda asignado; el mismo operario no puede estar en dos turnos con solapamiento el mismo día |
| UAT‑1‑14 | Operarios | M | Desactivar operario con asignaciones futuras | Avisa y pide reasignar; no se borra físicamente |
| UAT‑1‑15 | Inventario | C | Ingreso de 500,000 kg de polietileno, proveedor y documento (nota de ingreso) | Saldo aumenta exactamente 500,000; movimiento "Ingreso" en kardex con usuario, hora y documento |
| UAT‑1‑16 | Inventario | C | Salida manual de 600,000 kg con saldo 500,000 | Rechaza: "Stock insuficiente (saldo 500,000)"; el saldo no cambia |
| UAT‑1‑17 | Inventario | C | Anular el ingreso de 500,000 kg (motivo obligatorio) | Se crea contra‑movimiento; saldo vuelve al anterior; el ingreso original sigue visible como "Anulado"; no se borra |
| UAT‑1‑18 | Inventario | C | Dos usuarios hacen salida de 300,000 kg cada uno al mismo tiempo con saldo 500,000 | Una se registra, la otra es rechazada; saldo final 200,000 (nunca negativo) |
| UAT‑1‑19 | Inventario | A | Ajuste por inventario físico: sistema 200,000, físico 195,500 | Ajuste de −4,500 con motivo y autorización; kardex lo muestra; solo roles autorizados |
| UAT‑1‑20 | Inventario | M | Consultar kardex por MP y rango; exportar a Excel | Saldo corrido correcto fila a fila; Excel con números reales (no texto) |
| UAT‑1‑21 | General | A | Caída del servidor PostgreSQL con clientes abiertos | Los clientes muestran "No hay conexión con el servidor" sin cerrarse ni perder lo no guardado; reconectan al volver el servicio |
| UAT‑1‑22 | General | A | Instalación cliente en un PC limpio apuntando al servidor; abrir con usuario normal de Windows (no administrador) | Instala sin error, abre, conecta; escribe logs y config en su carpeta |

**Casos de aceptación parcial mínima para el hito de semana 4 (40 %):** los críticos (C) aplicables a S4 (UAT‑1‑01, 02, 03, 05, 07 con v1 congelada, 08, 10, 11 hasta Liberada, 15, 16, 17, 18) más UAT‑1‑13 (no doble orden en el mismo turno) en OK. Ver Plan 8.2.

### 3.4 UAT‑2 (semana 8): sistema completo (20 casos + regresión de UAT‑1)

| ID | Mód. | Pr. | Entrada | Resultado esperado |
|---|---|:-:|---|---|
| UAT‑2‑01 | Cálculo MP | C | Caso de oro 1: orden 5 000 u de Bolsa 30x40 (fórmula v1 de UAT‑1‑05) | Necesaria: PE 40,000 kg; pigmento 1,000 kg; aditivo 0,250 kg — idéntico al Excel del usuario clave |
| UAT‑2‑02 | Cálculo MP | C | Stock: PE 38,000 kg, pigmento 5,000, aditivo 0,100 | Muestra **faltante**: PE 2,000 kg; aditivo 0,150 kg; pigmento OK; estado global "Stock insuficiente"; lista imprimible de compras |
| UAT‑2‑03 | Cálculo MP | C | Dos órdenes abiertas que juntas piden más PE que el stock | Segunda orden advierte faltante considerando reserva de la primera |
| UAT‑2‑04 | Cálculo MP | A | Liberar orden con faltante | Bloquea (o exige autorización del supervisor con motivo, según decisión de semana 1); auditado |
| UAT‑2‑05 | Cálculo MP | A | Cambiar cantidad de la orden de 5 000 a 8 000 antes de liberar | Recalcula todo; reserva ajustada; auditoría con diff |
| UAT‑2‑06 | Cálculo MP | M | Orden con fórmula que tiene merma 3 % en una línea | Cantidad incluye merma; redondeo a 3 decimales con regla AwayFromZero |
| UAT‑2‑07 | Consumo | C | Liberar y registrar salida de MP por la orden (consumo teórico) | Salidas en kardex con referencia a la orden; saldo baja exactamente lo calculado |
| UAT‑2‑08 | Balanza | C | Estación de pesaje con balanza real: lote de 25 piezas, peso estable | Muestra peso en vivo; indicador ESTABLE; Capturar solo si estable y > 0; cantidad = peso neto / peso unitario; se grabó con origen "Balanza" |
| UAT‑2‑09 | Balanza | C | Peso inestable (se mueve la pieza) y luego intentar capturar | Botón deshabilitado/aviso "Esperando peso estable"; no se graba |
| UAT‑2‑10 | Balanza | C | Desconectar el cable USB‑serie con la pantalla abierta | Estado "Sin conexión" en < 5 s; captura bloqueada; no se pierde lo digitado; al reconectar (aun cambiando COM) se reanuda tras "Reconectar" |
| UAT‑2‑11 | Balanza | A | Modo manual de respaldo (balanza fuera de servicio) con peso 12,450 kg | Solo con permiso y motivo; registro marcado "MANUAL" y visible en reportes; nunca se mezcla sin marca |
| UAT‑2‑12 | Balanza | A | Peso fuera de rango del producto (tolerancia ± x % del estándar) | Advierte "Fuera de tolerancia"; permite grabar solo con observación del supervisor |
| UAT‑2‑13 | Balanza | A | Operador intenta usar simulador | Denegado; solo administrador; si se graba queda "SIMULADO" en reportes con asterisco |
| UAT‑2‑14 | Cierre | C | Cerrar orden: producida 4 920 u vs. planificada 5 000 y consumo real PE 40,900 kg vs. teórico 40,000 | Exige confirmar diferencias; muestra % de rendimiento y de merma real; estado "Cerrada con diferencias"; no editable; auditoría |
| UAT‑2‑15 | Cierre | A | Cerrar orden sin ninguna pesada ni consumo | Rechaza con mensaje guía |
| UAT‑2‑16 | Reportes | C | Reporte de producción por orden: totales de cantidad y peso | Totales coinciden con la suma manual de las pesadas vigentes (excluye anuladas); formato decimal con coma, peso 1–3 decimales según balanza |
| UAT‑2‑17 | Reportes | A | Reporte por turno y por período (semana); exportar PDF y Excel | Operarios y turnos correctos; PDF legible en A4 con logo; Excel con valores numéricos |
| UAT‑2‑18 | Reportes | A | Reporte de consumo real vs. teórico de MP por orden | Diferencia y % por MP; marca en rojo si supera umbral definido |
| UAT‑2‑19 | Respaldo | C | Ejecutar respaldo, **restaurar en otro PC/BD** y comparar saldos | La BD restaurada tiene el mismo saldo en cada MP y los mismos totales de reportes |
| UAT‑2‑20 | Seguridad | A | Intento de lectura directa de la BD con usuario de aplicación sin permisos DDL; contraseña de BD no visible en logs | La cuenta de aplicación no puede borrar tablas; logs y auditoría sin secretos |

**Regresión:** antes de UAT‑2 se re‑ejecutan UAT‑1‑01, 03, 07, 10, 11, 15, 16, 17, 18, 21 (10 casos clave). 
> Conteo: 22 IDs UAT‑1 + 20 IDs UAT‑2 = **42 casos con ID**; con la regresión se ejecutan 52 pruebas en total.

### 3.5 Casos borde cubiertos (trazabilidad con lo pedido)
Stock insuficiente (1‑16, 2‑02, 2‑03) · fórmula modificada con órdenes abiertas (1‑07) · peso inestable (2‑09) · balanza desconectada (2‑10) · anulación de movimiento (1‑17) · cierre con diferencias (2‑14) · permisos denegados (1‑03, 1‑04, 2‑13) · concurrencia (1‑18) · caída de servidor (1‑21) · restauración (2‑19).

---

## 4. Prueba con datos reales en semana 7

### 4.1 Cómo se organiza
- **Modalidad:** operación **en paralelo** durante 5 días hábiles consecutivos: el método actual (papel/Excel) sigue siendo el registro oficial; el sistema se alimenta con las mismas operaciones por el usuario clave y los operarios ya capacitados en lo básico.
- **Alcance mínimo:** ≥ 10 órdenes de producción reales de ≥ 3 productos distintos; todos los ingresos y salidas de MP de esos días; todas las pesadas en la estación con balanza real; los dos turnos al menos una vez.
- **Rutina diaria (30 min, 16:30):** conciliación del día: (a) saldo del sistema vs. cuaderno/Excel de almacén para las MP movidas; (b) cantidad producida del sistema vs. parte de producción; (c) lista de fricciones del operario. El dev corrige en horario nocturno/mañana; cada corrección se despliega **solo** a las 7:00 o a las 13:00 con aviso, nunca a media jornada.
- **Datos:** carga real de ítems, MP, fórmulas, operarios, turnos y stock inicial **provisional** (la definitiva se hace en semana 8, §5).
- **Bitácora de incidencias** en una hoja compartida (ID, fecha, quién, descripción, severidad, estado).

### 4.2 Criterios de salida (todos deben cumplirse para ir a la semana 8)
| # | Criterio | Meta |
|---|---|---|
| 1 | Conciliación de saldo de MP sistema vs. método actual | ≥ 98 % de las MP movidas con diferencia ≤ 0,5 % o ≤ resolución de la balanza; el resto explicado por escrito |
| 2 | Cantidades producidas vs. parte de producción | Diferencia total ≤ 1 % por orden, sin errores sistemáticos de conversión |
| 3 | Cálculo de MP de cada orden real vs. cálculo manual del usuario clave | 100 % de coincidencia en la cantidad necesaria (tras ajustar la fórmula si estaba mal en la hoja del cliente) |
| 4 | Balanza real | ≥ 99 % de las pesadas con peso estable capturado a la primera/segunda; 0 pérdidas de datos; reconexiones espontáneas ≤ 1 por jornada y sin pérdida |
| 5 | Fallas abiertas | 0 críticas, 0 altas; ≤ 5 medias con plan |
| 6 | Rendimiento | Apertura de pantallas ≤ 2 s; reporte semanal ≤ 5 s; grabar pesada ≤ 1 s |
| 7 | Aceptación del usuario | Usuario clave y supervisor declaran por escrito que "pueden trabajar sin el papel" para al menos 2 de 3 flujos (pedido→orden, inventario, pesaje) |

Si el criterio 1, 2 o 3 falla: **se amplía el paralelo** unos días (hay 1 semana de colchón dentro de la semana 8 solo en el caso de que el cliente haya cumplido sus tiempos; ver riesgo R‑01) y se avisa al cliente por escrito que el plazo depende de la colaboración (propuesta §5).

---

## 5. Carga inicial de datos y stock inicial

### 5.1 Datos maestros (semanas 1–4)
| Dato | Fuente [CLIENTE] | Plantilla | Validación |
|---|---|---|---|
| Ítems (productos) | Lista de productos con unidad y peso unitario estándar | Excel con columnas fijas, hoja "Items" | Sin duplicados (código y nombre), peso estándar > 0 |
| Materias primas | Lista con unidad (kg), mínimo/máximo, costo opcional | Hoja "MateriasPrimas" | Unidad única; sin duplicados |
| Fórmulas | Recetas por ítem | Hoja "Formulas" (ítem, MP, cantidad, base) | Suma total razonable (± 15 % del peso del producto × base), las MP existen |
| Operarios y turnos | RRHH/Producción | Hojas "Operarios", "Turnos" | Sin duplicados; turnos sin solapes de horario |
| Usuarios | Responsable del cliente | Hoja "Usuarios" (nombre, rol) | Rol válido; contraseñas temporales que se obliga a cambiar |

Se reutiliza el **importador CSV de Pesalab** (simulacro + informe, lotes de 1 000) adaptado a estas entidades: primero se corre en modo **simulacro**, se revisa el informe de errores con el usuario clave y luego se importa. Estimado: 8 h dentro de la semana 8 más lo de semana 4 ya computado en las 8 h de UAT‑1.

### 5.2 Stock inicial (día del arranque)
Procedimiento **"Toma de inventario de arranque"** (sábado o domingo previo al cutover, o el lunes antes de las 8:00 con la planta detenida):
1. **Congelar movimientos** de MP desde las 18:00 del día anterior (nada entra ni sale sin registro manual).
2. **Conteo físico por dos personas** (almacén + producción) con balanza de piso/plataforma del cliente; plantilla de conteo impresa con MP, ubicación, unidad, columna "conteo 1", "conteo 2", "diferencia".
3. **Regla de reconteo:** si conteo 1 y conteo 2 difieren > 1 % o > 2 kg, se reconteo por el supervisor.
4. **Pesar sacos abiertos/parciales** y registrar el neto (no estimar).
5. **Carga en el sistema** como un único movimiento de tipo **"Saldo inicial"** por MP (bloqueado después: solo ajustes con permiso), con fecha-hora del conteo.
6. **Conciliación:** el usuario clave firma la hoja de conteo y el reporte "Stock inicial" impreso; las diferencias contra el cuaderno anterior se documentan, **no** se "arreglan" en el sistema.
7. **Respaldo** inmediato (`pg_dump`) etiquetado `saldo_inicial_AAAA-MM-DD`; ese es el punto de reversa.

### 5.3 Validación posterior
- Día +1 y día +5: el almacén cuenta al azar 5 MP (las de mayor movimiento) y se compara con el saldo del sistema; tolerancia ≤ 0,5 % o ≤ 1 división de la balanza.
- Si ≥ 2 de 5 fallan, se auditan los movimientos del kardex antes de seguir y se corrige con ajustes documentados.

---

## 6. Instalación, puesta en marcha y corte (semana 8)

### 6.1 Checklist de go‑live

| # | Verificación | Quién | Evidencia |
|---|---|---|---|
| 1 | PC servidor: Windows con actualizaciones, IP fija o reserva DHCP, nombre fijo, sin hibernación, **UPS conectado** | Dev + cliente | Foto/ipconfig |
| 2 | PostgreSQL 16 instalado; servicio automático; puerto 5432 solo abierto a la LAN (firewall) | Dev | Captura del servicio |
| 3 | `pg_hba.conf` restringido a la subred; cuenta de aplicación sin DDL; cuenta de migración separada | Dev | Revisión |
| 4 | Migraciones aplicadas; `has-pending-model-changes` limpio | Dev | Log |
| 5 | Respaldo programado diario (tarea programada, `pg_dump` comprimido) + copia a disco externo/segundo PC; **prueba de restauración hecha** | Dev + admin de respaldo [CLIENTE] | Acta de restauración |
| 6 | Cada PC cliente instalado (almacén, producción, supervisión, gerencia); acceso directo; conexión OK | Dev | Planilla de PCs |
| 7 | Estación de pesaje: COM fijo en el Administrador de dispositivos, balanza leyendo, configuración **por equipo** guardada | Dev | Captura de prueba de balanza |
| 8 | Impresora y reportes PDF/Excel probados desde cada puesto con salida | Dev | Muestras |
| 9 | Usuarios creados con roles; contraseña `admin` inicial cambiada; contraseñas temporales entregadas en mano | Responsable del cliente | Lista firmada |
| 10 | Stock inicial cargado y firmado (§5.2) | Usuario clave | Acta de inventario |
| 11 | Manuales cortos entregados (3 por rol) y hoja de teclas/atajos | Dev | Entrega |
| 12 | Licencia/activación (si se aplica el mecanismo de Pesalab) y versión estable congelada | Dev | `Ayuda › Acerca de` |
| 13 | Respaldo "pre‑go‑live" tomado | Dev | Archivo + hash |
| 14 | Plan de reversa leído y firmado por el responsable | Responsable | Acta |

### 6.2 Plan de corte (cutover) y paralelo

```mermaid
flowchart LR
  A[Sem 7: paralelo de prueba] --> B[Vie sem 8: UAT-2 firmado]
  B --> C[Sáb: toma de inventario y carga de saldos]
  C --> D[Lun: ARRANQUE oficial en sistema]
  D --> E[Lun-Vie: paralelo con papel solo para consulta]
  E --> F[Vie: decisión de apagar el papel]
  F --> G[Soporte intensivo: 90 días]
```

- **Día D (lunes):** el dev está en planta de 6:30 a 17:00 [SUPUESTO: la fábrica opera en horario de oficina y 1–2 turnos]. El sistema es el registro **oficial**; el cuaderno se mantiene **solo** como respaldo en papel las 5 primeras jornadas.
- **Paralelo 5 días hábiles:** el usuario clave concilia cada tarde saldos de MP y producido contra el papel (10 min).
- **Decisión de "apagar el papel":** viernes de la semana de cutover, si la conciliación de los 5 días cumple los criterios 1–3 de §4.2.

### 6.3 Plan de reversa (rollback)
| Disparador | Acción | Tiempo | Pérdida |
|---|---|---|---|
| Falla crítica no resuelta en 4 h laborables durante la primera semana | Volver al método anterior; los movimientos del sistema se transcriben al cuaderno desde el reporte de kardex del día | ≤ 1 h | Ninguna: todos los datos están en el sistema |
| BD corrupta o servidor perdido | Restaurar el último `pg_dump` (máx. 24 h, o el de cierre de turno) en el PC servidor o en un PC de contingencia; clientes ya apuntan por nombre | ≤ 2 h | Hasta el último respaldo; el papel cubre el hueco |
| Balanza fuera de servicio | Modo manual con motivo (§3.4 UAT‑2‑11) o pesar con balanza auxiliar y transcribir | Inmediato | Ninguna |
| Decisión del cliente de no seguir | El cliente conserva la BD y exporta kardex a Excel | — | Ninguna |

**Ventana de reversa:** 10 días hábiles. Después, la reversa deja de ser realista por volumen de datos y se trabaja con correcciones.

### 6.4 Plan de contingencia sin servidor (B‑mono)
Si el cliente solo tiene un PC: servidor y estación de pesaje en el mismo equipo; riesgo de que se apague. Mitigación: UPS, respaldo diario a USB, y recomendación escrita de comprar un PC servidor básico cuando sea posible.

---

## 7. Capacitación por rol

### 7.1 Principios
- Pocas personas, mucha práctica, en el puesto real y con el **juego ART‑demo** (BD de capacitación, no producción).
- Un **usuario clave** (supervisor o jefe de planta) recibe 3 h extra para ser el primer nivel de soporte.
- Cada sesión termina con **evaluación práctica** de 15 min (lista de verificación, §7.3).

### 7.2 Agenda (semana 8; durante semana 7 se avanza la de operarios básicos con los casos reales)

| Sesión | Rol / público | Duración | Contenido | Material |
|---|---|---:|---|---|
| S1 | **Gerencia / administración** (1–3 personas) | 1,5 h | Qué muestra cada reporte, cómo leer rendimiento y merma, exportar, qué NO hacer; usuarios y respaldos conceptual | Guía de 4 páginas + hoja de reportes |
| S2 | **Planificación / producción** (jefe, supervisores) | 2,5 h | Fórmulas (alta, versión), pedidos, órdenes, estados, cálculo de MP y faltantes, cierre con diferencias, anulaciones | Manual de 12 páginas con capturas + hoja de estados |
| S3 | **Almacén** | 2 h | Ingresos, salidas, ajustes, kardex, anulación de movimientos, toma de inventario, saldo inicial | Manual de 8 páginas + plantilla de conteo |
| S4 | **Operarios / estación de pesaje** | 2 h | Turnos, asignación, pesaje paso a paso (estable → capturar), qué hacer si se desconecta la balanza, modo manual, buenas prácticas | Tarjeta plastificada de 1 página junto a la balanza + manual de 6 páginas |
| S5 | **Usuario clave** | 3 h | Respaldo y restauración, crear usuarios, desbloquear, ver logs y auditoría, reconectar balanza, a quién y cómo reportar incidencias | Guía de soporte de nivel 1 de 6 páginas |
| **Total** | | **11 h** de aula (el dev dicta 7,5 h para un mismo público de ≤ 8 personas por sesión; S5 y S1 son reducidas) | | |

> **Resumen:** 4 sesiones por rol básicas + la del usuario clave. En horas del dev: preparación 5 h (materiales, datos demo) + dictado ≈ 7 h dentro de las 66 h (el resto se solapa con la jornada de go‑live).

### 7.3 Evaluación práctica (lista de verificación; aprobado = 100 % en las marcadas ★)

| Rol | Tareas que debe ejecutar solo |
|---|---|
| Operario | ★ Iniciar sesión · ★ Elegir su orden · ★ Pesar 3 lotes con balanza real (estable) · ★ Reconocer "Sin conexión" y avisar · Cambiar su contraseña |
| Almacén | ★ Registrar ingreso · ★ Hacer salida con stock insuficiente y entender el aviso · ★ Anular un movimiento con motivo · Hacer un ajuste con permiso · Leer el kardex |
| Planificación | ★ Crear fórmula · ★ Crear pedido y convertirlo en orden · ★ Ver faltante de MP y decidir · ★ Cerrar una orden con diferencias · Anular orden |
| Gerencia | ★ Abrir 3 reportes y exportar PDF/Excel · Interpretar % de merma |
| Usuario clave | ★ Restaurar respaldo en BD de capacitación · ★ Crear y desbloquear usuario · ★ Reconectar balanza y verificar COM · Registrar incidencia con captura de pantalla |

Quien no aprueba repite la parte fallida en el puesto durante el paralelo. Registro de asistencia y resultado firmado por el responsable del cliente (parte del acta final).

---

## 8. Soporte de 90 días (hipercuidado)

### 8.1 Canales y horario [SUPUESTO — a confirmar]
| Canal | Uso | Horario |
|---|---|---|
| WhatsApp/teléfono del desarrollador | Críticos y avisos | Lun–vie 8:00–18:00; sáb 8:00–12:00 solo críticos si la fábrica opera |
| Correo / formulario de incidencias (hoja compartida) | Resto: medios, bajos, consultas | Respuesta en horario hábil |
| Visita en sitio | 1 visita semanal las 2 primeras semanas tras el go‑live, luego 1 cada 2 semanas (4 h c/u) | Acordada |
| Acceso remoto (VPN/escritorio remoto) | Diagnóstico sin visita, **solo con autorización del cliente** | Mismo horario |

**Un solo desarrollador implica:** vacaciones y enfermedad son un riesgo; se informa al cliente de **un ventana de respaldo** (persona de apoyo opcional) o de plazos mayores en esos días.

### 8.2 Clasificación y tiempos (horario hábil; realista para 1 persona)

| Severidad | Definición | Ejemplo | Primera respuesta | Meta de solución / alternativa |
|---|---|---|---|---|
| **Crítica (S1)** | El sistema no se puede usar o hay pérdida/corrupción de datos o saldos | Servidor caído; no abre ningún cliente; saldos mal calculados masivamente | **2 h** | Solución o alternativa (reversa, modo manual, restauración) ≤ **8 h hábiles** |
| **Alta (S2)** | Una función principal falla, con alternativa temporal | Balanza no lee en la estación (modo manual disponible); cierre de orden da error | **4 h** | ≤ **2 días hábiles** |
| **Media (S3)** | Error que no bloquea; reporte con formato incorrecto | Columna cortada, total mal redondeado sin afectar saldos | 1 día hábil | ≤ **5 días hábiles** (en la visita/versión siguiente) |
| **Baja (S4)** | Estético, mejora de texto o consulta de uso | Texto de ayuda, tamaño de letra | 2 días hábiles | Agrupadas en la versión de cierre |

Las respuestas fuera de horario para S1 son "mejor esfuerzo".

### 8.3 Qué es corrección incluida y qué es cambio (previene disputas)

| Incluido (corrección) | No incluido (cambio / adicional cotizable) |
|---|---|
| El sistema no hace lo descrito en el documento de procesos de la semana 1 o falla en un caso de UAT aceptado | Nuevos campos, nuevos reportes o módulos no listados |
| Errores de cálculo, de permisos, de interfaz o de integración de balanza (con la misma balanza validada) | Cambiar la fórmula de cálculo o los procesos **ya validados** en semana 1 |
| Reconfiguración de balanza por cambio de puerto, reinstalación de un PC cliente | Integrar **otra** balanza o un protocolo distinto al validado |
| Reinstalar o restaurar tras fallas, con sus respaldos | Reparar daños por apagados, virus, borrado manual de BD o mala operación del servidor |
| Capacitación de refuerzo hasta 2 h por rol, una vez | Nueva capacitación completa, o nuevos usuarios en masa |
| Ajustes menores de textos y reportes existentes ≤ 2 h por solicitud | Visor web, app móvil, integraciones contables, hosting |
**Regla:** el desarrollador estima cualquier cambio por escrito (horas y costo) **antes** de ejecutarlo. Los gastos de terceros (hosting, dominios, licencias, hardware) los paga el cliente (propuesta §4).

### 8.4 Presupuesto de horas del soporte
≈ 24 h en 90 días: 4–6 visitas, 2 h/semana de atención remota promedio las primeras 4 semanas y 1 h/semana después. Si el consumo promedio excede 150 % se avisa al cliente con registro de causas (suelen ser cambios disfrazados de fallas).

### 8.5 Entregables de cierre (día 90)
1. **Versión estable** (etiqueta/versión y hash del instalador), con CHANGELOG de lo corregido.
2. **Manuales finales actualizados** (por rol + guía de balanza + guía de instalación y soporte).
3. **Respaldo final completo** (BD + configuración + logs) entregado en disco y verificado con una restauración de prueba.
4. **Informe de soporte:** incidencias por severidad, tiempos reales de respuesta, lista de pendientes de baja prioridad o mejoras sugeridas.
5. **Acta de cierre** firmada por el responsable (traspaso de contraseñas administrativas, de la clave de instalación y de la responsabilidad de backups al administrador del cliente).
6. Oferta (opcional) de soporte continuo mensual, sin obligación.

---

## 9. Riesgos de calidad e implantación

| ID | Riesgo | Prob. | Impacto | Mitigación | Dueño |
|---|---|:-:|:-:|---|---|
| R‑01 | El cliente no entrega fórmulas/datos o no revisa en 3 días → no hay datos reales a tiempo para semana 7 | Alta | Alto | Fechas de entrega de datos en el acta de semana 1; plantillas Excel listas; avisar por escrito cada demora (la propuesta amplía el plazo); trabajar con datos demo mientras tanto | Dev + responsable |
| R‑02 | Fórmulas "de la cabeza" del operario, inconsistentes o sin unidades | Alta | Alto | Sesión de validación de cada fórmula con el jefe de producción antes de cargarla; firma de cada receta; cálculo de oro en Excel | Dev + jefe producción |
| R‑03 | Protocolo de balanza desconocido o balanza solo con display (sin salida de datos) | Media | Alto | Checklist semana 1; simulador + modo manual desde el inicio; si no hay salida, se avisa de inmediato y se propone adaptador/otro indicador (costo del cliente) | Dev |
| R‑04 | Adaptador USB‑serie (CH340) cambia de COM o el driver rechaza configuración (lección de Pesalab) | Media | Medio | Reutilizar `PuertoSerieWin32`; fijar COM; guía de balanza con la lección; guía para el usuario clave | Dev |
| R‑05 | Resistencia al cambio: operarios siguen en papel; datos incompletos | Alta | Alto | Capacitación en puesto; usuario clave; paralelo corto; apoyo del gerente; tarjetas junto a la balanza; reportes que se usan en reuniones | Gerencia |
| R‑06 | Inventario inicial incorrecto desde el día 1 (error de conteo) | Media | Alto | Doble conteo, pesar sacos abiertos, firma del usuario clave, validación día +1 y +5 | Almacén |
| R‑07 | Concurrencia: dos usuarios descuentan la misma MP | Media | Alto | Transacciones con bloqueo de fila/`SERIALIZABLE` solo en saldo; prueba UAT‑1‑18 y prueba automatizada | Dev |
| R‑08 | Cambio de procesos tras semana 1 (scope creep) | Alta | Alto | Documento de procesos firmado; lista de cambios con horas; ampliación de plazo según propuesta §5 | Dev + responsable |
| R‑09 | Un solo desarrollador enferma o se retrasa (incluida la semana 8, cargada: UAT‑2 + inventario + instalación + capacitación en 5 días) | Media | Alto | Mover capacitación de operarios básica a la semana 7; automatizar instalador desde semana 5; ensayo de instalación completo en PC de pruebas en semana 7; persona de respaldo opcional (contratación puntual 10–15 h) | Dev |
| R‑10 | Corrupción/pérdida de BD por corte eléctrico o servidor sin UPS | Media | Alto | UPS obligatorio (en la lista del cliente), WAL/`fsync` de PostgreSQL por defecto, respaldo diario + copia externa, restauración probada | Cliente + dev |
| R‑11 | El responsable de backups del cliente no existe o no los verifica | Media | Alto | Asignar nombre y apellido en el acta; recordatorio mensual; alerta visible en el sistema si el último respaldo tiene > 48 h; prueba de restauración trimestral en el soporte | Cliente |
| R‑12 | UAT‑1 se retrasa y el 40 % del pago se difiere | Media | Medio | Dos revisiones previas (internas) en semana 3–4; casos críticos definidos antes; entrega parcial con alcance reducido pero estable | Dev |
| R‑13 | Windows del cliente sin permisos de usuario estándar, antivirus bloquea el instalador sin firma | Media | Medio | Instalador por equipo con permisos como en Pesalab; excepción en antivirus por escrito; evaluar certificado de firma (costo del cliente) | Dev |
| R‑14 | Peso real fuera de tolerancia porque el peso unitario estándar fue mal definido | Media | Medio | Calibración del estándar con muestreo (30 piezas) en semana 6–7, antes de fijar tolerancias | Producción |
| R‑15 | Expectativa de acceso web/remoto por el "hosting" de la propuesta | Media | Medio | Aclarar por escrito en semana 1 (P1 del Anexo 01); VPN como alternativa | Dev + gerencia |

---

## 10. Preguntas abiertas para el cliente

1. ¿Quién es el responsable con autoridad y quién será el **usuario clave**? ¿Quién administrará los respaldos?
2. ¿La fábrica opera 1, 2 o 3 turnos y en qué días? ¿Hay un día de baja actividad para la toma de inventario de arranque?
3. ¿La liberación de una orden con faltante de MP se **bloquea** o se permite con autorización del supervisor?
4. ¿Cuál es la tolerancia aceptada (± %) entre peso real y estándar por ítem? ¿Se pesa producto terminado, MP o ambos (P5 del Anexo 01)?
5. ¿Cuántos PCs y estaciones de pesaje, y existe PC servidor 24/7 (P2)? ¿Habrá Wi‑Fi en algún puesto (P8)?
6. ¿Marca, modelo, puertos y manual de la balanza (P4)? ¿Dan acceso a ella en semana 1 y para una jornada en semana 6?
7. ¿Existen datos históricos (Excel/Access) a importar (P9)? ¿Qué antigüedad?
8. ¿El plazo de 3 días hábiles de revisión se acepta como "aprobación tácita" si no hay respuesta?
9. ¿Dónde se puede dejar a un PC de contingencia para restauración si el servidor falla?
10. ¿Aceptan el soporte remoto por VPN/escritorio remoto y bajo qué reglas de seguridad?

---

## 11. Estimación de horas (resumen)

| Bloque | Horas |
|---|---:|
| Plan de pruebas, datos demo, infraestructura de pruebas (semana 1) | 8 |
| Pruebas automáticas junto al desarrollo (semanas 2–3, 5) | 9 |
| UAT‑1 + carga de datos maestros (semana 4) | 8 |
| Jornada de balanza real (semana 6) | 4 |
| Paralelo con datos reales y conciliación (semana 7) | 10 |
| UAT‑2, inventario, instalación, cutover, capacitación, acta (semana 8) | 27 |
| **Total dentro de las 8 semanas** | **66** |
| Soporte de 90 días (fuera de las 8 semanas, incluido en el precio) | ≈ 24 |

> Nota: las pruebas automáticas de los módulos (35–40 h del código de prueba) están **dentro** de las horas de desarrollo de cada módulo (regla "no hay código sin prueba"), no duplicadas aquí; este anexo solo cuenta el esfuerzo específico de QA/implantación. Con un solo desarrollador, las 66 h compiten con las horas de construcción: el calendario del Anexo de plan debe reservarlas explícitamente.
