using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PesajeFaeneo.Application;
using PesajeFaeneo.Application.Abstracciones;
using PesajeFaeneo.Application.Boletas;
using PesajeFaeneo.Application.Catalogos;
using PesajeFaeneo.Application.Parametros;
using PesajeFaeneo.Infrastructure;
using PesajeFaeneo.Infrastructure.Common;
using PesajeFaeneo.Infrastructure.Persistencia;
using PesajeFaeneo.Tests.Utilidades;
using PesajeFaeneo.WinForms;
using PesajeFaeneo.WinForms.Formularios;
using PesajeFaeneo.WinForms.Formularios.Administracion;
using PesajeFaeneo.WinForms.Formularios.Boletas;
using PesajeFaeneo.WinForms.Formularios.Catalogos;
using PesajeFaeneo.WinForms.Formularios.Pesaje;
using PesajeFaeneo.WinForms.Formularios.Seguridad;
using PesajeFaeneo.WinForms.Formularios.Utilitarios;
using PesajeFaeneo.WinForms.Impresion;
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

            // F3: Servicios de balanza
            var balanza1 = proveedor.GetService<PesajeFaeneo.Application.Balanza.IServicioBalanza>();
            var balanza2 = proveedor.GetService<PesajeFaeneo.Application.Balanza.IServicioBalanza>();
            Assert.NotNull(balanza1);
            Assert.Same(balanza1, balanza2);

            Assert.NotNull(proveedor.GetService<PesajeFaeneo.Application.Balanza.IFabricaLectorBalanza>());
            Assert.NotNull(proveedor.GetService<PesajeFaeneo.Application.Balanza.ISimuladorBalanza>());
            Assert.NotNull(proveedor.GetService<PesajeFaeneo.Application.Balanza.IEnumeradorPuertosSerie>());
            Assert.NotNull(proveedor.GetService<PesajeFaeneo.Application.Balanza.IServicioConfiguracionBalanza>());
            Assert.NotNull(proveedor.GetService<PesajeFaeneo.Application.Pesajes.IServicioPesajes>());

            // F5: Servicios de boletas e impresión
            Assert.NotNull(proveedor.GetService<IServicioBoletas>());
            Assert.NotNull(proveedor.GetService<IServicioConfiguracionImpresion>());
            Assert.NotNull(proveedor.GetService<IAlmacenConfiguracionImpresion>());
            Assert.NotNull(proveedor.GetService<IImpresorBoletas>());
            Assert.NotNull(proveedor.GetService<IServicioImpresionBoletas>());

            // F6: Servicios de gestión de pesajes y auditoría
            Assert.NotNull(proveedor.GetService<PesajeFaeneo.Application.Pesajes.IServicioGestionPesajes>());
            Assert.NotNull(proveedor.GetService<PesajeFaeneo.Application.Auditoria.IServicioAuditoria>());

            // F7: Servicios de reportes
            Assert.NotNull(proveedor.GetService<PesajeFaeneo.Application.Reportes.IServicioReportes>());
            Assert.NotNull(proveedor.GetService<PesajeFaeneo.Application.Reportes.IGeneradorPdfReportes>());
            Assert.NotNull(proveedor.GetService<PesajeFaeneo.Application.Reportes.IGeneradorExcelReportes>());

            // F8: Servicios de utilitarios (backup, importación, logs)
            Assert.NotNull(proveedor.GetService<PesajeFaeneo.Application.Utilitarios.Backup.IMotorBackup>());
            Assert.NotNull(proveedor.GetService<PesajeFaeneo.Application.Utilitarios.Backup.IAplicadorRestauracion>());
            Assert.NotNull(proveedor.GetService<PesajeFaeneo.Application.Utilitarios.Backup.IAlmacenConfiguracionBackup>());
            Assert.NotNull(proveedor.GetService<PesajeFaeneo.Application.Utilitarios.Backup.IServicioBackup>());
            var prog1 = proveedor.GetService<PesajeFaeneo.Application.Utilitarios.Backup.IProgramadorBackupAutomatico>();
            var prog2 = proveedor.GetService<PesajeFaeneo.Application.Utilitarios.Backup.IProgramadorBackupAutomatico>();
            Assert.NotNull(prog1);
            Assert.Same(prog1, prog2);

            Assert.NotNull(proveedor.GetService<PesajeFaeneo.Application.Utilitarios.Importacion.ILectorOrigenDatos>());
            Assert.NotNull(proveedor.GetService<PesajeFaeneo.Application.Utilitarios.Importacion.IRepositorioImportacion>());
            Assert.NotNull(proveedor.GetService<PesajeFaeneo.Application.Utilitarios.Importacion.IServicioImportacion>());

            Assert.NotNull(proveedor.GetService<PesajeFaeneo.Application.Utilitarios.Logs.ILectorArchivosLog>());
            Assert.NotNull(proveedor.GetService<PesajeFaeneo.Application.Utilitarios.Logs.IServicioLogs>());

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

                // F3: Formulario de configuración de balanza
                using var frmConfigBalanza = formFactory.Crear<FrmConfiguracionBalanza>();
                Assert.NotNull(frmConfigBalanza);

                // F4: Formulario de registro de pesaje
                using var frmPesaje = formFactory.Crear<FrmPesaje>();
                Assert.NotNull(frmPesaje);

                // F5: Formularios de reimpresión y configuración de impresión
                using var frmReimprimir = formFactory.Crear<FrmReimprimirBoleta>();
                Assert.NotNull(frmReimprimir);

                using var frmConfigImpresion = formFactory.Crear<FrmConfiguracionImpresion>();
                Assert.NotNull(frmConfigImpresion);

                // F6: Formularios de gestión de boletas y visor de auditoría
                using var frmBuscarBoletas = formFactory.Crear<FrmBuscarBoletas>();
                Assert.NotNull(frmBuscarBoletas);

                using var frmEditarBoleta = formFactory.Crear<FrmEditarBoleta>();
                Assert.NotNull(frmEditarBoleta);

                using var frmAnularBoleta = formFactory.Crear<FrmAnularBoleta>();
                Assert.NotNull(frmAnularBoleta);

                using var frmVisorAuditoria = formFactory.Crear<FrmVisorAuditoria>();
                Assert.NotNull(frmVisorAuditoria);

                // F7: Formulario de reportes
                using var frmReportes = formFactory.Crear<PesajeFaeneo.WinForms.Formularios.Reportes.FrmReportes>();
                Assert.NotNull(frmReportes);

                // F8: Formularios de utilitarios
                using var frmBackupRestaurar = formFactory.Crear<FrmBackupRestaurar>();
                Assert.NotNull(frmBackupRestaurar);

                using var frmImportarLegado = formFactory.Crear<FrmImportarLegado>();
                Assert.NotNull(frmImportarLegado);

                using var frmVisorLogs = formFactory.Crear<FrmVisorLogs>();
                Assert.NotNull(frmVisorLogs);
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
