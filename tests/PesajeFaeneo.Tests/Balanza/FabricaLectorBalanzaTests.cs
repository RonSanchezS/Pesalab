using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using PesajeFaeneo.Application.Balanza.Dtos;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Infrastructure.Balanza;
using PesajeFaeneo.Infrastructure.Balanza.Simulador;
using Xunit;

namespace PesajeFaeneo.Tests.Balanza;

public class FabricaLectorBalanzaTests
{
    private readonly FabricaLectorBalanza _fabrica;

    public FabricaLectorBalanzaTests()
    {
        var simulador = new SimuladorBalanza(NullLogger<SimuladorBalanza>.Instance);
        var timeProvider = new FakeTimeProvider();
        _fabrica = new FabricaLectorBalanza(simulador, timeProvider, NullLoggerFactory.Instance);
    }

    [Theory]
    [InlineData(TipoConexion.Serial, typeof(LectorSerial))]
    [InlineData(TipoConexion.Tcp, typeof(LectorTcp))]
    [InlineData(TipoConexion.Simulador, typeof(LectorSimulador))]
    public void Crear_DevuelveTipoCorrectoSegunTipoConexion(TipoConexion tipo, Type tipoEsperado)
    {
        var config = new ConfiguracionBalanzaDto(
            TipoConexion: tipo,
            PuertoCom: "COM1",
            BaudRate: 9600,
            DataBits: 8,
            Paridad: ParidadSerial.None,
            BitsParada: BitsParada.One,
            HostTcp: "127.0.0.1",
            PuertoTcp: 4001,
            PatronRegex: @"(?<peso>\d+)",
            LecturasParaEstable: 5,
            ToleranciaKg: 0.5m,
            TimeoutMs: 1500);

        var lector = _fabrica.Crear(config);

        Assert.NotNull(lector);
        Assert.IsType(tipoEsperado, lector);
    }

    [Fact]
    public void Crear_ConTipoInvalido_LanzaArgumentOutOfRangeException()
    {
        var config = new ConfiguracionBalanzaDto(
            TipoConexion: (TipoConexion)999,
            PuertoCom: "COM1",
            BaudRate: 9600,
            DataBits: 8,
            Paridad: ParidadSerial.None,
            BitsParada: BitsParada.One,
            HostTcp: "127.0.0.1",
            PuertoTcp: 4001,
            PatronRegex: @"(?<peso>\d+)",
            LecturasParaEstable: 5,
            ToleranciaKg: 0.5m,
            TimeoutMs: 1500);

        Assert.Throws<ArgumentOutOfRangeException>(() => _fabrica.Crear(config));
    }

    [Fact]
    public void Crear_ConConfiguracionNula_LanzaArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _fabrica.Crear(null!));
    }
}
