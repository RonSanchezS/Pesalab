using Microsoft.Extensions.Logging;
using PesajeFaeneo.Application.Balanza;
using PesajeFaeneo.Application.Balanza.Dtos;
using PesajeFaeneo.Domain.Enums;

namespace PesajeFaeneo.Infrastructure.Balanza;

internal sealed class LectorSerial : LectorBalanzaBase
{
    private readonly ConfiguracionBalanzaDto _config;
    private readonly ILogger _logger;
    private readonly IPuertoSerie _puerto;
    private Thread? _hilo;
    private volatile bool _cerrando;
    private bool _disposed;

    public LectorSerial(ConfiguracionBalanzaDto config, ILogger logger)
        : this(config, logger, () => new PuertoSerieWin32(config))
    {
    }

    internal LectorSerial(ConfiguracionBalanzaDto config, ILogger logger, Func<IPuertoSerie> crearPuerto)
        : base(config?.DelimitadorTrama ?? DelimitadorTrama.FinDeLinea)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _puerto = (crearPuerto ?? throw new ArgumentNullException(nameof(crearPuerto)))();
    }

    public override string Descripcion => DescripcionConexion.Crear(_config);

    public override async Task ConectarAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await Task.Run(AbrirTraduciendo, CancellationToken.None).ConfigureAwait(false);

        _hilo = new Thread(Bombear)
        {
            IsBackground = true,
            Name = "LectorBalanzaSerial",
            Priority = ThreadPriority.AboveNormal
        };
        _hilo.Start();
    }

    public override ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return ValueTask.CompletedTask;
        }

        _disposed = true;
        _cerrando = true;

        return new ValueTask(Task.Run(() =>
        {
            try
            {
                _puerto.Cerrar();
            }
            catch
            {
                // Ignorar
            }

            if (_hilo != null && _hilo.IsAlive)
            {
                if (!_hilo.Join(TimeSpan.FromSeconds(2)))
                {
                    _logger.LogWarning("El hilo de lectura serie no terminó en el tiempo esperado.");
                }
            }

            try
            {
                _puerto.Dispose();
            }
            catch
            {
                // Ignorar
            }

            Finalizar(null);
        }));
    }

    private void AbrirTraduciendo()
    {
        try
        {
            _puerto.Abrir();
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new ConexionBalanzaException($"El puerto {_puerto.Nombre} está en uso por otra aplicación o no hay permiso para abrirlo.", ex);
        }
        catch (IOException ex)
        {
            throw new ConexionBalanzaException($"No se pudo abrir el puerto {_puerto.Nombre}. Verifique que el cable o adaptador esté conectado.", ex);
        }
        catch (ArgumentException ex)
        {
            throw new ConexionBalanzaException($"El nombre de puerto '{_puerto.Nombre}' no es válido.", ex);
        }
        catch (InvalidOperationException ex)
        {
            throw new ConexionBalanzaException($"El puerto {_puerto.Nombre} ya está abierto.", ex);
        }
    }

    private void Bombear()
    {
        var buffer = new byte[512];
        while (!_cerrando)
        {
            try
            {
                int n = _puerto.Leer(buffer, 0, buffer.Length);
                if (n > 0)
                {
                    EntregarBytes(buffer, n);
                }
            }
            catch (TimeoutException)
            {
                continue;
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException or ObjectDisposedException)
            {
                if (_cerrando) break;
                Finalizar(new ConexionBalanzaException($"Se perdió la comunicación con el puerto {_puerto.Nombre}.", ex));
                break;
            }
            catch (Exception ex)
            {
                if (_cerrando) break;
                _logger.LogError(ex, "Error inesperado en lectura serie.");
                Finalizar(new ConexionBalanzaException($"Se perdió la comunicación con el puerto {_puerto.Nombre}.", ex));
                break;
            }
        }
    }
}
