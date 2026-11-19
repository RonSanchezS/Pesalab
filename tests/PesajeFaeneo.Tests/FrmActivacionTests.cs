using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using PesajeFaeneo.Application.Licencias;
using PesajeFaeneo.Tests.Utilidades;
using PesajeFaeneo.Tests.Utilidades.Licencias;
using PesajeFaeneo.WinForms.Formularios.Licencias;
using Xunit;

namespace PesajeFaeneo.Tests;

/// <summary>F13 (D5, D9): pantalla de activación. Se prueba con el servicio real de licencias sobre un almacén y un servidor falsos.</summary>
public class FrmActivacionTests
{
    private const string ClaveBuena = "0123-4567-89AB-CDEF-0123";
    private const string TextoConsulta =
        " Ingrese una clave de activación o continúe en modo consulta (podrá ver el historial, los reportes y las copias de seguridad, pero no pesar).";

    private sealed class Entorno
    {
        public AlmacenLicenciaFalso Almacen { get; } = new();
        public ServidorLicenciasFalso Servidor { get; } = new();
        public IdentificadorEquipoFalso Equipo { get; } = new();
        public FakeTimeProvider Tiempo { get; } = new(new DateTimeOffset(2026, 10, 13, 12, 0, 0, TimeSpan.Zero));
        public ServicioLicencia Servicio { get; }

        public Entorno() =>
            Servicio = new ServicioLicencia(Almacen, Servidor, Equipo, Tiempo, NullLogger<ServicioLicencia>.Instance);

        public long Ahora => Tiempo.GetUtcNow().ToUnixTimeSeconds();
    }

    private static FrmActivacion Abrir(Entorno e, ModoActivacion modo, MotivoVencimiento? motivo = null)
    {
        var form = new FrmActivacion(e.Servicio);
        form.Configurar(modo, motivo);
        form.Show();
        return form;
    }

    // ------------------------------------------------------------ textos y botones por modo

    [Fact]
    public void NoIniciada_MuestraActivarProbarYSalir()
    {
        HiloSta.Ejecutar(() =>
        {
            using var form = Abrir(new Entorno(), ModoActivacion.NoIniciada);

            Assert.Equal("Activar Capataz", form.Text);
            Assert.Equal(
                "Ingrese su clave de activación. Si todavía no tiene una, puede probar el sistema durante 15 días.",
                form.lblIntro.Text);
            Assert.True(form.btnActivar.Visible);
            Assert.True(form.btnProbar.Visible);
            Assert.False(form.btnConsulta.Visible);
            Assert.True(form.btnSalir.Visible);
            Assert.Equal("Salir", form.btnSalir.Text);
            Assert.Equal("Probar 15 días", form.btnProbar.Text);
            Assert.Same(form.btnActivar, form.AcceptButton);
            Assert.Same(form.btnSalir, form.CancelButton);
        });
    }

    [Theory]
    [InlineData(MotivoVencimiento.PruebaTerminada, "La prueba de 15 días terminó.")]
    [InlineData(MotivoVencimiento.Reloj, "Se detectó un cambio en el reloj del sistema.")]
    [InlineData(MotivoVencimiento.Alterada, "Se detectó una alteración en los datos de la prueba.")]
    public void Vencida_MuestraActivarConsultaYSalir_ConElMotivo(MotivoVencimiento motivo, string frase)
    {
        HiloSta.Ejecutar(() =>
        {
            using var form = Abrir(new Entorno(), ModoActivacion.Vencida, motivo);

            Assert.Equal(frase + TextoConsulta, form.lblIntro.Text);
            Assert.True(form.btnActivar.Visible);
            Assert.False(form.btnProbar.Visible);
            Assert.True(form.btnConsulta.Visible);
            Assert.Equal("Continuar en modo consulta", form.btnConsulta.Text);
            Assert.Equal("Salir", form.btnSalir.Text);
        });
    }

