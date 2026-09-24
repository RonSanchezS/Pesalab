# Plan de implementación F1 — Seguridad, usuarios y shell principal

**Autor:** revisión senior (2026-09-24)
**Ejecutor:** agente Sonnet 5
**Punto de partida:** F0 + remediación post-revisión (`docs/plans/F0-remediacion.md`), build Release con 0 warnings, 49 tests en verde.
**HUs:** HU-F1-01 … HU-F1-06 (ver `Agents.md §3`).

Este documento es **la especificación**. Si algo no está aquí, se aplican las convenciones de `Agents.md §4`. Si algo contradice `Agents.md`, manda este documento y la diferencia se anota en el informe final.

---

## Parte 0 — Reglas de comportamiento del agente (obligatorias)

1. **Trabajas tú mismo.** No lances subagentes, no delegues y no dejes trabajo "en segundo plano". Usa directamente tus herramientas (Read/Edit/Write/PowerShell/Bash).
2. **No te detengas hasta terminar** las Partes 1 a 9. Responde al coordinador **una sola vez**, al final, con el informe de la Parte 10. Si algo te bloquea, documéntalo en el informe, elige la opción más conservadora compatible con este plan y continúa.
3. **Git:**
   - Antes de empezar, `git status` debe estar limpio y `git log` debe incluir el commit de la remediación F0. Si no se cumple, **detente** y repórtalo; no mezcles cambios ajenos.
   - Crea la rama `feat/f1-seguridad` desde el HEAD actual.
   - **No hagas commit ni push.** Deja todo en el árbol de trabajo.
4. **Compuerta de calidad después de cada Parte:**
   ```bash
   dotnet build src/PesajeFaeneo.sln -c Release
   ```
   ```bash
   dotnet test src/PesajeFaeneo.sln -c Release
   ```
   Debe haber 0 warnings (`TreatWarningsAsErrors=true`), 0 errores y todos los tests en verde. **No avances con rojo.** Nunca desactives un warning, un test ni un analizador para pasar la compuerta.
5. **Convenciones:**
   - Identificadores, comentarios, mensajes de UI y de log en **español**. Imita el estilo y la densidad de comentarios del código existente.
   - Finales de línea **CRLF**, UTF-8 (`.editorconfig`). Verifica que los archivos nuevos no queden con LF.
   - xUnit solo con `Assert`.
   - Siempre `TimeProvider` inyectado; en tests, `FakeTimeProvider`.
   - Fechas persistidas en UTC. En UI se muestran en hora local con `TimeProvider.LocalTimeZone`.
6. **Capas** (las hace cumplir `ArquitecturaTests`):
   - Domain no tiene NuGet.
   - Application depende de Domain, `Microsoft.Extensions.DependencyInjection.Abstractions` y `Microsoft.Extensions.Logging.Abstractions`. Nada de EF, WinForms, Serilog ni Hosting.
   - Infrastructure no referencia WinForms.
7. **Migraciones:** **no modifiques** la migración `Inicial`. Los cambios de esquema van en una migración nueva (Parte 2). Al terminar, `dotnet ef migrations has-pending-model-changes` no debe reportar cambios.
8. **Librerías:** antes de escribir código contra una API de librería (EF Core 8, Microsoft.Data.Sqlite, WinForms `ApplicationContext`, Microsoft.Extensions.Logging), consulta la documentación con Context7 (`ToolSearch` → `select:mcp__context7__resolve-library-id,mcp__context7__query-docs`).
9. **Hilo de UI:**
   - Una vez iniciado el bucle de mensajes, **nunca** bloquees el hilo de UI con `.Result`, `.Wait()` ni `.GetAwaiter().GetResult()` sobre código asíncrono. Hay contexto de sincronización de WinForms y se produce un deadlock.
   - Los manejadores de eventos asíncronos son `async void` y delegan en `FormBase.EjecutarAsync`.
10. **Tests de WinForms:** todo test que cree un `Form` o un control corre dentro de `HiloSta.Ejecutar(...)` y desecha lo que crea.
11. **Seguridad:**
    - Nunca escribas contraseñas, hashes ni salts en logs, auditoría, mensajes o excepciones.
    - No agregues "puertas traseras" de desarrollo (usuarios fijos, contraseñas maestras, flags para saltar el login).

---

## Parte 1 — Decisiones de diseño (asumidas; el usuario puede cambiarlas)

Estas decisiones son **definitivas para esta ejecución**. Centralízalas como constantes (Parte 3) para que cambiarlas sea trivial.

| # | Decisión | Valor |
|---|---|---|
| D1 | Matriz rol → permisos | Ver tabla D1 abajo |
| D2 | Política de contraseña | 6 a 100 caracteres. No puede ser igual al nombre de usuario (sin distinguir mayúsculas). Al cambiarla, la nueva debe ser distinta de la actual. Sin reglas de complejidad adicionales (planta de faeneo, operadores con teclado numérico). |
| D3 | Bloqueo por intentos fallidos | 5 intentos fallidos consecutivos → bloqueo temporal de **5 minutos**. Un login exitoso o un restablecimiento por el administrador reinician el contador y quitan el bloqueo. |
| D4 | Nombre de usuario | Se normaliza con `Trim()` y a minúsculas (`ToLowerInvariant`). Regex `^[a-z0-9._-]{3,30}$`. Es inmutable una vez creado. |
| D5 | Usuarios nuevos | Nacen con `DebeCambiarPassword = true`: el administrador define una contraseña inicial y el usuario la cambia en su primer ingreso. |
| D6 | Borrado de usuarios | **No existe.** Solo activar o desactivar: `Pesajes` y `Auditorias` tienen FK `Restrict` a `Usuarios`. |
| D7 | Protección del último administrador | No se puede desactivar ni degradar de rol al último administrador **activo**. Un usuario tampoco puede desactivarse ni cambiarse el rol a sí mismo. |
| D8 | Menú sin permiso | Las opciones sin permiso se **ocultan**. Un menú de primer nivel cuyos hijos están todos ocultos también se oculta. |
| D9 | Módulos de fases futuras | Aparecen en el menú (según permiso) con `Enabled = false` y ToolTip "Disponible en la fase Fx". |
| D10 | Mensajes de login | Usuario inexistente y contraseña incorrecta muestran el **mismo** mensaje: "Usuario o contraseña incorrectos.". Usuario inactivo y usuario bloqueado muestran su mensaje específico **solo si la contraseña ingresada es correcta** (inactivo) o siempre (bloqueado, porque el bloqueo ya revela que existe). |

