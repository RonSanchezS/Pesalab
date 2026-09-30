using Microsoft.Extensions.Logging.Abstractions;
using PesajeFaeneo.Application.Boletas;
using PesajeFaeneo.Application.Boletas.Dtos;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Domain.Excepciones;
using PesajeFaeneo.Tests.Utilidades;
using PesajeFaeneo.Tests.Utilidades.Boletas;
using PesajeFaeneo.WinForms.Impresion;
using Xunit;

namespace PesajeFaeneo.Tests.Impresion;

public class ServicioImpresionBoletasTests
{
    private readonly ServicioBoletasFalso _boletas = new();
    private readonly ServicioConfiguracionImpresionFalso _config = new();
    private readonly ImpresorBoletasFalso _impresor = new();
    private readonly ServicioImpresionBoletas _servicio;

    public ServicioImpresionBoletasTests()
    {
        _servicio = new ServicioImpresionBoletas(_boletas, _config, _impresor, new NullLogger<ServicioImpresionBoletas>());
    }

    [Fact]
    public async Task ImprimirAsync_AutomaticaYNoConfigurada_DevuelveOmitidaYSinInvocaciones()
    {
        _config.EquipoRetorno = new ConfiguracionImpresionEquipo(ReglasImpresion.PorDefecto, Configurada: false, NombreEquipo: "PC-TEST");

        var resultado = await _servicio.ImprimirAsync(1, SolicitudImpresion.AutomaticaAlGrabar);

        Assert.Equal(EstadoImpresion.Omitida, resultado.Estado);
        Assert.Empty(_boletas.IdsObtenidos);
        Assert.Empty(_impresor.Impresiones);
    }

    [Fact]
    public async Task ImprimirAsync_AutomaticaYConfiguradaPeroDesactivada_DevuelveOmitida()
    {
        var configDesactivada = new ConfiguracionImpresion("EPSON LX-350", FormatoBoleta.MediaCarta, null, 1, ImprimirAlGrabar: false, 0m, 0m);
        _config.EquipoRetorno = new ConfiguracionImpresionEquipo(configDesactivada, Configurada: true, NombreEquipo: "PC-TEST");

        var resultado = await _servicio.ImprimirAsync(1, SolicitudImpresion.AutomaticaAlGrabar);

        Assert.Equal(EstadoImpresion.Omitida, resultado.Estado);
        Assert.Empty(_boletas.IdsObtenidos);
        Assert.Empty(_impresor.Impresiones);
    }

    [Fact]
    public async Task ImprimirAsync_AutomaticaYActivada_ImprimeYRegistraAuditoria()
    {
        var configActiva = new ConfiguracionImpresion(null, FormatoBoleta.MediaCarta, null, 2, ImprimirAlGrabar: true, 0m, 0m);
        _config.EquipoRetorno = new ConfiguracionImpresionEquipo(configActiva, Configurada: true, NombreEquipo: "PC-TEST");
        _boletas.DatosRetorno = DatosBoletaDePrueba.Crear(pesajeId: 10, numeroBoleta: 55, impresionesPrevias: 0);

        var resultado = await _servicio.ImprimirAsync(10, SolicitudImpresion.AutomaticaAlGrabar);

        Assert.Equal(EstadoImpresion.Impresa, resultado.Estado);
        Assert.Contains("Boleta N° 55", resultado.Mensaje);
        Assert.Single(_impresor.Impresiones);
        Assert.Single(_boletas.ImpresionesRegistradas);

        var reg = _boletas.ImpresionesRegistradas[0];
        Assert.Equal(10, reg.PesajeId);
        Assert.Equal(55, reg.NumeroBoleta);
        Assert.False(reg.EsReimpresion);
        Assert.Equal(2, reg.Copias);
        Assert.Equal("(predeterminada)", reg.Impresora);
        Assert.Equal("PC-TEST", reg.NombreEquipo);
    }

