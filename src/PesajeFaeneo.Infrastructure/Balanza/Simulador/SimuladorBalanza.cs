using Microsoft.Extensions.Logging;
using PesajeFaeneo.Application.Balanza;

namespace PesajeFaeneo.Infrastructure.Balanza.Simulador;

internal sealed class SimuladorBalanza : ISimuladorBalanza
{
    private readonly ILogger<SimuladorBalanza> _logger;
    private readonly object _candado = new();
    private decimal _pesoObjetivoKg;
    private bool _desconexionSimulada;

    public SimuladorBalanza(ILogger<SimuladorBalanza> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public decimal PesoObjetivoKg
    {
        get
        {
            lock (_candado)
            {
                return _pesoObjetivoKg;
            }
        }
    }

    public bool DesconexionSimulada
    {
        get
        {
            lock (_candado)
            {
                return _desconexionSimulada;
            }
        }
        set
        {
            lock (_candado)
            {
                _desconexionSimulada = value;
            }
        }
    }

    public void EstablecerPesoObjetivo(decimal pesoKg)
    {
        decimal recortado = Math.Clamp(pesoKg, 0m, ReglasBalanza.PesoMaximoSimuladorKg);
        recortado = Math.Round(recortado, 1, MidpointRounding.AwayFromZero);

        lock (_candado)
        {
            _pesoObjetivoKg = recortado;
        }

        _logger.LogInformation("Peso simulado establecido en {Peso} kg", recortado);
    }
}
