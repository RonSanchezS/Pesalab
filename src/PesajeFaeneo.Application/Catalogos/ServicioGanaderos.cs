using System.Globalization;
using Microsoft.Extensions.Logging;
using PesajeFaeneo.Application.Abstracciones.Persistencia;
using PesajeFaeneo.Application.Auditoria;
using PesajeFaeneo.Application.Catalogos.Busqueda;
using PesajeFaeneo.Application.Catalogos.Dtos;
using PesajeFaeneo.Application.Seguridad;
using PesajeFaeneo.Domain.Entities;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Domain.Excepciones;

namespace PesajeFaeneo.Application.Catalogos;

public class ServicioGanaderos : IServicioGanaderos
{
    private readonly IUnidadDeTrabajoFactory _uowFactory;
    private readonly ISesionActual _sesionActual;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ServicioGanaderos> _logger;

    public ServicioGanaderos(
        IUnidadDeTrabajoFactory uowFactory,
        ISesionActual sesionActual,
        TimeProvider timeProvider,
        ILogger<ServicioGanaderos> logger)
    {
        _uowFactory = uowFactory ?? throw new ArgumentNullException(nameof(uowFactory));
        _sesionActual = sesionActual ?? throw new ArgumentNullException(nameof(sesionActual));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<GanaderoListado>> ListarAsync(bool incluirInactivos, CancellationToken cancellationToken = default)
    {
        _sesionActual.ExigirPermiso(Permiso.GestionarCatalogos);

        await using var uow = await _uowFactory.CrearAsync(cancellationToken);
        var ganaderos = await uow.Ganaderos.ListarAsync(incluirInactivos, cancellationToken);

        return ganaderos
            .Select(g => new GanaderoListado(g.Id, g.Codigo, g.Nombre, g.Activo))
            .ToList()
            .AsReadOnly();
    }

    public async Task<GanaderoListado> ObtenerAsync(int id, CancellationToken cancellationToken = default)
    {
        _sesionActual.ExigirPermiso(Permiso.GestionarCatalogos);

        await using var uow = await _uowFactory.CrearAsync(cancellationToken);
        var ganadero = await uow.Ganaderos.ObtenerPorIdAsync(id, cancellationToken);
        if (ganadero == null)
        {
            throw new EntidadNoEncontradaException("Ganadero", id);
        }

        return new GanaderoListado(ganadero.Id, ganadero.Codigo, ganadero.Nombre, ganadero.Activo);
    }

    public async Task<IReadOnlyList<ElementoBusqueda>> ListarParaBusquedaAsync(CancellationToken cancellationToken = default)
    {
        _sesionActual.ExigirSesion();

        await using var uow = await _uowFactory.CrearAsync(cancellationToken);
        var ganaderos = await uow.Ganaderos.ListarAsync(incluirInactivos: false, cancellationToken);

        return ganaderos
            .Select(g => new ElementoBusqueda(g.Id, g.Codigo, g.Nombre, null, g.Activo))
            .ToList()
            .AsReadOnly();
    }

    public Task<int> CrearAsync(CrearGanadero dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        _sesionActual.ExigirPermiso(Permiso.GestionarCatalogos);
        return CrearInternoAsync(dto, altaRapida: false, cancellationToken);
    }

    public Task<int> CrearRapidoAsync(CrearGanadero dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        _sesionActual.ExigirPermiso(Permiso.AltaRapidaCatalogos);
        return CrearInternoAsync(dto, altaRapida: true, cancellationToken);
    }

    private async Task<int> CrearInternoAsync(CrearGanadero dto, bool altaRapida, CancellationToken cancellationToken)
    {
        var sesion = _sesionActual.ExigirSesion();

        var nombre = ValidadorCatalogos.NormalizarMayusculas(dto.Nombre);

        var errores = ValidadorCatalogos.ValidarGanadero(nombre);
        if (errores.Count > 0)
        {
            throw new ValidacionException(errores);
        }

        await using var uow = await _uowFactory.CrearAsync(cancellationToken);

        var entidad = new Ganadero
        {
            Nombre = nombre,
            Activo = true,
            CreadoEn = _timeProvider.GetUtcNow().UtcDateTime
        };

        await using var tx = await uow.IniciarTransaccionAsync(cancellationToken);
        await AsignadorCodigos.GuardarNuevoAsync(uow, async ct =>
        {
            // F10 (O6): código correlativo automático (mayor numérico existente + 1; los alfanuméricos heredados se ignoran).
            var maximo = await uow.Ganaderos.ObtenerCodigoNumericoMaximoAsync(ct);
            if (maximo >= ReglasCatalogos.CodigoGanaderoNumericoMaximo)
            {
                throw new ReglaNegocioException("GAN_CODIGO_AGOTADO", $"Se alcanzó el último código de ganadero disponible ({ReglasCatalogos.CodigoGanaderoNumericoMaximo}).");
            }

            entidad.Codigo = (maximo + 1).ToString(CultureInfo.InvariantCulture);
            uow.Ganaderos.Agregar(entidad);
        }, cancellationToken);

        var motivo = altaRapida ? "Alta rápida desde pesaje" : "Alta de ganadero";
        var auditoria = FabricaAuditoria.Crear(
            _timeProvider,
            sesion.Id,
            AccionAuditoria.Crear,
            EntidadesAuditoria.Ganadero,
            entidad.Id.ToString(CultureInfo.InvariantCulture),
            despues: FabricaAuditoria.CrearSnapshot(entidad),
            motivo: motivo);

        uow.Auditoria.Registrar(auditoria);
        await uow.GuardarCambiosAsync(cancellationToken);
        await tx.ConfirmarAsync(cancellationToken);

        if (altaRapida)
        {
            _logger.LogInformation("Ganadero creado por alta rápida con Id {Id}, Código '{Codigo}' por usuario {Usuario}", entidad.Id, entidad.Codigo, sesion.NombreUsuario);
        }
        else
        {
            _logger.LogInformation("Ganadero creado con Id {Id}, Código '{Codigo}' por usuario {Usuario}", entidad.Id, entidad.Codigo, sesion.NombreUsuario);
        }

        return entidad.Id;
    }

    public async Task EditarAsync(EditarGanadero dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        _sesionActual.ExigirPermiso(Permiso.GestionarCatalogos);
        var sesion = _sesionActual.ExigirSesion();

        var nombre = ValidadorCatalogos.NormalizarMayusculas(dto.Nombre);

        var errores = ValidadorCatalogos.ValidarGanadero(nombre);
        if (errores.Count > 0)
        {
            throw new ValidacionException(errores);
        }

        await using var uow = await _uowFactory.CrearAsync(cancellationToken);
        var ganadero = await uow.Ganaderos.ObtenerPorIdAsync(dto.Id, cancellationToken);
        if (ganadero == null)
        {
            throw new EntidadNoEncontradaException("Ganadero", dto.Id);
        }

        var antes = FabricaAuditoria.CrearSnapshot(ganadero);

        // El código no se edita (F10, O6): solo el nombre.
        ganadero.Nombre = nombre;

        var despues = FabricaAuditoria.CrearSnapshot(ganadero);
        if (antes == despues)
        {
            return;
        }

        var auditoria = FabricaAuditoria.Crear(
            _timeProvider,
            sesion.Id,
            AccionAuditoria.Modificar,
            EntidadesAuditoria.Ganadero,
            ganadero.Id.ToString(CultureInfo.InvariantCulture),
            antes: antes,
            despues: despues,
            motivo: "Modificación de datos");

        uow.Auditoria.Registrar(auditoria);
        await uow.GuardarCambiosAsync(cancellationToken);

        _logger.LogInformation("Ganadero modificado Id {Id}, Código '{Codigo}' por usuario {Usuario}", ganadero.Id, ganadero.Codigo, sesion.NombreUsuario);
    }

    public async Task CambiarEstadoAsync(int id, bool activo, CancellationToken cancellationToken = default)
    {
        _sesionActual.ExigirPermiso(Permiso.GestionarCatalogos);
        var sesion = _sesionActual.ExigirSesion();

        await using var uow = await _uowFactory.CrearAsync(cancellationToken);
        var ganadero = await uow.Ganaderos.ObtenerPorIdAsync(id, cancellationToken);
        if (ganadero == null)
        {
            throw new EntidadNoEncontradaException("Ganadero", id);
        }

        if (ganadero.Activo == activo)
        {
            return;
        }

        var antes = FabricaAuditoria.CrearSnapshot(ganadero);
        ganadero.Activo = activo;
        var despues = FabricaAuditoria.CrearSnapshot(ganadero);

        var motivo = activo ? "Activación" : "Desactivación";
        var auditoria = FabricaAuditoria.Crear(
            _timeProvider,
            sesion.Id,
            AccionAuditoria.Modificar,
            EntidadesAuditoria.Ganadero,
            ganadero.Id.ToString(CultureInfo.InvariantCulture),
            antes: antes,
            despues: despues,
            motivo: motivo);

        uow.Auditoria.Registrar(auditoria);
        await uow.GuardarCambiosAsync(cancellationToken);

        _logger.LogInformation("Ganadero {Motivo} Id {Id}, Código '{Codigo}' por usuario {Usuario}", motivo, ganadero.Id, ganadero.Codigo, sesion.NombreUsuario);
    }
}
