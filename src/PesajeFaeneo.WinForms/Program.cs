using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PesajeFaeneo.Application;
using PesajeFaeneo.Application.Abstracciones;
using PesajeFaeneo.Infrastructure;
using PesajeFaeneo.Infrastructure.Common;
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

        // 1. Mutex de instancia única
        using var mutex = new Mutex(true, MutexName, out bool esNuevaInstancia);
        if (!esNuevaInstancia)
        {
            MessageBox.Show(
                "El sistema ya está en ejecución.",
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

        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
        Log.Information("Aplicación iniciada v{Version}", version);

        try
        {
            // 4. Configurar Host y DI. ContentRootPath se fija explícitamente en el directorio del
            // ejecutable: Host.CreateApplicationBuilder(args) por defecto usa Directory.GetCurrentDirectory(),
            // que depende de cómo se lanzó el proceso (por ejemplo, el "Iniciar en" de un acceso directo) y
            // podría no encontrar appsettings.json. El Host ya agrega "appsettings.json" automáticamente
            // desde ContentRootPath, por lo que no hace falta (ni conviene) agregarlo de nuevo aquí.
            var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
            {
                Args = args,
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

            // 5. Inicializar BD y Seeder inicial
            using (var scope = host.Services.CreateScope())
            {
                var initializer = scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>();
                initializer.InicializarAsync().GetAwaiter().GetResult();

                var seeder = scope.ServiceProvider.GetRequiredService<IDataSeeder>();
                seeder.SembrarAsync().GetAwaiter().GetResult();
            }

            // 6. Ejecutar interfaz principal
            var formFactory = host.Services.GetRequiredService<IFormFactory>();
            var mainForm = formFactory.Crear<FrmPrincipal>();

            WinFormsApp.Run(mainForm);

            host.StopAsync().GetAwaiter().GetResult();
            return 0;
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
}
