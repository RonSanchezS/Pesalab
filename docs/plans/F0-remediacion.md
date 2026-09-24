# Plan de remediación F0 — Fundaciones del proyecto

**Autor de la revisión:** revisión senior post-F0 (2026-09-24)
**Ejecutor:** subagente (Sonnet 5)
**Estado de partida:** commit `1231e4c`, build Release 0 warnings, 17/17 tests verdes, sin cambios de modelo pendientes.

A pesar de que F0 compila y los tests pasan, la revisión encontró defectos que afectan a todas las fases
siguientes (hilo STA de WinForms, pragmas SQLite, fuga de formularios en DI, logging desconectado) y huecos
de modelo de datos que son baratos de corregir **ahora** (no existe ninguna BD en producción) y caros después.

---

## 0. Reglas de ejecución (leer antes de empezar)

1. Crear la rama `fix/f0-remediacion` desde `master`. **No hacer commit ni push**; dejar los cambios en el árbol de trabajo para revisión.
2. Ejecutar las tareas **en el orden indicado**. Tras cada tarea: `dotnet build src/PesajeFaeneo.sln -c Release` (0 warnings, `TreatWarningsAsErrors=true`) y `dotnet test src/PesajeFaeneo.sln -c Release`. No avanzar con rojo.
3. Respetar convenciones del repo: identificadores, comentarios y mensajes en **español**; misma densidad de comentarios; xUnit solo con `Assert`; `TimeProvider` inyectado.
4. Capas: Domain sin NuGet; Application solo depende de Domain (+ abstracciones DI); Infrastructure no referencia WinForms.
5. Para APIs de librerías (Serilog.Extensions.Hosting, EF Core interceptores/convenciones, Microsoft.Data.Sqlite) consultar la documentación vía Context7 antes de escribir código.
6. La migración `Inicial` **se regenera** (tarea 11) porque no hay BD desplegada. No crear migraciones incrementales para F0.
7. Si una tarea resulta imposible tal como está descrita, documentar el motivo en el informe final y seguir con la siguiente; no improvisar cambios de alcance.

---

## P0 — Defectos críticos

### T1. `Main` asíncrono ejecuta WinForms fuera del hilo STA
**Archivo:** `src/PesajeFaeneo.WinForms/Program.cs`
**Defecto:** `[STAThread] static async Task Main`. Después del primer `await` (`host.StartAsync`, `MigrateAsync`) la continuación corre en un hilo del ThreadPool (MTA; aún no hay `WindowsFormsSynchronizationContext`). `Application.Run` termina ejecutándose en un hilo MTA → `ThreadStateException` en `OpenFileDialog`, portapapeles, drag&drop, `PrintDialog` (F5), etc. Además `[STAThread]` sobre un `async Main` no se aplica al punto de entrada sintetizado.
**Corrección:**
- Cambiar a `[STAThread] public static int Main(string[] args)` síncrono.
- La inicialización de BD/seed se ejecuta bloqueando en el hilo STA: `InicializarAsync().GetAwaiter().GetResult()` (seguro: todavía no existe contexto de sincronización) o envolver en `Task.Run(...).GetAwaiter().GetResult()`.
- `host.StartAsync/StopAsync` → `host.Start()` / `host.StopAsync().GetAwaiter().GetResult()` en `finally`; `using var host`.
- Eliminar `Environment.Exit(1)` dentro del `catch` (impide que se ejecute el `finally` con `Log.CloseAndFlush()`); devolver `1` como código de salida.
**Aceptación:** test `ProgramTests.Main_EsSincronoYSta` que por reflexión verifique que `Program.Main` retorna `int`/`void` (no `Task`) y tiene `STAThreadAttribute`.

