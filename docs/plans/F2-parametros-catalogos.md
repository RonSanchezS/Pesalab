# Plan de implementación F2 — Parámetros y catálogos

**Autor:** revisión senior (2026-09-24)
**Ejecutor:** agente de implementación (un solo agente, sin subagentes)
**Punto de partida:** F1 completado (`f18321d`) + corrección pendiente de `FrmUsuarios` (ver Parte 0, regla 3). Build Release con 0 warnings; **175 tests en verde** (medido el 2026-09-24 con esa corrección aplicada).
**HUs:** HU-F2-01 … HU-F2-06 (ver `Agents.md §3`).

Este documento es **la especificación**. Si algo no está aquí, se aplican las convenciones de `Agents.md §4` y, como plantilla de código, lo que ya hace F1 (`ServicioUsuarios`, `FrmUsuarios`, `FrmUsuarioEdicion`). Si algo contradice `Agents.md`, manda este documento y la diferencia se anota en el informe final.

La **Parte 12** recoge la revisión de este plan: huecos detectados, cómo se cubrieron y riesgos que quedan abiertos. **Léela antes de empezar**; varias reglas de las Partes 2–10 existen por lo que se explica allí.

---

## Parte 0 — Reglas de comportamiento del agente (obligatorias)

1. **Trabajas tú mismo.** No lances subagentes ni dejes trabajo en segundo plano.
2. **No te detengas hasta terminar** las Partes 1 a 11. Responde **una sola vez**, al final, con el informe de la Parte 11. Si algo te bloquea, documéntalo, elige la opción más conservadora compatible con este plan y continúa.
3. **Git:**
   - Antes de empezar, `git status` debe estar **limpio**. Hoy **no lo está**: hay una corrección sin commit en `src/PesajeFaeneo.WinForms/Formularios/Seguridad/FrmUsuarios.cs` y un test nuevo sin seguimiento `tests/PesajeFaeneo.Tests/FrmUsuariosTests.cs`. El coordinador debe commitearlos (por ejemplo `fix(F1): recargar grilla de usuarios tras cada operación`) antes de entregarte el plan. Si al empezar no está limpio, **detente y repórtalo**; no mezcles esos cambios con F2.
   - Crea la rama `feat/f2-catalogos` desde el HEAD actual.
   - **No hagas commit ni push.** Deja todo en el árbol de trabajo.
4. **Compuerta de calidad después de cada Parte:**
   ```bash
   dotnet build src/PesajeFaeneo.sln -c Release
   ```
   ```bash
   dotnet test src/PesajeFaeneo.sln -c Release
   ```
   0 warnings (`TreatWarningsAsErrors=true`), 0 errores y todos los tests en verde. **No avances con rojo.** Nunca desactives un warning, un test ni un analizador para pasar la compuerta. Si un test **existente** falla por una restricción nueva de esta fase, corrige los **datos del test**, nunca la restricción, y anótalo en el informe.
5. **Convenciones:** identificadores, comentarios, mensajes de UI y de log en **español**; imita el estilo y la densidad de comentarios del código existente. Finales de línea **CRLF**, UTF-8 (`.editorconfig`): al terminar cada Parte verifica que ningún archivo nuevo o tocado quede con LF (`git diff --stat` muestra el aviso "LF will be replaced by CRLF"; conviértelo). xUnit solo con `Assert`. Siempre `TimeProvider` inyectado; en tests, `FakeTimeProvider`. Fechas persistidas en UTC.
6. **Capas** (las hace cumplir `ArquitecturaTests`): Domain sin NuGet; Application sin EF, WinForms, Serilog, Hosting **ni `System.Drawing`**; Infrastructure sin WinForms.
7. **Migraciones:** no modifiques `Inicial` ni `F1_BloqueoUsuario`. Los cambios de esquema van en **una** migración nueva (Parte 2). Al terminar, `dotnet ef migrations has-pending-model-changes` no debe reportar cambios.
8. **Librerías:** antes de escribir código contra una API de librería (EF Core 8 transacciones y migraciones SQLite, `Microsoft.Data.Sqlite`, WinForms `ListBox`/`TextBox`/`PreviewKeyDown`/`OpenFileDialog`/`PictureBox`), consulta la documentación con Context7 (`ToolSearch` → `select:mcp__context7__resolve-library-id,mcp__context7__query-docs`).
9. **Hilo de UI:** nunca bloquees el hilo de UI con `.Result`, `.Wait()` ni `.GetAwaiter().GetResult()` una vez iniciado el bucle de mensajes. Los manejadores de eventos asíncronos son `async void` (o lambdas `async (_, _) =>`) y delegan en `FormBase.EjecutarAsync`.
10. **Regla de reentrada de `FormBase` (lección de F1, obligatoria):** `EjecutarAsync` descarta en silencio cualquier invocación mientras otra está en curso (`_ocupado`). Por lo tanto:
    - Los métodos de carga (`CargarAsync`) **no** se envuelven en `EjecutarAsync`; los envuelve **el llamador** (Shown, checkbox, botones). Si una acción envuelta necesita recargar, llama a `CargarAsync()` directamente dentro de su lambda.
    - Nunca anides `EjecutarAsync` dentro de otro `EjecutarAsync`.
    - Todo control cuyo evento dispare una recarga (p. ej. `chkMostrarInactivos`) se pasa como `botonDisparador`, para que quede deshabilitado durante la operación y el usuario no pueda cambiar su estado mientras su evento sería descartado.
11. **Tests de WinForms:** todo test que cree un `Form` o un control corre dentro de `HiloSta.Ejecutar(...)`, hace `Show()` del formulario cuando lo que se prueba depende de `Visible`, y desecha lo que crea. Los servicios falsos son clases escritas a mano (sin librerías de mocks). Los `MessageBox` no se pueden probar: expón ganchos `internal` que salten la confirmación, como hace `FrmUsuarios`.
12. **Seguridad:** sin puertas traseras, sin formularios "de demostración" ocultos, sin rutas de archivo absolutas persistidas en la BD.

---

## Parte 1 — Decisiones de diseño (asumidas; el usuario puede cambiarlas)

Definitivas para esta ejecución. Centralízalas como constantes (`ReglasCatalogos`, `ReglasParametros`, Parte 5) para que cambiarlas sea trivial.

| # | Decisión | Valor |
|---|---|---|
| D1 | Normalización de textos de catálogo | `Trim()`, colapso de espacios internos a uno y **`ToUpperInvariant()`**. Aplica a `Ganadero.Marca`, `Ganadero.Nombre`, `Cliente.Nombre`, `Cliente.Direccion`, `Fletero.Nombre`. `null` → `""`. **Motivo:** la collation `NOCASE` de SQLite solo pliega A–Z ASCII; "peña" y "PEÑA" serían distintos para el índice único y para las búsquedas. Guardando todo en mayúsculas (como el sistema legado y los datos semilla) la unicidad y las comparaciones son exactas. |
| D2 | Normalización de parámetros y celular | `Trim()` + colapso de espacios, **sin** pasar a mayúsculas (el nombre de la empresa se imprime tal cual lo escribe el administrador). |
| D3 | Marca del ganadero | Tras normalizar: 1 a 5 caracteres, sin espacios, patrón `^[\p{Lu}\p{Nd}&./-]{1,5}$`. Única (índice existente, NOCASE). |
| D4 | Nombres | Ganadero, cliente y fletero: 3 a 100 caracteres tras normalizar y al menos una letra (`\p{L}`). **No** son únicos (puede haber homónimos; la marca o el código los distinguen). |
| D5 | Código de cliente | Entero de 1 a 999 999, único. En el alta se **sugiere** `max(Codigo) + 1` contando también inactivos (o 1 si no hay), editable por el usuario. |
| D6 | Claves de negocio | `Marca` y `Codigo` solo se pueden cambiar si el registro **no tiene pesajes**. Con pesajes → `ReglaNegocioException` (`GAN_MARCA_EN_USO` / `CLI_CODIGO_EN_USO`). Nombre y dirección siempre son editables (queda auditado). |
| D7 | Borrado | **No existe** borrado físico en ningún catálogo (FK `Restrict` desde `Pesajes`). Solo activar/desactivar. Un alta cuya marca o código pertenece a un registro **inactivo** se rechaza con un mensaje que indica reactivarlo. |
| D8 | Fleteros | Máximo **4 activos**, uno por orden F1..F4 (índice único filtrado existente `Activo = 1`). Celular obligatorio: 6 a 20 caracteres de `[0-9+()\- ]` con al menos 6 dígitos. Los inactivos no ocupan orden. |
| D9 | Permisos | **Lectura** para búsqueda/boleta (`ListarParaBusquedaAsync`, `ListarActivosAsync`, `IServicioParametros.ObtenerAsync`): solo exige sesión (`ExigirSesion`), porque el operador de F4/F5 necesita ganaderos, clientes, fleteros y datos de empresa. **Gestión** (listar para administrar, obtener, crear, editar, cambiar estado): `GestionarCatalogos`. **Actualizar parámetros:** `GestionarParametros`. El alta rápida de F4-07 (`AltaRapidaCatalogos`) **no** se implementa aquí (ver Parte 12, R9). |
| D10 | Auditoría de catálogos | `EntidadId` = **Id numérico** del registro (estable aunque cambie marca, código u orden). Como el Id solo existe después del INSERT, el alta usa una **transacción explícita** con dos `GuardarCambiosAsync` (Parte 4.2). `Antes`/`Despues` siempre con **snapshots** (records), nunca la entidad. Acciones: `Crear`; `Modificar` (edición, `Motivo` = "Modificación de datos"; cambio de estado, `Motivo` = "Activación" / "Desactivación"). Parámetros: `CambioParametros`, `EntidadId = "1"`. |
| D11 | Sin cambios = sin auditoría | Si una edición o cambio de estado no modifica nada (snapshot antes == después), el servicio **no guarda ni audita**. |
| D12 | Parámetros editables | `NombreEmpresa` (3–100, obligatorio), `Direccion` (≤150), `Telefono` (≤50), `Ciudad` (≤100), `RegistroSanitario` (≤100), `TelefonosReclamos` (≤100), `ObservacionPorDefecto` (≤250, puede ser vacía), logo y `SiguienteNumeroBoleta`. `ConfiguracionInicialCompleta` **no** se toca en F2 (le pertenece a F9-04). |
| D13 | Siguiente número de boleta | Editable (necesario para continuar la numeración del sistema legado). Rango 1 a 999 999 999. Si cambia, debe ser **mayor** que el último `NumeroBoleta` existente en `Pesajes` (si hay). La UI pide confirmación explícita. Solo se escribe esa columna si el valor cambió (ver Parte 12, R6). |
| D14 | Logo | PNG, JPG/JPEG o BMP; 1 byte a 1 MiB; se valida la firma binaria (no solo la extensión). Se **copia** a `AppPaths.LogoDir` con nombre `logo-{UTC yyyyMMddHHmmssfff}{ext en minúsculas}` y en `LogoPath` se guarda **solo el nombre de archivo** (portable para backup/restauración de F8). Se puede quitar. El archivo anterior se borra (mejor esfuerzo) solo después de confirmar el guardado. |
| D15 | Búsqueda / autocompletado | En memoria sobre la lista activa (catálogos de cientos a pocos miles de filas), insensible a mayúsculas **y acentos** (`TextoBusqueda.Plegar`), con ranking definido en la Parte 8.1, máximo 20 resultados. Sin consultas SQL por pulsación. |
| D16 | Concurrencia | Sin token de concurrencia en F2 (llega en F6 para `Pesaje` y `ParametrosSistema`). Último en guardar gana; EF solo actualiza las columnas modificadas. |
| D17 | Menú | Catálogos › Ganaderos / Clientes / Fleteros y Administración › Parámetros del sistema pasan a **activos** (`FaseFutura = null`) con su manejador. |