    [Fact]
    public void DesdeMenu_MuestraActivarYCerrar()
    {
        HiloSta.Ejecutar(() =>
        {
            using var form = Abrir(new Entorno(), ModoActivacion.DesdeMenu);

            Assert.Equal("Ingrese su clave de activación para desbloquear la versión completa.", form.lblIntro.Text);
            Assert.True(form.btnActivar.Visible);
            Assert.False(form.btnProbar.Visible);
            Assert.False(form.btnConsulta.Visible);
            Assert.Equal("Cerrar", form.btnSalir.Text);
        });
    }

    [Fact]
    public void CampoDeClave_PonePlaceholder_Mayusculas_YElCodigoDeEquipo()
    {
        HiloSta.Ejecutar(() =>
        {
            var e = new Entorno();
            e.Equipo.Hwid = "abcd1234ffffffffffffffff";
            var servicio = new ServicioLicencia(e.Almacen, e.Servidor, e.Equipo, e.Tiempo, NullLogger<ServicioLicencia>.Instance);
            using var form = new FrmActivacion(servicio);
            form.Show();

            Assert.Equal("Clave de activación", form.lblClave.Text);
            Assert.Equal("XXXX-XXXX-XXXX-XXXX-XXXX", form.txtClave.PlaceholderText);
            Assert.Equal(CharacterCasing.Upper, form.txtClave.CharacterCasing);
            Assert.Equal("Código de equipo: ABCD-1234", form.lblCodigoEquipo.Text);
            Assert.Equal(Color.DarkRed, form.lblError.ForeColor);
        });
    }

    // ------------------------------------------------------------ activar

    [Fact]
    public void Activar_ClaveVacia_MuestraElErrorYNoCierra()
    {
        HiloSta.Ejecutar(() =>
        {
            var e = new Entorno();
            using var form = Abrir(e, ModoActivacion.NoIniciada);

            form.ActivarParaTestAsync().GetAwaiter().GetResult();

            Assert.Equal("Ingrese la clave de activación.", form.lblError.Text);
            Assert.Equal(ResultadoActivacion.Ninguno, form.Resultado);
            Assert.False(form.IsDisposed);
            Assert.Equal(0, e.Servidor.ActivacionesRecibidas);
        });
    }

    [Fact]
    public void Activar_FormatoInvalido_MuestraElErrorSinLlamarAlServidor()
    {
        HiloSta.Ejecutar(() =>
        {
            var e = new Entorno();
            using var form = Abrir(e, ModoActivacion.NoIniciada);

            form.txtClave.Text = "1234";
            form.ActivarParaTestAsync().GetAwaiter().GetResult();

            Assert.Equal("La clave debe tener el formato XXXX-XXXX-XXXX-XXXX-XXXX.", form.lblError.Text);
            Assert.Equal(0, e.Servidor.ActivacionesRecibidas);
            Assert.Equal(ResultadoActivacion.Ninguno, form.Resultado);
        });
    }

    [Theory]
    [InlineData(ResultadoActivacionServidor.YaUsada, "Esta clave ya fue utilizada en otro equipo.")]
    [InlineData(ResultadoActivacionServidor.Revocada, "Esta clave fue revocada.")]
    [InlineData(ResultadoActivacionServidor.Invalida, "Clave de activación inválida.")]
    public void Activar_RechazoDelServidor_MuestraElMensajeYNoCierra(ResultadoActivacionServidor resultado, string mensaje)
    {
        HiloSta.Ejecutar(() =>
        {
            var e = new Entorno();
            e.Servidor.ResultadoActivacion = resultado;
            using var form = Abrir(e, ModoActivacion.NoIniciada);

            form.txtClave.Text = ClaveBuena;
            form.ActivarParaTestAsync().GetAwaiter().GetResult();

            Assert.Equal(mensaje, form.lblError.Text);
            Assert.Equal(ResultadoActivacion.Ninguno, form.Resultado);
            Assert.False(form.IsDisposed);
            Assert.Null(e.Almacen.Datos.Activacion);
        });
    }