**Tabla D1 — Matriz de permisos**

| Permiso | Operador | Supervisor | Administrador |
|---|:-:|:-:|:-:|
| Pesar | ✔ | ✔ | ✔ |
| ImprimirBoleta | ✔ | ✔ | ✔ |
| ReimprimirBoleta | ✔ | ✔ | ✔ |
| AltaRapidaCatalogos | ✔ | ✔ | ✔ |
| VerReportes |  | ✔ | ✔ |
| EditarBoleta |  | ✔ | ✔ |
| AnularBoleta |  | ✔ | ✔ |
| GestionarCatalogos |  | ✔ | ✔ |
| VerAuditoria |  | ✔ | ✔ |
| GestionarUsuarios |  |  | ✔ |
| GestionarParametros |  |  | ✔ |
| ConfigurarBalanza |  |  | ✔ |
| BackupRestore |  |  | ✔ |
| ImportarLegado |  |  | ✔ |

---

## Parte 2 — Esquema: bloqueo de usuarios (base de HU-F1-01)

**Archivos:** `Domain/Entities/Entities.cs`, `Infrastructure/Persistencia/Configuraciones/UsuarioConfiguration.cs`, migración nueva.

1. Agrega a `Usuario`:
   ```csharp
   public int IntentosFallidos { get; set; }
   public DateTime? BloqueadoHasta { get; set; }   // UTC
   ```
2. En `UsuarioConfiguration`:
   - `IntentosFallidos` requerido, `HasDefaultValue(0)`.
   - Check constraint `CK_Usuarios_IntentosFallidos`: `IntentosFallidos >= 0`.
3. Genera la migración:
   ```bash
   dotnet ef migrations add F1_BloqueoUsuario -p src/PesajeFaeneo.Infrastructure -s src/PesajeFaeneo.WinForms -o Persistencia/Migraciones
   ```
   SQLite no altera tablas en sitio. Revisa que EF haya generado la reconstrucción de la tabla sin perder datos y **conserve** la collation `NOCASE` de `NombreUsuario` y las CK existentes (`CK_Usuarios_Rol`).
4. Agrega `ConBloqueo(int intentos, DateTime? hasta)` a `UsuarioBuilder` y `ConPassword(string password, IPasswordHasher hasher)` para crear usuarios con hash real.

**Tests (en `PersistenciaIntegracionTests`):**
- `Usuario_IntentosFallidosNegativos_LanzaDbUpdateException`.
- `Usuario_BloqueadoHasta_IdaYVueltaConservaUtc`.
- `ModeloVsMigracionTests` sigue en verde.

---

## Parte 3 — Contratos de Application (base de HU-F1-01 y HU-F1-02)

Crea estas piezas **exactamente con estos nombres y firmas** (puedes añadir comentarios XML). Todo va en `PesajeFaeneo.Application`. Agrega `Microsoft.Extensions.Logging.Abstractions` (versión 8.0.x) a `Directory.Packages.props` y al `.csproj` de Application.

### 3.1 Persistencia (`Application/Abstracciones/Persistencia/`)

```csharp
public interface IUnidadDeTrabajoFactory
{
    Task<IUnidadDeTrabajo> CrearAsync(CancellationToken cancellationToken = default);
}

/// Una unidad de trabajo = un DbContext = una transacción implícita en GuardarCambiosAsync.
/// El cambio de negocio y su registro de auditoría DEBEN guardarse en la misma llamada a GuardarCambiosAsync.
public interface IUnidadDeTrabajo : IAsyncDisposable
{
    IUsuarioRepositorio Usuarios { get; }
    IAuditoriaRepositorio Auditoria { get; }
    Task GuardarCambiosAsync(CancellationToken cancellationToken = default);
}

public interface IUsuarioRepositorio
{
    Task<Usuario?> ObtenerPorIdAsync(int id, CancellationToken cancellationToken = default);            // con tracking
    Task<Usuario?> ObtenerPorNombreUsuarioAsync(string nombreUsuario, CancellationToken cancellationToken = default); // con tracking
    Task<IReadOnlyList<Usuario>> ListarAsync(bool incluirInactivos, CancellationToken cancellationToken = default);  // sin tracking, orden NombreUsuario
    Task<bool> ExisteNombreUsuarioAsync(string nombreUsuario, CancellationToken cancellationToken = default);
    Task<int> ContarAdministradoresActivosAsync(CancellationToken cancellationToken = default);
    void Agregar(Usuario usuario);
}

public interface IAuditoriaRepositorio
{
    void Registrar(Auditoria entrada);
}
```

### 3.2 Seguridad (`Application/Seguridad/`)