### T2. Pragmas SQLite por conexión se aplican solo a una conexión
**Archivos:** `Infrastructure/Persistencia/DatabaseInitializer.cs`, `Infrastructure/DependencyInjection.cs`
**Defecto:** `busy_timeout` y `synchronous` son pragmas **por conexión**. Se ejecutan una vez en la conexión del inicializador; todas las demás conexiones (pool de Microsoft.Data.Sqlite) quedan con `synchronous=FULL` y `busy_timeout=0` → `SQLITE_BUSY`/"database is locked" en cuanto haya escritura concurrente (balanza + UI + backup automático F8). Solo `journal_mode=WAL` es persistente.
**Corrección:**
- Crear `Infrastructure/Persistencia/SqlitePragmasInterceptor.cs` (`DbConnectionInterceptor`) que en `ConnectionOpened` y `ConnectionOpenedAsync` ejecute `PRAGMA busy_timeout=5000; PRAGMA synchronous=NORMAL; PRAGMA foreign_keys=ON;`.
- Registrarlo con `options.AddInterceptors(...)` en `AddDbContextFactory` y también en `DesignTimeDbContextFactory`.
- `DatabaseInitializer` conserva solo `PRAGMA journal_mode=WAL;` (tras `MigrateAsync`).
**Aceptación:** test de integración con `SqliteArchivoFixture` (hoy sin uso) que ejecute `DatabaseInitializer`, abra **un contexto nuevo** y verifique `PRAGMA journal_mode` = `wal`, `PRAGMA synchronous` = `1`, `PRAGMA busy_timeout` = `5000`, `PRAGMA foreign_keys` = `1`. El fixture debe registrar el interceptor igual que producción (extraer un helper `ConfigurarSqlite(DbContextOptionsBuilder, string connectionString)` en Infrastructure y reutilizarlo en DI, design-time y fixtures).

### T3. `FormFactory` fuga formularios (transient `IDisposable` resueltos desde el contenedor raíz)
**Archivos:** `WinForms/Infraestructura/FormFactory.cs`, `Application/Abstracciones/IFormFactory.cs`
**Defecto:** los formularios `Transient` resueltos desde el `IServiceProvider` raíz quedan retenidos por el contenedor hasta el cierre de la app (MS.DI rastrea todos los `IDisposable` que crea). Cada apertura de pantalla en F1+ acumula memoria/handles GDI. Además `IFormFactory` es un concepto de UI dentro de Application y `where T : class` lo convierte en service locator genérico.
**Corrección:**
- Mover `IFormFactory` a `PesajeFaeneo.WinForms.Infraestructura` con restricción `where T : Form`.
- Implementación: crear un `IServiceScope` por formulario, instanciar con `ActivatorUtilities.CreateInstance<T>(scope.ServiceProvider)`, y `form.Disposed += (_, _) => scope.Dispose();`. Así cada pantalla obtiene sus servicios scoped y se liberan al cerrarla.
- Mantener el registro `AddTransient<FrmPrincipal>()` solo si sigue siendo necesario; documentar en `AddWinForms` que los formularios no se resuelven desde la raíz.
**Aceptación:** tests: (a) dos llamadas a `Crear<FrmPrincipal>()` devuelven instancias distintas; (b) un servicio scoped `IDisposable` de prueba inyectado en un formulario de prueba se desecha al hacer `form.Dispose()`. Crear formularios en tests dentro de un hilo STA (helper `EjecutarEnSta(Action)` con `Thread` + `SetApartmentState(STA)`) y desecharlos.

---

## P1 — Defectos altos

### T4. Serilog no está conectado a `Microsoft.Extensions.Logging`
**Archivo:** `WinForms/Program.cs`, `appsettings.json`
**Defecto:** solo se configura `Log.Logger` estático. Cualquier `ILogger<T>` inyectado (servicios de F1+) escribe en los proveedores por defecto del Host (Console, Debug, **EventLog**) y nunca llega al archivo `pesaje-*.log`. La sección `Serilog` de `appsettings.json` se ignora (código duplicado y divergente).
**Corrección:**
- Construir primero el `HostApplicationBuilder`, luego configurar Serilog con `.ReadFrom.Configuration(builder.Configuration)` + el sink de archivo calculado desde `AppPaths` (retención 30 días), y `builder.Services.AddSerilog()` (verificar la firma en Serilog.Extensions.Hosting 8.0.0 con Context7). `builder.Logging.ClearProviders()` para eliminar EventLog.
- Eliminar los `MinimumLevel.Override` duplicados en código (la fuente de verdad es `appsettings.json`).
**Aceptación:** test DI que verifique que `ILoggerProvider` registrado es `SerilogLoggerProvider` y que no hay `EventLogLoggerProvider`.