    [Fact]
    public void Activar_SinConexion_MuestraElMensajeDeRed()
    {
        HiloSta.Ejecutar(() =>
        {
            var e = new Entorno();
            e.Servidor.Error = new ServidorLicenciasException("No se pudo contactar al servidor de licencias. Revise la conexión a internet.");
            using var form = Abrir(e, ModoActivacion.NoIniciada);

            form.txtClave.Text = ClaveBuena;
            form.ActivarParaTestAsync().GetAwaiter().GetResult();

            Assert.Equal("No se pudo contactar al servidor de licencias. Revise la conexión a internet.", form.lblError.Text);
            Assert.Equal(ResultadoActivacion.Ninguno, form.Resultado);
        });
    }

    [Theory]
    [InlineData(ModoActivacion.NoIniciada)]
    [InlineData(ModoActivacion.Vencida)]
    [InlineData(ModoActivacion.DesdeMenu)]
    public void Activar_ClaveValida_GuardaLaActivacion_YCierraConResultadoActivada(ModoActivacion modo)
    {
        HiloSta.Ejecutar(() =>
        {
            var e = new Entorno();
            using var form = Abrir(e, modo, modo == ModoActivacion.Vencida ? MotivoVencimiento.PruebaTerminada : null);

            form.txtClave.Text = "0123456789abcdef0123";
            form.ActivarParaTestAsync().GetAwaiter().GetResult();

            Assert.Equal(ResultadoActivacion.Activada, form.Resultado);
            Assert.Equal(DialogResult.OK, form.DialogResult);
            Assert.Equal(EstadoLicencia.Activada, e.Servicio.ObtenerEstado());
            Assert.Equal(ClaveBuena, Assert.Single(e.Servidor.Claves));
        });
    }

    [Fact]
    public void CambiarElTextoDeLaClave_LimpiaElError()
    {
        HiloSta.Ejecutar(() =>
        {
            using var form = Abrir(new Entorno(), ModoActivacion.NoIniciada);
            form.ActivarParaTestAsync().GetAwaiter().GetResult();
            Assert.NotEqual(string.Empty, form.lblError.Text);

            form.txtClave.Text = "A";

            Assert.Equal(string.Empty, form.lblError.Text);
        });
    }

    // ------------------------------------------------------------ probar, consulta y salir

    [Fact]
    public void ProbarQuinceDias_IniciaLaPrueba_YCierraConResultadoPruebaIniciada()
    {
        HiloSta.Ejecutar(() =>
        {
            var e = new Entorno();
            using var form = Abrir(e, ModoActivacion.NoIniciada);

            form.IniciarPruebaParaTestAsync().GetAwaiter().GetResult();

            Assert.Equal(ResultadoActivacion.PruebaIniciada, form.Resultado);
            Assert.Equal(DialogResult.OK, form.DialogResult);
            Assert.Equal(EstadoLicencia.EnPrueba(15), e.Servicio.ObtenerEstado());
        });
    }

    [Fact]
    public void ProbarQuinceDias_SinConexion_IgualIniciaLaPrueba()
    {
        HiloSta.Ejecutar(() =>
        {
            var e = new Entorno();
            e.Servidor.Error = new ServidorLicenciasException("sin red");
            using var form = Abrir(e, ModoActivacion.NoIniciada);

            form.IniciarPruebaParaTestAsync().GetAwaiter().GetResult();

            Assert.Equal(ResultadoActivacion.PruebaIniciada, form.Resultado);
        });
    }

