using System.Drawing;
using System.Globalization;
using PesajeFaeneo.Application.Balanza;
using PesajeFaeneo.Application.Balanza.Dtos;
using PesajeFaeneo.WinForms.Infraestructura;
using Xunit;

namespace PesajeFaeneo.Tests.Balanza;

public class PresentacionBalanzaTests
{
    private static InstantaneaBalanza Crear(
        EstadoBalanza estado,
        decimal? peso = null,
        bool estable = false,
        string? mensaje = null,
        string descripcion = "COM1 9600 8N1",
        bool simulador = false) => new(
        Secuencia: 1,
        Estado: estado,
        PesoKg: peso,
        Estable: estable,
        InstanteLectura: DateTimeOffset.UtcNow,
        Mensaje: mensaje,
        Descripcion: descripcion,
        EsSimulador: simulador);

    [Theory]
    [InlineData(EstadoBalanza.Detenida, false, null, "Balanza: detenida")]
    [InlineData(EstadoBalanza.Detenida, true, null, "[SIMULADOR] Balanza: detenida")]
    [InlineData(EstadoBalanza.Conectando, false, null, "Balanza: conectando…")]
    [InlineData(EstadoBalanza.Conectando, false, "Reintentando en 2 s…", "Reintentando en 2 s…")]
    [InlineData(EstadoBalanza.Conectada, false, null, "Balanza: conectada (COM1 9600 8N1)")]
    [InlineData(EstadoBalanza.Conectada, true, null, "[SIMULADOR] Balanza: conectada (COM1 9600 8N1)")]
    [InlineData(EstadoBalanza.SinDatos, false, null, "Balanza: sin datos")]
    [InlineData(EstadoBalanza.TramasNoReconocidas, false, null, "Balanza: tramas no reconocidas")]
    [InlineData(EstadoBalanza.Error, false, "Puerto no disponible", "Balanza: error — Puerto no disponible")]
    [InlineData(EstadoBalanza.EnDiagnostico, false, null, "Balanza: en diagnóstico")]
    public void TextoEstado_FormateaCorrectamenteSegunEstadoYSimulador(
        EstadoBalanza estado,
        bool simulador,
        string? mensaje,
        string esperado)
    {
        var instantanea = Crear(estado, mensaje: mensaje, simulador: simulador);
        var resultado = PresentacionBalanza.TextoEstado(instantanea);
        Assert.Equal(esperado, resultado);
    }

    [Fact]
    public void TextoPeso_ConPesoNulo_DevuelveGuiones()
    {
        var instantanea = Crear(EstadoBalanza.Conectada, peso: null);
        Assert.Equal("---- kg", PresentacionBalanza.TextoPeso(instantanea));
    }

    [Fact]
    public void TextoPeso_ConPesoValido_FormateaSegunCulturaActual()
    {
        var culturaAnterior = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("es-ES");
            var instantanea = Crear(EstadoBalanza.Conectada, peso: 123.4m);
            Assert.Equal("123,4 kg", PresentacionBalanza.TextoPeso(instantanea));

            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            Assert.Equal("123.4 kg", PresentacionBalanza.TextoPeso(instantanea));
        }
        finally
        {
            CultureInfo.CurrentCulture = culturaAnterior;
        }
    }

    [Fact]
    public void ColorEstado_ConectadaYEstable_DevuelveVerdeOscuro()
    {
        var instantanea = Crear(EstadoBalanza.Conectada, estable: true);
        Assert.Equal(PresentacionBalanza.ColorVerdeOscuro, PresentacionBalanza.ColorEstado(instantanea));
    }

    [Fact]
    public void ColorEstado_ConectadaInestable_DevuelveControlText()
    {
        var instantanea = Crear(EstadoBalanza.Conectada, estable: false);
        Assert.Equal(SystemColors.ControlText, PresentacionBalanza.ColorEstado(instantanea));
    }

    [Theory]
    [InlineData(EstadoBalanza.Error)]
    [InlineData(EstadoBalanza.SinDatos)]
    [InlineData(EstadoBalanza.TramasNoReconocidas)]
    public void ColorEstado_ErroresYSinDatos_DevuelveRojoOscuro(EstadoBalanza estado)
    {
        var instantanea = Crear(estado);
        Assert.Equal(PresentacionBalanza.ColorRojoOscuro, PresentacionBalanza.ColorEstado(instantanea));
    }

    [Theory]
    [InlineData(EstadoBalanza.Detenida)]
    [InlineData(EstadoBalanza.Conectando)]
    [InlineData(EstadoBalanza.EnDiagnostico)]
    public void ColorEstado_Resto_DevuelveGris(EstadoBalanza estado)
    {
        var instantanea = Crear(estado);
        Assert.Equal(PresentacionBalanza.ColorGris, PresentacionBalanza.ColorEstado(instantanea));
    }
}
