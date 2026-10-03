using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PesajeFaeneo.Application;
using PesajeFaeneo.Application.Abstracciones;
using PesajeFaeneo.Application.Arranque;
using PesajeFaeneo.Application.Boletas.Diseno;
using PesajeFaeneo.Application.Utilitarios.Backup;
using PesajeFaeneo.Application.Utilitarios.Backup.Dtos;
using PesajeFaeneo.Infrastructure;
using PesajeFaeneo.Infrastructure.Common;
using PesajeFaeneo.Infrastructure.Instalacion;
using PesajeFaeneo.WinForms.Formularios;
using PesajeFaeneo.WinForms.Infraestructura;
using Serilog;
using WinFormsApp = System.Windows.Forms.Application;

namespace PesajeFaeneo.WinForms;

public static class Program
{
    private const string MutexName = @"Global\PesajeFaeneo";

    [STAThread]
    public static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        // Autoprueba de la instalación (F9-01, D5): no toma el mutex, no toca ProgramData y nunca muestra diálogos.
        var indiceAutoprueba = Array.FindIndex(args, a => string.Equals(a, ArgumentoAutoprueba, StringComparison.OrdinalIgnoreCase));
        if (indiceAutoprueba >= 0)
        {
            return EjecutarAutoprueba(indiceAutoprueba + 1 < args.Length ? args[indiceAutoprueba + 1] : null);
        }