```csharp
public static class PoliticaSeguridad
{
    public const int LongitudMinimaPassword = 6;
    public const int LongitudMaximaPassword = 100;
    public const int MaximoIntentosFallidos = 5;
    public static readonly TimeSpan DuracionBloqueo = TimeSpan.FromMinutes(5);
    public const string PatronNombreUsuario = "^[a-z0-9._-]{3,30}$";
}

public static class MatrizPermisos
{
    public static IReadOnlySet<Permiso> PermisosDe(Rol rol);   // tabla D1, conjuntos inmutables precalculados
    public static bool Tiene(Rol rol, Permiso permiso);
}

public sealed record UsuarioSesion(int Id, string NombreUsuario, string NombreCompleto, Rol Rol, bool DebeCambiarPassword);

public interface ISesionActual
{
    UsuarioSesion? Usuario { get; }
    bool EstaAutenticada { get; }
    bool TienePermiso(Permiso permiso);          // false si no hay sesión
    void ExigirPermiso(Permiso permiso);         // lanza NoAutorizadoException
    UsuarioSesion ExigirSesion();                // lanza NoAutorizadoException("Debe iniciar sesión.")
    event EventHandler? SesionCambiada;
    // Solo los usa el servicio de autenticación:
    void Iniciar(UsuarioSesion usuario);
    void Cerrar();
    void MarcarPasswordCambiada();              // DebeCambiarPassword = false en la sesión en memoria
}
```

- `SesionActual : ISesionActual` es la implementación en Application. Se registra **Singleton**: es una app de escritorio con un solo usuario por proceso.
- Protege el estado con un `lock`.
- `SesionCambiada` se dispara después de `Iniciar`, `Cerrar` y `MarcarPasswordCambiada`.

### 3.3 Validación y normalización (`Application/Seguridad/ValidadorUsuario.cs`)

Clase estática pura, sin I/O:
- `string NormalizarNombreUsuario(string? valor)`: Trim + minúsculas; `null` → `""`.
- `string NormalizarNombreCompleto(string? valor)`: Trim y colapso de espacios internos múltiples a uno.
- `IReadOnlyList<ErrorValidacion> ValidarAlta(string nombreUsuario, string nombreCompleto, Rol rol, string password, string confirmacion)`.
- `IReadOnlyList<ErrorValidacion> ValidarEdicion(string nombreCompleto, Rol rol)`.
- `IReadOnlyList<ErrorValidacion> ValidarPasswordNueva(string nombreUsuario, string nueva, string confirmacion, string? actual)`.

Los nombres de campo en `ErrorValidacion.Campo` son **constantes públicas** en `CamposUsuario`: `NombreUsuario`, `NombreCompleto`, `Rol`, `Password`, `Confirmacion`, `PasswordActual`. La UI los usa para asociar cada error a su control.

Mensajes exactos:

| Caso | Mensaje |
|---|---|
| Nombre de usuario vacío o que no cumple el patrón | "Use de 3 a 30 caracteres: letras minúsculas, números, punto, guion o guion bajo." |
| Nombre completo con menos de 3 o más de 100 caracteres | "Ingrese el nombre completo (3 a 100 caracteres)." |
| Rol fuera del enum (`Enum.IsDefined`) | "Seleccione un rol válido." |
| Contraseña fuera de longitud | "La contraseña debe tener entre 6 y 100 caracteres." |
| Contraseña igual al usuario | "La contraseña no puede ser igual al nombre de usuario." |
| Nueva igual a la actual | "La nueva contraseña debe ser distinta de la actual." |
| Confirmación distinta | "La confirmación no coincide con la contraseña." |

### 3.4 Auditoría (`Application/Auditoria/FabricaAuditoria.cs`)

Helper puro que construye entidades `Auditoria` con `FechaHora = timeProvider.GetUtcNow().UtcDateTime`:
- `Auditoria Crear(TimeProvider tiempo, int? usuarioId, AccionAuditoria accion, string entidad, string entidadId, object? antes = null, object? despues = null, string? motivo = null)`.
- Serializa `antes` y `despues` con `System.Text.Json` (`WriteIndented = false`, enums como string con `JsonStringEnumConverter`).
- Trunca `EntidadId` a 50 caracteres y `Motivo` a 250 (los límites de las columnas).
- Para usuarios, **solo** serializa un snapshot `UsuarioAuditoria(int Id, string NombreUsuario, string NombreCompleto, Rol Rol, bool Activo, bool DebeCambiarPassword)`. Nunca la entidad `Usuario` completa: evita que se filtren `Hash` y `Salt`.
- Constante `EntidadesAuditoria.Usuario = "Usuario"`.

---

## Parte 4 — Infrastructure: unidad de trabajo (base de HU-F1-01)

**Archivos nuevos en `Infrastructure/Persistencia/`:** `UnidadDeTrabajoFactory.cs`, `UnidadDeTrabajo.cs`, `Repositorios/UsuarioRepositorio.cs`, `Repositorios/AuditoriaRepositorio.cs`.

- `UnidadDeTrabajoFactory` usa `IDbContextFactory<PesajeDbContext>` y crea **un** `PesajeDbContext` por unidad. Se registra Singleton.
- `UnidadDeTrabajo.GuardarCambiosAsync` llama a `SaveChangesAsync` y traduce excepciones:
  - `DbUpdateConcurrencyException` → `ConcurrenciaException("El registro fue modificado por otro usuario.")`.
  - `DbUpdateException` cuya `InnerException` es `SqliteException` con `SqliteErrorCode == 19`:
    - si el mensaje contiene `"UNIQUE"` → `ConflictoException("Ya existe un registro con ese valor.")`;
    - si contiene `"CHECK"` → `ReglaNegocioException("BD_CHECK", "Los datos no cumplen una regla de integridad.")`.
  - En ambos casos conserva la excepción original como `InnerException`. Para eso agrega a `ConflictoException` y `ReglaNegocioException` constructores con `Exception inner`.
