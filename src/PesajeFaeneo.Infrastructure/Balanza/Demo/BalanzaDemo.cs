#if CAPTURAS_MANUAL
using System.Globalization;
using Microsoft.Extensions.Logging;
using PesajeFaeneo.Application.Balanza;
using PesajeFaeneo.Infrastructure.Balanza.Simulador;

namespace PesajeFaeneo.Infrastructure.Balanza.Demo;

/// <summary>
/// SOLO build de capturas para los manuales (constante CAPTURAS_MANUAL; nunca llega a la publicacion ni al instalador).
/// Controla la "balanza de demostracion": un lector que se presenta como una balanza serie normal (el servicio de
/// balanza lo publica con la descripcion "COM3 9600 8N1", sin marca de simulador y con origen Balanza) y reproduce
/// un guion de pesos. El guion de capturas puede forzar un peso (deterministico) o simular una falla de la balanza.
/// No toca ninguna regla antifraude (TextosBoleta, ControlCapturas, barra de estado): el "simulador" real sigue
/// marcando sus pesos; aqui, simplemente, el lector de demostracion ocupa el lugar de un lector serie.
/// </summary>
public static class ControladorBalanzaDemo
{
    private static readonly object Candado = new();
    private static decimal? _pesoForzado;
    private static bool _silencio;
    private static bool _desconectada;
    private static decimal _ruido;
    private static bool _formatoRx8;
    private static long _inicioGuion = Environment.TickCount64;

    /// <summary>Pasos del guion: (peso objetivo, segundos que se mantiene).</summary>
    private static readonly (decimal Peso, double Segundos)[] Guion =
    [
        (121.4m, 12), (0m, 4), (118.6m, 12), (0m, 4)
    ];

    /// <summary>Si no es null, el lector apunta a este peso en lugar de seguir el guion.</summary>
    public static decimal? PesoForzado
    {
        get { lock (Candado) { return _pesoForzado; } }
        set { lock (Candado) { _pesoForzado = value; } }
    }

    /// <summary>Si es true, el lector deja de enviar tramas (el servicio publica "sin datos" tras 3 vencimientos).</summary>
    public static bool Silencio
    {
        get { lock (Candado) { return _silencio; } }
        set { lock (Candado) { _silencio = value; } }
    }

    /// <summary>Si es true, la conexion se pierde (el servicio publica un error y reintenta).</summary>
    public static bool Desconectada
    {
        get { lock (Candado) { return _desconectada; } }
        set { lock (Candado) { _desconectada = value; } }
    }

    /// <summary>Si es mayor que 0, el peso oscila +/- este valor (kg) en cada trama: la balanza no llega a estabilizarse.</summary>
    public static decimal Ruido
    {
        get { lock (Candado) { return _ruido; } }
        set { lock (Candado) { _ruido = value; } }
    }

    /// <summary>Si es true, las tramas imitan al indicador RX8 (inicio con '=' y peso en gramos, p. ej. =121400).</summary>
    public static bool FormatoRx8
    {
        get { lock (Candado) { return _formatoRx8; } }
        set { lock (Candado) { _formatoRx8 = value; } }
    }

    public static void ReiniciarGuion()
    {
        lock (Candado)
        {
            _inicioGuion = Environment.TickCount64;
            _pesoForzado = null;
            _silencio = false;
            _desconectada = false;
            _ruido = 0m;
            _formatoRx8 = false;
        }
    }

    /// <summary>Peso al que debe tender el lector en este instante.</summary>
    internal static decimal PesoObjetivo()
    {
        lock (Candado)
        {
            if (_pesoForzado is { } forzado)
            {
                return forzado;
            }

            var total = Guion.Sum(p => p.Segundos);
            var t = (Environment.TickCount64 - _inicioGuion) / 1000.0 % total;
            foreach (var paso in Guion)
            {
                if (t < paso.Segundos)
                {
                    return paso.Peso;
                }

                t -= paso.Segundos;
            }

            return 0m;
        }
    }
}

internal sealed class LectorBalanzaDemo : LectorBalanzaBase
{
    private readonly TimeProvider _timeProvider;
    private readonly ILogger _logger;
    private readonly Random _aleatorio = new();
    private readonly CancellationTokenSource _cts = new();
    private Task? _bombeo;
    private bool _disposed;

    public LectorBalanzaDemo(string descripcion, TimeProvider timeProvider, ILogger logger)
    {
        Descripcion = descripcion;
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override string Descripcion { get; }

    public override Task ConectarAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (ControladorBalanzaDemo.Desconectada)
        {
            throw new ConexionBalanzaException("No se pudo abrir el puerto. Revise el cable o el adaptador USB-serie.");
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
        _cts.Cancel();
        if (_bombeo != null)
        {
            try
            {
                await _bombeo.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
            }
            catch
            {
                // Ignorar timeout o cancelacion al cerrar.
            }
        }

        _cts.Dispose();
        Finalizar(null);
    }

    private async Task BombearAsync()
    {
        decimal actual = 0m;
        var alterna = false;
        var generador = new GeneradorPesoSimulado();
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(ReglasBalanza.IntervaloSimuladorMs), _timeProvider, _cts.Token).ConfigureAwait(false);

                if (ControladorBalanzaDemo.Desconectada)
                {
                    Finalizar(new ConexionBalanzaException("Se perdió la conexión con la balanza."));
                    break;
                }

                if (ControladorBalanzaDemo.Silencio)
                {
                    continue;
                }

                actual = generador.Siguiente(actual, ControladorBalanzaDemo.PesoObjetivo(), _aleatorio);
                var mostrado = actual;
                if (ControladorBalanzaDemo.Ruido > 0m)
                {
                    alterna = !alterna;
                    mostrado += alterna ? ControladorBalanzaDemo.Ruido : -ControladorBalanzaDemo.Ruido;
                }

                mostrado = Math.Max(0m, mostrado);
                EntregarTrama(ControladorBalanzaDemo.FormatoRx8
                    ? "=" + ((long)Math.Round(mostrado * 1000m)).ToString("000000", CultureInfo.InvariantCulture)
                    : mostrado.ToString("0.0", CultureInfo.InvariantCulture));
            }
            catch (OperationCanceledException) when (_cts.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en el lector de balanza de demostración.");
                Finalizar(new ConexionBalanzaException("Error en la balanza de demostración.", ex));
                break;
            }
        }
    }
}

internal sealed class EnumeradorPuertosDemo : IEnumeradorPuertosSerie
{
    public IReadOnlyList<string> Listar() => ["COM1", "COM3"];
}
#endif