        // 1. Instancia única. Tras una restauración (F8-03) la aplicación se relanza con --reinicio: la nueva
        // instancia espera a que la anterior termine en lugar de avisar que ya está en ejecución.
        var esReinicio = args.Contains(InstanciaUnica.ArgumentoReinicio, StringComparer.OrdinalIgnoreCase);
        // El argumento es solo para el mutex: no se pasa al Host (su proveedor de línea de comandos lo tomaría
        // como una clave de configuración y, si hubiera otro argumento detrás, como su valor; H17).
        var argsHost = args.Where(a => !string.Equals(a, InstanciaUnica.ArgumentoReinicio, StringComparison.OrdinalIgnoreCase)).ToArray();
        using var instancia = InstanciaUnica.Adquirir(MutexName, esReinicio, TimeSpan.FromSeconds(30));
        if (instancia is null)
        {
            MessageBox.Show(
                "El sistema ya está en ejecución en este equipo (puede estar abierto en la sesión de otro usuario de Windows).",
                "Instancia en ejecución",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return 0;
        }

        // 2. Manejo global de excepciones
        WinFormsApp.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        WinFormsApp.ThreadException += (_, e) => ManejadorErrores.Mostrar(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            // Aquí el proceso YA va a terminar (e.IsTerminating es true en la práctica: no hay forma
            // de recuperarse de una excepción no controlada fuera de un hilo de UI). Se registra como
            // fatal y se intenta avisar brevemente al usuario, pero sin depender de ManejadorErrores
            // (que solo tiene sentido cuando la aplicación puede seguir ejecutándose) y asegurando que
            // el log quede en disco antes de que el proceso desaparezca.
            if (e.ExceptionObject is Exception ex)
            {
                Log.Fatal(ex, "Excepción no controlada fuera de un hilo de UI. El proceso va a terminar.");
            }
            else
            {
                Log.Fatal("Excepción no controlada fuera de un hilo de UI (objeto no es Exception: {ExceptionObject}). El proceso va a terminar.", e.ExceptionObject);
            }

            try
            {
                MessageBox.Show(
                    "Ocurrió un error irrecuperable y la aplicación debe cerrarse. Revise el archivo de log para más detalles.",
                    "Error Fatal",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            catch
            {
                // Si ni siquiera se puede mostrar el mensaje (por ejemplo, sin sesión interactiva),
                // no debe impedirse que el log fatal ya escrito llegue a disco.
            }

            Log.CloseAndFlush();
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Log.Error(e.Exception, "Excepción no observada en tarea en segundo plano.");
            e.SetObserved();
        };

        var appPaths = new AppPaths();
        appPaths.AsegurarDirectorios();

        // 3. Configurar Serilog (logger de arranque: cubre el tramo previo a que exista el Host;
        // appsettings.json es la única fuente de verdad para los niveles mínimos, por eso aquí no se
        // repiten los MinimumLevel.Override — esos se aplican luego vía ConfiguracionLogging).
        var logFilePath = Path.Combine(appPaths.LogsDir, "pesaje-.log");
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                logFilePath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateBootstrapLogger();

        Log.Information("Aplicación iniciada v{Version} ({Compilacion})", InfoVersion.Version, InfoVersion.Informativa);

        try
        {
            // 4. Configurar Host y DI. ContentRootPath se fija explícitamente en el directorio del
            // ejecutable: Host.CreateApplicationBuilder(args) por defecto usa Directory.GetCurrentDirectory(),
            // que depende de cómo se lanzó el proceso (por ejemplo, el "Iniciar en" de un acceso directo) y
            // podría no encontrar appsettings.json. El Host ya agrega "appsettings.json" automáticamente
            // desde ContentRootPath, por lo que no hace falta (ni conviene) agregarlo de nuevo aquí.
            var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
            {
                Args = argsHost,
                ContentRootPath = AppContext.BaseDirectory
            });

            // Reemplaza por completo el logger de arranque: conecta Serilog con Microsoft.Extensions.Logging
            // (ILogger<T> inyectado en servicios) y quita los proveedores por defecto del Host (EventLog, etc.).
            ConfiguracionLogging.ConfigurarSerilog(builder, appPaths);

            builder.Services.AddApplication();
            builder.Services.AddInfrastructure(appPaths);
            builder.Services.AddWinForms();

            using var host = builder.Build();

            // El hilo aún no tiene WindowsFormsSynchronizationContext instalado en este punto,
            // por lo que bloquear con GetAwaiter().GetResult() es seguro y mantiene el hilo en modo STA.
            host.Start();

            // 5. Restauración pendiente (F8-03): ANTES de abrir la base de datos.
            var aplicador = host.Services.GetRequiredService<IAplicadorRestauracion>();
            var restauracion = aplicador.AplicarPendiente();

            // 6. Inicializar BD y Seeder; si la base recién restaurada no inicializa, se vuelve a la anterior.
            restauracion = InicializarBaseDeDatos(host.Services, aplicador, restauracion);
            AvisarRestauracion(restauracion, host.Services.GetRequiredService<TimeProvider>().LocalTimeZone);

            // 7. Balanza: se inicia después de migrar (lee su configuración de la BD) y nunca impide abrir la
            // aplicación; si falla, la barra de estado lo mostrará y el administrador podrá corregirlo.
            var servicioBalanza = host.Services.GetRequiredService<PesajeFaeneo.Application.Balanza.IServicioBalanza>();
            try
            {
                servicioBalanza.IniciarAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "No se pudo iniciar el servicio de balanza. La aplicación continúa sin lectura de balanza.");
            }

            // 8. Backup automático diario (F8-02): nunca impide abrir la aplicación.
            var programador = host.Services.GetRequiredService<IProgramadorBackupAutomatico>();
            programador.Iniciar();

            // 9. Ciclo de aplicación
            bool reiniciar;
            using (var contexto = ActivatorUtilities.CreateInstance<ContextoAplicacion>(host.Services))
            {
                WinFormsApp.Run(contexto);
                reiniciar = contexto.ReinicioSolicitado;
            }

            // 10. Cierre: las esperas van dentro de Task.Run para no depender del contexto de sincronización de la UI.
            try
            {
                Task.Run(() => programador.DetenerAsync()).WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "El programador de backups no se detuvo a tiempo.");
            }

            // En un try propio: que DetenerAsync agote su espera no debe saltarse el backup de cierre.
            if (!reiniciar)
            {
                try
                {
                    Task.Run(() => programador.EjecutarAlCerrarAsync()).WaitAsync(TimeSpan.FromSeconds(60)).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "El backup automático de cierre no terminó correctamente.");
                }
            }