- `DisposeAsync` desecha el contexto.
- `ObtenerPorNombreUsuarioAsync` compara contra el valor ya normalizado. La collation `NOCASE` es una segunda defensa.
- Registra en `AddInfrastructure`: `IUnidadDeTrabajoFactory` Singleton. `TimeProvider.System` ya está registrado.

**Tests (`UnidadDeTrabajoTests`, SQLite en memoria):**
- Guardar un usuario y su auditoría en la misma unidad persiste ambos.
- Si falla un `SaveChanges` (usuario duplicado), **no** queda ni el usuario ni la auditoría → atomicidad.
- Duplicado → `ConflictoException` (no `DbUpdateException`).
- `Rol = 9` → `ReglaNegocioException` con código `BD_CHECK`.

> **Aislamiento de tests:** crea en `tests/Fixtures/` un helper `BaseDatosEnMemoria : IAsyncDisposable` con `static Task<BaseDatosEnMemoria> CrearAsync()`, que dé una BD nueva **por test**. Expone `ContextFactory` y `UnidadDeTrabajoFactory`. Todos los tests de servicios F1 lo usan. No compartas BD entre tests que crean o modifican usuarios.

---

## Parte 5 — HU-F1-01: Servicio de autenticación y auditoría base

**Archivo:** `Application/Seguridad/ServicioAutenticacion.cs` (+ interfaz `IServicioAutenticacion`).

```csharp
public enum ResultadoLoginTipo { Exitoso, CredencialesInvalidas, UsuarioInactivo, UsuarioBloqueado }

public sealed record ResultadoLogin(ResultadoLoginTipo Tipo, string Mensaje, bool DebeCambiarPassword = false);

public interface IServicioAutenticacion
{
    Task<ResultadoLogin> IniciarSesionAsync(string? nombreUsuario, string? password, CancellationToken cancellationToken = default);
    Task CerrarSesionAsync(CancellationToken cancellationToken = default);
    Task CambiarPasswordPropiaAsync(string actual, string nueva, string confirmacion, CancellationToken cancellationToken = default); // HU-F1-06
}
```

Dependencias: `IUnidadDeTrabajoFactory`, `IPasswordHasher`, `ISesionActual`, `TimeProvider`, `ILogger<ServicioAutenticacion>`. Se registra **Transient** en `AddApplication`.

### Algoritmo de `IniciarSesionAsync` (implementar en este orden)

1. Normaliza el nombre con `ValidadorUsuario.NormalizarNombreUsuario`. Si el nombre o la contraseña están vacíos → `CredencialesInvalidas` con el mensaje "Ingrese usuario y contraseña.". No audites este caso.
2. Abre una unidad y busca el usuario.
3. **Usuario inexistente:**
   - Ejecuta igual `IPasswordHasher.Verificar` contra un hash y salt ficticios, generados una sola vez con `Lazy<>`, para igualar el tiempo de respuesta.
   - Audita `LoginFallido` con `UsuarioId = null`, `EntidadId = nombre normalizado` y `Motivo = "Usuario inexistente"`.
   - Guarda y devuelve `CredencialesInvalidas` con "Usuario o contraseña incorrectos.".
4. **Bloqueado** (`BloqueadoHasta > ahoraUtc`):
   - Audita `LoginFallido` con `Motivo = "Usuario bloqueado"`.
   - Devuelve `UsuarioBloqueado` con "Usuario bloqueado temporalmente por intentos fallidos. Intente de nuevo en {n} minuto(s).". `n` es el techo de los minutos restantes, mínimo 1.
   - **No** verifiques la contraseña ni incrementes el contador.
5. Si `BloqueadoHasta` existe y ya venció: pon `BloqueadoHasta = null` e `IntentosFallidos = 0` antes de seguir.
6. **Contraseña incorrecta:**
   - `IntentosFallidos++`. Si llega a `MaximoIntentosFallidos`, entonces `BloqueadoHasta = ahora + DuracionBloqueo` y `IntentosFallidos = 0`.
   - Audita `LoginFallido` con `Motivo = "Contraseña incorrecta"`, o `"Contraseña incorrecta; usuario bloqueado"` si se acaba de bloquear.
   - Guarda y devuelve `CredencialesInvalidas` con el mensaje genérico. Si se acaba de bloquear, devuelve `UsuarioBloqueado` con el mensaje de bloqueo.
7. **Contraseña correcta pero `Activo == false`:** audita `LoginFallido` con `Motivo = "Usuario inactivo"` y devuelve `UsuarioInactivo` con "El usuario está desactivado. Contacte al administrador.".
8. **Éxito:**
   - `IntentosFallidos = 0`, `BloqueadoHasta = null`, `UltimoAcceso = ahoraUtc`.
   - Audita `Login` con `UsuarioId = usuario.Id`.
   - Guarda **y después** `ISesionActual.Iniciar(...)`. Si falla el guardado, no queda sesión abierta.
   - Devuelve `Exitoso` con `DebeCambiarPassword` del usuario.
9. Registra en el log (Information) cada login exitoso o fallido con el nombre de usuario y el tipo de resultado. **Jamás** registres la contraseña.

### `CerrarSesionAsync`

Si no hay sesión, no hace nada. Si la hay, audita `Logout`, guarda, llama a `ISesionActual.Cerrar()` y registra en el log.

### Tests (`ServicioAutenticacionTests`, `BaseDatosEnMemoria` + `FakeTimeProvider` + hasher real)

