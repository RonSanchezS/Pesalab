using System.Globalization;
using Microsoft.Extensions.Logging;
using PesajeFaeneo.Application.Abstracciones.Persistencia;
using PesajeFaeneo.Application.Auditoria;
using PesajeFaeneo.Application.Catalogos.Dtos;
using PesajeFaeneo.Application.Seguridad;
using PesajeFaeneo.Domain.Entities;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Domain.Excepciones;

namespace PesajeFaeneo.Application.Catalogos;

public class ServicioFleteros : IServicioFleteros
{
    private readonly IUnidadDeTrabajoFactory _uowFactory;
    private readonly ISesionActual _sesionActual;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ServicioFleteros> _logger;

    public ServicioFleteros(
        IUnidadDeTrabajoFactory uowFactory,
        ISesionActual sesionActual,
        TimeProvider timeProvider,
        ILogger<ServicioFleteros> logger)
    {
        _uowFactory = uowFactory ?? throw new ArgumentNullException(nameof(uowFactory));
        _sesionActual = sesionActual ?? throw new ArgumentNullException(nameof(sesionActual));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<FleteroListado>> ListarAsync(bool incluirInactivos, CancellationToken cancellationToken = default)
    {
        _sesionActual.ExigirPermiso(Permiso.GestionarCatalogos);

        await using var uow = await _uowFactory.CrearAsync(cancellationToken);
        var fleteros = await uow.Fleteros.ListarAsync(incluirInactivos, cancellationToken);

        return fleteros
            .Select(f => new FleteroListado(f.Id, f.Orden, f.Nombre, f.Celular, f.Activo))
            .ToList()
            .AsReadOnly();
    }

    public async Task<FleteroListado> ObtenerAsync(int id, CancellationToken cancellationToken = default)
    {
        _sesionActual.ExigirPermiso(Permiso.GestionarCatalogos);

        await using var uow = await _uowFactory.CrearAsync(cancellationToken);
        var fletero = await uow.Fleteros.ObtenerPorIdAsync(id, cancellationToken);
        if (fletero == null)
        {
            throw new EntidadNoEncontradaException("Fletero", id);
        }

        return new FleteroListado(fletero.Id, fletero.Orden, fletero.Nombre, fletero.Celular, fletero.Activo);
    }

    public async Task<IReadOnlyList<FleteroListado>> ListarActivosAsync(CancellationToken cancellationToken = default)
    {
        _sesionActual.ExigirSesion();

        await using var uow = await _uowFactory.CrearAsync(cancellationToken);
        var fleteros = await uow.Fleteros.ListarAsync(incluirInactivos: false, cancellationToken);

        return fleteros
            .OrderBy(f => f.Orden)
            .Select(f => new FleteroListado(f.Id, f.Orden, f.Nombre, f.Celular, f.Activo))
            .ToList()
            .AsReadOnly();
    }

    public async Task<int> CrearAsync(CrearFletero dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        _sesionActual.ExigirPermiso(Permiso.GestionarCatalogos);
        var sesion = _sesionActual.ExigirSesion();

        var nombre = ValidadorCatalogos.NormalizarMayusculas(dto.Nombre);
        var celular = ValidadorCatalogos.NormalizarTexto(dto.Celular);

        var errores = ValidadorCatalogos.ValidarFletero(dto.Orden, nombre, celular);
        if (errores.Count > 0)
        {
            throw new ValidacionException(errores);
        }

        await using var uow = await _uowFactory.CrearAsync(cancellationToken);
        if (await uow.Fleteros.ContarActivosAsync(cancellationToken) >= ReglasCatalogos.MaximoFleterosActivos)
        {
            throw new ReglaNegocioException("FLE_MAXIMO", "Ya hay 4 fleteros activos. Desactive uno antes de agregar otro.");
        }

        var ocupado = await uow.Fleteros.ObtenerActivoPorOrdenAsync(dto.Orden, cancellationToken);
        if (ocupado != null)
        {
            throw new ValidacionException(CamposCatalogo.Orden, $"El orden F{dto.Orden} ya está asignado a {ocupado.Nombre}.");
        }

        var entidad = new Fletero
        {
            Orden = dto.Orden,
            Nombre = nombre,
            Celular = celular,
            Activo = true,
            CreadoEn = _timeProvider.GetUtcNow().UtcDateTime
        };

        await using var tx = await uow.IniciarTransaccionAsync(cancellationToken);
        uow.Fleteros.Agregar(entidad);
        await uow.GuardarCambiosAsync(cancellationToken);

        var auditoria = FabricaAuditoria.Crear(
            _timeProvider,
            sesion.Id,
            AccionAuditoria.Crear,
            EntidadesAuditoria.Fletero,
            entidad.Id.ToString(CultureInfo.InvariantCulture),
            despues: FabricaAuditoria.CrearSnapshot(entidad),
            motivo: "Alta de fletero");

        uow.Auditoria.Registrar(auditoria);
        await uow.GuardarCambiosAsync(cancellationToken);
        await tx.ConfirmarAsync(cancellationToken);

        _logger.LogInformation("Fletero creado con Id {Id}, Orden F{Orden} por usuario {Usuario}", entidad.Id, dto.Orden, sesion.NombreUsuario);
        return entidad.Id;
    }

    public async Task EditarAsync(EditarFletero dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        _sesionActual.ExigirPermiso(Permiso.GestionarCatalogos);
        var sesion = _sesionActual.ExigirSesion();

        var nombre = ValidadorCatalogos.NormalizarMayusculas(dto.Nombre);
        var celular = ValidadorCatalogos.NormalizarTexto(dto.Celular);

        var errores = ValidadorCatalogos.ValidarFletero(dto.Orden, nombre, celular);
        if (errores.Count > 0)
        {
            throw new ValidacionException(errores);
        }

        await using var uow = await _uowFactory.CrearAsync(cancellationToken);
        var fletero = await uow.Fleteros.ObtenerPorIdAsync(dto.Id, cancellationToken);
        if (fletero == null)
        {
            throw new EntidadNoEncontradaException("Fletero", dto.Id);
        }

        var antes = FabricaAuditoria.CrearSnapshot(fletero);

        if (fletero.Activo && fletero.Orden != dto.Orden)
        {
            var ocupado = await uow.Fleteros.ObtenerActivoPorOrdenAsync(dto.Orden, cancellationToken);
            if (ocupado != null && ocupado.Id != dto.Id)
            {
                throw new ValidacionException(CamposCatalogo.Orden, $"El orden F{dto.Orden} ya está asignado a {ocupado.Nombre}.");
            }
        }

        fletero.Orden = dto.Orden;
        fletero.Nombre = nombre;
        fletero.Celular = celular;

        var despues = FabricaAuditoria.CrearSnapshot(fletero);
        if (antes == despues)
        {
            return;
        }

        var auditoria = FabricaAuditoria.Crear(
            _timeProvider,
            sesion.Id,
            AccionAuditoria.Modificar,
            EntidadesAuditoria.Fletero,
            fletero.Id.ToString(CultureInfo.InvariantCulture),
            antes: antes,
            despues: despues,
            motivo: "Modificación de datos");

        uow.Auditoria.Registrar(auditoria);
        await uow.GuardarCambiosAsync(cancellationToken);

        _logger.LogInformation("Fletero modificado Id {Id}, Orden F{Orden} por usuario {Usuario}", fletero.Id, fletero.Orden, sesion.NombreUsuario);
    }

    public async Task CambiarEstadoAsync(int id, bool activo, CancellationToken cancellationToken = default)
    {
        _sesionActual.ExigirPermiso(Permiso.GestionarCatalogos);
        var sesion = _sesionActual.ExigirSesion();

        await using var uow = await _uowFactory.CrearAsync(cancellationToken);
        var fletero = await uow.Fleteros.ObtenerPorIdAsync(id, cancellationToken);
        if (fletero == null)
        {
            throw new EntidadNoEncontradaException("Fletero", id);
        }

        if (fletero.Activo == activo)
        {
            return;
        }

        if (activo)
        {
            var ocupado = await uow.Fleteros.ObtenerActivoPorOrdenAsync(fletero.Orden, cancellationToken);
            if (ocupado != null && ocupado.Id != id)
            {
                throw new ReglaNegocioException("FLE_ORDEN_OCUPADO", $"No se puede activar: el orden F{fletero.Orden} está ocupado por {ocupado.Nombre}. Cambie su orden antes de activarlo.");
            }
        }

        var antes = FabricaAuditoria.CrearSnapshot(fletero);
        fletero.Activo = activo;
        var despues = FabricaAuditoria.CrearSnapshot(fletero);

        var motivo = activo ? "Activación" : "Desactivación";
        var auditoria = FabricaAuditoria.Crear(
            _timeProvider,
            sesion.Id,
            AccionAuditoria.Modificar,
            EntidadesAuditoria.Fletero,
            fletero.Id.ToString(CultureInfo.InvariantCulture),
            antes: antes,
            despues: despues,
            motivo: motivo);

        uow.Auditoria.Registrar(auditoria);
        await uow.GuardarCambiosAsync(cancellationToken);

        _logger.LogInformation("Fletero {Motivo} Id {Id}, Orden F{Orden} por usuario {Usuario}", motivo, fletero.Id, fletero.Orden, sesion.NombreUsuario);
    }
}
