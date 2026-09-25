using PesajeFaeneo.Application.Balanza;
using PesajeFaeneo.Infrastructure.Balanza;
using Xunit;

namespace PesajeFaeneo.Tests.Balanza;

public class EnumeradorPuertosSerieTests
{
    [Fact]
    public void Normalizar_FiltraDuplicadosInvalidadosYOrdenaNumericamente()
    {
        var entrada = new[] { "COM10", "com2 ", "COM2", "COM3\0x", "LPT1", "", null!, "com1" };

        var resultado = EnumeradorPuertosSerie.Normalizar(entrada);

        Assert.Equal(3, resultado.Count);
        Assert.Equal("COM1", resultado[0]);
        Assert.Equal("COM2", resultado[1]);
        Assert.Equal("COM10", resultado[2]);
    }

    [Fact]
    public void Normalizar_ConColeccionNulaOVacia_DevuelveListaVacia()
    {
        Assert.Empty(EnumeradorPuertosSerie.Normalizar(null!));
        Assert.Empty(EnumeradorPuertosSerie.Normalizar(Array.Empty<string>()));
    }

    [Fact]
    public void Listar_NoLanzaExcepcion()
    {
        var enumerador = new EnumeradorPuertosSerie(Microsoft.Extensions.Logging.Abstractions.NullLogger<EnumeradorPuertosSerie>.Instance);
        var lista = enumerador.Listar();
        Assert.NotNull(lista);
    }
}