Un test por cada caso:
- vacío;
- inexistente (audita con `UsuarioId` null);
- contraseña incorrecta (incrementa el contador);
- quinto fallo (bloquea 5 min, contador en 0);
- intento durante el bloqueo, aun con la contraseña correcta, rechazado sin cambiar el contador;
- bloqueo vencido (avanza el `FakeTimeProvider` 5 min y 1 s) → login exitoso;
- inactivo con contraseña correcta → `UsuarioInactivo`;
- inactivo con contraseña incorrecta → `CredencialesInvalidas`;
- éxito (actualiza `UltimoAcceso`, reinicia el contador, audita `Login`, abre la sesión);
- nombre con mayúsculas y espacios ("  ADMIN ") → éxito;
- `CerrarSesionAsync` audita `Logout` y cierra la sesión;
- ninguna fila de `Auditorias` contiene la contraseña, el hash ni el salt (búsqueda en `DatosAntesJson`, `DatosDespuesJson` y `Motivo`).

---

## Parte 6 — HU-F1-02: Sesión, permisos y autorización por rol

1. Implementa `MatrizPermisos` y `SesionActual` según la Parte 3.
2. **Autorización en servicios (defensa en profundidad):** todo método de servicio que exija un permiso llama a `ISesionActual.ExigirPermiso(...)` como **primera línea**, antes de cualquier I/O. La UI oculta opciones, pero el servicio es la autoridad.
3. Registra `ISesionActual` como Singleton en `AddApplication`.

**Tests:**
- `MatrizPermisosTests`: un `[Theory]` con `[MemberData]` que recorre **todas** las combinaciones Rol × Permiso contra la tabla D1, escrita a mano en el test y no derivada de `MatrizPermisos`.
- Test extra: todo valor de `Permiso` pertenece al rol Administrador, para que un permiso nuevo no quede huérfano.
- `SesionActualTests`:
  - sin sesión, `TienePermiso` → false y `ExigirPermiso` → `NoAutorizadoException`;
  - `Iniciar` dispara `SesionCambiada` y `EstaAutenticada` pasa a true;
  - `Cerrar` limpia la sesión;
  - `MarcarPasswordCambiada` actualiza la bandera.

---

## Parte 7 — HU-F1-06: Cambio de contraseña (antes que el login, porque el login lo necesita)

### 7.1 Servicio: `ServicioAutenticacion.CambiarPasswordPropiaAsync`

1. `ExigirSesion()`.
2. Valida con `ValidarPasswordNueva(...)`. Si hay errores → `ValidacionException`.
3. Carga el usuario de la sesión:
   - si no existe o está inactivo → `NoAutorizadoException`;
   - si `actual` no verifica → `ValidacionException(CamposUsuario.PasswordActual, "La contraseña actual es incorrecta.")`. **No** incrementes intentos fallidos aquí.
4. Hashea la nueva contraseña, `DebeCambiarPassword = false` y audita `CambioPassword`. En `Antes` y `Despues` va el snapshot `UsuarioAuditoria`, **sin** datos de la contraseña.
5. Guarda y llama a `ISesionActual.MarcarPasswordCambiada()`.

**Tests:** éxito (el login posterior funciona con la nueva y falla con la anterior); actual incorrecta; nueva igual a la actual; nueva igual al usuario; confirmación distinta; sin sesión → `NoAutorizadoException`; la auditoría no contiene la contraseña.

### 7.2 Formulario `WinForms/Formularios/Seguridad/FrmCambioPassword.cs`

- Hereda de `FormBase`. `FormBorderStyle = FixedDialog`, sin maximizar ni minimizar.
- Controles: `txtActual`, `txtNueva`, `txtConfirmacion` (`UseSystemPasswordChar = true`, `MaxLength = 100`), `btnAceptar` (`AcceptButton`), `btnCancelar` (`CancelButton`), `lblTitulo` y un `ErrorProvider`.
- Propiedad `bool Obligatorio { get; set; }`:
  - `true` (primer ingreso): título "Debe cambiar su contraseña para continuar", y cancelar significa "cerrar sesión". Ese caso lo maneja `ContextoAplicacion`.
  - `false` (desde el menú): cancelar solo cierra el diálogo.
- `btnAceptar_Click` → `EjecutarAsync(...)`:
  - si hay éxito, `DialogResult = OK` y se cierra;
  - si hay `ValidacionException`, se muestran los errores en los controles con el helper de la Parte 7.3 y el foco va al primer control con error.
- Después de cada intento fallido se limpian los tres campos de contraseña.
- Los controles se crean en `InitializeComponent()` escrito a mano, como `FrmPrincipal` (no hay diseñador). Usa `TableLayoutPanel` para que el formulario se adapte al DPI.

### 7.3 Helper de validación en `FormBase`

Agrega a `FormBase`:
```csharp
protected bool MostrarErroresValidacion(Exception ex, ErrorProvider proveedor, IReadOnlyDictionary<string, Control> controlesPorCampo);
```
- Limpia los errores previos.
- Si `ex` es `ValidacionException`, asigna cada error a su control, enfoca el primero y devuelve `true`.
- Los errores cuyo campo no tiene control se juntan en un único `MessageBox` de advertencia.
- Cualquier otra excepción devuelve `false`, y el llamador la relanza para que la maneje `EjecutarAsync`.

**Test (STA):** con una `ValidacionException` de dos campos, los dos controles quedan con su mensaje y el foco en el primero.

---

## Parte 8 — HU-F1-03 y HU-F1-04: Login, contexto de aplicación y shell

### 8.1 `WinForms/Formularios/Seguridad/FrmLogin.cs` (HU-F1-03)

- Inyecta `IServicioAutenticacion`. Hereda de `FormBase`.
- Estilo: `FixedDialog`, `StartPosition = CenterScreen`, `ShowInTaskbar = true`, título "Sistema de Pesaje para Faeneo — Iniciar sesión".
- Controles:
  - `txtUsuario` (`MaxLength = 30`) y `txtPassword` (`UseSystemPasswordChar`, `MaxLength = 100`);
  - `btnIngresar` (`AcceptButton`) y `btnSalir` (`CancelButton`);
  - `lblMensaje` en rojo, vacío al inicio;
  - `lblBloqMayus`, visible solo con Bloq Mayús activo: se revisa `Control.IsKeyLocked(Keys.CapsLock)` en `KeyUp` de `txtPassword` y en `Activated`;
  - `lblVersion`, con la versión del ensamblado.
