using System.Text;
using PesajeFaeneo.Domain.Excepciones;
using PesajeFaeneo.Infrastructure.Common;
using PesajeFaeneo.Infrastructure.Utilitarios.Logs;
using Serilog;
using Xunit;

namespace PesajeFaeneo.Tests;

public class LectorArchivosLogTests : IDisposable
{
    private readonly string _tempDir;
    private readonly AppPaths _appPaths;

    public LectorArchivosLogTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"logs_test_{Guid.NewGuid():N}");
        _appPaths = new AppPaths(_tempDir);
        _appPaths.AsegurarDirectorios();
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }
        catch
        {
            // Limpieza mejor esfuerzo
        }
    }

    [Fact]
    public void Test01_Listar_FiltraArchivosNoValidosYOrdenaDescendente()
    {
        var lector = new LectorArchivosLog(_appPaths);

        File.WriteAllText(Path.Combine(_appPaths.LogsDir, "pesaje-20261001.log"), "log 1");
        File.WriteAllText(Path.Combine(_appPaths.LogsDir, "pesaje-20261003.log"), "log 3");
        File.WriteAllText(Path.Combine(_appPaths.LogsDir, "pesaje-20261002.log"), "log 2");
        File.WriteAllText(Path.Combine(_appPaths.LogsDir, "otro.log"), "otro");
        File.WriteAllText(Path.Combine(_appPaths.LogsDir, "pesaje-invalido.log"), "invalido");

        var lista = lector.Listar();

        Assert.Equal(3, lista.Count);
        Assert.Equal("pesaje-20261003.log", lista[0].Nombre);
        Assert.Equal("pesaje-20261002.log", lista[1].Nombre);
        Assert.Equal("pesaje-20261001.log", lista[2].Nombre);
        Assert.Equal(new DateOnly(2026, 10, 3), lista[0].Fecha);
    }

    [Fact]
    public void Test02_LeerFinal_ConSerilogAbierto_LeeSinConflictoDeBloqueo()
    {
        var patronLog = Path.Combine(_appPaths.LogsDir, "pesaje-.log");
        var serilogLogger = new LoggerConfiguration()
            .WriteTo.File(patronLog, rollingInterval: RollingInterval.Day)
            .CreateLogger();

        try
        {
            serilogLogger.Information("Mensaje de prueba con Serilog abierto");

            var lector = new LectorArchivosLog(_appPaths);
            var archivos = lector.Listar();
            Assert.NotEmpty(archivos);

            var (texto, truncado, tamano) = lector.LeerFinal(archivos[0].Nombre, 4 * 1024 * 1024);

            Assert.Contains("Mensaje de prueba con Serilog abierto", texto);
            Assert.False(truncado);
            Assert.True(tamano > 0);
        }
        finally
        {
            (serilogLogger as IDisposable)?.Dispose();
        }
    }

    [Fact]
    public void Test03_LeerFinal_Truncado_DescartaLineaPartidaYComienzaEnLineaCompleta()
    {
        var lector = new LectorArchivosLog(_appPaths);
        var nombre = "pesaje-20261001.log";
        var ruta = Path.Combine(_appPaths.LogsDir, nombre);

        // 3 líneas de ~30 caracteres cada una
        var contenido =
            "2026-10-01 10:00:00.000 [INF] Linea 1 larga de prueba\n" +
            "2026-10-01 10:01:00.000 [INF] Linea 2 larga de prueba\n" +
            "2026-10-01 10:02:00.000 [INF] Linea 3 larga de prueba\n";

        File.WriteAllText(ruta, contenido, Encoding.UTF8);

        // Con maximoBytes = 60, cae en medio de la línea 2
        var (texto, truncado, tamano) = lector.LeerFinal(nombre, 60);

        Assert.True(truncado);
        Assert.True(tamano > 60);
        // Debe comenzar exactamente con la Línea 3 (descartó la línea 2 partida)
        Assert.StartsWith("2026-10-01 10:02:00.000 [INF] Linea 3", texto);
        Assert.DoesNotContain("Linea 1", texto);
    }

    [Fact]
    public void Test04_LeerFinal_NombreInvalido_LanzaValidacionException()
    {
        var lector = new LectorArchivosLog(_appPaths);

        var ex = Assert.Throws<ValidacionException>(() =>
            lector.LeerFinal("../pesaje-20261001.log", 1000));

        Assert.Equal("Archivo", ex.Errores.Single().Campo);
    }

    [Fact]
    public void Test05_LeerFinal_ArchivoInexistente_LanzaValidacionException()
    {
        var lector = new LectorArchivosLog(_appPaths);

        var ex = Assert.Throws<ValidacionException>(() =>
            lector.LeerFinal("pesaje-20261001.log", 1000));

        Assert.Equal("Archivo", ex.Errores.Single().Campo);
        Assert.Contains("ya no existe", ex.Errores.Single().Mensaje);
    }
}
