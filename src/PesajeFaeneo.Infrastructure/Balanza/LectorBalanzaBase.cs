using System.Threading.Channels;
using PesajeFaeneo.Application.Balanza;
using PesajeFaeneo.Domain.Enums;

namespace PesajeFaeneo.Infrastructure.Balanza;

internal abstract class LectorBalanzaBase : IScaleReader
{
    private readonly Channel<string> _canal;
    private readonly SeparadorTramas _separador;

    protected LectorBalanzaBase(DelimitadorTrama delimitador = DelimitadorTrama.FinDeLinea)
    {
        _separador = new SeparadorTramas(delimitador);

        var opciones = new BoundedChannelOptions(ReglasBalanza.CapacidadCanalTramas)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = true
        };
        _canal = Channel.CreateBounded<string>(opciones);
    }

    public abstract string Descripcion { get; }

    public abstract Task ConectarAsync(CancellationToken cancellationToken);

    public async ValueTask<string> LeerTramaAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _canal.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ChannelClosedException ex)
        {
            if (ex.InnerException is ConexionBalanzaException cbe)
            {
                throw cbe;
            }
            throw new ConexionBalanzaException("Se perdió la conexión con la balanza.", ex);
        }
    }

    public abstract ValueTask DisposeAsync();

    protected void EntregarBytes(byte[] buffer, int cantidad)
    {
        var tramas = _separador.Agregar(buffer, cantidad);
        foreach (var trama in tramas)
        {
            _canal.Writer.TryWrite(trama);
        }
    }

    protected void EntregarTrama(string trama)
    {
        _canal.Writer.TryWrite(trama);
    }

    protected void Finalizar(Exception? error)
    {
        _canal.Writer.TryComplete(error);
    }
}
