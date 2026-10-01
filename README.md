# Sistema de Pesaje para Faeneo

Sistema de control, registro de pesaje y emisión de guías de remisión de carne bovina para faeneo. Desarrollado en .NET 8 (Windows Forms) con SQLite y Entity Framework Core.

## Estructura de la Solución (`src/PesajeFaeneo.sln`)

- **PesajeFaeneo.Domain**: Núcleo de la aplicación. Entidades de negocio, enums, excepciones y reglas puras. Sin dependencias externas ni paquetes NuGet.
- **PesajeFaeneo.Application**: Casos de uso, interfaces, DTOs, modelo puro de boleta y validaciones de aplicación.
- **PesajeFaeneo.Infrastructure**: Acceso a datos con EF Core (SQLite), comunicación con balanzas (Serial/TCP/Simulador), almacenamiento JSON por equipo, exportación de reportes y logging.
- **PesajeFaeneo.WinForms**: Capa de presentación WinForms, adaptadores de interfaz de usuario, impresión GDI+ (`WinForms/Impresion`) y manejo de navegación.
- **PesajeFaeneo.Tests**: Batería de pruebas unitarias y de integración sobre SQLite en memoria y archivos temporales.

## Compilación y Ejecución

### Prerrequisitos
- .NET 8 SDK (o .NET 10 SDK con soporte para net8.0)
- Herramienta `dotnet-ef` instalada globalmente:
  ```bash
  dotnet tool install --global dotnet-ef
  ```

### Restaurar y Compilar
```powershell
dotnet restore src/PesajeFaeneo.sln
dotnet build src/PesajeFaeneo.sln -c Release
```

### Ejecutar Pruebas
```powershell
dotnet test src/PesajeFaeneo.sln --logger console;verbosity=normal
```

O mediante el script automatizado:
```powershell
.\build.ps1
```

### Migraciones con EF Core
Para agregar una nueva migración:
```powershell
dotnet ef migrations add <NombreMigracion> -p src/PesajeFaeneo.Infrastructure -s src/PesajeFaeneo.WinForms -o Persistencia/Migraciones
```

Para actualizar la base de datos de desarrollo manualmente:
```powershell
dotnet ef database update -p src/PesajeFaeneo.Infrastructure -s src/PesajeFaeneo.WinForms
```

Este comando (igual que la aplicación en producción) crea la base de datos SQLite en
`%ProgramData%\PesajeFaeneo\pesaje.db` (junto con `logs`, `logo`, `backups` y `restore` en la misma
carpeta), no en el directorio del repositorio. `DesignTimeDbContextFactory` crea esa carpeta si no
existe. Para partir de una base de datos limpia en desarrollo, basta con borrar
`%ProgramData%\PesajeFaeneo\pesaje.db*`.

## Primer Ingreso al Sistema

Al iniciar la aplicación por primera vez sobre una base de datos recién creada:
- **Usuario inicial:** `admin`
- **Contraseña inicial:** `admin`

Por motivos de seguridad, la cuenta administradora inicial tiene activada la marca `DebeCambiarPassword`. En consecuencia:
1. El sistema solicitará obligatoriamente cambiar la contraseña antes de permitir el acceso al menú principal.
2. La nueva contraseña no puede ser idéntica a la anterior (`admin`) y debe cumplir con la longitud mínima requerida por las políticas de seguridad (mínimo 6 caracteres).
3. Tras confirmar el cambio, la sesión continuará hacia la pantalla principal con el rol de Administrador.

## Catálogos y Parámetros

- **Catálogos de Ganaderos, Clientes y Fleteros:**
  - Persistencia normalizada en mayúsculas invariantes (`ToUpperInvariant`) y colapso de espacios intermedios.
  - Validación estricta con restricciones CHECK a nivel de SQLite (longitud de marca de 1 a 5 caracteres, código de cliente entre 1 y 999.999).
  - Reglas de unicidad y de negocio: reactivación sugerida al intentar dar de alta una clave existente inactiva; máximo de 4 fleteros activos (F1 a F4).
  - Componente de búsqueda y autocompletado en memoria con ranking de coincidencia multinivel, tolerante a acentos y caracteres especiales (plegado Unicode FormD).