    [Fact]
    public void ProbarQuinceDias_ConUnaPruebaVencidaEnElServidor_PasaAlModoVencido_SinCerrar()
    {
        HiloSta.Ejecutar(() =>
        {
            var e = new Entorno();
            e.Servidor.InicioPruebaRegistrada = e.Ahora - 30 * ReglasLicencia.SegundosDia;
            using var form = Abrir(e, ModoActivacion.NoIniciada);

            form.IniciarPruebaParaTestAsync().GetAwaiter().GetResult();

            Assert.Equal(ModoActivacion.Vencida, form.Modo);
            Assert.Equal(ResultadoActivacion.Ninguno, form.Resultado);
            Assert.False(form.IsDisposed);
            Assert.True(form.btnConsulta.Visible);
            Assert.False(form.btnProbar.Visible);
            Assert.StartsWith("La prueba de 15 días terminó.", form.lblIntro.Text);
        });
    }

    [Fact]
    public void ContinuarEnModoConsulta_CierraConResultadoModoConsulta()
    {
        HiloSta.Ejecutar(() =>
        {
            using var form = Abrir(new Entorno(), ModoActivacion.Vencida, MotivoVencimiento.Reloj);

            form.btnConsulta.PerformClick();

            Assert.Equal(ResultadoActivacion.ModoConsulta, form.Resultado);
            Assert.Equal(DialogResult.OK, form.DialogResult);
        });
    }

    [Theory]
    [InlineData(ModoActivacion.NoIniciada)]
    [InlineData(ModoActivacion.Vencida)]
    public void Salir_AlArrancar_CierraConResultadoSalir(ModoActivacion modo)
    {
        HiloSta.Ejecutar(() =>
        {
            using var form = Abrir(new Entorno(), modo, MotivoVencimiento.PruebaTerminada);

            form.btnSalir.PerformClick();

            Assert.Equal(ResultadoActivacion.Salir, form.Resultado);
            Assert.Equal(DialogResult.Cancel, form.DialogResult);
        });
    }

    [Fact]
    public void CerrarLaVentana_AlArrancar_EquivaleASalir()
    {
        HiloSta.Ejecutar(() =>
        {
            using var form = Abrir(new Entorno(), ModoActivacion.NoIniciada);

            form.Close();

            Assert.Equal(ResultadoActivacion.Salir, form.Resultado);
        });
    }

    [Fact]
    public void Cerrar_DesdeElMenu_NoEsSalir()
    {
        HiloSta.Ejecutar(() =>
        {
            using var form = Abrir(new Entorno(), ModoActivacion.DesdeMenu);

            form.btnSalir.PerformClick();

            Assert.Equal(ResultadoActivacion.Ninguno, form.Resultado);
        });
    }

    [Fact]
    public void ConfigurarDeNuevo_ReiniciaElResultado()
    {
        HiloSta.Ejecutar(() =>
        {
            using var form = Abrir(new Entorno(), ModoActivacion.NoIniciada);
            form.txtClave.Text = "1234";
            form.ActivarParaTestAsync().GetAwaiter().GetResult();
            Assert.NotEqual(string.Empty, form.lblError.Text);

            form.Configurar(ModoActivacion.Vencida, MotivoVencimiento.Alterada);

            Assert.Equal(string.Empty, form.lblError.Text);
            Assert.Equal(MotivoVencimiento.Alterada, form.Motivo);
        });
    }

    // ------------------------------------------------------------ layout

    [Theory]
    [InlineData(ModoActivacion.NoIniciada, null)]
    [InlineData(ModoActivacion.Vencida, MotivoVencimiento.Alterada)]
    [InlineData(ModoActivacion.Vencida, MotivoVencimiento.PruebaTerminada)]
    [InlineData(ModoActivacion.DesdeMenu, null)]
    public void SinTextosCortados_NiConEscalado(ModoActivacion modo, MotivoVencimiento? motivo)
    {
        HiloSta.Ejecutar(() =>
        {
            using var form = Abrir(new Entorno(), modo, motivo);
            form.lblError.Text = "No se pudo contactar al servidor de licencias. Revise la conexión a internet.";

            VerificadorLayout.Verificar(form);
            VerificadorLayout.VerificarEscalado(form);
        });
    }
}
