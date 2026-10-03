namespace PesajeFaeneo.Tests;

using System.IO;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using PesajeFaeneo.Application.Abstracciones;
using PesajeFaeneo.Application.Reportes;
using PesajeFaeneo.Application.Reportes.Dtos;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Infrastructure.Common;
using PesajeFaeneo.Infrastructure.Instalacion;
using PesajeFaeneo.Infrastructure.Persistencia;
using PesajeFaeneo.Infrastructure.Reportes;
using PesajeFaeneo.Infrastructure.Seguridad;
using PesajeFaeneo.Infrastructure.Utilitarios.Backup;
using Xunit;

public sealed class AutopruebaInstalacionTests : IAsyncDisposable
{
    private readonly string _dir;
    private readonly AppPaths _appPaths;
    private readonly ArchivoDbContextFactory _contextFactory;
    private readonly FakeTimeProvider _tiempo;

    public AutopruebaInstalacionTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"autoprueba_test_{Guid.NewGuid():N}");
        _appPaths = new AppPaths(_dir);
        _appPaths.AsegurarDirectorios();

        var optionsBuilder = new DbContextOptionsBuilder<PesajeDbContext>();
        optionsBuilder.ConfigurarSqlite(new SqliteConnectionStringBuilder(_appPaths.ConnectionString) { Pooling = false }.ToString());
        _contextFactory = new ArchivoDbContextFactory(optionsBuilder.Options);

        _tiempo = new FakeTimeProvider(new DateTimeOffset(2026, 10, 3, 14, 0, 0, TimeSpan.Zero));
        _tiempo.SetLocalTimeZone(TimeZoneInfo.CreateCustomTimeZone("UTC-4", TimeSpan.FromHours(-4), "UTC-4", "UTC-4"));
    }

    public async ValueTask DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            if (Directory.Exists(_dir))
            {
                Directory.Delete(_dir, true);
            }
        }
        catch
        {
            // Borrado de mejor esfuerzo
        }
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Autoprueba_TodoReal_ExitoConCincoPasosYZipEnManuales()
    {
        var inicializador = new DatabaseInitializer(_contextFactory);
        var sembrador = new DataSeeder(_contextFactory, new Pbkdf2PasswordHasher(), _tiempo);
        var pdf = new GeneradorPdfReportes(NullLogger<GeneradorPdfReportes>.Instance);
        var excel = new GeneradorExcelReportes(NullLogger<GeneradorExcelReportes>.Instance);
        var backup = new SqliteBackupService(_appPaths, _contextFactory, _tiempo, NullLogger<SqliteBackupService>.Instance, "EQUIPO-PRUEBA");

        var autoprueba = new AutopruebaInstalacion(
            _appPaths,
            inicializador,
            sembrador,
            _contextFactory,
            pdf,
            excel,
            backup,
            _tiempo,
            NullLogger<AutopruebaInstalacion>.Instance);

        var resultado = await autoprueba.EjecutarAsync();

        Assert.True(resultado.Exito);
        Assert.Equal(5, resultado.Pasos.Count);
        Assert.Equal("BaseDeDatos", resultado.Pasos[0].Nombre);
        Assert.True(resultado.Pasos[0].Exito);
        Assert.Equal("Pdf", resultado.Pasos[1].Nombre);
        Assert.True(resultado.Pasos[1].Exito);
        Assert.Equal("Excel", resultado.Pasos[2].Nombre);
        Assert.True(resultado.Pasos[2].Exito);
        Assert.Equal("Backup", resultado.Pasos[3].Nombre);
        Assert.True(resultado.Pasos[3].Exito);
        Assert.Equal("Escritura", resultado.Pasos[4].Nombre);
        Assert.True(resultado.Pasos[4].Exito);

        var zips = Directory.GetFiles(Path.Combine(_appPaths.BackupsDir, "manuales"), "*.zip");
        Assert.Single(zips);
    }

    [Fact]
    public async Task Autoprueba_ConPdfFallido_ExitoEsFalseYOtrosPasosExitosos()
    {
        var inicializador = new DatabaseInitializer(_contextFactory);
        var sembrador = new DataSeeder(_contextFactory, new Pbkdf2PasswordHasher(), _tiempo);
        var pdfFalso = new GeneradorPdfQueFalla();
        var excel = new GeneradorExcelReportes(NullLogger<GeneradorExcelReportes>.Instance);
        var backup = new SqliteBackupService(_appPaths, _contextFactory, _tiempo, NullLogger<SqliteBackupService>.Instance, "EQUIPO-PRUEBA");

        var autoprueba = new AutopruebaInstalacion(
            _appPaths,
            inicializador,
            sembrador,
            _contextFactory,
            pdfFalso,
            excel,
            backup,
            _tiempo,
            NullLogger<AutopruebaInstalacion>.Instance);

        var resultado = await autoprueba.EjecutarAsync();

        Assert.False(resultado.Exito);
        Assert.Equal(5, resultado.Pasos.Count);
        Assert.True(resultado.Pasos[0].Exito);
        Assert.False(resultado.Pasos[1].Exito);
        Assert.Contains("sin skia", resultado.Pasos[1].Mensaje);
        Assert.True(resultado.Pasos[2].Exito);
        Assert.True(resultado.Pasos[3].Exito);
        Assert.True(resultado.Pasos[4].Exito);
    }

    [Fact]
    public void CarpetaValida_CasosValidosEInvalidos()
    {
        Assert.False(AutopruebaInstalacion.CarpetaValida(null));
        Assert.False(AutopruebaInstalacion.CarpetaValida(""));
        Assert.False(AutopruebaInstalacion.CarpetaValida(@"relativa\x"));

        var programData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "PesajeFaeneo");
        Assert.False(AutopruebaInstalacion.CarpetaValida(programData));
        Assert.False(AutopruebaInstalacion.CarpetaValida(Path.Combine(programData, "subcarpeta")));

        var dirConArchivo = Path.Combine(Path.GetTempPath(), $"dir_con_archivo_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dirConArchivo);
        try
        {
            File.WriteAllText(Path.Combine(dirConArchivo, "archivo.txt"), "prueba");
            Assert.False(AutopruebaInstalacion.CarpetaValida(dirConArchivo));
        }
        finally
        {
            Directory.Delete(dirConArchivo, true);
        }

        var dirInexistente = Path.Combine(Path.GetTempPath(), $"dir_inexistente_{Guid.NewGuid():N}");
        Assert.True(AutopruebaInstalacion.CarpetaValida(dirInexistente));

        var dirVacio = Path.Combine(Path.GetTempPath(), $"dir_vacio_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dirVacio);
        try
        {
            Assert.True(AutopruebaInstalacion.CarpetaValida(dirVacio));
        }
        finally
        {
            Directory.Delete(dirVacio, true);
        }
    }

    [Fact]
    public void EscribirResultado_GeneraJsonValidoSinBom()
    {
        var pasos = new[]
        {
            new PasoAutoprueba("BaseDeDatos", true, "OK", 50),
            new PasoAutoprueba("Pdf", true, "OK", 20)
        };
        var res = new ResultadoAutoprueba(
            Version: "1.0.0",
            Compilacion: "1.0.0+abc",
            Runtime: ".NET 8.0",
            Proceso64Bits: true,
            FechaUtc: DateTime.UtcNow,
            Exito: true,
            Pasos: pasos);

        var rutaTemp = Path.Combine(Path.GetTempPath(), $"json_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(rutaTemp);
        try
        {
            AutopruebaInstalacion.EscribirResultado(rutaTemp, res);
            var rutaArchivo = Path.Combine(rutaTemp, "autoprueba.json");
            Assert.True(File.Exists(rutaArchivo));

            var bytes = File.ReadAllBytes(rutaArchivo);
            // Comprobar que no empieza con BOM UTF-8 (EF BB BF)
            Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);

            using var doc = JsonDocument.Parse(bytes);
            Assert.True(doc.RootElement.GetProperty("Exito").GetBoolean());
            var pasosArray = doc.RootElement.GetProperty("Pasos");
            Assert.Equal(2, pasosArray.GetArrayLength());
        }
        finally
        {
            Directory.Delete(rutaTemp, true);
        }
    }

    [Fact]
    public void ResultadoDeError_CreaPasoInicioConFallo()
    {
        var ex = new IOException("x");
        var res = AutopruebaInstalacion.ResultadoDeError(ex);

        Assert.False(res.Exito);
        Assert.Single(res.Pasos);
        Assert.Equal("Inicio", res.Pasos[0].Nombre);
        Assert.False(res.Pasos[0].Exito);
        Assert.Equal("IOException: x", res.Pasos[0].Mensaje);
    }

    private sealed class GeneradorPdfQueFalla : IGeneradorPdfReportes
    {
        public Task GenerarPdfAsync(ReportePesajes reporte, Stream destino, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("sin skia");

        public Task<IReadOnlyList<byte[]>> GenerarPaginasAsync(ReportePesajes reporte, int dpi, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("sin skia");
    }

    private sealed class ArchivoDbContextFactory : IDbContextFactory<PesajeDbContext>
    {
        private readonly DbContextOptions<PesajeDbContext> _options;

        public ArchivoDbContextFactory(DbContextOptions<PesajeDbContext> options)
        {
            _options = options;
        }

        public PesajeDbContext CreateDbContext() => new(_options);
    }
}