### T5. `ContentRoot` depende del directorio de trabajo; `appsettings.json` agregado dos veces
**Defecto:** `Host.CreateApplicationBuilder(args)` usa `Directory.GetCurrentDirectory()`; lanzado desde un acceso directo con otro "Iniciar en" no encuentra `appsettings.json`. Además `AddJsonFile("appsettings.json")` duplica la fuente que el Host ya agrega.
**Corrección:** `Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { Args = args, ContentRootPath = AppContext.BaseDirectory })` y quitar el `AddJsonFile` manual.

### T6. `AppPaths` duplicado y efectos secundarios en el registro DI
**Archivos:** `Infrastructure/DependencyInjection.cs`, `Common/AppPaths.cs`, `Persistencia/DesignTimeDbContextFactory.cs`, `ContenedorDITests.cs`
**Defecto:** `AddInfrastructure` crea su propio `AppPaths` y llama `AsegurarDirectorios()` al registrar servicios (I/O en composición). `Program` crea otro. El test de DI crea carpetas reales en `C:\ProgramData\PesajeFaeneo` (confirmado en esta máquina). `DesignTimeDbContextFactory` no crea el directorio → `dotnet ef database update` falla en una máquina limpia. El parámetro `configuration` se ignora.
**Corrección:**
- Firma `AddInfrastructure(this IServiceCollection services, AppPaths appPaths)` (o `IConfiguration` + clave `PesajeFaeneo:DirectorioDatos` que sobrescriba `BaseDir`). `Program` crea **una** instancia, llama `AsegurarDirectorios()` y la pasa.
- `AddInfrastructure` no hace I/O.
- `DesignTimeDbContextFactory` llama `AsegurarDirectorios()`.
- `ContenedorDITests` usa un directorio temporal y lo borra; construir el proveedor con `ValidateOnBuild = true, ValidateScopes = true`.
**Aceptación:** test DI pasa sin tocar `ProgramData`; `ValidateOnBuild/ValidateScopes` activos.

### T7. `ClasificadorErrores` incompleto
**Archivo:** `WinForms/Infraestructura/ClasificadorErrores.cs`
**Defecto:** `ConcurrenciaException` (y cualquier otra `DomainException` base) cae en "Error inesperado" con log y código de incidente. `OperationCanceledException` (cancelación del usuario) también se muestra como error. `AggregateException` con varias internas se desenvuelve a la primera sin criterio.
**Corrección:** `ConcurrenciaException` → `Negocio` ("El registro fue modificado por otro usuario. Vuelva a cargarlo."); `DomainException` genérica → `Negocio`; nuevo `TipoErrorClasificado.Cancelado` para `OperationCanceledException` (sin mensaje ni log; `ManejadorErrores` no muestra MessageBox). Desenvolver `AggregateException` solo si `Flatten().InnerExceptions.Count == 1`.
**Aceptación:** tests nuevos para cada caso (Concurrencia, EntidadNoEncontrada, Conflicto, DomainException base, OperationCanceled, TargetInvocation envolviendo Validacion).

### T8. Manejadores globales de excepción
**Archivo:** `Program.cs`, `ManejadorErrores.cs`
**Defecto:** en `AppDomain.UnhandledException` el proceso va a terminar; se muestra un MessageBox desde un hilo arbitrario y no se hace `Log.Fatal` + `CloseAndFlush`.
**Corrección:** en ese manejador: `Log.Fatal(...)`, intento de mensaje breve, `Log.CloseAndFlush()`. `ThreadException` se mantiene con `ManejadorErrores.Mostrar`.

### T9. `FormBase.EjecutarAsync` — cursor y reentrada
**Archivo:** `WinForms/Formularios/FormBase.cs`
**Defecto:** `Cursor.Current` se restablece en cuanto el bucle de mensajes procesa (inútil en operaciones `await`). Sin `botonDisparador` no hay protección contra doble clic/doble Enter (crítico para "Grabar pesaje" en F4: boletas duplicadas).
**Corrección:** usar `UseWaitCursor = true/false`; campo `_ocupado` que haga que una segunda invocación concurrente retorne inmediatamente; `Ejecutar` síncrono igual. Mantener firma pública.
**Aceptación:** test en STA: invocar `EjecutarAsync` dos veces con una acción que espera un `TaskCompletionSource` → la acción se ejecuta una sola vez (exponer vía subclase de prueba).

