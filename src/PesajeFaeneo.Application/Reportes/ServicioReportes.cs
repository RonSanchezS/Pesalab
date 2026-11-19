using System.Globalization;
using Microsoft.Extensions.Logging;
using PesajeFaeneo.Application.Abstracciones;
using PesajeFaeneo.Application.Abstracciones.Persistencia;
using PesajeFaeneo.Application.Boletas.Diseno;
using PesajeFaeneo.Application.Catalogos;
using PesajeFaeneo.Application.Catalogos.Busqueda;
using PesajeFaeneo.Application.Comun;
using PesajeFaeneo.Application.Reportes.Dtos;
using PesajeFaeneo.Application.Seguridad;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Domain.Excepciones;
using PesajeFaeneo.Domain.Reglas;

namespace PesajeFaeneo.Application.Reportes;

public class ServicioReportes : IServicioReportes
{
    private readonly IUnidadDeTrabajoFactory _uowFactory;
    private readonly ISesionActual _sesion;
    private readonly TimeProvider _timeProvider;
    private readonly IAlmacenLogo _almacenLogo;
    private readonly ILogger<ServicioReportes> _logger;

    internal int MaximoFilas { get; init; } = ReglasReportes.MaximoFilas;

    public ServicioReportes(
        IUnidadDeTrabajoFactory uowFactory,
        ISesionActual sesion,
        TimeProvider timeProvider,
        IAlmacenLogo almacenLogo,
        ILogger<ServicioReportes> logger)
    {
        _uowFactory = uowFactory ?? throw new ArgumentNullException(nameof(uowFactory));
        _sesion = sesion ?? throw new ArgumentNullException(nameof(sesion));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _almacenLogo = almacenLogo ?? throw new ArgumentNullException(nameof(almacenLogo));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<OpcionesReportes> ObtenerOpcionesAsync(CancellationToken cancellationToken = default)
    {
        _sesion.ExigirPermiso(Permiso.VerReportes);

        await using var uow = await _uowFactory.CrearAsync(cancellationToken);
        var ganaderos = await uow.Ganaderos.ListarAsync(incluirInactivos: true, cancellationToken);
        var clientes = await uow.Clientes.ListarAsync(incluirInactivos: true, cancellationToken);

        var listaGanaderos = ganaderos
            .Select(g => new ElementoBusqueda(g.Id, g.Codigo, g.Nombre, null, g.Activo))
            .ToList()
            .AsReadOnly();

        var listaClientes = clientes
            .Select(c => new ElementoBusqueda(c.Id, c.Codigo.ToString(CultureInfo.InvariantCulture), c.Nombre, c.Direccion, c.Activo))
            .ToList()
            .AsReadOnly();

        var razas = await uow.Pesajes.ListarRazasAsync(cancellationToken);

        return new OpcionesReportes(listaGanaderos, listaClientes, razas);
    }

    public async Task<ReportePesajes> GenerarAsync(FiltroReporte filtro, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);
        _sesion.ExigirPermiso(Permiso.VerReportes);
        var sesion = _sesion.ExigirSesion();

        if (!Enum.IsDefined(filtro.Tipo))
        {
            throw new ValidacionException(CamposReportes.Tipo, "Seleccione el tipo de reporte.");
        }

        var hasta = filtro.Tipo == TipoReporte.PesajesDelDia ? filtro.Desde : filtro.Hasta;
        var errores = new List<ErrorValidacion>();

        if (filtro.Desde > hasta)
        {
            errores.Add(new ErrorValidacion(CamposReportes.FechaHasta, "La fecha final no puede ser anterior a la inicial."));
        }

        if (filtro.Tipo == TipoReporte.PorGanadero && (filtro.GanaderoId is null || filtro.GanaderoId <= 0))
        {
            errores.Add(new ErrorValidacion(CamposReportes.Ganadero, "Seleccione el ganadero."));
        }

        if (filtro.Tipo == TipoReporte.PorCliente && (filtro.ClienteId is null || filtro.ClienteId <= 0))
        {
            errores.Add(new ErrorValidacion(CamposReportes.Cliente, "Seleccione el cliente."));
        }

        if (filtro.Tipo == TipoReporte.PorRaza && string.IsNullOrEmpty(ValidadorCatalogos.NormalizarMayusculas(filtro.ColorRaza)))
        {
            errores.Add(new ErrorValidacion(CamposReportes.Raza, "Seleccione la raza."));
        }

        if (errores.Count > 0)
        {
            throw new ValidacionException(errores);
        }

        int? ganaderoId = filtro.Tipo == TipoReporte.PorGanadero ? filtro.GanaderoId : null;
        int? clienteId = filtro.Tipo == TipoReporte.PorCliente ? filtro.ClienteId : null;
        var estado = filtro.Tipo == TipoReporte.Anuladas ? EstadoPesaje.Anulado : EstadoPesaje.Vigente;
        // F10 (O8): la raza se compara normalizada (espacios colapsados y mayúsculas, igual que se guarda).
        string? colorRaza = filtro.Tipo == TipoReporte.PorRaza ? ValidadorCatalogos.NormalizarMayusculas(filtro.ColorRaza) : null;

        await using var uow = await _uowFactory.CrearAsync(cancellationToken);

        string? textoGanadero = null;
        string? textoCliente = null;
        string? claveFiltro = null;

        if (ganaderoId.HasValue)
        {
            var g = await uow.Ganaderos.ObtenerPorIdAsync(ganaderoId.Value, cancellationToken);
            if (g is null)
            {
                throw new ValidacionException(CamposReportes.Ganadero, "El ganadero seleccionado no existe.");
            }
            textoGanadero = TextosBoleta.UnirNoVacios(" - ", g.Codigo, g.Nombre);
            claveFiltro = g.Codigo;
        }

        if (clienteId.HasValue)
        {
            var c = await uow.Clientes.ObtenerPorIdAsync(clienteId.Value, cancellationToken);
            if (c is null)
            {
                throw new ValidacionException(CamposReportes.Cliente, "El cliente seleccionado no existe.");
            }
            textoCliente = TextosBoleta.UnirNoVacios(" - ", c.Codigo.ToString(TextosBoleta.Cultura), c.Nombre);
            claveFiltro = c.Codigo.ToString(CultureInfo.InvariantCulture);
        }
        if (colorRaza is not null)
        {
            claveFiltro = colorRaza;
        }


        var zona = _timeProvider.LocalTimeZone;
        var rangoDesde = RangoDiaLocal.DelDia(filtro.Desde, zona);
        var rangoHasta = RangoDiaLocal.DelDia(hasta, zona);
        var criterios = new CriteriosReporte(rangoDesde.DesdeUtc, rangoHasta.HastaUtc, estado, ganaderoId, clienteId, colorRaza);

        var lista = await uow.Pesajes.ListarParaReporteAsync(criterios, MaximoFilas + 1, cancellationToken);
        if (lista.Count > MaximoFilas)
        {
            throw new ValidacionException(CamposReportes.FechaHasta, TextosReporte.MaximoSuperado);
        }

        var parametros = await uow.Parametros.ObtenerAsync(cancellationToken);
        string empresa = string.IsNullOrWhiteSpace(parametros.NombreEmpresa)
            ? TextosReporte.EmpresaPorDefecto
            : parametros.NombreEmpresa.Trim();
        var rutaLogo = _almacenLogo.ObtenerRutaCompleta(parametros.LogoPath);

        var filas = new List<FilaReporte>(lista.Count);
        for (int i = 0; i < lista.Count; i++)
        {
            var p = lista[i];
            var fechaLocal = DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(p.FechaHora, DateTimeKind.Utc), zona), DateTimeKind.Unspecified);

