using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using PesajeFaeneo.Application.Balanza;
using PesajeFaeneo.Application.Balanza.Dtos;
using PesajeFaeneo.Domain.Enums;

namespace PesajeFaeneo.Infrastructure.Balanza;

internal sealed class LectorTcp : LectorBalanzaBase
{
    private readonly ConfiguracionBalanzaDto _config;
    private readonly ILogger _logger;
    private TcpClient? _cliente;
    private NetworkStream? _stream;
    private Task? _bombeo;
    private readonly CancellationTokenSource _ctsInterno = new();
    private volatile bool _cerrando;
    private bool _disposed;

    public LectorTcp(ConfiguracionBalanzaDto config, ILogger logger)
        : base(config?.DelimitadorTrama ?? DelimitadorTrama.FinDeLinea)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override string Descripcion => DescripcionConexion.Crear(_config);

    public override async Task ConectarAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _cliente = new TcpClient { NoDelay = true };
        _cliente.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

        try
        {
            await _cliente.ConnectAsync(_config.HostTcp, _config.PuertoTcp, cancellationToken).ConfigureAwait(false);
        }
        catch (SocketException ex)
        {
            throw new ConexionBalanzaException($"No se pudo conectar a {_config.HostTcp}:{_config.PuertoTcp} ({ex.SocketErrorCode}).", ex);
        }

        _stream = _cliente.GetStream();
        _bombeo = Task.Run(BombearAsync);
    }

    public override async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _cerrando = true;

        _ctsInterno.Cancel();

        try
        {
            _stream?.Dispose();
        }
        catch
        {
            // Ignorar
        }

        try
        {
            _cliente?.Dispose();
        }
        catch
        {
            // Ignorar
        }

        if (_bombeo != null)
        {
            try
            {
                await _bombeo.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
            }
            catch
            {
                // Ignorar
            }
        }

        _ctsInterno.Dispose();
        Finalizar(null);
    }

    private async Task BombearAsync()
    {
        var buffer = new byte[512];
        while (!_cerrando)
        {
            try
            {
                int n = await _stream!.ReadAsync(buffer.AsMemory(0, buffer.Length), _ctsInterno.Token).ConfigureAwait(false);
                if (n == 0)
                {
                    Finalizar(new ConexionBalanzaException("La balanza cerró la conexión."));
                    break;
                }

                EntregarBytes(buffer, n);
            }
            catch (OperationCanceledException) when (_cerrando)
            {
                break;
            }
            catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException)
            {
                if (_cerrando) break;
                Finalizar(new ConexionBalanzaException($"Se perdió la conexión con {_config.HostTcp}:{_config.PuertoTcp}.", ex));
                break;
            }
            catch (Exception ex)
            {
                if (_cerrando) break;
                _logger.LogError(ex, "Error inesperado en bombeo TCP.");
                Finalizar(new ConexionBalanzaException($"Error inesperado en conexión TCP con {_config.HostTcp}:{_config.PuertoTcp}.", ex));
                break;
            }
        }
    }
}
