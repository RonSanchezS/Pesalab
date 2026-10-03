using PesajeFaeneo.Application.PuestaEnMarcha.Dtos;
using PesajeFaeneo.Application.Seguridad;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Domain.Excepciones;
using PesajeFaeneo.Tests.Utilidades;
using PesajeFaeneo.Tests.Utilidades.PuestaEnMarcha;
using PesajeFaeneo.WinForms.Formularios;
using PesajeFaeneo.WinForms.Formularios.PuestaEnMarcha;
using PesajeFaeneo.WinForms.Infraestructura;
using Xunit;

namespace PesajeFaeneo.Tests;

public class FrmAsistentePuestaEnMarchaTests
{
    private sealed class FormFactoryProhibida : IFormFactory
    {
        public T Crear<T>() where T : Form => throw new InvalidOperationException("Ningún test del asistente debe abrir un formulario hijo real.");
    }

    private static SesionActual Sesion(Rol rol)
    {
        var sesion = new SesionActual();
        sesion.Iniciar(new UsuarioSesion(1, "usuario_test", "Usuario de Prueba", rol, false));
        return sesion;
    }

    private static FrmAsistentePuestaEnMarcha Crear(
        ServicioPuestaEnMarchaFalso servicio,
        Rol rol = Rol.Administrador,
        Func<PasoPuestaEnMarcha, bool>? gancho = null)
    {
        var form = new FrmAsistentePuestaEnMarcha(servicio, Sesion(rol), new FormFactoryProhibida())
        {
            AbrirPasoParaTest = gancho ?? (_ => false),
            MostrarAdvertenciaParaTest = _ => { }
        };
        form.Show();
        return form;
    }

    private static EstadoPuestaEnMarcha Estado(bool empresa, TipoConexion tipo, int fleteros = 0, int usuarios = 0, bool impresion = false) =>
        new(false, empresa, empresa ? "FRIGORIFICO X" : "NOMBRE DE EMPRESA", tipo, impresion, fleteros, usuarios);

    [Fact]
    public void Carga_EstadoInicial_MuestraPendientesYSimulador()
    {
        HiloSta.Ejecutar(() =>
        {
            var servicio = new ServicioPuestaEnMarchaFalso { Estado = Estado(false, TipoConexion.Simulador) };
            using var form = Crear(servicio);
            form.CargarParaTestAsync().GetAwaiter().GetResult();

            Assert.Equal("Pendiente", form.EstadoPaso(PasoPuestaEnMarcha.Empresa).Text);
            Assert.Equal("SIMULADOR", form.EstadoPaso(PasoPuestaEnMarcha.Balanza).Text);
            Assert.Equal("Pendiente", form.EstadoPaso(PasoPuestaEnMarcha.Usuarios).Text);
            Assert.Equal("0 activos", form.EstadoPaso(PasoPuestaEnMarcha.Fleteros).Text);
            Assert.True(form.ChkSimulador.Visible);
            Assert.False(form.BtnFinalizar.Enabled);
            Assert.Equal("Falta: datos de la empresa.", form.LblResumen.Text);
        });
    }

    [Fact]
    public void Simulador_ConEmpresa_FinalizarSoloConLaCasillaMarcada()
    {
        HiloSta.Ejecutar(() =>
        {
            var servicio = new ServicioPuestaEnMarchaFalso { Estado = Estado(true, TipoConexion.Simulador) };
            using var form = Crear(servicio);
            form.CargarParaTestAsync().GetAwaiter().GetResult();

            Assert.False(form.BtnFinalizar.Enabled);
            Assert.Equal("Falta: configurar la balanza o confirmar el simulador.", form.LblResumen.Text);

            form.ChkSimulador.Checked = true;

            Assert.True(form.BtnFinalizar.Enabled);
            Assert.Equal(string.Empty, form.LblResumen.Text);
        });
    }

    [Fact]
    public void BalanzaSerial_OcultaCasillaYPermiteFinalizar()
    {
        HiloSta.Ejecutar(() =>
        {
            var servicio = new ServicioPuestaEnMarchaFalso { Estado = Estado(true, TipoConexion.Serial, fleteros: 2, usuarios: 3, impresion: true) };
            using var form = Crear(servicio);
            form.CargarParaTestAsync().GetAwaiter().GetResult();

            Assert.False(form.ChkSimulador.Visible);
            Assert.Equal("Listo (Serie)", form.EstadoPaso(PasoPuestaEnMarcha.Balanza).Text);
            Assert.Equal("Listo", form.EstadoPaso(PasoPuestaEnMarcha.Empresa).Text);
            Assert.Equal("Listo", form.EstadoPaso(PasoPuestaEnMarcha.Impresion).Text);
            Assert.Equal("2 activos", form.EstadoPaso(PasoPuestaEnMarcha.Fleteros).Text);
            Assert.Equal("3 usuarios", form.EstadoPaso(PasoPuestaEnMarcha.Usuarios).Text);
            Assert.True(form.BtnFinalizar.Enabled);
        });
    }

