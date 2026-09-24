using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PesajeFaeneo.Infrastructure.Common;
using PesajeFaeneo.WinForms.Infraestructura;
using Serilog;
using Xunit;

namespace PesajeFaeneo.Tests;

public class ConfiguracionLoggingTests
{
    [Fact]
    public void ConfigurarSerilog_NoRegistraProveedoresPorDefectoDelHost()
    {
        var directorioTemporal = Path.Combine(Path.GetTempPath(), $"pesaje_log_test_{Guid.NewGuid():N}");
        try
        {
            var appPaths = new AppPaths(directorioTemporal);
            appPaths.AsegurarDirectorios();

            var builder = Host.CreateApplicationBuilder();
            ConfiguracionLogging.ConfigurarSerilog(builder, appPaths);

            using var host = builder.Build();

            // ClearProviders() + AddSerilog deben dejar el pipeline de logging sin los proveedores por
            // defecto del Host genérico (Console, Debug y, sobre todo, EventLog en Windows).
            var proveedores = host.Services.GetServices<ILoggerProvider>().ToList();
            Assert.DoesNotContain(proveedores, p => p.GetType().Name == "EventLogLoggerProvider");
            Assert.DoesNotContain(proveedores, p => p.GetType().Name == "ConsoleLoggerProvider");
            Assert.DoesNotContain(proveedores, p => p.GetType().Name == "DebugLoggerProvider");
        }
        finally
        {
            if (Directory.Exists(directorioTemporal))
            {
                try { Directory.Delete(directorioTemporal, recursive: true); } catch { /* mejor esfuerzo */ }
            }
        }
    }

    [Fact]
    public async Task ConfigurarSerilog_ConectaILoggerDeMicrosoftExtensionsLogging_ConElArchivoDeSerilog()
    {
        var directorioTemporal = Path.Combine(Path.GetTempPath(), $"pesaje_log_test_{Guid.NewGuid():N}");
        try
        {
            var appPaths = new AppPaths(directorioTemporal);
            appPaths.AsegurarDirectorios();

            var builder = Host.CreateApplicationBuilder();
            ConfiguracionLogging.ConfigurarSerilog(builder, appPaths);

            using (var host = builder.Build())
            {
                // Un ILogger<T> inyectado (como lo usarán los servicios de F1+) debe llegar al mismo
                // archivo que configura ConfiguracionLogging, y no perderse en los proveedores por
                // defecto del Host que ClearProviders() eliminó.
                var logger = host.Services.GetRequiredService<ILogger<ConfiguracionLoggingTests>>();
                const string marcador = "MARCADOR_PRUEBA_LOGGING_MEF";
                logger.LogInformation(marcador);

                // Al desechar el host se desecha también el ILoggerProvider de Serilog registrado en
                // el contenedor, lo que fuerza el volcado del sink de archivo (sin tocar Log.Logger
                // estático, que otras pruebas podrían estar usando en paralelo).
            }

            var archivoDeHoy = Directory.GetFiles(appPaths.LogsDir, "pesaje-*.log").Single();
            var contenido = await File.ReadAllTextAsync(archivoDeHoy);
            Assert.Contains("MARCADOR_PRUEBA_LOGGING_MEF", contenido);
        }
        finally
        {
            if (Directory.Exists(directorioTemporal))
            {
                try { Directory.Delete(directorioTemporal, recursive: true); } catch { /* mejor esfuerzo */ }
            }
        }
    }
}
