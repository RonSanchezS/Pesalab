# Plan de implementación F3 — Integración con balanza

**Autor:** revisión senior (2026-09-24)
**Ejecutor:** agente de implementación (un solo agente, sin subagentes)
**Punto de partida:** F2 completado (hoy **sin commit** en `feat/f2-catalogos`; ver Parte 0, regla 3). Build Release con 0 warnings; 292 tests en verde según `Agents.md` (mídelo tú mismo antes de empezar y anota el número real).
**HUs:** HU-F3-01 … HU-F3-07 (ver `Agents.md §3`).

Este documento es **la especificación**. Si algo no está aquí, se aplican las convenciones de `Agents.md §4` y, como plantilla de código, lo que ya hace F2 (`ServicioParametros`, `FrmParametros`, `ValidadorParametros`, `MigracionF2Tests`). Si algo contradice `Agents.md`, manda este documento y la diferencia se anota en el informe final.

La **Parte 12** recoge la revisión de este plan: huecos detectados, cómo se cubrieron y riesgos que quedan abiertos. **Léela antes de empezar**; muchas reglas de las Partes 2–10 existen por lo que se explica allí (se citan como "ver R*n*").

---

## Mapa HU → Partes

| HU | Título | Partes |
|---|---|---|
| HU-F3-01 | Abstracción `IScaleReader` y ciclo de conexión | 3 (contratos), 6 (bucle de conexión) |
| HU-F3-02 | Parser de tramas configurable por regex | 4 |
| HU-F3-03 | Detector de estabilidad | 5 |
| HU-F3-04 | Lectores Serial (RS-232/USB) y TCP | 7 |
| HU-F3-05 | Simulador de balanza | 8 |
| HU-F3-06 | Servicio de balanza compartido y entrega segura a la UI | 6 (servicio), 9 (arranque/parada, UI) |
| HU-F3-07 | Configuración y diagnóstico de balanza | 2 (esquema), 10 (10.1 validador, 10.2 servicio, 10.3–10.4 formulario, 10.5 integración con el shell) |

Orden de ejecución obligatorio: **Parte 2 → 3 → 4 → 5 → 10.1 → 6 → 7 → 8 → 9 → 10.2 a 10.5 → 11**. Cada Parte termina con la compuerta de calidad (regla 0.4).

> **Ojo con las dependencias entre Partes:** `ServicioBalanza.ProbarAsync` (Parte 6) usa `ValidadorConfiguracionBalanza` (Parte 10.1), por eso 10.1 va antes de la Parte 6. Todo lo que referencia `FrmConfiguracionBalanza` (el ítem de menú de `FrmPrincipal` y su aserción en `ContenedorDITests`) está en la Parte 10.5, **no** en la 9, para que la compuerta de la Parte 9 compile.

---

## Parte 0 — Reglas de comportamiento del agente (obligatorias)

1. **Trabajas tú mismo.** No lances subagentes ni dejes trabajo en segundo plano.
2. **No te detengas hasta terminar** las Partes 1 a 11. Responde **una sola vez**, al final, con el informe de la Parte 11. Si algo te bloquea, documéntalo, elige la opción más conservadora compatible con este plan y continúa.
3. **Git:**
   - Antes de empezar, `git status` debe estar **limpio**. Hoy **no lo está**: todo F2 está sin commit en `feat/f2-catalogos` y varios archivos tienen finales **LF** (git avisa "LF will be replaced by CRLF"). El coordinador debe convertirlos a CRLF y commitear F2 (por ejemplo `feat(F2): parámetros y catálogos`) antes de entregarte este plan. Si al empezar no está limpio, **detente y repórtalo**; no mezcles F2 con F3.
   - Crea la rama `feat/f3-balanza` desde el HEAD actual (el commit de F2).
   - **No hagas commit ni push.** Deja todo en el árbol de trabajo.
4. **Compuerta de calidad después de cada Parte:**
   ```bash
   dotnet build src/PesajeFaeneo.sln -c Release
   ```
   ```bash
   dotnet test src/PesajeFaeneo.sln -c Release
   ```
   0 warnings (`TreatWarningsAsErrors=true`), 0 errores y todos los tests en verde. **No avances con rojo.** Nunca desactives un warning, un test ni un analizador para pasar la compuerta. Si un test **existente** falla por un cambio de esta fase, corrige los **datos o dobles del test**, nunca la regla nueva, y anótalo en el informe.
   - Ejecuta la suite **tres veces seguidas** al terminar las Partes 6, 7 y 9 (las que tienen hilos y temporizadores). Un test que falla 1 de 3 veces es un test roto: corrígelo (ver regla 0.13), no lo reintentes hasta que pase.
5. **Convenciones:** identificadores, comentarios, mensajes de UI y de log en **español**; imita el estilo y la densidad de comentarios del código existente. Finales de línea **CRLF**, UTF-8 (`.editorconfig`): al terminar cada Parte verifica que ningún archivo nuevo o tocado quede con LF (`git diff --stat` y `git status` muestran el aviso "LF will be replaced by CRLF"; conviértelo). xUnit solo con `Assert`. Siempre `TimeProvider` inyectado; en tests, `FakeTimeProvider`. Fechas persistidas en UTC.
   - **Única excepción al español:** la interfaz `IScaleReader`, cuyo nombre lo fija la HU-F3-01. Las propiedades existentes de la entidad (`HostTcp`, `BaudRate`, `DataBits`, `TimeoutMs`) no se renombran.
6. **Capas** (las hace cumplir `ArquitecturaTests`): Domain sin NuGet; Application sin EF, WinForms, Serilog, Hosting, `System.Drawing` **ni `System.IO.Ports`** (se agrega esa aserción en la Parte 11); Infrastructure sin WinForms. `System.Threading.Channels` y `System.Net.Sockets` son parte de la BCL de .NET 8: pueden usarse en Infrastructure sin paquetes nuevos. **No agregues paquetes NuGet** (`System.IO.Ports` 8.0.0 ya está referenciado por Infrastructure).
7. **Migraciones:** no modifiques `Inicial`, `F1_BloqueoUsuario` ni `F2_Catalogos`. Los cambios de esquema van en **una** migración nueva (Parte 2). Al terminar, `dotnet ef migrations has-pending-model-changes` no debe reportar cambios.
8. **Librerías:** antes de escribir código contra una API de librería, consulta la documentación con Context7 (`ToolSearch` → `select:mcp__context7__resolve-library-id,mcp__context7__query-docs`; biblioteca `/dotnet/docs` o `/websites/learn_microsoft_en-us_dotnet`). Como mínimo: `System.IO.Ports.SerialPort` (`Open`, `Close`, `Read`, `ReadTimeout`, `DiscardInBuffer`, `GetPortNames`), `System.Threading.Channels` (`BoundedChannelOptions`, `Complete(Exception)`, `ReadAsync`), `TcpClient.ConnectAsync(string, int, CancellationToken)`, `NetworkStream.ReadAsync(Memory<byte>, CancellationToken)`, `Task.Delay(TimeSpan, TimeProvider, CancellationToken)`, `new CancellationTokenSource(TimeSpan, TimeProvider)`, `FakeTimeProvider.Advance`, `Progress<T>`, `System.Windows.Forms.Timer`.
9. **Hilo de UI:** nunca bloquees el hilo de UI con `.Result`, `.Wait()` ni `.GetAwaiter().GetResult()` una vez iniciado el bucle de mensajes. Los manejadores de eventos asíncronos son `async void` (o lambdas `async (_, _) =>`) y delegan en `FormBase.EjecutarAsync`.
10. **Regla de reentrada de `FormBase` (0.10 de F2, sigue vigente):** `EjecutarAsync`/`Ejecutar` descartan en silencio cualquier invocación mientras otra está en curso. Nunca anides `EjecutarAsync` dentro de otro. Los métodos de carga no se envuelven a sí mismos: los envuelve el llamador. **Nuevo en F3:** el botón "Cancelar prueba" y el temporizador de monitoreo **no** usan `EjecutarAsync` (se ejecutan mientras otra operación envuelta está en curso; ver Parte 10).
11. **Tests de WinForms:** todo test que cree un `Form`, un control o un `System.Windows.Forms.Timer` corre dentro de `HiloSta.Ejecutar(...)` y desecha lo que crea. Los servicios falsos son clases escritas a mano (sin librerías de mocks). Los `MessageBox` no se pueden probar: expón ganchos `internal` que salten la confirmación, como hacen `FrmUsuarios` y `FrmParametros`.
12. **`ConfigureAwait(false)` obligatorio** en **todo** `await` del código de balanza en Application e Infrastructure (`ServicioBalanza`, lectores, simulador, fábrica). No en WinForms (allí se necesita volver al hilo de UI). Motivo: R6.
13. **Tests con hilos y tiempo:**
    - Nunca `Thread.Sleep` ni `Task.Delay` con un tiempo fijo "a ver si alcanza" para esperar un resultado. Usa el helper `Esperar` de la Parte 6.5 (sondeo con límite, que falla con un mensaje claro).
    - Todo test asíncrono con temporizadores tiene un límite global: el helper falla a los 5 s como máximo; ningún test debe poder colgarse.
    - Toda lógica que pueda ser pura (parser, separador de tramas, detector, generador del simulador, validador, textos de estado) se prueba **sin** hilos ni tiempo.
14. **Seguridad:** sin puertas traseras, sin formularios "de demostración" ocultos. El simulador **nunca** se activa solo por compilar en Debug ni por una variable de entorno: solo por la configuración guardada en BD (ver R14).
15. **Logging:** nunca registres cada trama recibida en nivel `Information` o superior (la balanza envía 5–20 tramas por segundo y los logs se retienen 30 días). Ver D13.

---

## Parte 1 — Decisiones de diseño (asumidas; el usuario puede cambiarlas)

Definitivas para esta ejecución. Centralízalas como constantes en `ReglasBalanza` (Parte 3.2) para que cambiarlas sea trivial.

| # | Decisión | Valor |
|---|---|---|
| D1 | Modo de transmisión | **Continuo**: la balanza (o su indicador) emite tramas sin que se le pidan. **No** se implementa el modo "a pedido" (enviar un comando y esperar respuesta). Ver R22. |
| D2 | Delimitación de tramas | Bytes decodificados con **Latin-1** (`Encoding.Latin1`, 1 byte = 1 carácter, nunca falla). Terminan una trama: `CR` (0x0D), `LF` (0x0A) y `ETX` (0x03). `STX` (0x02) **descarta** lo acumulado y empieza una trama nueva. Las tramas vacías se ignoran. Una trama que supera **256** caracteres sin delimitador se descarta entera (y se cuenta como descartada). |
| D3 | Parser | `Regex` configurable con grupo con nombre **`peso` obligatorio** y grupo **`estable` opcional**. Del valor de `peso` se quitan todos los espacios, la coma se toma como separador **decimal** (se reemplaza por punto) y se interpreta con `CultureInfo.InvariantCulture` y `NumberStyles.AllowLeadingSign \| NumberStyles.AllowDecimalPoint`. Se redondea a **0,1 kg** con `MidpointRounding.AwayFromZero` (igual que `DecimalARealConverter`). Válido solo si `\|peso\| ≤ 99 999,9`. Si el patrón define el grupo `estable`, la trama "reporta estable" cuando ese grupo capturó algo no vacío; si no lo define, la trama no reporta nada (`null`). Tiempo límite de evaluación de la regex: **100 ms** (`RegexMatchTimeoutException` → trama inválida). Unidad: **kg** siempre (ver R23). |
| D4 | Patrón por defecto | Cambia a `(?<peso>[-+]?\s*\d+(?:[.,]\d+)?)` (admite espacios entre el signo y los dígitos: muchos indicadores envían `-   12.5`). El patrón actual `(?<peso>[-+]?\d+(?:[.,]\d+)?)` pierde el signo en ese caso (ver R11). |
| D5 | Estabilidad | Ventana deslizante con las **últimas N lecturas válidas** (`LecturasParaEstable`, por defecto 5). Estable ⇔ la ventana está llena **y** `max − min ≤ ToleranciaKg` (por defecto 0,5). Si una trama reporta explícitamente "no estable" (grupo `estable` definido y vacío) la ventana se **vacía** y esa lectura no se agrega. Las tramas inválidas no vacían la ventana ni se agregan. El peso publicado es siempre la **última** lectura válida. Con N = 1 toda lectura válida es estable (salvo que la trama diga lo contrario). |
| D6 | Vigilancia de datos | Si no llega **ninguna trama** en `TimeoutMs` (por defecto 3000 ms): estado `SinDatos`, peso nulo, ventana vaciada. Tras **3** vencimientos seguidos se fuerza la reconexión (cierra y reabre). Si llegan tramas pero **ninguna válida** durante `TimeoutMs`: estado `TramasNoReconocidas` (no se reconecta: la conexión funciona, el patrón no). |
| D7 | Reconexión | Automática y sin fin mientras el servicio esté iniciado. Espera entre intentos: **1, 2, 4, 8, 10, 10, …** segundos. El contador vuelve a cero cuando se recibe la primera trama tras conectar. |
| D8 | Servicio compartido | **Un único** `ServicioBalanza` singleton por proceso, con **un único** lector abierto a la vez (el puerto serie es exclusivo). Arranca en `Program` **después** de migrar y sembrar la BD, y se detiene al salir (ver R4). No depende de que haya sesión: funciona también en la pantalla de login. |
| D9 | Entrega a la UI | **Sondeo** desde el hilo de UI: la UI lee cada **100 ms**, con un `System.Windows.Forms.Timer`, una instantánea **inmutable** (`InstantaneaBalanza`) publicada por el servicio, y solo redibuja si cambió su `Secuencia`. El servicio **no** expone eventos (ver R7). |
| D10 | Simulador | Es un tipo de conexión más (`TipoConexion.Simulador`, el valor sembrado por defecto) y sus tramas pasan por **el mismo** parser y detector que una balanza real. Emite una trama cada 200 ms con el formato `0.0` (cultura invariante). Solo lo controla quien tiene `ConfigurarBalanza`, desde `FrmConfiguracionBalanza`. Mientras está activo, la barra de estado muestra **"SIMULADOR"** resaltado. Ver R14. |
| D11 | Validación de la configuración | Ver tabla de la Parte 3.2 (`ReglasBalanza`) y Parte 10.1. Se validan **todos** los campos siempre (también los del transporte no elegido; los valores por defecto son válidos). |
| D12 | Permisos | `IServicioConfiguracionBalanza` (obtener, actualizar, listar puertos) y `IServicioBalanza.ProbarAsync`: **`ConfigurarBalanza`** (solo Administrador). `IServicioBalanza.Instantanea` y `ObtenerTramasRecientes`: **sin permiso ni sesión** (dato de hardware, no sensible, lo lee la barra de estado cada 100 ms). `IniciarAsync`/`DetenerAsync`/`ReiniciarAsync`: operaciones de sistema, sin permiso (las llama `Program` antes del login y el servicio de configuración tras guardar). |
| D13 | Auditoría y logs | Guardar configuración: `AccionAuditoria.CambioConfigBalanza`, `Entidad = "ConfiguracionBalanza"`, `EntidadId = "1"`, snapshots antes/después (record sin `Id` ni `ModificadoEn`), `Motivo = "Actualización de configuración de balanza"`. Sin cambios ⇒ ni guardado ni auditoría ni reinicio. Logs: `Information` al conectar y al detenerse; `Warning` al perder la conexión o fallar un intento, **solo si el mensaje difiere del último registrado** (evita una línea cada 10 s durante horas con la balanza apagada); tramas crudas solo en `Debug`. Probar la conexión y mover el simulador no se auditan (solo log `Information`). |
| D14 | Puerto serie | `Handshake.None`, `DtrEnable = true`, `RtsEnable = true` (muchos indicadores y adaptadores USB lo necesitan; ver R34), `ReadTimeout = 250 ms`, `WriteTimeout = 250 ms`. Tras `Open()`, `DiscardInBuffer()` (descarta bytes viejos del driver). Lectura con `SerialPort.Read(byte[], int, int)` bloqueante en un **hilo dedicado**, nunca `BaseStream.ReadAsync` ni el evento `DataReceived` (ver R1). Tras cerrar un puerto se esperan **500 ms** antes de volver a abrir uno (ver R2). |
| D15 | TCP | `TcpClient` con `NoDelay = true` y `KeepAlive` activado; tiempo límite de conexión = `TimeoutMs`; lectura con `NetworkStream.ReadAsync` en una tarea de bombeo; una lectura de 0 bytes significa que la balanza cerró la conexión. Rol **cliente** (la app se conecta al conversor serie-Ethernet; típicamente puerto 4001). |
| D16 | Esquema | Sin columnas nuevas. Migración `F3_Balanza` con restricciones CHECK nuevas o endurecidas y actualización del patrón sembrado (Parte 2). |
| D17 | Menú y barra de estado | Administración › Configuración de balanza pasa a **activo** (`FaseFutura = null`). La barra de estado de `FrmPrincipal` muestra el estado y el peso de la balanza para **todos** los roles. |

