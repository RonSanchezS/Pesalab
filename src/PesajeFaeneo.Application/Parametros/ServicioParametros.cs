using Microsoft.Extensions.Logging;
using PesajeFaeneo.Application.Abstracciones;
using PesajeFaeneo.Application.Abstracciones.Persistencia;
using PesajeFaeneo.Application.Auditoria;
using PesajeFaeneo.Application.Catalogos;
using PesajeFaeneo.Application.Parametros.Dtos;
using PesajeFaeneo.Application.Seguridad;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Domain.Excepciones;
using PesajeFaeneo.Domain.Reglas;

namespace PesajeFaeneo.Application.Parametros;

public class ServicioParametros : IServicioParametros
{
    private readonly IUnidadDeTrabajoFactory _uowFactory;
    private readonly ISesionActual _sesionActual;
    private readonly IAlmacenLogo _almacenLogo;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ServicioParametros> _logger;

    public ServicioParametros(
        IUnidadDeTrabajoFactory uowFactory,
        ISesionActual sesionActual,
        IAlmacenLogo almacenLogo,
        TimeProvider timeProvider,
        ILogger<ServicioParametros> logger)
    {
        _uowFactory = uowFactory ?? throw new ArgumentNullException(nameof(uowFactory));
        _sesionActual = sesionActual ?? throw new ArgumentNullException(nameof(sesionActual));
        _almacenLogo = almacenLogo ?? throw new ArgumentNullException(nameof(almacenLogo));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ParametrosDto> ObtenerAsync(CancellationToken cancellationToken = default)
    {
        _sesionActual.ExigirSesion();

        await using var uow = await _uowFactory.CrearAsync(cancellationToken);
        var p = await uow.Parametros.ObtenerAsync(cancellationToken);
        var ultimo = await uow.Parametros.ObtenerUltimoNumeroBoletaAsync(cancellationToken);
        var rutaCompleta = _almacenLogo.ObtenerRutaCompleta(p.LogoPath);

        return new ParametrosDto(
            p.NombreEmpresa,
            p.Direccion,
            p.Telefono,
            p.Ciudad,
            p.RegistroSanitario,
            p.TelefonosReclamos,
            p.ObservacionPorDefecto,
            p.LogoPath,
            rutaCompleta,
            p.SiguienteNumeroBoleta,
            ultimo,
            p.ModificadoEn);
    }

    public async Task<IdentidadEmpresa> ObtenerIdentidadEmpresaAsync(CancellationToken cancellationToken = default)
    {
        // Sin ExigirSesion a propósito: ver IServicioParametros.ObtenerIdentidadEmpresaAsync.
        await using var uow = await _uowFactory.CrearAsync(cancellationToken);
        var p = await uow.Parametros.ObtenerAsync(cancellationToken);
        return new IdentidadEmpresa(p.NombreEmpresa, _almacenLogo.ObtenerRutaCompleta(p.LogoPath));
    }

    public async Task<ModoDesposte> ObtenerModoDesposteAsync(CancellationToken cancellationToken = default)
    {
        _sesionActual.ExigirSesion();

        await using var uow = await _uowFactory.CrearAsync(cancellationToken);
        var p = await uow.Parametros.ObtenerAsync(cancellationToken);
        return ReglasDesposte.Efectivo(p.ModoDesposte);
    }

    public async Task<ConfiguracionDesposte> ObtenerConfiguracionDesposteAsync(CancellationToken cancellationToken = default)
    {
        _sesionActual.ExigirPermiso(Permiso.GestionarParametros);

        await using var uow = await _uowFactory.CrearAsync(cancellationToken);
        var p = await uow.Parametros.ObtenerAsync(cancellationToken);
        var (resCompleta, mediaRes) = await uow.Pesajes.ContarVigentesPorTipoDesposteAsync(cancellationToken);
        return new ConfiguracionDesposte(p.ModoDesposte, resCompleta, mediaRes);
    }

    public async Task CambiarModoDesposteAsync(ModoDesposte modo, CancellationToken cancellationToken = default)
    {
        _sesionActual.ExigirPermiso(Permiso.GestionarParametros);
        var sesion = _sesionActual.ExigirSesion();

        if (!ReglasDesposte.EsValido(modo))
        {
            throw new ValidacionException(CamposParametros.ModoDesposte, "Seleccione el tipo de pesaje.");
        }

        await using var uow = await _uowFactory.CrearAsync(cancellationToken);
        var p = await uow.Parametros.ObtenerAsync(cancellationToken);
        var anterior = p.ModoDesposte;

        // Desde "sin definir" se guarda y audita aunque el efectivo ya fuera Ambos: la elección queda explícita (D3).
        if (anterior == modo)
        {
            return;
        }

        p.ModoDesposte = modo;
        p.ModificadoEn = _timeProvider.GetUtcNow().UtcDateTime;

        uow.Auditoria.Registrar(FabricaAuditoria.Crear(
            _timeProvider,
            sesion.Id,
            AccionAuditoria.CambioParametros,
            EntidadesAuditoria.ParametrosSistema,
            "1",
            antes: new { ModoDesposte = anterior },
            despues: new { ModoDesposte = (ModoDesposte?)modo },
            motivo: $"Tipo de pesaje: {ReglasDesposte.Texto(anterior)} → {ReglasDesposte.Texto(modo)}"));

        await uow.GuardarCambiosAsync(cancellationToken);

        _logger.LogInformation(
            "Tipo de pesaje cambiado de {Antes} a {Despues} por {Usuario}",
            ReglasDesposte.Texto(anterior),
            ReglasDesposte.Texto(modo),
            sesion.NombreUsuario);
    }

    public async Task ActualizarAsync(ActualizarParametros dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        _sesionActual.ExigirPermiso(Permiso.GestionarParametros);
        var sesion = _sesionActual.ExigirSesion();

        var nombreEmpresa = ValidadorCatalogos.NormalizarTexto(dto.NombreEmpresa);
        var direccion = ValidadorCatalogos.NormalizarTexto(dto.Direccion);
        var telefono = ValidadorCatalogos.NormalizarTexto(dto.Telefono);
        var ciudad = ValidadorCatalogos.NormalizarTexto(dto.Ciudad);
        var registroSanitario = ValidadorCatalogos.NormalizarTexto(dto.RegistroSanitario);
        var telefonosReclamos = ValidadorCatalogos.NormalizarTexto(dto.TelefonosReclamos);
        var observacionPorDefecto = ValidadorCatalogos.NormalizarTexto(dto.ObservacionPorDefecto);

        var dtoNormalizado = dto with
        {
            NombreEmpresa = nombreEmpresa,
            Direccion = direccion,
            Telefono = telefono,
            Ciudad = ciudad,
            RegistroSanitario = registroSanitario,
            TelefonosReclamos = telefonosReclamos,
            ObservacionPorDefecto = observacionPorDefecto
        };

        var errores = ValidadorParametros.Validar(dtoNormalizado);
        if (errores.Count > 0)
        {
            throw new ValidacionException(errores);
        }

        await using var uow = await _uowFactory.CrearAsync(cancellationToken);
        var p = await uow.Parametros.ObtenerAsync(cancellationToken);
        var antes = FabricaAuditoria.CrearSnapshot(p);
        var ultimo = await uow.Parametros.ObtenerUltimoNumeroBoletaAsync(cancellationToken);

        // D1 (F10): el número de boleta solo lo mueven el grabado y la importación. Una vez emitida la primera
        // boleta (vigente o anulada) no se puede cambiar a mano; antes de eso se admite fijar el número inicial
        // para dar continuidad al talonario legado.
        var numeroCambio = dto.SiguienteNumeroBoleta != p.SiguienteNumeroBoleta;
        if (numeroCambio && ultimo.HasValue)
        {
            throw new ReglaNegocioException(
                "PAR_NUMERACION_AUTOMATICA",
                "El número de boleta es automático y no puede modificarse una vez emitida la primera boleta.");
        }

        p.NombreEmpresa = nombreEmpresa;
        p.Direccion = direccion;
        p.Telefono = telefono;
        p.Ciudad = ciudad;
        p.RegistroSanitario = registroSanitario;
        p.TelefonosReclamos = telefonosReclamos;
        p.ObservacionPorDefecto = observacionPorDefecto;

        if (numeroCambio)
        {
            p.SiguienteNumeroBoleta = dto.SiguienteNumeroBoleta;
        }

        string? nuevoArchivo = null;
        var logoAnterior = p.LogoPath;

        switch (dto.AccionLogo)
        {
            case AccionLogo.Reemplazar:
                nuevoArchivo = await _almacenLogo.GuardarAsync(dto.RutaLogoOrigen!, cancellationToken);
                p.LogoPath = nuevoArchivo;
                break;
            case AccionLogo.Quitar:
                p.LogoPath = null;
                break;
            case AccionLogo.SinCambio:
                break;
        }

        var despues = FabricaAuditoria.CrearSnapshot(p);
        if (antes == despues)
        {
            if (nuevoArchivo != null)
            {
                _almacenLogo.Eliminar(nuevoArchivo);
            }
            return;
        }

        p.ModificadoEn = _timeProvider.GetUtcNow().UtcDateTime;

        var motivo = numeroCambio
            ? $"Cambio de numeración de boletas: {antes.SiguienteNumeroBoleta} → {despues.SiguienteNumeroBoleta}"
            : "Actualización de parámetros";

        var auditoria = FabricaAuditoria.Crear(
            _timeProvider,
            sesion.Id,
            AccionAuditoria.CambioParametros,
            EntidadesAuditoria.ParametrosSistema,
            "1",
            antes: antes,
            despues: despues,
            motivo: motivo);

        uow.Auditoria.Registrar(auditoria);

        try
        {
            await uow.GuardarCambiosAsync(cancellationToken);
        }
        catch
        {
            if (nuevoArchivo != null)
            {
                _almacenLogo.Eliminar(nuevoArchivo);
            }
            throw;
        }

        if (!string.IsNullOrEmpty(logoAnterior) && logoAnterior != p.LogoPath)
        {
            _almacenLogo.Eliminar(logoAnterior);
        }

        _logger.LogInformation("Parámetros actualizados por usuario {Usuario}", sesion.NombreUsuario);
        if (numeroCambio)
        {
            _logger.LogWarning("Cambio de numeración de boletas: {Antes} → {Despues} por {Usuario}", antes.SiguienteNumeroBoleta, despues.SiguienteNumeroBoleta, sesion.NombreUsuario);
        }
    }
}
