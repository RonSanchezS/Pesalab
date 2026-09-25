using System.Globalization;
using Microsoft.Extensions.Logging;
using PesajeFaeneo.Application.Balanza;

namespace PesajeFaeneo.Infrastructure.Balanza.Simulador;

internal sealed class LectorSimulador : LectorBalanzaBase
{
    private readonly ISimuladorBalanza _simulador;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger _logger;
    private readonly Random _aleatorio;
    private readonly CancellationTokenSource _ctsInterno = new();
    private Task? _bombeo;
    private bool _disposed;

    public LectorSimulador(ISimuladorBalanza simulador, TimeProvider timeProvider, ILogger logger)
        : this(simulador, timeProvider, logger, new Random())
    {
    }

    internal LectorSimulador(ISimuladorBalanza simulador, TimeProvider timeProvider, ILogger logger, Random aleatorio)
    {
        _simulador = simulador ?? throw new ArgumentNullException(nameof(simulador));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _aleatorio = aleatorio ?? throw new ArgumentNullException(nameof(aleatorio));
    }

    public override string Descripcion => "Simulador";

    public override Task ConectarAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_simulador.DesconexionSimulada)
        {
            throw new ConexionBalanzaException("Desconexión simulada.");
        }

        _bombeo = Task.Run(BombearAsync);
        return Task.CompletedTask;
    }

    public override async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _ctsInterno.Cancel();

        if (_bombeo != null)
        {
            try
            {
                await _bombeo.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
            }
            catch
            {
                // Ignorar timeout o cancelación al cerrar
            }
        }

        _ctsInterno.Dispose();
        Finalizar(null);
    }

    private async Task BombearAsync()
    {
        decimal actual = 0m;
        var generador = new GeneradorPesoSimulado();

        while (!_ctsInterno.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(ReglasBalanza.IntervaloSimuladorMs), _timeProvider, _ctsInterno.Token).ConfigureAwait(false);

                if (_simulador.DesconexionSimulada)
                {
                    Finalizar(new ConexionBalanzaException("Desconexión simulada."));
                    break;
                }

                actual = generador.Siguiente(actual, _simulador.PesoObjetivoKg, _aleatorio);
                EntregarTrama(actual.ToString("0.0", CultureInfo.InvariantCulture));
            }
            catch (OperationCanceledException) when (_ctsInterno.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en el generador de tramas simuladas.");
                Finalizar(new ConexionBalanzaException("Error en el simulador de balanza.", ex));
                break;
            }
        }
    }
}