---

## Parte 2 — Esquema: restricciones de la configuración de balanza

**Archivos:** `Infrastructure/Persistencia/Configuraciones/ConfiguracionBalanzaConfiguration.cs`, migración nueva, `tests/.../PersistenciaIntegracionTests.cs`, test nuevo `MigracionF3Tests.cs`.

> SQLite **no** hace cumplir `HasMaxLength`. Las CK son la única defensa de la BD para rangos y longitudes (lección R2 de F2).

1. En `ConfiguracionBalanzaConfiguration.ToTable(...)` deja **exactamente** estas CK (reemplaza las dos existentes que se endurecen, conserva `Id` y `TipoConexion`):

   | Nombre | Expresión |
   |---|---|
   | `CK_ConfiguracionBalanza_Id` | `Id = 1` *(sin cambio)* |
   | `CK_ConfiguracionBalanza_TipoConexion` | `TipoConexion IN (1, 2, 3)` *(sin cambio)* |
   | `CK_ConfiguracionBalanza_LecturasParaEstable` | `LecturasParaEstable BETWEEN 1 AND 50` *(antes `>= 1`)* |
   | `CK_ConfiguracionBalanza_TimeoutMs` | `TimeoutMs BETWEEN 500 AND 60000` *(antes `> 0`)* |
   | `CK_ConfiguracionBalanza_BaudRate` | `BaudRate BETWEEN 110 AND 921600` |
   | `CK_ConfiguracionBalanza_DataBits` | `DataBits BETWEEN 5 AND 8` |
   | `CK_ConfiguracionBalanza_Paridad` | `Paridad IN (0, 1, 2, 3, 4)` |
   | `CK_ConfiguracionBalanza_BitsParada` | `BitsParada IN (1, 2, 3)` |
   | `CK_ConfiguracionBalanza_PuertoTcp` | `PuertoTcp BETWEEN 1 AND 65535` |
   | `CK_ConfiguracionBalanza_ToleranciaKg` | `ToleranciaKg >= 0 AND ToleranciaKg <= 10` |
   | `CK_ConfiguracionBalanza_PuertoCom` | `length(PuertoCom) BETWEEN 1 AND 20` |
   | `CK_ConfiguracionBalanza_HostTcp` | `length(HostTcp) BETWEEN 1 AND 100` |
   | `CK_ConfiguracionBalanza_PatronRegex` | `length(PatronRegex) BETWEEN 1 AND 250` |

   Las CK de BD son deliberadamente **más amplias** que el validador (Parte 3.2): la BD protege contra basura; el validador aplica la política.

2. En `HasData`, cambia `PatronRegex` al nuevo valor por defecto (D4): `@"(?<peso>[-+]?\s*\d+(?:[.,]\d+)?)"`. Cambia también el valor por defecto de la propiedad en `Domain/Entities/Entities.cs` (`ConfiguracionBalanza.PatronRegex`) para que coincidan. **No** cambies `ModificadoEn` del `HasData`.
3. Verifica antes de generar que el enum se persiste como entero (revisa `PesajeDbContextModelSnapshot.cs`: `Paridad`/`BitsParada` deben ser `INTEGER`). Si no lo fueran, **detente** y repórtalo: las CK de la tabla anterior asumen enteros.
4. Genera la migración:
   ```bash
   dotnet ef migrations add F3_Balanza -p src/PesajeFaeneo.Infrastructure -s src/PesajeFaeneo.WinForms -o Persistencia/Migraciones
   ```
5. **Revisa el SQL generado:**
   ```bash
   dotnet ef migrations script F2_Catalogos F3_Balanza -p src/PesajeFaeneo.Infrastructure -s src/PesajeFaeneo.WinForms
   ```
   Debe contener la reconstrucción de `ConfiguracionesBalanza` (`CREATE TABLE "ef_temp_ConfiguracionesBalanza"` … `INSERT INTO … SELECT` … `DROP TABLE` … `ALTER TABLE … RENAME`) con **todas** las CK de la tabla, y un `UPDATE "ConfiguracionesBalanza" SET "PatronRegex" = …` para `Id = 1`. Ninguna tabla tiene FK hacia `ConfiguracionesBalanza`, así que la reconstrucción es segura. Si falta algo, **no** edites el SQL a mano: revisa la configuración y regenera. Anota el resultado en el informe.
   - El `UPDATE` del patrón sobrescribe incondicionalmente el valor de la fila 1. Es aceptable **solo** porque hasta F3 no existía ninguna forma de editar esa fila (ver R11). Anótalo en el informe.

**Tests (en `PersistenciaIntegracionTests`, con los helpers que ya use esa clase):** una prueba por CK nueva o endurecida que modifique la fila sembrada con un valor fuera de rango y espere `DbUpdateException`: `LecturasParaEstable = 51`, `TimeoutMs = 499`, `BaudRate = 100`, `DataBits = 9`, `Paridad = (ParidadSerial)7`, `BitsParada = (BitsParada)4`, `PuertoTcp = 0`, `PuertoTcp = 65536`, `ToleranciaKg = -0.1m`, `ToleranciaKg = 10.1m`, `PuertoCom = ""`, `HostTcp = ""`, `PatronRegex = ""`. Usa `[Theory]` si la clase ya lo hace; si no, un `[Fact]` por caso.

**Test nuevo `MigracionF3Tests`** (SQLite en memoria, conexión abierta, `ConfigurarSqlite(connection)`, mismo esqueleto que `MigracionF2Tests`):
1. `MigrateAsync("20260924183125_F2_Catalogos")`.
2. Verifica que la fila 1 de `ConfiguracionesBalanza` tiene el patrón viejo.
3. Modifica con SQL crudo `LecturasParaEstable = 7`, `TimeoutMs = 2500` (valores válidos) para comprobar que la copia de la reconstrucción conserva datos.
4. `MigrateAsync()` (hasta la última).
5. Verifica: `LecturasParaEstable = 7`, `TimeoutMs = 2500`, `PatronRegex` = patrón nuevo, `PRAGMA foreign_key_check` sin filas, y que un `UPDATE … SET TimeoutMs = 100` con SQL crudo falla por CHECK.

`ModeloVsMigracionTests` debe seguir en verde.

---

## Parte 3 — Contratos de Application (base de HU-F3-01)

Crea estas piezas **exactamente con estos nombres y firmas** en `src/PesajeFaeneo.Application/Balanza/` (espacio de nombres `PesajeFaeneo.Application.Balanza`), salvo donde se indica otra carpeta. Puedes añadir comentarios XML.

### 3.1 Tipos de datos (`Balanza/Dtos/BalanzaDtos.cs` y `Balanza/EstadoBalanza.cs`)

```csharp
public enum EstadoBalanza
{
    Detenida = 0,             // servicio no iniciado o detenido
    Conectando = 1,           // abriendo el puerto / conectando (incluye esperas de reintento)
    Conectada = 2,            // conectada; PesoKg != null si ya llegó al menos una lectura válida
    SinDatos = 3,             // conectada, pero sin tramas durante TimeoutMs
    TramasNoReconocidas = 4,  // llegan tramas, pero ninguna válida durante TimeoutMs
    Error = 5,                // falló la conexión o se perdió; se reintentará (Mensaje explica)
    EnDiagnostico = 6         // servicio pausado por ProbarAsync
}

public sealed record ConfiguracionBalanzaDto(
    TipoConexion TipoConexion,
    string PuertoCom,
    int BaudRate,
    int DataBits,
    ParidadSerial Paridad,
    BitsParada BitsParada,
    string HostTcp,
    int PuertoTcp,
    string PatronRegex,
    int LecturasParaEstable,
    decimal ToleranciaKg,
    int TimeoutMs);

public sealed record ConfiguracionBalanzaLeida(ConfiguracionBalanzaDto Configuracion, DateTime ModificadoEn);

public sealed record ResultadoTrama(bool Valida, decimal? PesoKg, bool? EstableReportado, string? Error)
{
    public static ResultadoTrama Invalida(string error) => new(false, null, null, error);
}

/// Instantánea inmutable del estado de la balanza. Se reemplaza entera en cada publicación.
public sealed record InstantaneaBalanza(
    long Secuencia,
    EstadoBalanza Estado,
    decimal? PesoKg,
    bool Estable,
    DateTimeOffset? InstanteLectura,
    string? Mensaje,
    string Descripcion,
    bool EsSimulador)
{
    public static InstantaneaBalanza Inicial { get; } =
        new(0, EstadoBalanza.Detenida, null, false, null, null, string.Empty, false);

    /// Lo usará F4-05 para capturar: conectada, estable, con peso y con lectura reciente.
    public bool EsCapturable(DateTimeOffset ahora, TimeSpan antiguedadMaxima) =>
        Estado == EstadoBalanza.Conectada
        && Estable
        && PesoKg.HasValue
        && InstanteLectura.HasValue
        && ahora - InstanteLectura.Value <= antiguedadMaxima;
}

public sealed record AvancePruebaBalanza(string TramaVisible, ResultadoTrama Resultado, bool Estable);

public sealed record ResultadoPruebaBalanza(
    bool Conecto,
    string? Error,
    int TramasRecibidas,
    int TramasValidas,
    decimal? UltimoPesoKg,
    bool AlcanzoEstabilidad,
    bool Cancelada);
```

Invariantes de `InstantaneaBalanza` que el servicio debe respetar al publicar: `PesoKg` y `InstanteLectura` solo son no nulos en estado `Conectada`; `Estable` solo puede ser `true` en estado `Conectada` con `PesoKg` no nulo.

### 3.2 Reglas y campos (`Balanza/ReglasBalanza.cs`)

```csharp
public static class CamposBalanza
{
    public const string TipoConexion = nameof(TipoConexion);
    public const string PuertoCom = nameof(PuertoCom);
    public const string BaudRate = nameof(BaudRate);
    public const string DataBits = nameof(DataBits);
    public const string Paridad = nameof(Paridad);
    public const string BitsParada = nameof(BitsParada);
    public const string HostTcp = nameof(HostTcp);
    public const string PuertoTcp = nameof(PuertoTcp);
    public const string PatronRegex = nameof(PatronRegex);
    public const string LecturasParaEstable = nameof(LecturasParaEstable);
    public const string ToleranciaKg = nameof(ToleranciaKg);
    public const string TimeoutMs = nameof(TimeoutMs);
}

public static class ReglasBalanza
{
    public static readonly IReadOnlyList<int> BaudiosPermitidos = [1200, 2400, 4800, 9600, 19200, 38400, 57600, 115200];
    public static readonly IReadOnlyList<int> BitsDatosPermitidos = [7, 8];
    public static readonly IReadOnlyList<BitsParada> BitsParadaPermitidos = [BitsParada.One, BitsParada.Two]; // ver R24
    public const string PatronPuertoCom = "^COM[1-9][0-9]{0,2}$";   // tras Trim + ToUpperInvariant
    public const int LongitudMaximaHost = 100;
    public const int PuertoTcpMinimo = 1;
    public const int PuertoTcpMaximo = 65535;
    public const int LongitudMaximaPatron = 250;
    public const string GrupoPeso = "peso";
    public const string GrupoEstable = "estable";
    public const int LecturasMinimas = 1;
    public const int LecturasMaximas = 50;
    public const decimal ToleranciaMinimaKg = 0m;
    public const decimal ToleranciaMaximaKg = 10m;
    public const int TimeoutMinimoMs = 500;
    public const int TimeoutMaximoMs = 60000;

    public const decimal PesoMaximoAbsolutoKg = 99999.9m;
    public const int LongitudMaximaTrama = 256;
    public const int TiempoLimiteRegexMs = 100;
    public const int TimeoutsAntesDeReconectar = 3;
    public static readonly IReadOnlyList<int> EsperasReconexionSegundos = [1, 2, 4, 8, 10]; // el último se repite
    public const int EsperaTrasCerrarPuertoMs = 500;
    public const int CapacidadCanalTramas = 64;
    public const int TramasRecientesMaximo = 50;
    public const int DuracionPruebaSegundos = 10;
    public const int IntervaloSimuladorMs = 200;
    public const decimal PesoMaximoSimuladorKg = 2000m;
    public const int IntervaloMonitorUiMs = 100;
}
```

### 3.3 Abstracciones de lectura (`Balanza/IScaleReader.cs`, `Balanza/ConexionBalanzaException.cs`)

