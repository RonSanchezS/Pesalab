using Microsoft.Extensions.Logging;
using PesajeFaeneo.Domain.Excepciones;

namespace PesajeFaeneo.Application.Licencias;

public sealed class ServicioLicencia : IServicioLicencia
{
    public const string CampoClave = "Clave";
    public const string CodigoModoConsulta = "LIC_MODO_CONSULTA";

    private readonly IAlmacenLicenciaLocal _almacen;
    private readonly IServidorLicencias _servidor;
    private readonly IIdentificadorEquipo _equipo;
    private readonly TimeProvider _tiempo;
    private readonly ILogger<ServicioLicencia> _logger;

    private readonly object _candado = new();
    private EstadoLicencia? _ultimoEstado;

    public ServicioLicencia(
        IAlmacenLicenciaLocal almacen,
        IServidorLicencias servidor,
        IIdentificadorEquipo equipo,
        TimeProvider tiempo,
        ILogger<ServicioLicencia> logger)
    {
        _almacen = almacen ?? throw new ArgumentNullException(nameof(almacen));
        _servidor = servidor ?? throw new ArgumentNullException(nameof(servidor));
        _equipo = equipo ?? throw new ArgumentNullException(nameof(equipo));
        _tiempo = tiempo ?? throw new ArgumentNullException(nameof(tiempo));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        Hwid = _equipo.Hwid;
    }

    private string Hwid { get; }

    public event EventHandler<EstadoLicencia>? EstadoCambiado;

    public string CodigoEquipo
    {
        get
        {
            var inicio = Hwid.Length >= 8 ? Hwid[..8] : Hwid.PadRight(8, '0');
            return $"{inicio[..4]}-{inicio[4..8]}".ToUpperInvariant();
        }
    }

    public bool PermiteOperar => ObtenerEstado().PermiteOperar;

    public EstadoLicencia ObtenerEstado()
    {
        EstadoLicencia estado;
        bool cambio;
        lock (_candado)
        {
            estado = CalcularBloqueado();
            cambio = RegistrarEstadoBloqueado(estado);
        }

        if (cambio)
        {
            Notificar(estado);
        }

        return estado;
    }

    public void ExigirOperacion()
    {
        if (!ObtenerEstado().PermiteOperar)
        {
            throw new ReglaNegocioException(
                CodigoModoConsulta,
                "El sistema está en modo consulta: la prueba terminó o la licencia no está activa. Actívelo en Ayuda › Activar licencia.");
        }
    }

    public async Task<EstadoLicencia> ActivarAsync(string clave, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(clave))
        {
            throw new ValidacionException(CampoClave, "Ingrese la clave de activación.");
        }

        if (!ReglasLicencia.ClaveConFormatoValido(clave))
        {
            throw new ValidacionException(CampoClave, "La clave debe tener el formato XXXX-XXXX-XXXX-XXXX-XXXX.");
        }

        // La clave nunca se registra (ni en el log ni en la auditoría).
        var resultado = await _servidor.ActivarAsync(ReglasLicencia.FormatearClave(clave), Hwid, cancellationToken).ConfigureAwait(false);
        switch (resultado)
        {
            case ResultadoActivacionServidor.Ok:
                break;
            case ResultadoActivacionServidor.YaUsada:
                throw new ReglaNegocioException("LIC_CLAVE_USADA", "Esta clave ya fue utilizada en otro equipo.");
            case ResultadoActivacionServidor.Revocada:
                throw new ReglaNegocioException("LIC_CLAVE_REVOCADA", "Esta clave fue revocada.");
            default:
                throw new ReglaNegocioException("LIC_CLAVE_INVALIDA", "Clave de activación inválida.");
        }

        lock (_candado)
        {
            EscribirObligatorio(() => _almacen.EscribirActivacion(FirmaLicencia.TokenActivacion(Hwid)));
        }