---

## P2 — Endurecimiento del modelo de datos (antes de regenerar la migración)

### T10. Reglas de modelo
Archivos en `Infrastructure/Persistencia/Configuraciones/` y `PesajeDbContext.cs`:
1. **Unicidad sin distinguir mayúsculas:** `UseCollation("NOCASE")` en `Usuario.NombreUsuario` y `Ganadero.Marca` (hoy `"admin"` y `"Admin"` o `"ah"`/`"AH"` coexisten; el login sería sensible a mayúsculas). También en `Ganadero.Nombre`, `Cliente.Nombre`, `Fletero.Nombre` para búsquedas (F2-06).
2. **DateTime UTC:** SQLite devuelve `DateTimeKind.Unspecified`. En `ConfigureConventions` aplicar a `DateTime` y `DateTime?` un `ValueConverter` que guarde en UTC y lea con `DateTimeKind.Utc`. Documentar en `Agents.md §4` la convención: *se persiste UTC; los filtros "del día" (F4-03 secuencia, F7 reportes) convierten los límites del día local a UTC con `TimeProvider.LocalTimeZone`*.
3. **Conversor decimal→REAL centralizado:** extraer `DecimalARealConverter` (redondeo a 1 decimal, `AwayFromZero`) en una clase y reutilizarlo en `Pesaje` y `ConfiguracionBalanza` (hoy duplicado). Documentar que la resolución de peso es 0,1 kg.
4. **Check constraints nuevas:**
   - `Pesajes`: `Estado IN (1, 2)`; `PesoIzquierdo >= 0 AND PesoDerecho >= 0 AND PesoTotal >= 0`; coherencia de anulación: `(Estado = 1 AND MotivoAnulacion IS NULL AND AnuladoPorId IS NULL AND AnuladoEn IS NULL) OR (Estado = 2 AND MotivoAnulacion IS NOT NULL AND AnuladoPorId IS NOT NULL AND AnuladoEn IS NOT NULL)`; `Secuencia >= 1`.
   - `Usuarios`: `Rol IN (1, 2, 3)`.
   - `ParametrosSistema`: `SiguienteNumeroBoleta >= 1`.
   - `ConfiguracionesBalanza`: `TipoConexion IN (1, 2, 3)`, `LecturasParaEstable >= 1`, `TimeoutMs > 0`.
5. **Application sin Hosting:** en `PesajeFaeneo.Application.csproj` reemplazar `Microsoft.Extensions.Hosting` por `Microsoft.Extensions.DependencyInjection.Abstractions` (agregar versión 8.0.x en `Directory.Packages.props`).
6. **DataSeeder:** renombrar parámetro `_hasher` → `passwordHasher`; hacer `TimeProvider` obligatorio (ya registrado en DI; los tests pasan `FakeTimeProvider`).

### T11. Regenerar la migración `Inicial`
```bash
dotnet ef migrations remove -p src/PesajeFaeneo.Infrastructure -s src/PesajeFaeneo.WinForms
```
```bash
dotnet ef migrations add Inicial -p src/PesajeFaeneo.Infrastructure -s src/PesajeFaeneo.WinForms -o Persistencia/Migraciones
```
Si `remove` falla porque la BD de desarrollo ya la aplicó, borrar `C:\ProgramData\PesajeFaeneo\pesaje.db*` (solo datos de desarrollo) o usar `--force`. Revisar el SQL generado: collations `NOCASE`, todas las CK de T10.
**Aceptación:** `dotnet ef migrations has-pending-model-changes` → sin cambios.

---

## P3 — Pruebas e higiene

### T12. Builders de prueba
- `PesajeBuilder.Anulado(motivo, anuladoPorId)` ignora sus parámetros: debe asignar `MotivoAnulacion`, `AnuladoPorId`, `AnuladoEn` (si no, violará la CK de T10.4).
- Reemplazar `DateTime.UtcNow` por una fecha fija/`FakeTimeProvider` configurable (`ConFechaHora`).

