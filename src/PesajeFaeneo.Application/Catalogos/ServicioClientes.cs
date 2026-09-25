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

public class ServicioClientes : IServicioClientes
{
    private readonly IUnidadDeTrabajoFactory _uowFactory;
    private readonly ISesionActual _sesionActual;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ServicioClientes> _logger;

    public ServicioClientes(
        IUnidadDeTrabajoFactory uowFactory,
        ISesionActual sesionActual,
        TimeProvider timeProvider,
        ILogger<ServicioClientes> logger)
    {
        _uowFactory = uowFactory ?? throw new ArgumentNullException(nameof(uowFactory));
        _sesionActual = sesionActual ?? throw new ArgumentNullException(nameof(sesionActual));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<ClienteListado>> ListarAsync(bool incluirInactivos, CancellationToken cancellationToken = default)
    {
        _sesionActual.ExigirPermiso(Permiso.GestionarCatalogos);

        await using var uow = await _uowFactory.CrearAsync(cancellationToken);
        var clientes = await uow.Clientes.ListarAsync(incluirInactivos, cancellationToken);

        return clientes
            .Select(c => new ClienteListado(c.Id, c.Codigo, c.Nombre, c.Direccion, c.Activo))
            .ToList()
            .AsReadOnly();
    }

    public async Task<ClienteListado> ObtenerAsync(int id, CancellationToken cancellationToken = default)
    {
        _sesionActual.ExigirPermiso(Permiso.GestionarCatalogos);

        await using var uow = await _uowFactory.CrearAsync(cancellationToken);
        var cliente = await uow.Clientes.ObtenerPorIdAsync(id, cancellationToken);
        if (cliente == null)
        {
            throw new EntidadNoEncontradaException("Cliente", id);
        }

        return new ClienteListado(cliente.Id, cliente.Codigo, cliente.Nombre, cliente.Direccion, cliente.Activo);
    }

    public async Task<IReadOnlyList<ElementoBusqueda>> ListarParaBusquedaAsync(CancellationToken cancellationToken = default)
    {
        _sesionActual.ExigirSesion();

        await using var uow = await _uowFactory.CrearAsync(cancellationToken);
        var clientes = await uow.Clientes.ListarAsync(incluirInactivos: false, cancellationToken);

        return clientes
            .Select(c => new ElementoBusqueda(c.Id, c.Codigo.ToString(CultureInfo.InvariantCulture), c.Nombre, c.Direccion, c.Activo))
            .ToList()
            .AsReadOnly();
    }

    public async Task<int> SugerirSiguienteCodigoAsync(CancellationToken cancellationToken = default)
    {
        _sesionActual.ExigirPermiso(Permiso.GestionarCatalogos);

        await using var uow = await _uowFactory.CrearAsync(cancellationToken);
        var max = await uow.Clientes.ObtenerCodigoMaximoAsync(cancellationToken);
        var siguiente = (max ?? 0) + 1;
        return siguiente > ReglasCatalogos.CodigoClienteMaximo ? ReglasCatalogos.CodigoClienteMaximo : siguiente;
    }

    public async Task<int> CrearAsync(CrearCliente dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        _sesionActual.ExigirPermiso(Permiso.GestionarCatalogos);
        var sesion = _sesionActual.ExigirSesion();

        var nombre = ValidadorCatalogos.NormalizarMayusculas(dto.Nombre);
        var direccion = ValidadorCatalogos.NormalizarMayusculas(dto.Direccion);

        var errores = ValidadorCatalogos.ValidarCliente(dto.Codigo, nombre, direccion);
        if (errores.Count > 0)
        {
            throw new ValidacionException(errores);
        }

        await using var uow = await _uowFactory.CrearAsync(cancellationToken);
        var existente = await uow.Clientes.ObtenerPorCodigoAsync(dto.Codigo, cancellationToken);
        if (existente != null)
        {
            if (existente.Activo)
            {
                throw new ValidacionException(CamposCatalogo.Codigo, $"Ya existe un cliente con el código {dto.Codigo}.");
            }
            throw new ValidacionException(CamposCatalogo.Codigo, $"El código {dto.Codigo} pertenece a un cliente desactivado ({existente.Nombre}). Reactívelo en lugar de crear uno nuevo.");
        }

        var entidad = new Cliente
        {
            Codigo = dto.Codigo,
            Nombre = nombre,
            Direccion = direccion,
            Activo = true,
            CreadoEn = _timeProvider.GetUtcNow().UtcDateTime
        };

        await using var tx = await uow.IniciarTransaccionAsync(cancellationToken);
        uow.Clientes.Agregar(entidad);
        await uow.GuardarCambiosAsync(cancellationToken);

        var auditoria = FabricaAuditoria.Crear(
            _timeProvider,
            sesion.Id,
            AccionAuditoria.Crear,
            EntidadesAuditoria.Cliente,
            entidad.Id.ToString(CultureInfo.InvariantCulture),
            despues: FabricaAuditoria.CrearSnapshot(entidad),
            motivo: "Alta de cliente");

        uow.Auditoria.Registrar(auditoria);
        await uow.GuardarCambiosAsync(cancellationToken);
        await tx.ConfirmarAsync(cancellationToken);

        _logger.LogInformation("Cliente creado con Id {Id}, Código {Codigo} por usuario {Usuario}", entidad.Id, dto.Codigo, sesion.NombreUsuario);
        return entidad.Id;
    }

    public async Task EditarAsync(EditarCliente dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        _sesionActual.ExigirPermiso(Permiso.GestionarCatalogos);
        var sesion = _sesionActual.ExigirSesion();

        var nombre = ValidadorCatalogos.NormalizarMayusculas(dto.Nombre);
        var direccion = ValidadorCatalogos.NormalizarMayusculas(dto.Direccion);

        var errores = ValidadorCatalogos.ValidarCliente(dto.Codigo, nombre, direccion);
        if (errores.Count > 0)
        {
            throw new ValidacionException(errores);
        }

        await using var uow = await _uowFactory.CrearAsync(cancellationToken);
        var cliente = await uow.Clientes.ObtenerPorIdAsync(dto.Id, cancellationToken);
        if (cliente == null)
        {
            throw new EntidadNoEncontradaException("Cliente", dto.Id);
        }

        var antes = FabricaAuditoria.CrearSnapshot(cliente);

        if (cliente.Codigo != dto.Codigo)
        {
            if (await uow.Clientes.TienePesajesAsync(cliente.Id, cancellationToken))
            {
                throw new ReglaNegocioException("CLI_CODIGO_EN_USO", "No se puede cambiar el código de un cliente que ya tiene pesajes registrados.");
            }

            var otro = await uow.Clientes.ObtenerPorCodigoAsync(dto.Codigo, cancellationToken);
            if (otro != null && otro.Id != dto.Id)
            {
                if (otro.Activo)
                {
                    throw new ValidacionException(CamposCatalogo.Codigo, $"Ya existe un cliente con el código {dto.Codigo}.");
                }
                throw new ValidacionException(CamposCatalogo.Codigo, $"El código {dto.Codigo} pertenece a un cliente desactivado ({otro.Nombre}). Reactívelo en lugar de crear uno nuevo.");
            }
        }

        cliente.Codigo = dto.Codigo;
        cliente.Nombre = nombre;
        cliente.Direccion = direccion;

        var despues = FabricaAuditoria.CrearSnapshot(cliente);
        if (antes == despues)
        {
            return;
        }

        var auditoria = FabricaAuditoria.Crear(
            _timeProvider,
            sesion.Id,
            AccionAuditoria.Modificar,
            EntidadesAuditoria.Cliente,
            cliente.Id.ToString(CultureInfo.InvariantCulture),
            antes: antes,
            despues: despues,
            motivo: "Modificación de datos");

        uow.Auditoria.Registrar(auditoria);
        await uow.GuardarCambiosAsync(cancellationToken);

        _logger.LogInformation("Cliente modificado Id {Id}, Código {Codigo} por usuario {Usuario}", cliente.Id, cliente.Codigo, sesion.NombreUsuario);
    }

    public async Task CambiarEstadoAsync(int id, bool activo, CancellationToken cancellationToken = default)
    {
        _sesionActual.ExigirPermiso(Permiso.GestionarCatalogos);
        var sesion = _sesionActual.ExigirSesion();

        await using var uow = await _uowFactory.CrearAsync(cancellationToken);
        var cliente = await uow.Clientes.ObtenerPorIdAsync(id, cancellationToken);
        if (cliente == null)
        {
            throw new EntidadNoEncontradaException("Cliente", id);
        }

        if (cliente.Activo == activo)
        {
            return;
        }

        var antes = FabricaAuditoria.CrearSnapshot(cliente);
        cliente.Activo = activo;
        var despues = FabricaAuditoria.CrearSnapshot(cliente);

        var motivo = activo ? "Activación" : "Desactivación";
        var auditoria = FabricaAuditoria.Crear(
            _timeProvider,
            sesion.Id,
            AccionAuditoria.Modificar,
            EntidadesAuditoria.Cliente,
            cliente.Id.ToString(CultureInfo.InvariantCulture),
            antes: antes,
            despues: despues,
            motivo: motivo);

        uow.Auditoria.Registrar(auditoria);
        await uow.GuardarCambiosAsync(cancellationToken);

        _logger.LogInformation("Cliente {Motivo} Id {Id}, Código {Codigo} por usuario {Usuario}", motivo, cliente.Id, cliente.Codigo, sesion.NombreUsuario);
    }
}
