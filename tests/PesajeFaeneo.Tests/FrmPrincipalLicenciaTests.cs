using PesajeFaeneo.Application.Licencias;
using PesajeFaeneo.Application.Parametros;
using PesajeFaeneo.Application.Parametros.Dtos;
using PesajeFaeneo.Application.Seguridad;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Infrastructure.Common;
using PesajeFaeneo.Tests.Utilidades;
using PesajeFaeneo.Tests.Utilidades.Balanza;
using PesajeFaeneo.Tests.Utilidades.Licencias;
using PesajeFaeneo.Tests.Utilidades.Pesaje;
using PesajeFaeneo.WinForms.Formularios;
using PesajeFaeneo.WinForms.Formularios.Ayuda;
using PesajeFaeneo.WinForms.Formularios.Licencias;
using PesajeFaeneo.WinForms.Infraestructura;
using Xunit;

namespace PesajeFaeneo.Tests;

/// <summary>F13 (D4, D10): la barra de estado, el menú Ayuda y el bloqueo de pesaje e importación según la licencia.</summary>
public class FrmPrincipalLicenciaTests
{
    private const string TextoModoConsulta = "Modo consulta: active el sistema en Ayuda › Activar licencia.";

    private sealed class FabricaCancelable : IFormFactory
    {
        public List<Type> Solicitados { get; } = new();

        // Se anota el tipo pedido y se corta con una cancelación (sin log ni MessageBox, ver ClasificadorErrores).
        public T Crear<T>() where T : Form
        {
            Solicitados.Add(typeof(T));
            throw new OperationCanceledException();
        }
    }

    private static FrmPrincipal Crear(ServicioLicenciaFalso licencia, Rol rol = Rol.Operador, IFormFactory? fabrica = null)
    {
        var sesion = new SesionActual();
        sesion.Iniciar(new UsuarioSesion(1, "usuario", "Usuario", rol, false));
        return new FrmPrincipal(sesion, fabrica ?? new FormFactoryFalsa(), TimeProvider.System, new ServicioParametrosFalso(), new ServicioBalanzaFalso(), licencia);
    }

    private static ToolStripMenuItem Item(FrmPrincipal principal, string menu, string texto) =>
        principal.MenuPrincipal.Items.OfType<ToolStripMenuItem>().First(m => m.Text == menu)
            .DropDownItems.OfType<ToolStripMenuItem>().Single(i => i.Text == texto);

    [Fact]
    public void Activada_NoMuestraNadaDeLicencia_YTodoHabilitado()
    {
        HiloSta.Ejecutar(() =>
        {
            using var principal = Crear(new ServicioLicenciaFalso { Estado = EstadoLicencia.Activada }, Rol.Administrador);

            Assert.False(principal.LblStatusLicencia.Available);
            Assert.False(principal.ItemActivarLicencia.Available);
            Assert.True(principal.ItemRegistrarPesaje.Enabled);
            Assert.True(principal.TarjetaPesaje.Enabled);
            Assert.True(principal.ItemImportarLegado.Enabled);
        });
    }

    [Theory]
    [InlineData(15, "Prueba: 15 días")]
    [InlineData(2, "Prueba: 2 días")]
    [InlineData(1, "Prueba: 1 día")]
    public void EnPrueba_MuestraLosDiasEnNaranja_ConElEnlaceYLaActivacionEnElMenu(int dias, string texto)
    {
        HiloSta.Ejecutar(() =>
        {
            using var principal = Crear(new ServicioLicenciaFalso { Estado = EstadoLicencia.EnPrueba(dias) }, Rol.Administrador);

            Assert.True(principal.LblStatusLicencia.Available);
            Assert.True(principal.LblStatusLicencia.IsLink);
            Assert.Equal(texto, principal.LblStatusLicencia.Text);
            Assert.Equal(Color.DarkOrange, principal.LblStatusLicencia.ForeColor);
            Assert.Equal(Color.DarkOrange, principal.LblStatusLicencia.LinkColor);
            Assert.True(principal.ItemActivarLicencia.Available);
            Assert.Equal("Activar &licencia…", principal.ItemActivarLicencia.Text);
            Assert.True(principal.ItemRegistrarPesaje.Enabled);
            Assert.True(principal.TarjetaPesaje.Enabled);
            Assert.True(principal.ItemImportarLegado.Enabled);
        });
    }