---

## Parte 2 — Esquema: restricciones de catálogos (base de HU-F2-02)

**Archivos:** `Infrastructure/Persistencia/Configuraciones/GanaderoConfiguration.cs`, `ClienteConfiguration.cs`, migración nueva, `tests/.../PersistenciaIntegracionTests.cs`, test nuevo de migración.

> SQLite **no** hace cumplir `HasMaxLength` (el tipo `TEXT` no tiene longitud). Las CK son la única defensa de BD para los rangos.

1. `GanaderoConfiguration`: `builder.ToTable("Ganaderos", t => t.HasCheckConstraint("CK_Ganaderos_Marca", "length(Marca) BETWEEN 1 AND 5"));`
2. `ClienteConfiguration`: `builder.ToTable("Clientes", t => t.HasCheckConstraint("CK_Clientes_Codigo", "Codigo BETWEEN 1 AND 999999"));`
3. Genera la migración:
   ```bash
   dotnet ef migrations add F2_Catalogos -p src/PesajeFaeneo.Infrastructure -s src/PesajeFaeneo.WinForms -o Persistencia/Migraciones
   ```
4. **Revisa el SQL generado** (las CK en SQLite reconstruyen la tabla):
   ```bash
   dotnet ef migrations script F1_BloqueoUsuario F2_Catalogos -p src/PesajeFaeneo.Infrastructure -s src/PesajeFaeneo.WinForms
   ```
   Debe contener, para `Ganaderos` y `Clientes`: `PRAGMA foreign_keys = 0;` antes de la reconstrucción, `COLLATE NOCASE` en `Marca`, `Ganaderos.Nombre` y `Clientes.Nombre`, el `INSERT INTO ... SELECT` de copia, la recreación de `IX_Ganaderos_Marca` e `IX_Clientes_Codigo` (únicos) y `PRAGMA foreign_keys = 1;` al final. Si falta algo, **no** edites el SQL a mano: detente, revisa la configuración y regenera. Anota el resultado en el informe.

**Tests (en `PersistenciaIntegracionTests`, con los helpers que ya use esa clase):**
- `Ganadero_MarcaVacia_LanzaDbUpdateException` y `Ganadero_MarcaDeSeisCaracteres_LanzaDbUpdateException`.
- `Cliente_CodigoCero_LanzaDbUpdateException` y `Cliente_CodigoMayorAlMaximo_LanzaDbUpdateException`.

**Test nuevo `MigracionF2Tests.MigrarDesdeF1_ConservaDatosYRelaciones`** (SQLite en memoria, conexión abierta, `ConfigurarSqlite(connection)`):
1. `context.GetService<IMigrator>().MigrateAsync("20260924161919_F1_BloqueoUsuario")`.
2. Inserta con SQL crudo o con el contexto: un usuario, un ganadero (`"ÑA"`, `"PEÑA"`), un cliente (código 110) y un pesaje que los referencia.
3. `MigrateAsync()` (hasta la última).
4. Verifica:
   - las filas siguen ahí con sus valores (incluidos `"ÑA"` y `"PEÑA"` intactos);
   - `PRAGMA foreign_key_check` no devuelve filas y `PRAGMA foreign_keys` vale 1 en la conexión;
   - la collation sobrevivió: con un ganadero `"NA"` ya insertado, insertar `"na"` falla por UNIQUE. Usa letras ASCII a propósito: NOCASE no pliega `Ñ`, y eso lo resuelve el servicio al normalizar a mayúsculas (D1), no la BD.

`ModeloVsMigracionTests` debe seguir en verde.

---

## Parte 3 — Contratos de Application

Crea estas piezas **exactamente con estos nombres y firmas** (puedes añadir comentarios XML).

### 3.1 Persistencia (`Application/Abstracciones/Persistencia/`)

```csharp
/// Transacción explícita de una unidad de trabajo. Desecharla sin ConfirmarAsync revierte.
public interface ITransaccion : IAsyncDisposable
{
    Task ConfirmarAsync(CancellationToken cancellationToken = default);
}

public interface IUnidadDeTrabajo : IAsyncDisposable
{
    IUsuarioRepositorio Usuarios { get; }
    IAuditoriaRepositorio Auditoria { get; }
    IGanaderoRepositorio Ganaderos { get; }        // nuevo
    IClienteRepositorio Clientes { get; }          // nuevo
    IFleteroRepositorio Fleteros { get; }          // nuevo
    IParametrosRepositorio Parametros { get; }     // nuevo
    Task GuardarCambiosAsync(CancellationToken cancellationToken = default);
    /// Solo una transacción activa por unidad; una segunda llamada lanza InvalidOperationException.
    Task<ITransaccion> IniciarTransaccionAsync(CancellationToken cancellationToken = default); // nuevo
}

public interface IGanaderoRepositorio
{
    Task<Ganadero?> ObtenerPorIdAsync(int id, CancellationToken cancellationToken = default);          // con tracking
    Task<Ganadero?> ObtenerPorMarcaAsync(string marca, CancellationToken cancellationToken = default); // con tracking; recibe la marca ya normalizada
    Task<IReadOnlyList<Ganadero>> ListarAsync(bool incluirInactivos, CancellationToken cancellationToken = default); // sin tracking, orden Marca
    Task<bool> TienePesajesAsync(int ganaderoId, CancellationToken cancellationToken = default);
    void Agregar(Ganadero ganadero);
}

public interface IClienteRepositorio
{
    Task<Cliente?> ObtenerPorIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Cliente?> ObtenerPorCodigoAsync(int codigo, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Cliente>> ListarAsync(bool incluirInactivos, CancellationToken cancellationToken = default); // sin tracking, orden Codigo
    Task<bool> TienePesajesAsync(int clienteId, CancellationToken cancellationToken = default);
    Task<int?> ObtenerCodigoMaximoAsync(CancellationToken cancellationToken = default); // incluye inactivos; null si no hay
    void Agregar(Cliente cliente);
}

public interface IFleteroRepositorio
{
    Task<Fletero?> ObtenerPorIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Fletero?> ObtenerActivoPorOrdenAsync(int orden, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Fletero>> ListarAsync(bool incluirInactivos, CancellationToken cancellationToken = default); // sin tracking; activos primero, luego Orden, luego Nombre
    Task<int> ContarActivosAsync(CancellationToken cancellationToken = default);
    void Agregar(Fletero fletero);
}

public interface IParametrosRepositorio
{
    /// Con tracking. La fila Id = 1 la siembra HasData; si faltara, lanza InvalidOperationException.
    Task<ParametrosSistema> ObtenerAsync(CancellationToken cancellationToken = default);
    /// MAX(NumeroBoleta) de Pesajes (vigentes y anulados); null si no hay pesajes.
    Task<long?> ObtenerUltimoNumeroBoletaAsync(CancellationToken cancellationToken = default);
}
```

### 3.2 Almacén de logo (`Application/Abstracciones/IAlmacenLogo.cs`)

```csharp
public interface IAlmacenLogo
{
    /// Valida y copia el archivo a la carpeta de logos. Devuelve SOLO el nombre del archivo nuevo.
    /// Archivo inexistente, extensión no admitida, vacío, > 1 MiB o firma inválida →
    /// ValidacionException(CamposParametros.Logo, mensaje de la tabla 5.3).
    Task<string> GuardarAsync(string rutaOrigen, CancellationToken cancellationToken = default);

    /// Ruta completa del logo, o null si el nombre es null/vacío, no es un nombre de archivo simple
    /// (contiene separadores, "..", caracteres inválidos) o el archivo no existe.
    string? ObtenerRutaCompleta(string? nombreArchivo);

    /// Borra el archivo si existe. Nunca lanza (mejor esfuerzo; registra un warning si falla).
    void Eliminar(string? nombreArchivo);
}
```

### 3.3 Catálogos — DTOs (`Application/Catalogos/Dtos/CatalogoDtos.cs`)

```csharp
public sealed record GanaderoListado(int Id, string Marca, string Nombre, bool Activo);
public sealed record CrearGanadero(string Marca, string Nombre);
public sealed record EditarGanadero(int Id, string Marca, string Nombre);

public sealed record ClienteListado(int Id, int Codigo, string Nombre, string Direccion, bool Activo);
public sealed record CrearCliente(int Codigo, string Nombre, string? Direccion);
public sealed record EditarCliente(int Id, int Codigo, string Nombre, string? Direccion);

public sealed record FleteroListado(int Id, int Orden, string Nombre, string Celular, bool Activo);
public sealed record CrearFletero(int Orden, string Nombre, string Celular);
public sealed record EditarFletero(int Id, int Orden, string Nombre, string Celular);
```