            try
            {
                servicioBalanza.DetenerAsync().WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "La balanza no se detuvo correctamente al salir.");
            }

            host.StopAsync().GetAwaiter().GetResult();

            if (reiniciar)
            {
                // Soltar las conexiones agrupadas: la nueva instancia moverá pesaje.db apenas obtenga el mutex.
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                IniciarNuevaInstancia();
            }

            return 0;
        }
        catch (ArranqueBaseDatosException ex)
        {
            Log.Error(ex, "La base de datos no se pudo preparar ({Motivo}).", ex.Motivo);
            MessageBox.Show(ex.Message, "No se pudo abrir la base de datos", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 2;
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Error fatal al iniciar la aplicación.");
            MessageBox.Show(
                $"Ocurrió un error fatal al iniciar la aplicación: {ex.Message}",
                "Error Fatal de Arranque",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return 1;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    private static ResultadoRestauracionArranque InicializarBaseDeDatos(
        IServiceProvider servicios, IAplicadorRestauracion aplicador, ResultadoRestauracionArranque restauracion)
    {
        try
        {
            Inicializar(servicios);
        }
        catch (Exception ex) when (restauracion.Estado == EstadoRestauracionArranque.Aplicada)
        {
            Log.Error(ex, "La base de datos restaurada no se pudo inicializar; se vuelve a la anterior.");
            aplicador.Revertir(restauracion);
            Inicializar(servicios); // si también falla, sale por el catch general (error fatal de arranque)
            return restauracion with
            {
                Estado = EstadoRestauracionArranque.Fallida,
                Mensaje = "La copia restaurada no se pudo abrir con esta versión del sistema; se volvió a la base de datos anterior."
            };
        }

        if (restauracion.Estado == EstadoRestauracionArranque.Aplicada)
        {
            try
            {
                aplicador.RegistrarAuditoriaAsync(restauracion).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "No se pudo auditar la restauración aplicada.");
            }
        }

        return restauracion;
    }

    private static void Inicializar(IServiceProvider servicios)
    {
        using var scope = servicios.CreateScope();
        // F9-02: evalúa el esquema, copia antes de migrar si es una actualización y migra. Lanza
        // ArranqueBaseDatosException cuando la aplicación no debe abrir (base más nueva, copia o migración fallida).
        var actualizador = scope.ServiceProvider.GetRequiredService<IActualizadorBaseDatos>();
        var resultado = actualizador.PrepararAsync().GetAwaiter().GetResult();
        scope.ServiceProvider.GetRequiredService<IDataSeeder>().SembrarAsync().GetAwaiter().GetResult();

        try
        {
            actualizador.RegistrarAuditoriaAsync(resultado).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "No se pudo auditar la actualización de la base de datos.");
        }
    }

    private static void AvisarRestauracion(ResultadoRestauracionArranque r, TimeZoneInfo zona)
    {
        if (r.Estado == EstadoRestauracionArranque.Aplicada)
        {
            var fecha = r.FechaBackupUtc is { } f
                ? TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(f, DateTimeKind.Utc), zona).ToString("dd/MM/yyyy HH:mm", TextosBoleta.Cultura)
                : "(desconocida)";
            MessageBox.Show(
                $"Se restauró la copia de seguridad del {fecha}. Inicie sesión con los usuarios de esa copia.",
                "Restauración aplicada", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        else if (r.Estado == EstadoRestauracionArranque.Fallida)
        {
            MessageBox.Show(r.Mensaje ?? "No se pudo aplicar la restauración.", "Restauración no aplicada",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private static void IniciarNuevaInstancia()
    {
        try
        {
            var ruta = Environment.ProcessPath;
            if (ruta is null)
            {
                Log.Warning("No se pudo relanzar la aplicación: ruta del ejecutable desconocida.");
                return;
            }

            Log.Information("Reiniciando la aplicación para aplicar la restauración.");
            Process.Start(new ProcessStartInfo(ruta, InstanciaUnica.ArgumentoReinicio) { UseShellExecute = false });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "No se pudo relanzar la aplicación; la restauración se aplicará al abrirla manualmente.");
        }
    }

    internal const string ArgumentoAutoprueba = "--autoprueba";

    private static int EjecutarAutoprueba(string? carpeta)
    {
        if (!AutopruebaInstalacion.CarpetaValida(carpeta))
        {
            return 3;
        }

        var appPaths = new AppPaths(carpeta!);
        appPaths.AsegurarDirectorios();
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(Path.Combine(appPaths.LogsDir, "pesaje-.log"), rollingInterval: RollingInterval.Day,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateBootstrapLogger();

        ResultadoAutoprueba resultado;
        try
        {
            var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
            {
                Args = Array.Empty<string>(),
                ContentRootPath = AppContext.BaseDirectory
            });
            ConfiguracionLogging.ConfigurarSerilog(builder, appPaths);
            builder.Services.AddApplication();
            builder.Services.AddInfrastructure(appPaths);
            builder.Services.AddWinForms();

            using var host = builder.Build();
            using var scope = host.Services.CreateScope();
            var autoprueba = ActivatorUtilities.CreateInstance<AutopruebaInstalacion>(scope.ServiceProvider);
            resultado = autoprueba.EjecutarAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "La autoprueba no pudo iniciarse.");
            resultado = AutopruebaInstalacion.ResultadoDeError(ex);
        }

        try
        {
            AutopruebaInstalacion.EscribirResultado(appPaths.BaseDir, resultado);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "No se pudo escribir el resultado de la autoprueba.");
            resultado = resultado with { Exito = false };
        }
        finally
        {
            Log.CloseAndFlush();
        }

        return resultado.Exito ? 0 : 1;
    }
}
