using Microsoft.Extensions.Logging;
using PesajeFaeneo.Application.Abstracciones.Persistencia;
using PesajeFaeneo.Application.Balanza.Dtos;
using PesajeFaeneo.Application.Seguridad;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Domain.Excepciones;

namespace PesajeFaeneo.Application.Balanza;

public sealed class ServicioBalanza : IServicioBalanza, IDisposable, IAsyncDisposable
{
    private readonly IUnidadDeTrabajoFactory _uowFactory;
    private readonly IFabricaLectorBalanza _fabrica;
    private readonly ISesionActual _sesion;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ServicioBalanza> _logger;

    private InstantaneaBalanza _instantanea = InstantaneaBalanza.Inicial;
    private long _secuencia;
    private readonly SemaphoreSlim _control = new(1, 1);
    private CancellationTokenSource? _cts;
    private Task? _bucle;

    private readonly object _tramasLock = new();
    private readonly Queue<string> _tramasRecientes = new();
    private string? _ultimoMensajeRegistrado;
    private int _disposed;

    public ServicioBalanza(
        IUnidadDeTrabajoFactory uowFactory,
        IFabricaLectorBalanza fabrica,
        ISesionActual sesion,
        TimeProvider timeProvider,
        ILogger<ServicioBalanza> logger)
    {
        _uowFactory = uowFactory ?? throw new ArgumentNullException(nameof(uowFactory));
        _fabrica = fabrica ?? throw new ArgumentNullException(nameof(fabrica));
        _sesion = sesion ?? throw new ArgumentNullException(nameof(sesion));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public InstantaneaBalanza Instantanea => Volatile.Read(ref _instantanea);

    public IReadOnlyList<string> ObtenerTramasRecientes()
    {
        lock (_tramasLock)
        {
            return _tramasRecientes.ToList();
        }
    }

    public async Task IniciarAsync(CancellationToken cancellationToken = default)
    {
        LanzarSiDisposed();
        await _control.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            LanzarSiDisposed();
            if (_bucle != null && !_bucle.IsCompleted)
            {
                return;
            }

            await IniciarInternoAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _control.Release();
        }
    }