### T13. Tests faltantes (mínimo)
- `DatabaseInitializerTests` (archivo): BD nueva → migra, WAL, filas `HasData` de `ParametrosSistema` (Id=1, `SiguienteNumeroBoleta`=1) y `ConfiguracionesBalanza` (Id=1) presentes; ejecutar dos veces es idempotente.
- CK singleton: insertar `ParametrosSistema` con Id=2 → `DbUpdateException`.
- CK `Sexo` ('X'), `TipoDesposte` (3), `Estado` (3), anulación incoherente, pesos negativos, `Rol` (9) → `DbUpdateException`.
- Collation: `"Admin"` tras `"admin"` y `"ah"` tras `"AH"` → `DbUpdateException`.
- DateTime: ida y vuelta conserva `Kind == Utc` y el instante.
- Modelo vs snapshot: test que compare el modelo con el snapshot de migraciones (`IMigrationsAssembly.ModelSnapshot`, `IModelRuntimeInitializer`, `IMigrationsModelDiffer.HasDifferences` sobre `GetRelationalModel()`, con `#pragma warning disable EF1001`) para que un cambio de modelo sin migración rompa el build.
- Arquitectura: Application no referencia `Microsoft.EntityFrameworkCore`, `System.Windows.Forms`, `PesajeFaeneo.Infrastructure`, `PesajeFaeneo.WinForms`, `Serilog`, `Microsoft.Extensions.Hosting`; Infrastructure no referencia `PesajeFaeneo.WinForms` ni `System.Windows.Forms`.

### T14. Documentación
- `Agents.md`: añadir en la bitácora una entrada **"F0 — Remediación post-revisión"** con fecha, lista de defectos corregidos, nuevo conteo de tests y resultado de build; actualizar §4 con las convenciones nuevas (UTC, interceptor de pragmas, `IFormFactory` por scope, collation NOCASE, `FormBase` anti-reentrada).
- `README.md`: aclarar que `dotnet ef database update` crea la BD en `%ProgramData%\PesajeFaeneo`.

---

## Notas hacia fases futuras (NO implementar ahora; solo registrar en Agents.md §4 o en el HU correspondiente)
- **F2:** traducir `DbUpdateException` por restricción UNIQUE/CHECK de SQLite (`SqliteErrorCode 19`) a `ConflictoException`/`ValidacionException` en Infrastructure; la UI no debe ver `DbUpdateException`.
- **F4-02:** numeración atómica con `UPDATE ParametrosSistema SET SiguienteNumeroBoleta = SiguienteNumeroBoleta + 1 WHERE Id = 1 RETURNING ...` dentro de la misma transacción que el insert del pesaje y la auditoría.
- **F6:** agregar token de concurrencia (`Version` entero con `IsConcurrencyToken`, incrementado en `SaveChanges`) a `Pesaje` y `ParametrosSistema`, y mapear `DbUpdateConcurrencyException` → `ConcurrenciaException`.
- **F7:** QuestPDF 2024.x exige `QuestPDF.Settings.License = LicenseType.Community` al iniciar.
- **F9:** el instalador debe dar permisos de Modificar a `Users` sobre `%ProgramData%\PesajeFaeneo` (por defecto, archivos creados por un admin son de solo lectura para usuarios estándar → la app no podría escribir la BD). Revisar si el mutex `Global\` (bloquea a un segundo usuario de Windows en otra sesión) es el comportamiento deseado.
- **Seguridad F1:** el usuario `admin/admin` con `DebeCambiarPassword = true` exige que F1-03 fuerce el cambio antes de abrir el shell.

---

## Criterio de finalización
- Todas las tareas T1–T14 hechas (o justificadas en el informe).
- `.\build.ps1` termina en verde: 0 warnings, 0 errores, todos los tests pasan.
- `dotnet ef migrations has-pending-model-changes` sin cambios.
- La app arranca (`dotnet run --project src/PesajeFaeneo.WinForms`): se crea la BD, se escribe `logs\pesaje-*.log` con la línea "Aplicación iniciada", y se ve `FrmPrincipal`. Cerrar la app.
- Informe final: tabla tarea → archivos tocados → tests añadidos; desviaciones del plan; conteo final de tests.
