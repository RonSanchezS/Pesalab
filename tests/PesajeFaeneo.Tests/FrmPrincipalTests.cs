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

    [Fact]
    public void FrmPrincipal_ConSesionOperador_ItemAdministracionUsuariosQuedaNoDisponible()
    {
        HiloSta.Ejecutar(() =>
        {
            var sesion = new SesionActual();
            sesion.Iniciar(new UsuarioSesion(1, "operador", "Operador Uno", Rol.Operador, false));
            var formFactory = new FalsaFormFactory();
            var tiempo = TimeProvider.System;

            using var principal = new FrmPrincipal(sesion, formFactory, tiempo);

            // Buscar el menú Administración
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
            // El menú Administración no debe estar disponible para un Operador
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

            using var principal = new FrmPrincipal(sesion, formFactory, tiempo);

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
}
