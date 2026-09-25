using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PesajeFaeneo.Application;
using PesajeFaeneo.Application.Abstracciones;
using PesajeFaeneo.Application.Catalogos;
using PesajeFaeneo.Application.Parametros;
using PesajeFaeneo.Infrastructure;
using PesajeFaeneo.Infrastructure.Common;
using PesajeFaeneo.Infrastructure.Persistencia;
using PesajeFaeneo.Tests.Utilidades;
using PesajeFaeneo.WinForms;
using PesajeFaeneo.WinForms.Formularios;
using PesajeFaeneo.WinForms.Formularios.Administracion;
using PesajeFaeneo.WinForms.Formularios.Catalogos;
using PesajeFaeneo.WinForms.Formularios.Seguridad;
using PesajeFaeneo.WinForms.Infraestructura;
using Xunit;

namespace PesajeFaeneo.Tests;

public class ContenedorDITests
{
    [Fact]
    public void ContenedorDI_SeConstruyeSinErrores_YResuelveServicios()
    {
        var directorioTemporal = Path.Combine(Path.GetTempPath(), $"pesaje_di_test_{Guid.NewGuid():N}");
        try
        {
            var appPaths = new AppPaths(directorioTemporal);
            appPaths.AsegurarDirectorios();

            var builder = Host.CreateApplicationBuilder();

            builder.Services.AddApplication();
            builder.Services.AddInfrastructure(appPaths);
            builder.Services.AddWinForms();

            using var proveedor = builder.Services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            });

            Assert.NotNull(proveedor.GetService<IDbContextFactory<PesajeDbContext>>());
            Assert.NotNull(proveedor.GetService<IPasswordHasher>());
            Assert.NotNull(proveedor.GetService<IFormFactory>());
            Assert.NotNull(proveedor.GetService<TimeProvider>());
            Assert.NotNull(proveedor.GetService<PesajeFaeneo.Application.Seguridad.ISesionActual>());
            Assert.NotNull(proveedor.GetService<PesajeFaeneo.Application.Seguridad.IServicioAutenticacion>());
            Assert.NotNull(proveedor.GetService<PesajeFaeneo.Application.Seguridad.IServicioUsuarios>());
            Assert.NotNull(proveedor.GetService<PesajeFaeneo.Application.Abstracciones.Persistencia.IUnidadDeTrabajoFactory>());

            // F2: Servicios de catálogos, parámetros y almacenamiento de logo
            Assert.NotNull(proveedor.GetService<IServicioGanaderos>());
            Assert.NotNull(proveedor.GetService<IServicioClientes>());
            Assert.NotNull(proveedor.GetService<IServicioFleteros>());
            Assert.NotNull(proveedor.GetService<IServicioParametros>());
            Assert.NotNull(proveedor.GetService<IAlmacenLogo>());

            using (var scope = proveedor.CreateScope())
            {
                Assert.NotNull(scope.ServiceProvider.GetService<IDatabaseInitializer>());
                Assert.NotNull(scope.ServiceProvider.GetService<IDataSeeder>());
            }

            HiloSta.Ejecutar(() =>
            {
                var formFactory = proveedor.GetRequiredService<IFormFactory>();

                using var frmLogin = formFactory.Crear<FrmLogin>();
                Assert.NotNull(frmLogin);

                using var frmPrincipal = formFactory.Crear<FrmPrincipal>();
                Assert.NotNull(frmPrincipal);

                using var frmUsuarios = formFactory.Crear<FrmUsuarios>();
                Assert.NotNull(frmUsuarios);

                using var frmUsuarioEdicion = formFactory.Crear<FrmUsuarioEdicion>();
                Assert.NotNull(frmUsuarioEdicion);

                using var frmCambioPassword = formFactory.Crear<FrmCambioPassword>();
                Assert.NotNull(frmCambioPassword);

                using var frmRestablecer = formFactory.Crear<FrmRestablecerPassword>();
                Assert.NotNull(frmRestablecer);

                // F2: Formularios de catálogos y parámetros
                using var frmGanaderos = formFactory.Crear<FrmGanaderos>();
                Assert.NotNull(frmGanaderos);

                using var frmGanaderoEdicion = formFactory.Crear<FrmGanaderoEdicion>();
                Assert.NotNull(frmGanaderoEdicion);

                using var frmClientes = formFactory.Crear<FrmClientes>();
                Assert.NotNull(frmClientes);

                using var frmClienteEdicion = formFactory.Crear<FrmClienteEdicion>();
                Assert.NotNull(frmClienteEdicion);

                using var frmFleteros = formFactory.Crear<FrmFleteros>();
                Assert.NotNull(frmFleteros);

                using var frmFleteroEdicion = formFactory.Crear<FrmFleteroEdicion>();
                Assert.NotNull(frmFleteroEdicion);

                using var frmParametros = formFactory.Crear<FrmParametros>();
                Assert.NotNull(frmParametros);
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
