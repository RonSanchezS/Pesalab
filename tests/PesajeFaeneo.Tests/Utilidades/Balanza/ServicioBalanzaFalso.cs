using PesajeFaeneo.Application.Balanza;
using PesajeFaeneo.Application.Balanza.Dtos;

namespace PesajeFaeneo.Tests.Utilidades.Balanza;

public class ServicioBalanzaFalso : IServicioBalanza
{
    public InstantaneaBalanza Instantanea { get; set; } = InstantaneaBalanza.Inicial;

    public int VecesIniciado { get; private set; }
    public int VecesDetenido { get; private set; }
    public int VecesReiniciado { get; private set; }

    public List<string> TramasRecientes { get; } = new();
    public IReadOnlyList<string> ObtenerTramasRecientes() => TramasRecientes.ToList();

    public TaskCompletionSource? TcsPrueba { get; set; }
    public ResultadoPruebaBalanza ResultadoPrueba { get; set; } = new(true, null, 10, 10, 50m, true, false);

    public virtual Task IniciarAsync(CancellationToken cancellationToken = default)
    {
        VecesIniciado++;
        return Task.CompletedTask;
    }

    public virtual Task DetenerAsync(CancellationToken cancellationToken = default)
    {
        VecesDetenido++;
        return Task.CompletedTask;
    }

    public virtual Task ReiniciarAsync(CancellationToken cancellationToken = default)
    {
        VecesReiniciado++;
        return Task.CompletedTask;
    }

    public CancellationToken TokenPruebaRecibido { get; private set; }

    public virtual async Task<ResultadoPruebaBalanza> ProbarAsync(
        ConfiguracionBalanzaDto candidata,
        TimeSpan duracion,
        IProgress<AvancePruebaBalanza>? progreso = null,
        CancellationToken cancellationToken = default)
    {
        TokenPruebaRecibido = cancellationToken;
        if (TcsPrueba != null)
        {
            await TcsPrueba.Task;
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return new ResultadoPruebaBalanza(false, null, 0, 0, null, false, true);
        }

        return ResultadoPrueba;
    }

    public void Dispose()
    {
    }
}
