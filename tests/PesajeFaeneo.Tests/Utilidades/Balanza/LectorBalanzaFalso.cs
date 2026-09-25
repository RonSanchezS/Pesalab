using System.Threading.Channels;
using PesajeFaeneo.Application.Balanza;

namespace PesajeFaeneo.Tests.Utilidades.Balanza;

public class LectorBalanzaFalso : IScaleReader
{
    private readonly Channel<string> _canal = Channel.CreateUnbounded<string>();

    public string Descripcion { get; set; } = "Falso";
    public Exception? ExcepcionAlConectar { get; set; }
    public bool Desechado { get; private set; }
    public bool Conectado { get; private set; }

    public void Enviar(string trama)
    {
        _canal.Writer.TryWrite(trama);
    }

    public void FallarLectura(Exception ex)
    {
        _canal.Writer.TryComplete(ex);
    }

    public Task ConectarAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (ExcepcionAlConectar != null)
        {
            throw ExcepcionAlConectar;
        }

        Conectado = true;
        return Task.CompletedTask;
    }

    public async ValueTask<string> LeerTramaAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _canal.Reader.ReadAsync(cancellationToken);
        }
        catch (ChannelClosedException ex)
        {
            if (ex.InnerException != null)
            {
                throw ex.InnerException;
            }
            throw;
        }
    }

    public ValueTask DisposeAsync()
    {
        Desechado = true;
        Conectado = false;
        _canal.Writer.TryComplete();
        return ValueTask.CompletedTask;
    }
}
