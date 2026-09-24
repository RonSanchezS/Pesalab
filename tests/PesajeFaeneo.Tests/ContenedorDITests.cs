using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PesajeFaeneo.Application;
using PesajeFaeneo.Application.Abstracciones;
using PesajeFaeneo.Infrastructure;
using PesajeFaeneo.Infrastructure.Common;
using PesajeFaeneo.Infrastructure.Persistencia;
using PesajeFaeneo.Tests.Utilidades;
using PesajeFaeneo.WinForms;
using PesajeFaeneo.WinForms.Formularios;
using PesajeFaeneo.WinForms.Infraestructura;
using Xunit;

namespace PesajeFaeneo.Tests;

public class ContenedorDITests
{
    [Fact]
    public void ContenedorDI_SeConstruyeSinErrores_YResuelveServicios()
    {
        // Directorio temporal propio: AddInfrastructure ya no crea su propio AppPaths ni hace E/S al
        // registrar servicios, así que este test no debe tocar C:\ProgramData\PesajeFaeneo.
        var directorioTemporal = Path.Combine(Path.GetTempPath(), $"pesaje_di_test_{Guid.NewGuid():N}");
        try
        {
            var appPaths = new AppPaths(directorioTemporal);
            appPaths.AsegurarDirectorios();

            var builder = Host.CreateApplicationBuilder();

            builder.Services.AddApplication();
            builder.Services.AddInfrastructure(appPaths);
            builder.Services.AddWinForms();

            // ValidateOnBuild + ValidateScopes detectan de inmediato dependencias mal registradas
            // (por ejemplo, un servicio Scoped inyectado en uno Singleton) en lugar de fallar en
            // producción la primera vez que se resuelve ese servicio.
            using var proveedor = builder.Services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            });

            Assert.NotNull(proveedor.GetService<IDbContextFactory<PesajeDbContext>>());
            Assert.NotNull(proveedor.GetService<IPasswordHasher>());
            Assert.NotNull(proveedor.GetService<IFormFactory>());
            Assert.NotNull(proveedor.GetService<TimeProvider>());

            // IDatabaseInitializer/IDataSeeder son Scoped (igual que en producción, donde Program los
            // resuelve dentro de un scope); con ValidateScopes=true resolverlos desde la raíz lanzaría
            // InvalidOperationException, así que se comprueban dentro de su propio scope.
            using (var scope = proveedor.CreateScope())
            {
                Assert.NotNull(scope.ServiceProvider.GetService<IDatabaseInitializer>());
                Assert.NotNull(scope.ServiceProvider.GetService<IDataSeeder>());
            }

            HiloSta.Ejecutar(() =>
            {
                var formFactory = proveedor.GetRequiredService<IFormFactory>();
                using var frmPrincipal = formFactory.Crear<FrmPrincipal>();
                Assert.NotNull(frmPrincipal);
            });
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