    public async Task DetenerAsync(CancellationToken cancellationToken = default)
    {
        LanzarSiDisposed();
        await _control.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            LanzarSiDisposed();
            await DetenerInternoAsync().ConfigureAwait(false);
        }
        finally
        {
            _control.Release();
        }
    }

    public async Task ReiniciarAsync(CancellationToken cancellationToken = default)
    {
        // Defensa en profundidad: los llamadores actuales (FrmConfiguracionBalanza y
        // ServicioConfiguracionBalanza.ActualizarAsync) ya exigen este permiso, pero se repite aquí
        // porque este método puede invocarse directamente (p. ej. botón "Reconectar").
        _sesion.ExigirPermiso(Permiso.ConfigurarBalanza);

        LanzarSiDisposed();
        await _control.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            LanzarSiDisposed();
            await DetenerInternoAsync().ConfigureAwait(false);
            await IniciarInternoAsync(cancellationToken, trasDetenerLectorPropio: true).ConfigureAwait(false);
        }
        finally
        {
            _control.Release();
        }
    }

    public async Task<ResultadoPruebaBalanza> ProbarAsync(
        ConfiguracionBalanzaDto candidata,
        TimeSpan duracion,
        IProgress<AvancePruebaBalanza>? progreso,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidata);
        _sesion.ExigirPermiso(Permiso.ConfigurarBalanza);

        var candidataNormalizada = ValidadorConfiguracionBalanza.Normalizar(candidata);
        var errores = ValidadorConfiguracionBalanza.Validar(candidataNormalizada);
        if (errores.Count > 0)
        {
            throw new ValidacionException(errores);
        }

        LanzarSiDisposed();
        try
        {
            await _control.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return new ResultadoPruebaBalanza(false, null, 0, 0, null, false, Cancelada: true);
        }

        bool estabaIniciado = false;
        try
        {
            LanzarSiDisposed();
            estabaIniciado = _bucle != null && !_bucle.IsCompleted;
            await DetenerInternoAsync().ConfigureAwait(false);

            var descCandidata = DescripcionConexion.Crear(candidataNormalizada);
            var esSimulador = candidataNormalizada.TipoConexion == TipoConexion.Simulador;
            Publicar(EstadoBalanza.EnDiagnostico, descCandidata, esSimulador, mensaje: "Probando configuración…");

            if (candidataNormalizada.TipoConexion == TipoConexion.Serial)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(ReglasBalanza.EsperaTrasCerrarPuertoMs), _timeProvider, CancellationToken.None).ConfigureAwait(false);
            }

            return await EjecutarPruebaAsync(candidataNormalizada, duracion, progreso, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (estabaIniciado)
            {
                try
                {
                    // IniciarInternoAsync aplica la pausa tras cerrar el puerto serie si corresponde (D15):
                    // aquí siempre se acaba de cerrar el lector principal (y, si era serie, el de la
                    // candidata) en DetenerInternoAsync/EjecutarPruebaAsync.
                    await IniciarInternoAsync(CancellationToken.None, trasDetenerLectorPropio: true).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "No se pudo reanudar la balanza tras la prueba.");
                    var desc = _instantanea.Descripcion;
                    Publicar(EstadoBalanza.Error, desc, _instantanea.EsSimulador, mensaje: $"No se pudo reanudar la balanza: {ex.Message}");
                }
            }
            else
            {
                var actual = _instantanea;
                Publicar(EstadoBalanza.Detenida, actual.Descripcion, actual.EsSimulador, mensaje: null);
            }

            _control.Release();
        }
    }

    private void LanzarSiDisposed()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, this);
    }

    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        // Se adquiere el mismo semáforo que protege Iniciar/Detener/Reiniciar/ProbarAsync para no
        // cancelar ni liberar recursos mientras una de esas operaciones está en curso.
        await _control.WaitAsync().ConfigureAwait(false);
        try
        {
            _cts?.Cancel();
            if (_bucle != null)
            {
                try
                {
                    await _bucle.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // ignorar excepciones al esperar la terminación en Dispose
                }
            }

            _cts?.Dispose();
            _cts = null;
        }
        finally
        {
            _control.Release();
        }

        _control.Dispose();
    }

    /// <param name="trasDetenerLectorPropio">
    /// True cuando este arranque sigue inmediatamente a un DetenerInternoAsync/EjecutarPruebaAsync propio
    /// (ReiniciarAsync, o el resume de ProbarAsync): en ese caso, si la configuración es serie, el puerto
    /// COM que acabamos de cerrar puede tardar en liberarse en el SO (D15). No se aplica en el primer
    /// arranque (IniciarAsync) porque ahí no se cerró ningún puerto propio previamente.
    /// </param>
    private async Task IniciarInternoAsync(CancellationToken ct, bool trasDetenerLectorPropio = false)
    {
        ConfiguracionBalanzaDto config;
        await using (var uow = await _uowFactory.CrearAsync(ct).ConfigureAwait(false))
        {
            var entidad = await uow.ConfiguracionBalanza.ObtenerAsync(ct).ConfigureAwait(false);
            config = MapeoBalanza.ADto(entidad);
        }

        if (trasDetenerLectorPropio && config.TipoConexion == TipoConexion.Serial)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(ReglasBalanza.EsperaTrasCerrarPuertoMs), _timeProvider, ct).ConfigureAwait(false);
        }

        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        _bucle = Task.Run(() => BucleProtegidoAsync(config, token));
    }

    private async Task DetenerInternoAsync()
    {
        if (_bucle == null)
        {
            return;
        }

        _cts?.Cancel();
        try
        {
            // Tiempo real sin TimeProvider: salvaguarda de cierre del bucle
            await _bucle.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "El lector de balanza no se detuvo a tiempo");
        }
        catch (OperationCanceledException)
        {
            // Cancelación esperada
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
            _bucle = null;
            var actual = _instantanea;
            Publicar(EstadoBalanza.Detenida, actual.Descripcion, actual.EsSimulador, mensaje: null);
        }
    }

    private async Task BucleProtegidoAsync(ConfiguracionBalanzaDto config, CancellationToken ct)
    {
        try
        {
            await BucleAsync(config, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Cancelación normal, salir en silencio
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error interno del lector de balanza.");
            var desc = DescripcionConexion.Crear(config);
            Publicar(EstadoBalanza.Error, desc, config.TipoConexion == TipoConexion.Simulador, mensaje: "Error interno del lector de balanza. Revise el log.");
        }
    }

    private async Task BucleAsync(ConfiguracionBalanzaDto config, CancellationToken ct)
    {
        var descripcion = DescripcionConexion.Crear(config);
        var esSimulador = config.TipoConexion == TipoConexion.Simulador;

        var errorPatron = ParserTramas.ValidarPatron(config.PatronRegex);
        if (errorPatron != null)
        {
            _logger.LogError("Patrón de lectura inválido: {Error}", errorPatron);
            Publicar(EstadoBalanza.Error, descripcion, esSimulador, mensaje: $"Patrón de lectura inválido: {errorPatron}");
            return;
        }

        var parser = new ParserTramas(config.PatronRegex, config.UnidadPeso);
        var detector = new DetectorEstabilidad(config.LecturasParaEstable, config.ToleranciaKg);
        var timeout = TimeSpan.FromMilliseconds(config.TimeoutMs);
        int intento = 0;

        while (!ct.IsCancellationRequested)
        {
            var mensajeConectando = intento == 0 ? null : $"Reintentando conexión (intento {intento + 1})…";
            Publicar(EstadoBalanza.Conectando, descripcion, esSimulador, mensaje: mensajeConectando);
            detector.Reiniciar();

            IScaleReader lector = _fabrica.Crear(config);
            try
            {
                // --- conexión con tiempo límite ---
                using var ctsConexion = new CancellationTokenSource(timeout, _timeProvider);
                using var enlazado = CancellationTokenSource.CreateLinkedTokenSource(ct, ctsConexion.Token);
                try
                {
                    await lector.ConectarAsync(enlazado.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    throw new ConexionBalanzaException($"Tiempo de espera agotado al conectar con {descripcion}.");
                }

                _logger.LogInformation("Balanza conectada ({Descripcion})", descripcion);
                _ultimoMensajeRegistrado = null;
                Publicar(EstadoBalanza.Conectada, descripcion, esSimulador);

                int vencimientosSeguidos = 0;
                int tramasInvalidasSeguidas = 0;

                // --- lectura ---
                while (true)
                {
                    using var ctsTrama = new CancellationTokenSource(timeout, _timeProvider);
                    using var enlazadoTrama = CancellationTokenSource.CreateLinkedTokenSource(ct, ctsTrama.Token);
                    string trama;
                    try
                    {
                        trama = await lector.LeerTramaAsync(enlazadoTrama.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                    {
                        vencimientosSeguidos++;
                        detector.Reiniciar();

                        // D6: el estado SinDatos solo se publica tras 3 vencimientos consecutivos, no ante
                        // un único corte transitorio; al alcanzar el umbral se fuerza la reconexión.
                        if (vencimientosSeguidos < ReglasBalanza.TimeoutsAntesDeReconectar)
                        {
                            continue;
                        }

                        const string mensajeSinDatos = "La balanza no envía datos; se reintentará la conexión.";
                        RegistrarFallo(mensajeSinDatos, new ConexionBalanzaException(mensajeSinDatos) { EsSinDatos = true });
                        Publicar(EstadoBalanza.SinDatos, descripcion, esSimulador, mensaje: mensajeSinDatos);
                        throw new ConexionBalanzaException(mensajeSinDatos) { EsSinDatos = true };
                    }

                    vencimientosSeguidos = 0;
                    intento = 0;
                    RegistrarTramaReciente(trama);
                    var ahora = _timeProvider.GetUtcNow();
                    var r = parser.Parsear(trama);

                    if (!r.Valida)
                    {
                        tramasInvalidasSeguidas++;

                        // D6: TramasNoReconocidas se publica tras 3 tramas inválidas consecutivas, no ante
                        // la primera (que puede ser ruido puntual de línea).
                        if (tramasInvalidasSeguidas >= ReglasBalanza.TramasInvalidasConsecutivasParaAlerta)
                        {
                            detector.Reiniciar();
                            Publicar(EstadoBalanza.TramasNoReconocidas, descripcion, esSimulador, mensaje: r.Error);
                        }
                        continue;
                    }

                    tramasInvalidasSeguidas = 0;
                    var estable = detector.Agregar(r.PesoKg!.Value, r.EstableReportado);
                    Publicar(EstadoBalanza.Conectada, descripcion, esSimulador, peso: r.PesoKg, estable: estable, instante: ahora);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (ConexionBalanzaException ex)
            {
                if (!ex.EsSinDatos)
                {
                    // El caso EsSinDatos ya registró el log y publicó EstadoBalanza.SinDatos justo antes de
                    // lanzar (D6); no se sobrescribe aquí con EstadoBalanza.Error.
                    RegistrarFallo(ex.Message, ex);
                    Publicar(EstadoBalanza.Error, descripcion, esSimulador, mensaje: ex.Message);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                RegistrarFallo("Error de comunicación con la balanza.", ex);
                Publicar(EstadoBalanza.Error, descripcion, esSimulador, mensaje: "Error de comunicación con la balanza.");
            }
            finally
            {
                await DesecharSeguroAsync(lector).ConfigureAwait(false);
            }

            if (ct.IsCancellationRequested) break;

            int indiceEspera = Math.Min(intento, ReglasBalanza.EsperasReconexionSegundos.Count - 1);
            int segundos = ReglasBalanza.EsperasReconexionSegundos[indiceEspera];
            var espera = TimeSpan.FromSeconds(segundos);
            if (config.TipoConexion == TipoConexion.Serial)
            {
                var esperaMinimaSerial = TimeSpan.FromMilliseconds(ReglasBalanza.EsperaTrasCerrarPuertoMs);
                if (espera < esperaMinimaSerial)
                {
                    espera = esperaMinimaSerial;
                }
            }
            intento++;
            try
            {
                await Task.Delay(espera, _timeProvider, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task<ResultadoPruebaBalanza> EjecutarPruebaAsync(
        ConfiguracionBalanzaDto candidata,
        TimeSpan duracion,
        IProgress<AvancePruebaBalanza>? progreso,
        CancellationToken ct)
    {
        var descripcion = DescripcionConexion.Crear(candidata);
        var lector = _fabrica.Crear(candidata);
        var parser = new ParserTramas(candidata.PatronRegex, candidata.UnidadPeso);
        var detector = new DetectorEstabilidad(candidata.LecturasParaEstable, candidata.ToleranciaKg);
        var timeout = TimeSpan.FromMilliseconds(candidata.TimeoutMs);

        using var ctsDuracion = new CancellationTokenSource(duracion, _timeProvider);
        using var enlazadoGlobal = CancellationTokenSource.CreateLinkedTokenSource(ct, ctsDuracion.Token);

        bool conecto = false;
        string? errorConexion = null;
        int tramasRecibidas = 0;
        int tramasValidas = 0;
        decimal? ultimoPeso = null;
        bool alcanzoEstabilidad = false;

        try
        {
            using var ctsConectar = new CancellationTokenSource(timeout, _timeProvider);
            using var enlazadoConectar = CancellationTokenSource.CreateLinkedTokenSource(enlazadoGlobal.Token, ctsConectar.Token);
            try
            {
                await lector.ConectarAsync(enlazadoConectar.Token).ConfigureAwait(false);
                conecto = true;
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested && !ctsDuracion.IsCancellationRequested)
            {
                throw new ConexionBalanzaException($"Tiempo de espera agotado al conectar con {descripcion}.");
            }

            while (!enlazadoGlobal.Token.IsCancellationRequested)
            {
                using var ctsTrama = new CancellationTokenSource(timeout, _timeProvider);
                using var enlazadoTrama = CancellationTokenSource.CreateLinkedTokenSource(enlazadoGlobal.Token, ctsTrama.Token);
                string trama;
                try
                {
                    trama = await lector.LeerTramaAsync(enlazadoTrama.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested && !ctsDuracion.IsCancellationRequested)
                {
                    continue;
                }

                tramasRecibidas++;
                RegistrarTramaReciente(trama);
                var res = parser.Parsear(trama);
                bool estable = false;
                if (res.Valida)
                {
                    tramasValidas++;
                    ultimoPeso = res.PesoKg;
                    estable = detector.Agregar(res.PesoKg!.Value, res.EstableReportado);
                    if (estable)
                    {
                        alcanzoEstabilidad = true;
                    }
                }

                progreso?.Report(new AvancePruebaBalanza(TramaVisible.Formatear(trama), res, estable));
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return new ResultadoPruebaBalanza(
                conecto,
                errorConexion,
                tramasRecibidas,
                tramasValidas,
                ultimoPeso,
                alcanzoEstabilidad,
                Cancelada: true);
        }
        catch (OperationCanceledException) when (ctsDuracion.IsCancellationRequested)
        {
            // Duración vencida normalmente
        }
        catch (ConexionBalanzaException ex)
        {
            errorConexion = ex.Message;
        }
        catch (Exception ex)
        {
            errorConexion = ex.Message;
        }
        finally
        {
            await DesecharSeguroAsync(lector).ConfigureAwait(false);
        }

        return new ResultadoPruebaBalanza(
            Conecto: conecto,
            Error: errorConexion,
            TramasRecibidas: tramasRecibidas,
            TramasValidas: tramasValidas,
            UltimoPesoKg: ultimoPeso,
            AlcanzoEstabilidad: alcanzoEstabilidad,
            Cancelada: false);
    }

    private void Publicar(
        EstadoBalanza estado,
        string descripcion,
        bool esSimulador,
        decimal? peso = null,
        bool estable = false,
        DateTimeOffset? instante = null,
        string? mensaje = null)
    {
        if (estado != EstadoBalanza.Conectada)
        {
            peso = null;
            estable = false;
            instante = null;
        }
        else if (peso == null)
        {
            estable = false;
        }

        var sec = Interlocked.Increment(ref _secuencia);
        var nueva = new InstantaneaBalanza(
            sec,
            estado,
            peso,
            estable,
            instante,
            mensaje,
            descripcion,
            esSimulador);

        Volatile.Write(ref _instantanea, nueva);
    }

    private void RegistrarTramaReciente(string trama)
    {
        var visible = TramaVisible.Formatear(trama);
        _logger.LogDebug("Trama balanza recibida: {Trama}", visible);
        lock (_tramasLock)
        {
            if (_tramasRecientes.Count >= ReglasBalanza.TramasRecientesMaximo)
            {
                _tramasRecientes.Dequeue();
            }
            _tramasRecientes.Enqueue(visible);
        }
    }

    private void RegistrarFallo(string mensaje, Exception ex)
    {
        if (_ultimoMensajeRegistrado != mensaje)
        {
            _ultimoMensajeRegistrado = mensaje;
            _logger.LogWarning(ex, "Fallo en conexión con balanza: {Mensaje}", mensaje);
        }
    }

    private async Task DesecharSeguroAsync(IScaleReader? lector)
    {
        if (lector == null) return;
        try
        {
            await lector.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al cerrar el lector de balanza.");
        }
    }
}
