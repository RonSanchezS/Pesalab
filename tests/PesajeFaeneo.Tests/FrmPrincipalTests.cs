using PesajeFaeneo.Application.Balanza;
using PesajeFaeneo.Application.Parametros;
using PesajeFaeneo.Application.Parametros.Dtos;
using PesajeFaeneo.Application.Seguridad;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Tests.Utilidades;
using PesajeFaeneo.Tests.Utilidades.Balanza;
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
            var balanza = new ServicioBalanzaFalso();

            using var principal = new FrmPrincipal(sesion, formFactory, tiempo, paramService, balanza);

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
            var balanza = new ServicioBalanzaFalso();

            using var principal = new FrmPrincipal(sesion, formFactory, tiempo, paramService, balanza);

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
            var balanza = new ServicioBalanzaFalso();

            using var principal = new FrmPrincipal(sesion, formFactory, tiempo, paramService, balanza);

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
            var balanza = new ServicioBalanzaFalso();

            using var principal = new FrmPrincipal(sesion, formFactory, tiempo, paramService, balanza);

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
            var balanza = new ServicioBalanzaFalso();

            using var principal = new FrmPrincipal(sesion, formFactory, tiempo, paramService, balanza);

            principal.ActualizarEmpresaParaTestAsync().GetAwaiter().GetResult();

            Assert.Equal("MATADERO CENTRAL", principal.LblStatusEmpresa.Text);
        });
    }

    [Fact]
    public void FrmPrincipal_ConectadaConPeso_ActualizaLblStatusBalanza()
    {
        HiloSta.Ejecutar(() =>
        {
            var sesion = new SesionActual();
            sesion.Iniciar(new UsuarioSesion(1, "admin", "Administrador Uno", Rol.Administrador, false));
            var formFactory = new FalsaFormFactory();
            var tiempo = TimeProvider.System;
            var paramService = new FalsoServicioParametros();
            var balanza = new ServicioBalanzaFalso();

            using var principal = new FrmPrincipal(sesion, formFactory, tiempo, paramService, balanza);

            balanza.Instantanea = new PesajeFaeneo.Application.Balanza.Dtos.InstantaneaBalanza(
                1, EstadoBalanza.Conectada, 125.5m, true, DateTimeOffset.UtcNow, null, "COM1", false);

            principal.MonitorBalanza.Sondear();

            Assert.Contains("Balanza: conectada", principal.LblStatusBalanza.Text);
            Assert.Contains("125", principal.LblStatusBalanza.Text);
            Assert.Equal(PresentacionBalanza.ColorVerdeOscuro, principal.LblStatusBalanza.ForeColor);
        });
    }

    [Fact]
    public void FrmPrincipal_ConSimulador_MuestraPrefijoSimuladorYFondoNaranja()
    {
        HiloSta.Ejecutar(() =>
        {
            var sesion = new SesionActual();
            sesion.Iniciar(new UsuarioSesion(1, "admin", "Administrador Uno", Rol.Administrador, false));
            var formFactory = new FalsaFormFactory();
            var tiempo = TimeProvider.System;
            var paramService = new FalsoServicioParametros();
            var balanza = new ServicioBalanzaFalso();

            using var principal = new FrmPrincipal(sesion, formFactory, tiempo, paramService, balanza);

            balanza.Instantanea = new PesajeFaeneo.Application.Balanza.Dtos.InstantaneaBalanza(
                1, EstadoBalanza.Conectada, 100m, true, DateTimeOffset.UtcNow, null, "Simulador", true);

            principal.MonitorBalanza.Sondear();

            Assert.StartsWith("[SIMULADOR]", principal.LblStatusBalanza.Text);
            Assert.Equal(PresentacionBalanza.ColorFondoSimulador, principal.LblStatusBalanza.BackColor);
        });
    }

    [Fact]
    public void FrmPrincipal_ItemBalanza_VisibleSoloParaAdministrador()
    {
        HiloSta.Ejecutar(() =>
        {
            var formFactory = new FalsaFormFactory();
            var tiempo = TimeProvider.System;
            var paramService = new FalsoServicioParametros();
            var balanza = new ServicioBalanzaFalso();

            // 1. Con Administrador: disponible y habilitado
            var sesionAdmin = new SesionActual();
            sesionAdmin.Iniciar(new UsuarioSesion(1, "admin", "Admin", Rol.Administrador, false));
            using var principalAdmin = new FrmPrincipal(sesionAdmin, formFactory, tiempo, paramService, balanza);

            ToolStripMenuItem? mnuAdmin = null;
            foreach (ToolStripMenuItem item in principalAdmin.MenuPrincipal.Items)
            {
                if (item.Text?.Contains("Administración") == true) mnuAdmin = item;
            }
            Assert.NotNull(mnuAdmin);
            Assert.True(mnuAdmin.Available);

            ToolStripMenuItem? itemBalanza = null;
            foreach (ToolStripItem sub in mnuAdmin.DropDownItems)
            {
                if (sub.Text?.Contains("balanza", StringComparison.OrdinalIgnoreCase) == true)
                {
                    itemBalanza = (ToolStripMenuItem)sub;
                    break;
                }
            }
            Assert.NotNull(itemBalanza);
            Assert.True(itemBalanza.Available);
            Assert.True(itemBalanza.Enabled);

            // 2. Con Supervisor: no disponible
            var sesionSupervisor = new SesionActual();
            sesionSupervisor.Iniciar(new UsuarioSesion(2, "supervisor", "Supervisor", Rol.Supervisor, false));
            using var principalSupervisor = new FrmPrincipal(sesionSupervisor, formFactory, tiempo, paramService, balanza);

            ToolStripMenuItem? mnuAdminSup = null;
            foreach (ToolStripMenuItem item in principalSupervisor.MenuPrincipal.Items)
            {
                if (item.Text?.Contains("Administración") == true) mnuAdminSup = item;
            }
            if (mnuAdminSup != null && mnuAdminSup.Available)
            {
                ToolStripMenuItem? itemBalanzaSup = null;
                foreach (ToolStripItem sub in mnuAdminSup.DropDownItems)
                {
                    if (sub.Text?.Contains("balanza", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        itemBalanzaSup = (ToolStripMenuItem)sub;
                        break;
                    }
                }
                if (itemBalanzaSup != null)
                {
                    Assert.False(itemBalanzaSup.Available);
                }
            }

            // 3. Con Operador: menú Administración no disponible
            var sesionOperador = new SesionActual();
            sesionOperador.Iniciar(new UsuarioSesion(3, "operador", "Operador", Rol.Operador, false));
            using var principalOperador = new FrmPrincipal(sesionOperador, formFactory, tiempo, paramService, balanza);

            ToolStripMenuItem? mnuAdminOp = null;
            foreach (ToolStripMenuItem item in principalOperador.MenuPrincipal.Items)
            {
                if (item.Text?.Contains("Administración") == true) mnuAdminOp = item;
            }
            Assert.NotNull(mnuAdminOp);
            Assert.False(mnuAdminOp.Available);
        });
    }

    [Theory]
    [InlineData(Rol.Operador)]
    [InlineData(Rol.Supervisor)]
    [InlineData(Rol.Administrador)]
    public void FrmPrincipal_ItemRegistrarPesaje_DisponibleYHabilitadoParaTodosLosRoles(Rol rol)
    {
        HiloSta.Ejecutar(() =>
        {
            var sesion = new SesionActual();
            sesion.Iniciar(new UsuarioSesion(1, "usuario", "Usuario", rol, false));
            var formFactory = new FalsaFormFactory();
            var tiempo = TimeProvider.System;
            var paramService = new FalsoServicioParametros();
            var balanza = new ServicioBalanzaFalso();

            using var principal = new FrmPrincipal(sesion, formFactory, tiempo, paramService, balanza);

            ToolStripMenuItem? mnuPesaje = null;
            foreach (ToolStripMenuItem item in principal.MenuPrincipal.Items)
            {
                if (item.Text?.Contains("Pesaje") == true)
                {
                    mnuPesaje = item;
                    break;
                }
            }
            Assert.NotNull(mnuPesaje);
            Assert.True(mnuPesaje.Available);

            ToolStripMenuItem? itemRegistrarPesaje = null;
            foreach (ToolStripItem sub in mnuPesaje.DropDownItems)
            {
                if (sub.Text?.Contains("Registrar pesaje") == true)
                {
                    itemRegistrarPesaje = (ToolStripMenuItem)sub;
                    break;
                }
            }
            Assert.NotNull(itemRegistrarPesaje);
            Assert.True(itemRegistrarPesaje.Available);
            Assert.True(itemRegistrarPesaje.Enabled);
        });
    }

    [Theory]
    [InlineData(Rol.Operador)]
    [InlineData(Rol.Supervisor)]
    [InlineData(Rol.Administrador)]
    public void FrmPrincipal_ItemReimprimirBoleta_DisponibleYHabilitadoParaTodosLosRoles_ConF3(Rol rol)
    {
        HiloSta.Ejecutar(() =>
        {
            var sesion = new SesionActual();
            sesion.Iniciar(new UsuarioSesion(1, "usuario", "Usuario", rol, false));
            var formFactory = new FalsaFormFactory();
            var tiempo = TimeProvider.System;
            var paramService = new FalsoServicioParametros();
            var balanza = new ServicioBalanzaFalso();

            using var principal = new FrmPrincipal(sesion, formFactory, tiempo, paramService, balanza);

            ToolStripMenuItem? mnuBoletas = null;
            foreach (ToolStripMenuItem item in principal.MenuPrincipal.Items)
            {
                if (item.Text?.Contains("Boletas") == true)
                {
                    mnuBoletas = item;
                    break;
                }
            }
            Assert.NotNull(mnuBoletas);
            Assert.True(mnuBoletas.Available);

            ToolStripMenuItem? itemReimprimir = null;
            foreach (ToolStripItem sub in mnuBoletas.DropDownItems)
            {
                if (sub.Text?.Contains("Reimprimir boleta") == true)
                {
                    itemReimprimir = (ToolStripMenuItem)sub;
                    break;
                }
            }
            Assert.NotNull(itemReimprimir);
            Assert.True(itemReimprimir.Available);
            Assert.True(itemReimprimir.Enabled);
            Assert.Equal(Keys.F3, itemReimprimir.ShortcutKeys);
        });
    }

    [Fact]
    public void FrmPrincipal_ItemConfiguracionImpresion_VisibleSoloParaAdministrador()
    {
        HiloSta.Ejecutar(() =>
        {
            var formFactory = new FalsaFormFactory();
            var tiempo = TimeProvider.System;
            var paramService = new FalsoServicioParametros();
            var balanza = new ServicioBalanzaFalso();

            // 1. Con Administrador: disponible y habilitado
            var sesionAdmin = new SesionActual();
            sesionAdmin.Iniciar(new UsuarioSesion(1, "admin", "Admin", Rol.Administrador, false));
            using var principalAdmin = new FrmPrincipal(sesionAdmin, formFactory, tiempo, paramService, balanza);

            ToolStripMenuItem? mnuAdmin = null;
            foreach (ToolStripMenuItem item in principalAdmin.MenuPrincipal.Items)
            {
                if (item.Text?.Contains("Administración") == true) mnuAdmin = item;
            }
            Assert.NotNull(mnuAdmin);
            Assert.True(mnuAdmin.Available);

            ToolStripMenuItem? itemImpresion = null;
            foreach (ToolStripItem sub in mnuAdmin.DropDownItems)
            {
                if (sub.Text?.Contains("impresión", StringComparison.OrdinalIgnoreCase) == true)
                {
                    itemImpresion = (ToolStripMenuItem)sub;
                    break;
                }
            }
            Assert.NotNull(itemImpresion);
            Assert.True(itemImpresion.Available);
            Assert.True(itemImpresion.Enabled);

            // 2. Con Supervisor: no disponible
            var sesionSupervisor = new SesionActual();
            sesionSupervisor.Iniciar(new UsuarioSesion(2, "supervisor", "Supervisor", Rol.Supervisor, false));
            using var principalSupervisor = new FrmPrincipal(sesionSupervisor, formFactory, tiempo, paramService, balanza);

            ToolStripMenuItem? mnuAdminSup = null;
            foreach (ToolStripMenuItem item in principalSupervisor.MenuPrincipal.Items)
            {
                if (item.Text?.Contains("Administración") == true) mnuAdminSup = item;
            }
            if (mnuAdminSup != null && mnuAdminSup.Available)
            {
                ToolStripMenuItem? itemImpresionSup = null;
                foreach (ToolStripItem sub in mnuAdminSup.DropDownItems)
                {
                    if (sub.Text?.Contains("impresión", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        itemImpresionSup = (ToolStripMenuItem)sub;
                        break;
                    }
                }
                if (itemImpresionSup != null)
                {
                    Assert.False(itemImpresionSup.Available);
                }
            }

            // 3. Con Operador: menú Administración no disponible
            var sesionOperador = new SesionActual();
            sesionOperador.Iniciar(new UsuarioSesion(3, "operador", "Operador", Rol.Operador, false));
            using var principalOperador = new FrmPrincipal(sesionOperador, formFactory, tiempo, paramService, balanza);

            ToolStripMenuItem? mnuAdminOp = null;
            foreach (ToolStripMenuItem item in principalOperador.MenuPrincipal.Items)
            {
                if (item.Text?.Contains("Administración") == true) mnuAdminOp = item;
            }
            Assert.NotNull(mnuAdminOp);
            Assert.False(mnuAdminOp.Available);
        });
    }

    [Fact]
    public void FrmPrincipal_GestionBoletasYAuditoria_DisponiblesYHabilitadosParaSupervisorYAdministrador_NoDisponiblesParaOperador()
    {
        HiloSta.Ejecutar(() =>
        {
            var formFactory = new FalsaFormFactory();
            var tiempo = TimeProvider.System;
            var paramService = new FalsoServicioParametros();
            var balanza = new ServicioBalanzaFalso();

            // 1. Administrador: disponibles, habilitados y con atajo F6
            var sesionAdmin = new SesionActual();
            sesionAdmin.Iniciar(new UsuarioSesion(1, "admin", "Admin", Rol.Administrador, false));
            using (var principal = new FrmPrincipal(sesionAdmin, formFactory, tiempo, paramService, balanza))
            {
                var (itemBuscar, itemAnular, itemAuditoria) = ObtenerItemsF6(principal);
                Assert.NotNull(itemBuscar);
                Assert.True(itemBuscar.Available);
                Assert.True(itemBuscar.Enabled);
                Assert.Equal(Keys.F6, itemBuscar.ShortcutKeys);

                Assert.NotNull(itemAnular);
                Assert.True(itemAnular.Available);
                Assert.True(itemAnular.Enabled);
                Assert.Equal(Keys.None, itemAnular.ShortcutKeys);

                Assert.NotNull(itemAuditoria);
                Assert.True(itemAuditoria.Available);
                Assert.True(itemAuditoria.Enabled);
            }

            // 2. Supervisor: disponibles, habilitados y con atajo F6
            var sesionSupervisor = new SesionActual();
            sesionSupervisor.Iniciar(new UsuarioSesion(2, "supervisor", "Supervisor", Rol.Supervisor, false));
            using (var principal = new FrmPrincipal(sesionSupervisor, formFactory, tiempo, paramService, balanza))
            {
                var (itemBuscar, itemAnular, itemAuditoria) = ObtenerItemsF6(principal);
                Assert.NotNull(itemBuscar);
                Assert.True(itemBuscar.Available);
                Assert.True(itemBuscar.Enabled);
                Assert.Equal(Keys.F6, itemBuscar.ShortcutKeys);

                Assert.NotNull(itemAnular);
                Assert.True(itemAnular.Available);
                Assert.True(itemAnular.Enabled);

                Assert.NotNull(itemAuditoria);
                Assert.True(itemAuditoria.Available);
                Assert.True(itemAuditoria.Enabled);
            }

            // 3. Operador: no disponibles
            var sesionOperador = new SesionActual();
            sesionOperador.Iniciar(new UsuarioSesion(3, "operador", "Operador", Rol.Operador, false));
            using (var principal = new FrmPrincipal(sesionOperador, formFactory, tiempo, paramService, balanza))
            {
                var (itemBuscar, itemAnular, itemAuditoria) = ObtenerItemsF6(principal);
                Assert.NotNull(itemBuscar);
                Assert.False(itemBuscar.Available);

                Assert.NotNull(itemAnular);
                Assert.False(itemAnular.Available);

                // Auditoría no está disponible (o todo el menú Administración no está disponible)
                if (itemAuditoria != null)
                {
                    Assert.False(itemAuditoria.Available);
                }
            }
        });
    }

    [Fact]
    public void FrmPrincipal_Utilitarios_HabilitadosParaAdministrador()
    {
        HiloSta.Ejecutar(() =>
        {
            var formFactory = new FalsaFormFactory();
            var tiempo = TimeProvider.System;
            var paramService = new FalsoServicioParametros();
            var balanza = new ServicioBalanzaFalso();

            var sesionAdmin = new SesionActual();
            sesionAdmin.Iniciar(new UsuarioSesion(1, "admin", "Admin", Rol.Administrador, false));
            using var principal = new FrmPrincipal(sesionAdmin, formFactory, tiempo, paramService, balanza);

            ToolStripMenuItem? mnuUtilitarios = null;
            foreach (ToolStripMenuItem item in principal.MenuPrincipal.Items)
            {
                if (item.Text?.Contains("Utilitarios") == true) mnuUtilitarios = item;
            }

            Assert.NotNull(mnuUtilitarios);
            Assert.True(mnuUtilitarios.Available);
            Assert.Equal(3, mnuUtilitarios.DropDownItems.Count);
            foreach (ToolStripItem sub in mnuUtilitarios.DropDownItems)
            {
                Assert.True(sub.Available);
                Assert.True(sub.Enabled);
                Assert.True(string.IsNullOrEmpty(sub.ToolTipText));
            }
        });
    }

    [Theory]
    [InlineData(Rol.Supervisor)]
    [InlineData(Rol.Operador)]
    public void FrmPrincipal_Utilitarios_NoDisponiblesParaSupervisorYOperador(Rol rol)
    {
        HiloSta.Ejecutar(() =>
        {
            var formFactory = new FalsaFormFactory();
            var tiempo = TimeProvider.System;
            var paramService = new FalsoServicioParametros();
            var balanza = new ServicioBalanzaFalso();

            var sesion = new SesionActual();
            sesion.Iniciar(new UsuarioSesion(2, "usuario", "Usuario", rol, false));
            using var principal = new FrmPrincipal(sesion, formFactory, tiempo, paramService, balanza);

            ToolStripMenuItem? mnuUtilitarios = null;
            foreach (ToolStripMenuItem item in principal.MenuPrincipal.Items)
            {
                if (item.Text?.Contains("Utilitarios") == true) mnuUtilitarios = item;
            }

            Assert.NotNull(mnuUtilitarios);
            Assert.False(mnuUtilitarios.Available);
        });
    }

    [Fact]
    public void FrmPrincipal_Reportes_DisponiblesYHabilitadosParaSupervisorYAdministrador_NoDisponiblesParaOperador()
    {
        HiloSta.Ejecutar(() =>
        {
            var formFactory = new FalsaFormFactory();
            var tiempo = TimeProvider.System;
            var paramService = new FalsoServicioParametros();
            var balanza = new ServicioBalanzaFalso();

            (ToolStripMenuItem? Pesajes, ToolStripMenuItem? Anuladas, ToolStripMenuItem? Menu) ObtenerItemsReportes(FrmPrincipal p)
            {
                ToolStripMenuItem? mnuRep = null;
                ToolStripMenuItem? itmPes = null;
                ToolStripMenuItem? itmAnu = null;
                foreach (ToolStripMenuItem item in p.MenuPrincipal.Items)
                {
                    if (item.Text?.Contains("Reportes") == true)
                    {
                        mnuRep = item;
                        foreach (ToolStripItem sub in item.DropDownItems)
                        {
                            if (sub.Text?.Contains("Reportes de pesaje") == true) itmPes = (ToolStripMenuItem)sub;
                            if (sub.Text?.Contains("anuladas") == true) itmAnu = (ToolStripMenuItem)sub;
                        }
                    }
                }
                return (itmPes, itmAnu, mnuRep);
            }

            // 1. Administrador: disponibles, habilitados y sin tooltip de fase futura
            var sesionAdmin = new SesionActual();
            sesionAdmin.Iniciar(new UsuarioSesion(1, "admin", "Admin", Rol.Administrador, false));
            using (var principal = new FrmPrincipal(sesionAdmin, formFactory, tiempo, paramService, balanza))
            {
                var (itemPesajes, itemAnuladas, mnuReportes) = ObtenerItemsReportes(principal);
                Assert.NotNull(mnuReportes);
                Assert.True(mnuReportes.Available);

                Assert.NotNull(itemPesajes);
                Assert.True(itemPesajes.Available);
                Assert.True(itemPesajes.Enabled);
                Assert.Empty(itemPesajes.ToolTipText ?? "");

                Assert.NotNull(itemAnuladas);
                Assert.True(itemAnuladas.Available);
                Assert.True(itemAnuladas.Enabled);
                Assert.Empty(itemAnuladas.ToolTipText ?? "");
            }

            // 2. Supervisor: disponibles y habilitados
            var sesionSup = new SesionActual();
            sesionSup.Iniciar(new UsuarioSesion(2, "sup", "Supervisor", Rol.Supervisor, false));
            using (var principal = new FrmPrincipal(sesionSup, formFactory, tiempo, paramService, balanza))
            {
                var (itemPesajes, itemAnuladas, mnuReportes) = ObtenerItemsReportes(principal);
                Assert.NotNull(mnuReportes);
                Assert.True(mnuReportes.Available);

                Assert.NotNull(itemPesajes);
                Assert.True(itemPesajes.Available);
                Assert.True(itemPesajes.Enabled);

                Assert.NotNull(itemAnuladas);
                Assert.True(itemAnuladas.Available);
                Assert.True(itemAnuladas.Enabled);
            }

            // 3. Operador: menú Reportes no disponible
            var sesionOp = new SesionActual();
            sesionOp.Iniciar(new UsuarioSesion(3, "op", "Operador", Rol.Operador, false));
            using (var principal = new FrmPrincipal(sesionOp, formFactory, tiempo, paramService, balanza))
            {
                var (_, _, mnuReportes) = ObtenerItemsReportes(principal);
                Assert.NotNull(mnuReportes);
                Assert.False(mnuReportes.Available);
            }
        });
    }

    private static (ToolStripMenuItem? BuscarEditar, ToolStripMenuItem? Anular, ToolStripMenuItem? Auditoria)
        ObtenerItemsF6(FrmPrincipal principal)
    {
        ToolStripMenuItem? itemBuscar = null;
        ToolStripMenuItem? itemAnular = null;
        ToolStripMenuItem? itemAuditoria = null;

        foreach (ToolStripMenuItem top in principal.MenuPrincipal.Items)
        {
            if (top.Text?.Contains("Boletas") == true)
            {
                foreach (ToolStripItem sub in top.DropDownItems)
                {
                    if (sub.Text?.Contains("Buscar / editar", StringComparison.OrdinalIgnoreCase) == true)
                        itemBuscar = sub as ToolStripMenuItem;
                    if (sub.Text?.Contains("Anular", StringComparison.OrdinalIgnoreCase) == true)
                        itemAnular = sub as ToolStripMenuItem;
                }
            }
            if (top.Text?.Contains("Administración") == true)
            {
                foreach (ToolStripItem sub in top.DropDownItems)
                {
                    if (sub.Text?.Contains("auditoría", StringComparison.OrdinalIgnoreCase) == true)
                        itemAuditoria = sub as ToolStripMenuItem;
                }
            }
        }

        return (itemBuscar, itemAnular, itemAuditoria);
    }

    [Theory]
    [InlineData(Rol.Operador)]
    [InlineData(Rol.Supervisor)]
    [InlineData(Rol.Administrador)]
    public void FrmPrincipal_MenuAyuda_AcercaDe_VisibleParaTodosLosRoles(Rol rol)
    {
        HiloSta.Ejecutar(() =>
        {
            var sesion = new SesionActual();
            sesion.Iniciar(new UsuarioSesion(1, "usuario", "Nombre", rol, false));
            var formFactory = new FalsaFormFactory();
            var tiempo = TimeProvider.System;
            var paramService = new FalsoServicioParametros();
            var balanza = new ServicioBalanzaFalso();

            using var principal = new FrmPrincipal(sesion, formFactory, tiempo, paramService, balanza);

            ToolStripMenuItem? mnuAyuda = null;
            ToolStripMenuItem? itemAcercaDe = null;

            foreach (ToolStripMenuItem top in principal.MenuPrincipal.Items)
            {
                if (top.Text?.Replace("&", "").Contains("Ayuda", StringComparison.OrdinalIgnoreCase) == true)
                {
                    mnuAyuda = top;
                    foreach (ToolStripItem sub in top.DropDownItems)
                    {
                        if (sub.Text?.Replace("&", "").Contains("Acerca de", StringComparison.OrdinalIgnoreCase) == true)
                        {
                            itemAcercaDe = sub as ToolStripMenuItem;
                        }
                    }
                }
            }

            Assert.NotNull(mnuAyuda);
            Assert.True(mnuAyuda.Available);
            Assert.NotNull(itemAcercaDe);
            Assert.True(itemAcercaDe.Available);
        });
    }
}