        _logger.LogInformation("Licencia activada en este equipo.");
        return ObtenerEstado();
    }

    public async Task<EstadoLicencia> IniciarPruebaAsync(CancellationToken cancellationToken = default)
    {
        bool yaExiste;
        lock (_candado)
        {
            var datos = _almacen.Leer();
            yaExiste = ReglasLicencia.EstaActivada(datos, Hwid) || ReglasLicencia.LeerPrueba(datos, Hwid).Situacion != SituacionPrueba.Ninguna;
        }

        if (yaExiste)
        {
            // Ya hay activación o prueba (válida o alterada): no se reinicia nada.
            return ObtenerEstado();
        }

        var ahora = AhoraEpoch();

        // D6: la prueba puede iniciarse sin conexión. Si el servidor ya conoce este equipo se respeta su fecha (la más antigua).
        long inicio = ahora;
        try
        {
            if (await _servidor.RegistrarPruebaAsync(Hwid, cancellationToken).ConfigureAwait(false) is long servidor)
            {
                inicio = Math.Min(servidor, ahora);
            }
        }
        catch (ServidorLicenciasException ex)
        {
            _logger.LogInformation("La prueba se inició sin registrar en el servidor: {Mensaje}", ex.Message);
        }

        lock (_candado)
        {
            EscribirObligatorio(() => _almacen.EscribirPrueba(inicio, ahora, FirmaLicencia.Prueba(inicio, ahora, Hwid)));
        }

        return ObtenerEstado();
    }

    public async Task<EstadoLicencia> SincronizarAsync(CancellationToken cancellationToken = default)
    {
        EstadoRemotoLicencia remoto;
        try
        {
            remoto = await _servidor.ConsultarAsync(Hwid, cancellationToken).ConfigureAwait(false);
        }
        catch (ServidorLicenciasException ex)
        {
            _logger.LogInformation("No se pudo sincronizar la licencia: {Mensaje}", ex.Message);
            return ObtenerEstado();
        }

        var registrarPrueba = false;
        lock (_candado)
        {
            var datos = _almacen.Leer();
            if (ReglasLicencia.EstaActivada(datos, Hwid))
            {
                if (remoto.Clave == EstadoClaveRemota.Revocada)
                {
                    IntentarEscribir(() => _almacen.LimpiarActivacion(), "limpiar la activación revocada");
                    _logger.LogWarning("La clave de este equipo fue revocada en el servidor.");
                }
            }
            else if (remoto.Clave == EstadoClaveRemota.Activada)
            {
                // El registro local se perdió pero el servidor sabe que este equipo está activado.
                IntentarEscribir(() => _almacen.EscribirActivacion(FirmaLicencia.TokenActivacion(Hwid)), "restaurar la activación");
            }
            else
            {
                var prueba = ReglasLicencia.LeerPrueba(datos, Hwid);
                if (prueba.Situacion == SituacionPrueba.Valida)
                {
                    if (remoto.InicioPrueba is long servidor)
                    {
                        if (servidor < prueba.Inicio)
                        {
                            var ultimo = Math.Max(prueba.Ultimo, AhoraEpoch());
                            IntentarEscribir(
                                () => _almacen.EscribirPrueba(servidor, ultimo, FirmaLicencia.Prueba(servidor, ultimo, Hwid)),
                                "reconciliar la prueba");
                        }
                    }
                    else
                    {
                        registrarPrueba = true;
                    }
                }
            }
        }

        if (registrarPrueba)
        {
            try
            {
                await _servidor.RegistrarPruebaAsync(Hwid, cancellationToken).ConfigureAwait(false);
            }
            catch (ServidorLicenciasException ex)
            {
                _logger.LogInformation("No se pudo registrar la prueba en el servidor: {Mensaje}", ex.Message);
            }
        }

        return ObtenerEstado();
    }

    // ------------------------------------------------------------------ internos

    private long AhoraEpoch() => _tiempo.GetUtcNow().ToUnixTimeSeconds();

    private EstadoLicencia CalcularBloqueado()
    {
        var datos = _almacen.Leer();
        var calculo = ReglasLicencia.Calcular(datos, AhoraEpoch(), Hwid);
        if (calculo.ReescribirUltimo)
        {
            IntentarEscribir(
                () => _almacen.EscribirPrueba(calculo.Inicio, calculo.Ultimo, FirmaLicencia.Prueba(calculo.Inicio, calculo.Ultimo, Hwid)),
                "actualizar la última vez visto");
        }

        return calculo.Estado;
    }

    /// <summary>True si el estado cambió respecto del último conocido (el primero conocido no cuenta como cambio).</summary>
    private bool RegistrarEstadoBloqueado(EstadoLicencia estado)
    {
        var anterior = _ultimoEstado;
        _ultimoEstado = estado;
        return anterior is not null && anterior != estado;
    }

    private void Notificar(EstadoLicencia estado)
    {
        try
        {
            EstadoCambiado?.Invoke(this, estado);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Un suscriptor de EstadoCambiado falló.");
        }
    }

    private void IntentarEscribir(Action escritura, string accion)
    {
        try
        {
            escritura();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "No se pudo {Accion} en el almacén local de licencias.", accion);
        }
    }

    private void EscribirObligatorio(Action escritura)
    {
        try
        {
            escritura();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "No se pudo escribir en el almacén local de licencias.");
            throw new ReglaNegocioException(
                "LIC_ALMACEN",
                "No se pudo guardar la información de la licencia en este equipo. Revise los permisos del usuario de Windows.",
                ex);
        }
    }
}
