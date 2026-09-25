using Serilog;
using Timer = System.Windows.Forms.Timer;
using PesajeFaeneo.Application.Balanza;
using PesajeFaeneo.Application.Balanza.Dtos;

namespace PesajeFaeneo.WinForms.Infraestructura;

/// <summary>
/// Sondea IServicioBalanza.Instantanea desde el hilo de UI (System.Windows.Forms.Timer) y avisa solo
/// cuando cambió. Debe crearse y usarse en el hilo de UI. El evento se dispara siempre en ese hilo.
/// </summary>
public sealed class MonitorBalanzaUi : IDisposable
{
    private readonly IServicioBalanza _servicio;
    private readonly Timer _timer;
    private readonly HashSet<Type> _tiposExcepcionRegistrados = new();
    private bool _disposed;

    public MonitorBalanzaUi(IServicioBalanza servicio, int intervaloMs = ReglasBalanza.IntervaloMonitorUiMs)
    {
        _servicio = servicio ?? throw new ArgumentNullException(nameof(servicio));

        _timer = new Timer
        {
            Interval = Math.Max(10, intervaloMs)
        };
        _timer.Tick += OnTimerTick;
    }

    public InstantaneaBalanza? Ultima { get; private set; }

    public event EventHandler<InstantaneaBalanza>? Actualizado;

    public void Iniciar()
    {
        if (_disposed) return;
        _timer.Start();
        Sondear();
    }

    public void Detener()
    {
        _timer.Stop();
    }

    internal void Sondear()
    {
        if (_disposed) return;

        var actual = _servicio.Instantanea;
        if (Ultima == null || actual.Secuencia != Ultima.Secuencia)
        {
            Ultima = actual;
            try
            {
                Actualizado?.Invoke(this, actual);
            }
            catch (Exception ex)
            {
                if (_tiposExcepcionRegistrados.Add(ex.GetType()))
                {
                    Log.Warning(ex, "Error no controlado en suscriptor de MonitorBalanzaUi ({TipoExcepcion}). Se silenciarán advertencias futuras para este tipo de error.", ex.GetType().Name);
                }
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _timer.Stop();
        _timer.Tick -= OnTimerTick;
        _timer.Dispose();
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        Sondear();
    }
}