### 3.4 Búsqueda (`Application/Catalogos/Busqueda/`)

```csharp
public sealed record ElementoBusqueda
{
    public int Id { get; }
    public string Codigo { get; }          // Marca del ganadero, código del cliente como texto
    public string Descripcion { get; }     // Nombre
    public string? Detalle { get; }        // Dirección del cliente; null en ganaderos
    public bool Activo { get; }
    internal string CodigoPlegado { get; }       // TextoBusqueda.Plegar(Codigo)
    internal string DescripcionPlegada { get; }  // TextoBusqueda.Plegar(Descripcion)

    public ElementoBusqueda(int id, string codigo, string descripcion, string? detalle = null, bool activo = true);
    public string TextoMostrado => $"{Codigo} — {Descripcion}";
}

public static class TextoBusqueda { public static string Plegar(string? texto); }
public static class MotorBusqueda
{
    public const int MaximoPorDefecto = 20;
    public static IReadOnlyList<ElementoBusqueda> Buscar(IReadOnlyList<ElementoBusqueda> elementos, string? texto, int maximo = MaximoPorDefecto);
    public static ElementoBusqueda? ResolverExacto(IReadOnlyList<ElementoBusqueda> elementos, string? texto); // Parte 8.1
}
```
Agrega `InternalsVisibleTo("PesajeFaeneo.Tests")` al `.csproj` de Application si no existe.

### 3.5 Servicios (`Application/Catalogos/` y `Application/Parametros/`)

```csharp
public interface IServicioGanaderos
{
    Task<IReadOnlyList<GanaderoListado>> ListarAsync(bool incluirInactivos, CancellationToken cancellationToken = default); // GestionarCatalogos
    Task<GanaderoListado> ObtenerAsync(int id, CancellationToken cancellationToken = default);                              // GestionarCatalogos
    Task<IReadOnlyList<ElementoBusqueda>> ListarParaBusquedaAsync(CancellationToken cancellationToken = default);          // sesión; solo activos
    Task<int> CrearAsync(CrearGanadero dto, CancellationToken cancellationToken = default);                                // GestionarCatalogos
    Task EditarAsync(EditarGanadero dto, CancellationToken cancellationToken = default);                                   // GestionarCatalogos
    Task CambiarEstadoAsync(int id, bool activo, CancellationToken cancellationToken = default);                           // GestionarCatalogos
}

public interface IServicioClientes
{
    // Mismos seis métodos con ClienteListado / CrearCliente / EditarCliente, más:
    Task<int> SugerirSiguienteCodigoAsync(CancellationToken cancellationToken = default);                                  // GestionarCatalogos
}

public interface IServicioFleteros
{
    Task<IReadOnlyList<FleteroListado>> ListarAsync(bool incluirInactivos, CancellationToken cancellationToken = default); // GestionarCatalogos
    Task<FleteroListado> ObtenerAsync(int id, CancellationToken cancellationToken = default);                              // GestionarCatalogos
    Task<IReadOnlyList<FleteroListado>> ListarActivosAsync(CancellationToken cancellationToken = default);                 // sesión; orden Orden (para la boleta de F5)
    Task<int> CrearAsync(CrearFletero dto, CancellationToken cancellationToken = default);
    Task EditarAsync(EditarFletero dto, CancellationToken cancellationToken = default);
    Task CambiarEstadoAsync(int id, bool activo, CancellationToken cancellationToken = default);
}

public sealed record ParametrosDto(
    string NombreEmpresa, string Direccion, string Telefono, string Ciudad,
    string RegistroSanitario, string TelefonosReclamos, string ObservacionPorDefecto,
    string? LogoArchivo, string? RutaLogoCompleta, long SiguienteNumeroBoleta,
    long? UltimoNumeroBoletaEmitido, DateTime ModificadoEn);

public enum AccionLogo { SinCambio, Reemplazar, Quitar }

public sealed record ActualizarParametros(
    string NombreEmpresa, string? Direccion, string? Telefono, string? Ciudad,
    string? RegistroSanitario, string? TelefonosReclamos, string? ObservacionPorDefecto,
    long SiguienteNumeroBoleta,
    AccionLogo AccionLogo = AccionLogo.SinCambio,
    string? RutaLogoOrigen = null);   // obligatoria solo si AccionLogo == Reemplazar

public interface IServicioParametros
{
    Task<ParametrosDto> ObtenerAsync(CancellationToken cancellationToken = default);                     // sesión
    Task ActualizarAsync(ActualizarParametros dto, CancellationToken cancellationToken = default);       // GestionarParametros
}
```

Registra los cuatro servicios **Transient** en `AddApplication`.

---

## Parte 4 — Infrastructure

### 4.1 Repositorios (`Infrastructure/Persistencia/Repositorios/`)

`GanaderoRepositorio`, `ClienteRepositorio`, `FleteroRepositorio`, `ParametrosRepositorio`, con el mismo estilo que `UsuarioRepositorio` (constructor con `PesajeDbContext`, `ArgumentNullException`). Detalles:
- `TienePesajesAsync`: `_context.Pesajes.AnyAsync(p => p.GanaderoId == id)` (o `ClienteId`).
- `ObtenerCodigoMaximoAsync`: `MaxAsync(c => (int?)c.Codigo)` — el cast a `int?` evita la excepción de `Max` sobre secuencia vacía.
- `ObtenerUltimoNumeroBoletaAsync`: `MaxAsync(p => (long?)p.NumeroBoleta)`.
- `ObtenerActivoPorOrdenAsync`: `FirstOrDefaultAsync(f => f.Activo && f.Orden == orden)`.
- `ListarAsync` de fleteros: `OrderByDescending(f => f.Activo).ThenBy(f => f.Orden).ThenBy(f => f.Nombre)`.
- `ParametrosRepositorio.ObtenerAsync`: `FirstOrDefaultAsync(p => p.Id == 1)`; si es null → `InvalidOperationException("No existe la fila de parámetros del sistema (Id = 1).")`.

Expónlos en `UnidadDeTrabajo` como propiedades perezosas (`??=`), igual que `Usuarios`.

### 4.2 Transacción explícita en `UnidadDeTrabajo`

```csharp
public async Task<ITransaccion> IniciarTransaccionAsync(CancellationToken cancellationToken = default)
{
    if (_context.Database.CurrentTransaction is not null)
        throw new InvalidOperationException("Ya hay una transacción activa en esta unidad de trabajo.");
    var tx = await _context.Database.BeginTransactionAsync(cancellationToken);
    return new TransaccionEf(tx);
}
```
`TransaccionEf` (clase `internal sealed` en el mismo archivo o en `Persistencia/TransaccionEf.cs`): `ConfirmarAsync` → `CommitAsync`; `DisposeAsync` → `DisposeAsync` del `IDbContextTransaction` (EF revierte si no se confirmó). `GuardarCambiosAsync` no cambia: dentro de una transacción explícita, `SaveChangesAsync` la usa automáticamente.

**Patrón de alta con auditoría** (lo usan los tres servicios de catálogo):
```csharp
await using var uow = await _uowFactory.CrearAsync(ct);
// ... validaciones y verificaciones previas ...
await using var tx = await uow.IniciarTransaccionAsync(ct);
uow.Ganaderos.Agregar(entidad);
await uow.GuardarCambiosAsync(ct);                       // aquí se asigna entidad.Id
uow.Auditoria.Registrar(FabricaAuditoria.Crear(_timeProvider, sesion.Id, AccionAuditoria.Crear,
    EntidadesAuditoria.Ganadero, entidad.Id.ToString(CultureInfo.InvariantCulture),
    despues: FabricaAuditoria.CrearSnapshot(entidad), motivo: "Alta de ganadero"));
await uow.GuardarCambiosAsync(ct);
await tx.ConfirmarAsync(ct);
```
Ediciones y cambios de estado **no** necesitan transacción explícita: el Id ya se conoce y cambio + auditoría van en un único `GuardarCambiosAsync`.

### 4.3 Traducción de errores de clave foránea

En `UnidadDeTrabajo.GuardarCambiosAsync`, dentro del bloque `SqliteErrorCode == 19`, agrega antes del `throw;`:
- mensaje contiene `"FOREIGN KEY"` → `ReglaNegocioException("BD_FK", "El registro está relacionado con otros datos y no se puede guardar así.", ex)`.

### 4.4 `AlmacenLogoArchivos : IAlmacenLogo` (`Infrastructure/Archivos/AlmacenLogoArchivos.cs`)

Dependencias: `AppPaths`, `TimeProvider`, `ILogger<AlmacenLogoArchivos>`. Registro **Singleton** en `AddInfrastructure`.
- Constantes: `TamanoMaximoBytes = 1_048_576`; extensiones `.png`, `.jpg`, `.jpeg`, `.bmp` (comparación `OrdinalIgnoreCase`).
- `GuardarAsync`: valida en este orden (primer fallo lanza): existe → extensión → tamaño (`> 0` y `≤` máximo) → firma de los primeros bytes (PNG `89 50 4E 47 0D 0A 1A 0A`; JPEG `FF D8 FF`; BMP `42 4D`). Luego `Directory.CreateDirectory(LogoDir)` y copia con `File.Copy(origen, destino, overwrite: false)`. Si el nombre destino ya existe (dos guardados en el mismo milisegundo), añade `-1`, `-2`… Lee la firma con `FileStream` + `ReadAtLeastAsync`/bucle, cerrando el archivo antes de copiar.
- `ObtenerRutaCompleta`: rechaza (`null`) si `nombre != Path.GetFileName(nombre)`, si contiene `..` o `Path.GetInvalidFileNameChars()`; combina con `LogoDir`; devuelve `null` si no existe.
- `Eliminar`: mismas comprobaciones de nombre; `try { File.Delete } catch (IOException or UnauthorizedAccessException) { _logger.LogWarning(...) }`.

**Tests (`AlmacenLogoArchivosTests`, directorio temporal propio por test, borrado en `finally`):** PNG válido se copia y devuelve un nombre simple que empieza por `logo-`; extensión `.gif` rechazada; archivo vacío rechazado; archivo > 1 MiB rechazado; archivo `.png` con contenido de texto rechazado; archivo inexistente rechazado; `ObtenerRutaCompleta("..\\x.png")` y `("C:\\x.png")` → null; `Eliminar` de un nombre inexistente no lanza. Genera los bytes de prueba en código (firma PNG + relleno), sin archivos binarios en el repo.