```csharp
/// Una conexión física (o simulada) con la balanza. Una instancia = un intento de conexión:
/// tras un error se desecha y se crea otra con la fábrica. No es seguro para uso concurrente.
public interface IScaleReader : IAsyncDisposable
{
    /// Texto corto para la UI: "COM3 9600 8N1", "192.168.1.100:4001" o "Simulador".
    string Descripcion { get; }

    /// Abre el puerto o la conexión. Lanza ConexionBalanzaException con un mensaje apto para el
    /// usuario si no puede. Puede lanzar OperationCanceledException si se cancela el token.
    Task ConectarAsync(CancellationToken cancellationToken);

    /// Espera la siguiente trama completa (sin delimitadores). Lanza ConexionBalanzaException si la
    /// conexión se perdió y OperationCanceledException si se cancela el token (sin perder tramas:
    /// una trama no entregada sigue disponible para la próxima llamada).
    ValueTask<string> LeerTramaAsync(CancellationToken cancellationToken);
}

public interface IFabricaLectorBalanza
{
    IScaleReader Crear(ConfiguracionBalanzaDto configuracion);
}

/// Estado compartido del simulador (singleton). Lo manipula FrmConfiguracionBalanza.
public interface ISimuladorBalanza
{
    decimal PesoObjetivoKg { get; }
    /// Recorta a [0, PesoMaximoSimuladorKg] y redondea a 0,1 (AwayFromZero).
    void EstablecerPesoObjetivo(decimal pesoKg);
    /// Si es true, ConectarAsync del lector simulado falla y un lector ya conectado pierde la conexión.
    bool DesconexionSimulada { get; set; }
}

public interface IEnumeradorPuertosSerie
{
    /// Nombres de puerto COM presentes, sin duplicados, normalizados (Trim + mayúsculas) y ordenados
    /// por número (COM2 antes que COM10). Nunca lanza: ante error devuelve lista vacía.
    IReadOnlyList<string> Listar();
}

/// Error de comunicación con la balanza. Su Message siempre es apto para mostrarse al usuario.
public sealed class ConexionBalanzaException : Exception
{
    public ConexionBalanzaException(string mensaje) : base(mensaje) { }
    public ConexionBalanzaException(string mensaje, Exception inner) : base(mensaje, inner) { }
}
```

`ConexionBalanzaException` **no** hereda de `DomainException`: nunca debe llegar a `ManejadorErrores` (el servicio la convierte en estado; `ProbarAsync` la convierte en `ResultadoPruebaBalanza.Error`).

### 3.4 Servicios (`Balanza/IServicioBalanza.cs`, `Balanza/IServicioConfiguracionBalanza.cs`)

```csharp
public interface IServicioBalanza
{
    /// Última instantánea publicada. Seguro desde cualquier hilo. Sin permisos.
    InstantaneaBalanza Instantanea { get; }

    /// Copia de las últimas tramas crudas (máx. TramasRecientesMaximo), ya formateadas con
    /// TramaVisible.Formatear; la más reciente al final. Sin permisos.
    IReadOnlyList<string> ObtenerTramasRecientes();

    /// Lee la configuración de la BD y lanza el bucle de conexión en segundo plano. NO espera a que
    /// conecte. Si ya está iniciado, no hace nada.
    Task IniciarAsync(CancellationToken cancellationToken = default);

    /// Detiene el bucle, cierra el lector y publica Detenida. Si no está iniciado, no hace nada.
    Task DetenerAsync(CancellationToken cancellationToken = default);

    /// DetenerAsync + IniciarAsync (relee la configuración de la BD).
    Task ReiniciarAsync(CancellationToken cancellationToken = default);

    /// Exige ConfigurarBalanza. Valida la configuración candidata (ValidacionException si no es válida),
    /// pausa el bucle compartido (estado EnDiagnostico), se conecta con la candidata durante `duracion`
    /// informando cada trama por `progreso`, y al final (siempre, también ante error o cancelación)
    /// reanuda el bucle compartido con la configuración GUARDADA si estaba iniciado.
    /// La cancelación NO lanza: devuelve Cancelada = true. Los errores de conexión NO lanzan:
    /// devuelven Conecto = false y Error.
    Task<ResultadoPruebaBalanza> ProbarAsync(
        ConfiguracionBalanzaDto candidata,
        TimeSpan duracion,
        IProgress<AvancePruebaBalanza>? progreso,
        CancellationToken cancellationToken = default);
}

public interface IServicioConfiguracionBalanza
{
    Task<ConfiguracionBalanzaLeida> ObtenerAsync(CancellationToken cancellationToken = default);   // ConfigurarBalanza
    /// Devuelve true si había cambios (guardó, auditó y reinició la balanza); false si no había cambios.
    Task<bool> ActualizarAsync(ConfiguracionBalanzaDto dto, CancellationToken cancellationToken = default); // ConfigurarBalanza
    IReadOnlyList<string> ListarPuertosSerie();                                                    // ConfigurarBalanza
}
```

### 3.5 Persistencia (`Application/Abstracciones/Persistencia/`)

```csharp
public interface IConfiguracionBalanzaRepositorio
{
    /// Con tracking. La fila Id = 1 la siembra HasData; si faltara, lanza InvalidOperationException.
    Task<ConfiguracionBalanza> ObtenerAsync(CancellationToken cancellationToken = default);
}
```

Agrega a `IUnidadDeTrabajo`: `IConfiguracionBalanzaRepositorio ConfiguracionBalanza { get; }`.

> **Rompe la compilación de los tests:** `tests/.../ServicioParametrosTests.cs` contiene `UowDecorador : IUnidadDeTrabajo`. Agrégale la propiedad delegando en la unidad decorada. Busca con `grep -rn ": IUnidadDeTrabajo" tests src` cualquier otra implementación.

### 3.6 Utilidades puras de Application (`Balanza/`)

- `MapeoBalanza` (estática): `ConfiguracionBalanzaDto ADto(ConfiguracionBalanza entidad)` y `void Aplicar(ConfiguracionBalanzaDto dto, ConfiguracionBalanza entidad)` (copia los 12 campos; **no** toca `Id` ni `ModificadoEn`).
- `DescripcionConexion` (estática): `string Crear(ConfiguracionBalanzaDto c)`:
  - Serial → `$"{PuertoCom} {BaudRate} {DataBits}{letraParidad}{bitsParada}"`, con letras `N`, `O`, `E`, `M`, `S` y bits `1`, `2`, `1.5`. Ej.: `"COM3 9600 8N1"`.
  - Tcp → `$"{HostTcp}:{PuertoTcp}"`.
  - Simulador → `"Simulador"`.
- `TramaVisible` (estática): `string Formatear(string trama)`: reemplaza cada carácter de control (`char.IsControl`) por `<0xNN>` en hexadecimal en mayúsculas (`\t` → `<0x09>`) y recorta a `LongitudMaximaTrama` caracteres. Para mostrar y registrar en logs sin romper la UI.

### 3.7 Auditoría (`Application/Auditoria/FabricaAuditoria.cs`)

- `EntidadesAuditoria.ConfiguracionBalanza = "ConfiguracionBalanza"`.
- `public sealed record ConfiguracionBalanzaAuditoria(TipoConexion TipoConexion, string PuertoCom, int BaudRate, int DataBits, ParidadSerial Paridad, BitsParada BitsParada, string HostTcp, int PuertoTcp, string PatronRegex, int LecturasParaEstable, decimal ToleranciaKg, int TimeoutMs);`
- `CrearSnapshot(ConfiguracionBalanza)` y el caso correspondiente en `NormalizarObjeto`.
- Test en `FabricaAuditoriaTests`: el JSON de una `ConfiguracionBalanza` contiene `"TipoConexion":"Serial"` (enum como texto, ya lo hace `JsonStringEnumConverter`) y **no** contiene `ModificadoEn` ni `Id`.

---

## Parte 4 — HU-F3-02: parser de tramas y separador

### 4.1 `Application/Balanza/ParserTramas.cs` (clase sellada, pura, sin I/O)

```csharp
public sealed class ParserTramas
{
    /// Lanza ArgumentException con el mensaje de ValidarPatron si el patrón no es válido.
    public ParserTramas(string patron);

    /// null si el patrón es válido; si no, un mensaje apto para el usuario.
    public static string? ValidarPatron(string? patron);

    /// Nunca lanza.
    public ResultadoTrama Parsear(string? trama);
}
```

Reglas:
1. La regex se construye **siempre** con `new Regex(patron, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(ReglasBalanza.TiempoLimiteRegexMs))`. Nunca uses los métodos estáticos `Regex.Match(string, string)` (no tienen tiempo límite).
2. `ValidarPatron`, en este orden (primer error gana):
   - `null`, vacío o solo espacios → `"Ingrese el patrón de lectura."`
   - longitud > 250 → `"El patrón no puede superar 250 caracteres."`
   - no compila (`ArgumentException`) → `"El patrón no es una expresión regular válida."`
   - no define el grupo `peso` (`regex.GetGroupNames()`) → `"El patrón debe tener un grupo con nombre (?<peso>...)."`
   - El patrón **no** se recorta (un espacio al final puede ser intencional).
3. `Parsear`, en este orden:
   - `null` o vacía → `Invalida("Trama vacía.")`.
   - `Match`; `RegexMatchTimeoutException` → `Invalida("La trama tardó demasiado en analizarse.")`.
   - Sin coincidencia o grupo `peso` sin éxito o vacío → `Invalida("La trama no contiene un peso reconocible.")`.
   - Texto del grupo: quitar **todo** espacio en blanco (`char.IsWhiteSpace`), reemplazar `,` por `.`. `decimal.TryParse(texto, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var peso)`; si falla → `Invalida("El peso de la trama no es un número válido.")`.
   - `peso = Math.Round(peso, 1, MidpointRounding.AwayFromZero)`; si `Math.Abs(peso) > PesoMaximoAbsolutoKg` → `Invalida("El peso está fuera del rango admitido.")`.
   - `EstableReportado`: si el patrón define el grupo `estable` → `grupo.Success && grupo.Length > 0`; si no → `null`.
   - `new ResultadoTrama(true, peso, estableReportado, null)`.

**Tests `ParserTramasTests`** (`[Theory]` con `InlineData`; patrón por defecto `P0 = (?<peso>[-+]?\s*\d+(?:[.,]\d+)?)` y patrón con estabilidad `P1 = ^(?<estable>ST)?[^-+\d]*(?<peso>[-+]?\s*\d+(?:[.,]\d+)?)`):

| Patrón | Trama | Esperado |
|---|---|---|
| P0 | `"  123.4"` | 123.4, estable null |
| P0 | `"+0123.45 kg"` | 123.5 (redondeo AwayFromZero) |
| P0 | `"-5,2"` | -5.2 |
| P0 | `"-   12.5"` | -12.5 (signo con espacios) |
| P0 | `"0"` | 0.0 |
| P0 | `"ABC"` | inválida |
| P0 | `""` y `null` | inválida "Trama vacía." |
| P0 | `"123456"` | inválida (fuera de rango) |
| P0 | `"1,234.5"` | inválida (no es número tras reemplazar la coma; documentado, ver R12) |
| P1 | `"ST,GS,+  98.6kg"` | 98.6, estable **true** |
| P1 | `"US,GS,+  98.6kg"` | 98.6, estable **false** |
| P1 | `"ST,GS,-   0.4kg"` | -0.4, estable true |

Más: `ValidarPatron` con cada caso de error (vacío, 251 caracteres, `"(?<peso>"` sin cerrar, `"\d+"` sin grupo) y con P0/P1 → `null`; el constructor con patrón inválido lanza `ArgumentException`; un patrón catastrófico `(?<peso>(a+)+)b` contra `new string('a', 5000)` devuelve `Valida == false` **sin lanzar** y en menos de 2 s (mide con `Stopwatch` y usa un límite amplio para no volverlo inestable). **No** verifiques el texto del error en este caso: según las optimizaciones del motor de regex de .NET puede terminar por tiempo límite o simplemente sin coincidencia; lo que importa es que no se cuelga ni lanza.

### 4.2 `Infrastructure/Balanza/SeparadorTramas.cs` (clase sellada, pura)

```csharp
internal sealed class SeparadorTramas
{
    public int TramasDescartadas { get; }                        // por exceder LongitudMaximaTrama
    public IReadOnlyList<string> Agregar(byte[] buffer, int cantidad); // tramas completas en orden; lista vacía si no hay
    public void Reiniciar();                                     // descarta lo acumulado
}
```

Reglas (D2): decodifica con `Encoding.Latin1`; `CR`, `LF`, `ETX` cierran trama; `STX` descarta lo acumulado; tramas vacías (p. ej. el `LF` de un `CR LF`) no se emiten; si lo acumulado llega a 256 caracteres sin delimitador, se descarta, se incrementa `TramasDescartadas` y se ignora todo hasta el siguiente delimitador. Usa un `StringBuilder` interno. Agrega `InternalsVisibleTo PesajeFaeneo.Tests` en `PesajeFaeneo.Infrastructure.csproj` si no existe (hoy no existe: agrégalo igual que en Application).

**Tests `SeparadorTramasTests`:** `"12.3\r\n45.6\r\n"` → 2 tramas; la misma entrada partida en `"12."` + `"3\r\n45"` + `".6\r\n"` → las mismas 2; `"\r\n\r\n"` → ninguna; `"basura<STX>12.3<ETX>"` → solo `"12.3"`; 300 bytes `'1'` sin delimitador y luego `"\r\n5.0\r\n"` → solo `"5.0"` y `TramasDescartadas == 1`; byte 0xB0 (`°`) se conserva como `'°'`.

---

## Parte 5 — HU-F3-03: detector de estabilidad

### `Application/Balanza/DetectorEstabilidad.cs` (clase sellada, pura, **no** es segura entre hilos: la usa solo el bucle)

```csharp
public sealed class DetectorEstabilidad
{
    /// ArgumentOutOfRangeException si lecturasParaEstable no está en [LecturasMinimas, LecturasMaximas]
    /// o toleranciaKg no está en [ToleranciaMinimaKg, ToleranciaMaximaKg].
    public DetectorEstabilidad(int lecturasParaEstable, decimal toleranciaKg);

    public bool Estable { get; }

    /// Agrega una lectura válida y devuelve el nuevo valor de Estable (reglas D5).
    public bool Agregar(decimal pesoKg, bool? estableReportado = null);

    /// Vacía la ventana; Estable pasa a false.
    public void Reiniciar();
}
```

Implementación: `Queue<decimal>` de capacidad N; si `estableReportado == false` → `Reiniciar()` y devuelve `false` **sin** agregar; si no, encola, desencola si `Count > N`, y `Estable = Count == N && (max − min) <= tolerancia` (recorre la cola; N ≤ 50, no hace falta optimizar).

**Tests `DetectorEstabilidadTests`:**
- N=5, tol=0,5: cuatro lecturas iguales → `false`; la quinta → `true`.
- N=5, tol=0,5: `10.0, 10.2, 10.5, 10.3, 10.1` → `true` (rango 0,5, límite incluido); `10.0 … 10.6` → `false`.
- Estable y luego una lectura que se sale del rango → `false`; tras 5 lecturas nuevas dentro del rango → `true` (ventana deslizante).
- `estableReportado: false` con la ventana llena y estable → `false` y hacen falta N lecturas nuevas para volver a `true`.
- `estableReportado: true` no acelera: con 3 lecturas sigue `false`.
- N=1 → toda lectura es estable. Tol=0 → solo lecturas idénticas.
- `Reiniciar()` → `false`.
- Pesos negativos funcionan igual.
- Constructor con N=0, N=51, tol=-0,1, tol=10,1 → `ArgumentOutOfRangeException`.

---

## Parte 6 — HU-F3-01 y HU-F3-06: `ServicioBalanza` y ciclo de conexión

