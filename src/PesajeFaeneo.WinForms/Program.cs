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
    public static async Task Main(string[] args)
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
            return;
        }

        // 2. Manejo global de excepciones
        WinFormsApp.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        WinFormsApp.ThreadException += (_, e) => ManejadorErrores.Mostrar(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                ManejadorErrores.Mostrar(ex);
            }
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Log.Error(e.Exception, "Excepción no observada en tarea en segundo plano.");
            e.SetObserved();
        };

        var appPaths = new AppPaths();
        appPaths.AsegurarDirectorios();

        // 3. Configurar Serilog
        var logFilePath = Path.Combine(appPaths.LogsDir, "pesaje-.log");
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", Serilog.Events.LogEventLevel.Warning)
            .MinimumLevel.Override("System", Serilog.Events.LogEventLevel.Warning)
            .WriteTo.File(
                logFilePath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
        Log.Information("Aplicación iniciada v{Version}", version);

        try
        {
            // 4. Configurar Host y DI
            var builder = Host.CreateApplicationBuilder(args);
            builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

            builder.Services.AddApplication();
            builder.Services.AddInfrastructure(builder.Configuration);
            builder.Services.AddWinForms();

            var host = builder.Build();
            await host.StartAsync();

            // 5. Inicializar BD y Seeder inicial
            using (var scope = host.Services.CreateScope())
            {
                var initializer = scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>();
                await initializer.InicializarAsync();

                var seeder = scope.ServiceProvider.GetRequiredService<IDataSeeder>();
                await seeder.SembrarAsync();
            }

            // 6. Ejecutar interfaz principal
            var formFactory = host.Services.GetRequiredService<IFormFactory>();
            var mainForm = formFactory.Crear<FrmPrincipal>();

            WinFormsApp.Run(mainForm);

            await host.StopAsync();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Error fatal al iniciar la aplicación.");
            MessageBox.Show(
                $"Ocurrió un error fatal al iniciar la aplicación: {ex.Message}",
                "Error Fatal de Arranque",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            Environment.Exit(1);
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}
