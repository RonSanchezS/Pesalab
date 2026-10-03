namespace PesajeFaeneo.Infrastructure.Instalacion;

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PesajeFaeneo.Application.Abstracciones;
using PesajeFaeneo.Application.Reportes;
using PesajeFaeneo.Application.Reportes.Dtos;
using PesajeFaeneo.Application.Utilitarios.Backup;
using PesajeFaeneo.Application.Utilitarios.Backup.Dtos;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Infrastructure.Common;
using PesajeFaeneo.Infrastructure.Persistencia;

public sealed record PasoAutoprueba(string Nombre, bool Exito, string Mensaje, long Milisegundos);

public sealed record ResultadoAutoprueba(
    string Version,
    string Compilacion,
    string Runtime,
    bool Proceso64Bits,
    DateTime FechaUtc,
    bool Exito,
    IReadOnlyList<PasoAutoprueba> Pasos);

/// <summary>
/// Corre sin sesión, la invoca Program con --autoprueba; nunca toca ProgramData (F9-01, D5).
/// </summary>
public sealed class AutopruebaInstalacion
{
    private readonly AppPaths _appPaths;
    private readonly IDatabaseInitializer _inicializador;
    private readonly IDataSeeder _sembrador;
    private readonly IDbContextFactory<PesajeDbContext> _contextFactory;
    private readonly IGeneradorPdfReportes _pdf;
    private readonly IGeneradorExcelReportes _excel;
    private readonly IMotorBackup _motorBackup;
    private readonly TimeProvider _tiempo;
    private readonly ILogger<AutopruebaInstalacion> _logger;