**Archivo:** `Application/Balanza/ServicioBalanza.cs` (`public sealed class ServicioBalanza : IServicioBalanza, IDisposable`).

**Prerrequisito:** la Parte 10.1 (`ValidadorConfiguracionBalanza` y sus tests) ya debe estar hecha.

**Dependencias del constructor:** `IUnidadDeTrabajoFactory`, `IFabricaLectorBalanza`, `ISesionActual`, `TimeProvider`, `ILogger<ServicioBalanza>`. Todas obligatorias (`ArgumentNullException`).

> **Solo `IDisposable`, no `IAsyncDisposable`.** El host se desecha con `using var host` (síncrono); el contenedor de DI lanza `InvalidOperationException` al desechar de forma síncrona un singleton que solo implementa `IAsyncDisposable` (R5).

### 6.1 Estado interno

- `private InstantaneaBalanza _instantanea = InstantaneaBalanza.Inicial;` leído con `Volatile.Read` y escrito **solo** por `Publicar(...)` con `Volatile.Write`.
- `private long _secuencia;` incrementado con `Interlocked.Increment` en cada `Publicar`.
- `private readonly SemaphoreSlim _control = new(1, 1);` serializa `IniciarAsync`, `DetenerAsync`, `ReiniciarAsync` y `ProbarAsync` (una operación de control a la vez).
- `private CancellationTokenSource? _cts; private Task? _bucle;` — solo se tocan con `_control` tomado.
- Cola circular de tramas recientes (`Queue<string>` + `lock`), máx. `TramasRecientesMaximo`.
- `private string? _ultimoMensajeRegistrado;` para no repetir logs idénticos (D13).
- `Publicar(EstadoBalanza estado, string descripcion, bool esSimulador, decimal? peso = null, bool estable = false, DateTimeOffset? instante = null, string? mensaje = null)`: construye el record con la siguiente secuencia y respeta las invariantes de 3.1 (si `estado != Conectada`, fuerza `peso = null`, `instante = null`, `estable = false`).

### 6.2 Métodos de control

- `IniciarAsync`: toma `_control`; si `_bucle` no es null y no terminó → libera y sale. Si no: lee la configuración (`await using var uow = await _uowFactory.CrearAsync(ct)`, `uow.ConfiguracionBalanza.ObtenerAsync`, `MapeoBalanza.ADto`); crea `_cts`; `_bucle = Task.Run(() => BucleProtegidoAsync(config, _cts.Token))`; libera. Las excepciones al leer la configuración **se propagan** (las registra `Program`).
- `DetenerAsync`: toma `_control`; llama a `DetenerInternoAsync()`; libera.
- `DetenerInternoAsync()` (privado, **requiere** `_control` tomado): si `_bucle` es null → return. `_cts.Cancel()`; `await _bucle.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false)` dentro de `try/catch` (si vence, `LogWarning` "El lector de balanza no se detuvo a tiempo" y sigue); desecha `_cts`; `_bucle = null`; `Publicar(Detenida, …)`.
  - Usa aquí **tiempo real** (`WaitAsync(TimeSpan)` sin `TimeProvider`): es una salvaguarda de cierre, no lógica de negocio, y con `FakeTimeProvider` nunca vencería.
- `ReiniciarAsync`: toma `_control`; `DetenerInternoAsync()`; mismo cuerpo que `IniciarAsync` sin volver a tomar el semáforo (extrae `IniciarInternoAsync`); libera.
- `Dispose()`: `_cts?.Cancel()`; `_bucle?.Wait(TimeSpan.FromSeconds(2))` dentro de `try/catch` (el bucle corre en el pool sin contexto de sincronización, así que esperar no provoca interbloqueo); desecha `_cts` y `_control`. Idempotente.

### 6.3 Bucle de conexión (`BucleProtegidoAsync` → `BucleAsync`)

`BucleProtegidoAsync` envuelve `BucleAsync` en `try/catch (Exception ex)`: cualquier excepción inesperada (un defecto) se registra con `LogError`, publica `Error` con `"Error interno del lector de balanza. Revise el log."` y **termina** (no reintenta en bucle un defecto). `OperationCanceledException` con el token cancelado se ignora.

`BucleAsync(ConfiguracionBalanzaDto config, CancellationToken ct)` — sigue **exactamente** esta estructura:

```text
descripcion = DescripcionConexion.Crear(config); esSimulador = config.TipoConexion == Simulador
errorPatron = ParserTramas.ValidarPatron(config.PatronRegex)
si errorPatron != null → LogError, Publicar(Error, mensaje: $"Patrón de lectura inválido: {errorPatron}") y return   // R19
parser = new ParserTramas(config.PatronRegex)
detector = new DetectorEstabilidad(config.LecturasParaEstable, config.ToleranciaKg)
timeout = TimeSpan.FromMilliseconds(config.TimeoutMs)
intento = 0

mientras !ct.IsCancellationRequested:
    Publicar(Conectando, mensaje: intento == 0 ? null : $"Reintentando conexión (intento {intento + 1})…")
    detector.Reiniciar()
    lector = _fabrica.Crear(config)
    try:
        // --- conexión con tiempo límite ---
        using ctsConexion = new CancellationTokenSource(timeout, _timeProvider)
        using enlazado = CancellationTokenSource.CreateLinkedTokenSource(ct, ctsConexion.Token)
        try: await lector.ConectarAsync(enlazado.Token)
        catch OperationCanceledException when !ct.IsCancellationRequested:
            throw new ConexionBalanzaException($"Tiempo de espera agotado al conectar con {descripcion}.")
        _logger.LogInformation("Balanza conectada ({Descripcion})", descripcion); _ultimoMensajeRegistrado = null
        Publicar(Conectada)                                // aún sin peso
        ultimaValida = _timeProvider.GetUtcNow(); vencimientosSeguidos = 0

        // --- lectura ---
        mientras true:
            using ctsTrama = new CancellationTokenSource(timeout, _timeProvider)
            using enlazadoTrama = CreateLinkedTokenSource(ct, ctsTrama.Token)
            string trama
            try: trama = await lector.LeerTramaAsync(enlazadoTrama.Token)
            catch OperationCanceledException when !ct.IsCancellationRequested:
                vencimientosSeguidos++
                detector.Reiniciar()
                Publicar(SinDatos, mensaje: "La balanza no envía datos.")
                si vencimientosSeguidos >= TimeoutsAntesDeReconectar:
                    throw new ConexionBalanzaException("La balanza no envía datos; se reintentará la conexión.")
                continuar
            vencimientosSeguidos = 0
            intento = 0                                   // conexión sana: reinicia la espera de reintentos (D7)
            RegistrarTramaReciente(trama)                 // TramaVisible.Formatear + LogDebug
            ahora = _timeProvider.GetUtcNow()
            r = parser.Parsear(trama)
            si !r.Valida:
                si ahora - ultimaValida >= timeout:
                    detector.Reiniciar()
                    Publicar(TramasNoReconocidas, mensaje: r.Error)
                continuar
            ultimaValida = ahora
            estable = detector.Agregar(r.PesoKg!.Value, r.EstableReportado)
            Publicar(Conectada, peso: r.PesoKg, estable: estable, instante: ahora)
    catch OperationCanceledException when ct.IsCancellationRequested:
        salir del bucle
    catch ConexionBalanzaException ex:
        RegistrarFallo(ex.Message, ex)            // LogWarning solo si el mensaje cambió (D13)
        Publicar(Error, mensaje: ex.Message)
    catch Exception ex when ex is not OperationCanceledException:
        // un lector mal implementado que deja escapar IOException, SocketException, etc.
        RegistrarFallo("Error de comunicación con la balanza.", ex)
        Publicar(Error, mensaje: "Error de comunicación con la balanza.")
    finally:
        await DesecharSeguroAsync(lector)         // try/catch: un fallo al cerrar se registra y no interrumpe

    si ct.IsCancellationRequested: salir
    espera = EsperasReconexionSegundos[min(intento, último índice)] segundos
    si config.TipoConexion == Serial: espera = max(espera, EsperaTrasCerrarPuertoMs)   // R2 (hoy siempre ≥ 1 s; la regla protege cambios futuros)
    intento++
    try: await Task.Delay(espera, _timeProvider, ct)
    catch OperationCanceledException: salir
```

Notas obligatorias:
- Todo `await` con `.ConfigureAwait(false)` (regla 0.12).
- El `CancellationTokenSource(TimeSpan, TimeProvider)` de cada trama se crea y se desecha en cada vuelta (5–20 por segundo: aceptable). **No** reutilices uno con `CancelAfter`: `CancelAfter` usa tiempo real e ignora el `TimeProvider`, y los tests con `FakeTimeProvider` dejarían de ser deterministas.
- En estados distintos de `Conectada`, `Publicar` ya anula peso y estabilidad (6.1): no hace falta repetirlo.
- La estructura "una instancia de lector por intento" es obligatoria: garantiza que las tramas de una conexión vieja nunca se mezclen con las de la nueva.

### 6.4 `ProbarAsync`

```text
_sesion.ExigirPermiso(ConfigurarBalanza)
candidata = ValidadorConfiguracionBalanza.Normalizar(candidata)     // a partir de aquí se usa la normalizada
errores = ValidadorConfiguracionBalanza.Validar(candidata)
si hay errores → throw new ValidacionException(errores)
await _control.WaitAsync(ct)
try:
    estabaIniciado = _bucle != null
    await DetenerInternoAsync()
    Publicar(EnDiagnostico, descripcion de la candidata, mensaje: "Probando configuración…")
    si candidata.TipoConexion == Serial:
        await Task.Delay(TimeSpan.FromMilliseconds(EsperaTrasCerrarPuertoMs), _timeProvider, CancellationToken.None)  // R2
        // Con FakeTimeProvider esta espera no avanza sola: en los tests usa candidatas Tcp o Simulador
        // (la fábrica falsa ignora el tipo), o avanza el reloj con Esperar.AvanzandoHastaAsync.
    return await EjecutarPruebaAsync(candidata, duracion, progreso, ct)
finally:
    si estabaIniciado: try { await IniciarInternoAsync(CancellationToken.None) } catch (Exception ex) { LogError; Publicar(Error, "No se pudo reanudar la balanza: …") }
    sino: Publicar(Detenida)
    _control.Release()
```

`EjecutarPruebaAsync`: crea el lector con la fábrica, `ParserTramas` y `DetectorEstabilidad` de la **candidata**, `using ctsDuracion = new CancellationTokenSource(duracion, _timeProvider)` enlazado con `ct`. Conecta (con el mismo tiempo límite de 6.3); lee tramas hasta que venza la duración, contando recibidas y válidas, guardando el último peso y si alguna vez fue estable, e informando `progreso?.Report(new AvancePruebaBalanza(TramaVisible.Formatear(trama), resultado, estable))`. Un vencimiento de `TimeoutMs` sin tramas durante la prueba **no** la termina (sigue esperando hasta la duración). Resultado:
- Cancelado por `ct` (el usuario) → `Cancelada = true` con los contadores acumulados.
- Vence la duración → `Conecto = true`, `Cancelada = false`.
- `ConexionBalanzaException` al conectar o leer → `Conecto` según haya llegado a conectar, `Error = ex.Message`.
- El lector se desecha siempre (`finally`).

Ninguna de esas salidas lanza. Solo lanzan `NoAutorizadoException` y `ValidacionException` (antes de pausar nada).

### 6.5 Tests de `ServicioBalanza` (`ServicioBalanzaTests`)

**Dobles (en `tests/PesajeFaeneo.Tests/Utilidades/Balanza/`):**
- `LectorBalanzaFalso : IScaleReader` con un `Channel<string>` sin límite que el test alimenta (`Enviar(string)`), y propiedades configurables: `ExcepcionAlConectar`, `Descripcion`, `Desechado` (bool), `Conectado` (bool), `FallarLectura(Exception)` (completa el canal con esa excepción; `LeerTramaAsync` debe relanzarla tal cual, no envuelta en `ChannelClosedException`: captúrala y relanza `InnerException`).
- `FabricaLectorFalsa : IFabricaLectorBalanza` que devuelve lectores de una cola preparada por el test (o crea uno nuevo si la cola está vacía), guarda `ConfiguracionesRecibidas` y expone `Creados` (lista de lectores en orden). Segura entre hilos (`lock`).
- `Esperar` (estática):
  ```csharp
  public static async Task HastaAsync(Func<bool> condicion, string descripcion, TimeSpan? limite = null)
  // sondea cada 10 ms en tiempo real; si a los 5 s (o `limite`) no se cumple → Assert.Fail($"No se cumplió: {descripcion}")
  public static async Task AvanzandoHastaAsync(FakeTimeProvider reloj, TimeSpan paso, Func<bool> condicion, string descripcion)
  // repite: si condicion() → return; reloj.Advance(paso); await Task.Delay(10); hasta 5 s reales → Assert.Fail
  ```
- La BD: `BaseDatosEnMemoria.CrearAsync()` (el servicio lee la configuración sembrada). Para cambiar la configuración usa el `ContextFactory` del fixture.
- `ServicioBalanza` siempre en `using` (o `Dispose()` en `finally`) para que ningún bucle sobreviva al test.

**Casos mínimos** (con `FakeTimeProvider` salvo que se indique):
1. `Iniciar_PublicaConectandoYLuegoConectada`.
2. `CincoTramasIguales_PublicaPesoEstable` (N=5, tol=0,5 sembrados): envía `"10.0"` ×5 → `Estado == Conectada`, `PesoKg == 10.0m`, `Estable`.
3. `TramasQueVarianMasQueLaTolerancia_NoEsEstable`.
4. `TramasInvalidas_TrasTimeout_PublicaTramasNoReconocidas` (envía `"ABC"`, avanza `TimeoutMs` y envía otra `"ABC"`).
5. `SinTramas_TrasTimeout_PublicaSinDatosYAnulaElPeso` (estaba estable → pasa a `SinDatos`, `PesoKg == null`, `Estable == false`).
6. `TresTimeoutsSeguidos_Reconecta`: el primer lector queda `Desechado` y `Creados.Count` llega a 2 tras avanzar la espera de 1 s.
7. `FalloAlConectar_PublicaErrorConMensaje_YEsperaAntesDeReintentar`: tras el fallo, `Creados.Count == 1` después de avanzar 900 ms y `== 2` después de avanzar 100 ms más.
8. `EsperasDeReintentoCrecen_1_2_4` y `SeReiniciaTrasRecibirUnaTrama`.
9. `ErrorDuranteLectura_PublicaError_YReconecta`.
10. `LectorQueLanzaIOException_NoRompeElBucle` (excepción no `ConexionBalanzaException`).
11. `Detener_PublicaDetenida_DesechaElLector_YNoReconecta` (avanza 60 s tras detener: `Creados.Count` no cambia).
12. `IniciarDosVeces_UnSoloBucle`.
13. `Reiniciar_RelecturaDeConfiguracion`: cambia `PuertoCom` en la BD, `ReiniciarAsync` → la última `ConfiguracionesRecibidas` tiene el valor nuevo.
14. `PatronInvalidoEnBd_PublicaError_SinCrearLector` (escribe un patrón sin grupo `peso` con SQL crudo: la CK no valida regex).
15. `Secuencia_EsEstrictamenteCreciente` y `TramasRecientes_SeLimitanA50`.
16. `ProbarAsync_SinPermiso_LanzaNoAutorizado` (sesión de Supervisor con `SesionesDePrueba`) y `ProbarAsync_ConfiguracionInvalida_LanzaValidacion_SinPausar` (el lector principal sigue sin desecharse).
17. `ProbarAsync_PausaElPrincipal_UsaLaCandidata_Informa_YReanuda`: durante la prueba `Instantanea.Estado == EnDiagnostico` y el lector principal está desechado; `progreso` recibe las tramas enviadas; al terminar la duración el resultado trae los contadores y se creó un lector nuevo con la configuración **de la BD**. Usa un `IProgress<T>` escrito a mano que agregue a una lista con `lock` (no `Progress<T>`: sin contexto de sincronización publica en el pool y el test no sabe cuándo llegó).
18. `ProbarAsync_Cancelada_DevuelveCancelada_YReanuda`.
19. `ProbarAsync_FalloDeConexion_DevuelveError_YReanuda`.
20. `ProbarAsync_ServicioNoIniciado_NoLoIniciaAlTerminar` (queda `Detenida`).
21. `Dispose_DetieneElBucle` (tiempo real; `Esperar.HastaAsync` sobre `Desechado`).
22. `EsCapturable`: tabla de casos puros sobre `InstantaneaBalanza` (estado, estable, peso nulo, lectura vieja por 1 ms más que la antigüedad máxima).