### 4.5 Tests de `UnidadDeTrabajoTests` (añadir)
- Transacción confirmada: ganadero + auditoría con `EntidadId = Id` persisten.
- Transacción **no** confirmada (se desecha sin `ConfirmarAsync`): tras un primer `GuardarCambiosAsync` exitoso del ganadero, no queda ni el ganadero ni la auditoría.
- Segundo `GuardarCambiosAsync` falla por FK (auditoría con `UsuarioId = 9999`) → `ReglaNegocioException` con código `BD_FK`, y tras desechar la transacción el ganadero **no** existe.
- `IniciarTransaccionAsync` dos veces en la misma unidad → `InvalidOperationException`.

---

## Parte 5 — HU-F2-02: Reglas de validación y normalización

### 5.1 `Application/Catalogos/ReglasCatalogos.cs`

```csharp
public static class ReglasCatalogos
{
    public const int LongitudMaximaMarca = 5;
    public const string PatronMarca = @"^[\p{Lu}\p{Nd}&./-]{1,5}$";
    public const int LongitudMinimaNombre = 3;
    public const int LongitudMaximaNombre = 100;
    public const int LongitudMaximaDireccion = 150;
    public const int CodigoClienteMinimo = 1;
    public const int CodigoClienteMaximo = 999_999;
    public const int OrdenFleteroMinimo = 1;
    public const int OrdenFleteroMaximo = 4;
    public const int MaximoFleterosActivos = 4;
    public const int LongitudMinimaCelular = 6;
    public const int LongitudMaximaCelular = 20;
    public const string PatronCelular = @"^[0-9+()\- ]{6,20}$";
    public const int DigitosMinimosCelular = 6;
}

public static class CamposCatalogo
{
    public const string Marca = "Marca";
    public const string Nombre = "Nombre";
    public const string Codigo = "Codigo";
    public const string Direccion = "Direccion";
    public const string Orden = "Orden";
    public const string Celular = "Celular";
}
```

### 5.2 `Application/Catalogos/ValidadorCatalogos.cs` (estática, pura, sin I/O)

- `string NormalizarTexto(string? valor)`: Trim + colapso de espacios (`\s+` → `" "`); `null` → `""`. (Reutiliza la lógica de `ValidadorUsuario.NormalizarNombreCompleto`; muévela a un helper interno compartido `NormalizacionTexto.Colapsar` si así evitas duplicarla, sin cambiar el comportamiento de F1.)
- `string NormalizarMayusculas(string? valor)` = `NormalizarTexto(valor).ToUpperInvariant()`.
- `string NormalizarMarca(string? valor)`: `NormalizarMayusculas` **y además** elimina todos los espacios (una marca "A H" pasa a "AH").
- `IReadOnlyList<ErrorValidacion> ValidarGanadero(string marcaNormalizada, string nombreNormalizado)`
- `IReadOnlyList<ErrorValidacion> ValidarCliente(int codigo, string nombreNormalizado, string direccionNormalizada)`
- `IReadOnlyList<ErrorValidacion> ValidarFletero(int orden, string nombreNormalizado, string celularNormalizado)`

Los validadores **reciben valores ya normalizados** (el servicio normaliza primero y valida después; así la validación ve exactamente lo que se guardará). Todas las regex se construyen con `RegexOptions.CultureInvariant` y un timeout (`TimeSpan.FromMilliseconds(250)`), como campos `static readonly`.

Mensajes exactos:

| Caso | Campo | Mensaje |
|---|---|---|
| Marca vacía o que no cumple el patrón | Marca | "Ingrese la marca: 1 a 5 caracteres (letras, números o & . / -), sin espacios." |
| Nombre fuera de 3–100 o sin ninguna letra | Nombre | "Ingrese el nombre (3 a 100 caracteres)." |
| Código fuera de rango | Codigo | "El código debe ser un número entre 1 y 999999." |
| Dirección > 150 | Direccion | "La dirección no puede superar 150 caracteres." |
| Orden fuera de 1–4 | Orden | "Seleccione un orden entre F1 y F4." |
| Celular vacío, fuera de patrón o con menos de 6 dígitos | Celular | "Ingrese un celular válido (6 a 20 caracteres: números, espacios, +, -, paréntesis)." |

### 5.3 `Application/Parametros/ReglasParametros.cs` + `ValidadorParametros`

```csharp
public static class CamposParametros
{
    public const string NombreEmpresa = "NombreEmpresa";
    public const string Direccion = "Direccion";
    public const string Telefono = "Telefono";
    public const string Ciudad = "Ciudad";
    public const string RegistroSanitario = "RegistroSanitario";
    public const string TelefonosReclamos = "TelefonosReclamos";
    public const string ObservacionPorDefecto = "ObservacionPorDefecto";
    public const string SiguienteNumeroBoleta = "SiguienteNumeroBoleta";
    public const string Logo = "Logo";
}
```
Límites: los de D12; `SiguienteNumeroBoleta` 1 a 999 999 999. `ValidadorParametros.Validar(ActualizarParametros dtoNormalizado)` devuelve la lista de errores.

| Caso | Campo | Mensaje |
|---|---|---|
| Nombre de empresa fuera de 3–100 | NombreEmpresa | "Ingrese el nombre de la empresa (3 a 100 caracteres)." |
| Texto opcional demasiado largo | (su campo) | "No puede superar {n} caracteres." |
| Número fuera de rango | SiguienteNumeroBoleta | "El número debe estar entre 1 y 999999999." |
| Número ≤ último emitido (lo valida el servicio) | SiguienteNumeroBoleta | "Debe ser mayor que el último número de boleta emitido ({ultimo})." |
| `Reemplazar` sin ruta | Logo | "Seleccione el archivo del logo." |
| Logo: no existe | Logo | "No se encontró el archivo seleccionado." |
| Logo: extensión | Logo | "El logo debe ser una imagen PNG, JPG o BMP." |
| Logo: vacío o > 1 MiB | Logo | "El logo debe pesar como máximo 1 MB." |
| Logo: firma inválida | Logo | "El archivo no es una imagen válida." |

### 5.4 Auditoría (`Application/Auditoria/FabricaAuditoria.cs`)

1. `EntidadesAuditoria`: agrega `Ganadero = "Ganadero"`, `Cliente = "Cliente"`, `Fletero = "Fletero"`, `ParametrosSistema = "ParametrosSistema"`.
2. Snapshots (records en el mismo archivo):
   ```csharp
   public sealed record GanaderoAuditoria(int Id, string Marca, string Nombre, bool Activo);
   public sealed record ClienteAuditoria(int Id, int Codigo, string Nombre, string Direccion, bool Activo);
   public sealed record FleteroAuditoria(int Id, int Orden, string Nombre, string Celular, bool Activo);
   public sealed record ParametrosAuditoria(string NombreEmpresa, string Direccion, string Telefono, string Ciudad,
       string RegistroSanitario, string TelefonosReclamos, string ObservacionPorDefecto, string? LogoPath,
       long SiguienteNumeroBoleta);
   ```
3. Sobrecargas `CrearSnapshot(Ganadero)`, `CrearSnapshot(Cliente)`, `CrearSnapshot(Fletero)`, `CrearSnapshot(ParametrosSistema)`, y amplía `NormalizarObjeto` con los cuatro tipos. **Motivo:** `Ganadero` y `Cliente` tienen la navegación `Pesajes`; serializar la entidad podría arrastrar pesajes o fallar por ciclos.

**Tests:**
- `ValidadorCatalogosTests`: un `[Theory]` por regla con casos válidos e inválidos. Como mínimo: marca `"ah"` → `"AH"` válida; `" a h "` → `"AH"`; `"ÑA"` válida; `"ABCDEF"` inválida; `""` inválida; `"A*"` inválida; nombre `"  juan   pérez "` → `"JUAN PÉREZ"`; nombre `"123"` inválido (sin letras); nombre de 101 caracteres inválido; código 0 y 1 000 000 inválidos; orden 0 y 5 inválidos; celular `"7123-4567"` válido, `"+591 71234567"` válido, `"12345"` inválido, `"abcdefg"` inválido, `"(+)-- --"` inválido (sin dígitos).
- `FabricaAuditoriaTests`: un `Ganadero` con un `Pesaje` en su colección `Pesajes` serializa **solo** `Id`, `Marca`, `Nombre`, `Activo` (el JSON no contiene `"Pesajes"`).

---

## Parte 6 — HU-F2-03, F2-04 y F2-05: servicios de catálogos

`ServicioGanaderos`, `ServicioClientes`, `ServicioFleteros` en `Application/Catalogos/`. Dependencias: `IUnidadDeTrabajoFactory`, `ISesionActual`, `TimeProvider`, `ILogger<T>`. Constructor con `ArgumentNullException` como `ServicioUsuarios`.

**Reglas comunes a todos los métodos:**
1. Primera línea: `ExigirPermiso(...)` o `ExigirSesion()` según D9, antes de cualquier I/O. Los que auditan obtienen `var sesion = _sesionActual.ExigirSesion();` justo después.
2. Normalizar → validar → `ValidacionException(errores)` si hay errores.
3. Inexistente → `EntidadNoEncontradaException("Ganadero" | "Cliente" | "Fletero", id)`.
4. Una unidad de trabajo por operación. Alta con transacción (Parte 4.2); edición y estado con un único `GuardarCambiosAsync`.
5. D11: comparar `CrearSnapshot` antes y después (igualdad de records); si son iguales, `return` sin guardar ni auditar.
6. Log `Information` de cada alta, edición y cambio de estado con el Id, la clave de negocio y el usuario de la sesión.
7. Los métodos de listado mapean a DTO y devuelven `IReadOnlyList` (`.ToList().AsReadOnly()`).

### 6.1 `ServicioGanaderos`