- **Parámetros del Sistema y Logo:**
  - Configuración de datos fiscales y operativos de la empresa, y control del siguiente número de boleta.
  - Almacenamiento seguro del logo en `%ProgramData%\PesajeFaeneo\logo` mediante nombre de archivo relativo y validación binaria de firma de imagen (PNG, JPEG, BMP).
  - Carga desacoplada de archivos para visualización sin bloquear el archivo en el sistema operativo.

## Balanza e Integración de Pesaje

- **Tipos de conexión soportados:**
  - **Serie (RS-232 / USB):** acceso directo a la API serie de Windows (no `System.IO.Ports`) que respeta la configuración del driver y solo cambia baudios/formato cuando difieren; necesario para adaptadores USB-serie CH340, que rechazan la reconfiguración completa que hace `SerialPort.Open()`. Lectura en hilo dedicado (puertos COM1 a COM999, baudios 1200 a 115200, paridad y bits de parada).
  - **Red (TCP/IP):** cliente socket TCP asíncrono para indicadores con puerto Ethernet o conversores serie-Ethernet.
  - **Simulador:** emulador interno para pruebas y desarrollo con curva amortiguada de peso hacia objetivo y simulación de desconexión.
- **Configuración y Diagnóstico:**
  - Accesible desde el menú **Administración › Configuración de balanza** (requiere permiso `ConfigurarBalanza` / rol Administrador).
  - Herramienta de diagnóstico interactivo integrada ("Probar conexión") que suspende temporalmente el bucle de producción sin desconectar la aplicación.
- **Patrón de tramas (Regex):**
  - Patrón por defecto sembrado: `^[^-+\d]*(?<peso>[-+]?\s*\d+(?:[.,]\d+)?)` (sin grupo `estable`: la estabilidad se deduce por dispersión).
  - **Separación de tramas:** fin de línea (CR/LF, por defecto), espacio, o inicio con `=` (indicadores RX8 Accurex / XK3190, que transmiten `=NNNNNN` sin CR/LF).
  - **Unidad que transmite:** kilogramos (por defecto) o gramos (se convierte a kg antes de redondear a 0,1 kg).
  - Configuración verificada en sitio para RX8 Accurex con adaptador CH340: Serie, 9600 8N1, inicio con `=`, gramos.
  - Debe contener obligatoriamente el grupo con nombre `(?<peso>...)` y opcionalmente `(?<estable>...)` si la balanza envía banderas de estabilidad (p. ej. `ST` para estable, `US` para inestable según el formato estándar P1).
  - Admite tanto coma `,` como punto `.` decimal (la coma se normaliza internamente a decimal).
- **Indicador en barra de estado:**
  - La barra inferior muestra el estado de conexión de la balanza en tiempo real y el peso actual con código de color (verde estable, rojo error/desconexión).
  - Cuando el tipo de conexión activo es **Simulador**, la barra de estado destaca el indicador con prefijo `[SIMULADOR]` y fondo naranja de advertencia.

## Registro de Pesaje

- **Acceso rápido:** Tecla `F2` desde la pantalla principal o menú **Pesaje › Registrar pesaje** (requiere permiso `Pesar`).
- **Display en vivo:** Panel superior con visualización de peso vivo en tiempo real (fuente 48 pt negrita), indicador de estabilidad ("ESTABLE" / "INESTABLE"), estado de conexión de la balanza y próxima boleta a emitir.
- **Tipos de desposte:**
  - **Res Completa:** Requiere capturar ambos lados (izquierdo y derecho). Entre la captura del primer lado y del segundo, la balanza debe descender obligatoriamente a cero (peso ≤ 2,0 kg) para impedir capturar dos veces el mismo cuarto.
  - **Media Res:** Requiere capturar un único lado (izquierdo o derecho).