---

## Parte 7 — HU-F3-04: lectores Serial y TCP

**Carpeta:** `Infrastructure/Balanza/`. Todas las clases `internal sealed` salvo la fábrica y el enumerador (que se registran en DI; pueden ser `internal` si el registro está en el mismo ensamblado — lo está).

### 7.1 `LectorBalanzaBase` (abstracta, `internal`)

- Implementa `IScaleReader`. Contiene el `Channel<string>` (`Channel.CreateBounded<string>(new BoundedChannelOptions(ReglasBalanza.CapacidadCanalTramas) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true, SingleWriter = true })`) y un `SeparadorTramas`.
- `protected void EntregarBytes(byte[] buffer, int cantidad)` → separa y escribe cada trama con `TryWrite`.
- `protected void EntregarTrama(string trama)` → `TryWrite` (lo usa el simulador).
- `protected void Finalizar(Exception? error)` → `Writer.TryComplete(error)`; idempotente.
- `public async ValueTask<string> LeerTramaAsync(CancellationToken ct)` → `await _canal.Reader.ReadAsync(ct)`; captura `ChannelClosedException ex` y lanza `ex.InnerException as ConexionBalanzaException ?? new ConexionBalanzaException("Se perdió la conexión con la balanza.", ex)` (R15). **No** captures `OperationCanceledException`.
- `DisposeAsync` abstracto en derivadas, que al final llaman `Finalizar(null)`.

### 7.2 `LectorSerial`

Para poder probarlo sin hardware, el acceso al puerto pasa por un adaptador:

```csharp
internal interface IPuertoSerie : IDisposable
{
    string Nombre { get; }
    void Abrir();                                  // Open() + DiscardInBuffer()
    int Leer(byte[] buffer, int offset, int count); // bloqueante; TimeoutException si no hay datos en ReadTimeout
    void Cerrar();                                  // Close(); no lanza
}

internal sealed class PuertoSerieSystem : IPuertoSerie   // envuelve System.IO.Ports.SerialPort con la configuración D14
```

`PuertoSerieSystem` mapea los enums **con `switch` explícito** (no con cast, aunque los valores numéricos coincidan hoy): `ParidadSerial.None → Parity.None`, `Odd → Odd`, `Even → Even`, `Mark → Mark`, `Space → Space`; `BitsParada.One → StopBits.One`, `Two → StopBits.Two`, `OnePointFive → StopBits.OnePointFive`; cualquier otro valor → `ArgumentOutOfRangeException`.

`LectorSerial(ConfiguracionBalanzaDto config, ILogger logger)` y un constructor `internal LectorSerial(ConfiguracionBalanzaDto config, ILogger logger, Func<IPuertoSerie> crearPuerto)` para tests.

- `ConectarAsync(ct)`: `ct.ThrowIfCancellationRequested()`; luego `await Task.Run(() => AbrirTraduciendo(), CancellationToken.None).ConfigureAwait(false)`. **No** pases `ct` a `Task.Run` ni uses `WaitAsync(ct)`: `SerialPort.Open` no es cancelable y abandonar la espera dejaría un puerto abierto huérfano que bloquea el siguiente intento (R20). Tras abrir, arranca el hilo de bombeo.
- Traducción de excepciones de `Abrir()` a `ConexionBalanzaException` (mensaje exacto):
  - `UnauthorizedAccessException` → `$"El puerto {Nombre} está en uso por otra aplicación o no hay permiso para abrirlo."`
  - `IOException` (incluye `FileNotFoundException`) → `$"No se pudo abrir el puerto {Nombre}. Verifique que el cable o adaptador esté conectado."`
  - `ArgumentException` → `$"El nombre de puerto '{Nombre}' no es válido."`
  - `InvalidOperationException` → `$"El puerto {Nombre} ya está abierto."`
- Hilo de bombeo: `new Thread(Bombear) { IsBackground = true, Name = "LectorBalanzaSerial" }`. `Bombear`: buffer de 512 bytes; bucle mientras `!_cerrando`: `n = _puerto.Leer(...)`; `TimeoutException` → continuar; `n > 0` → `EntregarBytes`; `IOException`, `InvalidOperationException`, `UnauthorizedAccessException`, `ObjectDisposedException` → si `_cerrando` salir en silencio; si no, `Finalizar(new ConexionBalanzaException($"Se perdió la comunicación con el puerto {Nombre}.", ex))` y salir. Cualquier otra excepción → `logger.LogError` y `Finalizar(new ConexionBalanzaException(...))`. **Una excepción nunca debe escapar del hilo** (terminaría el proceso).
- `DisposeAsync`: `_cerrando = true` (campo `volatile`); `_puerto.Cerrar()` dentro de `try/catch` (desbloquea `Read`); `_hilo?.Join(TimeSpan.FromSeconds(2))` (si no termina, `LogWarning` y sigue); `_puerto.Dispose()` en `try/catch`; `Finalizar(null)`. Idempotente. Todo el trabajo bloqueante dentro de `await Task.Run(...).ConfigureAwait(false)` para no bloquear a quien llame desde un contexto asíncrono.

**Tests `LectorSerialTests`** con `PuertoSerieFalso : IPuertoSerie` (cola de "lecturas" preparadas: bytes, `TimeoutException`, `IOException`; cuando la cola se vacía, `Leer` duerme 5 ms y lanza `TimeoutException`; `Cerrar` marca un flag y hace que el siguiente `Leer` lance `InvalidOperationException`):
- Bytes `"12.3\r\n"` partidos en dos lecturas con un `TimeoutException` en medio → `LeerTramaAsync` devuelve `"12.3"`.
- `Abrir` que lanza cada excepción de la tabla → `ConexionBalanzaException` con el mensaje exacto.
- `IOException` durante la lectura → `LeerTramaAsync` lanza `ConexionBalanzaException` "Se perdió la comunicación…".
- `DisposeAsync` → el puerto quedó cerrado y desechado, el hilo terminó, y no hay excepción aunque `Cerrar` lance.
- `ConectarAsync` con token ya cancelado → `OperationCanceledException` y `Abrir` nunca se llamó.
- El mapeo de enums de `PuertoSerieSystem`: exponlo como método `internal static` y pruébalo con todos los valores, **sin** abrir un puerto real.

### 7.3 `LectorTcp`

- `ConectarAsync(ct)`: `_cliente = new TcpClient { NoDelay = true }`; `_cliente.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true)`; `await _cliente.ConnectAsync(host, puerto, ct)`. `SocketException ex` → `ConexionBalanzaException($"No se pudo conectar a {host}:{puerto} ({ex.SocketErrorCode}).", ex)`. La cancelación (`OperationCanceledException`) se propaga tal cual (el servicio la convierte en "Tiempo de espera agotado…"). Luego arranca `_bombeo = Task.Run(BombearAsync)`.
- `BombearAsync`: buffer 512 bytes; `n = await stream.ReadAsync(buffer, _ctsInterno.Token)`; `n == 0` → `Finalizar(new ConexionBalanzaException("La balanza cerró la conexión."))`; `IOException`/`SocketException`/`ObjectDisposedException` → si `_cerrando` salir, si no `Finalizar(new ConexionBalanzaException($"Se perdió la conexión con {host}:{puerto}.", ex))`; `OperationCanceledException` con `_cerrando` → salir. Ninguna excepción escapa de la tarea.
- `DisposeAsync`: `_cerrando = true`; `_ctsInterno.Cancel()`; `_cliente?.Dispose()`; `await _bombeo.WaitAsync(TimeSpan.FromSeconds(2))` en `try/catch`; `Finalizar(null)`. Idempotente.

**Tests `LectorTcpTests`** con un `TcpListener(IPAddress.Loopback, 0)` real en el test (puerto efímero; lee el puerto asignado de `((IPEndPoint)listener.LocalEndpoint).Port`):
- Conecta, el servidor envía `"12.3\r\n45.6\r\n"` → dos tramas.
- El servidor cierra el socket → `LeerTramaAsync` lanza `ConexionBalanzaException` "La balanza cerró la conexión."
- Puerto sin nadie escuchando (abre un listener, toma su puerto y detenlo antes de conectar) → `ConexionBalanzaException` con `ConnectionRefused` en el mensaje.
- `DisposeAsync` con una lectura pendiente termina en menos de 2 s.
- Todos con `CancellationTokenSource` de 5 s como red de seguridad para que el test no se cuelgue.

### 7.4 `FabricaLectorBalanza : IFabricaLectorBalanza` y `EnumeradorPuertosSerie : IEnumeradorPuertosSerie`

- Fábrica (dependencias: `ISimuladorBalanza`, `TimeProvider`, `ILoggerFactory`): `switch (config.TipoConexion)` → `LectorSerial` / `LectorTcp` / `LectorSimulador`; otro valor → `ArgumentOutOfRangeException`.
- Enumerador: `SerialPort.GetPortNames()` en `try/catch` (→ lista vacía + `LogWarning`); `Trim()`, `ToUpperInvariant()`, descarta los que no cumplan `ReglasBalanza.PatronPuertoCom` (algunos drivers devuelven nombres con basura al final), `Distinct`, ordena por el número tras "COM".
- Tests: la fábrica devuelve el tipo correcto para cada `TipoConexion` (sin conectar); el ordenamiento y filtrado del enumerador, exponiendo la parte pura como `internal static IReadOnlyList<string> Normalizar(IEnumerable<string> nombres)` (`["COM10", "com2 ", "COM2", "COM3\0x"]` → `["COM2", "COM10"]`).

---

## Parte 8 — HU-F3-05: simulador de balanza

**Carpeta:** `Infrastructure/Balanza/Simulador/`.

### 8.1 `GeneradorPesoSimulado` (`internal static`, puro)

```csharp
internal static decimal Siguiente(decimal actual, decimal objetivo, Random aleatorio)
```
- `diferencia = objetivo − actual`. Si `|diferencia| ≤ 0,2` → devuelve `objetivo` **exacto** (así el detector alcanza la estabilidad con tolerancia 0).
- Si no → `actual + diferencia × 0,35 + ruido`, con `ruido` uniforme en `[−0,5; +0,5]` (`(decimal)(aleatorio.NextDouble() − 0.5)`), redondeado a 0,1 (`AwayFromZero`), recortado a `[−10; PesoMaximoSimuladorKg]`.

Tests (`Random` con semilla fija): desde 0 hacia 120 llega a exactamente 120,0 en ≤ 40 pasos y se queda ahí; desde 120 hacia 0 igual; con objetivo = actual devuelve siempre el objetivo; nunca supera los límites.

### 8.2 `SimuladorBalanza : ISimuladorBalanza` (singleton)

Estado protegido con `lock`: `PesoObjetivoKg` (inicial 0), `DesconexionSimulada` (inicial false). `EstablecerPesoObjetivo` recorta y redondea (contrato de 3.3) y registra `LogInformation("Peso simulado establecido en {Peso} kg", …)`.

### 8.3 `LectorSimulador : LectorBalanzaBase`

Dependencias: `ISimuladorBalanza`, `TimeProvider`, `ILogger`, y un constructor `internal` que recibe el `Random` (tests). `Descripcion = "Simulador"`.
- `ConectarAsync`: si `DesconexionSimulada` → `ConexionBalanzaException("Desconexión simulada.")`. Si no, arranca `Task.Run(BombearAsync)`.
- `BombearAsync`: `actual = 0`; bucle: `await Task.Delay(TimeSpan.FromMilliseconds(IntervaloSimuladorMs), _timeProvider, _ctsInterno.Token)`; si `DesconexionSimulada` → `Finalizar(new ConexionBalanzaException("Desconexión simulada."))` y salir; `actual = GeneradorPesoSimulado.Siguiente(actual, simulador.PesoObjetivoKg, aleatorio)`; `EntregarTrama(actual.ToString("0.0", CultureInfo.InvariantCulture))`. Cancelación → salir en silencio.
- `DisposeAsync`: cancela, espera la tarea (≤ 2 s), `Finalizar(null)`.

Tests: con `FakeTimeProvider`, tras `Advance(200 ms)` llega una trama y es parseable por `ParserTramas` con el patrón por defecto; `DesconexionSimulada = true` a mitad → `LeerTramaAsync` lanza `ConexionBalanzaException`; `ConectarAsync` con desconexión simulada falla.

**Integración:** un test de extremo a extremo `ServicioBalanza` + `FabricaLectorBalanza` real + `SimuladorBalanza` + `FakeTimeProvider`: con el peso objetivo en 150 y avanzando el reloj de a 200 ms, el servicio llega a `Conectada`, `PesoKg == 150.0m`, `Estable == true`.

---

## Parte 9 — HU-F3-06: servicio compartido en la aplicación y entrega segura a la UI

### 9.1 Registro en DI

