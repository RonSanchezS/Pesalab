using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PesajeFaeneo.Application.Abstracciones;
using PesajeFaeneo.Infrastructure.Common;
using PesajeFaeneo.Infrastructure.Persistencia;
using PesajeFaeneo.Infrastructure.Seguridad;

namespace PesajeFaeneo.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Recibe <paramref name="appPaths"/> ya construido (y con <c>AsegurarDirectorios()</c> ya invocado)
    /// en lugar de crear su propia instancia: el registro de servicios no debe hacer E/S, y antes había
    /// dos instancias de <see cref="AppPaths"/> distintas (una aquí, otra en Program) más una creación de
    /// carpetas reales en <c>C:\ProgramData\PesajeFaeneo</c> cada vez que un test de DI construía el
    /// contenedor.
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        AppPaths appPaths)
    {
        services.AddSingleton(appPaths);

        services.AddDbContextFactory<PesajeDbContext>(options =>
        {
            options.ConfigurarSqlite(appPaths.ConnectionString);
        });

        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<PesajeFaeneo.Application.Abstracciones.Persistencia.IUnidadDeTrabajoFactory, UnidadDeTrabajoFactory>();
        services.AddSingleton<IAlmacenLogo, Archivos.AlmacenLogoArchivos>();
        services.AddSingleton<IAlmacenConfiguracionImpresion>(sp =>
            new Archivos.AlmacenConfiguracionImpresionJson(
                appPaths,
                sp.GetRequiredService<ILogger<Archivos.AlmacenConfiguracionImpresionJson>>(),
                Environment.MachineName));
        services.AddScoped<IDatabaseInitializer, DatabaseInitializer>();
        services.AddScoped<IDataSeeder, DataSeeder>();

        services.AddSingleton<Application.Balanza.ISimuladorBalanza, Balanza.Simulador.SimuladorBalanza>();
        services.AddSingleton<Application.Balanza.IFabricaLectorBalanza, Balanza.FabricaLectorBalanza>();
        services.AddSingleton<Application.Balanza.IEnumeradorPuertosSerie, Balanza.EnumeradorPuertosSerie>();

        services.AddSingleton<Application.Reportes.IGeneradorPdfReportes, Reportes.GeneradorPdfReportes>();
        services.AddSingleton<Application.Reportes.IGeneradorExcelReportes, Reportes.GeneradorExcelReportes>();

        services.AddSingleton<Application.Utilitarios.Backup.IMotorBackup>(sp =>
            new Utilitarios.Backup.SqliteBackupService(
                appPaths,
                sp.GetRequiredService<IDbContextFactory<PesajeDbContext>>(),
                sp.GetRequiredService<TimeProvider>(),
                sp.GetRequiredService<ILogger<Utilitarios.Backup.SqliteBackupService>>(),
                Environment.MachineName));

        services.AddSingleton<Application.Utilitarios.Backup.IAplicadorRestauracion, Utilitarios.Backup.AplicadorRestauracion>();
        services.AddSingleton<Application.Utilitarios.Backup.IAlmacenConfiguracionBackup, Utilitarios.Backup.AlmacenConfiguracionBackupJson>();

        services.AddSingleton<Application.Utilitarios.Importacion.ILectorOrigenDatos, Utilitarios.Importacion.LectorOrigenDatos>();
        services.AddSingleton<Application.Utilitarios.Importacion.IRepositorioImportacion, Utilitarios.Importacion.RepositorioImportacion>();

        services.AddSingleton<Application.Utilitarios.Logs.ILectorArchivosLog, Utilitarios.Logs.LectorArchivosLog>();

        // F13: licencias. En el build de capturas el almacén es en memoria (ya activado) y el servidor, de demostración.
#if CAPTURAS_MANUAL
        services.AddSingleton<Application.Licencias.IIdentificadorEquipo, Licencias.Demo.IdentificadorEquipoDemo>();
        services.AddSingleton<Application.Licencias.IAlmacenLicenciaLocal, Licencias.Demo.AlmacenLicenciaMemoria>();
        services.AddSingleton<Application.Licencias.IServidorLicencias, Licencias.Demo.ServidorLicenciasDemo>();
#else
        services.AddSingleton<Application.Licencias.IIdentificadorEquipo>(_ =>
            OperatingSystem.IsWindows() ? new Licencias.IdentificadorEquipoWindows() : throw new PlatformNotSupportedException());
        services.AddSingleton<Application.Licencias.IAlmacenLicenciaLocal>(_ =>
            OperatingSystem.IsWindows() ? new Licencias.AlmacenLicenciaRegistro() : throw new PlatformNotSupportedException());
        services.AddSingleton<Application.Licencias.IServidorLicencias, Licencias.ServidorLicenciasSupabase>();
#endif

#if CAPTURAS_MANUAL
        // Solo build de capturas para los manuales: puertos COM de demostración en lugar de los del equipo.
        services.AddSingleton<Application.Balanza.IEnumeradorPuertosSerie, Balanza.Demo.EnumeradorPuertosDemo>();
#endif

        services.AddSingleton(TimeProvider.System);

        return services;
    }
}