- Enter en `txtUsuario` mueve el foco a `txtPassword`. Enter en `txtPassword` ingresa (por `AcceptButton`).
- `btnIngresar` → `EjecutarAsync(..., btnIngresar)`:
  - llama a `IniciarSesionAsync`;
  - si no hay éxito, `lblMensaje.Text = resultado.Mensaje`, se limpia `txtPassword` y se le da el foco;
  - si hay éxito, se expone `bool DebeCambiarPassword` como propiedad, `DialogResult = OK` y se cierra.
- `btnSalir` → `DialogResult = Cancel`.
- No muestres `MessageBox` para los fallos de credenciales: solo `lblMensaje`.

### 8.2 `WinForms/Infraestructura/ContextoAplicacion.cs` (flujo de sesión)

`public sealed class ContextoAplicacion : ApplicationContext`. Recibe `IFormFactory`, `IServicioAutenticacion`, `ISesionActual` y `ILogger<ContextoAplicacion>`.

- **No** asignes `MainForm`, para que el cierre de un formulario no termine la aplicación. Gestiona cada formulario manualmente con su `FormClosed`.
- El constructor llama a `MostrarLogin()`.
- `MostrarLogin()`: crea `FrmLogin` con la fábrica, se suscribe a `FormClosed` y hace `Show()`. En `FormClosed`:
  - si `DialogResult == OK`, llama a `ContinuarTrasLogin(login.DebeCambiarPassword)`;
  - si no, `ExitThread()`.
- `ContinuarTrasLogin(bool debeCambiar)`:
  - si `debeCambiar`, crea `FrmCambioPassword { Obligatorio = true }` y lo muestra modal (`ShowDialog()`; es seguro, el bucle ya corre). Si el resultado no es OK, `await CerrarSesionAsync()` y vuelve a `MostrarLogin()`;
  - luego `MostrarShell()`.
- `MostrarShell()`: crea `FrmPrincipal`, se suscribe a `FormClosed` y hace `Show()`. En `FormClosed`, **método `async void` con try/catch que llama a `ManejadorErrores.Mostrar`**:
  1. `await _autenticacion.CerrarSesionAsync()`.
  2. Si `shell.Solicitud == SolicitudCierreShell.CerrarSesion` → `MostrarLogin()`; si no → `ExitThread()`.
- Un error al auditar el logout no debe impedir que se cierre la app: registra en el log y continúa.
- `Program.cs`: reemplaza la creación de `FrmPrincipal` y `WinFormsApp.Run(mainForm)` por:
  ```csharp
  using var contexto = ActivatorUtilities.CreateInstance<ContextoAplicacion>(host.Services);
  WinFormsApp.Run(contexto);
  ```
  No registres `ContextoAplicacion` en el contenedor.

### 8.3 Shell `FrmPrincipal` (HU-F1-04)

Reescribe `FrmPrincipal`. Inyecta `ISesionActual`, `IFormFactory` y `TimeProvider`.

- `public enum SolicitudCierreShell { SalirAplicacion, CerrarSesion }` y la propiedad `Solicitud`, que por defecto es `SalirAplicacion`.
- Título: "Sistema de Pesaje para Faeneo — {NombreCompleto} ({Rol})". `WindowState = Maximized`.
- **MenuStrip.** Cada ítem ejecutable lleva en `Tag` un `OpcionMenu(Permiso? Permiso, string? FaseFutura)` (record en WinForms).

  | Menú | Ítem | Permiso | Estado |
  |---|---|---|---|
  | Pesaje | Registrar pesaje (F2 de teclado) | Pesar | F4 |
  | Boletas | Reimprimir boleta | ReimprimirBoleta | F5 |
  | Boletas | Buscar / editar boleta | EditarBoleta | F6 |
  | Boletas | Anular boleta | AnularBoleta | F6 |
  | Catálogos | Ganaderos | GestionarCatalogos | F2 |
  | Catálogos | Clientes | GestionarCatalogos | F2 |
  | Catálogos | Fleteros | GestionarCatalogos | F2 |
  | Reportes | Reportes de pesaje | VerReportes | F7 |
  | Reportes | Boletas anuladas | VerReportes | F7 |
  | Administración | Usuarios | GestionarUsuarios | **F1 (activo)** |
  | Administración | Parámetros del sistema | GestionarParametros | F2 |
  | Administración | Configuración de balanza | ConfigurarBalanza | F3 |
  | Administración | Visor de auditoría | VerAuditoria | F6 |
  | Utilitarios | Backup / Restaurar | BackupRestore | F8 |
  | Utilitarios | Importar datos legados | ImportarLegado | F8 |
  | Utilitarios | Visor de logs | GestionarParametros | F8 |
  | Sesión | Cambiar contraseña… | *(ninguno)* | **F1 (activo)** |
  | Sesión | Cerrar sesión (Ctrl+Shift+L) | *(ninguno)* | **F1 (activo)** |
  | Sesión | Salir | *(ninguno)* | **F1 (activo)** |

- **`WinForms/Infraestructura/AutorizadorMenu.cs`:** clase estática pura.
  - `void Aplicar(ToolStripItemCollection items, Func<Permiso, bool> tienePermiso)`.
  - Por cada `ToolStripMenuItem` con `Tag is OpcionMenu`: `Visible = Permiso is null || tienePermiso(Permiso)`. Si `FaseFutura` no es null, `Enabled = false` y `ToolTipText = $"Disponible en la fase {FaseFutura}"`.
  - Recursivo. Un padre sin hijos visibles se oculta. **Usa `Available`** en lugar de `Visible` para leer el estado, porque `Visible` devuelve false si el formulario aún no se mostró.