    [Theory]
    [InlineData(Rol.Operador)]
    [InlineData(Rol.Supervisor)]
    [InlineData(Rol.Administrador)]
    public void ModoConsulta_DeshabilitaPesajeEImportar_ConElAviso_YDejaElRestoDisponible(Rol rol)
    {
        HiloSta.Ejecutar(() =>
        {
            using var principal = Crear(new ServicioLicenciaFalso { Estado = EstadoLicencia.VencidaPor(MotivoVencimiento.PruebaTerminada) }, rol);

            Assert.True(principal.LblStatusLicencia.Available);
            Assert.Equal("Modo consulta", principal.LblStatusLicencia.Text);
            Assert.Equal(Color.Firebrick, principal.LblStatusLicencia.ForeColor);
            Assert.True(principal.ItemActivarLicencia.Available);

            Assert.False(principal.ItemRegistrarPesaje.Enabled);
            Assert.Equal(Keys.F2, principal.ItemRegistrarPesaje.ShortcutKeys);
            Assert.Equal(TextoModoConsulta, principal.ItemRegistrarPesaje.ToolTipText);
            Assert.False(principal.TarjetaPesaje.Enabled);
            Assert.Equal(TextoModoConsulta, principal.TarjetaPesaje.Descripcion);
            Assert.False(principal.ItemImportarLegado.Enabled);
            Assert.Equal(TextoModoConsulta, principal.ItemImportarLegado.ToolTipText);

            // Historial, reimpresión y el resto de las tarjetas siguen disponibles.
            Assert.True(principal.TarjetaHistorial.Enabled);
            Assert.True(Item(principal, "&Pesaje", "&Historial de pesajes").Enabled);
        });
    }

    [Fact]
    public void ModoConsulta_ElSupervisorSigueConReimpresion_Edicion_YReportes()
    {
        HiloSta.Ejecutar(() =>
        {
            using var principal = Crear(new ServicioLicenciaFalso { Estado = EstadoLicencia.VencidaPor(MotivoVencimiento.Reloj) }, Rol.Supervisor);

            Assert.True(principal.TarjetaTicket.Enabled);
            Assert.True(Item(principal, "&Boletas", "&Buscar / editar boleta").Enabled);
            Assert.True(Item(principal, "&Boletas", "&Anular boleta").Enabled);
            Assert.True(Item(principal, "&Reportes", "&Reportes de pesaje").Enabled);
        });
    }

    [Fact]
    public void ElCambioDeEstado_SeReflejaSinReiniciar_ActivarRehabilitaElPesaje()
    {
        HiloSta.Ejecutar(() =>
        {
            var licencia = new ServicioLicenciaFalso { Estado = EstadoLicencia.VencidaPor(MotivoVencimiento.PruebaTerminada) };
            using var principal = Crear(licencia, Rol.Administrador);
            Assert.False(principal.TarjetaPesaje.Enabled);

            licencia.CambiarEstado(EstadoLicencia.Activada);

            Assert.False(principal.LblStatusLicencia.Available);
            Assert.False(principal.ItemActivarLicencia.Available);
            Assert.True(principal.ItemRegistrarPesaje.Enabled);
            Assert.Equal(string.Empty, principal.ItemRegistrarPesaje.ToolTipText);
            Assert.True(principal.TarjetaPesaje.Enabled);
            Assert.NotEqual(TextoModoConsulta, principal.TarjetaPesaje.Descripcion);
            Assert.Equal("Registrar el pesaje de una res: ganadero, cliente y lecturas de la balanza.", principal.TarjetaPesaje.Descripcion);
            Assert.True(principal.ItemImportarLegado.Enabled);
        });
    }

    [Fact]
    public void UnaPruebaQueVenceConLaVentanaAbierta_PasaAModoConsulta()
    {
        HiloSta.Ejecutar(() =>
        {
            var licencia = new ServicioLicenciaFalso { Estado = EstadoLicencia.EnPrueba(1) };
            using var principal = Crear(licencia);
            Assert.True(principal.TarjetaPesaje.Enabled);

            licencia.CambiarEstado(EstadoLicencia.VencidaPor(MotivoVencimiento.PruebaTerminada));

            Assert.Equal("Modo consulta", principal.LblStatusLicencia.Text);
            Assert.False(principal.TarjetaPesaje.Enabled);
        });
    }

