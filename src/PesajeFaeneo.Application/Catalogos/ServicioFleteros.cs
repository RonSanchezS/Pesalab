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
            .Select(f => new FleteroListado(f.Id, f.Codigo, f.Nombre, f.Celular, f.Activo))
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

        return new FleteroListado(fletero.Id, fletero.Codigo, fletero.Nombre, fletero.Celular, fletero.Activo);
    }

    public async Task<IReadOnlyList<FleteroListado>> ListarActivosAsync(CancellationToken cancellationToken = default)
    {
        _sesionActual.ExigirSesion();

        await using var uow = await _uowFactory.CrearAsync(cancellationToken);
        var fleteros = await uow.Fleteros.ListarAsync(incluirInactivos: false, cancellationToken);

        return fleteros
            .OrderBy(f => f.Codigo)
            .Select(f => new FleteroListado(f.Id, f.Codigo, f.Nombre, f.Celular, f.Activo))
            .ToList()
            .AsReadOnly();
    }

    /// <summary>Fleteros activos para el selector del pesaje (búsqueda por código o nombre); cualquier usuario con sesión.</summary>
    public async Task<IReadOnlyList<ElementoBusqueda>> ListarParaBusquedaAsync(CancellationToken cancellationToken = default)
    {
        _sesionActual.ExigirSesion();

        await using var uow = await _uowFactory.CrearAsync(cancellationToken);
        var fleteros = await uow.Fleteros.ListarAsync(incluirInactivos: false, cancellationToken);

        return fleteros
            .Select(f => new ElementoBusqueda(f.Id, f.Codigo.ToString(CultureInfo.InvariantCulture), f.Nombre, f.Celular, f.Activo))
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

        var errores = ValidadorCatalogos.ValidarFletero(nombre, celular);
        if (errores.Count > 0)
        {
            throw new ValidacionException(errores);
        }

        await using var uow = await _uowFactory.CrearAsync(cancellationToken);

        var entidad = new Fletero
        {
            Nombre = nombre,
            Celular = celular,
            Activo = true,
            CreadoEn = _timeProvider.GetUtcNow().UtcDateTime
        };

        await using var tx = await uow.IniciarTransaccionAsync(cancellationToken);
        await AsignadorCodigos.GuardarNuevoAsync(uow, async ct =>
        {
            // F10 (O5/O6): sin límite de fleteros; código correlativo automático (mayor existente + 1).
            entidad.Codigo = (await uow.Fleteros.ObtenerCodigoMaximoAsync(ct) ?? 0) + 1;
            uow.Fleteros.Agregar(entidad);
        }, cancellationToken);

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

        _logger.LogInformation("Fletero creado con Id {Id}, Código {Codigo} por usuario {Usuario}", entidad.Id, entidad.Codigo, sesion.NombreUsuario);
        return entidad.Id;
    }

    public async Task EditarAsync(EditarFletero dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        _sesionActual.ExigirPermiso(Permiso.GestionarCatalogos);
        var sesion = _sesionActual.ExigirSesion();

        var nombre = ValidadorCatalogos.NormalizarMayusculas(dto.Nombre);
        var celular = ValidadorCatalogos.NormalizarTexto(dto.Celular);

        var errores = ValidadorCatalogos.ValidarFletero(nombre, celular);
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

        // El código no se edita (F10, O6): solo nombre y celular.
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

        _logger.LogInformation("Fletero modificado Id {Id}, Código {Codigo} por usuario {Usuario}", fletero.Id, fletero.Codigo, sesion.NombreUsuario);
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

        _logger.LogInformation("Fletero {Motivo} Id {Id}, Código {Codigo} por usuario {Usuario}", motivo, fletero.Id, fletero.Codigo, sesion.NombreUsuario);
    }
}