    [Fact]
    public void Finalizar_Exito_LlamaAlServicioYCierraConOk()
    {
        HiloSta.Ejecutar(() =>
        {
            var servicio = new ServicioPuestaEnMarchaFalso { Estado = Estado(true, TipoConexion.Simulador) };
            using var form = Crear(servicio);
            form.CargarParaTestAsync().GetAwaiter().GetResult();
            form.ChkSimulador.Checked = true;

            form.BtnFinalizar.PerformClick();

            Assert.Equal(new[] { true }, servicio.LlamadasMarcarCompleta);
            Assert.Equal(DialogResult.OK, form.DialogResult);
        });
    }

    [Fact]
    public void Finalizar_ConErrorDeValidacion_MuestraAdvertenciaYRecargaSinCerrar()
    {
        HiloSta.Ejecutar(() =>
        {
            var servicio = new ServicioPuestaEnMarchaFalso
            {
                Estado = Estado(true, TipoConexion.Serial),
                ExcepcionAlMarcar = new ValidacionException("Empresa", "falta empresa")
            };
            using var form = Crear(servicio);
            string? advertencia = null;
            form.MostrarAdvertenciaParaTest = t => advertencia = t;
            form.CargarParaTestAsync().GetAwaiter().GetResult();

            form.BtnFinalizar.PerformClick();

            Assert.Contains("falta empresa", advertencia);
            Assert.NotEqual(DialogResult.OK, form.DialogResult);
            Assert.False(form.IsDisposed);
            Assert.Equal(2, servicio.LlamadasObtenerEstado);
        });
    }

    [Fact]
    public void ClicEnUnPaso_AbreElPasoYRecargaElEstado()
    {
        HiloSta.Ejecutar(() =>
        {
            var servicio = new ServicioPuestaEnMarchaFalso { Estado = Estado(true, TipoConexion.Simulador) };
            PasoPuestaEnMarcha? abierto = null;
            using var form = Crear(servicio, gancho: p =>
            {
                abierto = p;
                return false;
            });
            form.CargarParaTestAsync().GetAwaiter().GetResult();

            form.BotonPaso(PasoPuestaEnMarcha.Balanza).PerformClick();

            Assert.Equal(PasoPuestaEnMarcha.Balanza, abierto);
            Assert.Equal(2, servicio.LlamadasObtenerEstado);
        });
    }

    [Fact]
    public void PasoQuePideReinicio_CierraConCancelYMarcaReinicio()
    {
        HiloSta.Ejecutar(() =>
        {
            var servicio = new ServicioPuestaEnMarchaFalso { Estado = Estado(false, TipoConexion.Simulador) };
            using var form = Crear(servicio, gancho: p => p == PasoPuestaEnMarcha.CopiasSeguridad);
            form.CargarParaTestAsync().GetAwaiter().GetResult();

            form.BotonPaso(PasoPuestaEnMarcha.CopiasSeguridad).PerformClick();

            Assert.True(form.ReinicioSolicitado);
            Assert.Equal(DialogResult.Cancel, form.DialogResult);
            Assert.Equal(1, servicio.LlamadasObtenerEstado);
        });
    }

    [Fact]
    public void Supervisor_NoPuedeAbrirElPasoDeBalanza()
    {
        HiloSta.Ejecutar(() =>
        {
            var servicio = new ServicioPuestaEnMarchaFalso { Estado = Estado(true, TipoConexion.Serial) };
            using var form = Crear(servicio, Rol.Supervisor);
            form.CargarParaTestAsync().GetAwaiter().GetResult();

            Assert.False(form.BotonPaso(PasoPuestaEnMarcha.Balanza).Enabled);
        });
    }

    [Fact]
    public void Omitir_CierraConCancelSinMarcarCompleta()
    {
        HiloSta.Ejecutar(() =>
        {
            var servicio = new ServicioPuestaEnMarchaFalso { Estado = Estado(true, TipoConexion.Serial) };
            using var form = Crear(servicio);
            form.CargarParaTestAsync().GetAwaiter().GetResult();

            Assert.Equal(DialogResult.Cancel, form.BtnOmitir.DialogResult);
            Assert.Same(form.BtnOmitir, form.CancelButton);
            Assert.Empty(servicio.LlamadasMarcarCompleta);
        });
    }

    [Fact]
    public void Enter_NoFinaliza_PorqueNoHayBotonAceptar()
    {
        HiloSta.Ejecutar(() =>
        {
            var servicio = new ServicioPuestaEnMarchaFalso { Estado = Estado(true, TipoConexion.Serial) };
            using var form = Crear(servicio);
            form.CargarParaTestAsync().GetAwaiter().GetResult();

            Assert.Null(form.AcceptButton);
            Assert.Empty(servicio.LlamadasMarcarCompleta);
        });
    }
}