- `Application/DependencyInjection.cs`: `services.AddSingleton<Balanza.IServicioBalanza, Balanza.ServicioBalanza>();` y `services.AddTransient<Balanza.IServicioConfiguracionBalanza, Balanza.ServicioConfiguracionBalanza>();`
- `Infrastructure/DependencyInjection.cs`: `AddSingleton<IFabricaLectorBalanza, FabricaLectorBalanza>()`, `AddSingleton<ISimuladorBalanza, SimuladorBalanza>()`, `AddSingleton<IEnumeradorPuertosSerie, EnumeradorPuertosSerie>()`.
- `UnidadDeTrabajo`: propiedad `ConfiguracionBalanza => _configuracionBalanza ??= new ConfiguracionBalanzaRepositorio(_context)`; repositorio nuevo en `Infrastructure/Persistencia/Repositorios/ConfiguracionBalanzaRepositorio.cs` (copia el estilo de `ParametrosRepositorio`).
- `ContenedorDITests`: agrega aserciones para `IServicioBalanza`, `IServicioConfiguracionBalanza`, `IFabricaLectorBalanza`, `ISimuladorBalanza` e `IEnumeradorPuertosSerie` (la de `FrmConfiguracionBalanza` se agrega en 10.5). `IServicioConfiguracionBalanza` se crea en la Parte 10.2: en esta Parte registra y verifica solo lo que ya existe, y completa el registro en 10.5. Verifica además que resolver `IServicioBalanza` dos veces devuelve **la misma** instancia. Desecha el proveedor al final (ya lo hace con `using`): comprueba que ese `Dispose` síncrono no lanza (R5).

### 9.2 Arranque y parada en `Program.Main`

Después del bloque "5. Inicializar BD y Seeder inicial" y **antes** de crear `ContextoAplicacion`:

```csharp
// 6. Balanza: se inicia después de migrar (lee su configuración de la BD) y nunca impide abrir la
// aplicación; si falla, la barra de estado lo mostrará y el administrador podrá corregirlo.
var servicioBalanza = host.Services.GetRequiredService<IServicioBalanza>();
try
{
    servicioBalanza.IniciarAsync().GetAwaiter().GetResult();
}
catch (Exception ex)
{
    Log.Error(ex, "No se pudo iniciar el servicio de balanza. La aplicación continúa sin lectura de balanza.");
}
```

Después de `WinFormsApp.Run(contexto);` y **antes** de `host.StopAsync()`:

```csharp
try
{
    servicioBalanza.DetenerAsync().WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
}
catch (Exception ex)
{
    Log.Warning(ex, "La balanza no se detuvo correctamente al salir.");
}
```

Renumera los comentarios siguientes. **No** uses `IHostedService`: `host.Start()` se ejecuta **antes** de migrar la BD y el servicio leería una BD sin esquema (R4). Bloquear aquí con `GetResult()` es seguro solo porque todo el código de balanza usa `ConfigureAwait(false)` y corre en el pool (R6); si el cierre se cuelga en la prueba de arranque (Parte 11), revisa esa regla primero.

### 9.3 `WinForms/Infraestructura/MonitorBalanzaUi.cs`

```csharp
/// Sondea IServicioBalanza.Instantanea desde el hilo de UI (System.Windows.Forms.Timer) y avisa solo
/// cuando cambió. Debe crearse y usarse en el hilo de UI. El evento se dispara siempre en ese hilo.
public sealed class MonitorBalanzaUi : IDisposable
{
    public MonitorBalanzaUi(IServicioBalanza servicio, int intervaloMs = ReglasBalanza.IntervaloMonitorUiMs);
    public InstantaneaBalanza? Ultima { get; }
    public event EventHandler<InstantaneaBalanza>? Actualizado;
    public void Iniciar();   // arranca el Timer y hace un Sondear() inmediato
    public void Detener();
    internal void Sondear(); // lee la instantánea; si Secuencia != Ultima?.Secuencia, actualiza y dispara Actualizado
    public void Dispose();   // Detener + Dispose del Timer; idempotente; después, Sondear no hace nada
}
```

- `Sondear` envuelve la invocación del evento en `try/catch (Exception ex)`: registra con Serilog `Log.Warning` **una sola vez** por tipo de excepción (campo `HashSet<Type>`) y sigue. Motivo: una excepción en `Tick` iría a `Application.ThreadException` → `MessageBox` cada 100 ms (R8).
- El formulario dueño llama `Dispose()` en su `Dispose(bool disposing)`.

### 9.4 `WinForms/Infraestructura/PresentacionBalanza.cs` (estática, pura)

- `string TextoEstado(InstantaneaBalanza i)` → prefijo `"[SIMULADOR] "` si `EsSimulador`, y luego:
  - `Detenida` → `"Balanza: detenida"`
  - `Conectando` → `"Balanza: conectando…"` (o el `Mensaje` si existe, p. ej. el de reintento)
  - `Conectada` → `$"Balanza: conectada ({Descripcion})"`
  - `SinDatos` → `"Balanza: sin datos"`
  - `TramasNoReconocidas` → `"Balanza: tramas no reconocidas"`
  - `Error` → `$"Balanza: error — {Mensaje}"`
  - `EnDiagnostico` → `"Balanza: en diagnóstico"`
- `string TextoPeso(InstantaneaBalanza i)` → `"---- kg"` si `PesoKg` es nulo; si no, `PesoKg.Value.ToString("0.0", CultureInfo.CurrentCulture) + " kg"`.
- `Color ColorEstado(InstantaneaBalanza i)` → `Conectada` y estable: verde oscuro; `Conectada` inestable: `SystemColors.ControlText`; `Error`/`SinDatos`/`TramasNoReconocidas`: rojo oscuro; resto: gris. Si `EsSimulador`, el fondo del indicador en la barra de estado es naranja claro (lo aplica `FrmPrincipal`).
- Tests `PresentacionBalanzaTests`: un caso por estado, con y sin simulador, y `TextoPeso` nulo/no nulo (fija `CultureInfo.CurrentCulture` en el test y restáurala en `finally`).

### 9.5 `FrmPrincipal`

- Nuevo parámetro de constructor `IServicioBalanza servicioBalanza` (al final; `ArgumentNullException`).
- Nuevo `ToolStripStatusLabel lblStatusBalanza` (con borde, como los demás), insertado **después** de `lblStatusRol` y antes del separador `Spring`. Expón `internal ToolStripStatusLabel LblStatusBalanza => lblStatusBalanza;`.
- Crea `_monitorBalanza = new MonitorBalanzaUi(servicioBalanza)` en el constructor; en `Actualizado` actualiza `lblStatusBalanza.Text = $"{PresentacionBalanza.TextoEstado(i)}  {PresentacionBalanza.TextoPeso(i)}"`, `ForeColor` y `BackColor` (naranja claro si `EsSimulador`; `SystemColors.Control` si no). Llama `_monitorBalanza.Iniciar()` en `Shown` **fuera** de `EjecutarAsync` (no es una operación del usuario; si entrara en `EjecutarAsync` y `ActualizarEmpresaAsync` estuviera en curso, se descartaría en silencio — regla 0.10).
- `Dispose(bool)`: `_monitorBalanza?.Dispose()` junto al del reloj.
- El ítem de menú "Configuración de balanza" **no** se toca en esta Parte (sigue con `FaseFutura = "F3"`); se activa en 10.5, cuando el formulario ya existe.
- **Tests existentes:** `FrmPrincipalTests` construye `FrmPrincipal` a mano: agrega un `ServicioBalanzaFalso` (en `tests/.../Utilidades/Balanza/`, implementa toda la interfaz; `Instantanea` con setter público para el test; los métodos de control registran llamadas; `ProbarAsync` devuelve un resultado configurable y puede esperar un `TaskCompletionSource` que el test controla). Si hay aserciones sobre el ítem "Configuración de balanza" deshabilitado con "F3", cámbialas a habilitado.
- **Tests nuevos:** `LblStatusBalanza` refleja una instantánea de `Conectada` con peso tras `Sondear()` (expón `internal MonitorBalanzaUi MonitorBalanza => _monitorBalanza;`); con `EsSimulador = true` el texto empieza por `[SIMULADOR]`.

### 9.6 Tests de `MonitorBalanzaUi` (STA, `ServicioBalanzaFalso`)

- `Sondear` sin cambio de `Secuencia` no dispara el evento; con cambio lo dispara una vez con la instantánea nueva.
- Un manejador que lanza no propaga la excepción y el siguiente `Sondear` con cambio vuelve a disparar.
- Tras `Dispose`, `Sondear` no dispara y un segundo `Dispose` no lanza.

---

## Parte 10 — HU-F3-07: configuración y diagnóstico de balanza

### 10.1 `Application/Balanza/ValidadorConfiguracionBalanza.cs` (estática, pura)

```csharp
public static ConfiguracionBalanzaDto Normalizar(ConfiguracionBalanzaDto dto); // PuertoCom: Trim+ToUpperInvariant; HostTcp: Trim; PatronRegex: SIN cambios
public static IReadOnlyList<ErrorValidacion> Validar(ConfiguracionBalanzaDto dtoNormalizado);
```

| Campo | Regla | Mensaje |
|---|---|---|
| TipoConexion | `Enum.IsDefined` | "Seleccione el tipo de conexión." |
| PuertoCom | cumple `PatronPuertoCom` | "Ingrese un puerto válido (COM1 a COM999)." |
| BaudRate | en `BaudiosPermitidos` | "Seleccione una velocidad válida." |
| DataBits | en `BitsDatosPermitidos` | "Los bits de datos deben ser 7 u 8." |
| Paridad | `Enum.IsDefined` | "Seleccione la paridad." |
| BitsParada | en `BitsParadaPermitidos` | "Los bits de parada deben ser 1 o 2." |
| HostTcp | no vacío, ≤ 100, `Uri.CheckHostName(host)` ∈ {`Dns`, `IPv4`, `IPv6`} | "Ingrese una dirección IP o nombre de equipo válido." |
| PuertoTcp | 1..65535 | "El puerto TCP debe estar entre 1 y 65535." |
| PatronRegex | `ParserTramas.ValidarPatron` == null | el mensaje que devuelva `ValidarPatron` |
| LecturasParaEstable | 1..50 | "Las lecturas para estabilidad deben estar entre 1 y 50." |
| ToleranciaKg | 0..10 y como máximo 1 decimal (`t == Math.Round(t, 1)`) | "La tolerancia debe estar entre 0,0 y 10,0 kg (un decimal)." |
| TimeoutMs | 500..60000 | "El tiempo sin datos debe estar entre 500 y 60000 ms." |

Se validan **todos** los campos siempre (D11), también los del transporte no elegido. Tests `ValidadorConfiguracionBalanzaTests`: la configuración sembrada es válida; un caso inválido por fila (con el `Campo` esperado); casos límite válidos (COM1, COM999, 1, 65535, 1, 50, 0, 10, 500, 60000, host `"localhost"`, `"192.168.1.100"`, `"::1"`); `"COM0"`, `"COM1000"`, `"LPT1"`, host `"con espacios"`, tolerancia `0.25` inválidos; `Normalizar` convierte `" com3 "` en `"COM3"` y no toca el patrón.

### 10.2 `Application/Balanza/ServicioConfiguracionBalanza.cs`

Dependencias: `IUnidadDeTrabajoFactory`, `ISesionActual`, `IServicioBalanza`, `IEnumeradorPuertosSerie`, `TimeProvider`, `ILogger<ServicioConfiguracionBalanza>`.

- `ObtenerAsync`: `ExigirPermiso(ConfigurarBalanza)`; lee y devuelve `new ConfiguracionBalanzaLeida(MapeoBalanza.ADto(e), e.ModificadoEn)`.
- `ListarPuertosSerie`: `ExigirPermiso(ConfigurarBalanza)`; delega en el enumerador.
- `ActualizarAsync(dto)`, **en este orden** (copia la estructura de `ServicioParametros.ActualizarAsync`):
  1. `ArgumentNullException.ThrowIfNull(dto)`; `ExigirPermiso(ConfigurarBalanza)`; `sesion = ExigirSesion()`.
  2. `n = Normalizar(dto)`; `errores = Validar(n)`; si hay → `ValidacionException(errores)`.
  3. `await using var uow`; `e = await uow.ConfiguracionBalanza.ObtenerAsync()`; `antes = FabricaAuditoria.CrearSnapshot(e)`.
  4. `MapeoBalanza.Aplicar(n, e)`; `despues = CrearSnapshot(e)`; si `antes == despues` → `return false` (sin guardar, sin auditar, sin reiniciar).
  5. `e.ModificadoEn = _timeProvider.GetUtcNow().UtcDateTime`; registra la auditoría (D13) y `await uow.GuardarCambiosAsync()`.
  6. `LogInformation("Configuración de balanza actualizada por {Usuario}: {Descripcion}", …)`.
  7. **Después** de guardar y con la unidad de trabajo ya desechada (sal del `await using` antes de este paso): `try { await _servicioBalanza.ReiniciarAsync(ct); } catch (Exception ex) { _logger.LogError(ex, "…"); }` — un fallo al reiniciar **no** deshace ni invalida el guardado; la barra de estado mostrará el problema.
  8. `return true`.

Tests `ServicioConfiguracionBalanzaTests` (BD en memoria, `SesionesDePrueba`, `FakeTimeProvider`, `ServicioBalanzaFalso`, enumerador falso):
- `ObtenerAsync`/`ActualizarAsync`/`ListarPuertosSerie` con Operador y con Supervisor → `NoAutorizadoException`; con Administrador `ObtenerAsync` devuelve la configuración sembrada (patrón nuevo incluido).
- Actualizar válido → persistido, `" com3 "` guardado como `"COM3"`, `ModificadoEn` = hora del reloj falso, **una** auditoría `CambioConfigBalanza` con `EntidadId = "1"`, `UsuarioId` de la sesión, JSON antes/después con el `PuertoCom` viejo/nuevo; `ReiniciarAsync` llamado **una** vez; devuelve `true`.
- Sin cambios → devuelve `false`, sin auditoría nueva, sin `ReiniciarAsync`.
- DTO inválido → `ValidacionException` con el campo correcto, nada persistido, sin `ReiniciarAsync`.
- `ReiniciarAsync` que lanza → `ActualizarAsync` devuelve `true` sin lanzar y el cambio quedó persistido.

### 10.3 `WinForms/Formularios/Administracion/FrmConfiguracionBalanza.cs : FormBase`

Dependencias: `IServicioConfiguracionBalanza`, `IServicioBalanza`, `ISimuladorBalanza`. Diálogo modal, tamaño fijo (~900×650), `FormBorderStyle.FixedDialog`, controles `internal` (como `FrmParametros`). Diseño por secciones:

1. **Conexión** (`GroupBox`): `cmbTipoConexion` (`DropDownList`; textos "Serie (RS-232 / USB)", "Red (TCP/IP)", "Simulador"; el valor real se guarda en un par texto-enum, no por índice).
   - Subgrupo **Serie**: `cmbPuerto` (`DropDown` editable, con los puertos de `ListarPuertosSerie()` **más** el puerto guardado aunque no esté presente — R21), `btnRefrescarPuertos`, `cmbBaudios` (`BaudiosPermitidos`), `cmbBitsDatos` (7, 8), `cmbParidad` (Ninguna, Impar, Par, Marca, Espacio), `cmbBitsParada` (1, 2).
   - Subgrupo **Red**: `txtHost`, `numPuertoTcp` (1..65535).
   - Los subgrupos están **siempre visibles**; el que no corresponde al tipo elegido queda `Enabled = false` (así un error de validación en él sigue siendo visible; R16).
