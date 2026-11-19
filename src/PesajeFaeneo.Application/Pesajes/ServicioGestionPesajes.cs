using System.Globalization;
using Microsoft.Extensions.Logging;
using PesajeFaeneo.Application.Abstracciones.Persistencia;
using PesajeFaeneo.Application.Auditoria;
using PesajeFaeneo.Application.Catalogos.Busqueda;
using PesajeFaeneo.Application.Comun;
using PesajeFaeneo.Application.Pesajes.Dtos;
using PesajeFaeneo.Application.Seguridad;
using PesajeFaeneo.Domain.Entities;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Domain.Excepciones;
using PesajeFaeneo.Domain.Reglas;

namespace PesajeFaeneo.Application.Pesajes;

public class ServicioGestionPesajes : IServicioGestionPesajes
{
    private readonly IUnidadDeTrabajoFactory _uowFactory;
    private readonly ISesionActual _sesion;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ServicioGestionPesajes> _logger;

    public ServicioGestionPesajes(
        IUnidadDeTrabajoFactory uowFactory,
        ISesionActual sesion,
        TimeProvider timeProvider,
        ILogger<ServicioGestionPesajes> logger)
    {
        _uowFactory = uowFactory ?? throw new ArgumentNullException(nameof(uowFactory));
        _sesion = sesion ?? throw new ArgumentNullException(nameof(sesion));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    private void ExigirPermisoGestion()
    {
        _sesion.ExigirSesion();
        if (!_sesion.TienePermiso(Permiso.EditarBoleta) && !_sesion.TienePermiso(Permiso.AnularBoleta))
        {
            throw new NoAutorizadoException("No tiene permisos para gestionar boletas de pesaje.");
        }
    }

    public async Task<OpcionesBusquedaBoletas> ObtenerOpcionesBusquedaAsync(CancellationToken cancellationToken = default)
    {
        ExigirPermisoGestion();

        await using var uow = await _uowFactory.CrearAsync(cancellationToken);
        var ganaderos = await uow.Ganaderos.ListarAsync(incluirInactivos: true, cancellationToken);
        var clientes = await uow.Clientes.ListarAsync(incluirInactivos: true, cancellationToken);

        var ganaderosMapeados = ganaderos
            .Select(g => new ElementoBusqueda(g.Id, g.Codigo, g.Nombre, null, g.Activo))
            .ToList()
            .AsReadOnly();

        var clientesMapeados = clientes
            .Select(c => new ElementoBusqueda(c.Id, c.Codigo.ToString(CultureInfo.InvariantCulture), c.Nombre, c.Direccion, c.Activo))
            .ToList()
            .AsReadOnly();

        return new OpcionesBusquedaBoletas(ganaderosMapeados, clientesMapeados);
    }

    public async Task<ResultadoBusquedaBoletas> BuscarAsync(FiltroBoletas filtro, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);
        ExigirPermisoGestion();

        if (filtro.Desde is { } desde && filtro.Hasta is { } hasta && desde > hasta)
        {
            throw new ValidacionException(CamposGestionPesajes.FechaHasta, "La fecha final no puede ser anterior a la inicial.");
        }

        if (filtro.NumeroBoleta is { } num && num <= 0)
        {
            throw new ValidacionException(CamposGestionPesajes.NumeroBoleta, "Ingrese un número de boleta válido.");
        }

        var zona = _timeProvider.LocalTimeZone;
        DateTime? desdeUtc = filtro.Desde is { } d ? RangoDiaLocal.DelDia(d, zona).DesdeUtc : null;
        DateTime? hastaUtc = filtro.Hasta is { } h ? RangoDiaLocal.DelDia(h, zona).HastaUtc : null;

        var criterios = new CriteriosBusquedaPesaje(
            desdeUtc,
            hastaUtc,
            filtro.NumeroBoleta,
            filtro.GanaderoId,
            filtro.ClienteId,
            filtro.Estado);

        await using var uow = await _uowFactory.CrearAsync(cancellationToken);
        var lista = await uow.Pesajes.BuscarAsync(criterios, ReglasGestionPesajes.BusquedaMaximoFilas + 1, cancellationToken);

        bool truncado = lista.Count > ReglasGestionPesajes.BusquedaMaximoFilas;
        var pesajes = lista.Take(ReglasGestionPesajes.BusquedaMaximoFilas).ToList();

        var boletas = new List<BoletaEncontrada>(pesajes.Count);
        foreach (var p in pesajes)
        {
            var fechaUtc = DateTime.SpecifyKind(p.FechaHora, DateTimeKind.Utc);
            var fechaLocal = DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(fechaUtc, zona), DateTimeKind.Unspecified);

            boletas.Add(new BoletaEncontrada(
                p.Id,
                p.NumeroBoleta,
                p.Version,
                fechaLocal,
                p.Ganadero?.Codigo ?? string.Empty,
                p.Ganadero?.Nombre ?? string.Empty,
                p.Secuencia,
                p.Cliente?.Nombre ?? string.Empty,
                p.TipoDesposte,
                p.PesoIzquierdo,
                p.PesoDerecho,
                p.PesoTotal,
                p.OrigenPeso,
                p.Estado,
                p.MotivoAnulacion,
                p.Fletero?.Nombre));
        }