    [Fact]
    public void UnRolSinPermisoDePesar_NoRecuperaLaTarjetaAlActivar()
    {
        // Ningún rol actual carece de Pesar: se simula con una sesión sin usuario (sin permisos).
        HiloSta.Ejecutar(() =>
        {
            var licencia = new ServicioLicenciaFalso { Estado = EstadoLicencia.Activada };
            using var principal = new FrmPrincipal(new SesionActual(), new FormFactoryFalsa(), TimeProvider.System, new ServicioParametrosFalso(), new ServicioBalanzaFalso(), licencia);
            Assert.False(principal.TarjetaPesaje.Enabled);
            var descripcion = principal.TarjetaPesaje.Descripcion;

            licencia.CambiarEstado(EstadoLicencia.EnPrueba(3));
            licencia.CambiarEstado(EstadoLicencia.Activada);

            Assert.False(principal.TarjetaPesaje.Enabled);
            Assert.Equal(descripcion, principal.TarjetaPesaje.Descripcion);
        });
    }

    [Fact]
    public void ElEnlaceDeLaBarraYElMenuAbrenLaActivacionDesdeElMenu()
    {
        HiloSta.Ejecutar(() =>
        {
            var fabrica = new FabricaCancelable();
            using var principal = Crear(new ServicioLicenciaFalso { Estado = EstadoLicencia.EnPrueba(5) }, Rol.Administrador, fabrica);

            // ToolStripItem.PerformClick no actúa con la ventana sin mostrar: se invoca el evento directamente.
            principal.LblStatusLicencia.GetType().GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(principal.LblStatusLicencia, new object[] { EventArgs.Empty });
            principal.ItemActivarLicencia.GetType().GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(principal.ItemActivarLicencia, new object[] { EventArgs.Empty });

            Assert.Equal(new[] { typeof(FrmActivacion), typeof(FrmActivacion) }, fabrica.Solicitados);
        });
    }

    [Fact]
    public void AlCerrarElShell_SeDejaDeEscucharLaLicencia()
    {
        HiloSta.Ejecutar(() =>
        {
            var licencia = new ServicioLicenciaFalso { Estado = EstadoLicencia.EnPrueba(5) };
            var principal = Crear(licencia);
            principal.Dispose();

            // No debe lanzar ni tocar un formulario desechado.
            licencia.CambiarEstado(EstadoLicencia.Activada);
        });
    }

    [Fact]
    public void BarraDeEstado_ConLicencia_SinTextosCortados()
    {
        HiloSta.Ejecutar(() =>
        {
            using var principal = Crear(new ServicioLicenciaFalso { Estado = EstadoLicencia.EnPrueba(15) }, Rol.Administrador);
            principal.Show();

            VerificadorLayout.Verificar(principal);
        });
    }

    // ------------------------------------------------------------ Acerca de

    [Theory]
    [MemberData(nameof(EstadosConTexto))]
    public void AcercaDe_MuestraLaLicencia_YElCodigoDeEquipo(EstadoLicencia estado, string texto)
    {
        HiloSta.Ejecutar(() =>
        {
            var appPaths = new AppPaths(Path.Combine(Path.GetTempPath(), "PesajeFaeneo-Test-" + Guid.NewGuid().ToString("N")));
            using var form = new FrmAcercaDe(appPaths, new ServicioLicenciaFalso { Estado = estado, CodigoEquipo = "AB12-CD34" });

            Assert.Contains("Licencia: " + texto, form.TextoSoporte);
            Assert.Contains("Código de equipo: AB12-CD34", form.TextoSoporte);
            form.Show();
            VerificadorLayout.Verificar(form);
        });
    }

    public static IEnumerable<object[]> EstadosConTexto() => new[]
    {
        new object[] { EstadoLicencia.Activada, "Activada" },
        new object[] { EstadoLicencia.EnPrueba(9), "Prueba (9 días restantes)" },
        new object[] { EstadoLicencia.EnPrueba(1), "Prueba (1 día restante)" },
        new object[] { EstadoLicencia.VencidaPor(MotivoVencimiento.PruebaTerminada), "Modo consulta (prueba terminada)" }
    };
}