2. **Lectura**: `txtPatron` (ancho completo, fuente monoespaciada), `numLecturas` (1..50), `numTolerancia` (0..10, `DecimalPlaces = 1`, `Increment = 0.1`), `numTimeout` (500..60000, `Increment = 500`). Etiqueta de ayuda bajo el patrón: "Debe incluir (?<peso>...). Opcional: (?<estable>...) si la balanza indica estabilidad."
3. **Probar patrón** (sin puerto): `txtTramaEjemplo`, `btnProbarPatron`, `lblResultadoPatron`. Usa directamente `ParserTramas.ValidarPatron` y `new ParserTramas(p).Parsear(trama)`; muestra "Peso: 123.4 kg — estable: sí/no/no informado" o el error. Envuelto en `Ejecutar`.
4. **Diagnóstico**: `btnProbarConexion`, `btnCancelarPrueba` (deshabilitado salvo durante la prueba), `lblEstadoPrueba`, `lblPesoPrueba` (fuente grande), `lstTramas` (`ListBox`, máx. 50 ítems: al superar, quita el primero).
5. **Balanza en uso** (estado del servicio compartido): `lblEstadoActual`, `lblPesoActual` (fuente grande) actualizados por un `MonitorBalanzaUi` propio del formulario (creado en el constructor, iniciado en `Shown`, desechado en `Dispose`), y `btnReconectar` (`EjecutarAsync(() => _servicioBalanza.ReiniciarAsync(), btnReconectar)`).
6. **Simulador** (`GroupBox`, `Visible` solo si el **tipo guardado** es `Simulador`, es decir, el que está usando el servicio): `numPesoObjetivo` (0..2000, 1 decimal), `btnAplicarPeso`, `btnCargaAleatoria` ("Simular carga": objetivo aleatorio entre 80,0 y 160,0 kg), `btnVaciar` (objetivo 0), `chkDesconexion` (→ `DesconexionSimulada`). Estas acciones son síncronas y locales: **sin** `Ejecutar`, para que funcionen incluso durante una prueba.
7. Botones inferiores: `btnGuardar` (`AcceptButton`), `btnCerrar` (`CancelButton`, `DialogResult.Cancel`). `ErrorProvider _errorProvider` y diccionario `CamposBalanza.* → control`.

**Comportamiento:**
- `Shown` → `await EjecutarAsync(CargarAsync)` y `_monitor.Iniciar()` (este último fuera de `EjecutarAsync`).
- `CargarAsync()` (no se envuelve a sí mismo, regla 0.10): `ObtenerAsync()` + `ListarPuertosSerie()`; vuelca el DTO en los controles; guarda `_configuracionCargada = dto`; aplica `Enabled` de los subgrupos y la visibilidad del simulador.
- `LeerDtoDeControles()`: construye el `ConfiguracionBalanzaDto` desde los controles **sin** normalizar (lo hace el servicio).
- **Guardar** (`EjecutarAsync(GuardarInternoAsync, btnGuardar)`): `_errorProvider.Clear()`; `cambio = await ActualizarAsync(dto)`; `ValidacionException` → `MostrarErroresValidacion` (patrón de `FrmParametros`: captura dentro de la lambda); si `cambio` → `_configuracionCargada = dto` y `MessageBox` "Configuración guardada. La balanza se reinició con la nueva configuración."; si no → "No hay cambios para guardar.". El formulario **queda abierto** para observar la balanza. Tras guardar, actualiza la visibilidad del grupo Simulador según el nuevo tipo.
- **Probar conexión** (`EjecutarAsync(ProbarConexionInternoAsync, btnProbarConexion)`):
  1. `_errorProvider.Clear()`; `_ctsPrueba = new CancellationTokenSource()`; `_pruebaEnCurso = true`; `lstTramas.Items.Clear()`; deshabilita `grpConexion`, `grpLectura`, `btnGuardar`, `btnProbarPatron`, `btnReconectar`; habilita `btnCancelarPrueba`; `lblEstadoPrueba.Text = "Probando…"`.
  2. `var progreso = new Progress<AvancePruebaBalanza>(MostrarAvance);` — **creado en el hilo de UI**, así sus callbacks vuelven al hilo de UI.
  3. `resultado = await _servicioBalanza.ProbarAsync(LeerDtoDeControles(), TimeSpan.FromSeconds(ReglasBalanza.DuracionPruebaSegundos), progreso, _ctsPrueba.Token)`; `ValidacionException` → `MostrarErroresValidacion`.
  4. `finally`: `_pruebaEnCurso = false` **antes** de mostrar el resultado; restaura los `Enabled`; `btnCancelarPrueba.Enabled = false`; desecha `_ctsPrueba`.
  5. Muestra el resultado en `lblEstadoPrueba`: "Conexión correcta: {válidas} de {recibidas} tramas reconocidas. Estabilidad: alcanzada/no alcanzada.", "Prueba cancelada.", o "No se pudo conectar: {Error}". Si `TramasRecibidas > 0 && TramasValidas == 0` agrega "Revise el patrón de lectura.".
  6. Si `_cerrarAlTerminar` → `Close()`.
- `MostrarAvance(AvancePruebaBalanza a)`: si `!_pruebaEnCurso || IsDisposed` → return (descarta avances rezagados que `Progress<T>` publica después de que la prueba terminó; R17). Si no, agrega `a.TramaVisible` a `lstTramas` (máx. 50), actualiza `lblPesoPrueba` y su color según `a.Estable`.
- **Cancelar prueba**: manejador **plano**, `_ctsPrueba?.Cancel()` — **sin** `Ejecutar`/`EjecutarAsync` (se descartaría porque la prueba ya tiene `_ocupado = true`).
- **Cerrar el formulario durante la prueba** (`FormClosing`): si `_pruebaEnCurso` → `_ctsPrueba?.Cancel()`, `_cerrarAlTerminar = true`, `e.Cancel = true` (el formulario se cierra solo al terminar la prueba, que ya reanudó la balanza; R18).
- **Cambios sin guardar** (`FormClosing`, si no hay prueba en curso y `DialogResult != OK`): si `LeerDtoDeControles() != _configuracionCargada` (igualdad de records) → confirmar "Hay cambios sin guardar. ¿Desea cerrar de todos modos?" (salteable con un gancho `internal bool ConfirmarCierre { get; set; } = true` para tests).
- `cmbTipoConexion.SelectedIndexChanged` → solo aplica `Enabled` de subgrupos (no toca el simulador: su visibilidad depende del tipo **guardado**).
- Ganchos `internal` para tests: `CargarParaTestAsync()`, `GuardarParaTestAsync()`, `ProbarConexionParaTestAsync()`, `CancelarPruebaParaTest()`, `MostrarAvanceParaTest(AvancePruebaBalanza)`, `EstaPruebaEnCurso`.

### 10.4 Tests de `FrmConfiguracionBalanza` (STA, dobles escritos a mano)

- Cargar vuelca todos los campos; el puerto guardado `"COM7"` aparece en `cmbPuerto` aunque el enumerador falso devuelva solo `["COM1", "COM3"]`.
- Tipo Serial → subgrupo Red deshabilitado y viceversa; ambos siguen visibles.
- Grupo Simulador visible solo si el tipo guardado es Simulador; cambiar el combo sin guardar no lo altera.
- Guardar envía un DTO con los valores de los controles; con `ValidacionException(CamposBalanza.HostTcp, …)` el `ErrorProvider` marca `txtHost`.
- Probar patrón: P1 + `"US,GS,+  98.6kg"` → el texto del resultado contiene "98" y "no".
- Probar conexión con un `ServicioBalanzaFalso` cuyo `ProbarAsync` espera un `TaskCompletionSource`: mientras espera, `EstaPruebaEnCurso`, `btnGuardar.Enabled == false`, `btnCancelarPrueba.Enabled == true`; `CancelarPruebaParaTest()` cancela el token recibido por el falso; al completar el TCS, los botones vuelven a su estado y `lblEstadoPrueba` muestra el resultado. Para esperar en STA sin bloquear el bucle de mensajes, bombea con `Application.DoEvents()` en un bucle con límite de 5 s (o sigue el patrón que ya usen los tests de `FrmParametros` para operaciones asíncronas; revísalo primero y **usa el mismo**).
- `MostrarAvanceParaTest` después de terminar la prueba no agrega ítems a `lstTramas`.
- `lstTramas` nunca supera 50 ítems.

### 10.5 Integración con el shell y DI

- `Application/DependencyInjection.cs`: registra `IServicioConfiguracionBalanza` si no lo hiciste en 9.1.
- `FrmPrincipal`: `itemBalanza.Tag = new OpcionMenu(Permiso.ConfigurarBalanza, null)` y `itemBalanza.Click += (_, _) => AbrirConfiguracionBalanza();` con el mismo patrón que `AbrirUsuarios` (`Ejecutar` + `_sesion.ExigirPermiso(Permiso.ConfigurarBalanza)` + `using var frm = _formFactory.Crear<FrmConfiguracionBalanza>(); frm.ShowDialog(this);`).
- `FrmConfiguracionBalanza` controla `ISimuladorBalanza` sin comprobar permisos por su cuenta: es aceptable porque el formulario solo se abre tras `ExigirPermiso(ConfigurarBalanza)`. **No** expongas el control del simulador en ningún otro formulario.
- `ContenedorDITests`: `IServicioConfiguracionBalanza` resoluble y `formFactory.Crear<FrmConfiguracionBalanza>()` dentro del bloque `HiloSta` existente.
- `FrmPrincipalTests`: si había aserciones sobre el ítem "Configuración de balanza" deshabilitado con "F3", cámbialas a habilitado; agrega que es visible para Administrador y no para Supervisor/Operador (usa `AutorizadorMenu` como los tests existentes).

---

## Parte 11 — Cierre

1. **Arquitectura:** `ArquitecturaTests` en verde. Agrega a la lista de prohibidos de Application: `"System.IO.Ports"` (el puerto serie vive solo en Infrastructure).
2. **`Agents.md`:**
   - §2: F3 ✅ Completado, 7/7, fecha; F4 "⏳ Siguiente en turno".
   - §3: marca las 7 HUs de F3.
   - §4: agrega una viñeta **"Balanza"** con: servicio singleton con un único lector; arranque explícito en `Program` tras migrar (no `IHostedService`, y por qué); `IDisposable` y no solo `IAsyncDisposable`; `ConfigureAwait(false)` obligatorio en el código de balanza; hilo dedicado para el puerto serie; entrega a la UI por sondeo con `MonitorBalanzaUi` (sin eventos cruzando hilos); parser con grupos `peso`/`estable`, coma decimal y tiempo límite de regex; detector con ventana deslizante; vigilancia `SinDatos`/`TramasNoReconocidas`; espera de 500 ms tras cerrar un puerto; `EsCapturable` para F4.
   - "Notas hacia fases futuras": agrega los puntos *abiertos* de la Parte 12 (R13-F4, R14, R21, R22, R23, R34).
   - §5: entrada "Fase F3" con build, tests antes → después, migración `F3_Balanza`, decisiones D1–D17 y desviaciones.
3. **`README.md`:** sección breve "Balanza": tipos de conexión, cómo se configura (Administración › Configuración de balanza), el patrón por defecto, el ejemplo P1 con indicador de estabilidad, que la coma se toma como decimal, y que el modo simulador se indica en la barra de estado.
4. **Verificaciones finales:**
   ```powershell
   .\build.ps1
   ```
   ```bash
   dotnet ef migrations has-pending-model-changes -p src/PesajeFaeneo.Infrastructure -s src/PesajeFaeneo.WinForms
   ```
   Ejecuta `dotnet test` tres veces seguidas (regla 0.4) y anota los tres resultados.
5. **Prueba de arranque:** borra `C:\ProgramData\PesajeFaeneo\pesaje.db*`, lanza `src\PesajeFaeneo.WinForms\bin\Release\net8.0-windows\PesajeFaeneo.WinForms.exe`, verifica que sigue vivo a los 5 s, que `__EFMigrationsHistory` contiene `F3_Balanza` y que el log tiene "Aplicación iniciada" y "Balanza conectada (Simulador)" sin errores. Ciérralo **como lo haría el usuario** (no lo mates): en PowerShell, `$p = Get-Process PesajeFaeneo.WinForms; $p.CloseMainWindow()` envía el cierre a la ventana de login; luego `$p.WaitForExit(10000)` debe devolver `True`. Solo si no termina, `Stop-Process` y repórtalo. Verifica que **el proceso termina en menos de 10 s** (si queda colgado, es R6: revisa `ConfigureAwait(false)` y la parada en `Program`). No automatices clics dentro de la aplicación. Repite sobre una copia de una BD creada con F2 para confirmar que migra.
6. **Emulador TCP para la prueba manual** (no se agrega al repo; déjalo en el informe para el usuario). Un servidor PowerShell que emite tramas estilo "ST,GS" cada 200 ms:
   ```powershell
   $l = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 4001); $l.Start(); $c = $l.AcceptTcpClient(); $s = $c.GetStream(); $w = [System.IO.StreamWriter]::new($s); $w.AutoFlush = $true; $p = 0.0; while ($true) { $p = [Math]::Min(120.0, $p + 7.3); $e = if ($p -ge 120.0) { 'ST' } else { 'US' }; $w.Write(("{0},GS,+{1,8}kg`r`n" -f $e, $p.ToString('0.0', [cultureinfo]::InvariantCulture))); Start-Sleep -Milliseconds 200 }
   ```
   (Configurar la app con Red (TCP/IP), host `127.0.0.1`, puerto 4001 y el patrón P1.)
7. **Informe final** (tu única respuesta):
   - Tabla HU → archivos creados o modificados → tests añadidos.
   - Tests antes → después, los tres resultados de la regla 0.4, warnings, resultado de `has-pending-model-changes`, el resumen del SQL revisado en la Parte 2 y el resultado de la prueba de arranque/cierre.
   - Desviaciones del plan y su motivo.
   - **Checklist de prueba manual:**
     1. Arranque con BD nueva: barra de estado "[SIMULADOR] Balanza: conectada (Simulador)  0.0 kg" con fondo naranja.
     2. Como admin, Configuración de balanza › Simulador: "Simular carga" → el peso sube, oscila y queda estable (verde) en la barra de estado y en "Balanza en uso"; "Vaciar" → vuelve a 0.
     3. "Simular desconexión" → la barra pasa a error y reintenta cada 1, 2, 4, 8, 10 s (ver log: una sola línea de warning, no una por intento); desmarcar → reconecta sola.
     4. Probar patrón con P1 y `US,GS,+  98.6kg` → "98.6 kg — estable: no".
     5. Tipo Red (TCP/IP) con el emulador del punto 6: Probar conexión → lista de tramas, peso, "Estabilidad: alcanzada"; durante la prueba la barra dice "en diagnóstico"; al terminar vuelve al simulador (configuración guardada). Guardar → la barra pasa a "conectada (127.0.0.1:4001)". Detener el emulador → "sin datos" y luego reconexión.
     6. Patrón sin `(?<peso>…)` → error de validación en el campo; host "con espacios" → error visible aunque el tipo sea Serie.
     7. Cerrar el formulario durante una prueba → se cierra al terminar la prueba, sin errores.
     8. Si hay un adaptador USB-serie: tipo Serie con su COM; desenchufarlo → error "Se perdió la comunicación…" y reintentos; volver a enchufarlo (mismo COM) → reconecta solo.
     9. Como Supervisor y como Operador: la barra de estado muestra la balanza; el menú Configuración de balanza no aparece.
     10. La tabla `Auditorias` tiene `CambioConfigBalanza` con `EntidadId = "1"` y el JSON antes/después; guardar sin cambios no agrega fila.

