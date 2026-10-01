using System.Globalization;
using Microsoft.Extensions.Logging;
using PesajeFaeneo.Application.Abstracciones.Persistencia;
using PesajeFaeneo.Application.Auditoria.Dtos;
using PesajeFaeneo.Application.Comun;
using PesajeFaeneo.Application.Pesajes;
using PesajeFaeneo.Application.Seguridad;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Domain.Excepciones;
using PesajeFaeneo.Domain.Reglas;
using AuditoriaEntidad = PesajeFaeneo.Domain.Entities.Auditoria;

namespace PesajeFaeneo.Application.Auditoria;

public class ServicioAuditoria : IServicioAuditoria
{
    private readonly IUnidadDeTrabajoFactory _uowFactory;
    private readonly ISesionActual _sesion;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ServicioAuditoria> _logger;

    public ServicioAuditoria(
        IUnidadDeTrabajoFactory uowFactory,
        ISesionActual sesion,
        TimeProvider timeProvider,
        ILogger<ServicioAuditoria> logger)
    {
        _uowFactory = uowFactory ?? throw new ArgumentNullException(nameof(uowFactory));
        _sesion = sesion ?? throw new ArgumentNullException(nameof(sesion));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<UsuarioFiltro>> ListarUsuariosAsync(CancellationToken cancellationToken = default)
    {
        _sesion.ExigirPermiso(Permiso.VerAuditoria);

        await using var uow = await _uowFactory.CrearAsync(cancellationToken);
        var usuarios = await uow.Usuarios.ListarAsync(incluirInactivos: true, cancellationToken);

        return usuarios
            .OrderBy(u => u.NombreUsuario, StringComparer.OrdinalIgnoreCase)
            .Select(u => new UsuarioFiltro(u.Id, u.NombreUsuario, u.NombreCompleto))
            .ToList();
    }

    public async Task<ResultadoAuditoria> ConsultarAsync(FiltroAuditoria filtro, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);
        _sesion.ExigirPermiso(Permiso.VerAuditoria);

        if (filtro.Desde.HasValue && filtro.Hasta.HasValue && filtro.Desde.Value > filtro.Hasta.Value)
        {
            throw new ValidacionException(CamposGestionPesajes.FechaHasta, "La fecha final no puede ser anterior a la inicial.");
        }

        if (filtro.NumeroBoleta.HasValue && filtro.NumeroBoleta.Value <= 0)
        {
            throw new ValidacionException(CamposGestionPesajes.NumeroBoleta, "Ingrese un número de boleta válido.");
        }

        DateTime? desdeUtc = null;
        DateTime? hastaUtc = null;
        var zona = _timeProvider.LocalTimeZone;

        if (filtro.Desde.HasValue)
        {
            desdeUtc = RangoDiaLocal.DelDia(filtro.Desde.Value, zona).DesdeUtc;
        }

        if (filtro.Hasta.HasValue)
        {
            hastaUtc = RangoDiaLocal.DelDia(filtro.Hasta.Value, zona).HastaUtc;
        }

        await using var uow = await _uowFactory.CrearAsync(cancellationToken);

        string? entidad = filtro.Entidad;
        string? entidadId = null;

        if (filtro.NumeroBoleta.HasValue)
        {
            long n = filtro.NumeroBoleta.Value;
            var p = await uow.Pesajes.ObtenerParaBoletaPorNumeroAsync(n, cancellationToken);
            if (p == null)
            {
                throw new ValidacionException(CamposGestionPesajes.NumeroBoleta, $"No existe la boleta N° {n}.");
            }

            entidad = EntidadesAuditoria.Pesaje;
            entidadId = p.Id.ToString(CultureInfo.InvariantCulture);
        }

        var criterios = new CriteriosAuditoria(
            DesdeUtc: desdeUtc,
            HastaUtc: hastaUtc,
            Entidad: entidad,
            Accion: filtro.Accion,
            UsuarioId: filtro.UsuarioId,
            EntidadId: entidadId);

        var registros = await uow.Auditoria.ConsultarAsync(criterios, ReglasGestionPesajes.AuditoriaMaximoFilas + 1, cancellationToken);
        bool truncado = registros.Count > ReglasGestionPesajes.AuditoriaMaximoFilas;
        var seleccionados = truncado ? registros.Take(ReglasGestionPesajes.AuditoriaMaximoFilas) : registros;

        var dtos = seleccionados.Select(a =>
        {
            DateTime local = TimeZoneInfo.ConvertTimeFromUtc(a.FechaHora, zona);
            DateTime localSinKind = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);

            string usuario = a.Usuario?.NombreUsuario ?? "(sistema)";
            string idEntidad = a.EntidadId;

            // D19: Enmascaramiento de LoginFallido sin usuario
            if (a.Accion == AccionAuditoria.LoginFallido && a.UsuarioId is null)
            {
                idEntidad = "(usuario no existente)";
            }

            return new RegistroAuditoria(
                Id: a.Id,
                FechaHoraLocal: localSinKind,
                Usuario: usuario,
                Accion: a.Accion,
                Entidad: a.Entidad,
                EntidadId: idEntidad,
                Motivo: a.Motivo,
                DatosAntesJson: a.DatosAntesJson,
                DatosDespuesJson: a.DatosDespuesJson);
        }).ToList();

        _logger.LogInformation(
            "Consulta de auditoría ejecutada por usuario {UsuarioId}. Registros devueltos: {Total}, Truncado: {Truncado}",
            _sesion.Usuario?.Id,
            dtos.Count,
            truncado);

        return new ResultadoAuditoria(dtos, truncado);
    }
}
