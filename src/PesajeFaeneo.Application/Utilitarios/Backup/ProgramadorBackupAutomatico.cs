namespace PesajeFaeneo.Application.Utilitarios.Backup;

using Microsoft.Extensions.Logging;
using PesajeFaeneo.Application.Utilitarios.Backup.Dtos;
using PesajeFaeneo.Domain.Excepciones;

public sealed class ProgramadorBackupAutomatico : IProgramadorBackupAutomatico, IDisposable
{
    public static readonly TimeSpan PrimeraRevision = TimeSpan.FromMinutes(2);
    public static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(60);

    private readonly IMotorBackup _motor;
    private readonly IAlmacenConfiguracionBackup _almacen;
    private readonly TimeProvider _tiempo;
    private readonly ILogger<ProgramadorBackupAutomatico> _logger;

    private readonly object _cerrojo = new();
    private CancellationTokenSource? _cts;
    private Task? _bucle;
    private ResultadoBackupAutomatico? _ultimoResultado;
    private readonly TaskCompletionSource _temporizadorListo = new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal Task TemporizadorListoParaTest => _temporizadorListo.Task;

    public ResultadoBackupAutomatico? UltimoResultado => Volatile.Read(ref _ultimoResultado);

    public ProgramadorBackupAutomatico(
        IMotorBackup motor,
        IAlmacenConfiguracionBackup almacen,
        TimeProvider tiempo,
        ILogger<ProgramadorBackupAutomatico> logger)
    {
        _motor = motor ?? throw new ArgumentNullException(nameof(motor));
        _almacen = almacen ?? throw new ArgumentNullException(nameof(almacen));
        _tiempo = tiempo ?? throw new ArgumentNullException(nameof(tiempo));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void Iniciar()
    {
        lock (_cerrojo)
        {
            if (_bucle is not null) return;
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            _bucle = Task.Run(() => BucleAsync(token));
        }
    }

    private async Task BucleAsync(CancellationToken ct)
    {
        using var temporizador = new PeriodicTimer(PrimeraRevision, _tiempo);
        _temporizadorListo.TrySetResult();
        try
        {
            while (await temporizador.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                temporizador.Period = Intervalo;
                await EjecutarCicloAsync(ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Detención normal.
        }
    }

    public async Task DetenerAsync()
    {
        Task? bucle;
        lock (_cerrojo)
        {
            bucle = _bucle;
            _cts?.Cancel();
            _bucle = null;
        }

        if (bucle is not null)
        {
            await bucle.ConfigureAwait(false);
        }
    }

    internal async Task<ResultadoBackupAutomatico?> EjecutarCicloAsync(CancellationToken ct)
    {
        var config = await _almacen.LeerAsync(ct).ConfigureAwait(false);
        if (!config.AutomaticoActivo)
        {
            return null;
        }

        var hoy = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(_tiempo.GetUtcNow().UtcDateTime, _tiempo.LocalTimeZone));
        if (await _motor.ExisteAutomaticoDelDiaAsync(hoy, ct).ConfigureAwait(false))
        {
            return null;
        }

        return await CrearAsync(config, ct).ConfigureAwait(false);
    }

    public async Task<ResultadoBackupAutomatico?> EjecutarAlCerrarAsync(CancellationToken cancellationToken = default)
    {
        var config = await _almacen.LeerAsync(cancellationToken).ConfigureAwait(false);
        if (!config.AutomaticoActivo || !config.AlCerrar)
        {
            return null;
        }

        return await CrearAsync(config, cancellationToken).ConfigureAwait(false);
    }

    private async Task<ResultadoBackupAutomatico> CrearAsync(ConfiguracionBackup config, CancellationToken ct)
    {
        var ahora = _tiempo.GetUtcNow().UtcDateTime;
        ResultadoBackupAutomatico resultado;
        try
        {
            var r = await _motor.CrearAsync(
                new SolicitudBackup(TipoBackup.Automatico, config.DiasRetencion, config.CarpetaSecundaria),
                ct).ConfigureAwait(false);

            resultado = new ResultadoBackupAutomatico(ahora, true, r.Backup.NombreArchivo, r.AdvertenciaCopiaSecundaria);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falló el backup automático.");
            resultado = new ResultadoBackupAutomatico(
                ahora,
                false,
                null,
                ex is DomainException ? ex.Message : "Error inesperado; revise el log.");
        }

        Volatile.Write(ref _ultimoResultado, resultado);
        return resultado;
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }
}