| Método | Reglas |
|---|---|
| `ListarAsync` / `ObtenerAsync` | Mapeo directo a `GanaderoListado`. |
| `ListarParaBusquedaAsync` | Solo activos: `new ElementoBusqueda(g.Id, g.Marca, g.Nombre, null, g.Activo)`. |
| `CrearAsync` | `marca = NormalizarMarca`, `nombre = NormalizarMayusculas`; validar; `ObtenerPorMarcaAsync(marca)`: si existe y está activo → `ValidacionException(Marca, "Ya existe un ganadero con la marca '{marca}'.")`; si existe e inactivo → `ValidacionException(Marca, "La marca '{marca}' pertenece a un ganadero desactivado ({nombre}). Reactívelo en lugar de crear uno nuevo.")`. `Activo = true`, `CreadoEn = ahoraUtc`. Alta transaccional, `Motivo = "Alta de ganadero"`. Devuelve el Id. |
| `EditarAsync` | Cargar; normalizar y validar; si la marca cambió (`string.Equals(..., StringComparison.Ordinal)` sobre valores normalizados): `TienePesajesAsync` → `ReglaNegocioException("GAN_MARCA_EN_USO", "No se puede cambiar la marca de un ganadero que ya tiene pesajes registrados.")`; otro ganadero (Id distinto) con esa marca → mismos mensajes que en el alta. Aplicar cambios; D11; auditar `Modificar`, `Motivo = "Modificación de datos"`. |
| `CambiarEstadoAsync` | Mismo estado → no-op. Auditar `Modificar` con `Motivo = "Activación"`/`"Desactivación"`. |

### 6.2 `ServicioClientes`

Igual que 6.1 con estas diferencias:
- Normaliza `Nombre` y `Direccion` con `NormalizarMayusculas` (`Direccion` null → `""`).
- Unicidad por `Codigo` (`ObtenerPorCodigoAsync`): "Ya existe un cliente con el código {codigo}." / "El código {codigo} pertenece a un cliente desactivado ({nombre}). Reactívelo en lugar de crear uno nuevo."
- Cambio de código con pesajes → `ReglaNegocioException("CLI_CODIGO_EN_USO", "No se puede cambiar el código de un cliente que ya tiene pesajes registrados.")`.
- `SugerirSiguienteCodigoAsync`: `(max ?? 0) + 1`; si supera `CodigoClienteMaximo`, devuelve el máximo (el alta fallará con el mensaje de duplicado y el usuario elegirá otro).
- `ListarParaBusquedaAsync`: `new ElementoBusqueda(c.Id, c.Codigo.ToString(CultureInfo.InvariantCulture), c.Nombre, c.Direccion, c.Activo)`.
- `Motivo` del alta: "Alta de cliente".

### 6.3 `ServicioFleteros`

- Normaliza `Nombre` con `NormalizarMayusculas`, `Celular` con `NormalizarTexto`.
- `CrearAsync`: validar; `ContarActivosAsync() >= MaximoFleterosActivos` → `ReglaNegocioException("FLE_MAXIMO", "Ya hay 4 fleteros activos. Desactive uno antes de agregar otro.")`; `ObtenerActivoPorOrdenAsync(orden)` no nulo → `ValidacionException(Orden, "El orden F{orden} ya está asignado a {nombre}.")`. Alta transaccional, `Motivo = "Alta de fletero"`.
- `EditarAsync`: si el fletero está **activo** y el orden cambió, el orden destino ocupado por **otro** activo → misma `ValidacionException(Orden, ...)`. Si está inactivo, no se comprueba ocupación (los inactivos no ocupan orden).
- `CambiarEstadoAsync(id, true)`: si su orden lo ocupa otro activo → `ReglaNegocioException("FLE_ORDEN_OCUPADO", "No se puede activar: el orden F{orden} está ocupado por {nombre}. Cambie su orden antes de activarlo.")`.
- `ListarActivosAsync`: solo activos, orden por `Orden`.

### 6.4 Tests (`ServicioGanaderosTests`, `ServicioClientesTests`, `ServicioFleterosTests`)

`BaseDatosEnMemoria` + `FakeTimeProvider` + `SesionActual` real iniciada con el rol que corresponda + `NullLogger<T>.Instance`. Una BD por test. Crea un helper de prueba `SesionesDePrueba.Iniciar(SesionActual, Rol, int usuarioId)`; el usuario debe **existir** en la BD (la auditoría tiene FK a `Usuarios`): inserta uno con `UsuarioBuilder` y usa su Id.

Para cada servicio:
- Sin sesión → `NoAutorizadoException` en **cada** método; con sesión de Operador → `NoAutorizadoException` en los métodos de gestión y **éxito** en `ListarParaBusquedaAsync`/`ListarActivosAsync`.
- Alta válida: normaliza a mayúsculas, queda activo, `CreadoEn` = hora del `FakeTimeProvider`, auditoría `Crear` con `EntidadId == Id.ToString()` y `DatosDespuesJson` con el snapshot.
- Alta duplicada (misma clave con distinta capitalización: `"ah"` contra `"AH"`) → `ValidacionException` en el campo clave; duplicada contra un **inactivo** → el mensaje de reactivar.
- Alta inválida → `ValidacionException` con el campo correcto.
- Edición de nombre → auditoría `Modificar` con antes y después.
- Edición sin cambios → **ninguna** auditoría nueva.
- Cambio de clave sin pesajes → permitido; con pesajes (inserta un `Pesaje` con `PesajeBuilder`) → `ReglaNegocioException` con el código correspondiente.
- Desactivar y reactivar → dos auditorías con sus motivos; mismo estado → no-op.
- `ListarAsync(false)` excluye inactivos; `ListarParaBusquedaAsync` excluye inactivos.
- Clientes: `SugerirSiguienteCodigoAsync` devuelve 1 con BD vacía y `max+1` contando inactivos.
- Fleteros: quinto activo → `FLE_MAXIMO`; orden ocupado → `ValidacionException(Orden)`; dos inactivos con el mismo orden conviven; reactivar con orden ocupado → `FLE_ORDEN_OCUPADO`; `ListarActivosAsync` ordenado por orden.

---

## Parte 7 — HU-F2-01: servicio de parámetros del sistema

`Application/Parametros/ServicioParametros.cs`. Dependencias: `IUnidadDeTrabajoFactory`, `ISesionActual`, `IAlmacenLogo`, `TimeProvider`, `ILogger<ServicioParametros>`.

### `ObtenerAsync`
`ExigirSesion()`; carga parámetros y `ObtenerUltimoNumeroBoletaAsync`; `RutaLogoCompleta = _almacenLogo.ObtenerRutaCompleta(p.LogoPath)`.

### `ActualizarAsync` (en este orden)
1. `ExigirPermiso(GestionarParametros)`; `sesion = ExigirSesion()`.
2. Normaliza todos los textos con `ValidadorCatalogos.NormalizarTexto` (D2; opcionales `null` → `""`). Valida con `ValidadorParametros`; errores → `ValidacionException`. Si `AccionLogo == Reemplazar` y `RutaLogoOrigen` vacía → error de campo `Logo`.
3. Abre la unidad, carga la entidad (con tracking), `antes = CrearSnapshot(p)`, `ultimo = ObtenerUltimoNumeroBoletaAsync()`.
4. Si `dto.SiguienteNumeroBoleta != p.SiguienteNumeroBoleta` y `ultimo is not null` y `dto.SiguienteNumeroBoleta <= ultimo` → `ValidacionException(SiguienteNumeroBoleta, "Debe ser mayor que el último número de boleta emitido ({ultimo}).")`.
5. Asigna los textos. **Asigna `SiguienteNumeroBoleta` solo si cambió** (así EF no incluye la columna en el `UPDATE` cuando no cambió; ver R6).
6. Logo:
   - `Reemplazar`: `nuevoArchivo = await _almacenLogo.GuardarAsync(ruta)` (sus `ValidacionException` se propagan tal cual) y `p.LogoPath = nuevoArchivo`.
   - `Quitar`: `p.LogoPath = null`.
   - `SinCambio`: nada.
7. `despues = CrearSnapshot(p)`. Si `antes == despues` → no guardes ni audites; si se copió un archivo nuevo (no puede ocurrir con Reemplazar, pero protégete), elimínalo; `return`.
8. `p.ModificadoEn = ahoraUtc`. Auditoría `CambioParametros`, `EntidadId = "1"`, antes y después; `Motivo` = `"Cambio de numeración de boletas: {antes} → {despues}"` si cambió el número, si no `"Actualización de parámetros"`.
9. `try { await uow.GuardarCambiosAsync(); } catch { _almacenLogo.Eliminar(nuevoArchivo); throw; }` — si falla el guardado, el archivo copiado no queda huérfano.
10. Después del guardado exitoso, si el `LogoPath` anterior no es null y es distinto del actual → `_almacenLogo.Eliminar(anterior)`.
11. Log `Information` con el usuario; si cambió la numeración, log `Warning` con los dos valores.

### Tests (`ServicioParametrosTests`)
Usa un `AlmacenLogoFalso : IAlmacenLogo` escrito a mano que registra llamadas (y un test de integración con el `AlmacenLogoArchivos` real en directorio temporal).
- Sin sesión → `ObtenerAsync` lanza; con Operador → `ObtenerAsync` funciona y `ActualizarAsync` lanza `NoAutorizadoException`.
- Actualización válida: persiste los textos normalizados (sin pasar a mayúsculas), `ModificadoEn` cambia, auditoría `CambioParametros` con antes/después.
- Sin cambios → no audita.
- Nombre de empresa vacío → `ValidacionException(NombreEmpresa)`.
- Número de boleta: con un pesaje 500 existente, 500 → error y 501 → ok con el motivo de numeración; sin pesajes, bajar de 10 a 1 → ok.
- Número sin cambios y un pesaje existente mayor (dato inconsistente heredado) → **no** falla (la regla solo aplica si el valor cambia).
- Logo `Reemplazar` → `LogoPath` = nombre devuelto por el almacén; el anterior se elimina **después** de guardar; `Quitar` → null y se elimina el anterior; si `GuardarCambiosAsync` falla (fuerza el fallo con una CK: imposible desde el DTO, así que usa un `IUnidadDeTrabajoFactory` decorador de prueba que lance en `GuardarCambiosAsync`) → el archivo nuevo se elimina y el anterior **no**.

---

## Parte 8 — HU-F2-06: búsqueda y control reutilizable de autocompletado