---

## Parte 12 — Revisión del plan: huecos detectados y riesgos

Esta sección es el resultado de revisar el plan contra el código real de F0–F2 y contra el comportamiento conocido de .NET 8 en Windows. Cada punto indica **qué podía salir mal** y **qué regla del plan lo evita**. Los marcados *abierto* no se resuelven en F3 y pasan a "Notas hacia fases futuras".

| # | Hueco o riesgo | Consecuencia si se ignora | Cubierto por |
|---|---|---|---|
| R1 | En Windows, `SerialPort.BaseStream.ReadAsync` no respeta `ReadTimeout` y la cancelación no es fiable; `DataReceived` se dispara en el pool con semántica difícil (varios eventos, datos parciales). | Lectores que nunca terminan al desconectar, bucles que no se detienen al salir, tramas partidas. | D14: hilo dedicado con `Read` bloqueante y `ReadTimeout = 250 ms`; `Close()` desbloquea la lectura (7.2). |
| R2 | La documentación de `SerialPort` advierte que el puerto puede no cerrarse al instante: reabrirlo enseguida falla con `UnauthorizedAccessException`. | "Probar conexión" sobre el mismo COM que usa el servicio fallaría siempre con "puerto en uso"; también reconexiones inmediatas. | `EsperaTrasCerrarPuertoMs` en 6.3 y 6.4. |
| R3 | El puerto COM es exclusivo y el servicio compartido lo mantiene abierto. | El diagnóstico no podría abrir el puerto, o dos lectores competirían por los datos. | `ProbarAsync` pausa el bucle compartido (estado `EnDiagnostico`), prueba y lo reanuda siempre en `finally` (6.4). |
| R4 | `Program` llama a `host.Start()` **antes** de migrar y sembrar la BD. | Un `IHostedService` leería la configuración de una BD sin tablas en el primer arranque y fallaría. | Arranque explícito tras la siembra (9.2); prohibido `IHostedService`. |
| R5 | El host se desecha con `using var host` (síncrono). MS.DI lanza `InvalidOperationException` al desechar síncronamente un singleton que solo implementa `IAsyncDisposable`. | Excepción al cerrar la aplicación (después del log de cierre, difícil de ver) y bucle de balanza vivo. | `ServicioBalanza : IDisposable` (Parte 6) + aserción en `ContenedorDITests` (9.1). |
| R6 | Tras `Application.Run`, el hilo principal puede conservar `WindowsFormsSynchronizationContext`; `DetenerAsync().GetAwaiter().GetResult()` se interbloquearía si alguna continuación intenta volver a ese contexto. | La aplicación no termina al cerrarla (proceso colgado, el mutex impide volver a abrirla). | `ConfigureAwait(false)` obligatorio (regla 0.12), bucle en `Task.Run`, `WaitAsync(5 s)` en la parada y verificación de cierre en la prueba de arranque (11.5). |
| R7 | Eventos de un singleton disparados en hilos de fondo y suscritos por formularios. | Acceso a controles desde otro hilo (`InvalidOperationException`), formularios cerrados retenidos por la suscripción (fuga) o `ObjectDisposedException` al invocar en un formulario desechado. | D9: sin eventos; sondeo de una instantánea inmutable con `System.Windows.Forms.Timer` (`MonitorBalanzaUi`). |
| R8 | Una excepción en el `Tick` de un `Timer` va a `Application.ThreadException`. | Un `MessageBox` de error cada 100 ms, aplicación inutilizable. | `Sondear` captura, registra una vez por tipo y sigue (9.3). |
| R9 | La balanza envía 5–20 tramas/s y los logs se conservan 30 días; con la balanza apagada se reintenta cada 10 s durante horas. | Logs de cientos de MB y el visor de F8-06 inservible. | Regla 0.15 y D13: tramas solo en `Debug`, warnings solo cuando cambia el mensaje. |
| R10 | Un patrón regex mal escrito (backtracking catastrófico) configurado por el administrador. | El hilo del bucle se queda minutos en una sola trama; la balanza "se congela". | Tiempo límite de 100 ms (D3), prohibidos los métodos estáticos de `Regex`, test con patrón catastrófico. |
| R11 | El patrón sembrado `[-+]?\d+` no admite espacios entre el signo y los dígitos, formato habitual de los indicadores (`-   12.5`). | Pesos negativos leídos como positivos (p. ej. tara mal hecha que se ve como peso real). | D4: patrón nuevo + `UPDATE` en la migración. Sobrescribir la fila es seguro porque no existía forma de editarla antes de F3. |
| R12 | Ambigüedad entre coma decimal y separador de miles (`1,234`). | Un peso de 1234 leído como 1,2 kg o al revés. | D3: la coma siempre es decimal; `1,234.5` se rechaza (test). *Abierto:* confirmar con tramas reales de la balanza del cliente. |
| R13 | Lectura "estable" congelada: si se desconecta el cable justo con un peso estable, la última instantánea seguiría diciendo "estable". | F4 grabaría un peso viejo como si fuera actual. | D6: vigilancia `SinDatos` vacía la ventana y anula el peso; `EsCapturable` exige lectura reciente. *Abierto para F4-05:* capturar **solo** con `EsCapturable(ahora, TimeoutMs)` y con peso > 0. |
| R14 | *Abierto.* El tipo sembrado por defecto es **Simulador**: en una instalación nueva el operador podría grabar pesajes con pesos inventados. | Riesgo de fraude o de boletas con pesos falsos. | En F3: indicador "[SIMULADOR]" naranja en la barra de estado para todos los roles, control del simulador solo con `ConfigurarBalanza`, nunca activado por compilación o entorno (regla 0.14). **F4/F5 deben decidir** (pregunta 2): bloquear la grabación en modo simulador salvo para administradores, o imprimir "SIMULADO" en la boleta. F9-04 (asistente) debería obligar a elegir el tipo de conexión. |
| R15 | `ChannelReader.ReadAsync` de un canal completado con error lanza `ChannelClosedException` que **envuelve** la excepción original. | El servicio mostraría "The channel has been closed" en lugar del mensaje amigable. | 7.1: se desenvuelve `InnerException`; el falso de tests hace lo mismo. |
| R16 | Validar campos del transporte no elegido con su panel oculto. | Error de validación en un control invisible: el usuario no puede ver ni corregir el problema. | D11 + 10.3: subgrupos siempre visibles, solo deshabilitados. |
| R17 | `Progress<T>` publica los avances de forma asíncrona en el hilo de UI: pueden llegar **después** de que `ProbarAsync` terminó. | El resultado final se ve sobrescrito por una trama vieja, o acceso a un formulario ya desechado. | `_pruebaEnCurso = false` antes de mostrar el resultado y verificación en `MostrarAvance` (10.3) + test. |
| R18 | Cerrar el formulario con la prueba en curso. | La continuación de la prueba toca controles desechados; la balanza podría quedar sin reanudar si se abandonara la tarea. | `FormClosing` cancela y difiere el cierre; `ProbarAsync` reanuda en `finally` pase lo que pase. |
| R19 | Un patrón inválido en la BD (la CK solo valida longitud; un importador de F8 o una edición manual podría dejarlo). | Excepción en cada intento de conexión, reintentos infinitos con el mismo error. | 6.3: se valida antes del bucle, se publica `Error` y no se reintenta; test 14. |
| R20 | `SerialPort.Open()` no es cancelable. Esperarlo con `WaitAsync(ct)` y abandonarlo deja el puerto abierto en segundo plano. | El siguiente intento falla con "puerto en uso" hasta reiniciar la aplicación. | 7.2: `Task.Run` sin token; se comprueba la cancelación solo antes de abrir. |
| R21 | *Abierto.* Los adaptadores USB-serie pueden cambiar de número de COM al reconectarse en otro puerto USB; al desenchufarlos, `Close()` puede lanzar o demorar. | El lector reintenta para siempre un COM que ya no existe. | En F3: reintentos con espera creciente, excepciones de cierre capturadas, el combo muestra el COM guardado aunque no exista y los COM presentes. **F9-06 (guía de balanza)** debe explicar cómo fijar el número de COM en el Administrador de dispositivos. |
| R22 | *Abierto.* Algunos indicadores solo envían el peso cuando se les pide (comando tipo `P`, `W` o `ENQ`). | Con esas balanzas F3 mostraría "sin datos" permanentemente. | D1 lo excluye explícitamente. Si la balanza del cliente lo requiere (pregunta 1), agregar un campo `ComandoSolicitud` e intervalo de sondeo en una fase posterior. |
| R23 | *Abierto.* Unidades distintas de kg (lb, g) o tramas con la unidad indicada. | Pesos multiplicados o divididos por un factor. | D3 asume kg. Pregunta 3. |
| R24 | `StopBits.OnePointFive` solo es válido en Windows con 5 bits de datos; con 7 u 8 `Open()` lanza. | Configuración "válida" que nunca conecta. | `BitsParadaPermitidos = [One, Two]`; el enum conserva `OnePointFive` por compatibilidad del esquema. |
| R25 | Ampliar `IUnidadDeTrabajo` rompe el doble `UowDecorador` de `ServicioParametrosTests`; agregar un parámetro a `FrmPrincipal` rompe `FrmPrincipalTests`. | Compuerta en rojo por causas ajenas a F3. | 3.5 y 9.5 lo señalan explícitamente. |
| R26 | F2 sin commit y con finales LF. | El diff de F3 mezclaría toda F2; los avisos LF enmascararían archivos nuevos mal convertidos. | Regla 0.3. |
| R27 | Tramas sin delimitador (ruido, velocidad mal configurada) o consumidor lento. | Memoria creciente sin límite. | 256 caracteres máximo por trama (D2) y canal acotado de 64 con `DropOldest` (7.1): lo que importa es la lectura más reciente. |
| R28 | Tests con hilos, sockets y temporizadores inestables. | Fallos intermitentes que terminan "arreglados" con `Thread.Sleep` o desactivados. | Regla 0.13: lógica pura sin tiempo, `FakeTimeProvider` + `CancellationTokenSource(TimeSpan, TimeProvider)` (no `CancelAfter`), helper `Esperar`, suite tres veces. |
| R29 | Bytes viejos en el buffer del driver al abrir el puerto (datos recibidos antes). | Primeras lecturas con un peso anterior mostradas como actuales. | `DiscardInBuffer()` tras `Open()` (D14). |
| R30 | Velocidad o paridad mal configuradas producen bytes basura que pueden "parecer" números al regex por defecto (muy permisivo). | Lecturas aleatorias mostradas como peso. | Parcial: el rango de ±99 999,9, el detector (la basura rara vez es estable) y "Probar conexión" muestran las tramas crudas para detectarlo. *Abierto:* recomendar en la guía (F9-06) un patrón específico del indicador (como P1) en lugar del genérico. |
| R31 | La barra de estado y el formulario leen la instantánea cada 100 ms. | Consumo innecesario si se redibuja en cada `Tick`. | Solo se redibuja cuando cambia `Secuencia` (9.3). |
| R32 | `MonitorBalanzaUi.Iniciar()` dentro de `EjecutarAsync`. | Si otra operación envuelta estuviera en curso, el monitor nunca arrancaría (descarte silencioso de la regla 0.10). | 9.5 y 10.3: se inicia fuera de `EjecutarAsync`. |
| R33 | Tolerancia 0 con una balanza que oscila ±0,1. | Nunca alcanza la estabilidad y F4 nunca podría capturar. | El validador lo permite (hay balanzas muy estables); "Probar conexión" informa "Estabilidad: no alcanzada". El checklist lo menciona. |
| R35 | *(Revisión del propio plan.)* Dependencias entre Partes en orden inverso: `ProbarAsync` (Parte 6) usa el validador de la Parte 10, y el menú y la prueba de DI de la Parte 9 usaban `FrmConfiguracionBalanza` (Parte 10). | Un agente que siga el orden de forma literal no pasaría la compuerta de compilación de la Parte 6 ni de la 9, y podría "arreglarlo" con stubs o saltándose la compuerta. | Orden de ejecución explícito (10.1 antes de la 6) y la integración con el shell movida a 10.5. |
| R36 | *(Revisión del propio plan.)* Las esperas con `FakeTimeProvider` (`Task.Delay(…, TimeProvider)`, pausa de 500 ms del modo Serie en `ProbarAsync`) no avanzan solas. | Tests que se cuelgan hasta que vence el límite del helper y parecen fallos intermitentes. | Nota en 6.4: candidatas Tcp/Simulador en los tests, o avanzar el reloj con `Esperar.AvanzandoHastaAsync`; límite global de 5 s (regla 0.13). |
| R37 | *(Revisión del propio plan.)* `Task.Delay` no tiene sobrecarga `(int, TimeProvider, CancellationToken)`; solo `(TimeSpan, TimeProvider, CancellationToken)`. | Error de compilación o, peor, uso de `Task.Delay(int)` en tiempo real que rompe el determinismo de los tests. | Toda espera se escribe con `TimeSpan.FromMilliseconds(...)` + `_timeProvider`. |
| R38 | *(Revisión del propio plan.)* El test de regex catastrófica podía exigir un mensaje concreto. | Según las optimizaciones del motor de .NET 8, el patrón puede terminar sin coincidencia en lugar de por tiempo límite: test rojo sin defecto real. | 4.1: solo se exige `Valida == false`, sin lanzar y en menos de 2 s. |
| R34 | *Abierto.* `DtrEnable`/`RtsEnable` en `true` fijo: algunos equipos se alimentan de esas líneas y otros las usan para control de flujo. | Una balanza concreta podría no transmitir. | D14 elige `true` (lo más compatible). Si la balanza del cliente lo requiere, agregar columnas `Dtr`/`Rts` en una fase posterior (pregunta 5). |

**Preguntas para el usuario (no bloquean; el plan ya asume una respuesta):**
1. ¿Qué marca y modelo de balanza/indicador se usará? ¿Transmite en modo continuo? Una **muestra de tramas reales** (capturada con cualquier terminal serie) permitiría fijar un patrón por defecto específico y un test con ese formato.
2. ¿Qué hacer con el simulador en producción (R14)? Opciones: (a) solo indicador visual (lo que hace F3), (b) bloquear la grabación de pesajes en modo simulador salvo administradores, (c) imprimir "SIMULADO" en la boleta.
3. ¿Siempre kilogramos? ¿La balanza puede enviar la unidad en la trama?
4. ¿5 lecturas y 0,5 kg de tolerancia son adecuados para medias reses colgadas (que oscilan en el riel)? Con 5 tramas/s son ~1 s de estabilidad.
5. ¿Hace falta configurar DTR/RTS o control de flujo?