- **Rangos de peso válidos:** 10,0 a 600,0 kg por lado. Total de 10,0 a 1200,0 kg para Res Completa y 10,0 a 600,0 kg para Media Res. Resolución de 0,1 kg con redondeo estándar (`AwayFromZero`).
- **Atajos de teclado y flujo operativo:**
  - `F5`: Capturar lado izquierdo (solo si la balanza está conectada, estable y dentro de rango).
  - `F6`: Capturar lado derecho.
  - `F8`: Borrar pesos capturados para reiniciar la pesada del animal.
  - `F12`: Grabar pesaje (habilitado únicamente cuando los datos requeridos y los pesos están completos).
  - `Enter`: Navegación secuencial entre los campos de datos (ganadero → secuencia → cliente → dirección → color/raza → sexo → desposte → observación). En los buscadores de catálogo, `Enter` autoselecciona cuando hay una única coincidencia.
  - `Escape`: Cerrar la pantalla de pesaje (con advertencia si existen pesos capturados sin grabar).
- **Numeración atómica garantizada:** La asignación del número de boleta se realiza atómicamente a nivel de base de datos (`UPDATE ... RETURNING`) en la misma transacción que el insert del pesaje y su auditoría, garantizando unicidad y continuidad sin huecos ni carreras.
- **Sugerencia de secuencia:** Al seleccionar un ganadero, el sistema sugiere automáticamente el siguiente número de secuencia según los pesajes del día para dicho ganadero (iniciando en 1 cada nuevo día).
- **Alta rápida de ganaderos y clientes:** Si el texto tecleado en el buscador no existe en el catálogo, al pulsar `Enter` se abre directamente el diálogo de alta rápida (prellenando marca/código y nombre) bajo el permiso `AltaRapidaCatalogos`. Tras guardar, el nuevo registro se selecciona inmediatamente en la pantalla de pesaje.
- **Política de balanza en modo simulador:**
  - Cuando la balanza está configurada en modo **Simulador**, únicamente los administradores (`ConfigurarBalanza`) tienen permitido capturar y grabar pesajes.
  - Para los operadores y supervisores, los botones de captura y grabado se bloquean con mensaje guía de advertencia y el panel de control del simulador permanece oculto.
  - Todo pesaje grabado en este modo queda marcado con origen `SIMULADO` en base de datos, en la grilla del día y en los registros de auditoría.

## Boleta de Pesaje e Impresión

- **Formatos soportados:**
  - **Media Carta (216 × 140 mm):** Diseño en dos columnas para impresoras matriciales, láser o de inyección de tinta, optimizado para formularios horizontales. Incluye encabezado con datos fiscales de la empresa y logo opcional, fecha/hora local, correlativo de boleta, datos del ganadero (marca y nombre), cliente, fleteros (F1 a F4), detalle de pesos (lado izquierdo, lado derecho y total en kg) y observaciones.
  - **Ticket 80 mm:** Diseño continuo en una sola columna para impresoras térmicas de punto de venta.
- **Configuración por equipo:**
  - Accesible desde el menú **Administración › Configuración de impresión** (requiere rol Administrador / permiso `GestionarParametros`).
  - Almacenamiento independiente por equipo en `%ProgramData%\PesajeFaeneo\config\impresion.json` con guardado atómico (archivo temporal + reemplazo atómico) y vinculado al nombre del equipo (`Environment.MachineName`). Si la base de datos se migra o restaura en otra máquina, la configuración de hardware local no se ve afectada.
  - Selección de impresora de Windows (o uso de la impresora predeterminada).
  - Detección de impresoras virtuales ("Microsoft Print to PDF", "XPS Document Writer", "Fax"): muestra advertencia preventiva recomendando desactivar la impresión automática para evitar bloqueos por cuadros de diálogo modales.
  - Manejo robusto de impresoras no instaladas o apagadas: preserva el nombre configurado (`(NO INSTALADA)`) sin sustituciones silenciosas ni fallos de carga.