### 8.1 `TextoBusqueda` y `MotorBusqueda` (Application, puro)

`TextoBusqueda.Plegar(texto)`: `null`/espacios → `""`; `Normalize(NormalizationForm.FormD)`, elimina los caracteres de categoría `UnicodeCategory.NonSpacingMark`, `Normalize(FormC)`, `ToUpperInvariant()`, Trim + colapso de espacios. Así "Peña", "PENA" y "peña" pliegan igual a `"PENA"` y "José" a `"JOSE"`.

`MotorBusqueda.Buscar(elementos, texto, maximo)`:
1. `t = Plegar(texto)`; si `t == ""` → lista vacía.
2. Cada elemento recibe un **rango** (menor es mejor); los que no coinciden se descartan:
   - 0: `CodigoPlegado == t`
   - 1: `CodigoPlegado.StartsWith(t, Ordinal)`
   - 2: `DescripcionPlegada.StartsWith(t, Ordinal)`
   - 3: **todas** las palabras de `t` (separadas por espacio) son prefijo de alguna palabra de `DescripcionPlegada`
   - 4: `DescripcionPlegada.Contains(t, Ordinal)`
3. Orden: rango, luego `DescripcionPlegada`, luego `CodigoPlegado` (ordinal). `Take(maximo)`.

`MotorBusqueda.ResolverExacto(elementos, texto)`: devuelve el único elemento con rango 0 si existe; si no, el único resultado de `Buscar` si hay exactamente uno; si no, `null`. Lo usa el control para Enter/Tab sin navegar la lista.

**Tests (`MotorBusquedaTests`):** acentos y eñes ("pena" encuentra "PEÑA"); código exacto antes que prefijo ("11" con códigos 11, 110, 111 → 11 primero); prefijo de código antes que nombre; "juan per" encuentra "JUAN PEREZ" y "PEDRO JUAN PERALTA" (rango 3) y no "JUANA LOPEZ"; "ANA" encuentra "JUANA" por rango 4 y "ANA MARIA" por rango 2, en ese orden (2 antes que 4); texto vacío → vacío; máximo respetado; `ResolverExacto` con código exacto, con resultado único y con ambigüedad (null).

### 8.2 `WinForms/Controles/ControlBusquedaCatalogo.cs : UserControl`

**API pública:**
```csharp
public event EventHandler<ElementoBusqueda>? ElementoSeleccionado;
public event EventHandler? SeleccionBorrada;
public event EventHandler<string>? AltaRapidaSolicitada;   // la usará F4-07
public ElementoBusqueda? Seleccion { get; }
public bool PermitirAltaRapida { get; set; }               // por defecto false
public string Placeholder { get; set; }                     // → TextBox.PlaceholderText
public int MaximoResultados { get; set; } = MotorBusqueda.MaximoPorDefecto;
public void EstablecerElementos(IReadOnlyList<ElementoBusqueda> elementos); // conserva la selección si su Id sigue presente; si no, la borra
public void Seleccionar(int id);                             // programática; dispara ElementoSeleccionado
public void Limpiar();                                       // texto vacío, sin selección, lista oculta
```

**Estructura:**
- Contiene un único `TextBox txtTexto` (`Dock = Fill`, `CharacterCasing = Normal`). La altura del control sigue a la del `TextBox`.
- La lista de resultados es un `ListBox lstResultados` (`TabStop = false`, `IntegralHeight = true`, `Visible = false`) que **no** es hijo del control: se agrega a `FindForm().Controls` la primera vez que se muestra y se lleva al frente (`BringToFront()`). **Motivo:** un hijo del `UserControl` quedaría recortado por los límites del control o de su `TableLayoutPanel`.
- Posición: debajo del `TextBox`, `form.PointToClient(txtTexto.PointToScreen(new Point(0, txtTexto.Height)))`; ancho = `Math.Max(txtTexto.Width, 350)`; alto = `ItemHeight * Math.Min(resultados, 8) + 4`. Si se sale del borde inferior del `ClientSize` del formulario, se muestra **encima** del `TextBox`. Se recalcula cada vez que se muestra.
- `Dispose(bool)`: quita `lstResultados` de su padre y lo desecha.
- Filtrado en `TextChanged`, sin temporizador (búsqueda en memoria). Bandera `_cambiandoTextoPorCodigo` para que asignar `Text` desde el propio control no dispare búsqueda ni borre la selección.
- Al seleccionar: `Seleccion = e`, `txtTexto.Text = e.TextoMostrado` (con la bandera), oculta la lista, `txtTexto.SelectAll()`, dispara `ElementoSeleccionado`.
- Si el usuario modifica el texto teniendo `Seleccion` no nula → `Seleccion = null` y dispara `SeleccionBorrada` antes de buscar.

**Teclado (crítico):** un `TextBox` de una línea **no recibe** `KeyDown` de Enter ni de Escape cuando el formulario tiene `AcceptButton`/`CancelButton`: `ProcessDialogKey` los consume antes. Por eso:
- En `txtTexto.PreviewKeyDown`: `e.IsInputKey = true` para **Enter** cuando la lista está visible **o** (`Seleccion == null` y hay texto); para **Escape** solo cuando la lista está visible. En cualquier otro caso no los marques, para que Enter siga llegando al `AcceptButton` (o al flujo por teclado de F4-06) cuando ya hay una selección.
- En `txtTexto.KeyDown` (delegando en `internal bool ProcesarTecla(Keys tecla)`, que devuelve si la manejó; si la manejó, `e.Handled = e.SuppressKeyPress = true`):
  - **Down/Up**: mueven el índice en la lista (si está oculta y hay texto, la muestran). **PageDown/PageUp**: de 8 en 8.
  - **Enter**: lista visible con índice ≥ 0 → selecciona ese elemento. Si no: `ResolverExacto` → selecciona; si no hay resultado y `PermitirAltaRapida` → dispara `AltaRapidaSolicitada(texto)`; si hay varios, muestra la lista con el primero marcado.
  - **Escape**: oculta la lista.
- **Tab/Leave:** en `txtTexto.Leave`, si el puntero está sobre la lista (`lstResultados.Visible && lstResultados.Bounds.Contains(form.PointToClient(Cursor.Position))`) no hagas nada (el clic en la lista llega después). Si no: si `Seleccion == null` y hay texto, `ResolverExacto` y selecciona si hay resultado; luego oculta la lista.
- **Clic** en un elemento de la lista (`lstResultados.MouseClick` con `IndexFromPoint`) → selecciona y devuelve el foco a `txtTexto`.
- También oculta la lista en `form.Deactivate`.

**Ganchos internos para pruebas:** `internal TextBox CajaTexto`, `internal ListBox ListaResultados`, `internal bool ListaVisible => lstResultados.Visible`, `internal bool ProcesarTecla(Keys tecla)`, `internal void SimularSalida()` (ejecuta la lógica de `Leave` sin el chequeo del puntero).

**Tests (`ControlBusquedaCatalogoTests`, STA, el control dentro de un `Form` mostrado):**
- Escribir "11" muestra la lista con los elementos esperados en el orden del motor.
- Down + Enter selecciona el primero, dispara `ElementoSeleccionado` una vez y oculta la lista.
- Escribir el código exacto "110" + Enter selecciona sin navegar.
- Escape oculta la lista sin seleccionar.
- Editar el texto tras seleccionar → `Seleccion` null y `SeleccionBorrada` disparado una vez.
- `Seleccionar(id)` asigna texto y selección; `EstablecerElementos` sin ese Id borra la selección.
- `SimularSalida` con texto ambiguo no selecciona; con código exacto selecciona.
- Enter sin resultados y `PermitirAltaRapida = true` dispara `AltaRapidaSolicitada` con el texto.
- Al desechar el control, la lista ya no está en `form.Controls`.

---

## Parte 9 — UI de catálogos (HU-F2-03, F2-04, F2-05)

Carpeta `WinForms/Formularios/Catalogos/`. Todo escrito a mano en `InitializeComponent()` con `TableLayoutPanel`, como en F1. **Toma `FrmUsuarios` y `FrmUsuarioEdicion` como plantilla** (incluida la regla de reentrada de la Parte 0.10).

### 9.1 Formularios de lista: `FrmGanaderos`, `FrmClientes`, `FrmFleteros`

- Inyectan su servicio e `IFormFactory`. `Text` = "Ganaderos" / "Clientes" / "Fleteros". Tamaño y estilo como `FrmUsuarios`.
- Fila superior: etiqueta "Buscar:" + `ControlBusquedaCatalogo busqueda` (no en Fleteros: son como mucho unas decenas) + `chkMostrarInactivos` ("Mostrar inactivos").
- `DataGridView` de solo lectura, fila completa, sin agregar filas, `AutoGenerateColumns = false`, `RowHeadersVisible = false`. Columnas:
  - Ganaderos: Marca (80), Nombre (Fill), Activo (Sí/No, 70).
  - Clientes: Código (80), Nombre (Fill), Dirección (250), Activo (70).
  - Fleteros: Orden ("F1".."F4"; inactivos muestran "—"), Nombre (Fill), Celular (140), Activo (70).
- Botones: `btnNuevo`, `btnEditar` (también doble clic y Enter en la grilla), `btnActivarDesactivar` (texto según la fila), `btnCerrar` (`CancelButton`).
- `CargarAsync(int? idASeleccionar = null)` **sin** `EjecutarAsync` propio: llama a `ListarAsync(chkMostrarInactivos.Checked)`, enlaza la grilla, conserva la selección por Id y alimenta `busqueda.EstablecerElementos(...)` construyendo los `ElementoBusqueda` desde la lista recién cargada (sin otra llamada al servicio).
- `Shown` → `EjecutarAsync(() => CargarAsync())`. `chkMostrarInactivos.CheckedChanged` → `EjecutarAsync(() => CargarAsync(), chkMostrarInactivos)`.
- `busqueda.ElementoSeleccionado` → selecciona la fila con ese Id (si está filtrada por inactivos, no hace nada).
- Nuevo → abre el diálogo de edición en alta; si devuelve OK → `CargarAsync(frm.IdGuardado)`.
  - Clientes: antes de abrir el diálogo, `await _servicio.SugerirSiguienteCodigoAsync()` y `frm.ConfigurarAlta(codigoSugerido)`.
  - Fleteros: `frm.ConfigurarAlta(primerOrdenLibre)` calculado con las filas **activas** de la lista cargada; si no hay orden libre, no abras el diálogo y muestra "Ya hay 4 fleteros activos. Desactive uno antes de agregar otro." (el servicio también lo valida).