    [Fact]
    public async Task ImprimirAsync_ManualSinConfiguracion_ImprimeConPorDefecto()
    {
        _config.EquipoRetorno = new ConfiguracionImpresionEquipo(ReglasImpresion.PorDefecto, Configurada: false, NombreEquipo: "PC-TEST");
        _boletas.DatosRetorno = DatosBoletaDePrueba.Crear(pesajeId: 1, numeroBoleta: 100);

        var resultado = await _servicio.ImprimirAsync(1, SolicitudImpresion.Manual);

        Assert.Equal(EstadoImpresion.Impresa, resultado.Estado);
        Assert.Single(_impresor.Impresiones);
        Assert.Equal(ReglasImpresion.PorDefecto, _impresor.Impresiones[0].Config);
    }

    [Fact]
    public async Task ImprimirAsync_ConImpresionesPrevias_RegistraComoReimpresion()
    {
        _boletas.DatosRetorno = DatosBoletaDePrueba.Crear(pesajeId: 1, numeroBoleta: 100, impresionesPrevias: 2);

        var resultado = await _servicio.ImprimirAsync(1, SolicitudImpresion.Manual);

        Assert.Equal(EstadoImpresion.Impresa, resultado.Estado);
        Assert.True(resultado.EsReimpresion);
        Assert.Contains("Reimpresión de la boleta N° 100", resultado.Mensaje);

        Assert.Single(_boletas.ImpresionesRegistradas);
        Assert.True(_boletas.ImpresionesRegistradas[0].EsReimpresion);
    }

    [Fact]
    public async Task ImprimirAsync_ImpresorLanzaReglaNegocioException_DevuelveFallidaYSinAuditoria()
    {
        _impresor.ExcepcionImprimir = new ReglaNegocioException("IMP_IMPRESORA_NO_VALIDA", "Impresora no encontrada.");
        _boletas.DatosRetorno = DatosBoletaDePrueba.Crear(pesajeId: 1, numeroBoleta: 100);

        var resultado = await _servicio.ImprimirAsync(1, SolicitudImpresion.Manual);

        Assert.Equal(EstadoImpresion.Fallida, resultado.Estado);
        Assert.Equal("Impresora no encontrada.", resultado.Mensaje);
        Assert.Empty(_boletas.ImpresionesRegistradas);
    }

    [Fact]
    public async Task ImprimirAsync_AuditoriaLanzaExcepcion_InformaImpresaConMensajeDeAdvertencia()
    {
        _boletas.ExcepcionRegistrarImpresion = new InvalidOperationException("Fallo de BD en auditoría.");
        _boletas.DatosRetorno = DatosBoletaDePrueba.Crear(pesajeId: 1, numeroBoleta: 100);

        var resultado = await _servicio.ImprimirAsync(1, SolicitudImpresion.Manual);

        Assert.Equal(EstadoImpresion.Impresa, resultado.Estado);
        Assert.Contains("impresa, pero no se pudo registrar la impresión en la auditoría", resultado.Mensaje);
        Assert.Single(_impresor.Impresiones);
    }

    [Fact]
    public async Task ImprimirAsync_ObtenerPorIdLanzaNoAutorizado_SePropaga()
    {
        _boletas.ExcepcionObtenerPorId = new NoAutorizadoException("Sin permiso.");

        await Assert.ThrowsAsync<NoAutorizadoException>(() => _servicio.ImprimirAsync(1, SolicitudImpresion.Manual));
        Assert.Empty(_impresor.Impresiones);
    }

    [Fact]
    public async Task ImprimirAsync_ImpresorLanzaExcepcionInesperada_SePropaga()
    {
        _impresor.ExcepcionImprimir = new InvalidOperationException("Fallo inesperado del driver.");

        await Assert.ThrowsAsync<InvalidOperationException>(() => _servicio.ImprimirAsync(1, SolicitudImpresion.Manual));
        Assert.Empty(_boletas.ImpresionesRegistradas);
    }

    [Fact]
    public async Task CrearVistaPreviaAsync_InvocaImpresorSinRegistrarAuditoria()
    {
        HiloSta.Ejecutar(() =>
        {
            _boletas.DatosRetorno = DatosBoletaDePrueba.Crear(pesajeId: 5, numeroBoleta: 88);

            using var doc = _servicio.CrearVistaPreviaAsync(5).GetAwaiter().GetResult();

            Assert.NotNull(doc);
            Assert.Single(_impresor.VistasPrevias);
            Assert.Equal(88, _impresor.VistasPrevias[0].Datos.NumeroBoleta);
            Assert.Empty(_boletas.ImpresionesRegistradas);
        });
    }
}