        var parametros = await uow.Parametros.ObtenerAsync(cancellationToken);
        return new ResultadoBusquedaBoletas(boletas.AsReadOnly(), truncado, ReglasDesposte.Efectivo(parametros.ModoDesposte));
    }

    public async Task<BoletaParaEdicion> ObtenerParaEdicionAsync(int pesajeId, CancellationToken cancellationToken = default)
    {
        _sesion.ExigirPermiso(Permiso.EditarBoleta);
        if (pesajeId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pesajeId));
        }

        await using var uow = await _uowFactory.CrearAsync(cancellationToken);
        var p = await uow.Pesajes.ObtenerParaEdicionAsync(pesajeId, cancellationToken);
        if (p == null)
        {
            throw new EntidadNoEncontradaException("Pesaje", pesajeId);
        }

        if (p.Estado == EstadoPesaje.Anulado)
        {
            throw new ReglaNegocioException("PES_ANULADA", $"La boleta N° {p.NumeroBoleta} está anulada y no se puede modificar.");
        }

        var usuario = await uow.Usuarios.ObtenerPorIdAsync(p.UsuarioId, cancellationToken);
        string nombreOperador = usuario is null
            ? "(desconocido)"
            : !string.IsNullOrWhiteSpace(usuario.NombreCompleto)
                ? usuario.NombreCompleto
                : usuario.NombreUsuario;

        var ganaderosActivos = (await uow.Ganaderos.ListarAsync(incluirInactivos: false, cancellationToken))
            .Select(g => new ElementoBusqueda(g.Id, g.Codigo, g.Nombre, null, g.Activo))
            .ToList();

        if (p.Ganadero != null)
        {
            if (!ganaderosActivos.Any(g => g.Id == p.Ganadero.Id))
            {
                ganaderosActivos.Add(new ElementoBusqueda(p.Ganadero.Id, p.Ganadero.Codigo, p.Ganadero.Nombre, null, p.Ganadero.Activo));
            }
        }
        else
        {
            var gActual = await uow.Ganaderos.ObtenerPorIdAsync(p.GanaderoId, cancellationToken);
            if (gActual != null && !ganaderosActivos.Any(g => g.Id == gActual.Id))
            {
                ganaderosActivos.Add(new ElementoBusqueda(gActual.Id, gActual.Codigo, gActual.Nombre, null, gActual.Activo));
            }
        }

        var clientesActivos = (await uow.Clientes.ListarAsync(incluirInactivos: false, cancellationToken))
            .Select(c => new ElementoBusqueda(c.Id, c.Codigo.ToString(CultureInfo.InvariantCulture), c.Nombre, c.Direccion, c.Activo))
            .ToList();

        if (p.Cliente != null)
        {
            if (!clientesActivos.Any(c => c.Id == p.Cliente.Id))
            {
                clientesActivos.Add(new ElementoBusqueda(p.Cliente.Id, p.Cliente.Codigo.ToString(CultureInfo.InvariantCulture), p.Cliente.Nombre, p.Cliente.Direccion, p.Cliente.Activo));
            }
        }
        else
        {
            var cActual = await uow.Clientes.ObtenerPorIdAsync(p.ClienteId, cancellationToken);
            if (cActual != null && !clientesActivos.Any(c => c.Id == cActual.Id))
            {
                clientesActivos.Add(new ElementoBusqueda(cActual.Id, cActual.Codigo.ToString(CultureInfo.InvariantCulture), cActual.Nombre, cActual.Direccion, cActual.Activo));
            }
        }

        // F10 (O5): el fletero también es un dato descriptivo editable (activos + el actual aunque esté inactivo).
        var fleterosActivos = (await uow.Fleteros.ListarAsync(incluirInactivos: false, cancellationToken))
            .Select(f => new ElementoBusqueda(f.Id, f.Codigo.ToString(CultureInfo.InvariantCulture), f.Nombre, f.Celular, f.Activo))
            .ToList();

        if (p.FleteroId is int fleteroActualId && !fleterosActivos.Any(f => f.Id == fleteroActualId))
        {
            var fActual = p.Fletero ?? await uow.Fleteros.ObtenerPorIdAsync(fleteroActualId, cancellationToken);
            if (fActual != null)
            {
                fleterosActivos.Add(new ElementoBusqueda(fActual.Id, fActual.Codigo.ToString(CultureInfo.InvariantCulture), fActual.Nombre, fActual.Celular, fActual.Activo));
            }
        }

        var zona = _timeProvider.LocalTimeZone;
        var fechaUtc = DateTime.SpecifyKind(p.FechaHora, DateTimeKind.Utc);
        var fechaLocal = DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(fechaUtc, zona), DateTimeKind.Unspecified);
        var modo = ReglasDesposte.Efectivo((await uow.Parametros.ObtenerAsync(cancellationToken)).ModoDesposte);

        return new BoletaParaEdicion(
            p.Id,
            p.NumeroBoleta,
            p.Version,
            fechaLocal,
            p.GanaderoId,
            p.ClienteId,
            p.Direccion,
            p.Secuencia,
            p.ColorRaza,
            p.Sexo,
            p.Observacion,
            p.TipoDesposte,
            p.PesoIzquierdo,
            p.PesoDerecho,
            p.PesoTotal,
            p.OrigenPeso,
            nombreOperador,
            ganaderosActivos.AsReadOnly(),
            clientesActivos.AsReadOnly(),
            p.FleteroId,
            fleterosActivos.AsReadOnly(),
            ReglasDesposte.MostrarDesposte(modo, p.TipoDesposte));
    }

    public async Task<ResultadoEdicion> EditarAsync(EditarPesaje dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        _sesion.ExigirPermiso(Permiso.EditarBoleta);
        var sesion = _sesion.ExigirSesion();

        if (dto.PesajeId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dto.PesajeId));
        }

        var (normalizado, errores) = ValidadorGestionPesajes.ValidarEdicion(dto);
        if (errores.Count > 0)
        {
            throw new ValidacionException(errores);
        }

        await using var uow = await _uowFactory.CrearAsync(cancellationToken);
        var p = await uow.Pesajes.ObtenerParaEdicionAsync(normalizado.PesajeId, cancellationToken);
        if (p == null)
        {
            throw new EntidadNoEncontradaException("Pesaje", normalizado.PesajeId);
        }

        if (p.Estado == EstadoPesaje.Anulado)
        {
            throw new ReglaNegocioException("PES_ANULADA", $"La boleta N° {p.NumeroBoleta} está anulada y no se puede modificar.");
        }

        if (p.Version != normalizado.VersionEsperada)
        {
            throw new ConcurrenciaException("La boleta fue modificada por otro usuario o en otra ventana. Ciérrela y vuelva a abrirla.");
        }

        var antes = FabricaAuditoria.CrearSnapshot(p, p.Ganadero, p.Cliente, p.Fletero);

        Ganadero? ganaderoNuevo = null;
        if (normalizado.GanaderoId != p.GanaderoId)
        {
            var g = await uow.Ganaderos.ObtenerPorIdAsync(normalizado.GanaderoId, cancellationToken);
            if (g == null || !g.Activo)
            {
                throw new ValidacionException(CamposPesaje.Ganadero, "El ganadero seleccionado no existe o está desactivado.");
            }
            ganaderoNuevo = g;
        }

        Cliente? clienteNuevo = null;
        if (normalizado.ClienteId != p.ClienteId)
        {
            var c = await uow.Clientes.ObtenerPorIdAsync(normalizado.ClienteId, cancellationToken);
            if (c == null || !c.Activo)
            {
                throw new ValidacionException(CamposPesaje.Cliente, "El cliente seleccionado no existe o está desactivado.");
            }
            clienteNuevo = c;
        }

        Fletero? fleteroNuevo = null;
        if (normalizado.FleteroId is int fleteroId && fleteroId != p.FleteroId)
        {
            var f = await uow.Fleteros.ObtenerPorIdAsync(fleteroId, cancellationToken);
            if (f == null || !f.Activo)
            {
                throw new ValidacionException(CamposPesaje.Fletero, "El fletero seleccionado no existe o está desactivado.");
            }
            fleteroNuevo = f;
        }

        p.GanaderoId = normalizado.GanaderoId;
        p.ClienteId = normalizado.ClienteId;
        p.Direccion = normalizado.Direccion ?? string.Empty;
        p.Secuencia = normalizado.Secuencia;
        p.ColorRaza = normalizado.ColorRaza ?? string.Empty;
        p.Sexo = normalizado.Sexo;
        p.Observacion = normalizado.Observacion ?? string.Empty;
        p.FleteroId = normalizado.FleteroId;

        var despues = FabricaAuditoria.CrearSnapshot(p, ganaderoNuevo ?? p.Ganadero, clienteNuevo ?? p.Cliente, fleteroNuevo ?? (p.FleteroId is null ? null : p.Fletero));

        if (antes == despues)
        {
            return new ResultadoEdicion(false, Array.Empty<string>());
        }

        var camposModificados = new List<string>();
        if (antes.GanaderoId != despues.GanaderoId) camposModificados.Add("Ganadero");
        if (antes.Secuencia != despues.Secuencia) camposModificados.Add("Secuencia");
        if (antes.ClienteId != despues.ClienteId) camposModificados.Add("Cliente");
        if (antes.Direccion != despues.Direccion) camposModificados.Add("Dirección");
        if (antes.ColorRaza != despues.ColorRaza) camposModificados.Add("Color/raza");
        if (antes.Sexo != despues.Sexo) camposModificados.Add("Sexo");
        if (antes.Observacion != despues.Observacion) camposModificados.Add("Observación");
        if (antes.FleteroId != despues.FleteroId) camposModificados.Add("Fletero");

        p.ModificadoEn = _timeProvider.GetUtcNow().UtcDateTime;

        var motivo = $"Edición de boleta {p.NumeroBoleta}: {string.Join(", ", camposModificados)}";
        if (!string.IsNullOrWhiteSpace(normalizado.Motivo))
        {
            motivo += " — " + normalizado.Motivo;
        }

        var aud = FabricaAuditoria.Crear(
            _timeProvider,
            sesion.Id,
            AccionAuditoria.Modificar,
            EntidadesAuditoria.Pesaje,
            p.Id.ToString(CultureInfo.InvariantCulture),
            antes,
            despues,
            motivo);

        uow.Auditoria.Registrar(aud);
        await uow.GuardarCambiosAsync(cancellationToken);

        _logger.LogInformation("Boleta {Numero} (Id {Id}) editada por {Usuario}", p.NumeroBoleta, p.Id, sesion.NombreUsuario);

        return new ResultadoEdicion(true, camposModificados.AsReadOnly());
    }

    public async Task AnularAsync(AnularPesaje dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        _sesion.ExigirPermiso(Permiso.AnularBoleta);
        var sesion = _sesion.ExigirSesion();

        if (dto.PesajeId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dto.PesajeId));
        }

        var (normalizado, errores) = ValidadorGestionPesajes.ValidarAnulacion(dto);
        if (errores.Count > 0)
        {
            throw new ValidacionException(errores);
        }

        await using var uow = await _uowFactory.CrearAsync(cancellationToken);
        var p = await uow.Pesajes.ObtenerParaEdicionAsync(normalizado.PesajeId, cancellationToken);
        if (p == null)
        {
            throw new EntidadNoEncontradaException("Pesaje", normalizado.PesajeId);
        }

        if (p.Estado == EstadoPesaje.Anulado)
        {
            throw new ReglaNegocioException("PES_YA_ANULADA", $"La boleta N° {p.NumeroBoleta} ya está anulada.");
        }

        if (p.Version != normalizado.VersionEsperada)
        {
            throw new ConcurrenciaException("La boleta fue modificada por otro usuario o en otra ventana. Ciérrela y vuelva a abrirla.");
        }

        var antes = FabricaAuditoria.CrearSnapshot(p, p.Ganadero, p.Cliente, p.Fletero);
        var ahora = _timeProvider.GetUtcNow().UtcDateTime;

        p.Estado = EstadoPesaje.Anulado;
        p.MotivoAnulacion = normalizado.Motivo;
        p.AnuladoPorId = sesion.Id;
        p.AnuladoEn = ahora;
        p.ModificadoEn = ahora;

        var despues = FabricaAuditoria.CrearSnapshot(p, p.Ganadero, p.Cliente, p.Fletero);

        var aud = FabricaAuditoria.Crear(
            _timeProvider,
            sesion.Id,
            AccionAuditoria.Anular,
            EntidadesAuditoria.Pesaje,
            p.Id.ToString(CultureInfo.InvariantCulture),
            antes,
            despues,
            $"Anulación de boleta {p.NumeroBoleta}: {normalizado.Motivo}");

        uow.Auditoria.Registrar(aud);
        await uow.GuardarCambiosAsync(cancellationToken);

        _logger.LogInformation("Boleta {Numero} (Id {Id}) anulada por {Usuario}", p.NumeroBoleta, p.Id, sesion.NombreUsuario);
    }
}