- **Calibración y ajuste fino:**
  - Función "Imprimir página de calibración": genera un recuadro de prueba a exactamente 10,0 mm de cada borde físico del papel para medir desviaciones del driver o del arrastre mecánico.
  - Parámetros de desplazamiento horizontal (X) y vertical (Y) en milímetros (rango de -20,0 a +20,0 mm con paso de 0,5 mm) para alinear milimétricamente la impresión con formularios preimpresos.
  - Funciones de "Vista previa (muestra)" e "Imprimir muestra" que renderizan una boleta ficticia de prueba con la leyenda `MUESTRA — SIN VALOR` sin afectar la numeración de boletas ni la base de datos.
- **Impresión desde la pantalla de pesaje:**
  - **Impresión automática al grabar:** Opción configurable por equipo. Al presionar `F12` (Grabar), el pesaje se persiste atómicamente y a continuación se dispara la impresión sin bloquear el hilo principal. Si la impresora está sin papel o apagada, el pesaje queda grabado de forma segura y el panel guía informa el error en rojo indicando `Pulse F9 para reintentar`.
  - **Impresión manual (F9):** Permite imprimir en cualquier momento la boleta seleccionada en la grilla del día pulsando la tecla `F9` o el botón "Imprimir (F9)".
- **Reimpresión de boletas:**
  - Acceso directo mediante la tecla `F3` desde la pantalla principal o menú **Boletas › Reimprimir boleta** (disponible para Operador, Supervisor y Administrador).
  - Búsqueda ágil por número de boleta (prellenado por defecto con la última boleta emitida). Si se cambia el número, el resumen se limpia y F4/F9 quedan deshabilitados hasta volver a buscar con `Enter`: nunca se imprime una boleta distinta de la que muestra el campo.
  - Visualización del resumen completo de la boleta, estado de vigencia, procedencia (Balanza o Simulador) y conteo de impresiones previas.
  - Vista previa en pantalla (`F4`) mediante visor seguro que no permite imprimir directamente para prevenir elusiones de auditoría.
  - Botón de reimpresión (`F9`) con auditoría atómica.
- **Leyendas y seguridad antifraude:**
  - **ORIGINAL / COPIA:** Impresión configurable de 1 a 3 copias físicas independientes. La primera copia se rotula como `ORIGINAL` y las subsiguientes como `COPIA 1`, `COPIA 2`.
  - **REIMPRESIÓN:** Todo documento impreso después de la primera emisión incluye en el encabezado la leyenda indeleble `REIMPRESIÓN {n} — {fecha/hora local}`.
  - **PESO SIMULADO:** Toda boleta cuyo origen sea el simulador de balanza lleva en la cabecera `PESO SIMULADO — SIN VALOR COMERCIAL` y, en media carta, una marca de agua gris `SIMULADO` sobre los pesos (en ticket, la línea `*** SIMULADO ***`).
  - **BOLETA ANULADA:** En boletas anuladas se estampa en letras mayúsculas la leyenda `ANULADA` y el motivo de la anulación.
  - Las leyendas se acumulan (una boleta anulada de peso simulado lleva ambas) en el orden MUESTRA, ANULADA, PESO SIMULADO, IMPORTADA, REIMPRESIÓN, ORIGINAL/COPIA; solo la marca de agua muestra una (la de mayor prioridad).
- **Auditoría completa:**
  - Cada impresión física genera un registro en la tabla `Auditorias` con la acción `Imprimir` (primera emisión) o `Reimprimir`, fecha/hora UTC, ID del pesaje y usuario responsable.

## Búsqueda, Edición, Anulación y Auditoría de Boletas

- **Acceso rápido y permisos:**
  - Menú **Boletas › Buscar / editar boleta** o atajo global `F6` desde la pantalla principal (requiere rol Supervisor o Administrador / permiso `EditarBoleta` o `AnularBoleta`).
  - Menú **Boletas › Anular boleta** abre directamente el buscador posicionando la intención de anulación.
  - Para los operadores, estas opciones permanecen ocultas en el menú para resguardar la integridad del pesaje.
