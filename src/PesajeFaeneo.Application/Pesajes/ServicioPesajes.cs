using System.Globalization;
using Microsoft.Extensions.Logging;
using PesajeFaeneo.Application.Abstracciones.Persistencia;
using PesajeFaeneo.Application.Auditoria;
using PesajeFaeneo.Application.Balanza;
using PesajeFaeneo.Application.Comun;
using PesajeFaeneo.Application.Licencias;
using PesajeFaeneo.Application.Parametros;
using PesajeFaeneo.Application.Pesajes.Dtos;
using PesajeFaeneo.Application.Seguridad;
using PesajeFaeneo.Domain.Entities;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Domain.Excepciones;
using PesajeFaeneo.Domain.Reglas;

namespace PesajeFaeneo.Application.Pesajes;

public class ServicioPesajes : IServicioPesajes
{
    private readonly IUnidadDeTrabajoFactory _uowFactory;
    private readonly ISesionActual _sesionActual;
    private readonly IServicioBalanza _servicioBalanza;
    private readonly IServicioLicencia _licencia;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ServicioPesajes> _logger;

    public ServicioPesajes(
        IUnidadDeTrabajoFactory uowFactory,
        ISesionActual sesionActual,
        IServicioBalanza servicioBalanza,
        IServicioLicencia licencia,
        TimeProvider timeProvider,
        ILogger<ServicioPesajes> logger)
    {
        _uowFactory = uowFactory ?? throw new ArgumentNullException(nameof(uowFactory));
        _sesionActual = sesionActual ?? throw new ArgumentNullException(nameof(sesionActual));
        _servicioBalanza = servicioBalanza ?? throw new ArgumentNullException(nameof(servicioBalanza));
        _licencia = licencia ?? throw new ArgumentNullException(nameof(licencia));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<SugerenciaSecuencia> SugerirSecuenciaAsync(int ganaderoId, CancellationToken cancellationToken = default)
    {
        _sesionActual.ExigirPermiso(Permiso.Pesar);

        if (ganaderoId <= 0)
        {
            throw new ValidacionException(CamposPesaje.Ganadero, "Seleccione el ganadero.");
        }

        var rango = RangoDiaLocal.DeHoy(_timeProvider);
        await using var uow = await _uowFactory.CrearAsync(cancellationToken);
        var max = await uow.Pesajes.ObtenerSecuenciaMaximaAsync(ganaderoId, rango.DesdeUtc, rango.HastaUtc, cancellationToken);
        var cuenta = await uow.Pesajes.ContarVigentesAsync(ganaderoId, rango.DesdeUtc, rango.HastaUtc, cancellationToken);

        return new SugerenciaSecuencia(ReglasPesaje.SugerirSecuencia(max), cuenta);
    }

    public async Task<IReadOnlyList<PesajeResumen>> ListarDelDiaAsync(CancellationToken cancellationToken = default)
    {
        _sesionActual.ExigirPermiso(Permiso.Pesar);

        var rango = RangoDiaLocal.DeHoy(_timeProvider);
        return await ListarRangoAsync(rango, ReglasPesaje.PesajesDelDiaMaximo, cancellationToken);
    }

    public async Task<IReadOnlyList<PesajeResumen>> ListarPorDiaAsync(DateOnly dia, CancellationToken cancellationToken = default)
    {
        _sesionActual.ExigirPermiso(Permiso.Pesar);

        var rango = RangoDiaLocal.DelDia(dia, _timeProvider.LocalTimeZone);
        return await ListarRangoAsync(rango, ReglasPesaje.HistorialMaximo, cancellationToken);
    }

    private async Task<IReadOnlyList<PesajeResumen>> ListarRangoAsync(RangoUtc rango, int maximo, CancellationToken cancellationToken)
    {
        await using var uow = await _uowFactory.CrearAsync(cancellationToken);
        var lista = await uow.Pesajes.ListarPorRangoAsync(rango.DesdeUtc, rango.HastaUtc, maximo, cancellationToken);

        return lista.Select(p => new PesajeResumen(
            p.Id,
            p.NumeroBoleta,
            p.FechaHora,
            p.Ganadero.Codigo,
            p.Ganadero.Nombre,
            p.Secuencia,
            p.Cliente.Nombre,
            p.TipoDesposte,
            p.PesoIzquierdo,
            p.PesoDerecho,
            p.PesoTotal,
            p.Estado,
            p.OrigenPeso,
            p.Fletero?.Nombre
        )).ToList().AsReadOnly();
    }

    public async Task<PesajeRegistrado> RegistrarAsync(RegistrarPesaje dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        _sesionActual.ExigirPermiso(Permiso.Pesar);
        var sesion = _sesionActual.ExigirSesion();

        // F13 (D4): en modo consulta no se graban boletas. Va antes de validar y de abrir cualquier transacción.
        _licencia.ExigirOperacion();

        var (n, errores) = ValidadorPesaje.Validar(dto);
        if (errores.Count > 0)
        {
            throw new ValidacionException(errores);
        }

        var instantanea = _servicioBalanza.Instantanea;
        var origen = (instantanea.EsSimulador || dto.CapturadoConSimulador) ? OrigenPeso.Simulador : OrigenPeso.Balanza;

        if (origen == OrigenPeso.Simulador && !_sesionActual.TienePermiso(Permiso.ConfigurarBalanza))
        {
            throw new ReglaNegocioException("PES_SIMULADOR", "La balanza está en modo simulador. Solo un administrador puede grabar pesajes simulados.");
        }

        await using var uow = await _uowFactory.CrearAsync(cancellationToken);

        var ganadero = await uow.Ganaderos.ObtenerPorIdAsync(n.GanaderoId, cancellationToken);
        if (ganadero == null || !ganadero.Activo)
        {
            throw new ValidacionException(CamposPesaje.Ganadero, "El ganadero seleccionado no existe o está desactivado.");
        }

        var cliente = await uow.Clientes.ObtenerPorIdAsync(n.ClienteId, cancellationToken);
        if (cliente == null || !cliente.Activo)
        {
            throw new ValidacionException(CamposPesaje.Cliente, "El cliente seleccionado no existe o está desactivado.");
        }

        // F11 (D7): defensa en profundidad. La pantalla ya no ofrece un tipo que el modo no permite, pero el modo puede
        // haber cambiado con la pantalla abierta. Se valida antes de la transacción (la reserva del número debe ser su
        // primera sentencia), igual que ganadero y cliente.
        var parametros = await uow.Parametros.ObtenerAsync(cancellationToken);
        var modo = ReglasDesposte.Efectivo(parametros.ModoDesposte);
        if (!ReglasDesposte.Permite(modo, n.TipoDesposte))
        {
            throw new ReglaNegocioException(
                "PES_DESPOSTE_NO_PERMITIDO",
                $"El tipo de pesaje configurado es «{ReglasDesposte.Texto(modo)}». Cierre y vuelva a abrir la pantalla de pesaje.");
        }

        // F10 (O5): el fletero es opcional; si se indica debe existir y estar activo (como ganadero y cliente).
        Fletero? fletero = null;
        if (n.FleteroId is int fleteroId)
        {
            fletero = await uow.Fleteros.ObtenerPorIdAsync(fleteroId, cancellationToken);
            if (fletero == null || !fletero.Activo)
            {
                throw new ValidacionException(CamposPesaje.Fletero, "El fletero seleccionado no existe o está desactivado.");
            }
        }

        var ahora = _timeProvider.GetUtcNow().UtcDateTime;

        await using var tx = await uow.IniciarTransaccionAsync(cancellationToken);

        var numero = await uow.Parametros.ReservarNumeroBoletaAsync(cancellationToken);
        if (numero > ReglasParametros.SiguienteNumeroBoletaMaximo)
        {
            throw new ReglaNegocioException("PES_NUMERACION_AGOTADA", "Se agotó la numeración de boletas. Revise los parámetros del sistema.");
        }

        var pesaje = new Pesaje
        {
            NumeroBoleta = numero,
            FechaHora = ahora,
            GanaderoId = n.GanaderoId,
            ClienteId = n.ClienteId,
            FleteroId = fletero?.Id,
            Direccion = n.Direccion ?? string.Empty,
            Secuencia = n.Secuencia,
            ColorRaza = n.ColorRaza ?? string.Empty,
            Sexo = n.Sexo,
            TipoDesposte = n.TipoDesposte,
            PesoIzquierdo = n.PesoIzquierdo,
            PesoDerecho = n.PesoDerecho,
            PesoTotal = ReglasPesaje.CalcularTotal(n.PesoIzquierdo, n.PesoDerecho),
            Observacion = n.Observacion ?? string.Empty,
            Estado = EstadoPesaje.Vigente,
            OrigenPeso = origen,
            UsuarioId = sesion.Id,
            CreadoEn = ahora,
            ModificadoEn = null
        };

        uow.Pesajes.Agregar(pesaje);

        try
        {
            await uow.GuardarCambiosAsync(cancellationToken);
        }
        catch (ConflictoException ex)
        {
            throw new ReglaNegocioException("PES_NUMERO_DUPLICADO", $"El número de boleta {numero} ya existe. Revise 'Siguiente número de boleta' en Parámetros del sistema.", ex);
        }

        var auditoria = FabricaAuditoria.Crear(
            _timeProvider,
            sesion.Id,
            AccionAuditoria.Crear,
            EntidadesAuditoria.Pesaje,
            pesaje.Id.ToString(CultureInfo.InvariantCulture),
            despues: FabricaAuditoria.CrearSnapshot(pesaje),
            motivo: $"Alta de pesaje boleta {numero}");

        uow.Auditoria.Registrar(auditoria);
        await uow.GuardarCambiosAsync(cancellationToken);
        await tx.ConfirmarAsync(cancellationToken);

        _logger.LogInformation(
            "Pesaje registrado: boleta {Numero}, Id {Id}, ganadero {GanaderoId}, cliente {ClienteId}, total {Total} kg, origen {Origen}, usuario {Usuario}",
            numero, pesaje.Id, n.GanaderoId, n.ClienteId, pesaje.PesoTotal, origen, sesion.NombreUsuario);

        return new PesajeRegistrado(pesaje.Id, pesaje.NumeroBoleta, pesaje.FechaHora, pesaje.PesoTotal, pesaje.OrigenPeso);
    }
}