- Editar → `ObtenerAsync(id)` → `ConfigurarEdicion(dto)` → si OK → `CargarAsync(id)`.
- Activar/Desactivar → confirmación Sí/No con la clave y el nombre ("¿Desactivar al ganadero 'AH — JUAN PÉREZ'?"); luego `CambiarEstadoAsync` y `CargarAsync(id)` dentro de **un** `EjecutarAsync`.
- Ganchos internos como en `FrmUsuarios`: `GrillaParaTest`, `CargarParaTestAsync()`, `MostrarInactivosParaTest`, `SeleccionarFilaParaTest(int)`, `CambiarEstadoSeleccionadoParaTestAsync()`.

### 9.2 Diálogos de edición: `FrmGanaderoEdicion`, `FrmClienteEdicion`, `FrmFleteroEdicion`

- `FixedDialog`, sin maximizar/minimizar, `ShowInTaskbar = false`, `CenterParent`, `AcceptButton = btnGuardar`, `CancelButton = btnCancelar`, `ErrorProvider` con `BlinkStyle = NeverBlink`.
- `ConfigurarAlta(...)` / `ConfigurarEdicion(dto)`; título "Nuevo …" / "Editar …". Propiedad `int? IdGuardado`.
- Controles (`internal` para los tests):
  - Ganadero: `txtMarca` (`MaxLength = 5`, `CharacterCasing = Upper`), `txtNombre` (`MaxLength = 100`, `Upper`).
  - Cliente: `numCodigo` (`NumericUpDown`, `Minimum = 1`, `Maximum = 999999`), `txtNombre` (100, `Upper`), `txtDireccion` (150, `Upper`).
  - Fletero: `cmbOrden` (`DropDownList` con "F1".."F4" asociados a 1..4), `txtNombre` (100, `Upper`), `txtCelular` (20).
- `btnGuardar_Click` → `EjecutarAsync(async () => { try { ...crear/editar...; IdGuardado = ...; DialogResult = OK; } catch (Exception ex) when (MostrarErroresValidacion(ex, _errorProvider, _controlesPorCampo)) { } }, btnGuardar)`, exactamente como `FrmUsuarioEdicion`. El mapa campo → control usa `CamposCatalogo`.
- En edición, si el servicio responde `GAN_MARCA_EN_USO`/`CLI_CODIGO_EN_USO`, lo muestra `ManejadorErrores` como cualquier `ReglaNegocioException`; no dupliques la regla en la UI.

### 9.3 Tests de UI (STA, servicios falsos)
- `FrmGanaderosTests`: tras cargar, desactivar la fila seleccionada recarga la grilla y la columna Activo pasa a "No" (con inactivos visibles) — mismo patrón que `FrmUsuariosTests`.
- `FrmClientesTests`: la grilla muestra código, nombre y dirección; seleccionar en `busqueda` un elemento selecciona la fila de ese Id.
- `FrmFleterosTests`: la columna Orden muestra "F2" para un activo de orden 2 y "—" para un inactivo.
- `FrmGanaderoEdicionTests`: con un servicio falso que lanza `ValidacionException(Marca, ...)`, guardar deja el error en `txtMarca` y `DialogResult == None`. Expón `internal Task GuardarParaTestAsync()`.
- `FrmClienteEdicionTests`: `ConfigurarAlta(215)` deja `numCodigo.Value == 215`.

---

## Parte 10 — UI de parámetros (HU-F2-01) y shell

### 10.1 `WinForms/Formularios/Administracion/FrmParametros.cs`

- Inyecta `IServicioParametros`. `FixedDialog`, `CenterParent`, título "Parámetros del sistema".
- Campos (`TextBox`, sin `CharacterCasing`): Nombre de la empresa (100), Dirección (150), Teléfono (50), Ciudad (100), Registro sanitario (100), Teléfonos de reclamos (100), Observación por defecto (250, `Multiline`, 2 líneas).
- `numSiguienteBoleta` (`NumericUpDown`, `Minimum = 1`, `Maximum = 999999999`, `ThousandsSeparator = false`) y una etiqueta "Último número emitido: {n}" o "Aún no hay boletas emitidas".
- Logo: `PictureBox picLogo` (`SizeMode = Zoom`, 160×80, borde), `btnCambiarLogo` (`OpenFileDialog`, filtro `"Imágenes (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp"`), `btnQuitarLogo`. Estado local `AccionLogo _accionLogo` y `string? _rutaLogoNueva`.
- **Carga de imágenes sin bloquear el archivo:** nunca `Image.FromFile` (deja el archivo abierto y luego no se puede borrar el logo anterior). Usa:
  ```csharp
  using var fs = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
  using var original = Image.FromStream(fs);
  var copia = new Bitmap(original);
  ```
  Antes de asignar una imagen nueva a `picLogo.Image`, desecha la anterior; desecha también la actual en `Dispose(bool)`. Si la carga falla (`ArgumentException`, `OutOfMemoryException`, `IOException`), muestra el texto "No se pudo cargar la imagen." en lugar de la vista previa, sin lanzar.
- `Shown` → `EjecutarAsync(CargarAsync)`: rellena campos y vista previa desde `RutaLogoCompleta`.
- `btnGuardar` → `EjecutarAsync`:
  1. Si `numSiguienteBoleta.Value` difiere del cargado, confirma: "Cambiará el siguiente número de boleta de {a} a {b}. Esta acción afecta la numeración oficial. ¿Continuar?" (Sí/No). Si es No, no hagas nada.
  2. `ActualizarAsync(...)`; errores de validación al `ErrorProvider` con el mapa `CamposParametros` → control (`Logo` → `picLogo`).
  3. Éxito → "Parámetros guardados." y `DialogResult = OK`.
- Ganchos internos: `internal Task CargarParaTestAsync()`, `internal Task GuardarSinConfirmarParaTestAsync()`, acceso a los controles.

### 10.2 `FrmPrincipal`

1. Inyecta además `IServicioParametros`. Actualiza **todos** los `new FrmPrincipal(...)` de `FrmPrincipalTests` con un servicio falso.
2. En `ConfigurarMenu`: `FaseFutura = null` para Ganaderos, Clientes, Fleteros y Parámetros del sistema, con `Click` que abre su formulario igual que `AbrirUsuarios` (`Ejecutar` + `ExigirPermiso` + `using` + `ShowDialog(this)`).
3. `StatusStrip`: nuevo `lblStatusEmpresa` (primero de la izquierda). Se rellena en `Shown` con `EjecutarAsync(ActualizarEmpresaAsync)` y de nuevo tras cerrar `FrmParametros` con OK. **Esa recarga no puede ir dentro del `Ejecutar` que abrió el diálogo** (regla 0.10): `AbrirParametros` guarda el `DialogResult` en una variable, sale del `Ejecutar` y luego llama a `await EjecutarAsync(ActualizarEmpresaAsync)` (el manejador del menú pasa a `async`).
4. Un fallo al leer el nombre de la empresa no debe impedir usar el shell: `ActualizarEmpresaAsync` atrapa la excepción, la registra con `ILogger` o Serilog estático como el resto del proyecto y deja la etiqueta vacía.

### 10.3 Registro y DI

- `AddInfrastructure`: `IAlmacenLogo` → `AlmacenLogoArchivos` (Singleton).
- `AddApplication`: los cuatro servicios Transient.
- `ContenedorDITests`: resuelve los cuatro servicios e `IAlmacenLogo`, y crea con la fábrica (STA) `FrmGanaderos`, `FrmGanaderoEdicion`, `FrmClientes`, `FrmClienteEdicion`, `FrmFleteros`, `FrmFleteroEdicion` y `FrmParametros`.
- `FrmPrincipalTests`: con sesión de Supervisor, Catálogos › Ganaderos está disponible y **habilitado**; Administración › Parámetros no está disponible. Con Administrador, Parámetros está disponible y habilitado.
- `AutorizadorMenuTests` no debería cambiar (sus ítems se construyen en el propio test); verifícalo.

### 10.4 Tests de `FrmParametros` (STA, servicio falso)
- `CargarParaTestAsync` rellena los campos y la etiqueta de último número.
- Con un servicio falso que lanza `ValidacionException(NombreEmpresa, ...)`, guardar deja el error en el `TextBox` correspondiente.

---

## Parte 11 — Cierre

1. **Arquitectura:** `ArquitecturaTests` en verde. Agrega un caso: Application no referencia `System.Drawing`.
2. **`Agents.md`:**
   - §2: F2 ✅ Completado, 6/6, fecha; F3 "⏳ Siguiente en turno".
   - §3: marca las 6 HUs de F2.
   - §4: agrega mayúsculas en catálogos (y su motivo: NOCASE solo ASCII), `ITransaccion` para altas con auditoría por Id, búsqueda en memoria plegando acentos, `ControlBusquedaCatalogo` y el manejo de Enter/Escape con `PreviewKeyDown`, logo por nombre de archivo en `LogoDir`, la regla de reentrada de `FormBase` (0.10).
   - "Notas hacia fases futuras": agrega R6, R9, R10, R11 y R12 de la Parte 12.
   - §5: entrada "Fase F2" con build, conteo de tests antes → después, la migración `F2_Catalogos`, decisiones D1–D17 y desviaciones.
3. **`README.md`:** corrige "mínimo 4 caracteres" → 6 (la política real de F1) y agrega una sección breve "Catálogos y parámetros" (dónde se guarda el logo, que los catálogos se guardan en mayúsculas).
4. **Verificaciones finales:**
   ```powershell
   .\build.ps1
   ```
   ```bash
   dotnet ef migrations has-pending-model-changes -p src/PesajeFaeneo.Infrastructure -s src/PesajeFaeneo.WinForms
   ```
