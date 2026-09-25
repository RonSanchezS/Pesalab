using PesajeFaeneo.Application.Parametros;
using PesajeFaeneo.Application.Parametros.Dtos;
using PesajeFaeneo.Application.Seguridad;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Tests.Utilidades;
using PesajeFaeneo.WinForms.Formularios;
using PesajeFaeneo.WinForms.Infraestructura;
using Xunit;

namespace PesajeFaeneo.Tests;

public class FrmPrincipalTests
{
    private class FalsaFormFactory : IFormFactory
    {
        public T Crear<T>() where T : Form => throw new NotImplementedException();
    }

    private class FalsoServicioParametros : IServicioParametros
    {
        public ParametrosDto Dto { get; set; } = new(
            NombreEmpresa: "MATADERO CENTRAL",
            Direccion: "AV PRINCIPAL 100",
            Telefono: "0212-1234567",
            Ciudad: "CARACAS",
            RegistroSanitario: "RS-999",
            TelefonosReclamos: "0800-CARNE",
            ObservacionPorDefecto: "OBS PREDETERMINADA",
            SiguienteNumeroBoleta: 10,
            UltimoNumeroBoletaEmitido: 9,
            LogoArchivo: null,
            RutaLogoCompleta: null,
            ModificadoEn: DateTime.UtcNow);

        public Task<ParametrosDto> ObtenerAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Dto);