- **Buscador de boletas (`FrmBuscarBoletas`):**
  - Búsqueda multicriterio con combinación lógica AND: rango de fechas (desde/hasta), número de boleta exacto (anula las fechas por prioridad), ganadero, cliente y estado (Vigente o Anulado).
  - Límite de seguridad de 200 filas con aviso en color naranja cuando los resultados son truncados.
  - Visualización diferenciada en grilla con formato contable: las boletas anuladas se destacan en color gris tenue.
  - Atajos en la grilla: `Enter` sobre una fila vigente abre el diálogo de edición (no ejecuta nueva búsqueda); tecla `F4` abre el editor; tecla `F8` abre el diálogo de anulación; `Escape` cierra la ventana.
  - Tras editar o anular, la grilla se recarga conservando el último filtro ejecutado (`_ultimoFiltro`) y manteniendo seleccionada la boleta modificada.
- **Edición de boletas (`FrmEditarBoleta`):**
  - **Qué se puede editar:** únicamente atributos descriptivos del animal y del despacho (ganadero, secuencia, cliente, dirección, color/raza, sexo y observación).
  - **Qué NO se puede editar:** los pesos (izquierdo, derecho y total) y el tipo de desposte (Res Completa / Media Res) son estrictamente de solo lectura. Para corregir un pesaje erróneo, la política operativa exige **anular la boleta y registrar un nuevo pesaje**.
  - **Conservación de datos históricos:** cambiar de cliente en el diálogo de edición no altera ni sobrescribe la dirección física ya registrada en la boleta.
  - **Detección inteligente de cambios:** si el usuario presiona "Guardar" (`F12`) sin haber realizado modificaciones, el sistema advierte que no hubo cambios y evita generar escrituras en base de datos o registros de auditoría vacíos.
  - **Manejo de concurrencia optimista:** control mediante el token `Version` en la tabla `Pesajes`. Si otro usuario modificó o anuló la boleta concurrentemente, se informa al usuario mediante una advertencia clara y se recargan los datos frescos.
- **Anulación de boletas (`FrmAnularBoleta`):**
  - **Carácter irreversible:** la anulación de una boleta es definitiva; una boleta anulada no puede volver a estado vigente ni modificarse.
  - **Motivo obligatorio:** exige ingresar una justificación detallada de entre 5 y 100 caracteres antes de permitir la confirmación.
  - **Trazabilidad y numeración:** el número de boleta anulada no se elimina ni se reutiliza; la correlatividad del sistema permanece intacta.
  - **Efecto en la impresión:** reimprimir una boleta anulada estampa en letras destacadas la leyenda `ANULADA` junto con el motivo y una marca de agua de advertencia.
- **Visor de auditoría (`FrmVisorAuditoria`):**
  - Accesible desde el menú **Administración › Visor de auditoría** (requiere permiso `VerAuditoria` / roles Supervisor o Administrador).
  - Interfaz de **solo lectura**: sin botones de borrado, modificación ni exportación que comprometan la seguridad de los registros.
  - Filtros avanzados por rango de fechas locales, tipo de entidad (`Pesaje`, `Usuario`, `Ganadero`, `Cliente`, etc.), acción (`Crear`, `Modificar`, `Anular`, `Login`, `LoginFallido`, etc.), usuario responsable y número de boleta.
  - **Pestaña "Cambios":** comparador visual de diferencias que resalta en amarillo suave los campos modificados, muestra "—" para valores nulos y traduce nombres técnicos a etiquetas de negocio comprensibles ("Dirección", "Ganadero", "Fecha y hora").
  - **Pestaña "JSON":** visualización técnica completa del estado anterior y posterior en texto monoespaciado.
  - **Seguridad y privacidad:** los intentos de inicio de sesión fallidos sin usuario reconocido se enmascaran automáticamente (`EntidadId = "***"`).