    public AutopruebaInstalacion(
        AppPaths appPaths,
        IDatabaseInitializer inicializador,
        IDataSeeder sembrador,
        IDbContextFactory<PesajeDbContext> contextFactory,
        IGeneradorPdfReportes pdf,
        IGeneradorExcelReportes excel,
        IMotorBackup motorBackup,
        TimeProvider tiempo,
        ILogger<AutopruebaInstalacion> logger)
    {
        _appPaths = appPaths ?? throw new ArgumentNullException(nameof(appPaths));
        _inicializador = inicializador ?? throw new ArgumentNullException(nameof(inicializador));
        _sembrador = sembrador ?? throw new ArgumentNullException(nameof(sembrador));
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _pdf = pdf ?? throw new ArgumentNullException(nameof(pdf));
        _excel = excel ?? throw new ArgumentNullException(nameof(excel));
        _motorBackup = motorBackup ?? throw new ArgumentNullException(nameof(motorBackup));
        _tiempo = tiempo ?? throw new ArgumentNullException(nameof(tiempo));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ResultadoAutoprueba> EjecutarAsync(CancellationToken ct = default)
    {
        var pasos = new List<PasoAutoprueba>();

        // 1. BaseDeDatos
        pasos.Add(await EjecutarPasoAsync("BaseDeDatos", async () =>
        {
            await _inicializador.InicializarAsync(ct).ConfigureAwait(false);
            await _sembrador.SembrarAsync(ct).ConfigureAwait(false);

            await using var context = await _contextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
            var usuarios = await context.Usuarios.CountAsync(ct).ConfigureAwait(false);
            var journalMode = await context.Database.SqlQueryRaw<string>("SELECT journal_mode AS \"Value\" FROM pragma_journal_mode()").SingleAsync(ct).ConfigureAwait(false);

            if (usuarios < 1 || !string.Equals(journalMode, "wal", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Condiciones de base de datos no cumplidas: usuarios={usuarios}, journal_mode={journalMode}");
            }

            var migracionesCount = context.Database.GetMigrations().Count();
            return $"Base creada y migrada ({migracionesCount} migraciones).";
        }).ConfigureAwait(false));

        // 2. Pdf
        pasos.Add(await EjecutarPasoAsync("Pdf", async () =>
        {
            using var ms = new MemoryStream();
            await _pdf.GenerarPdfAsync(ReporteDeEjemplo(_tiempo), ms, ct).ConfigureAwait(false);
            var bytes = ms.ToArray();

            if (bytes.Length < 4 || bytes[0] != 0x25 || bytes[1] != 0x50 || bytes[2] != 0x44 || bytes[3] != 0x46)
            {
                throw new InvalidOperationException("El archivo generado no tiene cabecera PDF válida.");
            }

            return $"PDF generado ({bytes.Length} bytes).";
        }).ConfigureAwait(false));

        // 3. Excel
        pasos.Add(await EjecutarPasoAsync("Excel", async () =>
        {
            using var ms = new MemoryStream();
            await _excel.GenerarExcelAsync(ReporteDeEjemplo(_tiempo), ms, ct).ConfigureAwait(false);
            var bytes = ms.ToArray();

            if (bytes.Length < 2 || bytes[0] != 0x50 || bytes[1] != 0x4B)
            {
                throw new InvalidOperationException("El archivo generado no tiene cabecera ZIP/Excel válida.");
            }

            return $"Excel generado ({bytes.Length} bytes).";
        }).ConfigureAwait(false));

        // 4. Backup
        pasos.Add(await EjecutarPasoAsync("Backup", async () =>
        {
            var r = await _motorBackup.CrearAsync(new SolicitudBackup(TipoBackup.Manual, ReglasBackup.DiasRetencionPorDefecto, null), ct).ConfigureAwait(false);
            if (!File.Exists(r.Backup.RutaArchivo))
            {
                throw new InvalidOperationException($"El archivo de backup no existe: {r.Backup.RutaArchivo}");
            }

            return $"Copia creada: {r.Backup.NombreArchivo}.";
        }).ConfigureAwait(false));

        // 5. Escritura
        pasos.Add(await EjecutarPasoAsync("Escritura", async () =>
        {
            var rutaTmp = Path.Combine(_appPaths.ConfigDir, "autoprueba.tmp");
            await File.WriteAllTextAsync(rutaTmp, "ok", ct).ConfigureAwait(false);
            var leido = await File.ReadAllTextAsync(rutaTmp, ct).ConfigureAwait(false);
            File.Delete(rutaTmp);

            if (leido != "ok" || File.Exists(rutaTmp))
            {
                throw new InvalidOperationException("La verificación de lectura/borrado temporal en la carpeta de configuración falló.");
            }

            return "Carpeta de datos escribible.";
        }).ConfigureAwait(false));

        return new ResultadoAutoprueba(
            Version: InfoVersion.Version,
            Compilacion: InfoVersion.Informativa,
            Runtime: RuntimeInformation.FrameworkDescription,
            Proceso64Bits: Environment.Is64BitProcess,
            FechaUtc: _tiempo.GetUtcNow().UtcDateTime,
            Exito: pasos.All(p => p.Exito),
            Pasos: pasos);
    }

    private async Task<PasoAutoprueba> EjecutarPasoAsync(string nombre, Func<Task<string>> accion)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var mensaje = await accion().ConfigureAwait(false);
            sw.Stop();
            return new PasoAutoprueba(nombre, true, mensaje, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogWarning(ex, "Autoprueba: falló el paso {Paso}.", nombre);
            return new PasoAutoprueba(nombre, false, $"{ex.GetType().Name}: {ex.Message}", sw.ElapsedMilliseconds);
        }
    }

    internal static ReportePesajes ReporteDeEjemplo(TimeProvider tiempo)
    {
        var ahoraLocal = TimeZoneInfo.ConvertTimeFromUtc(tiempo.GetUtcNow().UtcDateTime, tiempo.LocalTimeZone);
        var ahoraUnspecified = DateTime.SpecifyKind(ahoraLocal, DateTimeKind.Unspecified);
        var hoy = DateOnly.FromDateTime(ahoraLocal);

        var fila = new FilaReporte(
            Item: 1,
            PesajeId: 1,
            NumeroBoleta: 1,
            FechaHoraLocal: ahoraUnspecified,
            Secuencia: 1,
            MarcaGanadero: "AB",
            NombreGanadero: "GANADERO DE PRUEBA",
            CodigoCliente: 1,
            NombreCliente: "CLIENTE DE PRUEBA",
            Direccion: "-",
            TipoDesposte: TipoDesposte.ResCompleta,
            PesoIzquierdo: 100.0m,
            PesoDerecho: 100.0m,
            PesoTotal: 200.0m,
            Origen: OrigenPeso.Balanza,
            AnuladoEnLocal: null,
            AnuladoPor: null,
            MotivoAnulacion: null);

        var totales = new TotalesReporte(
            Boletas: 1,
            ResesCompletas: 1,
            MediasReses: 0,
            Reses: 1m,
            TotalIzquierdoKg: 100.0m,
            TotalDerechoKg: 100.0m,
            TotalNetoKg: 200.0m,
            Simuladas: 0);

        return new ReportePesajes(
            Tipo: TipoReporte.PesajesDelDia,
            Desde: hoy,
            Hasta: hoy,
            ClaveFiltro: null,
            Empresa: "AUTOPRUEBA",
            RutaLogo: null,
            Titulo: "AUTOPRUEBA",
            Subtitulo: "",
            GeneradoPor: "autoprueba",
            GeneradoEnLocal: ahoraUnspecified,
            Filas: new[] { fila },
            Totales: totales);
    }

    public static bool CarpetaValida(string? carpeta)
    {
        if (string.IsNullOrWhiteSpace(carpeta))
        {
            return false;
        }

        if (!Path.IsPathFullyQualified(carpeta))
        {
            return false;
        }

        var fullPath = Path.GetFullPath(carpeta).TrimEnd('\\', '/');
        var programDataPesaje = Path.GetFullPath(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "PesajeFaeneo")).TrimEnd('\\', '/');

        if (string.Equals(fullPath, programDataPesaje, StringComparison.OrdinalIgnoreCase) ||
            fullPath.StartsWith(programDataPesaje + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            fullPath.StartsWith(programDataPesaje + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (Directory.Exists(fullPath) && Directory.EnumerateFileSystemEntries(fullPath).Any())
        {
            return false;
        }

        return true;
    }

    public static ResultadoAutoprueba ResultadoDeError(Exception ex)
    {
        return new ResultadoAutoprueba(
            Version: InfoVersion.Version,
            Compilacion: InfoVersion.Informativa,
            Runtime: RuntimeInformation.FrameworkDescription,
            Proceso64Bits: Environment.Is64BitProcess,
            FechaUtc: DateTime.UtcNow,
            Exito: false,
            Pasos: new[] { new PasoAutoprueba("Inicio", false, $"{ex.GetType().Name}: {ex.Message}", 0) });
    }

    public static void EscribirResultado(string carpeta, ResultadoAutoprueba r)
    {
        var opciones = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(r, opciones);
        var ruta = Path.Combine(carpeta, "autoprueba.json");
        File.WriteAllText(ruta, json, new UTF8Encoding(false));
    }
}
