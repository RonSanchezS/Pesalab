# Sistema de Pesaje para Faeneo

Sistema de control, registro de pesaje y emisión de guías de remisión de carne bovina para faeneo. Desarrollado en .NET 8 (Windows Forms) con SQLite y Entity Framework Core.

## Estructura de la Solución (`src/PesajeFaeneo.sln`)

- **PesajeFaeneo.Domain**: Núcleo de la aplicación. Entidades de negocio, enums, excepciones y reglas puras. Sin dependencias externas ni paquetes NuGet.
- **PesajeFaeneo.Application**: Casos de uso, interfaces, DTOs y validaciones de aplicación.
- **PesajeFaeneo.Infrastructure**: Acceso a datos con EF Core (SQLite), comunicación con balanzas (Serial/TCP/Simulador), generación e impresión de boletas y exportación de reportes.
- **PesajeFaeneo.WinForms**: Capa de presentación WinForms, adaptadores de interfaz de usuario y manejo de navegación.
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