- **StatusStrip:** usuario, rol, fecha y hora local (un `System.Windows.Forms.Timer` de 1 s que usa `TimeProvider`) y versión.
- **Acciones:**
  - *Usuarios* → `_formFactory.Crear<FrmUsuarios>()` y `ShowDialog(this)` dentro de `using`.
  - *Cambiar contraseña* → `FrmCambioPassword { Obligatorio = false }`.
  - *Cerrar sesión* → confirmación Sí/No; si es Sí, `Solicitud = CerrarSesion` y `Close()`.
  - *Salir* → `Close()`.
- **Revalidación:** en el handler de cada acción con permiso, llama a `_sesion.ExigirPermiso(...)` dentro de `Ejecutar`, antes de abrir el formulario.
- El `Timer` se detiene y se desecha en `Dispose(bool)`.

### 8.4 Tests de UI (STA)

- `AutorizadorMenuTests`:
  - un operador ve Pesaje y Boletas › Reimprimir; no ve Administración ni Catálogos;
  - un administrador ve todo;
  - un menú padre con todos sus hijos ocultos queda oculto;
  - un ítem con `FaseFutura` queda deshabilitado con su ToolTip.
- `FrmLoginTests`: con un `IServicioAutenticacion` falso (clase de prueba escrita a mano, sin librerías de mocks), invocar el ingreso con un resultado `CredencialesInvalidas` deja el mensaje en `lblMensaje`, limpia `txtPassword` y deja `DialogResult == None`. Expón el disparo con un método `internal Task IngresarAsync()` y agrega `InternalsVisibleTo("PesajeFaeneo.Tests")` en el `.csproj` de WinForms.
- `FrmPrincipalTests`: crear el formulario con una sesión de Operador y comprobar que el ítem Administración › Usuarios queda no disponible.
- Actualiza `ContenedorDITests`:
  - resuelve `IServicioAutenticacion`, `ISesionActual` e `IUnidadDeTrabajoFactory`;
  - crea `FrmLogin`, `FrmPrincipal`, `FrmUsuarios`, `FrmUsuarioEdicion` y `FrmCambioPassword` con la fábrica, en STA. Así, `ValidateOnBuild` y `ValidateScopes` cubren sus dependencias.

---

## Parte 9 — HU-F1-05: Gestión de usuarios (CRUD)

### 9.1 Servicio `Application/Seguridad/ServicioUsuarios.cs` (+ `IServicioUsuarios`)

DTOs (records en `Application/Seguridad/Dtos/`):
```csharp
public sealed record UsuarioListado(int Id, string NombreUsuario, string NombreCompleto, Rol Rol, bool Activo, bool Bloqueado, DateTime? UltimoAcceso);
public sealed record CrearUsuario(string NombreUsuario, string NombreCompleto, Rol Rol, string Password, string Confirmacion);
public sealed record EditarUsuario(int Id, string NombreCompleto, Rol Rol);
```

Métodos. **Cada uno** empieza con `ExigirPermiso(Permiso.GestionarUsuarios)`:

| Método | Reglas | Auditoría |
|---|---|---|
| `ListarAsync(bool incluirInactivos)` | `Bloqueado = BloqueadoHasta > ahora` | — |
| `ObtenerAsync(int id)` → `UsuarioListado` | inexistente → `EntidadNoEncontradaException("Usuario", id)` | — |
| `CrearAsync(CrearUsuario)` → `int` | normaliza, `ValidarAlta`, duplicado → `ValidacionException(NombreUsuario, "Ya existe un usuario con ese nombre.")` (verificación previa; la `ConflictoException` de la unidad cubre la carrera), `DebeCambiarPassword = true`, `CreadoEn = ahora` | `Crear`, `Despues` = snapshot |
| `EditarAsync(EditarUsuario)` | `ValidarEdicion`; editarse a sí mismo cambiando el rol → `ReglaNegocioException("USR_PROPIO_ROL", "No puede cambiar su propio rol.")`; degradar al último administrador activo → `ReglaNegocioException("USR_ULTIMO_ADMIN", "Debe existir al menos un administrador activo.")` | `Modificar`, antes y después |
| `CambiarEstadoAsync(int id, bool activo)` | desactivarse a sí mismo → `ReglaNegocioException("USR_PROPIO_ESTADO", "No puede desactivar su propio usuario.")`; desactivar al último administrador activo → `USR_ULTIMO_ADMIN`; si ya está en ese estado, no hace nada ni audita | `Modificar`, antes y después, `Motivo` = "Activación" o "Desactivación" |
| `RestablecerPasswordAsync(int id, string nueva, string confirmacion)` | `ValidarPasswordNueva(nombre, nueva, conf, actual: null)`; `DebeCambiarPassword = true`, `IntentosFallidos = 0`, `BloqueadoHasta = null` | `RestablecerPassword`, sin datos de la contraseña |

Todo en una unidad de trabajo por operación, con la auditoría en el mismo `GuardarCambiosAsync`. Se registra Transient.

**Tests (`ServicioUsuariosTests`):**
- sin sesión o con sesión de Supervisor → `NoAutorizadoException` en cada método;
- alta válida, que queda con `DebeCambiarPassword` y auditoría `Crear`;
- alta duplicada con otra capitalización ("Juan" contra "juan") → `ValidacionException`;
- alta inválida (cada regla de validación);
- edición;
- cambio del propio rol;
- degradar al último administrador;
- desactivarse a sí mismo;
- desactivar al último administrador;
- con dos administradores sí se puede desactivar uno;
- reactivar;
- estado sin cambio → sin auditoría;
- restablecer desbloquea y fuerza el cambio, y el login posterior pide cambiar la contraseña;
- ninguna auditoría contiene la contraseña, el hash ni el salt.

