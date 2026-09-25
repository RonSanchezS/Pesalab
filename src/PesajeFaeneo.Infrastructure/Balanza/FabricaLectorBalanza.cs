using Microsoft.Extensions.Logging;
using PesajeFaeneo.Application.Balanza;
using PesajeFaeneo.Application.Balanza.Dtos;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Infrastructure.Balanza.Simulador;

namespace PesajeFaeneo.Infrastructure.Balanza;

internal sealed class FabricaLectorBalanza : IFabricaLectorBalanza
{
    private readonly ISimuladorBalanza _simulador;
    private readonly TimeProvider _timeProvider;
    private readonly ILoggerFactory _loggerFactory;

    public FabricaLectorBalanza(
        ISimuladorBalanza simulador,
        TimeProvider timeProvider,
        ILoggerFactory loggerFactory)
    {
        _simulador = simulador ?? throw new ArgumentNullException(nameof(simulador));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
    }

    public IScaleReader Crear(ConfiguracionBalanzaDto configuracion)
    {
        ArgumentNullException.ThrowIfNull(configuracion);

        return configuracion.TipoConexion switch
        {
            TipoConexion.Serial => new LectorSerial(configuracion, _loggerFactory.CreateLogger<LectorSerial>()),
            TipoConexion.Tcp => new LectorTcp(configuracion, _loggerFactory.CreateLogger<LectorTcp>()),
            TipoConexion.Simulador => new LectorSimulador(_simulador, _timeProvider, _loggerFactory.CreateLogger<LectorSimulador>()),
            _ => throw new ArgumentOutOfRangeException(nameof(configuracion), configuracion.TipoConexion, "Tipo de conexión no soportado.")
        };
    }
}