## Reportes

- **Acceso y permisos:**
  - Menú **Reportes › Reportes de pesaje** (abre por defecto "Pesajes del día") y **Reportes › Boletas anuladas**.
  - Requiere permiso `VerReportes` (disponible para Supervisor y Administrador; el menú permanece oculto para Operadores).
- **Tipos de reporte:**
  1. **Pesajes del día:** pesajes vigentes del día seleccionado (fecha desde y hasta sincronizadas).
  2. **Pesajes entre fechas:** pesajes vigentes dentro de un rango de fechas arbitrario.
  3. **Pesajes por ganadero:** pesajes vigentes filtrados por un ganadero específico dentro de un rango de fechas.
  4. **Pesajes por cliente:** pesajes vigentes filtrados por un cliente específico dentro de un rango de fechas.
  5. **Boletas anuladas:** boletas anuladas emitidas dentro del rango de fechas, con fecha/hora de anulación, usuario responsable y motivo completo de anulación.
- **Logotipo de la empresa:**
  - Utiliza el mismo logo configurado en **Administración › Parámetros del sistema**.
  - Si no hay logo configurado o el archivo de imagen no está disponible, el reporte se genera normalmente omitiendo el recuadro del logo, y la pantalla informa al usuario que el reporte saldrá sin logo.
- **Boletas incluidas y regla de cómputo de reses:**
  - Los 4 reportes de pesaje procesan exclusivamente boletas en estado **Vigente**; las boletas anuladas se excluyen estrictamente de los totales y del listado.
  - El reporte de **Boletas anuladas** procesa únicamente boletas en estado **Anulado**.
  - **Regla de reses:** cada res completa computa como `1,0` y cada media res computa como `0,5` reses.
  - **Pesos decimales:** formateados con 1 decimal y coma (`#,##0.0`) bajo cultura boliviana (`es-BO`).
- **Boletas con peso simulado:**
  - Si se emitieron boletas bajo modo simulador (por un Administrador), se incluyen en el reporte identificadas con un asterisco `*` en el número de boleta y una nota de advertencia al pie: `(*) n boleta(s) con peso SIMULADO — sin valor comercial.`
- **Límites de protección operativa:**
  - **Máximo de consulta:** hasta 20.000 boletas por reporte. Si se supera el límite, el sistema rechaza la consulta solicitando acotar el rango de fechas.
  - **Grilla en pantalla:** presenta las primeras 1.000 boletas para preservar la fluidez de la interfaz; los totales de la cabecera y pie reflejan la totalidad de las boletas.
  - **Impresión directa:** limitada a reportes de hasta 2.000 boletas. Para volúmenes mayores, se recomienda exportar a PDF e imprimir desde el lector externo.
- **Exportación a PDF y Excel:**
  - Botones "Exportar PDF..." y "Exportar Excel..." con cuadro de diálogo estándar para seleccionar la ruta y nombre de guardado.
  - Tras guardar exitosamente, el sistema consulta al usuario si desea abrir inmediatamente el archivo generado con la aplicación predeterminada del sistema.
  - El PDF replica el diseño y proporciones del formato original en orientación vertical mediante QuestPDF.
  - El Excel genera celdas numéricas y fechas reales con formato nativo mediante ClosedXML, sin fórmulas y listo para análisis o autosuma.
  - El archivo exportado corresponde exactamente a la instantánea del reporte que se visualiza en la pantalla.
- **Vista previa e impresión física:**
  - Botón "Vista previa" con visor integrado de páginas del reporte a escala proporcional.
  - Botón "Imprimir..." que abre el diálogo de selección de impresora de Windows e imprime las páginas en orientación vertical.
- **Invalidación reactiva:**
  - Cualquier modificación en los filtros (tipo de reporte, fechas, ganadero o cliente) invalida y limpia inmediatamente el reporte previo y deshabilita los botones de exportación e impresión, exigiendo presionar "Generar" para actualizar la información.