### 9.2 `WinForms/Formularios/Seguridad/FrmUsuarios.cs`

- Inyecta `IServicioUsuarios`, `IFormFactory` y `TimeProvider`.
- `DataGridView` de solo lectura, con selección de fila completa, sin agregar filas y `AutoGenerateColumns = false`. Columnas: Usuario, Nombre completo, Rol, Activo (Sí/No), Bloqueado (Sí/No), Último acceso (hora local `dd/MM/yyyy HH:mm`, o "—").
- `chkMostrarInactivos` (recarga al cambiar). Botones: `btnNuevo`, `btnEditar` (también con doble clic o Enter en la grilla), `btnActivarDesactivar` (el texto cambia según la fila), `btnRestablecer` y `btnCerrar`.
- La carga ocurre en `Shown` con `EjecutarAsync`. Tras cada operación se recarga y se conserva la selección por `Id`.
- Desactivar y restablecer piden confirmación Sí/No con el nombre del usuario en el mensaje.
- Restablecer abre un pequeño diálogo `FrmRestablecerPassword` (nueva + confirmación, mismo helper de validación). Al terminar, informa: "La contraseña fue restablecida. El usuario deberá cambiarla al ingresar.".

### 9.3 `WinForms/Formularios/Seguridad/FrmUsuarioEdicion.cs`

- Un solo formulario para alta y edición: `ConfigurarAlta()` / `ConfigurarEdicion(UsuarioListado)`.
- Campos:
  - `txtNombreUsuario`: solo lectura en edición; en alta se pasa a minúsculas al salir del campo;
  - `txtNombreCompleto`;
  - `cmbRol` (`DropDownList`, con los textos "Administrador", "Supervisor" y "Operador");
  - `txtPassword` y `txtConfirmacion`: visibles solo en alta.
- `btnGuardar` → `EjecutarAsync`. Los errores de validación van al `ErrorProvider` por campo, con el mapa `CamposUsuario` → control. Las `ReglaNegocioException` las muestra `ManejadorErrores`, como siempre.
- En edición, si el usuario edita su propio registro, `cmbRol` queda deshabilitado. La regla también se valida en el servicio.

**Tests (STA):** crear `FrmUsuarioEdicion` en modo alta y en modo edición: la visibilidad de los campos de contraseña y el `ReadOnly` del usuario son los correctos.

---

## Parte 10 — Cierre

1. **Arquitectura:** `ArquitecturaTests` sigue en verde. Agrega: Application no referencia `System.Text.Json` de terceros (el de la BCL está bien, no es un paquete NuGet).
2. **`Agents.md`:**
   - §2: F1 ✅ Completado, 6/6, fecha; F2 "⏳ Siguiente en turno".
   - §3: marca las 6 HUs de F1.
   - §4: agrega unidad de trabajo + auditoría atómica, `ISesionActual` singleton, `ExigirPermiso` como primera línea de cada servicio, `ContextoAplicacion` y la regla de no bloquear el hilo de UI.
   - §5: entrada de bitácora "Fase F1" con build, conteo de tests, la migración `F1_BloqueoUsuario`, las decisiones D1–D10 aplicadas y las desviaciones.
3. **`README.md`:** sección "Primer ingreso": usuario `admin`, contraseña `admin`; el sistema obliga a cambiarla.
4. **Verificaciones finales:**
   ```powershell
   .\build.ps1
   ```
   ```bash
   dotnet ef migrations has-pending-model-changes -p src/PesajeFaeneo.Infrastructure -s src/PesajeFaeneo.WinForms
   ```
5. **Prueba de arranque:**
   - Borra `C:\ProgramData\PesajeFaeneo\pesaje.db*` (datos de desarrollo) y lanza `src\PesajeFaeneo.WinForms\bin\Release\net8.0-windows\PesajeFaeneo.WinForms.exe`.
   - Verifica que el proceso sigue vivo a los 5 s, que existe la BD con la migración `F1_BloqueoUsuario` aplicada (consulta `__EFMigrationsHistory`) y que el log tiene "Aplicación iniciada" sin errores.
   - Cierra el proceso.
   - No intentes automatizar clics.
6. **Informe final** (tu única respuesta al coordinador):
   - Tabla HU → archivos creados o modificados → tests añadidos.
   - Conteo de tests antes → después, warnings y resultado de `has-pending-model-changes`.
   - Desviaciones del plan y su motivo.
   - **Checklist de prueba manual** para una persona:
     1. `admin`/`admin` obliga a cambiar la contraseña, y cancelar vuelve al login.
     2. Cinco fallos bloquean al usuario.
     3. Un operador no ve Administración.
     4. Crear un usuario operador, entrar con él y comprobar que se le pide cambiar la contraseña.
     5. No se puede desactivar al último administrador.
     6. Cerrar sesión vuelve al login.
     7. Salir cierra la app.
     8. Las entradas de auditoría esperadas existen en la tabla `Auditorias`.

---

## Fuera de alcance (no implementar; solo mencionarlo si aplica)

- Cierre de sesión por inactividad y bloqueo de pantalla.
- Recuperación de contraseña sin administrador. Si se pierden todas las credenciales de administrador, la vía será un utilitario en F8/F9.
- El visor de auditoría (F6-05) y el diff genérico de auditoría (F6-04). En F1 solo se escriben entradas.
- Leer `ParametrosSistema` para mostrar el nombre de la empresa en el login (llega con F2-01).
