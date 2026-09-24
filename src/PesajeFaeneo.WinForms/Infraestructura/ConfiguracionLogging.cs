using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PesajeFaeneo.Infrastructure.Common;
using Serilog;

namespace PesajeFaeneo.WinForms.Infraestructura;

/// <summary>
/// Conecta Serilog con <c>Microsoft.Extensions.Logging</c>: sin esto, cualquier <c>ILogger&lt;T&gt;</c>
/// inyectado en servicios de F1+ escribe en los proveedores por defecto del Host (Consola, Debug,
/// EventLog en Windows) y nunca llega al archivo de log. Se expone como método reutilizable para que
/// tanto <c>Program</c> (producción) como las pruebas de DI puedan verificar el mismo cableado.
/// </summary>
public static class ConfiguracionLogging
{
    public static void ConfigurarSerilog(HostApplicationBuilder builder, AppPaths appPaths)
    {
        var logFilePath = Path.Combine(appPaths.LogsDir, "pesaje-.log");

        // Elimina los proveedores de logging por defecto del Host genérico (Console, Debug, EventLog);
        // Serilog pasa a ser la única fuente de verdad para todo lo registrado con ILogger<T>.
        builder.Logging.ClearProviders();

        builder.Services.AddSerilog((servicios, configuracionLogger) => configuracionLogger
            .ReadFrom.Configuration(builder.Configuration)
            .ReadFrom.Services(servicios)
            .WriteTo.File(
                logFilePath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}"));
    }
}