5. **Prueba de arranque:** borra `C:\ProgramData\PesajeFaeneo\pesaje.db*`, lanza `src\PesajeFaeneo.WinForms\bin\Release\net8.0-windows\PesajeFaeneo.WinForms.exe`, verifica que sigue vivo a los 5 s, que `__EFMigrationsHistory` contiene `F2_Catalogos` y que el log tiene "Aplicación iniciada" sin errores. Ciérralo. No automatices clics. Prueba también sobre una copia de una BD creada **con F1** (genera una con la build de F1 en un directorio temporal o reutiliza la de `MigracionF2Tests`) para confirmar que migra.
6. **Informe final** (tu única respuesta):
   - Tabla HU → archivos creados o modificados → tests añadidos.
   - Tests antes → después, warnings, resultado de `has-pending-model-changes` y el resumen del SQL revisado en la Parte 2.
   - Desviaciones del plan y su motivo.
   - **Checklist de prueba manual:**
     1. Como admin, Parámetros: cambiar nombre de empresa → aparece en la barra de estado; cargar un PNG → vista previa; cambiar el logo otra vez → en `C:\ProgramData\PesajeFaeneo\logo` queda solo el nuevo; quitarlo.
     2. Intentar cargar un `.txt` renombrado a `.png` → mensaje "El archivo no es una imagen válida.".
     3. Cambiar el siguiente número de boleta pide confirmación y queda auditado.
     4. Ganaderos: crear "ah / juan pérez" → se guarda "AH / JUAN PÉREZ"; crear otra vez "AH" → error; desactivarlo y volver a crear "AH" → mensaje de reactivar.
     5. Buscar "perez" y "pérez" en el buscador de ganaderos → ambos encuentran a JUAN PÉREZ; Enter lo selecciona en la grilla; Escape cierra la lista sin cerrar el formulario.
     6. Clientes: el alta propone el siguiente código; código duplicado rechazado.
     7. Fleteros: cargar F1..F4; el quinto se rechaza; desactivar F2 y crear uno nuevo en F2.
     8. Como Supervisor: ve Catálogos pero no Parámetros. Como Operador: no ve Catálogos.
     9. La tabla `Auditorias` tiene `Crear`/`Modificar`/`CambioParametros` con `EntidadId` numérico para catálogos y `"1"` para parámetros.

---

## Parte 12 — Revisión del plan: huecos detectados y riesgos

Esta sección es el resultado de revisar el plan contra el código real de F0/F1. Cada punto indica **qué podía salir mal** y **qué regla del plan lo evita**. Los marcados como *abierto* no se resuelven en F2 y deben pasar a "Notas hacia fases futuras".

| # | Hueco o riesgo | Consecuencia si se ignora | Cubierto por |
|---|---|---|---|
| R1 | La collation `NOCASE` de SQLite solo pliega A–Z ASCII. | "PEÑA" y "peña", o "JOSÉ" y "josé", serían distintos para el índice único y para la búsqueda; el `Agents.md` actual da a entender que NOCASE basta. | D1 (mayúsculas al guardar) + D15 (búsqueda en memoria con plegado de acentos). |
| R2 | `HasMaxLength` no se aplica en SQLite. | Una marca de 20 caracteres entraría en la BD si un camino (p. ej. el importador de F8-04) saltara el validador. | CK nuevas de la Parte 2 y validador de la Parte 5. |
| R3 | Reconstruir `Ganaderos`/`Clientes` en SQLite para añadir CK, con `Pesajes` apuntándoles por FK y `foreign_keys = ON` en cada conexión. | Pérdida de la collation, del índice único o fallo de FK al migrar una BD real. | Revisión del SQL (Parte 2.4) + `MigracionF2Tests` con datos y `PRAGMA foreign_key_check`. |
| R4 | El Id de un registro nuevo no existe antes del INSERT; F1 resolvió esto usando el nombre de usuario como `EntidadId`. | Con catálogos, la marca/código/orden pueden cambiar y el visor de F6-05 perdería el historial de un registro. | D10 + `ITransaccion` (Parte 4.2), con test de reversión. |
| R5 | Serializar entidades con navegaciones (`Ganadero.Pesajes`) en la auditoría. | JSON enorme, ciclos o datos de pesajes filtrados a la auditoría del catálogo. | Snapshots + `NormalizarObjeto` (Parte 5.4) con test. |
| R6 | *Parcialmente abierto.* `SiguienteNumeroBoleta` lo incrementará F4-02 con un `UPDATE ... RETURNING` atómico; un `FrmParametros` abierto con datos viejos podría pisar ese contador. | Números de boleta repetidos (UNIQUE lo impediría, pero con error en plena operación). | En F2: la columna solo se asigna si el usuario la cambió (EF no la incluye en el `UPDATE` si no cambió) y la regla "mayor que el último emitido" se evalúa dentro de la misma unidad. **Abierto para F4-02/F6:** al cambiar la numeración, comprobar en la misma transacción que el valor actual en BD no cambió desde que se cargó (o usar el token `Version` de F6). |
| R7 | Regla de reentrada de `FormBase`: una recarga anidada en otro `EjecutarAsync` se descarta en silencio (bug real corregido en `FrmUsuarios`). | Grillas que no se refrescan tras guardar; checkbox "Mostrar inactivos" que no coincide con la grilla. | Regla 0.10, Partes 9.1 y 10.2.3, tests de recarga en cada lista. |
| R8 | `Enter`/`Escape` no llegan al `KeyDown` de un `TextBox` cuando el formulario tiene `AcceptButton`/`CancelButton`. | El autocompletado cerraría el diálogo o dispararía "Guardar" en vez de elegir un resultado. | `PreviewKeyDown` con `IsInputKey` condicional (Parte 8.2) + tests de `ProcesarTecla`. Los tests llaman a `ProcesarTecla` directamente, así que **no** prueban el enrutamiento real de Windows: está en el checklist manual (punto 5). |
| R9 | *Abierto.* F4-07 necesita altas por usuarios con `AltaRapidaCatalogos` (Operador), que no tienen `GestionarCatalogos`. | Si F4 reutiliza `CrearAsync`, el operador recibirá `NoAutorizadoException`. | F4-07 debe agregar `CrearRapidoAsync` (permiso `AltaRapidaCatalogos`, mismas validaciones, `Motivo = "Alta rápida desde pesaje"`) en vez de relajar `CrearAsync`. El evento `AltaRapidaSolicitada` del control ya existe. |
| R10 | *Abierto.* F5 imprimirá desde el logo y F8 hace backup/restauración. | Un backup solo de `pesaje.db` pierde el logo; restaurar en otra PC deja `LogoPath` apuntando a un archivo que no existe. | En F2 el logo se guarda por nombre de archivo (D14) y `ObtenerRutaCompleta` devuelve null si falta. **F8-01/03** deben incluir `LogoDir` en el backup y **F5** debe tolerar logo ausente o ilegible. |
| R11 | *Abierto.* `ConfiguracionInicialCompleta` sigue en `false` tras guardar parámetros. | Si F9-04 muestra el asistente mientras esté en false, lo mostraría aunque el admin ya configuró todo. | F9-04 decide si marcarla o si detecta el nombre de empresa distinto de "NOMBRE DE EMPRESA". Anotarlo. |
| R12 | *Abierto.* Sin token de concurrencia en catálogos. | En una sola PC con instancia única el riesgo es bajo (dos diálogos del mismo registro no pueden abrirse a la vez porque son modales). | D16. Si alguna vez se permite más de una PC contra la misma BD, agregar `Version` también a catálogos. |
| R13 | Archivo de logo huérfano o borrado prematuro. | Si el guardado falla después de copiar, queda un archivo suelto; si se borrara el anterior antes de confirmar y el guardado fallara, la boleta quedaría sin logo. | Orden estricto de la Parte 7 (pasos 6, 9 y 10) con tests. |
| R14 | `Image.FromFile` bloquea el archivo hasta que se desecha la imagen. | `Eliminar` del logo anterior falla en silencio y se acumulan archivos. | Carga con `FileStream` + copia `Bitmap` (Parte 10.1). |
| R15 | Validar con valores sin normalizar. | "  ah " pasaría como marca de 5 caracteres con espacios, o un nombre de 101 caracteres con espacios dobles sería rechazado aunque se guardaría con 99. | Los validadores reciben valores ya normalizados (Parte 5.2). |
| R16 | Auditoría con FK a `Usuarios`: los tests de servicios con un `UsuarioSesion` inventado fallan con `BD_FK`. | Tests en rojo por una causa ajena a lo que prueban. | Parte 6.4: la sesión de prueba usa un usuario insertado en la BD. |
| R17 | Tests existentes con datos que violen las CK nuevas. | Compuerta en rojo. | Regla 0.4: corregir datos de prueba, nunca la CK. Hoy los builders usan marca "AH" y código 110, que las cumplen. |
| R18 | Árbol de trabajo no limpio al empezar (corrección de `FrmUsuarios` sin commit, además con finales LF). | El diff de F2 mezclaría una corrección de F1. | Regla 0.3. |
| R19 | La lista de resultados como hijo del `Form` puede quedar debajo de otros controles o fuera de lugar si cambia el layout. | Lista invisible o mal posicionada. | `BringToFront()` y reposición en cada `Mostrar` (Parte 8.2). Verificación visual en el checklist (punto 5). En F4-04 revisar con la pantalla de pesaje real. |
| R20 | Mayúsculas por `CharacterCasing = Upper` en la UI y `ToUpperInvariant` en el servicio. | Si solo lo hiciera la UI, el importador o F4-07 guardarían minúsculas. | El servicio es la autoridad (D1); la UI solo ayuda visualmente. |

**Preguntas para el usuario (no bloquean; el plan ya asume una respuesta):**
1. ¿Los catálogos deben guardarse en mayúsculas (D1)? Si prefieren mayúsculas y minúsculas, hay que agregar una columna de búsqueda plegada o comparar siempre en memoria, porque NOCASE no sirve para ñ ni acentos.
2. ¿El formato de marca (1–5 caracteres, `& . / -`) cubre las marcas del sistema legado? Conviene revisarlo con una muestra de los datos que se importarán en F8-04.
3. ¿Es correcto que el nombre de ganaderos y clientes pueda repetirse (D4)?
4. ¿Celular de fletero obligatorio (D8)? El esquema actual lo exige (`IsRequired`).
5. ¿Límite de 1 MB para el logo y formatos PNG/JPG/BMP (D14)?