            DateTime? anuladoEnLocal = null;
            string? anuladoPor = null;
            string? motivo = null;

            if (estado == EstadoPesaje.Anulado)
            {
                if (p.AnuladoEn.HasValue)
                {
                    anuladoEnLocal = DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(p.AnuladoEn.Value, DateTimeKind.Utc), zona), DateTimeKind.Unspecified);
                }
                if (p.AnuladoPor is not null)
                {
                    anuladoPor = string.IsNullOrWhiteSpace(p.AnuladoPor.NombreCompleto)
                        ? p.AnuladoPor.NombreUsuario
                        : p.AnuladoPor.NombreCompleto;
                }
                motivo = p.MotivoAnulacion;
            }

            filas.Add(new FilaReporte(
                Item: i + 1,
                PesajeId: p.Id,
                NumeroBoleta: p.NumeroBoleta,
                FechaHoraLocal: fechaLocal,
                Secuencia: p.Secuencia,
                CodigoGanadero: p.Ganadero?.Codigo ?? string.Empty,
                NombreGanadero: p.Ganadero?.Nombre ?? string.Empty,
                CodigoCliente: p.Cliente?.Codigo ?? 0,
                NombreCliente: p.Cliente?.Nombre ?? string.Empty,
                Direccion: p.Direccion,
                TipoDesposte: p.TipoDesposte,
                PesoIzquierdo: p.PesoIzquierdo,
                PesoDerecho: p.PesoDerecho,
                PesoTotal: p.PesoTotal,
                Origen: p.OrigenPeso,
                AnuladoEnLocal: anuladoEnLocal,
                AnuladoPor: anuladoPor,
                MotivoAnulacion: motivo,
                NombreFletero: p.Fletero?.Nombre ?? string.Empty));
        }

        var totales = CalculadoraTotalesReporte.Calcular(filas);
        var ahoraUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var ahoraLocal = DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(ahoraUtc, zona), DateTimeKind.Unspecified);
        string generadoPor = string.IsNullOrWhiteSpace(sesion.NombreCompleto) ? sesion.NombreUsuario : sesion.NombreCompleto;

        var reporte = new ReportePesajes(
            Tipo: filtro.Tipo,
            Desde: filtro.Desde,
            Hasta: hasta,
            ClaveFiltro: claveFiltro,
            Empresa: empresa,
            RutaLogo: rutaLogo,
            Titulo: TextosReporte.Titulo(filtro.Tipo, colorRaza),
            Subtitulo: TextosReporte.Subtitulo(filtro.Tipo, filtro.Desde, hasta, textoGanadero, textoCliente, colorRaza),
            GeneradoPor: generadoPor,
            GeneradoEnLocal: ahoraLocal,
            Filas: filas.AsReadOnly(),
            Totales: totales,
            MostrarDesposte: ReglasDesposte.MostrarDesposte(ReglasDesposte.Efectivo(parametros.ModoDesposte), filas.Select(f => f.TipoDesposte)));

        _logger.LogInformation("Reporte {Tipo} generado: {Filas} boleta(s) del {Desde} al {Hasta} por el usuario {UsuarioId}.", filtro.Tipo, filas.Count, filtro.Desde, hasta, sesion.Id);

        return reporte;
    }
}
