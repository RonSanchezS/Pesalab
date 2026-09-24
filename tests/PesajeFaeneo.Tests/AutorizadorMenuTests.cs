using PesajeFaeneo.Application.Seguridad;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Tests.Utilidades;
using PesajeFaeneo.WinForms.Infraestructura;
using Xunit;

namespace PesajeFaeneo.Tests;

public class AutorizadorMenuTests
{
    private MenuStrip ConstruirMenuCompleto()
    {
        var menu = new MenuStrip();

        // Pesaje (Pesar: Operador/Supervisor/Admin)
        var mnuPesaje = new ToolStripMenuItem("Pesaje");
        var itemPesar = new ToolStripMenuItem("Pesar") { Tag = new OpcionMenu(Permiso.Pesar, "F4") };
        mnuPesaje.DropDownItems.Add(itemPesar);

        // Boletas
        var mnuBoletas = new ToolStripMenuItem("Boletas");
        var itemReimprimir = new ToolStripMenuItem("Reimprimir") { Tag = new OpcionMenu(Permiso.ReimprimirBoleta, "F5") };
        var itemEditar = new ToolStripMenuItem("Editar") { Tag = new OpcionMenu(Permiso.EditarBoleta, "F6") };
        mnuBoletas.DropDownItems.Add(itemReimprimir);
        mnuBoletas.DropDownItems.Add(itemEditar);

        // Catalogos
        var mnuCatalogos = new ToolStripMenuItem("Catálogos");
        var itemGanaderos = new ToolStripMenuItem("Ganaderos") { Tag = new OpcionMenu(Permiso.GestionarCatalogos, "F2") };
        mnuCatalogos.DropDownItems.Add(itemGanaderos);

        // Administracion
        var mnuAdmin = new ToolStripMenuItem("Administración");
        var itemUsuarios = new ToolStripMenuItem("Usuarios") { Tag = new OpcionMenu(Permiso.GestionarUsuarios, null) };
        mnuAdmin.DropDownItems.Add(itemUsuarios);

        menu.Items.Add(mnuPesaje);
        menu.Items.Add(mnuBoletas);
        menu.Items.Add(mnuCatalogos);
        menu.Items.Add(mnuAdmin);

        return menu;
    }

    [Fact]
    public void Aplicar_Operador_VePesajeYReimprimir_NoVeAdministracionNiCatalogos()
    {
        HiloSta.Ejecutar(() =>
        {
            using var menu = ConstruirMenuCompleto();

            AutorizadorMenu.Aplicar(menu.Items, p => MatrizPermisos.Tiene(Rol.Operador, p));

            var mnuPesaje = (ToolStripMenuItem)menu.Items[0];
            var mnuBoletas = (ToolStripMenuItem)menu.Items[1];
            var mnuCatalogos = (ToolStripMenuItem)menu.Items[2];
            var mnuAdmin = (ToolStripMenuItem)menu.Items[3];

            Assert.True(mnuPesaje.Available);
            Assert.True(mnuBoletas.Available);
            Assert.True(mnuBoletas.DropDownItems[0].Available); // Reimprimir
            Assert.False(mnuBoletas.DropDownItems[1].Available); // Editar
            Assert.False(mnuCatalogos.Available); // Oculto porque sus hijos están ocultos
            Assert.False(mnuAdmin.Available);     // Oculto porque Usuarios no está permitido
        });
    }

    [Fact]
    public void Aplicar_Administrador_VeTodo()
    {
        HiloSta.Ejecutar(() =>
        {
            using var menu = ConstruirMenuCompleto();

            AutorizadorMenu.Aplicar(menu.Items, p => MatrizPermisos.Tiene(Rol.Administrador, p));

            foreach (ToolStripMenuItem item in menu.Items)
            {
                Assert.True(item.Available);
                foreach (ToolStripItem sub in item.DropDownItems)
                {
                    Assert.True(sub.Available);
                }
            }
        });
    }

    [Fact]
    public void Aplicar_MenuPadreConTodosLosHijosOcultos_QuedaOculto()
    {
        HiloSta.Ejecutar(() =>
        {
            using var menu = new MenuStrip();
            var mnuPadre = new ToolStripMenuItem("Padre");
            mnuPadre.DropDownItems.Add(new ToolStripMenuItem("Hijo1") { Tag = new OpcionMenu(Permiso.GestionarUsuarios) });
            mnuPadre.DropDownItems.Add(new ToolStripMenuItem("Hijo2") { Tag = new OpcionMenu(Permiso.ConfigurarBalanza) });
            menu.Items.Add(mnuPadre);

            // Operador no tiene ni GestionarUsuarios ni ConfigurarBalanza
            AutorizadorMenu.Aplicar(menu.Items, p => MatrizPermisos.Tiene(Rol.Operador, p));

            Assert.False(mnuPadre.Available);
        });
    }

    [Fact]
    public void Aplicar_MenuPadreConSeparadorYTodosLosHijosOcultos_QuedaOculto()
    {
        HiloSta.Ejecutar(() =>
        {
            using var menu = new MenuStrip();
            var mnuPadre = new ToolStripMenuItem("Padre");
            mnuPadre.DropDownItems.Add(new ToolStripMenuItem("Hijo1") { Tag = new OpcionMenu(Permiso.GestionarUsuarios) });
            mnuPadre.DropDownItems.Add(new ToolStripSeparator());
            mnuPadre.DropDownItems.Add(new ToolStripMenuItem("Hijo2") { Tag = new OpcionMenu(Permiso.ConfigurarBalanza) });
            menu.Items.Add(mnuPadre);

            // Operador no tiene ni GestionarUsuarios ni ConfigurarBalanza; el separador
            // (Available = true por defecto) no debe mantener visible al padre.
            AutorizadorMenu.Aplicar(menu.Items, p => MatrizPermisos.Tiene(Rol.Operador, p));

            Assert.False(mnuPadre.Available);
        });
    }

    [Fact]
    public void Aplicar_ItemConFaseFutura_QuedaDeshabilitadoConToolTip()
    {
        HiloSta.Ejecutar(() =>
        {
            using var menu = new MenuStrip();
            var mnuPadre = new ToolStripMenuItem("Menu");
            var itemFuturo = new ToolStripMenuItem("Pesaje F4") { Tag = new OpcionMenu(Permiso.Pesar, "F4") };
            mnuPadre.DropDownItems.Add(itemFuturo);
            menu.Items.Add(mnuPadre);

            AutorizadorMenu.Aplicar(menu.Items, _ => true);

            Assert.True(itemFuturo.Available);
            Assert.False(itemFuturo.Enabled);
            Assert.Equal("Disponible en la fase F4", itemFuturo.ToolTipText);
        });
    }
}