        public Task ActualizarAsync(ActualizarParametros dto, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    [Fact]
    public void FrmPrincipal_ConSesionOperador_ItemAdministracionUsuariosQuedaNoDisponible()
    {
        HiloSta.Ejecutar(() =>
        {
            var sesion = new SesionActual();
            sesion.Iniciar(new UsuarioSesion(1, "operador", "Operador Uno", Rol.Operador, false));
            var formFactory = new FalsaFormFactory();
            var tiempo = TimeProvider.System;
            var paramService = new FalsoServicioParametros();

            using var principal = new FrmPrincipal(sesion, formFactory, tiempo, paramService);

            ToolStripMenuItem? mnuAdmin = null;
            foreach (ToolStripMenuItem item in principal.MenuPrincipal.Items)
            {
                if (item.Text?.Contains("Administración") == true)
                {
                    mnuAdmin = item;
                    break;
                }
            }

            Assert.NotNull(mnuAdmin);
            Assert.False(mnuAdmin.Available);
        });
    }

    [Fact]
    public void FrmPrincipal_ConSesionAdministrador_ItemAdministracionUsuariosQuedaDisponible()
    {
        HiloSta.Ejecutar(() =>
        {
            var sesion = new SesionActual();
            sesion.Iniciar(new UsuarioSesion(1, "admin", "Administrador Uno", Rol.Administrador, false));
            var formFactory = new FalsaFormFactory();
            var tiempo = TimeProvider.System;
            var paramService = new FalsoServicioParametros();

            using var principal = new FrmPrincipal(sesion, formFactory, tiempo, paramService);

            ToolStripMenuItem? mnuAdmin = null;
            foreach (ToolStripMenuItem item in principal.MenuPrincipal.Items)
            {
                if (item.Text?.Contains("Administración") == true)
                {
                    mnuAdmin = item;
                    break;
                }
            }

            Assert.NotNull(mnuAdmin);
            Assert.True(mnuAdmin.Available);

            ToolStripMenuItem? itemUsuarios = null;
            foreach (ToolStripItem sub in mnuAdmin.DropDownItems)
            {
                if (sub.Text?.Contains("Usuarios") == true)
                {
                    itemUsuarios = (ToolStripMenuItem)sub;
                    break;
                }
            }

            Assert.NotNull(itemUsuarios);
            Assert.True(itemUsuarios.Available);
        });
    }

    [Fact]
    public void FrmPrincipal_ConSesionSupervisor_CatalogosGanaderosDisponibleYHabilitado_ParametrosNoDisponible()
    {
        HiloSta.Ejecutar(() =>
        {
            var sesion = new SesionActual();
            sesion.Iniciar(new UsuarioSesion(2, "supervisor", "Supervisor Uno", Rol.Supervisor, false));
            var formFactory = new FalsaFormFactory();
            var tiempo = TimeProvider.System;
            var paramService = new FalsoServicioParametros();

            using var principal = new FrmPrincipal(sesion, formFactory, tiempo, paramService);

            ToolStripMenuItem? mnuCatalogos = null;
            ToolStripMenuItem? mnuAdmin = null;
            foreach (ToolStripMenuItem item in principal.MenuPrincipal.Items)
            {
                if (item.Text?.Contains("Catálogos") == true) mnuCatalogos = item;
                if (item.Text?.Contains("Administración") == true) mnuAdmin = item;
            }

            Assert.NotNull(mnuCatalogos);
            Assert.True(mnuCatalogos.Available);

            ToolStripMenuItem? itemGanaderos = null;
            foreach (ToolStripItem sub in mnuCatalogos.DropDownItems)
            {
                if (sub.Text?.Contains("Ganaderos") == true)
                {
                    itemGanaderos = (ToolStripMenuItem)sub;
                    break;
                }
            }

            Assert.NotNull(itemGanaderos);
            Assert.True(itemGanaderos.Available);
            Assert.True(itemGanaderos.Enabled);

            // Parámetros del sistema requiere GestionarParametros (Supervisor no lo tiene)
            if (mnuAdmin != null && mnuAdmin.Available)
            {
                ToolStripMenuItem? itemParametros = null;
                foreach (ToolStripItem sub in mnuAdmin.DropDownItems)
                {
                    if (sub.Text?.Contains("Parámetros") == true)
                    {
                        itemParametros = (ToolStripMenuItem)sub;
                        break;
                    }
                }

                if (itemParametros != null)
                {
                    Assert.False(itemParametros.Available);
                }
            }
        });
    }

    [Fact]
    public void FrmPrincipal_ConSesionAdministrador_ParametrosDisponibleYHabilitado()
    {
        HiloSta.Ejecutar(() =>
        {
            var sesion = new SesionActual();
            sesion.Iniciar(new UsuarioSesion(1, "admin", "Administrador Uno", Rol.Administrador, false));
            var formFactory = new FalsaFormFactory();
            var tiempo = TimeProvider.System;
            var paramService = new FalsoServicioParametros();

            using var principal = new FrmPrincipal(sesion, formFactory, tiempo, paramService);

            ToolStripMenuItem? mnuAdmin = null;
            foreach (ToolStripMenuItem item in principal.MenuPrincipal.Items)
            {
                if (item.Text?.Contains("Administración") == true)
                {
                    mnuAdmin = item;
                    break;
                }
            }

            Assert.NotNull(mnuAdmin);
            Assert.True(mnuAdmin.Available);

            ToolStripMenuItem? itemParametros = null;
            foreach (ToolStripItem sub in mnuAdmin.DropDownItems)
            {
                if (sub.Text?.Contains("Parámetros") == true)
                {
                    itemParametros = (ToolStripMenuItem)sub;
                    break;
                }
            }

            Assert.NotNull(itemParametros);
            Assert.True(itemParametros.Available);
            Assert.True(itemParametros.Enabled);
        });
    }

    [Fact]
    public void FrmPrincipal_ActualizarEmpresa_CargaNombreEnStatusStrip()
    {
        HiloSta.Ejecutar(() =>
        {
            var sesion = new SesionActual();
            sesion.Iniciar(new UsuarioSesion(1, "admin", "Administrador Uno", Rol.Administrador, false));
            var formFactory = new FalsaFormFactory();
            var tiempo = TimeProvider.System;
            var paramService = new FalsoServicioParametros();

            using var principal = new FrmPrincipal(sesion, formFactory, tiempo, paramService);

            principal.ActualizarEmpresaParaTestAsync().GetAwaiter().GetResult();

            Assert.Equal("MATADERO CENTRAL", principal.LblStatusEmpresa.Text);
        });
    }
}
